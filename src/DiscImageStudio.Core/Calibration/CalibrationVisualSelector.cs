using DiscImageStudio.Localization;

namespace DiscImageStudio.Core.Calibration;

/// <summary>A test strip in one of the two banks; indices run from the centre outwards.</summary>
public sealed record CalibrationVisualBand(string Bank, int Index,
    double InnerRadiusMm, double OuterRadiusMm, double CenterRadiusMm,
    double TrackProgress, double OffsetMm, double CandidateRadiusMm, double GapMm);

/// <summary>Two visual choices. No physical dimension or confirmation is persisted here.</summary>
public sealed record VisualSelectorSelection(int InnerBand, int OuterBand);

/// <summary>A constant-pitch starting estimate, not a measurement of optical accuracy.</summary>
public sealed record CalibrationVisualEstimate(CalibrationParameters Parameters,
    int InnerBand, int OuterBand, bool IsAtRangeEdge);

/// <summary>
/// Experimental visual null gauge. Each strip integrates a different local radius law
/// into the recorded phase. The viewer chooses the least-winding boundary in each bank.
/// Two local-radius hypotheses determine a coarse constant-pitch radial envelope.
/// This model does not establish whether a person can reliably make those choices.
/// </summary>
public sealed class CalibrationVisualSelector
{
    public const int BandsPerBank = 21;
    private readonly CalibrationParameters _parameters;
    private readonly DvdTrackGeometry _nominal;
    private readonly double _squaredRange;
    private readonly double _phaseScale;
    private readonly double _innerBankStart, _innerBankStep, _outerBankStart, _outerBankStep;

    public CalibrationVisualSelector(CalibrationParameters parameters, double rangeMm = 1)
    {
        ValidateParameters(parameters, rangeMm);
        _parameters = parameters;
        RangeMm = rangeMm;
        _squaredRange = parameters.OuterRadiusMm * parameters.OuterRadiusMm
            - parameters.InnerRadiusMm * parameters.InnerRadiusMm;
        _phaseScale = 2 * parameters.TrackLengthMm / _squaredRange;
        _nominal = DvdTrackGeometry.Create(parameters.InnerRadiusMm, parameters.OuterRadiusMm, parameters.TrackLengthMm);
        double scale = (parameters.OuterRadiusMm - parameters.InnerRadiusMm) / 34;
        _innerBankStart = parameters.InnerRadiusMm + .4 * scale;
        _innerBankStep = 15.9 * scale / BandsPerBank;
        _outerBankStart = parameters.InnerRadiusMm + 17 * scale;
        _outerBankStep = 16.6 * scale / BandsPerBank;
        List<CalibrationVisualBand> bands = [];
        for (int bank = 0; bank < 2; bank++)
        {
            double start = bank == 0 ? _innerBankStart : _outerBankStart;
            double step = bank == 0 ? _innerBankStep : _outerBankStep;
            for (int i = 0; i < BandsPerBank; i++)
            {
                double inner = start + i * step, outer = inner + step, middle = (inner + outer) / 2;
                double progress = (middle * middle - parameters.InnerRadiusMm * parameters.InnerRadiusMm) / _squaredRange;
                double offset = (i - (BandsPerBank - 1) / 2.0) * StepMm;
                // Wider separators after every fifth strip aid counting; they are encoded,
                // unlike preview-only numeric labels which must not warp with the phase.
                double gap = (i + 1) % 5 == 0 ? .11 * scale : .055 * scale;
                bands.Add(new(bank == 0 ? "I" : "O", i + 1, inner, outer, middle,
                    progress, offset, middle + offset, gap));
            }
        }
        Bands = bands.AsReadOnly();
    }

    public double RangeMm { get; }
    public double StepMm => 2 * RangeMm / (BandsPerBank - 1);
    public IReadOnlyList<CalibrationVisualBand> Bands { get; }

