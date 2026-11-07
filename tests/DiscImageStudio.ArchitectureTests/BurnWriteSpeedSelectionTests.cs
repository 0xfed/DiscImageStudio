using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using DiscImageStudio.Burning;
using DiscImageStudio.ViewModels;

internal static class BurnWriteSpeedSelectionTests
{
    private static readonly OpticalWriteSpeed TwoX = new(1353, false);
    private static readonly OpticalWriteSpeed FourX = new(2705, false);

    internal static void Run()
    {
        Exception? error = null;
        Thread thread = new(() =>
        {
            try { RunOnSta(); }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
        Console.WriteLine("burn-write-speed-selection: passed (stable item identity, bound 4x selection, overlapping queries, same-drive refresh, stale enumeration, automatic choice and unsupported/error fallback)");
    }

    private static void RunOnSta()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        ShellViewModel? shell = null;
        Window? window = null;
        try
        {
            var manual = new BurnViewModel.BurnWriteSpeedOption(FourX, "4×");
            foreach (var option in new[] { manual, BurnViewModel.BurnWriteSpeedOption.Automatic })
            {
                int hash = option.GetHashCode();
                HashSet<BurnViewModel.BurnWriteSpeedOption> indexed = [option];
                PropertyChangedEventHandler handler = (_, _) => { };
                option.PropertyChanged += handler;
                Require(option.GetHashCode() == hash && indexed.Contains(option),
                    "display-label subscriptions must not change an indexed item's identity");
                option.RefreshLabel();
                option.PropertyChanged -= handler;
                Require(option.GetHashCode() == hash && indexed.Contains(option),
                    "removing a label subscription keeps the same selection identity");
            }

            var recorder = new DeferredRecorder();
            shell = new ShellViewModel(Dispatcher.CurrentDispatcher, recorder);
            shell.State.DvdTotalSectors = "16";
            shell.Calibration.SelectedDiscType = "DVD";
            shell.Calibration.SelectFineModeCommand.Execute(null);
            shell.Calibration.CreateTargetCommand.Execute(null);
            BurnViewModel burn = shell.CalibrationBurn;
            var devices = BoundSelector(nameof(BurnViewModel.Devices), nameof(BurnViewModel.SelectedDevice), "DisplayName");
            var speeds = BoundSelector(nameof(BurnViewModel.Speeds), nameof(BurnViewModel.SelectedSpeed), "DisplayName");
            speeds.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(BurnViewModel.IsSpeedComboEnabled)));
            var panel = new StackPanel();
            panel.Children.Add(devices);
            panel.Children.Add(speeds);
            window = new Window { DataContext = burn, Content = panel, Width = 360, Height = 160,
                Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show();

            Task initial = burn.RefreshDevicesAsync();
            recorder.Devices[0].SetResult([Device("A"), Device("B")]);
            PumpUntil(() => recorder.Speeds.Count == 1);
            recorder.Speeds[0].SetResult([TwoX, FourX]);
            Finish(initial);
            SelectFourX();
            speeds.IsDropDownOpen = true;
            Pump();
            speeds.IsDropDownOpen = false;
            foreach (var option in burn.Speeds) option.RefreshLabel();
            Pump();
            Require(burn.SelectedSpeed?.Speed == FourX && ReferenceEquals(speeds.SelectedItem, burn.SelectedSpeed),
                "opening the real ComboBox and refreshing labels preserves the selected 4x item");

            int first = recorder.Speeds.Count;
            Task oldSpeed = burn.RefreshWriteSpeedsAsync();
            Task newSpeed = burn.RefreshWriteSpeedsAsync();
            Pump();
            Require(burn.SelectedSpeed?.Speed == FourX && !speeds.IsEnabled,
                "overlapping queries leave the deliberate speed visible while controls are disabled");
            burn.ConfirmChecked = true;
            Require(!burn.StartBurnCommand.CanExecute(null), "a pending speed query cannot burn using stale media capabilities");
            recorder.Speeds[first + 1].SetResult([TwoX, FourX]);
            Finish(newSpeed);
            recorder.Speeds[first].SetException(new InvalidOperationException("obsolete query"));
            Finish(oldSpeed);
            Require(burn.SelectedSpeed?.Speed == FourX && !burn.ConfirmChecked
                && !burn.SpeedStatusText.Contains("obsolete query", StringComparison.Ordinal),
                "the newest result preserves 4x and an older failure cannot overwrite it");

            // Explicitly choosing Automatic during a query must not resurrect its old manual choice.
            first = recorder.Speeds.Count;
            Task automatic = burn.RefreshWriteSpeedsAsync();
            burn.SelectedSpeed = BurnViewModel.BurnWriteSpeedOption.Automatic;
            recorder.Speeds[first].SetResult([TwoX, FourX]);
            Finish(automatic);
            Require(burn.SelectedSpeed?.Speed is null, "the latest deliberate Automatic choice wins");
            SelectFourX();

            first = recorder.Speeds.Count;
            devices.SelectedItem = burn.Devices!.Single(device => device.Id == "B");
            PumpUntil(() => recorder.Speeds.Count > first);
            recorder.Speeds[first].SetResult([TwoX, FourX]);
            PumpUntil(() => burn.IsSpeedComboEnabled);
            SelectFourX();
            int firstDevice = recorder.Devices.Count;
            Task oldDevices = burn.RefreshDevicesAsync();
            Task newDevices = burn.RefreshDevicesAsync();
            first = recorder.Speeds.Count;
            recorder.Devices[firstDevice + 1].SetResult([Device("A"), Device("B")]);
            PumpUntil(() => recorder.Speeds.Count > first);
            recorder.Speeds[first].SetResult([TwoX, FourX]);
            Finish(newDevices);
            int queryCount = recorder.Speeds.Count;
            recorder.Devices[firstDevice].SetResult([Device("A")]);
            Finish(oldDevices);
            Require(burn.SelectedDevice?.Id == "B" && burn.SelectedSpeed?.Speed == FourX
                && recorder.Speeds.Count == queryCount && ReferenceEquals(devices.SelectedItem, burn.SelectedDevice),
                "same-drive reenumeration retains drive B and 4x; stale device results cannot replace either");

            first = recorder.Speeds.Count;
            Task unsupported = burn.RefreshWriteSpeedsAsync();
            recorder.Speeds[first].SetResult([TwoX]);
            Finish(unsupported);
            Require(burn.SelectedSpeed?.Speed is null && burn.Speeds.All(option => option.Speed != FourX),
                "a newly unsupported speed falls back to Automatic rather than being forced");

            first = recorder.Speeds.Count;
            Task failed = burn.RefreshWriteSpeedsAsync();
            recorder.Speeds[first].SetException(new InvalidOperationException("current query"));
            Finish(failed);
            Require(burn.SelectedSpeed?.Speed is null && burn.Speeds.Count == 1
                && burn.SpeedStatusText.Contains("current query", StringComparison.Ordinal),
                "a current query failure clears stale manual choices and reports its error");

            void SelectFourX()
            {
                speeds.SelectedItem = BurnViewModel.BurnWriteSpeedOption.Automatic;
                Pump();
                burn.ConfirmChecked = true;
                speeds.SelectedItem = burn.Speeds.Single(option => option.Speed == FourX);
                Pump();
                Require(burn.SelectedSpeed?.Speed == FourX && !burn.ConfirmChecked,
                    "a bound 4x selection stays selected and requires fresh burn confirmation");
            }
        }
        finally
        {
            window?.Close();
            shell?.Calibration.OnWindowClosed();
            shell?.LivePreview.OnWindowClosed();
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static ComboBox BoundSelector(string items, string selected, string display)
    {
        var combo = new ComboBox { DisplayMemberPath = display };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(items));
        combo.SetBinding(Selector.SelectedItemProperty, new Binding(selected) { Mode = BindingMode.TwoWay });
        return combo;
    }

    private static OpticalBurnDevice Device(string id) => new(id, "Memory recorder " + id, []);
    private static void Finish(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); Pump(); }
    private static void Pump()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void PumpUntil(Func<bool> complete)
    {
        long deadline = Environment.TickCount64 + 10_000;
        while (!complete())
        {
            if (Environment.TickCount64 > deadline) throw new TimeoutException("Burn speed selection test timed out.");
            Pump();
            Thread.Sleep(1);
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Burn write speed selection: " + message); }

    private sealed class DeferredRecorder : IOpticalDiscBurner
    {
        internal List<TaskCompletionSource<IReadOnlyList<OpticalBurnDevice>>> Devices { get; } = [];
        internal List<TaskCompletionSource<IReadOnlyList<OpticalWriteSpeed>>> Speeds { get; } = [];
        public Task<IReadOnlyList<OpticalBurnDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<IReadOnlyList<OpticalBurnDevice>> request = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Devices.Add(request);
            return request.Task;
        }
        public Task<long> GetMediaSectorCountAsync(string deviceId, OpticalBurnMediaKind mediaKind,
            CancellationToken cancellationToken = default) => Task.FromResult(64L);
        public Task<OpticalMediaCapacity> GetMediaCapacityAsync(string deviceId, OpticalBurnMediaKind mediaKind,
            CancellationToken cancellationToken = default) => Task.FromResult(new OpticalMediaCapacity(64, 64));
        public Task<IReadOnlyList<OpticalWriteSpeed>> GetSupportedWriteSpeedsAsync(string deviceId,
            OpticalBurnMediaKind mediaKind, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<IReadOnlyList<OpticalWriteSpeed>> request = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Speeds.Add(request);
            return request.Task;
        }
        public Task<long> GetMediaSectorCountAsync(string deviceId, OpticalBurnMediaKind mediaKind,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This fixture cannot query real media.");
        public Task<OpticalBurnResult> BurnAsync(OpticalBurnRequest request,
            IProgress<OpticalBurnProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This selection test must never burn.");
    }
}
