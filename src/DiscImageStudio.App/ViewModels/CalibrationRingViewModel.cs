using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscImageStudio.Controls;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;

namespace DiscImageStudio.ViewModels;

public partial class CalibrationViewModel
{
    private CalibrationRingFitResult? _ringFit;
    [ObservableProperty] private bool _useCirclePattern;
    [ObservableProperty] private bool _useRingMeasurements;
    [ObservableProperty] private string _diameterTolerance = "0.10";
    [ObservableProperty] private bool _ringMeasurementsConfirmed;
    [ObservableProperty] private string _ringFitSummary = string.Empty;
    public ObservableCollection<RingDiameterRow> RingDiameters { get; } = [];
    public bool HasMeasurableRings => RingDiameters.Count >= 3;
    public bool CanSelectRingMeasurementMethod => HasMeasurableRings && _target?.IsRingPattern != true;
    public bool UsePhotoWorkflow => !UseRingMeasurements;
    public bool ShowPhotoBoundaryTools => UsePhotoWorkflow && IsPhotoStep;
    public bool ShowPhotoTraceTools => UsePhotoWorkflow && IsTraceStep;
    public string CalibrationStageTitle => UseRingMeasurements ? L.T("Calibration circles") : L.T("Xaml.CalibrationPhoto");
    public string? RingStageInstruction => UseRingMeasurements ? L.T("Circle preview · enter measured diameters.") : null;
    public string CalibrationCanvasHint => L.T(UseRingMeasurements ? "Xaml.RingMeasurementHelp" : "Xaml.ScrollToZoomRightDragToPanDragPoints");
    public string SecondStepTitle => UseRingMeasurements ? L.T("Measure ring diameters") : L.T("Xaml.RectifyPhoto");
    public string ThirdStepTitle => UseRingMeasurements ? L.T("Check and apply radii") : L.T("Xaml.TraceAndSolve");
    public RelayCommand SolveRingsCommand { get; private set; } = null!;
    public RelayCommand ApplyRingParametersCommand { get; private set; } = null!;
    public RelayCommand ReviewRingMeasurementsCommand { get; private set; } = null!;
    private CalibrationPatternKind SelectedPatternKind => UseCirclePattern ? CalibrationPatternKind.ConcentricRings
        : UseCoarsePattern ? CalibrationPatternKind.WideSectors : CalibrationPatternKind.FineCross;

    private void InitializeRingCommands()
    {
        ReviewRingMeasurementsCommand = new RelayCommand(() => StepTwoExpanded = true, () => !IsBusy);
        SolveRingsCommand = new RelayCommand(SolveRings, () => CanUseTarget() && UseRingMeasurements && HasMeasurableRings);
        ApplyRingParametersCommand = new RelayCommand(() =>
        {
            if (CanApplyRingParameters() && _ringFit is { } fit) ApplyCalibrationParameters(fit.Parameters);
        }, CanApplyRingParameters);
    }

    partial void OnUseCirclePatternChanged(bool value)
    {
        if (value) UseCoarsePattern = false;
        OnPropertyChanged(nameof(ActivePatternSummary));
        RefreshCommands();
    }

    partial void OnUseRingMeasurementsChanged(bool value)
    {
        RingMeasurementsConfirmed = false;
        ShowTargetPreview = value;
        OnPropertyChanged(nameof(UsePhotoWorkflow));
        OnPropertyChanged(nameof(ShowPhotoBoundaryTools)); OnPropertyChanged(nameof(ShowPhotoTraceTools));
        OnPropertyChanged(nameof(CalibrationStageTitle));
        OnPropertyChanged(nameof(RingStageInstruction)); OnPropertyChanged(nameof(CalibrationCanvasHint));
        OnPropertyChanged(nameof(SecondStepTitle)); OnPropertyChanged(nameof(ThirdStepTitle));
        OnPropertyChanged(nameof(StepTwoState)); OnPropertyChanged(nameof(StepThreeState));
        RefreshCommands();
    }
    partial void OnDiameterToleranceChanged(string value) => InvalidateRingFit();
    partial void OnRingMeasurementsConfirmedChanged(bool value) => ApplyRingParametersCommand?.NotifyCanExecuteChanged();

    private bool CanApplyRingParameters() => !IsBusy && UseRingMeasurements && RingMeasurementsConfirmed
        && _ringFit is { } fit && double.TryParse(DiameterTolerance, NumberStyles.Float, CultureInfo.InvariantCulture, out double tolerance)
        && tolerance is >= .001 and <= 2 && fit.IsWithinTolerance(tolerance);

    private void ResetRingMeasurements()
    {
        RingDiameters.Clear();
        if (_target is { } target)
            foreach (CalibrationRing ring in CalibrationRingFitter.GetRings(target))
                RingDiameters.Add(new(ring, InvalidateRingFit));
        DiameterTolerance = "0.10";
        UseRingMeasurements = _target?.IsRingPattern == true;
        InvalidateRingFit();
        OnPropertyChanged(nameof(HasMeasurableRings));
        OnPropertyChanged(nameof(CanSelectRingMeasurementMethod));
    }

