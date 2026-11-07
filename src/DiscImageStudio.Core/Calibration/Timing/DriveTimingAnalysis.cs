namespace DiscImageStudio.Core.Calibration;

// Adapted from Ashley-QwQ/disc-probe, discprobe/analysis.py (GPL-3.0).
// See THIRD-PARTY-NOTICES.txt for the reviewed revision and attribution.
public static class DriveTimingAnalysis
{
    private const double Tau = 2 * Math.PI;

    public sealed record Period(double Seconds, double Coherence, long BaseTicks, long Frequency,
        double OriginSeconds, double JitterSeconds, double[] Times, double?[] Residuals)
    {
        public int Used => Residuals.Count(r => r is not null);
        public double Rpm => 60 / Seconds;
    }

    public readonly record struct Phase(double Degrees, double StandardDeviationDegrees, double StandardErrorDegrees, int Used);

    public static Period? EstimatePeriod(IReadOnlyList<long> ticks, long frequency, double? priorSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        if (ticks.Count < 5 || ticks.Count > 4096 || frequency <= 0) return null;
        long baseline = ticks[0];
        double[] times = ticks.Select(t => (t - baseline) / (double)frequency).ToArray();
        double span = times[^1];
        if (!double.IsFinite(span) || span <= 0 || times.Zip(times.Skip(1)).Any(p => p.First >= p.Second)) return null;
        double df = 1 / (8 * span);
        var candidates = new List<(double Frequency, double Coherence)>();
        if (priorSeconds is > 0 && double.IsFinite(priorSeconds.Value))
        {
            for (int k = 1; k <= 3; k++)
            {
                double center = k / priorSeconds.Value;
                var best = Best(center * .98, center * 1.02, df);
                candidates.Add(Refine(best.Frequency, df));
            }
        }
        else
        {
            const double low = 1 / .4, high = 1 / .003;
            df = Math.Max(df, (high - low) / 200_000);
            var best = Refine(Best(low, high, df).Frequency, df);
            candidates.Add(best);
            for (int k = 2; k <= 12 && best.Frequency / k >= low; k++)
                candidates.Add(Refine(best.Frequency / k, df));
        }
        double maximum = candidates.Max(c => c.Coherence);
        double selected = candidates.Where(c => c.Coherence >= .9 * maximum).Min(c => c.Frequency);
        double coherence = Coherence(selected), period = 1 / selected, origin = 0;
        if (period is < .003 or > .4) return null;
        int[] keep = Enumerable.Range(0, times.Length).ToArray();
        double[] residuals = new double[times.Length];
        for (int iteration = 0; iteration < 3; iteration++)
        {
            double[] turns = keep.Select(i => Math.Round((times[i] - origin) / period)).ToArray();
            double meanTurn = turns.Average(), meanTime = keep.Average(i => times[i]);
            double sxx = turns.Sum(n => (n - meanTurn) * (n - meanTurn));
            if (sxx > 0)
                period = keep.Select((i, j) => (turns[j] - meanTurn) * (times[i] - meanTime)).Sum() / sxx;
            if (!double.IsFinite(period) || period is < .003 or > .4) return null;
            origin = meanTime - period * meanTurn;
            for (int i = 0; i < times.Length; i++)
                residuals[i] = times[i] - origin - Math.Round((times[i] - origin) / period) * period;
            double median = Median(keep.Select(i => residuals[i]));
            double limit = Math.Max(5 * Median(keep.Select(i => Math.Abs(residuals[i] - median))) * 1.4826, 20e-6);
            keep = Enumerable.Range(0, times.Length).Where(i => Math.Abs(residuals[i] - median) <= limit).ToArray();
            if (keep.Length < 4) return null;
        }
        var kept = keep.ToHashSet();
        double meanResidual = keep.Average(i => residuals[i]);
        double jitter = Math.Sqrt(keep.Average(i => Math.Pow(residuals[i] - meanResidual, 2)));
        return new(period, coherence, baseline, frequency, origin, jitter, times,
            residuals.Select((r, i) => kept.Contains(i) ? (double?)r : null).ToArray());

        double Coherence(double f)
        {
            double cosine = 0, sine = 0;
            foreach (double t in times)
            {
                var (s, c) = Math.SinCos(Tau * f * t);
                cosine += c; sine += s;
            }
            return Math.Sqrt(cosine * cosine + sine * sine) / times.Length;
        }
        (double Frequency, double Coherence) Best(double low, double high, double step)
        {
            int steps = Math.Clamp((int)Math.Min(200_000, (high - low) / step) + 1, 2, 200_001);
            double bestFrequency = low, bestCoherence = -1;
            for (int i = 0; i < steps; i++)
            {
                if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                double f = low + (high - low) * i / (steps - 1), c = Coherence(f);
                if (c > bestCoherence) { bestFrequency = f; bestCoherence = c; }
            }
            return (bestFrequency, bestCoherence);
        }
        (double Frequency, double Coherence) Refine(double f, double step)
        {
            for (int i = 0; i < 3; i++)
            {
                f = Best(f - step, f + step, step / 20).Frequency;
                step /= 10;
            }
            return (f, Coherence(f));
        }
    }

    public static Phase? MeasurePhase(Period period, IReadOnlyList<long> targetTicks)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(targetTicks);
        var references = period.Times.Select((t, i) => (Time: t, Residual: period.Residuals[i]))
            .Where(r => r.Residual.HasValue).Select(r => (r.Time, Residual: r.Residual!.Value)).ToArray();
        if (references.Length < 4 || targetTicks.Count < 4) return null;
        double[] angles = targetTicks.Select(tick =>
        {
            double t = (tick - period.BaseTicks) / (double)period.Frequency;
            int index = Array.FindIndex(references, r => r.Time >= t);
            double residual;
            if (index == 0) residual = references[0].Residual;
            else if (index < 0) residual = references[^1].Residual;
            else
            {
                var a = references[index - 1]; var b = references[index];
                residual = a.Residual + (b.Residual - a.Residual) * (t - a.Time) / (b.Time - a.Time);
            }
            return Tau * ((t - period.OriginSeconds - residual) / period.Seconds % 1);
        }).ToArray();
        double initial = Mean(angles);
        double limit = Math.Max(5 * Median(angles.Select(a => Math.Abs(Wrap(a - initial)))) * 1.4826, Math.PI / 180);
        double[] keep = angles.Where(a => Math.Abs(Wrap(a - initial)) <= limit).ToArray();
        if (keep.Length < 4) return null;
        double mean = Mean(keep), cosine = keep.Average(Math.Cos), sine = keep.Average(Math.Sin);
        double length = Math.Clamp(Math.Sqrt(cosine * cosine + sine * sine), 0, 1);
        if (length <= 0) return null;
        double std = Math.Sqrt(-2 * Math.Log(length)) * 180 / Math.PI;
        return new((mean * 180 / Math.PI + 360) % 360, std, std / Math.Sqrt(keep.Length), keep.Length);

        static double Mean(double[] values) => Math.Atan2(values.Sum(Math.Sin), values.Sum(Math.Cos));
    }

    internal static double Wrap(double radians) => radians - 2 * Math.PI * Math.Floor((radians + Math.PI) / (2 * Math.PI));
    internal static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        return sorted.Length == 0 ? double.NaN : sorted.Length % 2 == 0
            ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2 : sorted[sorted.Length / 2];
    }
}
