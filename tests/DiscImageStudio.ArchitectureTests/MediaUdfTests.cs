using System.IO;
using System.Buffers.Binary;
using System.Text;
using DiscImageStudio.Cd;
using DiscImageStudio.Core;
using DiscImageStudio.Dvd;
using DiscImageStudio.ImageIO;
using DiscImageStudio.Iso;

internal static class MediaUdfTests
{
    internal static void Run()
    {
        foreach (bool cd in new[] { true, false })
            foreach (bool outer in new[] { false, true }) Test(cd, outer);
        Console.WriteLine("media-udf: passed (CD/DVD generators, unaligned CD capacity, DVD ECC alignment, both file placements, three new sessions, postgaps, add/remove/replace, immutable drawing)");
    }

    private static void Test(bool cd, bool outer)
    {
        string root = Path.Combine(Path.GetTempPath(), "media-udf-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "files");
        Directory.CreateDirectory(source);
        try
        {
            File.WriteAllText(Path.Combine(source, "remove.txt"), "original");
            File.WriteAllText(Path.Combine(source, "replace.txt"), "before");
            DateTime originalTimestamp = File.GetLastWriteTimeUtc(Path.Combine(source, "replace.txt"));
            byte[] retained = Enumerable.Range(0, 512 * 1024).Select(i => (byte)(i * 31)).ToArray();
            File.WriteAllBytes(Path.Combine(source, "keep.bin"), retained);
            string picture = Path.Combine(root, "image.png");
            byte[] pixels = Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray();
            RasterCodec.SavePng(picture, 16, 16, pixels, grayscale: false);
            uint capacity = cd ? 8193u : 8192u, recorded = cd ? 1025u : 1024u, alignment = cd ? 1u : 16u;
            uint drawingStart, drawingEnd;
            using MemoryStream output = new();
            if (cd)
            {
                CdDiscParameters parameters = new(24.5, 56.8, capacity);
                CdDataLayoutPlan plan = CdDataImageGenerator.PrepareLayout(source, parameters, "MEDIA_VAT", outer,
                    useUdf250: true, updateReserveSectors: capacity - recorded);
                Require(plan.TotalSectors == capacity && plan.RecordedSectors == recorded, "CD capacity and written length differ");
                CdDataImageGenerator.GenerateToStream(picture, output, parameters, plan);
                drawingStart = plan.DrawingStartLba; drawingEnd = plan.DrawingEndLbaExclusive;
            }
            else
            {
                DvdStreamingOptions options = new(capacity, 24, 58, FastParallelism: 1);
                DvdHybridStreamingPlan plan = DvdStreamingGenerator.PrepareHybrid(source, options, "MEDIA_VAT", outer,
                    useUdf250: true, updateReserveSectors: capacity - recorded);
                Require(plan.TotalSectors == capacity && plan.ContentLength == recorded * 2048L, "DVD reserve stays unrecorded");
                Require(plan.DrawingStartLba % 16 == 0 && plan.DrawingEndLbaExclusive % 16 == 0, "DVD drawing uses complete ECC blocks");
                DvdStreamingGenerator.GenerateHybrid(picture, output, options, plan);
                drawingStart = plan.DrawingStartLba; drawingEnd = plan.DrawingEndLbaExclusive;
            }
            byte[] image = output.ToArray();
            Require(image.Length == recorded * 2048L, "exact initial recording length");
            Require(UdfFilesystemTests.Inspect(image, recorded, "MEDIA_VAT", drawingStart, drawingEnd).Count == 3,
                "generated initial filesystem independently readable");
            Save("initial", image);
            string export = Path.Combine(root, "export.iso");
            IOpticalDiscModule module = cd ? new CdDiscModule() : new DvdDiscModule();
            string command = cd ? "cd-generate" : "solve";
            string[] arguments = cd
                ? ["--input", picture, "--output", export, "--data-dir", source, "--sectors", recorded.ToString(),
                    "--volume-label", "MEDIA_VAT", "--file-position", outer ? "outer" : "inner", "--file-system", "udf250"]
                : ["--image", picture, "--iso-output", export, "--data-dir", source, "--total-sectors", recorded.ToString(),
                    "--volume-label", "MEDIA_VAT", "--file-position", outer ? "outer" : "inner", "--file-system", "udf250"];
            TextWriter console = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                DiscJobResult result = module.ExecuteAsync(new DiscJobRequest(command, arguments)).GetAwaiter().GetResult();
                Require(result.Succeeded, "CLI file-system option routes to UDF generation");
            }
            finally { Console.SetOut(console); }
            Require(UdfFilesystemTests.Inspect(File.ReadAllBytes(export), recorded, "MEDIA_VAT").Count == 3,
                "CLI exports contain readable VAT files and complete tail metadata");
            Require(cd ? CdDataImageMetadata.TryLoad(export)?.UseUdf250 == true : DvdImageMetadata.TryLoad(export)?.UseUdf250 == true,
                "artifact sidecars retain the selected filesystem");

            string typedExport = Path.Combine(root, "typed.iso");
            if (cd)
                CdImageGenerator.Generate(new(picture, typedExport, new(24.5, 56.8, recorded),
                    ToneProfile.LegacyBinary, DataDirectory: source, VolumeLabel: "MEDIA_VAT",
                    FilesAtOuterRing: outer, UseUdf250: true));
            else
                DvdImageGenerator.Generate(new(picture, typedExport, new(recorded, 24, 58, FastParallelism: 1),
                    source, "MEDIA_VAT", outer, UseUdf250: true));
            Require(UdfFilesystemTests.Inspect(File.ReadAllBytes(typedExport), recorded, "MEDIA_VAT").Count == 3,
                "typed GUI exports preserve the UDF selection and readable file tree");
            Require(cd ? CdDataImageMetadata.TryLoad(typedExport)?.UseUdf250 == true
                : DvdImageMetadata.TryLoad(typedExport)?.UseUdf250 == true,
                "typed GUI export sidecars preserve UDF for later file updates");

            File.Delete(Path.Combine(source, "remove.txt"));
            Directory.CreateDirectory(Path.Combine(source, "新增"));
            File.WriteAllText(Path.Combine(source, "新增", "add.txt"), "added");
            File.WriteAllText(Path.Combine(source, "replace.txt"), "after!");
            File.SetLastWriteTimeUtc(Path.Combine(source, "replace.txt"), originalTimestamp);
            int batchReads = 0;
            for (int update = 0; update < 3; update++)
            {
                // Native CD recording adds a 150-sector zero postgap. A later
                // session starts beyond that gap; DVD sessions also have overhead.
                if (cd) image = [..image, ..new byte[150 * 2048]];
                uint last = checked((uint)(image.Length / 2048 - 1));
                UdfImageWriter.SequentialSnapshot previous = UdfImageWriter.ReadSequentialSnapshot(Read, last, alignment, ReadMany);
                uint nwa = checked((uint)(image.Length / 2048) + (cd ? 17u : 32u));
                byte[] before = image;
                UdfImageWriter.LayoutPlan plan = UdfImageWriter.PlanAppend(source, previous, nwa, newSession: true);
                using MemoryStream delta = new();
                UdfImageWriter.WriteAppend(plan, delta);
                Require(delta.Length < 700 * 2048L, "unchanged file sectors are reused rather than recopied");
                image = [..image, ..new byte[checked((int)(nwa * 2048 - image.Length))], ..delta.ToArray()];
                Require(image.AsSpan(0, before.Length).SequenceEqual(before), "old data and drawing stay byte-identical");
                Require(Encoding.ASCII.GetString(image, checked((int)(nwa + 17) * 2048 + 1), 5) == "NSR03", "each session has its own VRS");
                int anchor = checked((int)(nwa + 256) * 2048);
                Require(BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(anchor + 20)) == nwa + 32, "session anchor points at copied VDS");
                Dictionary<string, byte[]> files = UdfFilesystemTests.Inspect(image, plan.VolumeSectors,
                    "MEDIA_VAT", drawingStart, drawingEnd);
                Require(files.Count == 3 && files["keep.bin"].SequenceEqual(retained)
                    && Encoding.UTF8.GetString(files["replace.txt"]) == "after!"
                    && Encoding.UTF8.GetString(files["新增/add.txt"]) == "added", "new VAT exposes the complete updated file tree");
            }
            Require(batchReads >= 4, "unchanged files are verified using bounded multi-sector reads");
            Save("updated", image);
            byte[] Read(uint sector) => image.AsSpan(checked((int)sector * 2048), 2048).ToArray();
            byte[] ReadMany(uint sector, ushort blocks)
            { batchReads++; return image.AsSpan(checked((int)sector * 2048), blocks * 2048).ToArray(); }
            void Save(string stage, byte[] bytes)
            {
                if (Environment.GetEnvironmentVariable("DISC_IMAGE_STUDIO_UDF_FIXTURES") is not { Length: > 0 } fixtures) return;
                Directory.CreateDirectory(fixtures);
                File.WriteAllBytes(Path.Combine(fixtures, $"{(cd ? "cd" : "dvd")}-{(outer ? "outer" : "inner")}-{stage}.iso"), bytes);
            }
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Media UDF: " + message); }
}
