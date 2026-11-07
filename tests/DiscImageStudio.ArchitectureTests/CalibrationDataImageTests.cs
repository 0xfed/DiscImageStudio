using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using DiscImageStudio.Iso;

internal static class CalibrationDataImageTests
{
    internal static void Run()
    {
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        CalibrationDataImage.Write(first, 8192);
        CalibrationDataImage.Write(second, 8192);
        byte[] a = first.ToArray(), b = second.ToArray();
        Require(a.Length == 8192 * 2048, "exact volume length");
        Require(a.AsSpan(16 * 2048 + 1, 5).SequenceEqual("CD001"u8), "ISO9660 primary descriptor");
        Require(BinaryPrimitives.ReadUInt32LittleEndian(a.AsSpan(16 * 2048 + 80)) == 8192
            && BinaryPrimitives.ReadUInt32BigEndian(a.AsSpan(16 * 2048 + 84)) == 8192, "recorded volume sector count");
        Require(a.AsSpan(100 * 2048).SequenceEqual(b.AsSpan(100 * 2048)), "seeded measurement data are deterministic despite filesystem timestamps");
        Require(a.AsSpan(100 * 2048, 2048).ToArray().Distinct().Count() > 240
            && !a.AsSpan(100 * 2048, 2048).SequenceEqual(a.AsSpan(101 * 2048, 2048)), "plain data is not a repeated picture texture");
        using var different = new MemoryStream();
        CalibrationDataImage.Write(different, 8192, seed: 2);
        Require(!SHA256.HashData(a.AsSpan(100 * 2048)).SequenceEqual(SHA256.HashData(different.ToArray().AsSpan(100 * 2048))), "seed changes the measurement data");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var untouched = new MemoryStream();
        try { CalibrationDataImage.Write(untouched, 8192, cancellationToken: cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        Require(untouched.Length == 0, "cancelled preparation writes nothing");
        Console.WriteLine("calibration-data-image: passed (normal ISO headers, random data, capacity, deterministic seed and cancellation)");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Measurement ISO: " + message); }
}
