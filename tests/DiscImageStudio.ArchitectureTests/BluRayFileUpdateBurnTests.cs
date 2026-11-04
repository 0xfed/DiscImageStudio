using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DiscImageStudio.BluRay;
using DiscImageStudio.Burning;
using DiscImageStudio.Iso;

internal static class BluRayFileUpdateBurnTests
{
    internal static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vat-burn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "remove.txt"), "before");
            BluRayIsoPlan plan = BluRayIsoGenerator.Prepare(new(TotalSectors: 4096), directory,
                updateReserveSectors: 3072);
            using MemoryStream output = new();
            BluRayIsoGenerator.GenerateToStream(output, plan, new Mapping());
            byte[] before = output.ToArray();
            File.Delete(Path.Combine(directory, "remove.txt"));
            File.WriteAllText(Path.Combine(directory, "new.txt"), "after");
            using Session session = new(before);
            OpticalBurnResult result = WindowsImapiBurner.BurnBluRayAppend(session, "fake", Prepare);
            Require(session.AcquiredCount == 1 && session.ReleaseCount == 1 && session.FlushCount == 1
                && session.WriteCount == 1 && session.CloseCount == 0, "one locked append, flush and release without closing the disc");
            Require(session.Image.AsSpan(0, before.Length).SequenceEqual(before), "append never rewrites old sectors");
            Dictionary<string, byte[]> files = UdfFilesystemTests.Inspect(session.Image,
                checked((uint)(session.Image.Length / 2048)), "BD_IMAGE", plan.DrawingStartSector, plan.DrawingEndSectorExclusive);
            Require(files.Count == 1 && System.Text.Encoding.UTF8.GetString(files["new.txt"]) == "after",
                "a drive-backed snapshot and append expose the new file tree");
            Require(result.BytesWritten == session.Image.Length - before.Length, "result counts only appended bytes");

            using Session closed = new(before);
            closed.Media = closed.Media with { DiscIsClosed = true };
            Throws<InvalidOperationException>(() => WindowsImapiBurner.BurnBluRayAppend(closed, "fake", Prepare));
            Require(closed.WriteCount == 0 && closed.ReleaseCount == 1, "finalized discs are rejected before writing");
            using Session full = new(before);
            full.Media = full.Media with { FreeBlocks = 32 };
            Throws<InvalidOperationException>(() => WindowsImapiBurner.BurnBluRayAppend(full, "fake", Prepare));
            Require(full.WriteCount == 0 && full.ReleaseCount == 1, "insufficient physical capacity fails before writing");
            using Session noVat = new(new byte[before.Length]);
            Throws<InvalidDataException>(() => WindowsImapiBurner.BurnBluRayAppend(noVat, "fake", Prepare));
            Require(noVat.WriteCount == 0 && noVat.ReleaseCount == 1, "foreign and damaged volumes fail before writing");
            using Session interrupted = new(before) { FailWrite = true };
            Throws<IOException>(() => WindowsImapiBurner.BurnBluRayAppend(interrupted, "fake", Prepare));
            Require(interrupted.WriteCount == 1 && interrupted.FlushCount == 1 && interrupted.ReleaseCount == 1
                && interrupted.CloseCount == 0, "failed updates are flushed and released without retry or disc finalization");
            using Session cancelled = new(before);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Throws<OperationCanceledException>(() => WindowsImapiBurner.BurnBluRayAppend(cancelled,
                "fake", Prepare, cancellationToken: cancellation.Token));
            Require(cancelled.AcquiredCount == 0, "pre-cancellation does not access a device");

            BluRayAppendContent Prepare(BluRayAppendContext context)
            {
                UdfImageWriter.SequentialSnapshot snapshot = UdfImageWriter.ReadSequentialSnapshot(
                    context.ReadSector, context.LastRecordedLba);
                UdfImageWriter.LayoutPlan update = UdfImageWriter.PlanAppend(directory, snapshot, context.NextWritableLba);
                return new((long)(update.VolumeSectors - update.StartLba) * 2048,
                    (destination, token) => UdfImageWriter.WriteAppend(update, destination, token));
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
        Console.WriteLine("blu-ray-file-update-burn: passed (locked VAT read, exact append LBAs, add/remove files, open disc, capacity, cancellation and cleanup; fake recorder only)");
    }

    private sealed class Session(byte[] initial) : IBluRaySequentialSession
    {
        internal byte[] Image { get; private set; } = initial;
        internal BluRaySequentialMediaInfo Media { get; set; } = new(0x41, false, 2, false, true, 0,
            checked((uint)(initial.Length / 2048)), 12_219_392 - checked((uint)(initial.Length / 2048)),
            12_219_392, 12_219_392, 1, checked((uint)(initial.Length / 2048 - 1)));
        internal int AcquiredCount, ReleaseCount, FlushCount, WriteCount, CloseCount;
        internal bool FailWrite;
        private bool _locked;
        public void Acquire() { _locked = true; AcquiredCount++; }
        public BluRaySequentialMediaInfo ReadMedia() { Require(_locked, "media is queried under lock"); return Media; }
        public byte[] ReadSector(uint sector)
        { Require(_locked, "VAT sectors are read under lock"); return Image.AsSpan(checked((int)sector * 2048), 2048).ToArray(); }
        public OpticalWriteSpeed ConfigureWrite(OpticalWriteSpeed? requested) => new(4394, false);
        public void Write(IStream content, uint sectors) => throw new InvalidOperationException("Append must specify its start LBA.");
        public void WriteAppend(IStream content, uint sectors, uint start)
        {
            WriteCount++;
            Require(start == Image.Length / 2048 && start == Media.NextWritableAddress, "write starts at the current NWA");
            if (FailWrite) throw new IOException("fake write failure");
            using MemoryStream delta = new();
            byte[] buffer = new byte[65536];
            IntPtr count = Marshal.AllocHGlobal(sizeof(long));
            try
            {
                for (uint written = 0; written < sectors; written += 32)
                {
                    content.Read(buffer, buffer.Length, count);
                    Require(Marshal.ReadInt32(count) == buffer.Length, "producer supplies complete recording clusters");
                    delta.Write(buffer);
                }
                Image = [..Image, ..delta.ToArray()];
            }
            finally { Marshal.FreeHGlobal(count); }
        }
        public void Flush() { FlushCount++; }
        public void CloseTrack(int track) { CloseCount++; }
        public void CloseDisc() { CloseCount++; }
        public void Release() { _locked = false; ReleaseCount++; }
        public void CancelWrite() { }
        public void Dispose() { }
    }
    private sealed class Mapping : IBluRayPayloadMapping
    { public void FillScrambledPayload(BluRaySectorContext context, Span<byte> destination) => destination.Fill(0x51); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("VAT burn: " + message); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}
