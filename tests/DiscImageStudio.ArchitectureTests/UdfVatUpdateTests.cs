using System.Buffers.Binary;
using System.IO;
using DiscImageStudio.BluRay;
using DiscImageStudio.Iso;

internal static class UdfVatUpdateTests
{
    internal static void Run()
    {
        foreach (bool outer in new[] { false, true }) TestUpdates(outer);
        Console.WriteLine("udf-vat-updates: passed (UDF 2.50, inner/outer files, reserved capacity, add/remove/replace, chained VATs, immutable prior sectors, cancellation and invalid discs)");
    }

    private static void TestUpdates(bool outer)
    {
        string directory = Path.Combine(Path.GetTempPath(), "udf-vat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "remove.txt"), "old bytes remain recorded");
            File.WriteAllText(Path.Combine(directory, "replace.txt"), "before");
            BluRayIsoPlan first = BluRayIsoGenerator.Prepare(new(TotalSectors: 4096), directory,
                "VAT_蓝光", filesAtOuterRing: outer, updateReserveSectors: 3072);
            Require(first.RecordedSectors == 1024 && first.ContentLength == 2 * 1024 * 1024,
                "reserved update capacity is not written or included in the initial stream");
            using MemoryStream original = new();
            BluRayIsoGenerator.GenerateToStream(original, first, new Mapping());
            byte[] image = original.ToArray();
            string metadataPath = directory + ".iso";
            try
            {
                BluRayIsoMetadata metadata = BluRayIsoMetadata.FromPlan(first, directory + ".png", metadataPath,
                    false, DiscImageStudio.Core.ToneProfile.LegacyBinary, DateTimeOffset.UtcNow);
                metadata.Save(metadataPath);
                Require(BluRayIsoMetadata.TryLoad(metadataPath)?.ContentLength == image.Length,
                    "sidecars retain the shorter recording length and the full disc geometry");
            }
            finally { File.Delete(metadataPath + ".json"); }
            SaveFixture(outer, "initial", image);
            UdfImageWriter.SequentialSnapshot previous = Read(image);
            Require(previous.CapacitySectors == 4096 && previous.RecordedSectors == 1024,
                "immutable partition descriptor retains the full capacity");
            File.Delete(Path.Combine(directory, "remove.txt"));
            Directory.CreateDirectory(Path.Combine(directory, "新增"));
            File.WriteAllText(Path.Combine(directory, "新增", "add.txt"), "new content");
            File.WriteAllText(Path.Combine(directory, "replace.txt"), "after replacement");
            for (int update = 0; update < 3; update++)
            {
                byte[] before = image;
                UdfImageWriter.LayoutPlan plan = UdfImageWriter.PlanAppend(directory, previous, previous.RecordedSectors);
                using MemoryStream delta = new();
                UdfImageWriter.WriteAppend(plan, delta);
                Require(delta.Length == (long)(plan.VolumeSectors - plan.StartLba) * 2048,
                    "append stream contains only new sectors");
                image = [..before, ..delta.ToArray()];
                Require(image.AsSpan(0, before.Length).SequenceEqual(before), "no recorded sector is overwritten");
                Dictionary<string, byte[]> files = UdfFilesystemTests.Inspect(image, plan.VolumeSectors,
                    "VAT_蓝光", first.DrawingStartSector, first.DrawingEndSectorExclusive);
                Require(files.Count == 2 && !files.ContainsKey("remove.txt")
                    && System.Text.Encoding.UTF8.GetString(files["replace.txt"]) == "after replacement"
                    && System.Text.Encoding.UTF8.GetString(files["新增/add.txt"]) == "new content",
                    "the latest VAT exposes additions, replacements and removals");
                int vatEntry = checked((int)(plan.VolumeSectors - 1) * 2048);
                uint vatBlock = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(vatEntry + 180, 4));
                int vat = checked((int)(previous.PartitionStartLba + vatBlock) * 2048);
                Require(BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(vat + 132, 4)) == previous.VatIcb,
                    "each committed VAT points to its predecessor");
                UdfImageWriter.SequentialSnapshot current = Read(image);
                Require(current.LastUniqueId > previous.LastUniqueId, "unique IDs advance across updates");
                previous = current;
            }
            SaveFixture(outer, "updated", image);
            using CancellationTokenSource cancel = new();
            cancel.Cancel();
            using MemoryStream untouched = new();
            UdfImageWriter.LayoutPlan cancelled = UdfImageWriter.PlanAppend(directory, previous, previous.RecordedSectors);
            Throws<OperationCanceledException>(() => UdfImageWriter.WriteAppend(cancelled, untouched, cancel.Token));
            Require(untouched.Length == 0, "pre-cancellation writes no new sectors");
            Throws<ArgumentOutOfRangeException>(() => UdfImageWriter.PlanAppend(directory,
                previous with { CapacitySectors = previous.RecordedSectors }, previous.RecordedSectors));
            byte[] damaged = (byte[])image.Clone();
            damaged[damaged.Length - 2048 + 27] = 5;
            Throws<InvalidDataException>(() => Read(damaged));
            File.WriteAllText(Path.Combine(directory, "changed-after-planning.txt"), "stale");
            Throws<IOException>(() => UdfImageWriter.WriteAppend(cancelled, untouched));
            Require(untouched.Length == 0, "stale source trees fail before physical output");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static UdfImageWriter.SequentialSnapshot Read(byte[] image) => UdfImageWriter.ReadSequentialSnapshot(
        sector => image.AsSpan(checked((int)sector * 2048), 2048).ToArray(), checked((uint)(image.Length / 2048 - 1)));

    private static void SaveFixture(bool outer, string stage, byte[] image)
    {
        if (Environment.GetEnvironmentVariable("DISC_IMAGE_STUDIO_UDF_FIXTURES") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, $"{(outer ? "outer" : "inner")}-{stage}.iso"), image);
    }

    private sealed class Mapping : IBluRayPayloadMapping
    {
        public void FillScrambledPayload(BluRaySectorContext context, Span<byte> destination)
            => destination.Fill((byte)context.Sector);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("UDF VAT: " + message); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}