    public static void ValidateParameters(CalibrationParameters parameters, double rangeMm)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();
        ValidateRange(rangeMm);
        if (parameters.Kind != CalibrationDiscKind.Dvd || parameters.PitchLinear != 0
            || parameters.PitchQuadratic != 0 || parameters.PitchCubic != 0
            || parameters.OuterRadiusMm > 60 || parameters.InnerRadiusMm <= rangeMm
            || parameters.Sectors > uint.MaxValue || parameters.Sectors % 16 != 0)
            throw new ArgumentException(L.T("Visual selection requires a DVD with constant pitch, complete ECC blocks and radii within 60 mm."));
    }

    public static void ValidateRange(double rangeMm)
    {
        if (!double.IsFinite(rangeMm) || rangeMm is < .001 or > 2)
            throw new ArgumentException(L.T("Choose a visual selector range between 0.001 and 2 mm."));
    }

    public CalibrationVisualBand? BandAtProgress(double progress)
    {
        if (!double.IsFinite(progress) || progress is < 0 or > 1) return null;
        double radius = NominalRadius(progress);
        double innerIndex = (radius - _innerBankStart) / _innerBankStep;
        double outerIndex = (radius - _outerBankStart) / _outerBankStep;
        int index = innerIndex is >= 0 and < BandsPerBank ? (int)innerIndex
            : outerIndex is >= 0 and < BandsPerBank ? BandsPerBank + (int)outerIndex : -1;
        if (index < 0) return null;
        CalibrationVisualBand band = Bands[index];
        return radius >= band.InnerRadiusMm + band.GapMm
            && radius <= band.OuterRadiusMm - band.GapMm ? band : null;
    }

    /// <summary>Analytic candidate phase; q is the global recording fraction, including headers/ECC/SYNC.</summary>
    public double CandidatePhaseAtProgress(double progress)
    {
        CalibrationVisualBand band = BandAtProgress(progress)
            ?? throw new ArgumentException(L.T("The recording position is outside a visual selector band."));
        return CandidatePhase(progress, band);
    }

    /// <summary>Binary texture sampled directly at a payload codeword's recording position.</summary>
    public byte SampleRecordingProgress(double progress)
    {
        CalibrationVisualBand? band = BandAtProgress(progress);
        return band is null ? (byte)255 : Binary(CandidatePhase(progress, band));
    }

    /// <summary>
    /// Equivalent source-coordinate pattern. The DVD encoder passes +trackAngle in its
    /// Y-down sampling coordinates; subtracting that nominal phase leaves the candidate.
    /// A raster export is for inspection, not an encoding intermediate.
    /// </summary>
    public byte SampleSource(double xMm, double yMm)
    {
        if (!TrySource(xMm, yMm, out double radius, out double progress, out CalibrationVisualBand? band)) return 255;
        double phase = Math.Atan2(yMm, xMm) - _nominal.AngleAtRadius(radius) + CandidatePhase(progress, band!);
        return Binary(phase);
    }

    /// <summary>
    /// Nominal preview with pixel-footprint filtering. Gaussian filtering of the local
    /// square-wave phase suppresses unresolved spiral aliases; 2x2 coverage softens band
    /// edges. This is a display approximation, not measured disc brightness or burn data.
    /// </summary>
    public byte SamplePreview(double xMm, double yMm, double footprintMm)
    {
        if (!double.IsFinite(footprintMm) || footprintMm < 0)
            throw new ArgumentOutOfRangeException(nameof(footprintMm));
        if (footprintMm == 0) return SampleSource(xMm, yMm);
        double sum = 0;
        for (int j = 0; j < 2; j++)
        for (int i = 0; i < 2; i++)
        {
            double x = xMm + (i == 0 ? -.25 : .25) * footprintMm;
            double y = yMm + (j == 0 ? -.25 : .25) * footprintMm;
            if (!TrySource(x, y, out double radius, out double q, out CalibrationVisualBand? band))
            { sum += 255; continue; }
            double phase = Math.Atan2(y, x) - _nominal.AngleAtRadius(radius) + CandidatePhase(q, band!);
            double radialDerivative = _phaseScale * (radius / (NominalRadius(q) + band!.OffsetMm) - 1);
            // d(theta)/dr is zero along a radial line, while the tangential gradient is 1/r.
            double sigmaPhase = footprintMm / Math.Sqrt(12)
                * Math.Sqrt(radialDerivative * radialDerivative + 1 / (radius * radius));
            double value = 0;
            for (int harmonic = 1; harmonic <= 23; harmonic += 2)
            {
                double attenuation = Math.Exp(-.5 * harmonic * harmonic * sigmaPhase * sigmaPhase);
                double sign = (harmonic & 3) == 1 ? 1 : -1;
                value += sign * Math.Cos(harmonic * phase) / harmonic * attenuation;
            }
            sum += Math.Clamp(127.5 - 127.5 * 4 / Math.PI * value, 0, 255);
        }
        return (byte)Math.Clamp((int)Math.Round(sum / 4), 0, 255);
    }

    public CalibrationVisualEstimate Estimate(int innerBand, int outerBand)
    {
        ValidateSelection(new(innerBand, outerBand));
        CalibrationVisualBand inner = Bands[innerBand - 1], outer = Bands[BandsPerBank + outerBand - 1];
        double slope = (outer.CandidateRadiusMm * outer.CandidateRadiusMm - inner.CandidateRadiusMm * inner.CandidateRadiusMm)
            / (outer.TrackProgress - inner.TrackProgress);
        double intercept = inner.CandidateRadiusMm * inner.CandidateRadiusMm - inner.TrackProgress * slope;
        if (!double.IsFinite(intercept + slope) || intercept <= 0 || slope <= 0
            || intercept + slope > 60 * 60)
            throw new ArgumentException(L.T("These band choices do not give valid DVD radii. Check both band numbers."));
        CalibrationParameters estimate = _parameters with
        {
            InnerRadiusMm = Math.Sqrt(intercept), OuterRadiusMm = Math.Sqrt(intercept + slope),
            PitchLinear = 0, PitchQuadratic = 0, PitchCubic = 0,
        };
        estimate.Validate();
        return new(estimate, innerBand, outerBand,
            innerBand is 1 or BandsPerBank || outerBand is 1 or BandsPerBank);
    }

    public static void ValidateSelection(VisualSelectorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.InnerBand is < 1 or > BandsPerBank || selection.OuterBand is < 1 or > BandsPerBank)
            throw new ArgumentException(L.T("Choose one band from 1 to 21 in each visual selector bank."));
    }

    private bool TrySource(double x, double y, out double radius, out double progress, out CalibrationVisualBand? band)
    {
        radius = Math.Sqrt(x * x + y * y); progress = double.NaN; band = null;
        if (!double.IsFinite(radius) || radius < _parameters.InnerRadiusMm || radius > _parameters.OuterRadiusMm) return false;
        progress = _nominal.ArcLengthAtRadius(radius) / _parameters.TrackLengthMm;
        band = BandAtProgress(progress);
        return band is not null;
    }

    private double NominalRadius(double progress)
        => Math.Sqrt(_parameters.InnerRadiusMm * _parameters.InnerRadiusMm + _squaredRange * progress);

    private double CandidatePhase(double progress, CalibrationVisualBand band)
    {
        double radius = NominalRadius(progress), deltaRadius = radius - band.CenterRadiusMm;
        double logarithm = band.OffsetMm == 0 ? 0 : Math.Log(1 + deltaRadius / band.CandidateRadiusMm);
        return _phaseScale * (band.CenterRadiusMm - _parameters.InnerRadiusMm
            + deltaRadius - band.OffsetMm * logarithm);
    }

    private static byte Binary(double phase) => Math.Cos(phase) >= 0 ? (byte)0 : (byte)255;
}
