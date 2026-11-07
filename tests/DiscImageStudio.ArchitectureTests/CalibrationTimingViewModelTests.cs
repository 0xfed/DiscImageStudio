using System.Globalization;
using System.IO;
using System.Windows.Threading;
using DiscImageStudio.Burning;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;
using DiscImageStudio.ViewModels;

internal static class CalibrationTimingViewModelTests
{
    internal static void Run() => WpfTestHost.Run(() => Wait(Check()));

    internal static DriveTimingResult Fixture(DriveTimingMedia media = DriveTimingMedia.Dvd)
    {
        long sectors = media == DriveTimingMedia.Dvd ? 2_295_104 : 359_849;
        double bit = media == DriveTimingMedia.Dvd ? 133.3e-6 : 1200.0 / 4_321_800;
        double u = 24.06 / bit, v = (media == DriveTimingMedia.Dvd ? .0007453 : .0016) / bit;
        uint reference = (uint)(sectors / 2 / 16 * 16);
        double origin = DriveTimingGeometry.Angle(DriveTimingGeometry.ReadEndBits(media, reference), u, v);
        var targets = Enumerable.Range(0, 20).Select(i =>
        {
            uint lba = (uint)(sectors * (.05 + .8 * i / 19) / 16) * 16;
            double n = DriveTimingGeometry.ReadEndBits(media, lba);
            double angle = (DriveTimingGeometry.Angle(n, u, v) - origin) * 180 / Math.PI;
            double phase = (angle % 360 + 360) % 360;
            return new DriveTimingTarget(lba, n, phase, .2, .5, 16, angle, angle, 0, 20);
        }).ToArray();
        var result = new DriveTimingResult(media, sectors, "Synthetic measurement", u, v, 0, reference, 20,
            DriveTimingCacheStrategy.ForceUnitAccess, 0, 0, .1, targets);
        result.Validate();
        return result;
    }

    private static async Task Check()
    {
        string originalLanguage = L.PreferredLanguage;
        var directory = Directory.CreateTempSubdirectory("disc-timing-vm-");
        var shell = new ShellViewModel(Dispatcher.CurrentDispatcher, new NoHardware());
        try
        {
            var model = shell.Calibration;
            model.SelectedDiscType = "DVD";
            model.SelectFineModeCommand.Execute(null);
            model.CreateTargetCommand.Execute(null);
            var target = model.Target;
            string projectRadius = shell.State.DvdInnerRadius;
            string path = Path.Combine(directory.FullName, "timing.json");
            var result = Fixture(); DriveTimingRecord.Save(path, result);
            model.Parameters.DvdReadCompatibility = true;
            model.Parameters.DvdUsableSectors = "2200000";
            await model.LoadTimingRecordFromAsync(path);
            Require(model.UseDriveTiming && model.TimingResult?.U == result.U && model.ApplyTimingResultCommand.CanExecute(null), "saved measurement opens in its workflow");
            Require(shell.State.DvdInnerRadius == projectRadius && ReferenceEquals(target, model.Target), "opening preserves drawing geometry and the burned target");
            Require(!model.GenerateImageCommand.CanExecute(null) && !model.StreamBurnCommand.CanExecute(null)
                && !model.CanUseCurrentCalibrationSource, "timing mode cannot export or burn a hidden photo target");
            model.ApplyTimingResultCommand.Execute(null);
            var expected = result.ToParameters(long.Parse(model.TimingTotalSectors, CultureInfo.InvariantCulture));
            Require(Math.Abs(double.Parse(model.Parameters.DvdInnerRadius, CultureInfo.InvariantCulture) - expected.InnerRadiusMm) < 1e-9
                && model.Parameters.DvdReadCompatibility && shell.State.DvdInnerRadius == projectRadius
                && model.Parameters.DvdUsableSectors == "2200000"
                && ReferenceEquals(target, model.Target), "application changes only independent calibration geometry and retains usable capacity");
            model.SelectPatternCalibrationCommand.Execute(null);
            Require(model.GenerateImageCommand.CanExecute(null), "returning to photo calibration restores target output");
            model.SelectDriveTimingCommand.Execute(null);
            model.Parameters.DvdUsableSectors = "4294967296";
            Require(!model.ApplyTimingResultCommand.CanExecute(null), "an overflowing usable limit disables application");
            model.Parameters.DvdUsableSectors = "2200000";
            var retained = model.TimingResult;
            L.SetLanguage(L.English); model.Relocalize();
            string english = model.TimingSummary;
            L.SetLanguage(L.Japanese); model.Relocalize();
            Require(model.TimingSummary != english && ReferenceEquals(retained, model.TimingResult), "language switching preserves measurement data");
            model.TimingTotalSectors = "bad";
            Require(!model.ApplyTimingResultCommand.CanExecute(null), "invalid capacity blocks application");
            File.WriteAllText(path, "{invalid}");
            await model.LoadTimingRecordFromAsync(path);
            Require(ReferenceEquals(retained, model.TimingResult) && !shell.JobHasResult, "failed loading retains the previous measurement");
            model.SelectedDiscType = "BD";
            Require(!model.UseDriveTiming && !model.SelectDriveTimingCommand.CanExecute(null), "unsupported Blu-ray keeps pattern calibration");
            var cd = Fixture(DriveTimingMedia.Cd); DriveTimingRecord.Save(path, cd);
            await model.LoadTimingRecordFromAsync(path);
            Require(model.SelectedDiscType == "CD" && model.TimingResult!.RequiresFineCalibration, "CD keeps its unknown origin and follow-up calibration requirement");
            model.TimingTotalSectors = "359849";
            model.Parameters.CdUsableSectors = "350000";
            model.ApplyTimingResultCommand.Execute(null);
            Require(model.IsFineMode && model.Parameters.CdUsableSectors == "350000"
                && model.TimingResultHint == L.T("Timing.CdFineRequired"), "CD application prepares the fine stage and retains usable capacity");
        }
        finally
        {
            shell.Calibration.OnWindowClosed(); shell.LivePreview.OnWindowClosed();
            L.Initialize(originalLanguage); directory.Delete(true);
        }
        Console.WriteLine("timing-view-model: passed (portable load, explicit apply, independent state, target snapshot, language changes, invalid capacity and CD refinement)");
    }

    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
        if (!task.IsCompleted) Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Timing VM: " + message); }
    private sealed class NoHardware : IOpticalDiscBurner
    {
        public Task<IReadOnlyList<OpticalBurnDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) => throw new Exception("No device access in a record test.");
        public Task<long> GetMediaSectorCountAsync(string deviceId, OpticalBurnMediaKind mediaKind, CancellationToken cancellationToken = default) => throw new Exception("No capacity query in a record test.");
        public Task<IReadOnlyList<OpticalWriteSpeed>> GetSupportedWriteSpeedsAsync(string deviceId, OpticalBurnMediaKind mediaKind, CancellationToken cancellationToken = default) => throw new Exception("No speed query in a record test.");
        public Task<OpticalBurnResult> BurnAsync(OpticalBurnRequest request, IProgress<OpticalBurnProgress>? progress = null, CancellationToken cancellationToken = default) => throw new Exception("No recording in a record test.");
    }
}
