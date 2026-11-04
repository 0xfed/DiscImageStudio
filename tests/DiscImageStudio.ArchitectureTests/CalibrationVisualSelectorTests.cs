using System.Text.Json.Nodes;
using DiscImageStudio.Core;
using DiscImageStudio.Core.Calibration;

internal static class CalibrationVisualSelectorTests
{
    internal static void Run()
    {
        TestSnapshotAndLayout();
        TestPhaseAndSampling();
        TestCoarseLookup();
        TestPersistenceAndValidation();
        Console.WriteLine("calibration-visual-selector: passed (DVD snapshot, phase derivative, source/codeword parity, coarse mathematical lookup, range edges and legacy records; no physical visibility claim)");
    }

    private static CalibrationParameters Parameters() => new(CalibrationDiscKind.Dvd, 24, 58, 2295104, 133.3);
    private static CalibrationTarget Target(double range = 1) => CalibrationTarget.Create(Parameters(),
        new(VisualSelectorRangeMm: range), CalibrationPatternKind.VisualSelector);

    private static void TestSnapshotAndLayout()
    {
        CalibrationParameters original = Parameters() with { PitchLinear = .02, PitchQuadratic = -.01, PitchCubic = .003 };
        CalibrationTarget target = CalibrationTarget.Create(original, patternKind: CalibrationPatternKind.VisualSelector);
        Require(original.PitchLinear == .02 && target.Parameters == Parameters(), "only the saved selector snapshot uses zero pitch shape");
        Require(target.IsVisualSelectorPattern && target.VisualSelector is not null && target.SchemaVersion == 9,
            "explicit selector pattern identity and schema");
        Require(target.Groups.Count == 0 && target.ControlCells.Count == 0 && target.ControlPoints.Count == 0
            && target.RingRadiiMm.Count == 0, "selector bands cannot be mistaken for trace arms or measurable rings");
        CalibrationVisualSelector selector = target.VisualSelector!;
        Require(selector.Bands.Count == 42 && selector.StepMm == .1, "two complete numbered banks");
        foreach (string bank in new[] { "I", "O" })
        {
            var strips = selector.Bands.Where(b => b.Bank == bank).ToArray();
            Require(strips.Select(b => b.Index).SequenceEqual(Enumerable.Range(1, 21)), "indices run inside out in each bank");
            Require(strips[0].OffsetMm == -1 && strips[10].OffsetMm == 0 && strips[^1].OffsetMm == 1,
                "symmetric candidate offsets include an exact centre");
            foreach (var strip in strips)
            {
                Require(strip.GapMm > 0 && strip.InnerRadiusMm + strip.GapMm < strip.CenterRadiusMm
                    && strip.CenterRadiusMm < strip.OuterRadiusMm - strip.GapMm, "every strip has an active centre and countable gaps");
                Require(selector.BandAtProgress(strip.TrackProgress) == strip, "recording position retains band identity");
            }
        }
        Require(selector.BandAtProgress(0) is null && selector.BandAtProgress(1) is null
            && selector.SampleRecordingProgress(double.NaN) == 255 && target.Sample(0, 0) == 255,
            "unused and invalid positions remain blank");
        CalibrationVisualSelector smaller = new(Parameters() with { OuterRadiusMm = 38 }, .2);
        Require(smaller.Bands.All(b => b.InnerRadiusMm > 24 && b.OuterRadiusMm < 38), "bank layout scales to an 8 cm recording band");
    }

    private static void TestPhaseAndSampling()
    {
        CalibrationTarget target = Target();
        CalibrationVisualSelector selector = target.VisualSelector!;
        CalibrationParameters p = target.Parameters;
        DvdTrackGeometry geometry = DvdTrackGeometry.Create(p.InnerRadiusMm, p.OuterRadiusMm, p.TrackLengthMm);
        const double h = 1e-7;
        foreach (var strip in selector.Bands)
        {
            double q = strip.TrackProgress;
            double derivative = (selector.CandidatePhaseAtProgress(q + h) - selector.CandidatePhaseAtProgress(q - h)) / (2 * h);
            double expected = p.TrackLengthMm / strip.CandidateRadiusMm;
            Require(Math.Abs(derivative / expected - 1) < 1e-7,
                "candidate phase rate equals track length over its hypothesized local radius");
            for (int i = -10; i <= 10; i++)
            {
                double progress = q + i * 1e-6;
                double radius = geometry.RadiusAtArcLength(progress * p.TrackLengthMm);
                double angle = geometry.AngleAtRadius(radius);
                byte direct = selector.SampleRecordingProgress(progress);
                byte source = selector.SampleSource(radius * Math.Cos(angle), radius * Math.Sin(angle));
                Require(direct == source && source == target.Sample(radius * Math.Cos(angle), radius * Math.Sin(angle)),
                    "binary source and direct recording samplers agree on the encoder's +angle sample coordinates");
            }
        }
        // Centre strips encode the nominal angle and therefore retain a radial boundary.
        foreach (var strip in selector.Bands.Where(b => b.Index == 11))
        {
            foreach (double radius in new[] { strip.CenterRadiusMm - .1, strip.CenterRadiusMm, strip.CenterRadiusMm + .1 })
            {
                Require(selector.SampleSource(radius, 0) == 0 && selector.SampleSource(-radius, 0) == 255,
                    "zero-offset candidate stays coherent across its band");
                Require(selector.SamplePreview(radius, 0, 0) == selector.SampleSource(radius, 0), "zero footprint is exact source sampling");
            }
        }
    }

