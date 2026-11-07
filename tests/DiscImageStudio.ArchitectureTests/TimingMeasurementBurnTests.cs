using System.Buffers.Binary;
using System.IO;
using System.Windows.Threading;
using DiscImageStudio.Burning;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;
using DiscImageStudio.Services;
using DiscImageStudio.ViewModels;

internal static class TimingMeasurementBurnTests
{
    internal static void Run() => WpfTestHost.Run(() => Wait(Check()));

    private static async Task Check()
    {
        string language = L.PreferredLanguage;
        var directory = Directory.CreateTempSubdirectory("timing-burn-");
        RecordingBurner recorder = new();
        ShellViewModel shell = new(Dispatcher.CurrentDispatcher, recorder,
            new DiscParametersState(Path.Combine(directory.FullName, "presets.json")),
            new RecentProjectStore(Path.Combine(directory.FullName, "recent.json")));
        try
        {
            L.SetLanguage(L.English);
            CheckImageParity(directory.FullName);
            CalibrationViewModel model = shell.Calibration;
            BurnViewModel burn = shell.CalibrationBurn;
            model.SelectedDiscType = "DVD";
            model.TimingTotalSectors = "9000";
            model.Parameters.DvdUsableSectors = "8992";
            model.Parameters.DvdReadCompatibility = true;
            model.SelectDriveTimingCommand.Execute(null);
            string projectRadius = shell.State.DvdInnerRadius;
            model.StreamBurnTimingImageCommand.Execute(null);
            await burn.RefreshDevicesAsync();
            Require(shell.CurrentWorkspace == Workspace.CalibrationBurn && model.Target is null
                && !shell.IsProjectOpen && burn.IsTimingMeasurement && !burn.UseCalibrationPattern,
                "measurement burn opens without a project, pattern or save dialog");
            Require(!burn.CanConfigureFileUpdates && !burn.ShowDvdReadCompatibility && !burn.DvdReadCompatibility
                && burn.IsDvdBurn && !burn.IsBluRayBurn, "measurement uses ordinary closed data without artwork options");
            Require(recorder.SpeedQueries[^1] == OpticalBurnMediaKind.DvdData, "DVD data speed routing");

            shell.ConfirmHandler = (_, _, _) => Task.FromResult(false);
            burn.ConfirmChecked = true;
            await burn.StartBurnCommand.ExecuteAsync(null);
            Require(recorder.Burns == 0, "declining confirmation never writes");

            // Editing the next calibration setup cannot change already selected material.
            model.SelectedDiscType = "CD";
            model.Parameters.DvdTotalSectors = "16384";
            model.Parameters.DvdUsableSectors = "16384";
            model.Parameters.CdDataDirectory = "missing-unused-folder";
            burn.UpdateSourceSummary();
            await burn.RefreshWriteSpeedsAsync();
            Require(burn.FamilyLabel == "DVD" && recorder.SpeedQueries[^1] == OpticalBurnMediaKind.DvdData,
                "family and speeds follow the captured measurement, not later setup edits");
            string? confirmation = null;
            shell.ConfirmHandler = (_, message, _) => { confirmation = message; return Task.FromResult(true); };
            burn.ConfirmChecked = true;
            await burn.StartBurnCommand.ExecuteAsync(null);
            Require(recorder.Burns == 1 && recorder.Request is { MediaKind: OpticalBurnMediaKind.DvdData,
                ContentLength: 8448L * 2048, LeaveDiscOpen: false, UseUdf250: false },
                "writable capacity constrains and aligns the captured DVD data image");
            CheckVolume(recorder.Payload!, 8448);
            Require(confirmation?.Contains(L.Format("Timing.BurnSourceDetails", 8448, 8448 * 2048.0 / (1024 * 1024))) == true,
                "confirmation describes the actual constrained image size");
            Require(model.Target is null && shell.State.DvdInnerRadius == projectRadius && !burn.ConfirmChecked,
                "streaming neither creates a photo target nor changes the project, and disarms confirmation");

            model.TimingTotalSectors = "9000";
            model.Parameters.CdUsableSectors = "8998";
            Require(model.CaptureTimingMeasurement().Sectors == 8750, "CD measurement retains its closing allowance");
            model.Parameters.CdUsableSectors = "8500";
            Require(model.CaptureTimingMeasurement().Sectors == 8500, "smaller explicit CD limits are not reduced twice");
            recorder.Capacity = new(9000, 8500);
            model.StreamBurnTimingImageCommand.Execute(null);
            Require(burn.FamilyLabel == "CD" && recorder.SpeedQueries[^1] == OpticalBurnMediaKind.CdData,
                "CD measurement selects the data writer, independent of artwork and folders");
            shell.SetBusy(true);
            model.SelectedDiscType = "DVD";
            model.StreamBurnTimingImageCommand.Execute(null);
            Require(burn.FamilyLabel == "CD", "a late command cannot replace the burn source while another job is running");
            shell.SetBusy(false);
            model.SelectedDiscType = "CD";
            burn.ConfirmChecked = true;
            await burn.StartBurnCommand.ExecuteAsync(null);
            Require(recorder.Burns == 2 && recorder.Request is { MediaKind: OpticalBurnMediaKind.CdData,
                ContentLength: 8500L * 2048, LeaveDiscOpen: false, UseUdf250: false }, "CD is a readable closed data track");
            CheckVolume(recorder.Payload!, 8500);

            recorder.Capacity = new(9000, 8191);
            burn.ConfirmChecked = true;
            await burn.StartBurnCommand.ExecuteAsync(null);
            Require(recorder.Burns == 2, "an insufficient measurement area fails before recording");

            // A delayed capacity query must not write a source selected before a new entry.
            TaskCompletionSource<OpticalMediaCapacity> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            recorder.PendingCapacity = pending.Task;
            burn.ConfirmChecked = true;
            Task oldPreparation = burn.StartBurnCommand.ExecuteAsync(null);
            Require(!oldPreparation.IsCompleted, "capacity query is pending");
            model.SelectedDiscType = "DVD";
            model.StreamBurnTimingImageCommand.Execute(null);
            pending.SetResult(new(16384, 16384));
            await oldPreparation;
            recorder.PendingCapacity = null;
            Require(recorder.Burns == 2 && !burn.ConfirmChecked && burn.FamilyLabel == "DVD",
                "changing the prepared source invalidates the old request and confirmation");
            Require(!Directory.EnumerateFiles(directory.FullName, "*.iso").Any(), "streaming creates no full ISO file");

            model.SelectPatternCalibrationCommand.Execute(null);
            model.SelectFineModeCommand.Execute(null);
            model.CreateTargetCommand.Execute(null);
            Require(burn.IsTimingMeasurement, "creating a new photo target does not silently replace a measurement source");
            model.SaveSessionTo(Path.Combine(directory.FullName, "pattern.calibration.json"));
            model.StreamBurnCommand.Execute(null);
            Require(burn.UseCalibrationPattern && !burn.IsTimingMeasurement && burn.ShowDvdReadCompatibility,
                "the pattern burn entry explicitly restores the saved pattern source");
            model.SelectedDiscType = "BD";
            Require(!model.StreamBurnTimingImageCommand.CanExecute(null), "Blu-ray timing burns are unavailable");
        }
        finally
        {
            shell.Calibration.OnWindowClosed(); shell.LivePreview.OnWindowClosed();
            L.Initialize(language); directory.Delete(true);
        }
        Console.WriteLine("timing-measurement-burn: passed (CD/DVD data routing, exact ISO/stream bytes, frozen sources, capacity, confirmation, cancellation and no temporary ISO)");
    }

