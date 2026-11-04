using System.IO;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Dvd;

internal static class CalibrationVisualEncodingTests
{
    internal static void Run()
    {
        var parameters = new CalibrationParameters(CalibrationDiscKind.Dvd, 24, 58, 1024, 133.3);
        var target = CalibrationTarget.Create(parameters, patternKind: CalibrationPatternKind.VisualSelector);
        using MemoryStream serial = new(), parallel = new();
        var updates = new List<DvdStreamingProgress>();
        DvdStreamingSummary summary = DvdStreamingGenerator.GenerateCalibrationPattern(target, serial, updates.Add, fastParallelism: 1);
        DvdStreamingGenerator.GenerateCalibrationPattern(target, parallel, fastParallelism: 2);
        byte[] bytes = serial.ToArray();
        Require(bytes.SequenceEqual(parallel.ToArray()), "parallel and serial visual payloads match");
        Require(bytes.Length == 1024 * 2048 && summary.BytesWritten == bytes.Length && summary.ControlledWords == bytes.Length,
            "whole captured data area is encoded");
        Require(updates.Count == 64 && updates[^1].CompletedBytes == bytes.Length && updates[^1].Fraction == 1,
            "progress covers complete sectors");

        // Independently locate payload slots and remove the physical-sector scrambler.
        // This catches sector/word-centre offsets in the specialized generator.
        var seen = new HashSet<byte>();
        for (int sector = 0; sector < 1024; sector++)
        {
            byte[] mask = Scrambler(0x30000u + (uint)sector);
            for (int index = 0; index < 2048; index++)
            {
                int frameByte = 12 + index, row = frameByte / 172, column = frameByte % 172;
                double center = sector * 38688d + (row * 2 + column / 91) * 1488 + 32 + (column % 91) * 16 + 8;
                byte gray = target.VisualSelector!.SampleRecordingProgress(center / (1024 * 38688d));
                byte expected = gray < 128 ? (byte)0xA5 : (byte)0x92;
                byte actual = (byte)(bytes[sector * 2048 + index] ^ mask[index]);
                Require(actual == expected, "recorded bytes use the exact codeword-centre selector phase");
                seen.Add(actual);
            }
        }
        Require(seen.SetEquals(new byte[] { 0xA5, 0x92 }), "both DVD texture levels are generated");

        // Existing patterns must retain their original bytes through the shared entry point.
        var ordinary = CalibrationTarget.Create(parameters with { Sectors = 16 }, patternKind: CalibrationPatternKind.WideSectors);
        using MemoryStream oldOutput = new(), newOutput = new();
        DvdStreamingGenerator.GeneratePattern(ordinary.Sample, oldOutput, new(16, 24, 58, FastParallelism: 1));
        DvdStreamingGenerator.GenerateCalibrationPattern(ordinary, newOutput, fastParallelism: 1);
        Require(oldOutput.ToArray().SequenceEqual(newOutput.ToArray()), "existing calibration patterns keep byte parity");

        using MemoryStream cancelled = new();
        using var stop = new CancellationTokenSource(); stop.Cancel();
        try
        {
            DvdStreamingGenerator.GenerateCalibrationPattern(target, cancelled, cancellationToken: stop.Token);
            throw new InvalidOperationException("Cancellation was ignored.");
        }
        catch (OperationCanceledException) { Require(cancelled.Length == 0, "pre-cancelled generation writes nothing"); }
        Console.WriteLine("calibration-visual-encoding: passed (independent payload slots/scrambler, parallel parity, legacy bytes and cancellation; no optical validation)");
    }

    private static byte[] Scrambler(uint psn)
    {
        ushort[] seeds = [1, 0x5500, 2, 0x2A00, 4, 0x5400, 8, 0x2800, 0x10, 0x5000, 0x20, 0x2001, 0x40, 0x4002, 0x80, 5];
        int state = seeds[(psn >> 4) & 15];
        byte[] mask = new byte[2048];
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = (byte)state;
            for (int bit = 0; bit < 8; bit++)
                state = ((state << 1) | (((state >> 14) ^ (state >> 10)) & 1)) & 0x7FFF;
        }
        return mask;
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Visual selector encoding: " + message); }
}