    private static void TestCoarseLookup()
    {
        CalibrationVisualSelector selector = Target().VisualSelector!;
        CalibrationVisualEstimate nominal = selector.Estimate(11, 11);
        Require(Math.Abs(nominal.Parameters.InnerRadiusMm - 24) < 1e-10
            && Math.Abs(nominal.Parameters.OuterRadiusMm - 58) < 1e-10 && !nominal.IsAtRangeEdge,
            "nominal centre choices recover the saved endpoints");
        Require(selector.Estimate(1, 11).IsAtRangeEdge && selector.Estimate(11, 21).IsAtRangeEdge,
            "boundary choices expose exhausted range rather than false precision");
        // Ground-truth selection only checks candidate quantization. It is not an image
        // recognizer and does not claim a human could select these bands on a real disc.
        foreach ((double ri, double ro) in new[] { (24.1, 57.85), (23.85, 58.25), (24.2, 57.6) })
        {
            int Pick(string bank) => selector.Bands.Where(b => b.Bank == bank)
                .MinBy(b => Math.Abs(Math.Sqrt(ri * ri + b.TrackProgress * (ro * ro - ri * ri)) - b.CandidateRadiusMm))!.Index;
            CalibrationVisualEstimate fit = selector.Estimate(Pick("I"), Pick("O"));
            Require(Math.Abs(fit.Parameters.InnerRadiusMm - ri) < .1 && Math.Abs(fit.Parameters.OuterRadiusMm - ro) < .1,
                "mathematical candidate choices produce coarse starting endpoints");
            Require(fit.Parameters.Sectors == Parameters().Sectors && fit.Parameters.LinearDensity == 133.3
                && fit.Parameters.PitchLinear == 0 && fit.Parameters.PitchQuadratic == 0 && fit.Parameters.PitchCubic == 0,
                "lookup preserves density and capacity and explicitly leaves pitch uniform");
        }
    }

    private static void TestPersistenceAndValidation()
    {
        CalibrationTarget target = Target(.2);
        var session = new CalibrationSession(target, [], VisualSelectorSelection: new(9, 13));
        string json = CalibrationSessionJson.Serialize(session);
        CalibrationSession restored = CalibrationSessionJson.Deserialize(json);
        Require(restored.Target.VisualSelector?.RangeMm == .2 && restored.VisualSelectorSelection == new VisualSelectorSelection(9, 13),
            "range, target and choices survive reopening");
        Require(!json.Contains("isAtRangeEdge", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("candidateRadiusMm", StringComparison.OrdinalIgnoreCase), "derived estimates and bands are not persisted as trusted results");
        foreach (int schema in new[] { 6, 7, 8 })
        {
            JsonNode forged = JsonNode.Parse(json)!;
            forged["target"]!["schemaVersion"] = schema;
            Throws(() => CalibrationSessionJson.Deserialize(forged.ToJsonString()));
        }
        foreach ((int schema, CalibrationPatternKind kind) in new[]
        {
            (6, CalibrationPatternKind.FineCross), (7, CalibrationPatternKind.FineCross),
            (7, CalibrationPatternKind.WideSectors), (8, CalibrationPatternKind.FineCross),
            (8, CalibrationPatternKind.WideSectors), (8, CalibrationPatternKind.ConcentricRings),
        })
        {
            JsonNode old = JsonNode.Parse(CalibrationSessionJson.Serialize(new(CalibrationTarget.Create(Parameters(), patternKind: kind), [])))!;
            old["target"]!["schemaVersion"] = schema;
            Require(CalibrationSessionJson.Deserialize(old.ToJsonString()).Target.Options.PatternKind == kind,
                "older fine, wide and ring targets retain their meanings");
        }
        Throws(() => target.VisualSelector!.Estimate(0, 10));
        Throws(() => target.VisualSelector!.Estimate(10, 22));
        Throws(() => new CalibrationVisualSelector(Parameters(), double.NaN));
        Throws(() => new CalibrationVisualSelector(Parameters(), 0));
        Throws(() => new CalibrationVisualSelector(Parameters(), 2.001));
        Throws(() => new CalibrationVisualSelector(Parameters() with { PitchLinear = .01 }));
        Throws(() => new CalibrationVisualSelector(Parameters() with { OuterRadiusMm = 61 }));
        Throws(() => new CalibrationVisualSelector(Parameters() with { Sectors = 1001 }));
        Throws(() => CalibrationTarget.Create(Parameters() with { Kind = CalibrationDiscKind.Cd }, patternKind: CalibrationPatternKind.VisualSelector));
        Throws(() => CalibrationSessionJson.Serialize(session with { VisualSelectorSelection = new(1, 22) }));
        Throws(() => CalibrationSessionJson.Serialize(session with { Target = CalibrationTarget.Create(Parameters()) }));
        Throws(() => CalibrationSessionJson.Serialize(session with { UseRingMeasurements = true }));
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Visual selector: " + message); }
    private static void Throws(Action action)
    { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected rejected visual selector input."); }
}
