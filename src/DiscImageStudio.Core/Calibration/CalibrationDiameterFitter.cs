using DiscImageStudio.Localization;

namespace DiscImageStudio.Core.Calibration;

public sealed record CalibrationDiameterRing(int Index, double SourceRadiusMm, double TrackProgress)
{
    public double NominalDiameterMm => 2 * SourceRadiusMm;
}

public sealed record CalibrationRingMeasurement(int RingIndex, double DiameterMm);
public sealed record CalibrationRingResidual(int RingIndex, double MeasuredDiameterMm, double PredictedDiameterMm)
{
    public double ErrorMm => PredictedDiameterMm - MeasuredDiameterMm;
}

public sealed record CalibrationRingFitResult(CalibrationParameters Parameters,
    IReadOnlyList<CalibrationRingResidual> Residuals, double RmsDiameterErrorMm, double MaxDiameterErrorMm)
{
    public bool IsWithinTolerance(double diameterToleranceMm) => MaxDiameterErrorMm <= diameterToleranceMm;
}

/// <summary>Fits the radial envelope at saved recording positions, independently of angular phase.
/// Sector count, linear density and the saved pitch shape remain fixed.</summary>
public static class CalibrationDiameterFitter
{
    public static IReadOnlyList<CalibrationDiameterRing> GetRings(CalibrationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        // Wide address bands use their own constant-pitch edge model. Their centres
        // must not be interpreted as thin circles under the saved fine-pitch shape.
        if (!target.IsRingPattern && !target.UsesReferencePattern) return [];
        var source = new CalibrationGeometry(target.Parameters);
        double halfWidth = target.RingThicknessMm / 2;
        return target.RingRadiiMm.Where(radius => radius - halfWidth >= target.DrawingInnerRadiusMm
                && radius + halfWidth <= target.DrawingOuterRadiusMm)
            .Select((radius, index) => new CalibrationDiameterRing(index + 1, radius, source.ProgressAtRadius(radius))).ToArray();
    }

    public static void ValidateMeasurements(CalibrationTarget target, IReadOnlyList<CalibrationRingMeasurement> measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);
        var rings = GetRings(target).ToDictionary(ring => ring.Index);
        if (measurements.Count > rings.Count || measurements.Any(value => value is null
                || !rings.ContainsKey(value.RingIndex) || !double.IsFinite(value.DiameterMm)
                || value.DiameterMm <= 0 || value.DiameterMm > 2000)
            || measurements.Select(value => value.RingIndex).Distinct().Count() != measurements.Count)
            throw new ArgumentException(L.T("Check the ring numbers and positive measured diameters in millimetres."));
    }

    public static CalibrationRingFitResult Fit(CalibrationTarget target, IReadOnlyList<CalibrationRingMeasurement> measurements)
    {
        ValidateMeasurements(target, measurements);
        if (measurements.Count < 3)
            throw new ArgumentException(L.T("Enter diameters for at least three rings."));
        var rings = GetRings(target).ToDictionary(ring => ring.Index);
        var data = measurements.OrderBy(value => value.RingIndex).Select(value =>
            (Progress: rings[value.RingIndex].TrackProgress, Measurement: value)).ToArray();
        if (data[^1].Progress - data[0].Progress < .5)
            throw new ArgumentException(L.T("Measure rings spanning at least half of the recording progress, including inner and outer rings."));
        if (data.Zip(data.Skip(1), (a, b) => b.Measurement.DiameterMm <= a.Measurement.DiameterMm).Any(value => value))
            throw new ArgumentException(L.T("Measured diameters must increase from the innermost ring outward."));

        // For a tightly wound constant-pitch spiral, r² is affine in recording progress.
        // This supplies an initial estimate; exact saved geometry refines both endpoints below.
        double meanQ = data.Average(value => value.Progress);
        double meanR2 = data.Average(value => value.Measurement.DiameterMm * value.Measurement.DiameterMm / 4);
        double slope = data.Sum(value => (value.Progress - meanQ)
            * (value.Measurement.DiameterMm * value.Measurement.DiameterMm / 4 - meanR2))
            / data.Sum(value => (value.Progress - meanQ) * (value.Progress - meanQ));
        double intercept = meanR2 - slope * meanQ;
        if (intercept <= 0 || slope <= 0 || !double.IsFinite(intercept + slope))
            throw new ArgumentException(L.T("These diameters do not define valid inner and outer radii. Check ring identities and measurements."));
        double inner = Math.Sqrt(intercept), outer = Math.Sqrt(intercept + slope);
        if (target.Parameters.Kind == CalibrationDiscKind.BluRay) outer = Math.Min(outer, 60);
        CalibrationParameters Parameters(double ri, double ro) => target.Parameters with { InnerRadiusMm = ri, OuterRadiusMm = ro };
        double[] Predict(double ri, double ro)
        {
            var parameters = Parameters(ri, ro);
            parameters.Validate();
            var geometry = new CalibrationGeometry(parameters);
            return data.Select(value => 2 * geometry.AtProgress(value.Progress).Radius).ToArray();
        }
        double Cost(double[] prediction) => prediction.Select((value, index) =>
            Math.Pow(value - data[index].Measurement.DiameterMm, 2)).Sum();

        double[] prediction = Predict(inner, outer);
        double cost = Cost(prediction), damping = 1e-8;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            double h = Math.Min(1e-5, (outer - inner) / 8);
            double[] innerPrediction = Predict(inner + h, outer);
            // A backward difference stays inside BD's physical 60 mm maximum.
            double[] outerPrediction = Predict(inner, outer - h);
            double aa = 0, ab = 0, bb = 0, ga = 0, gb = 0;
            for (int index = 0; index < data.Length; index++)
            {
                double a = (innerPrediction[index] - prediction[index]) / h;
                double b = (prediction[index] - outerPrediction[index]) / h;
                double error = prediction[index] - data[index].Measurement.DiameterMm;
                aa += a * a; ab += a * b; bb += b * b; ga += a * error; gb += b * error;
            }
            double determinant = (aa + damping) * (bb + damping) - ab * ab;
            if (determinant <= 1e-20) break;
            double di = (-(bb + damping) * ga + ab * gb) / determinant;
            double dout = (ab * ga - (aa + damping) * gb) / determinant;
            bool improved = false;
            for (double scale = 1; scale >= 1.0 / 4096; scale /= 2)
            {
                try
                {
                    double[] next = Predict(inner + di * scale, outer + dout * scale);
                    double nextCost = Cost(next);
                    if (nextCost >= cost) continue;
                    inner += di * scale; outer += dout * scale;
                    prediction = next; cost = nextCost; improved = true;
                    break;
                }
                catch (ArgumentException) { }
            }
            if (!improved || Math.Max(Math.Abs(di), Math.Abs(dout)) < 1e-10) break;
        }
        var residuals = data.Select((value, index) => new CalibrationRingResidual(value.Measurement.RingIndex,
            value.Measurement.DiameterMm, prediction[index])).ToArray();
        return new(Parameters(inner, outer), residuals, Math.Sqrt(cost / data.Length), residuals.Max(value => Math.Abs(value.ErrorMm)));
    }
}
