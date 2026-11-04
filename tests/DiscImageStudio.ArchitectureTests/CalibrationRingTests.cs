using System.IO;
using System.Text.Json.Nodes;
using DiscImageStudio.Core;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Dvd;
using DiscImageStudio.ImageIO;

internal static class CalibrationRingTests
{
    internal static void Run()
    {
        foreach (CalibrationDiscKind kind in Enum.GetValues<CalibrationDiscKind>())
        {
            CalibrationParameters p = Parameters(kind);
            CalibrationTarget target = CalibrationTarget.Create(p, patternKind: CalibrationPatternKind.ConcentricRings);
            var rings = CalibrationRingFitter.GetRings(target);
            Require(target.IsRingPattern && rings.Count == 7 && target.Groups.Count == 0
                && target.ControlPoints.Count == 0, "seven complete circles without cross controls");
            foreach (CalibrationRing ring in rings)
                for (int angle = 0; angle < 360; angle += 7)
                {
                    double a = angle * Math.PI / 180;
                    Require(target.Sample(ring.SourceRadiusMm * Math.Cos(a), ring.SourceRadiusMm * Math.Sin(a)) == 0,
                        "ring sampling is independent of angular phase");
                }
            Require(target.Sample(0, 0) == 255 && target.Sample(59, 0) == 255
                && target.Sample((rings[0].SourceRadiusMm + rings[1].SourceRadiusMm) / 2, 0) == 255,
                "clear center and spaces, without a cross");
            foreach ((double innerError, double outerError) in new[] { (.1, -.08), (2.0, -1.0), (-2.0, 1.0), (.5, .5) })
                TestRecovery(target, p with { InnerRadiusMm = p.InnerRadiusMm + innerError, OuterRadiusMm = p.OuterRadiusMm + outerError });
            TestRecovery(CalibrationTarget.Create(p), p with { InnerRadiusMm = 24.1, OuterRadiusMm = 57.9 });
            TestRecordsAndErrors(target);
        }
        CalibrationParameters shaped = Parameters(CalibrationDiscKind.Dvd) with { PitchLinear = .15, PitchQuadratic = -.10, PitchCubic = .02 };
        TestRecovery(CalibrationTarget.Create(shaped, patternKind: CalibrationPatternKind.ConcentricRings),
            shaped with { InnerRadiusMm = 25, OuterRadiusMm = 57 });
        TestEncodedOutput();
        WriteFixtures();
        Console.WriteLine("calibration-rings: passed (seven phase-independent circles, legacy guides, exact/rounded diameters, ±2 mm radius mismatch, saved pitch shape, residuals, validation and records)");
    }

    private static CalibrationParameters Parameters(CalibrationDiscKind kind) => new(kind, 24, 58,
        kind == CalibrationDiscKind.Cd ? 359849 : kind == CalibrationDiscKind.Dvd ? 2295104 : 12219392,
        kind == CalibrationDiscKind.Cd ? 1200 : kind == CalibrationDiscKind.Dvd ? 133.3 : 74.5);

    internal static List<CalibrationRingMeasurement> Measurements(CalibrationTarget target, CalibrationParameters actual)
    {
        var geometry = DvdTrackGeometry.Create(actual.InnerRadiusMm, actual.OuterRadiusMm, actual.TrackLengthMm,
            actual.PitchLinear, actual.PitchQuadratic, actual.PitchCubic);
        return CalibrationRingFitter.GetRings(target).Select(ring => new CalibrationRingMeasurement(ring.Index,
            2 * geometry.RadiusAtArcLength(ring.TrackProgress * actual.TrackLengthMm))).ToList();
    }

    private static void TestRecovery(CalibrationTarget target, CalibrationParameters actual)
    {
        var measurements = Measurements(target, actual);
        CalibrationRingFitResult fit = CalibrationRingFitter.Fit(target, measurements);
        Require(Math.Abs(fit.Parameters.InnerRadiusMm - actual.InnerRadiusMm) < 1e-6
            && Math.Abs(fit.Parameters.OuterRadiusMm - actual.OuterRadiusMm) < 1e-6
            && fit.MaxDiameterErrorMm < 1e-6, "known diameters recover both radial endpoints");
        Require(fit.Parameters with { InnerRadiusMm = actual.InnerRadiusMm, OuterRadiusMm = actual.OuterRadiusMm } == actual,
            "the fit preserves saved density, sectors, polarity, range, timestamp and pitch shape");
        var rounded = measurements.Select(value => value with { DiameterMm = Math.Round(value.DiameterMm, 2) }).ToList();
        CalibrationRingFitResult realistic = CalibrationRingFitter.Fit(target, rounded);
        Require(realistic.IsWithinTolerance(.01) && Math.Abs(realistic.Parameters.InnerRadiusMm - actual.InnerRadiusMm) < .02
            && Math.Abs(realistic.Parameters.OuterRadiusMm - actual.OuterRadiusMm) < .02,
            "rounded diameters remain useful without claiming physical accuracy");
        measurements[measurements.Count / 2] = measurements[measurements.Count / 2] with
        { DiameterMm = measurements[measurements.Count / 2].DiameterMm + .5 };
        Require(!CalibrationRingFitter.Fit(target, measurements).IsWithinTolerance(.1), "inconsistent diameters expose a large residual");
    }

