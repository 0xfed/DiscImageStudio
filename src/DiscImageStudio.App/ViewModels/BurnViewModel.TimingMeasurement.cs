using DiscImageStudio.Localization;
using DiscImageStudio.Services;

namespace DiscImageStudio.ViewModels;

public partial class BurnViewModel
{
    private TimingMeasurementSource? _timingMeasurementSource;
    public bool IsTimingMeasurement => _timingMeasurementSource is not null;

    internal void PrepareTimingMeasurement(TimingMeasurementSource source)
    {
        _timingMeasurementSource = source;
        OnFamilyInputsChanged();
    }

    internal void PrepareCalibrationPattern()
    {
        _timingMeasurementSource = null;
        OnFamilyInputsChanged();
    }

    private static string DescribeTimingMeasurement(long contentLength)
        => L.Format("Timing.BurnSourceDetails", contentLength / 2048, contentLength / (1024.0 * 1024.0));
}
