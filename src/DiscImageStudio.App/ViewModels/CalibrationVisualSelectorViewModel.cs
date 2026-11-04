using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;

namespace DiscImageStudio.ViewModels;

public partial class CalibrationViewModel
{
    private CalibrationVisualEstimate? _visualSelectorEstimate;
    [ObservableProperty] private bool _useVisualSelectorPattern;
    [ObservableProperty] private int? _selectedInnerVisualBand;
    [ObservableProperty] private int? _selectedOuterVisualBand;
    [ObservableProperty] private string _visualSelectorEstimateSummary = string.Empty;
    [ObservableProperty] private VisualSelectorRangeChoice _selectedVisualSelectorRange = VisualSelectorRanges[0];
    private static readonly IReadOnlyList<VisualSelectorRangeChoice> VisualSelectorRanges = [new(1), new(.2), new(.05), new(2)];

    public ObservableCollection<VisualSelectorRangeChoice> VisualSelectorRangeChoices { get; } = new(VisualSelectorRanges);
    public IReadOnlyList<int> VisualSelectorBandChoices { get; } = Enumerable.Range(1, CalibrationVisualSelector.BandsPerBank).ToArray();
    public bool CanSelectVisualSelectorPattern => SelectedDiscType == "DVD";
    public bool IsVisualSelectorWorkflow => _target?.IsVisualSelectorPattern == true;
    public bool IsConventionalCalibrationWorkflow => !IsVisualSelectorWorkflow;
    public bool CanUseCurrentCalibrationSource => CanGenerateTarget();
    internal bool IsSelectedPatternCurrent => _target is not null && _target.Options.PatternKind == SelectedPatternKind
        && (!IsVisualSelectorWorkflow || (_target.VisualSelector!.RangeMm == SelectedVisualSelectorRange.RangeMm
            && CanSelectVisualSelectorPattern));
    public string VisualSelectorSnapshotSummary => _target?.VisualSelector is { } selector
        ? L.Format("VisualSelector.SavedRange", selector.RangeMm, selector.StepMm) : string.Empty;
    public RelayCommand ComputeVisualSelectorEstimateCommand { get; private set; } = null!;
    public RelayCommand ApplyVisualSelectorEstimateCommand { get; private set; } = null!;

    private void InitializeVisualSelectorCommands()
    {
        ComputeVisualSelectorEstimateCommand = new RelayCommand(ComputeVisualSelectorEstimate,
            () => CanGenerateTarget() && IsVisualSelectorWorkflow && SelectedInnerVisualBand is >= 1 and <= 21
                && SelectedOuterVisualBand is >= 1 and <= 21);
        ApplyVisualSelectorEstimateCommand = new RelayCommand(() =>
        {
            if (CanApplyVisualSelectorEstimate() && _visualSelectorEstimate is { } estimate)
                ApplyCalibrationParameters(estimate.Parameters);
        }, CanApplyVisualSelectorEstimate);
    }

    private bool CanApplyVisualSelectorEstimate() => CanGenerateTarget() && IsVisualSelectorWorkflow
        && _visualSelectorEstimate is { IsAtRangeEdge: false } estimate
        && estimate.InnerBand == SelectedInnerVisualBand && estimate.OuterBand == SelectedOuterVisualBand;

    partial void OnUseVisualSelectorPatternChanged(bool value)
    {
        if (value && !CanSelectVisualSelectorPattern) { UseVisualSelectorPattern = false; return; }
        if (value) { UseCoarsePattern = false; UseCirclePattern = false; }
        InvalidateVisualSelectorEstimate();
        OnPropertyChanged(nameof(ActivePatternSummary));
        OnPropertyChanged(nameof(CanUseCurrentCalibrationSource));
        RefreshCommands();
    }

    partial void OnSelectedVisualSelectorRangeChanged(VisualSelectorRangeChoice value)
    {
        if (!_installing) InvalidateVisualSelectorEstimate();
        OnPropertyChanged(nameof(ActivePatternSummary));
        OnPropertyChanged(nameof(CanUseCurrentCalibrationSource));
        RefreshCommands();
    }

    partial void OnSelectedInnerVisualBandChanged(int? value) => OnVisualBandChanged();
    partial void OnSelectedOuterVisualBandChanged(int? value) => OnVisualBandChanged();
    private void OnVisualBandChanged()
    {
        InvalidateVisualSelectorEstimate();
        ComputeVisualSelectorEstimateCommand?.NotifyCanExecuteChanged();
    }

