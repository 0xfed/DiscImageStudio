using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DiscImageStudio.Burning;
using DiscImageStudio.Iso;

internal static class SequentialFileUpdateBurnTests
{
    internal static void Run()
    {
        dynamic flags = new System.Dynamic.ExpandoObject();
        WindowsImapiBurner.ConfigureDataFormat((object)flags, leaveOpen: true, udfCd: true);
        Require(!flags.ForceMediaToBeClosed && flags.DisableConsumerDvdCompatibilityMode
            && !flags.ForceOverwrite && !flags.BufferUnderrunFreeDisabled && flags.PostgapAlreadyInImage,
            "DVD+R compatibility mode is disabled so native Write leaves the disc open");
        foreach (OpticalBurnMediaKind kind in new[] { OpticalBurnMediaKind.CdData, OpticalBurnMediaKind.DvdData }) Test(kind);
        foreach ((int type, int profile) in new[] { (6, 0x1b), (9, 0x11) })
            WindowsImapiBurner.ValidateVatMedia(OpticalBurnMediaKind.DvdData, type, [profile]);
        foreach ((int type, int profile) in new[] { (5, 0x12), (7, 0x1a), (10, 0x14), (10, 0x13), (8, 0x2b), (6, 0x11) })
            Throws<InvalidOperationException>(() => WindowsImapiBurner.ValidateVatMedia(OpticalBurnMediaKind.DvdData, type, [profile]));
        Console.WriteLine("sequential-file-updates: passed (CD/DVD locked VAT read, new sessions, exact append LBAs, media/profile validation, capacity, cancellation and cleanup; fake recorders only)");
    }

