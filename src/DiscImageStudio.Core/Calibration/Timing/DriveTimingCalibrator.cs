using DiscImageStudio.Localization;
using System.Text.Json;

namespace DiscImageStudio.Core.Calibration;

// Native adaptation of Ashley-QwQ/disc-probe's timing.py and geo.py (GPL-3.0).
// The device boundary is read-only; fitting and quality checks also run on a simulated clock.
public static class DriveTimingCalibrator
{
    public static DriveTimingResult Measure(IDriveTimingReader reader, DriveTimingOptions? options = null,
        IProgress<DriveTimingProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        options ??= DriveTimingOptions.For(reader.Disc.Media);
        options.Validate();
        var measurement = new Measurement(reader, options, progress, cancellationToken);
        try { return measurement.Run(); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Optional diagnostics must never replace the original device or fit error.
            try { measurement.AttachDiagnostics(error); }
            catch { }
            throw;
        }
    }

    private sealed class Measurement(IDriveTimingReader reader, DriveTimingOptions options,
        IProgress<DriveTimingProgress>? progress, CancellationToken token)
    {
        private const double Radians = Math.PI / 180;
        private readonly Random _random = new(1);
        private readonly List<DriveTimingTarget> _targets = [];
        private DriveTimingCacheStrategy _strategy;
        private double _cacheThreshold = .003, _pauseSeconds = .025, _period;
        private long? _startedTicks;
        private int _mediumErrors;
        private double _u, _v, _c, _u0, _v0, _sigmaU, _sigmaV, _chiSquared;
        private double[,]? _covariance;
        private uint _reference, _low, _high;
        private double _referenceBits;
        private double? _plainMedian, _forceMedian, _evictedMedian, _flushMedian;
        private readonly Queue<PeriodDiagnostic> _periodChecks = new();
        private sealed record PeriodDiagnostic(string Stage, uint Lba, int Attempt, int Samples,
            int Succeeded, int Usable, double? PeriodMilliseconds, double? Coherence, int Kept);
        private int Block => reader.Disc.Media == DriveTimingMedia.Dvd ? 16 : 1;
        private bool IsCd => reader.Disc.Media == DriveTimingMedia.Cd;

        internal DriveTimingResult Run()
        {
            Report(DriveTimingStage.CheckingMedia);
            if (!Enum.IsDefined(reader.Disc.Media) || reader.Disc.RecordedSectors is < 8192 or > 3_000_000
                || (IsCd && reader.Disc.RecordedSectors > 450_000) || reader.TimestampFrequency <= 0
                || reader.MaximumReadSectors < 1)
                Fail("Timing.InvalidMedia");
            long capacity = reader.Disc.RecordedSectors;
            double bit = IsCd ? 1200.0 / 4_321_800 : 133.3e-6;
            _u = _u0 = (IsCd ? 25 : 24) / bit;
            _v = _v0 = (IsCd ? .0016 : .00074) / bit;
            _sigmaU = _u0 * (IsCd ? .08 : .01); _sigmaV = _v0 * (IsCd ? .15 : .03);
            _low = Align(capacity * .03); _high = Align(capacity * .85);
            _reference = Align((_low + (double)_high) / 2);
            _referenceBits = Bits(_reference);
            foreach (double fraction in new[] { .05, .25, .5, .75, .95 }) Read(Align(capacity * fraction), 1, false);
            CheckCache();
            CheckRotation();
            Settle();

            var used = new HashSet<uint> { _reference };
            foreach (double turns in IsCd ? new[] { .5, 1.5 } : [1.5, 4.0])
            {
                uint lba = AtTurns(turns);
                if (used.Any(a => Math.Abs((double)a - lba) < Block)) lba = AtTurns(-turns);
                if (!used.Add(lba) || lba == _reference) Fail("Timing.InsufficientCoverage");
                var target = MeasureTarget(lba) ?? RetryTarget(lba);
                if (target is null) Fail("Timing.UnsteadyDrive");
                double predicted = Predict(target!.ChannelBits) / Radians;
                if (_targets.Count == 0)
                {
                    _c = DriveTimingAnalysis.Wrap((target.PhaseDegrees - predicted) * Radians);
                    predicted = Predict(target.ChannelBits) / Radians;
                }
                Add(target, predicted);
            }
            Fit();
            var tried = _targets.Select(t => t.Lba).ToHashSet();
            while (_targets.Count < options.MaximumTargets)
            {
                token.ThrowIfCancellationRequested();
                var candidates = Candidates().Where(lba => !tried.Contains(lba)
                    && tried.All(t => Math.Abs((double)t - lba) >= (IsCd ? 64 : 4000))).ToArray();
                if (candidates.Length == 0) break;
                var safe = candidates.Where(lba => PredictionSigma(Bits(lba)) / Radians < 25).ToArray();
                // Do not guess a turn count when the existing measurements cannot resolve it.
                if (safe.Length == 0) Fail("Timing.AmbiguousTurns");
                uint next;
                if (safe.Length == candidates.Length)
                    next = _targets.Count % 3 == 0
                        ? safe.MaxBy(lba => tried.Append(_reference).Min(t => Math.Abs((double)t - lba)))
                        : safe.MinBy(SpanVarianceAfter);
                else next = safe.MaxBy(lba => Math.Abs(Bits(lba) - _referenceBits));
                tried.Add(next);
                double prediction = Predict(Bits(next)) / Radians;
                var measured = MeasureTarget(next) ?? RetryTarget(next);
                if (measured is null) continue;
                Add(measured, prediction);
                Fit();
                if (_chiSquared > 25 || _targets.Max(t => Math.Abs(t.ResidualDegrees)) > 90)
                    Fail("Timing.AmbiguousTurns");
            }
            if (_targets.Count < 8) Fail("Timing.InsufficientCoverage");
            double[] spanGradient = GradientDifference(_targets.Min(t => t.ChannelBits), _targets.Max(t => t.ChannelBits));
            double angleSigma = Math.Sqrt(Math.Max(0, Variance(spanGradient))) / Radians;
            var result = new DriveTimingResult(reader.Disc.Media, capacity, reader.Disc.DeviceName,
                _u, _v, _c / Radians, _reference, _period * 1000, _strategy,
                Math.Sqrt(_targets.Average(t => t.ResidualDegrees * t.ResidualDegrees)), _chiSquared,
                angleSigma, _targets.ToArray());
            result.Validate();
            Report(DriveTimingStage.Complete, _targets.Count, options.MaximumTargets);
            return result;
        }

        private void CheckCache()
        {
            Report(DriveTimingStage.CheckingCache);
            uint lba = Align(reader.Disc.RecordedSectors * .5);
            DriveTimingRead[] plain = Enumerable.Range(0, options.CacheSamples).Select(_ => Read(lba, 1, false)).ToArray();
            DriveTimingRead[] forced = Enumerable.Range(0, options.CacheSamples).Select(_ => Read(lba, 1, true)).ToArray();
            double plainMedian = MedianDuration(plain), forceMedian = MedianDuration(forced);
            _plainMedian = plainMedian; _forceMedian = forceMedian;
            bool cached = plainMedian < .003;
            if (forced.All(r => r.Succeeded) && forceMedian >= .003 && forceMedian >= 3 * plainMedian) cached = true;
            if (!cached)
            {
                var evicted = new List<DriveTimingRead>();
                for (int i = 0; i < 8; i++) { Evict(); evicted.Add(Read(lba, 1, false)); }
                double slower = MedianDuration(evicted);
                _evictedMedian = slower;
                if (evicted.All(r => r.Succeeded) && slower >= 3 * plainMedian)
                {
                    cached = true;
                    _cacheThreshold = Math.Max(.003, Math.Sqrt(plainMedian * slower));
                }
            }
            if (!cached && plain.All(r => r.Succeeded)) { _strategy = DriveTimingCacheStrategy.Plain; return; }
            if (forced.All(r => r.Succeeded) && forceMedian >= _cacheThreshold)
            { _strategy = DriveTimingCacheStrategy.ForceUnitAccess; return; }
            bool flushed = true;
            var afterFlush = new List<DriveTimingRead>();
            for (int i = 0; i < options.CacheSamples; i++)
            {
                token.ThrowIfCancellationRequested();
                flushed &= reader.FlushReadCache(lba);
                afterFlush.Add(Read(lba, 1, false));
            }
            _flushMedian = MedianDuration(afterFlush);
            _strategy = flushed && afterFlush.All(r => r.Succeeded) && _flushMedian.Value >= _cacheThreshold
                ? DriveTimingCacheStrategy.Flush : DriveTimingCacheStrategy.Evict;
        }

        private void CheckRotation()
        {
            Settle();
            var periods = new List<double>();
            int zone = 0;
            foreach (double fraction in new[] { .05, .5, .95 })
            {
                uint lba = Align(reader.Disc.RecordedSectors * fraction);
                Report(DriveTimingStage.CheckingRotation, zone++, 3, lba);
                DriveTimingAnalysis.Period? fit = null;
                // A seek can leave a transient in the first window. Retry fresh
                // samples at this zone without weakening the rotation criteria.
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    fit = SamplePeriod(lba, options.PeriodSamples, DriveTimingStage.CheckingRotation, attempt);
                    if (fit is { Coherence: >= .93, Used: >= 8 }) break;
                }
                if (fit is null || fit.Coherence < .93 || fit.Used < 8) Fail("Timing.UnsteadyDrive");
                periods.Add(fit!.Seconds);
            }
            if (periods.Max() / periods.Min() - 1 >= .03) Fail("Timing.NotConstantRotation");
        }

