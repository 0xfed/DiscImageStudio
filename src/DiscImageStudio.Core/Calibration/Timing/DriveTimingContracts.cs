using DiscImageStudio.Localization;
using System.Text.Json.Serialization;

namespace DiscImageStudio.Core.Calibration;

public enum DriveTimingMedia { Cd, Dvd }
public enum DriveTimingCacheStrategy { Plain, ForceUnitAccess, Flush, Evict }
public enum DriveTimingStage { CheckingMedia, CheckingCache, CheckingRotation, Settling, Measuring, Complete }

public sealed record DriveTimingDisc(DriveTimingMedia Media, long RecordedSectors, string DeviceName);

/// <summary>Ticks are captured immediately around the native read, on one monotonic clock.</summary>
public readonly record struct DriveTimingRead(long StartTicks, long EndTicks, bool Succeeded, int SenseKey = 0);

/// <summary>The geometry solver owns no device handles and cannot issue recording commands.</summary>
public interface IDriveTimingReader
{
    DriveTimingDisc Disc { get; }
    long TimestampFrequency { get; }
    int MaximumReadSectors { get; }
    DriveTimingRead Read(uint lba, int sectors, bool forceUnitAccess);
    bool FlushReadCache(uint lba);
    void Pause(TimeSpan duration, CancellationToken cancellationToken);
}

public sealed record DriveTimingOptions(int SamplesPerTarget = 24, int MaximumTargets = 22,
    int PeriodSamples = 60, int CacheSamples = 40, int MaximumMinutes = 15)
{
    public static DriveTimingOptions For(DriveTimingMedia media)
        => media == DriveTimingMedia.Cd ? new(32, 36) : new();

    internal void Validate()
    {
        if (SamplesPerTarget is < 8 or > 128 || MaximumTargets is < 8 or > 128
            || PeriodSamples is < 16 or > 256 || CacheSamples is < 8 or > 128 || MaximumMinutes is < 1 or > 60)
            throw new ArgumentException(L.T("Timing.InvalidOptions"));
    }
}

public sealed record DriveTimingProgress(DriveTimingStage Stage, int Completed = 0, int Total = 0,
    uint? Lba = null, double? PeriodMilliseconds = null);

public sealed record DriveTimingTarget(uint Lba, double ChannelBits, double PhaseDegrees, double SigmaDegrees,
    double StandardDeviationDegrees, int SamplesUsed, double PredictedDegrees, double UnwrappedDegrees,
    double ResidualDegrees, double PeriodMilliseconds);