    private void InvalidateRingFit()
    {
        _ringFit = null;
        RingMeasurementsConfirmed = false;
        RingFitSummary = string.Empty;
        foreach (RingDiameterRow row in RingDiameters) row.Residual = string.Empty;
        OnPropertyChanged(nameof(StepTwoState)); OnPropertyChanged(nameof(StepThreeState));
        ApplyRingParametersCommand?.NotifyCanExecuteChanged();
    }

    private List<CalibrationRingMeasurement>? ReadRingMeasurements()
    {
        List<CalibrationRingMeasurement> result = [];
        foreach (RingDiameterRow row in RingDiameters.Where(row => !string.IsNullOrWhiteSpace(row.MeasuredDiameter)))
        {
            if (!double.TryParse(row.MeasuredDiameter, NumberStyles.Float, CultureInfo.InvariantCulture, out double diameter)
                || !double.IsFinite(diameter) || diameter <= 0 || diameter > 2000)
                throw new ArgumentException(L.T("Check the ring numbers and positive measured diameters in millimetres."));
            result.Add(new(row.Ring.Index, diameter));
        }
        return result.Count == 0 ? null : result;
    }

    private double ReadDiameterTolerance()
    {
        if (!double.TryParse(DiameterTolerance, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || !double.IsFinite(value) || value is < .001 or > 2)
            throw new ArgumentException(L.T("Enter a diameter tolerance between 0.001 and 2 mm."));
        return value;
    }

    private void SolveRings()
    {
        InvalidateRingFit();
        try
        {
            if (_target is null || !UseRingMeasurements) return;
            _ = ReadDiameterTolerance();
            _ringFit = CalibrationRingFitter.Fit(_target, ReadRingMeasurements() ?? []);
            foreach (CalibrationRingResidual residual in _ringFit.Residuals)
                RingDiameters.Single(row => row.Ring.Index == residual.RingIndex).Residual
                    = L.Format("Diameter residual: {0:0.###} mm", residual.ErrorMm);
            UpdateRingFitSummary();
            StepThreeExpanded = true;
            OnPropertyChanged(nameof(StepTwoState)); OnPropertyChanged(nameof(StepThreeState));
            ApplyRingParametersCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { Report(ex); RingFitSummary = ex.Message; }
    }

    private void UpdateRingFitSummary()
    {
        if (_ringFit is not { } fit) return;
        RingFitSummary = L.Format("Estimated inner radius: {0:0.0000} mm; outer radius: {1:0.0000} mm.",
            fit.Parameters.InnerRadiusMm, fit.Parameters.OuterRadiusMm)
            + "\n" + L.Format("Diameter residuals: RMS {0:0.###} mm; maximum {1:0.###} mm.",
                fit.RmsDiameterErrorMm, fit.MaxDiameterErrorMm)
            + (fit.IsWithinTolerance(ReadDiameterTolerance()) ? string.Empty
                : "\n" + L.T("A diameter residual exceeds the tolerance. Check measurements and ring identities before applying."));
    }

    private void RestoreRingMeasurements(CalibrationSession session)
    {
        if (session.RingMeasurements is { } values)
            foreach (CalibrationRingMeasurement value in values)
                RingDiameters.Single(row => row.Ring.Index == value.RingIndex).MeasuredDiameter
                    = value.DiameterMm.ToString("G", CultureInfo.InvariantCulture);
        DiameterTolerance = (session.RingDiameterToleranceMm ?? .1).ToString("G", CultureInfo.InvariantCulture);
        UseRingMeasurements = session.UseRingMeasurements || session.Target.IsRingPattern;
        if (UseRingMeasurements) { ShowTargetPreview = true; StepTwoExpanded = true; }
    }

    private void RelocalizeRings()
    {
        OnPropertyChanged(nameof(SecondStepTitle)); OnPropertyChanged(nameof(ThirdStepTitle));
        OnPropertyChanged(nameof(CalibrationStageTitle));
        OnPropertyChanged(nameof(RingStageInstruction)); OnPropertyChanged(nameof(CalibrationCanvasHint));
        foreach (RingDiameterRow row in RingDiameters) row.Relocalize();
        UpdateRingFitSummary();
        if (_ringFit is { } fit)
            foreach (CalibrationRingResidual residual in fit.Residuals)
                RingDiameters.Single(row => row.Ring.Index == residual.RingIndex).Residual
                    = L.Format("Diameter residual: {0:0.###} mm", residual.ErrorMm);
    }
}

public sealed partial class RingDiameterRow(CalibrationRing ring, Action changed) : ObservableObject
{
    public CalibrationRing Ring { get; } = ring;
    public string Label => L.Format("Ring {0} · generated diameter {1:0.###} mm", Ring.Index, Ring.NominalDiameterMm);
    [ObservableProperty] private string _measuredDiameter = string.Empty;
    [ObservableProperty] private string _residual = string.Empty;
    partial void OnMeasuredDiameterChanged(string value) => changed();
    public void Relocalize() => OnPropertyChanged(nameof(Label));
}
