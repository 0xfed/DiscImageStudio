using System.Buffers.Binary;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Iso;

/// <summary>An ordinary, readable ISO volume with pseudorandom unused sectors for timing measurements.</summary>
public static class CalibrationDataImage
{
    public static void Write(Stream output, uint sectors, ulong seed = 1,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (sectors is < 8192 or > 3_000_000) throw new ArgumentOutOfRangeException(nameof(sectors), L.T("Timing.InvalidCapacity"));
        var plan = Iso9660ImageWriter.Plan(null, sectors, "DISC_TIMING");
        Iso9660ImageWriter.WriteSequentialPrefix(plan, output, plan.DataEndLbaExclusive, cancellationToken);
        long written = (long)plan.DataEndLbaExclusive * 2048, total = (long)sectors * 2048;
        byte[] buffer = new byte[1024 * 1024];
        ulong state = seed;
        while (written < total)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(buffer.Length, total - written);
            for (int i = 0; i < count; i += 8)
            {
                // SplitMix64: stable seeded bytes, no runtime- or platform-specific Random implementation.
                state += 0x9e3779b97f4a7c15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
                z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
                BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(i, 8), z ^ (z >> 31));
            }
            output.Write(buffer, 0, count);
            written += count;
            progress?.Report(written / (double)total);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