        private void Settle()
        {
            Report(DriveTimingStage.Settling);
            double? previous = null;
            for (int round = 0; round < 10; round++)
            {
                var fit = SamplePeriod(_reference, 16, DriveTimingStage.Settling, round + 1);
                if (fit is { Coherence: > .95 } && previous is { } prior && Math.Abs(fit.Seconds / prior - 1) < .002)
                {
                    _period = fit.Seconds;
                    _pauseSeconds = Math.Max(_pauseSeconds, 1.25 * _period);
                    return;
                }
                previous = fit is { Coherence: > .95 } ? fit.Seconds : null;
            }
            Fail("Timing.UnsteadyDrive");
        }

        private DriveTimingAnalysis.Period? SamplePeriod(uint lba, int count, DriveTimingStage stage, int attempt)
        {
            var times = new List<long>();
            int succeeded = 0;
            for (int i = 0; i < count; i++)
            {
                var read = Timed(lba);
                if (read.Succeeded) succeeded++;
                if (Usable(read)) times.Add(read.EndTicks);
            }
            var fit = DriveTimingAnalysis.EstimatePeriod(times, reader.TimestampFrequency, cancellationToken: token);
            _periodChecks.Enqueue(new(stage.ToString(), lba, attempt, count, succeeded, times.Count,
                fit?.Seconds * 1000, fit?.Coherence, fit?.Used ?? 0));
            while (_periodChecks.Count > 24) _periodChecks.Dequeue();
            return fit;
        }