    private static void Test(OpticalBurnMediaKind kind)
    {
        string folder = Path.Combine(Path.GetTempPath(), "native-vat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "old.txt"), "old");
            uint alignment = kind == OpticalBurnMediaKind.CdData ? 1u : 16u;
            UdfImageWriter.LayoutPlan first = UdfImageWriter.Plan(folder, 1024, "NATIVE", capacitySectors: 8192, blockAlignment: alignment);
            using MemoryStream initial = new();
            UdfImageWriter.WriteSequentialPrefix(first, initial, first.DrawingStartLba);
            initial.Write(new byte[(first.DrawingEndLbaExclusive - first.DrawingStartLba) * 2048]);
            UdfImageWriter.WriteSequentialSuffix(first, initial, first.DrawingEndLbaExclusive);
            byte[] before = initial.ToArray();
            if (alignment == 1) before = [..before, ..new byte[150 * 2048]];
            File.Delete(Path.Combine(folder, "old.txt"));
            File.WriteAllText(Path.Combine(folder, "new.txt"), "new bytes");
            Session session = new(before, alignment);
            OpticalBurnResult result = WindowsImapiBurner.BurnFileUpdate(session, "fake", kind, Prepare);
            Require(session.Writes == 1 && session.Releases == 1 && !session.Locked, "one write and paired release");
            Require(session.Image.AsSpan(0, before.Length).SequenceEqual(before), "old sectors are immutable");
            Dictionary<string, byte[]> files = UdfFilesystemTests.Inspect(session.Image,
                checked((uint)(session.Image.Length / 2048)), "NATIVE", first.DrawingStartLba, first.DrawingEndLbaExclusive);
            Require(files.Count == 1 && System.Text.Encoding.UTF8.GetString(files["new.txt"]) == "new bytes", "native append exposes replacement file list");
            Require(result.BytesWritten == session.WrittenBytes, "only newly supplied sectors are counted");
            foreach (int invalid in new[] { 2, 4 | 8, 4 | 0x1000, 4 | 0x4000, 4 | 0x2000 })
            {
                Session rejected = new(before, alignment); rejected.Media = rejected.Media with { Status = invalid };
                Throws<InvalidOperationException>(() => WindowsImapiBurner.BurnFileUpdate(rejected, "fake", kind, Prepare));
                Require(rejected.Writes == 0 && rejected.Releases == 1, "invalid media rejected under paired lock");
            }
            Session full = new(before, alignment); full.Media = full.Media with { FreeSectors = 1 };
            Throws<InvalidOperationException>(() => WindowsImapiBurner.BurnFileUpdate(full, "fake", kind, Prepare));
            Require(full.Writes == 0 && full.Releases == 1, "insufficient space never starts a write");
            Session failed = new(before, alignment) { FailWrite = true };
            Throws<IOException>(() => WindowsImapiBurner.BurnFileUpdate(failed, "fake", kind, Prepare));
            Require(failed.Writes == 1 && failed.Releases == 1, "write failure releases without retry");
            Session changed = new(before, alignment) { ChangeOnSecondRead = true };
            Throws<InvalidOperationException>(() => WindowsImapiBurner.BurnFileUpdate(changed, "fake", kind, Prepare));
            Require(changed.Writes == 0 && changed.Releases == 1, "changed media fails before writing");
            using CancellationTokenSource cancel = new(); cancel.Cancel();
            Session cancelled = new(before, alignment);
            Throws<OperationCanceledException>(() => WindowsImapiBurner.BurnFileUpdate(cancelled, "fake", kind, Prepare, cancellationToken: cancel.Token));
            Require(cancelled.Acquires == 0, "pre-cancellation never locks the drive");

            FileUpdateContent Prepare(FileUpdateContext context)
            {
                Require(session is not null && context.StartsNewSession && context.BlockAlignment == alignment, "native session context");
                UdfImageWriter.SequentialSnapshot previous = UdfImageWriter.ReadSequentialSnapshot(context.ReadSector, context.LastRecordedLba, context.BlockAlignment);
                UdfImageWriter.LayoutPlan plan = UdfImageWriter.PlanAppend(folder, previous, context.NextWritableLba, newSession: true);
                return new((long)(plan.VolumeSectors - plan.StartLba) * 2048, (output, token) => UdfImageWriter.WriteAppend(plan, output, token));
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    private sealed class Session : ISequentialFileUpdateSession
    {
        internal byte[] Image;
        internal SequentialFileMedia Media;
        internal int Acquires, Releases, Writes, Reads;
        internal bool Locked, FailWrite, ChangeOnSecondRead;
        internal long WrittenBytes;
        internal Session(byte[] image, uint alignment)
        {
            Image = image;
            uint nwa = checked((uint)(image.Length / 2048) + (alignment == 1 ? 17u : 32u));
            Media = new(4, nwa, checked((uint)(image.Length / 2048 - 1)), 8192, 8192 - nwa);
        }
        public void Acquire() { Acquires++; Locked = true; }
        public void Release() { Releases++; Locked = false; }
        public SequentialFileMedia ReadMedia()
        {
            Require(Locked, "media access under lock");
            return ChangeOnSecondRead && ++Reads > 1 ? Media with { NextWritableLba = Media.NextWritableLba + 16 } : Media;
        }
        public byte[] ReadSector(uint sector)
        { Require(Locked, "sector access under lock"); return Image.AsSpan(checked((int)sector * 2048), 2048).ToArray(); }
        public OpticalWriteSpeed ConfigureWrite(OpticalWriteSpeed? requested) => new(1000, false);
        public void CancelWrite() { }
        public void Write(IStream content)
        {
            Writes++;
            if (FailWrite) throw new IOException("fake write failure");
            using MemoryStream delta = new();
            byte[] buffer = new byte[65536];
            IntPtr count = Marshal.AllocHGlobal(sizeof(long));
            try
            {
                while (true)
                {
                    content.Read(buffer, buffer.Length, count);
                    int read = Marshal.ReadInt32(count);
                    if (read == 0) break;
                    delta.Write(buffer, 0, read);
                }
                WrittenBytes = delta.Length;
                Image = [..Image, ..new byte[checked((int)(Media.NextWritableLba * 2048 - Image.Length))], ..delta.ToArray()];
            }
            finally { Marshal.FreeHGlobal(count); }
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Sequential update: " + message); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}