    private static void CheckImageParity(string directory)
    {
        foreach (DriveTimingMedia media in new[] { DriveTimingMedia.Cd, DriveTimingMedia.Dvd })
        {
            var source = new TimingMeasurementSource(media, 9000, DateTimeOffset.UnixEpoch);
            var speed = new OpticalWriteSpeed(4500, false);
            uint expectedSectors = media == DriveTimingMedia.Cd ? 8451u : 8448u;
            OpticalBurnRequest request = source.CreateBurnRequest("memory", speed, 8451);
            request.Validate();
            Require(request.WriteSpeed == speed && !request.LeaveDiscOpen && !request.UseUdf250,
                "the request preserves speed and uses a closed plain-data disc");
            string path = Path.Combine(directory, "parity.bin");
            using (var file = File.Create(path)) (source with { Sectors = expectedSectors }).Write(file, CancellationToken.None);
            using MemoryStream output = new();
            using (var forward = new ForwardOnlyOutput(output)) request.ProduceContent(forward, CancellationToken.None);
            Require(File.ReadAllBytes(path).AsSpan().SequenceEqual(output.ToArray()), "ISO export and forward-only stream match byte for byte");
            CheckVolume(output.ToArray(), expectedSectors);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            using MemoryStream untouched = new();
            try { request.ProduceContent(untouched, cancelled.Token); throw new InvalidOperationException("Cancellation ignored."); }
            catch (OperationCanceledException) { Require(untouched.Length == 0, "cancelled producer writes no ISO headers or data"); }
        }
    }