    private static void TestRecordsAndErrors(CalibrationTarget target)
    {
        var values = Measurements(target, target.Parameters);
        var session = new CalibrationSession(target, [], RingMeasurements: values, UseRingMeasurements: true, RingDiameterToleranceMm: .1);
        string json = CalibrationSessionJson.Serialize(session);
        var restored = CalibrationSessionJson.Deserialize(json);
        Require(restored.Target.IsRingPattern && restored.UseRingMeasurements
            && restored.RingMeasurements!.SequenceEqual(values) && restored.RingDiameterToleranceMm == .1,
            "the durable record keeps target, diameters, method and tolerance");
        Throws(() => CalibrationRingFitter.Fit(target, values.Take(2).ToArray()));
        Throws(() => CalibrationRingFitter.Fit(target, values.Take(3).ToArray()));
        Throws(() => CalibrationRingFitter.Fit(target, [values[0], values[0], values[^1]]));
        Throws(() => CalibrationRingFitter.Fit(target, [values[0] with { DiameterMm = double.NaN }, values[3], values[^1]]));
        Throws(() => CalibrationRingFitter.Fit(target, [values[0] with { DiameterMm = values[^1].DiameterMm }, values[3], values[^1]]));
        Throws(() => CalibrationSessionJson.Serialize(session with { RingDiameterToleranceMm = -1 }));
        JsonNode old = JsonNode.Parse(json)!;
        old["target"]!["schemaVersion"] = CalibrationTarget.LegacyPatternSchemaVersion;
        Throws(() => CalibrationSessionJson.Deserialize(old.ToJsonString()));
        foreach (CalibrationPatternKind kind in new[] { CalibrationPatternKind.FineCross, CalibrationPatternKind.WideSectors })
        {
            var legacy = CalibrationTarget.Create(target.Parameters, patternKind: kind);
            JsonNode record = JsonNode.Parse(CalibrationSessionJson.Serialize(new(legacy, [])))!;
            record["target"]!["schemaVersion"] = CalibrationTarget.LegacyPatternSchemaVersion;
            Require(CalibrationSessionJson.Deserialize(record.ToJsonString()).Target.Options.PatternKind == kind,
                "schema 7 fine and wide records remain readable");
        }
    }

    private static void TestEncodedOutput()
    {
        var p = Parameters(CalibrationDiscKind.Dvd) with { Sectors = 1024 };
        var target = CalibrationTarget.Create(p, patternKind: CalibrationPatternKind.ConcentricRings);
        var options = new DvdStreamingOptions(1024, 24, 58, FastParallelism: 1);
        using MemoryStream output = new();
        DvdStreamingGenerator.GeneratePattern(target.Sample, output, options);
        Require(output.Length == options.ContentLength, "complete encoded circle-pattern payload");
    }

    private static void WriteFixtures()
    {
        if (Environment.GetEnvironmentVariable("DISC_IMAGE_STUDIO_CALIBRATION_FIXTURES") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        var target = CalibrationTarget.Create(Parameters(CalibrationDiscKind.Dvd), patternKind: CalibrationPatternKind.ConcentricRings);
        const int size = CalibrationReferencePattern.PixelSize;
        byte[] pixels = new byte[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var point = CalibrationReferencePattern.PixelToDisc(x, y, target.Parameters.OuterRadiusMm, target.Parameters.Kind);
            pixels[y * size + x] = target.Sample(point.X, point.Y);
        }
        RasterCodec.SavePng(Path.Combine(folder, "concentric-rings-dvd.png"), size, size, pixels, grayscale: true);
        CalibrationSessionJson.Save(Path.Combine(folder, "concentric-rings-dvd.calibration.json"),
            new(target, [], UseRingMeasurements: true, RingDiameterToleranceMm: .1));
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Calibration rings: " + message); }
    private static void Throws(Action action)
    { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected rejected ring input."); }
}
