using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiscImageStudio.Core;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Dvd;
using DiscImageStudio.ImageIO;

internal static class CalibrationPatternTests
{
    internal static void Run()
    {
        foreach (CalibrationDiscKind kind in Enum.GetValues<CalibrationDiscKind>()) TestPatternAndPersistence(kind);
        TestAngularDrift();
        TestCoarseEdgeFit();
        TestEncodedPatternAndFixtures();
        Console.WriteLine("calibration-patterns: passed (wide sectors, constant-angle edges, no guide-ring ambiguity, legacy/current records, geometry/sector-offset contrast, edge fitting and encoded payload)");
    }

    private static CalibrationParameters Parameters(CalibrationDiscKind kind = CalibrationDiscKind.Dvd)
        => new(kind, 24, 58, kind == CalibrationDiscKind.Cd ? 359849 : kind == CalibrationDiscKind.Dvd ? 2295104 : 12219392,
            kind == CalibrationDiscKind.Cd ? 1200 : kind == CalibrationDiscKind.Dvd ? 133.3 : 74.5);

    private static void TestPatternAndPersistence(CalibrationDiscKind kind)
    {
        CalibrationTarget fine = CalibrationTarget.Create(Parameters(kind));
        CalibrationTarget coarse = CalibrationTarget.Create(Parameters(kind), patternKind: CalibrationPatternKind.WideSectors);
        Require(!fine.IsCoarsePattern && coarse.IsCoarsePattern && coarse.RingRadiiMm.Count == 0,
            "coarse mode is explicit and has no guide circles to confuse with sector edges");
        Require(coarse.ControlPoints.Count == fine.ControlPoints.Count && coarse.Groups.Count == 4,
            "sector edges retain the same four radial reference directions");
        double r = coarse.DrawingInnerRadiusMm + (coarse.DrawingOuterRadiusMm - coarse.DrawingInnerRadiusMm) * 0.125;
        Require(coarse.Sample(r / Math.Sqrt(2), r / Math.Sqrt(2)) == 0
            && coarse.Sample(-r / Math.Sqrt(2), r / Math.Sqrt(2)) == 255
            && coarse.Sample(-r / Math.Sqrt(2), -r / Math.Sqrt(2)) == 0
            && coarse.Sample(r / Math.Sqrt(2), -r / Math.Sqrt(2)) == 255, "alternating 90-degree sectors");
        Require(coarse.Sample(0, 0) == 255 && coarse.Sample(59, 0) == 255, "drawing annulus clips the new pattern");
        for (int angle = 0; angle < 360; angle += 90)
        {
            double a = angle * Math.PI / 180;
            Require(At(coarse, r, a - 0.001) != At(coarse, r, a + 0.001), "every reference arm is an actual texture boundary");
        }
        string json = CalibrationSessionJson.Serialize(new(coarse, []));
        CalibrationTarget loaded = CalibrationSessionJson.Deserialize(json).Target;
        Require(loaded.IsCoarsePattern && loaded.Options == coarse.Options
            && loaded.RingRadiiMm.SequenceEqual(coarse.RingRadiiMm), "saved records reproduce the same pattern");
        JsonNode legacy = JsonNode.Parse(CalibrationSessionJson.Serialize(new(fine, [])))!;
        legacy["target"]!["schemaVersion"] = CalibrationTarget.LegacyFineCrossSchemaVersion;
        CalibrationTarget old = CalibrationSessionJson.Deserialize(legacy.ToJsonString()).Target;
        Require(!old.IsCoarsePattern && old.Sample(r, 0) == fine.Sample(r, 0), "schema 6 cross records remain readable");
        JsonNode incompatible = JsonNode.Parse(json)!;
        incompatible["target"]!["schemaVersion"] = CalibrationTarget.LegacyFineCrossSchemaVersion;
        Throws<ArgumentException>(() => CalibrationSessionJson.Deserialize(incompatible.ToJsonString()));
        Throws<ArgumentException>(() => CalibrationTarget.Create(Parameters(kind), patternKind: (CalibrationPatternKind)99));
    }

    private static byte At(CalibrationTarget target, double radius, double angle)
        => target.Sample(radius * Math.Cos(angle), radius * Math.Sin(angle));

