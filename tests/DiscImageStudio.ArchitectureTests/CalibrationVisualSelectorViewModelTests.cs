using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using DiscImageStudio.Controls;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.ViewModels;

internal static class CalibrationVisualSelectorViewModelTests
{
    internal static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "disc-visual-selector-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var state = new DiscParametersState { DvdPitchLinear = "0.001" };
            var model = Create(state);
            model.UseVisualSelectorPattern = true;
            Require(!model.UseVisualSelectorPattern && !model.CanSelectVisualSelectorPattern,
                "visual selector cannot be enabled for a CD project");
            model.SelectedDiscType = "DVD";
            model.UseVisualSelectorPattern = true;
            model.CreateTargetCommand.Execute(null);
            var target = model.Target ?? throw new InvalidOperationException(model.LastStatusMessage);
            Require(target.IsVisualSelectorPattern && model.IsVisualSelectorWorkflow && model.ShowTargetPreview
                && !model.UsePhotoWorkflow && !model.UseRingMeasurements && model.RingDiameters.Count == 0,
                "visual targets enter the band-selection workflow without measurement inputs");
            Require(target.Parameters.PitchLinear == 0 && state.DvdPitchLinear == "0.001",
                "target snapshots constant pitch without changing project parameters");
            Require(model.StreamBurnCommand.CanExecute(null) && model.GenerateImageCommand.CanExecute(null)
                && !model.ExportPngCommand.CanExecute(null) && !model.ImportPhotoCommand.CanExecute(null)
                && !model.SolveCommand.CanExecute(null) && !model.ApplyRingParametersCommand.CanExecute(null),
                "direct output is usable while raster export and unrelated calibration methods are disabled");
            Require(model.SelectedInnerVisualBand is null && model.SelectedOuterVisualBand is null
                && !model.ComputeVisualSelectorEstimateCommand.CanExecute(null), "both banks require deliberate selections");
            model.SelectedInnerVisualBand = 11;
            Require(!model.ComputeVisualSelectorEstimateCommand.CanExecute(null), "one bank cannot determine both radii");
            model.SelectedOuterVisualBand = 11;
            model.ComputeVisualSelectorEstimateCommand.Execute(null);
            Require(model.ApplyVisualSelectorEstimateCommand.CanExecute(null)
                && !string.IsNullOrWhiteSpace(model.VisualSelectorEstimateSummary), "a complete interior pair produces an applicable coarse estimate");
            model.ApplyVisualSelectorEstimateCommand.Execute(null);
            Require(Math.Abs(double.Parse(state.DvdInnerRadius, CultureInfo.InvariantCulture) - target.Parameters.InnerRadiusMm) < 1e-6
                && Math.Abs(double.Parse(state.DvdOuterRadius, CultureInfo.InvariantCulture) - target.Parameters.OuterRadiusMm) < 1e-6
                && state.DvdPitchLinear == "0" && ReferenceEquals(model.Target, target),
                "applying writes starting radii and zero pitch while preserving the burn snapshot");

            string path = Path.Combine(folder, "visual.calibration.json");
            model.SaveSessionTo(path);
            var session = CalibrationSessionJson.Load(path);
            Require(session.VisualSelectorSelection == new VisualSelectorSelection(11, 11)
                && session.Photo is null && session.RingMeasurements is null && !session.UseRingMeasurements,
                "a visual-selection session requires neither photograph nor measured dimensions");
            var reopened = Create(new DiscParametersState());
            ((Task)typeof(CalibrationViewModel).GetMethod("LoadSessionFromAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(reopened, [path])!).GetAwaiter().GetResult();
            Require(reopened.Target?.Id == target.Id && reopened.IsVisualSelectorWorkflow
                && reopened.SelectedInnerVisualBand == 11 && reopened.SelectedOuterVisualBand == 11
                && reopened.ShowTargetPreview && !reopened.ApplyVisualSelectorEstimateCommand.CanExecute(null),
                "reopening restores choices and requires recalculation rather than restoring an applied estimate");
            reopened.ComputeVisualSelectorEstimateCommand.Execute(null);
            reopened.SelectedInnerVisualBand = 10;
            Require(!reopened.ApplyVisualSelectorEstimateCommand.CanExecute(null)
                && reopened.VisualSelectorEstimateSummary.Length == 0, "changing either bank invalidates the estimate");
            reopened.SelectedInnerVisualBand = 1;
            reopened.ComputeVisualSelectorEstimateCommand.Execute(null);
            Require(!reopened.ApplyVisualSelectorEstimateCommand.CanExecute(null), "edge-band selections cannot be applied");
            reopened.SelectedVisualSelectorRange = reopened.VisualSelectorRangeChoices.Single(r => r.RangeMm == 2);
            Require(!reopened.StreamBurnCommand.CanExecute(null) && !reopened.GenerateImageCommand.CanExecute(null)
                && !reopened.ComputeVisualSelectorEstimateCommand.CanExecute(null),
                "changing the range blocks stale output and lookup until a new target is created");
            reopened.CreateTargetCommand.Execute(null);
            Require(reopened.Target!.VisualSelector!.RangeMm == 2 && reopened.SelectedInnerVisualBand is null
                && reopened.SelectedOuterVisualBand is null && reopened.StreamBurnCommand.CanExecute(null),
                "a new range is captured in the new target and clears old selections");
            reopened.UseCoarsePattern = true;
            Require(!reopened.UseVisualSelectorPattern && !reopened.StreamBurnCommand.CanExecute(null),
                "pattern choices are mutually exclusive and do not silently change an existing burn target");
            reopened.CreateTargetCommand.Execute(null);
            Require(!reopened.IsVisualSelectorWorkflow && reopened.UsePhotoWorkflow && reopened.Target!.IsCoarsePattern
                && reopened.ImportPhotoCommand.CanExecute(null), "wide-sector refinement restores the established photo workflow");
        }
        finally { Directory.Delete(folder, recursive: true); }
        Console.WriteLine("calibration-visual-selector-view-model: passed (DVD-only selection, no measurements, snapshot, lookup/apply, reload, edge rejection, range invalidation and refinement workflow)");
    }

    private static CalibrationViewModel Create(DiscParametersState state)
    {
        var shell = (ShellViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ShellViewModel));
        Set(shell, "_log", new StringBuilder()); Set(shell, "_dispatcher", Dispatcher.CurrentDispatcher);
        Set(shell, "<Toasts>k__BackingField", new ObservableCollection<ToastItem>());
        foreach (string name in new[] { "OpenOutputCommand", "BrowseCdTrackCommand", "BrowseDvdIsoCommand" })
            Set(shell, $"<{name}>k__BackingField", new RelayCommand(() => { }));
        return new(shell, state);
    }
    private static void Set(object instance, string name, object value)
        => instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Visual selector VM: " + message); }
}