    private static void CheckVolume(byte[] bytes, uint sectors)
    {
        Require(bytes.LongLength == (long)sectors * 2048 && bytes.AsSpan(16 * 2048 + 1, 5).SequenceEqual("CD001"u8),
            "ordinary ISO9660 descriptor and complete data sectors");
        Require(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16 * 2048 + 80)) == sectors,
            "ISO volume length matches the emitted stream");
        Require(bytes.AsSpan(100 * 2048, 2048).ToArray().Distinct().Count() > 240,
            "measurement contains random data rather than a drawing texture");
    }

    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
        if (!task.IsCompleted) Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Timing measurement burn: " + message);
    }

    private sealed class RecordingBurner : IOpticalDiscBurner
    {
        internal int Burns;
        internal OpticalBurnRequest? Request;
        internal byte[]? Payload;
        internal OpticalMediaCapacity Capacity = new(9000, 8451);
        internal Task<OpticalMediaCapacity>? PendingCapacity;
        internal List<OpticalBurnMediaKind> SpeedQueries = [];
        public Task<IReadOnlyList<OpticalBurnDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OpticalBurnDevice>>([new("memory", "Memory recorder", [])]);
        public Task<long> GetMediaSectorCountAsync(string deviceId, OpticalBurnMediaKind kind, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The measurement writer must query both geometry and writable capacity.");
        public Task<OpticalMediaCapacity> GetMediaCapacityAsync(string deviceId, OpticalBurnMediaKind kind, CancellationToken cancellationToken = default)
            => PendingCapacity ?? Task.FromResult(Capacity);
        public Task<IReadOnlyList<OpticalWriteSpeed>> GetSupportedWriteSpeedsAsync(string deviceId, OpticalBurnMediaKind kind,
            CancellationToken cancellationToken = default)
        {
            SpeedQueries.Add(kind);
            return Task.FromResult<IReadOnlyList<OpticalWriteSpeed>>([new(4500, false)]);
        }
        public Task<OpticalBurnResult> BurnAsync(OpticalBurnRequest request, IProgress<OpticalBurnProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Request = request; Burns++;
            using MemoryStream output = new();
            using (var forward = new ForwardOnlyOutput(output)) request.ProduceContent(forward, cancellationToken);
            Payload = output.ToArray();
            return Task.FromResult(new OpticalBurnResult(request.DeviceId, request.MediaKind, output.Length, TimeSpan.Zero));
        }
    }

    private sealed class ForwardOnlyOutput(Stream output) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => output.Flush();
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => output.Write(buffer);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