    private void InvalidateVisualSelectorEstimate()
    {
        _visualSelectorEstimate = null;
        VisualSelectorEstimateSummary = string.Empty;
        OnPropertyChanged(nameof(StepTwoState));
        OnPropertyChanged(nameof(StepThreeState));
        ApplyVisualSelectorEstimateCommand?.NotifyCanExecuteChanged();
    }

    private void ResetVisualSelector()
    {
        SelectedInnerVisualBand = null;
        SelectedOuterVisualBand = null;
        InvalidateVisualSelectorEstimate();
        while (VisualSelectorRangeChoices.Count > VisualSelectorRanges.Count)
            VisualSelectorRangeChoices.RemoveAt(VisualSelectorRangeChoices.Count - 1);
        if (_target?.VisualSelector is { } selector)
        {
            var choice = VisualSelectorRangeChoices.FirstOrDefault(c => c.RangeMm == selector.RangeMm);
            if (choice is null) { choice = new(selector.RangeMm); VisualSelectorRangeChoices.Add(choice); }
            SelectedVisualSelectorRange = choice;
            ShowTargetPreview = true;
        }
        NotifyVisualWorkflow();
    }

    private void NotifyVisualWorkflow()
    {
        OnPropertyChanged(nameof(IsVisualSelectorWorkflow));
        OnPropertyChanged(nameof(IsConventionalCalibrationWorkflow));
        OnPropertyChanged(nameof(UsePhotoWorkflow));
        OnPropertyChanged(nameof(ShowPhotoBoundaryTools));
        OnPropertyChanged(nameof(ShowPhotoTraceTools));
        OnPropertyChanged(nameof(CalibrationStageTitle));
        OnPropertyChanged(nameof(RingStageInstruction));
        OnPropertyChanged(nameof(CalibrationCanvasHint));
        OnPropertyChanged(nameof(SecondStepTitle));
        OnPropertyChanged(nameof(ThirdStepTitle));
        OnPropertyChanged(nameof(VisualSelectorSnapshotSummary));
        OnPropertyChanged(nameof(CanUseCurrentCalibrationSource));
        OnPropertyChanged(nameof(StepTwoState));
        OnPropertyChanged(nameof(StepThreeState));
    }

    private void ComputeVisualSelectorEstimate()
    {
        InvalidateVisualSelectorEstimate();
        if (!ComputeVisualSelectorEstimateCommand.CanExecute(null) || _target?.VisualSelector is not { } selector) return;
        try
        {
            _visualSelectorEstimate = selector.Estimate(SelectedInnerVisualBand!.Value, SelectedOuterVisualBand!.Value);
            UpdateVisualSelectorEstimateSummary();
            StepThreeExpanded = true;
            OnPropertyChanged(nameof(StepTwoState));
            OnPropertyChanged(nameof(StepThreeState));
            ApplyVisualSelectorEstimateCommand.NotifyCanExecuteChanged();
        }
        catch (ArgumentException ex) { Report(ex); VisualSelectorEstimateSummary = ex.Message; }
    }

    private void UpdateVisualSelectorEstimateSummary()
    {
        if (_visualSelectorEstimate is not { } estimate) return;
        VisualSelectorEstimateSummary = L.Format("VisualSelector.Result", estimate.InnerBand, estimate.OuterBand,
            estimate.Parameters.InnerRadiusMm, estimate.Parameters.OuterRadiusMm)
            + "\n" + L.T(estimate.IsAtRangeEdge ? "VisualSelector.EdgeSelection" : "VisualSelector.EstimateLimit");
    }

    private VisualSelectorSelection? ReadVisualSelectorSelection() => IsVisualSelectorWorkflow
        && SelectedInnerVisualBand is { } inner && SelectedOuterVisualBand is { } outer ? new(inner, outer) : null;

    private void RestoreVisualSelectorSelection(CalibrationSession session)
    {
        if (session.VisualSelectorSelection is { } selection)
        {
            SelectedInnerVisualBand = selection.InnerBand;
            SelectedOuterVisualBand = selection.OuterBand;
        }
        if (IsVisualSelectorWorkflow) { ShowTargetPreview = true; StepTwoExpanded = true; }
        InvalidateVisualSelectorEstimate();
    }

    private void RelocalizeVisualSelector()
    {
        NotifyVisualWorkflow();
        UpdateVisualSelectorEstimateSummary();
    }
}

public sealed record VisualSelectorRangeChoice(double RangeMm)
{
    public string Label => "±" + RangeMm.ToString("0.###", CultureInfo.InvariantCulture) + " mm";
}