        internal void AttachDiagnostics(Exception error)
        {
            error.Data["Timing.DeviceName"] = reader.Disc.DeviceName;
            error.Data["Timing.Media"] = reader.Disc.Media;
            error.Data["Timing.RecordedSectors"] = reader.Disc.RecordedSectors;
            error.Data["Timing.TimestampFrequency"] = reader.TimestampFrequency;
            error.Data["Timing.CacheStrategy"] = _strategy;
            error.Data["Timing.CacheThresholdMs"] = _cacheThreshold * 1000;
            error.Data["Timing.PlainReadMedianMs"] = _plainMedian * 1000;
            error.Data["Timing.FuaReadMedianMs"] = _forceMedian * 1000;
            error.Data["Timing.EvictedReadMedianMs"] = _evictedMedian * 1000;
            error.Data["Timing.FlushReadMedianMs"] = _flushMedian * 1000;
            error.Data["Timing.PeriodChecks"] = string.Join(Environment.NewLine,
                _periodChecks.Select(check => JsonSerializer.Serialize(check)));
        }

        private DriveTimingTarget? RetryTarget(uint lba) { Settle(); return MeasureTarget(lba); }

        private DriveTimingTarget? MeasureTarget(uint lba)
        {
            Report(DriveTimingStage.Measuring, _targets.Count, options.MaximumTargets, lba);
            var references = new List<long>(); var targets = new List<long>();
            for (int i = 0; i < options.SamplesPerTarget; i++)
            {
                var a = Timed(_reference); if (Usable(a)) references.Add(a.EndTicks);
                var b = Timed(lba); if (Usable(b)) targets.Add(b.EndTicks);
            }
            var fit = DriveTimingAnalysis.EstimatePeriod(references, reader.TimestampFrequency, _period, token);
            if (fit is null || fit.Coherence < .93 || Math.Abs(fit.Seconds / _period - 1) > .002 || targets.Count < 8) return null;
            var phase = DriveTimingAnalysis.MeasurePhase(fit, targets);
            if (phase is not { } p || !double.IsFinite(p.StandardDeviationDegrees) || p.StandardDeviationDegrees > 30) return null;
            _period = fit.Seconds;
            return new(lba, Bits(lba), p.Degrees, Math.Max(.2, p.StandardErrorDegrees),
                p.StandardDeviationDegrees, p.Used, 0, 0, 0, _period * 1000);
        }