/// <summary>Dimensionless spiral parameters u=r0/b and v=p/b. The phase constant is a
/// decoder delay, not an image rotation or an independently measured physical radius.</summary>
public sealed record DriveTimingResult(DriveTimingMedia Media, long RecordedSectors, string DeviceName,
    double U, double V, double PhaseConstantDegrees, uint ReferenceLba, double PeriodMilliseconds,
    DriveTimingCacheStrategy CacheStrategy, double ResidualRmsDegrees, double ReducedChiSquared,
    double AngleUncertaintyDegrees, IReadOnlyList<DriveTimingTarget> Targets)
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonIgnore] public bool RequiresFineCalibration => Media == DriveTimingMedia.Cd;
    [JsonIgnore] public double AssumedBitLengthMm => Media == DriveTimingMedia.Cd ? 1200.0 / 4_321_800 : 133.3e-6;
    [JsonIgnore]
    public (double Inner, double Outer) MeasuredRadiusRange =>
        (Radius(Targets.Min(t => t.ChannelBits)), Radius(Targets.Max(t => t.ChannelBits)));

    private double Radius(double channelBits) => AssumedBitLengthMm * Math.Sqrt(U * U + V * channelBits / Math.PI);

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || !Enum.IsDefined(Media) || !Enum.IsDefined(CacheStrategy)
            || RecordedSectors is < 8192 or > 3_000_000 || (Media == DriveTimingMedia.Cd && RecordedSectors > 450_000)
            || string.IsNullOrWhiteSpace(DeviceName) || DeviceName.Length > 1024
            || !double.IsFinite(U) || !double.IsFinite(V) || U <= 0 || V <= 0
            || !double.IsFinite(PhaseConstantDegrees) || !double.IsFinite(PeriodMilliseconds)
            || PeriodMilliseconds is < 3 or > 400 || ReferenceLba >= RecordedSectors
            || !double.IsFinite(ResidualRmsDegrees) || ResidualRmsDegrees is < 0 or > 30
            || !double.IsFinite(ReducedChiSquared) || ReducedChiSquared is < 0 or > 25
            || !double.IsFinite(AngleUncertaintyDegrees) || AngleUncertaintyDegrees is < 0 or > 25
            || Targets is null || Targets.Count is < 8 or > 128)
            throw new ArgumentException(L.T("Timing.InvalidRecord"));
        int block = Media == DriveTimingMedia.Dvd ? 16 : 1;
        var addresses = new HashSet<uint>();
        double squared = 0, chi = 0;
        double referenceAngle = DriveTimingGeometry.Angle(DriveTimingGeometry.ReadEndBits(Media, ReferenceLba), U, V);
        foreach (var target in Targets)
        {
            double expectedBits = DriveTimingGeometry.ReadEndBits(Media, target.Lba);
            if (target.Lba >= RecordedSectors || target.Lba % block != 0 || !addresses.Add(target.Lba)
                || !double.IsFinite(target.ChannelBits) || target.ChannelBits != expectedBits
                || !double.IsFinite(target.PhaseDegrees) || target.PhaseDegrees is < 0 or >= 360
                || !double.IsFinite(target.SigmaDegrees) || target.SigmaDegrees is < .2 or > 30
                || !double.IsFinite(target.StandardDeviationDegrees) || target.StandardDeviationDegrees is < 0 or > 30
                || target.SamplesUsed is < 4 or > 128 || !double.IsFinite(target.PredictedDegrees)
                || !double.IsFinite(target.UnwrappedDegrees) || !double.IsFinite(target.ResidualDegrees)
                || Math.Abs(target.ResidualDegrees) > 90 || !double.IsFinite(target.PeriodMilliseconds)
                || target.PeriodMilliseconds is < 3 or > 400)
                throw new ArgumentException(L.T("Timing.InvalidRecord"));
            double model = (DriveTimingGeometry.Angle(target.ChannelBits, U, V) - referenceAngle) * 180 / Math.PI + PhaseConstantDegrees;
            double residual = target.UnwrappedDegrees - model;
            double wrappedDifference = DriveTimingAnalysis.Wrap((target.UnwrappedDegrees - target.PhaseDegrees) * Math.PI / 180);
            if (Math.Abs(residual - target.ResidualDegrees) > 1e-5 || Math.Abs(wrappedDifference) > 1e-7)
                throw new ArgumentException(L.T("Timing.InvalidRecord"));
            squared += residual * residual;
            chi += Math.Pow(residual / target.SigmaDegrees, 2);
        }
        if (Math.Abs(Math.Sqrt(squared / Targets.Count) - ResidualRmsDegrees) > 1e-5
            || Math.Abs(chi / Math.Max(1, Targets.Count - 3) - ReducedChiSquared) > 1e-5)
            throw new ArgumentException(L.T("Timing.InvalidRecord"));
        if (Targets.Max(t => t.Lba) - Targets.Min(t => t.Lba) < RecordedSectors * .4)
            throw new ArgumentException(L.T("Timing.InsufficientCoverage"));
        _ = ToParameters(RecordedSectors, validateRecord: false);
    }

    public CalibrationParameters ToParameters(long totalSectors, double alongTrackFrames = 0)
        => ToParameters(totalSectors, alongTrackFrames, validateRecord: true);

    private CalibrationParameters ToParameters(long totalSectors, double alongTrackFrames = 0, bool validateRecord = true)
    {
        if (validateRecord) Validate();
        if (totalSectors < 8192 || totalSectors > (Media == DriveTimingMedia.Cd ? 450_000 : 3_000_000)
            || totalSectors <= Targets.Max(t => t.Lba) || !double.IsFinite(alongTrackFrames)
            || Math.Abs(alongTrackFrames) > 10_000 || (Media != DriveTimingMedia.Cd && alongTrackFrames != 0))
            throw new ArgumentException(L.T("Timing.InvalidCapacity"));
        double origin = U * U - V * alongTrackFrames * 588 / Math.PI;
        double bitsPerSector = Media == DriveTimingMedia.Cd ? 57_624 : 38_688;
        double inner = AssumedBitLengthMm * Math.Sqrt(origin);
        double outer = AssumedBitLengthMm * Math.Sqrt(origin + V * totalSectors * bitsPerSector / Math.PI);
        if (!double.IsFinite(inner) || !double.IsFinite(outer) || inner is < 15 or > 35 || outer is < 35 or > 65)
            throw new ArgumentException(L.T("Timing.InvalidRecord"));
        var result = new CalibrationParameters(Media == DriveTimingMedia.Cd ? CalibrationDiscKind.Cd : CalibrationDiscKind.Dvd,
            inner, outer, totalSectors, Media == DriveTimingMedia.Cd ? 1200 : 133.3);
        result.Validate();
        return result;
    }
}

public static class DriveTimingGeometry
{
    public static double Angle(double channelBits, double u, double v)
        => 2 * channelBits / (Math.Sqrt(u * u + v * channelBits / Math.PI) + u);

    public static double ReadEndBits(DriveTimingMedia media, uint lba)
        => media == DriveTimingMedia.Cd ? ((lba + 1.0) * 98 + 108) * 588 : ((long)lba / 16 + 1) * 16.0 * 38_688;

    internal static double[] Derivative(double n, double u, double v)
    {
        double s = Math.Sqrt(u * u + v * n / Math.PI), angle = Angle(n, u, v);
        return [-angle / s, -angle * angle / (4 * Math.PI * s), 1];
    }
}
