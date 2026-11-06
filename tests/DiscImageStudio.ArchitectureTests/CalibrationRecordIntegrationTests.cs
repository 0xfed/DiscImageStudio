using System.Text.Json.Nodes;
using DiscImageStudio.Core.Calibration;

internal static class CalibrationRecordIntegrationTests
{
    internal static void Run()
    {
        CalibrationParameters parameters = new(CalibrationDiscKind.Dvd, 24, 58, 2295104, 133.3);
        foreach (CalibrationPatternKind kind in Enum.GetValues<CalibrationPatternKind>())
        {
            var target = CalibrationTarget.Create(parameters, patternKind: kind);
            JsonNode record = JsonNode.Parse(CalibrationSessionJson.Serialize(new(target, [])))!;
            record["target"]!["schemaVersion"] = kind == CalibrationPatternKind.ConcentricRings ? 8 : 9;
            record["target"]!.AsObject().Remove("artwork");
            var restored = CalibrationSessionJson.Deserialize(record.ToJsonString()).Target;
            Require(restored.Options.PatternKind == kind && restored.Artwork == CalibrationArtwork.LegacyReference,
                "records without an artwork discriminator retain the fork's pattern identity");
            for (double x = -55; x <= 55; x += 3)
                for (double y = -55; y <= 55; y += 3)
                    Require(target.Sample(x, y) == restored.Sample(x, y), "legacy recording samples are unchanged");
            Require(target.RingRadiiMm.SequenceEqual(restored.RingRadiiMm), "legacy ring identities are unchanged");
        }
        foreach (var artwork in new[] { CalibrationArtwork.SweptReference, CalibrationArtwork.ReferenceCross,
                     CalibrationArtwork.AngleSweepBands, CalibrationArtwork.WavyBoundary })
        {
            var target = CalibrationTarget.Create(parameters, artwork: artwork);
            string json = CalibrationSessionJson.Serialize(new(target, []));
            Require(JsonNode.Parse(json)!["target"]!["artwork"] is not null,
                "new records explicitly distinguish upstream artwork from legacy fork patterns");
            var restored = CalibrationSessionJson.Deserialize(json).Target;
            Require(restored.Artwork == artwork && restored.SchemaVersion == target.SchemaVersion
                && restored.VisibleRings.SequenceEqual(target.VisibleRings), "upstream records round-trip independently");
        }
        Console.WriteLine("calibration-record-integration: passed (fork v8/v9 samples, circle identities and explicit upstream artwork)");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Calibration record integration: " + message);
    }
}