        private DriveTimingRead Timed(uint lba)
        {
            reader.Pause(TimeSpan.FromSeconds(_random.NextDouble() * _pauseSeconds), token);
            if (_strategy == DriveTimingCacheStrategy.Evict) Evict();
            else if (_strategy == DriveTimingCacheStrategy.Flush && !reader.FlushReadCache(lba)) Fail("Timing.CacheFailed");
            return Read(lba, 1, _strategy == DriveTimingCacheStrategy.ForceUnitAccess);
        }

        private bool Usable(DriveTimingRead read) => read.Succeeded
            && (_strategy is DriveTimingCacheStrategy.Plain or DriveTimingCacheStrategy.ForceUnitAccess
                || Duration(read) >= _cacheThreshold);

        private int _evictionCursor;
        private void Evict()
        {
            int chunk = Math.Min(32, reader.MaximumReadSectors);
            long start = (long)(reader.Disc.RecordedSectors * .6);
            int region = (int)Math.Min(32_768, reader.Disc.RecordedSectors - start);
            for (int done = 0; done < Math.Min(1024, region);)
            {
                int count = Math.Min(chunk, Math.Min(region - _evictionCursor, 1024 - done));
                Read((uint)(start + _evictionCursor), count, false);
                _evictionCursor = (_evictionCursor + count) % region;
                done += count;
            }
        }

        private DriveTimingRead Read(uint lba, int sectors, bool fua)
        {
            token.ThrowIfCancellationRequested();
            if ((long)lba + sectors > reader.Disc.RecordedSectors || sectors < 1) Fail("Timing.InvalidMedia");
            var sample = reader.Read(lba, sectors, fua);
            if (sample.StartTicks < 0 || sample.EndTicks < sample.StartTicks) Fail("Timing.InvalidClock");
            _startedTicks ??= sample.StartTicks;
            if ((sample.EndTicks - _startedTicks.Value) / (double)reader.TimestampFrequency > options.MaximumMinutes * 60)
                Fail("Timing.TimeLimit");
            if (!sample.Succeeded && sample.SenseKey == 3)
            { if (++_mediumErrors >= 8) Fail("Timing.UnreadableDisc"); }
            else if (sample.Succeeded) _mediumErrors = 0;
            token.ThrowIfCancellationRequested();
            return sample;
        }

        private double Duration(DriveTimingRead read) => (read.EndTicks - read.StartTicks) / (double)reader.TimestampFrequency;
        private double MedianDuration(IEnumerable<DriveTimingRead> reads)
            => DriveTimingAnalysis.Median(reads.Where(r => r.Succeeded).Select(Duration));
        private uint Align(double lba) => (uint)(Math.Clamp(lba, 0, reader.Disc.RecordedSectors - Block) / Block) * (uint)Block;
        private double Bits(uint lba) => DriveTimingGeometry.ReadEndBits(reader.Disc.Media, lba);
        private double Predict(double n) => DriveTimingGeometry.Angle(n, _u, _v)
            - DriveTimingGeometry.Angle(_referenceBits, _u, _v) + _c;
        private uint AtTurns(double turns)
        {
            double angle = DriveTimingGeometry.Angle(_referenceBits, _u, _v) + 2 * Math.PI * turns;
            double s = _u + _v * angle / (2 * Math.PI);
            double n = Math.PI * (s * s - _u * _u) / _v;
            double lba = IsCd ? (n - 108 * 588) / 57_624 - 1 : n / 38_688;
            return Allowed(lba);
        }
        private uint Allowed(double lba) => Align(Math.Clamp(lba, _low, _high - Block));
        private uint[] Candidates()
        {
            var values = new HashSet<uint>();
            for (int i = 0; i < 40; i++) values.Add(Allowed(_low + (_high - Block - (double)_low) * i / 39));
            for (int i = 0; i < 18; i++) { values.Add(AtTurns(Math.Pow(2, i))); values.Add(AtTurns(-Math.Pow(2, i))); }
            values.Remove(_reference);
            return values.Order().ToArray();
        }
        private void Add(DriveTimingTarget target, double prediction)
            => _targets.Add(target with { PredictedDegrees = prediction,
                UnwrappedDegrees = target.PhaseDegrees + 360 * Math.Round((prediction - target.PhaseDegrees) / 360) });