    private static void TestAngularDrift()
    {
        CalibrationTarget fine = CalibrationTarget.Create(Parameters());
        CalibrationTarget coarse = CalibrationTarget.Create(Parameters(), patternKind: CalibrationPatternKind.WideSectors);
        List<object> results = [];
        foreach ((double innerError, double offsetSectors) in new[] { (0.1, 0.0), (-0.1, 0.0), (0.0, 512.0), (0.0, -512.0) })
        {
            double fineContrast = Contrast(fine, innerError, offsetSectors);
            double coarseContrast = Contrast(coarse, innerError, offsetSectors);
            results.Add(new { innerErrorMm = innerError, offsetSectors, fineContrast, coarseContrast });
            Require(coarseContrast > 0.8 && coarseContrast > fineContrast * 4,
                $"coarse contrast survives the modelled error: inner={innerError}, offset={offsetSectors}, fine={fineContrast:F3}, coarse={coarseContrast:F3}");
        }
        Require(Contrast(coarse, 0, 0) == 1, "matching geometry retains full digital contrast");
        if (Environment.GetEnvironmentVariable("DISC_IMAGE_STUDIO_CALIBRATION_FIXTURES") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "contrast-simulation.json"), JsonSerializer.Serialize(new
            {
                modelOnly = true, observationRadiusMm = 37, radialFootprintMm = 0.1,
                note = "Digital geometry contrast; this is not a physical-disc reflectance measurement.", results,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    // Average a 0.1 mm radial footprint to model finite observation resolution.
    // This tests digital geometry visibility, not optical reflectance or a real burn.
    private static double Contrast(CalibrationTarget target, double innerError, double offsetSectors)
    {
        CalibrationParameters p = target.Parameters;
        DvdTrackGeometry generated = DvdTrackGeometry.Create(p.InnerRadiusMm, p.OuterRadiusMm, p.TrackLengthMm);
        DvdTrackGeometry actual = DvdTrackGeometry.Create(p.InnerRadiusMm + innerError, p.OuterRadiusMm, p.TrackLengthMm);
        const int samples = 128;
        var coordinates = new (double Radius, double Phase)[samples];
        for (int index = 0; index < samples; index++)
        {
            double radius = 37 + ((index + 0.5) / samples - 0.5) * 0.1;
            double progress = actual.ArcLengthAtRadius(radius) / p.TrackLengthMm - offsetSectors / p.Sectors;
            var source = generated.AtArcLengthFast(progress * p.TrackLengthMm);
            coordinates[index] = (source.Radius, source.Angle - actual.AngleAtRadius(radius));
        }
        double low = 1, high = 0;
        for (int index = 0; index < 720; index++)
        {
            double angle = 2 * Math.PI * index / 720, total = 0;
            foreach (var source in coordinates) total += At(target, source.Radius, angle + source.Phase) / 255.0;
            double value = total / samples;
            low = Math.Min(low, value); high = Math.Max(high, value);
        }
        return high - low;
    }

    private static void TestCoarseEdgeFit()
    {
        CalibrationTarget target = CalibrationTarget.Create(Parameters(), patternKind: CalibrationPatternKind.WideSectors);
        CalibrationParameters actual = target.Parameters with { InnerRadiusMm = 24.003, OuterRadiusMm = 57.998 };
        Func<CalibrationPoint, CalibrationPoint> map = target.CreateMapping(actual, 0.41);
        CalibrationPoint[] points = Enumerable.Range(0, 97).Select(index =>
        {
            double radius = 26 + 30.0 * index / 96;
            return map(new(0, -radius));
        }).ToArray();
        CalibrationFitResult fit = CalibrationCrossFitter.Fit(target, [new(0, points)], new(FitDvdPitch: false));
        Require(fit.Succeeded && fit.BestParameters is not null
            && Math.Abs(fit.BestParameters.InnerRadiusMm - actual.InnerRadiusMm) < 0.000001
            && Math.Abs(fit.BestParameters.OuterRadiusMm - actual.OuterRadiusMm) < 0.000001,
            "a continuous wide-sector boundary fits the existing radius/phase model");
    }

    private static void TestEncodedPatternAndFixtures()
    {
        CalibrationParameters p = Parameters() with { Sectors = 1024 };
        CalibrationTarget fine = CalibrationTarget.Create(p);
        CalibrationTarget coarse = CalibrationTarget.Create(p, patternKind: CalibrationPatternKind.WideSectors);
        DvdStreamingOptions options = new(1024, 24, 58, FastParallelism: 1);
        using MemoryStream first = new(), second = new();
        DvdStreamingGenerator.GeneratePattern(fine.Sample, first, options);
        DvdStreamingGenerator.GeneratePattern(coarse.Sample, second, options);
        Require(first.Length == options.ContentLength && second.Length == first.Length
            && !SHA256.HashData(first.ToArray()).SequenceEqual(SHA256.HashData(second.ToArray())),
            "the pattern option changes encoded calibration bytes without changing geometry or stream length");
        if (Environment.GetEnvironmentVariable("DISC_IMAGE_STUDIO_CALIBRATION_FIXTURES") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        CalibrationTarget full = CalibrationTarget.Create(Parameters(), patternKind: CalibrationPatternKind.WideSectors);
        const int size = CalibrationReferencePattern.PixelSize;
        byte[] pixels = new byte[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            CalibrationPoint point = CalibrationReferencePattern.PixelToDisc(x, y, full.Parameters.OuterRadiusMm, full.Parameters.Kind);
            pixels[y * size + x] = full.Sample(point.X, point.Y);
        }
        RasterCodec.SavePng(Path.Combine(folder, "wide-sectors-dvd.png"), size, size, pixels, grayscale: true);
        CalibrationSessionJson.Save(Path.Combine(folder, "wide-sectors-dvd.calibration.json"), new(full, []));
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Calibration patterns: " + message); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}
