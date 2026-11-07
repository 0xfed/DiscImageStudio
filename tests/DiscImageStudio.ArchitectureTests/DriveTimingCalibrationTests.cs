using DiscImageStudio.Core;
using DiscImageStudio.Core.Calibration;

internal static class DriveTimingCalibrationTests
{
    internal static void Run()
    {
        foreach (var media in new[] { DriveTimingMedia.Dvd, DriveTimingMedia.Cd })
        {
            foreach (var strategy in Enum.GetValues<DriveTimingCacheStrategy>())
            {
                var drive = new SimulatedDrive(media, strategy);
                var result = DriveTimingCalibrator.Measure(drive, new(16, media == DriveTimingMedia.Dvd ? 18 : 28, 32, 12, 60));
                result.Validate();
                Require(result.CacheStrategy == strategy, "cache strategy: " + strategy);
                double[] differences = Enumerable.Range(0, 400).Select(i =>
                {
                    double bits = DriveTimingGeometry.ReadEndBits(media, (uint)(drive.Disc.RecordedSectors * (.03 + .82 * i / 399)));
                    return (DriveTimingGeometry.Angle(bits, result.U, result.V) - drive.Angle(bits)) * 180 / Math.PI;
                }).ToArray();
                Require(differences.Max() - differences.Min() < 1.5, "off-nominal geometry recovered without using nominal radii");
                Require(result.RequiresFineCalibration == (media == DriveTimingMedia.Cd), "CD timing origin remains explicit");
                var parameters = result.ToParameters(drive.Disc.RecordedSectors);
                var geometry = DvdTrackGeometry.Create(parameters.InnerRadiusMm, parameters.OuterRadiusMm, parameters.TrackLengthMm);
                double bitsPerSector = media == DriveTimingMedia.Cd ? 57624 : 38688;
                double bit = result.AssumedBitLengthMm;
                double error = Enumerable.Range(0, 101).Select(i =>
                {
                    double n = drive.Disc.RecordedSectors * bitsPerSector * i / 100;
                    return Math.Abs(geometry.AngleAtArcLength(n * bit) - DriveTimingGeometry.Angle(n, result.U, result.V)) * 180 / Math.PI;
                }).Max();
                Require(error < .005, "native preset conversion matches the measured angle map");
                string path = Path.Combine(Path.GetTempPath(), "drive-timing-" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    DriveTimingRecord.Save(path, result);
                    var loaded = DriveTimingRecord.Load(path);
                    Require(loaded.U == result.U && loaded.V == result.V && loaded.Targets.SequenceEqual(result.Targets), "portable record round trip");
                    Throws(() => (loaded with { ReducedChiSquared = 26 }).Validate());
                    Throws(() => (loaded with { U = double.NaN }).Validate());
                    Throws(() => (loaded with { Targets = loaded.Targets.Take(2).ToArray() }).Validate());
                    Throws(() => (loaded with { PhaseConstantDegrees = loaded.PhaseConstantDegrees + 1 }).Validate());
                }
                finally { File.Delete(path); }
                Console.WriteLine($"drive timing {media}/{strategy}: {result.Targets.Count} targets, angle spread {differences.Max() - differences.Min():0.###} deg");
            }
        }
        var slowCache = new SimulatedDrive(DriveTimingMedia.Dvd, DriveTimingCacheStrategy.Evict) { CacheLatencySeconds = .005 };
        Require(DriveTimingCalibrator.Measure(slowCache, new(16, 18, 32, 12, 60)).CacheStrategy == DriveTimingCacheStrategy.Evict,
            "slow USB cache hits are detected by eviction comparison");
        Throws(() => DriveTimingCalibrator.Measure(new SimulatedDrive(DriveTimingMedia.Dvd, DriveTimingCacheStrategy.ForceUnitAccess) { VariableSpeed = true }, new(12, 12, 24, 8)));
        var broken = new SimulatedDrive(DriveTimingMedia.Dvd, DriveTimingCacheStrategy.ForceUnitAccess) { MediumError = true };
        Throws(() => DriveTimingCalibrator.Measure(broken));
        Require(broken.ReadCount <= 8, "unreadable media fails early");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { DriveTimingCalibrator.Measure(new SimulatedDrive(DriveTimingMedia.Dvd, DriveTimingCacheStrategy.ForceUnitAccess), cancellationToken: cancellation.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        Console.WriteLine("drive-timing-calibration: passed (independent simulated spindle, cache rejection, CAV, CD/DVD geometry, conversion, records, cancellation)");

    }

    private static void Throws(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new Exception("Invalid timing measurement was accepted."); }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Timing calibration: " + message); }

    private sealed class SimulatedDrive : IDriveTimingReader
    {
        private readonly Random _random = new(7);
        private readonly Queue<long> _cache = new();
        private readonly DriveTimingCacheStrategy _strategy;
        private double _clock = 1000, _radius = 24;
        private readonly double _u, _v;
        public bool VariableSpeed { get; init; }
        public bool MediumError { get; init; }
        public double CacheLatencySeconds { get; init; } = .0011;
        public int ReadCount { get; private set; }
        public DriveTimingDisc Disc { get; }
        public long TimestampFrequency => 1_000_000_000;
        public int MaximumReadSectors => 32;

        internal SimulatedDrive(DriveTimingMedia media, DriveTimingCacheStrategy strategy)
        {
            Disc = new(media, media == DriveTimingMedia.Dvd ? 2_295_104 : 359_849, "Independent simulated spindle");
            _strategy = strategy;
            double bit = media == DriveTimingMedia.Dvd ? 133.6e-6 : 1200.0 / 4_321_800;
            _u = media == DriveTimingMedia.Dvd ? 23.93 / bit : 25 / bit * 1.01;
            _v = media == DriveTimingMedia.Dvd ? .000735 / bit : .0016 / bit * .99;
        }

        internal double Angle(double n) => 2 * Math.PI / _v * (Math.Sqrt(_u * _u + _v * n / Math.PI) - _u);
        public DriveTimingRead Read(uint lba, int sectors, bool forceUnitAccess)
        {
            ReadCount++;
            long start = (long)(_clock * TimestampFrequency);
            if (MediumError) { _clock += 7.5; return new(start, (long)(_clock * TimestampFrequency), false, 3); }
            long block = lba / (Disc.Media == DriveTimingMedia.Dvd ? 16 : 1);
            bool cached = _strategy != DriveTimingCacheStrategy.Plain && _cache.Contains(block)
                && !(forceUnitAccess && _strategy == DriveTimingCacheStrategy.ForceUnitAccess);
            if (cached) _clock += CacheLatencySeconds;
            else
            {
                // Physical truth is computed independently of the probe's sector-completion helper.
                double endSector = Disc.Media == DriveTimingMedia.Dvd ? ((lba + sectors - 1) / 16 + 1) * 16.0 : lba + sectors + 108.0 / 98;
                double n = endSector * (Disc.Media == DriveTimingMedia.Dvd ? 38688 : 57624);
                double bit = Disc.Media == DriveTimingMedia.Dvd ? 133.6e-6 : 1200.0 / 4_321_800;
                double radius = bit * Math.Sqrt(_u * _u + _v * n / Math.PI);
                double period = .02 * (VariableSpeed ? radius / 24 : 1);
                _clock += .001 + _random.NextDouble() * .008 + Math.Abs(radius - _radius) * .0001;
                double phase = Angle(n) / (2 * Math.PI);
                _clock = (Math.Ceiling(_clock / period - phase) + phase) * period;
                _clock += (_random.NextDouble() - .5) * .00008;
                _radius = radius;
            }
            for (int i = 0; i < sectors; i++)
            {
                _cache.Enqueue(block + i);
                while (_cache.Count > 12) _cache.Dequeue();
            }
            return new(start, (long)(_clock * TimestampFrequency), true);
        }
        public bool FlushReadCache(uint lba) { if (_strategy == DriveTimingCacheStrategy.Flush) _cache.Clear(); return true; }
        public void Pause(TimeSpan duration, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _clock += duration.TotalSeconds; }
    }
}