        private double[] GradientDifference(double from, double to)
        {
            var a = DriveTimingGeometry.Derivative(from, _u, _v); var b = DriveTimingGeometry.Derivative(to, _u, _v);
            return [b[0] - a[0], b[1] - a[1], 0];
        }
        private double[] Gradient(double n)
        {
            var g = GradientDifference(_referenceBits, n); g[2] = 1; return g;
        }
        private double Variance(double[] g)
        {
            double sum = 0;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) sum += g[i] * _covariance![i, j] * g[j];
            return sum;
        }
        private double PredictionSigma(double n) => Math.Sqrt(Math.Max(0, Variance(Gradient(n))));
        private double SpanVarianceAfter(uint lba)
        {
            double[] g = GradientDifference(Bits(_low), Bits(_high - (uint)Block)), h = Gradient(Bits(lba));
            double variance = Variance(g), denominator = Math.Pow(DriveTimingAnalysis.Median(_targets.Select(t => t.SigmaDegrees)) * Radians, 2) + Variance(h);
            double cross = 0;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) cross += g[i] * _covariance![i, j] * h[j];
            return variance - cross * cross / denominator;
        }

        private void Fit()
        {
            double[,] inverse = new double[3, 3]; double[] scales = new double[3];
            for (int iteration = 0; iteration < 15; iteration++)
            {
                token.ThrowIfCancellationRequested();
                var rows = _targets.Select(t => Gradient(t.ChannelBits).Select(d => d / (t.SigmaDegrees * Radians)).ToArray()).ToList();
                var residuals = _targets.Select(t => (t.UnwrappedDegrees * Radians - Predict(t.ChannelBits)) / (t.SigmaDegrees * Radians)).ToList();
                rows.Add([1 / _sigmaU, 0, 0]); residuals.Add((_u0 - _u) / _sigmaU);
                rows.Add([0, 1 / _sigmaV, 0]); residuals.Add((_v0 - _v) / _sigmaV);
                for (int i = 0; i < 3; i++) scales[i] = Math.Sqrt(rows.Sum(row => row[i] * row[i]));
                var normal = new double[3, 3]; var rhs = new double[3];
                for (int k = 0; k < rows.Count; k++)
                    for (int i = 0; i < 3; i++)
                    {
                        double a = rows[k][i] / scales[i]; rhs[i] += a * residuals[k];
                        for (int j = 0; j < 3; j++) normal[i, j] += a * rows[k][j] / scales[j];
                    }
                inverse = Inverse(normal);
                double[] step = Enumerable.Range(0, 3).Select(i => Enumerable.Range(0, 3).Sum(j => inverse[i, j] * rhs[j]) / scales[i]).ToArray();
                _u += step[0]; _v += step[1]; _c += step[2];
                if (!double.IsFinite(_u) || !double.IsFinite(_v) || !double.IsFinite(_c) || _u <= 0 || _v <= 0) Fail("Timing.UnstableFit");
                if (step.Select((s, i) => Math.Abs(s * scales[i])).Max() < 1e-9) break;
            }
            double chi = 0;
            for (int i = 0; i < _targets.Count; i++)
            {
                double residual = _targets[i].UnwrappedDegrees - Predict(_targets[i].ChannelBits) / Radians;
                _targets[i] = _targets[i] with { ResidualDegrees = residual };
                chi += Math.Pow(residual / _targets[i].SigmaDegrees, 2);
            }
            _chiSquared = chi / Math.Max(1, _targets.Count - 3);
            _covariance = new double[3, 3];
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                _covariance[i, j] = inverse[i, j] / (scales[i] * scales[j]) * Math.Max(1, _chiSquared);
        }

        private static double[,] Inverse(double[,] matrix)
        {
            var a = new double[3, 6];
            for (int i = 0; i < 3; i++) for (int j = 0; j < 6; j++) a[i, j] = j < 3 ? matrix[i, j] : j - 3 == i ? 1 : 0;
            for (int column = 0; column < 3; column++)
            {
                int pivot = Enumerable.Range(column, 3 - column).MaxBy(i => Math.Abs(a[i, column]));
                if (!double.IsFinite(a[pivot, column]) || Math.Abs(a[pivot, column]) < 1e-14) Fail("Timing.UnstableFit");
                for (int j = 0; j < 6; j++) (a[pivot, j], a[column, j]) = (a[column, j], a[pivot, j]);
                double scale = a[column, column];
                for (int j = 0; j < 6; j++) a[column, j] /= scale;
                for (int i = 0; i < 3; i++) if (i != column)
                {
                    double factor = a[i, column];
                    for (int j = 0; j < 6; j++) a[i, j] -= factor * a[column, j];
                }
            }
            var result = new double[3, 3];
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) result[i, j] = a[i, j + 3];
            return result;
        }
        private void Report(DriveTimingStage stage, int completed = 0, int total = 0, uint? lba = null)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(new(stage, completed, total, lba, _period > 0 ? _period * 1000 : null));
        }
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void Fail(string key) => throw new InvalidOperationException(L.T(key));
    }
}
