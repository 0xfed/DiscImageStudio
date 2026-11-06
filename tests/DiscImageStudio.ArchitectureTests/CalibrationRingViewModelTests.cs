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

internal static class CalibrationRingViewModelTests
{
    internal static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "disc-ring-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var state = new DiscParametersState();
            var model = Create(state);
            model.SelectedDiscType = "DVD";
            model.UseCirclePattern = true;
            model.CreateTargetCommand.Execute(null);
            var target = model.Target ?? throw new InvalidOperationException(model.LastStatusMessage);
            Require(target.IsRingPattern && model.UseRingMeasurements && model.HasMeasurableRings
                && !model.CanSelectRingMeasurementMethod && model.ShowTargetPreview && model.StreamBurnCommand.CanExecute(null),
                "circle targets open the measurement workflow and preserve a usable burn source");
            var actual = target.Parameters with { InnerRadiusMm = 25.25, OuterRadiusMm = 57 };
            SetMeasurements(model, actual);
            model.DiameterTolerance = "0.03";
            model.SolveDiametersCommand.Execute(null);
            Require(!string.IsNullOrEmpty(model.RingFitSummary) && !model.ApplyRingParametersCommand.CanExecute(null),
                "solving displays radii but requires checking measured ring identities");
            model.RingMeasurementsConfirmed = true;
            Require(model.ApplyRingParametersCommand.CanExecute(null), "consistent measured circles enable application");
            model.ApplyRingParametersCommand.Execute(null);
            Require(Math.Abs(double.Parse(state.DvdInnerRadius, CultureInfo.InvariantCulture) - actual.InnerRadiusMm) < 1e-6
                && Math.Abs(double.Parse(state.DvdOuterRadius, CultureInfo.InvariantCulture) - actual.OuterRadiusMm) < 1e-6
                && model.Target.Parameters == target.Parameters, "applying updates project radii while keeping the burned snapshot");
            string path = Path.Combine(folder, "measured.calibration.json");
            model.SaveSessionTo(path);
            var saved = CalibrationSessionJson.Load(path);
            Require(saved.UseRingMeasurements && saved.Photo is null && saved.RingMeasurements?.Count == 7,
                "a standalone diameter record needs no photograph");
            var reopened = Create(new DiscParametersState());
            ((Task)typeof(CalibrationViewModel).GetMethod("LoadSessionFromAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(reopened, [path])!).GetAwaiter().GetResult();
            Require(reopened.Target?.Id == target.Id && reopened.UseCirclePattern && reopened.UseRingMeasurements
                && reopened.DiameterTolerance == "0.03" && reopened.RingDiameters.All(row => row.MeasuredDiameter.Length > 0)
                && !reopened.ApplyRingParametersCommand.CanExecute(null), "reopening restores inputs but requires a fresh fit");
            reopened.SolveDiametersCommand.Execute(null);
            reopened.RingMeasurementsConfirmed = true;
            Require(reopened.ApplyRingParametersCommand.CanExecute(null), "restored diameters solve again");
            reopened.RingDiameters[3].MeasuredDiameter = (double.Parse(reopened.RingDiameters[3].MeasuredDiameter,
                CultureInfo.InvariantCulture) + .5).ToString("G", CultureInfo.InvariantCulture);
            Require(!reopened.ApplyRingParametersCommand.CanExecute(null) && !reopened.RingMeasurementsConfirmed,
                "editing one diameter invalidates the applied candidate and confirmation");
            reopened.SolveDiametersCommand.Execute(null);
            reopened.RingMeasurementsConfirmed = true;
            Require(!reopened.ApplyRingParametersCommand.CanExecute(null), "large residuals cannot be applied even if checked");

            model.UseCirclePattern = false;
            Require(!model.StreamBurnCommand.CanExecute(null), "changing the selection requires creating the next target");
            model.SelectFineModeCommand.Execute(null);
            model.CreateTargetCommand.Execute(null);
            Require(model.Target is { IsRingPattern: false } && model.HasMeasurableRings && model.CanSelectRingMeasurementMethod,
                "the original fine target exposes its existing observation circles for measurement");
            model.UseRingMeasurements = true;
            SetMeasurements(model, model.Target.Parameters with { InnerRadiusMm = 25.1, OuterRadiusMm = 57.1 });
            model.SolveDiametersCommand.Execute(null);
            model.RingMeasurementsConfirmed = true;
            Require(model.ApplyRingParametersCommand.CanExecute(null), "existing fine-cross circles can calibrate without a visible cross");
        }
        finally { Directory.Delete(folder, recursive: true); }
        Console.WriteLine("calibration-ring-view-model: passed (manual diameters, project apply, confirmation, residual rejection, invalidation, portable reload and existing circles)");
    }

    private static void SetMeasurements(CalibrationViewModel model, CalibrationParameters actual)
    {
        foreach (var value in CalibrationRingTests.Measurements(model.Target!, actual))
            model.RingDiameters.Single(row => row.Ring.Index == value.RingIndex).MeasuredDiameter
                = value.DiameterMm.ToString("G", CultureInfo.InvariantCulture);
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
    { if (!condition) throw new InvalidOperationException("Ring VM: " + message); }
}
