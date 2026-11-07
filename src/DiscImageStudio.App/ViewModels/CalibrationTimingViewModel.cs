using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Iso;
using DiscImageStudio.Localization;
using DiscImageStudio.Services;

namespace DiscImageStudio.ViewModels;

public partial class CalibrationViewModel
{
    [ObservableProperty] private bool _useDriveTiming;
    [ObservableProperty] private DriveTimingResult? _timingResult;
    [ObservableProperty] private string _timingStatus = string.Empty;
    private string _cdReadSpeed = "1411", _dvdReadSpeed = "5540";
    private int _timingRevision;
    public bool ShowPatternCalibration => !UseDriveTiming;
    public bool CanSelectDriveTiming => SelectedDiscType != "BD" && !IsBusy;
    public bool CanEditTiming => !IsBusy;
    public bool HasTimingResult => TimingResult is not null;
    public IReadOnlyList<string> TimingMediaChoices { get; } = ["CD", "DVD"];
    public string TimingReadSpeed
    {
        get => SelectedDiscType == "CD" ? _cdReadSpeed : _dvdReadSpeed;
        set
        {
            if (SelectedDiscType == "CD") _cdReadSpeed = value; else _dvdReadSpeed = value;
            OnPropertyChanged();
        }
    }
    public string TimingTotalSectors
    {
        get => SelectedDiscType == "CD" ? Parameters.CdSectors : Parameters.DvdTotalSectors;
        set
        {
            if (SelectedDiscType == "CD") Parameters.CdSectors = value; else Parameters.DvdTotalSectors = value;
            RefreshTimingProperties();
        }
    }
    public string TimingResultHint => TimingResult is { } result && (result.Media == DriveTimingMedia.Cd ? "CD" : "DVD") != SelectedDiscType
        ? L.T("Timing.MediaMismatch") : TimingResult?.RequiresFineCalibration == true ? L.T("Timing.CdFineRequired") : L.T("Timing.ResultHint");
    public string TimingSummary
    {
        get
        {
            if (TimingResult is not { } result) return string.Empty;
            var range = result.MeasuredRadiusRange;
            string summary = L.Format("Timing.Summary", result.DeviceName, result.ResidualRmsDegrees,
                result.AngleUncertaintyDegrees, range.Inner, range.Outer, result.Targets.Count, result.Media == DriveTimingMedia.Cd ? "CD" : "DVD");
            if (CanApplyTiming())
            {
                var parameters = result.ToParameters(ReadTimingCapacity());
                summary += Environment.NewLine + L.Format("Timing.Radii", parameters.InnerRadiusMm, parameters.OuterRadiusMm);
            }
            return summary;
        }
    }
    public RelayCommand SelectDriveTimingCommand { get; private set; } = null!;
    public RelayCommand SelectPatternCalibrationCommand { get; private set; } = null!;
    public IAsyncRelayCommand RefreshTimingReadersCommand { get; private set; } = null!;
    public IAsyncRelayCommand MeasureDriveTimingCommand { get; private set; } = null!;
    public IAsyncRelayCommand GenerateTimingImageCommand { get; private set; } = null!;
    public IAsyncRelayCommand OpenTimingRecordCommand { get; private set; } = null!;
    public RelayCommand SaveTimingRecordCommand { get; private set; } = null!;
    public RelayCommand ApplyTimingResultCommand { get; private set; } = null!;

    private void InitializeTimingCommands()
    {
        SelectDriveTimingCommand = new RelayCommand(() => UseDriveTiming = true, () => CanSelectDriveTiming);
        SelectPatternCalibrationCommand = new RelayCommand(() => UseDriveTiming = false, () => !IsBusy);
        RefreshTimingReadersCommand = new AsyncRelayCommand(RefreshTimingReadersAsync, () => !IsBusy);
        MeasureDriveTimingCommand = new AsyncRelayCommand(MeasureDriveTimingAsync, () => CanSelectDriveTiming && SelectedCapacityRecorder is not null);
        GenerateTimingImageCommand = new AsyncRelayCommand(GenerateTimingImageAsync, () => CanSelectDriveTiming);
        OpenTimingRecordCommand = new AsyncRelayCommand(OpenTimingRecordAsync, () => !IsBusy);
        SaveTimingRecordCommand = new RelayCommand(SaveTimingRecord, () => !IsBusy && TimingResult is not null);
        ApplyTimingResultCommand = new RelayCommand(ApplyTimingResult, () => !IsBusy && CanApplyTiming());
    }

    partial void OnUseDriveTimingChanged(bool value)
    {
        if (value && SelectedDiscType == "BD") { UseDriveTiming = false; return; }
        OnPropertyChanged(nameof(ShowPatternCalibration));
        RefreshCommands();
    }
    partial void OnTimingResultChanged(DriveTimingResult? value) => RefreshTimingProperties();

    private bool CanApplyTiming()
    {
        if (TimingResult is not { } result || (result.Media == DriveTimingMedia.Cd ? "CD" : "DVD") != SelectedDiscType) return false;
        try { _ = result.ToParameters(ReadTimingCapacity()); return true; }
        catch (ArgumentException) { return false; }
    }

    private long ReadTimingCapacity()
    {
        if (!long.TryParse(TimingTotalSectors, NumberStyles.None, CultureInfo.InvariantCulture, out long sectors)
            || sectors is < 8192 or > 3_000_000 || (SelectedDiscType == "CD" && sectors > 450_000))
            throw new ArgumentException(L.T("Timing.InvalidCapacity"));
        return sectors;
    }

    private Task RefreshTimingReadersAsync() => RunAsync(L.T("Timing.FindingDrives"), async token =>
    {
        string? previous = SelectedCapacityRecorder?.Id;
        var devices = await _shell.OpticalBurner.GetDevicesAsync(token);
        token.ThrowIfCancellationRequested();
        CapacityRecorders = devices;
        SelectedCapacityRecorder = devices.FirstOrDefault(d => d.Id == previous) ?? (devices.Count == 1 ? devices[0] : null);
        TimingStatus = _statusMessage = L.T(devices.Count == 0 ? "CalibrationMedia.NoDevices"
            : SelectedCapacityRecorder is null ? "CalibrationMedia.PickDevice" : "Timing.Ready");
    });

    private async Task MeasureDriveTimingAsync()
    {
        if (SelectedCapacityRecorder is not { } recorder) return;
        if (!ushort.TryParse(TimingReadSpeed, NumberStyles.None, CultureInfo.InvariantCulture, out ushort speed))
        { Report(new ArgumentException(L.T("Timing.InvalidSpeed"))); return; }
        string family = SelectedDiscType;
        int revision = ++_timingRevision;
        await RunAsync(L.T("Timing.Measuring"), async token =>
        {
            var progress = new Progress<DriveTimingProgress>(p =>
            {
                if (token.IsCancellationRequested || revision != _timingRevision || _cancellation?.Token != token) return;
                TimingStatus = FormatTimingProgress(p);
                _shell.ReportDockJob(TimingStatus, p.Total > 0 ? (double)p.Completed / p.Total : 0);
            });
            var result = await _shell.OpticalBurner.ReadDiscAsync(recorder.Id, speed, session =>
            {
                var reader = new DriveTimingReader(session);
                if ((reader.Disc.Media == DriveTimingMedia.Cd ? "CD" : "DVD") != family)
                    throw new InvalidOperationException(L.T("Timing.WrongMedia"));
                return DriveTimingCalibrator.Measure(reader, progress: progress, cancellationToken: token);
            }, token);
            token.ThrowIfCancellationRequested();
            if (family != SelectedDiscType || recorder.Id != SelectedCapacityRecorder?.Id) throw new OperationCanceledException();
            TimingResult = result;
            TimingStatus = _statusMessage = L.T("Timing.Measured");
            _shell.AppendLog(TimingSummary + Environment.NewLine);
        });
    }

    private async Task GenerateTimingImageAsync()
    {
        long capacity;
        try { capacity = ReadTimingCapacity(); } catch (Exception ex) { Report(ex); return; }
        uint sectors = checked((uint)(SelectedDiscType == "CD" ? capacity - 250 : capacity / 16 * 16));
        string? path = _shell.Dialogs?.PickSave(L.T("光盘镜像|*.iso"), ".iso", "timing-measurement.iso");
        if (path is null) return;
        await GenerateTimingImageToAsync(path, sectors);
    }

    internal Task GenerateTimingImageToAsync(string path, uint sectors) => RunAsync(L.T("Timing.CreatingImage"), async token =>
    {
        int revision = ++_timingRevision;
        string target = Path.GetFullPath(path), staging = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var progress = new Progress<double>(p => { if (!token.IsCancellationRequested && revision == _timingRevision && _cancellation?.Token == token) _shell.ReportDockJob(L.T("Timing.CreatingImage"), p); });
            await Task.Run(() =>
            {
                using var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write);
                CalibrationDataImage.Write(stream, sectors, progress: progress, cancellationToken: token);
                stream.Flush(flushToDisk: true);
            }, token);
            token.ThrowIfCancellationRequested();
            File.Move(staging, target, overwrite: true);
            TimingStatus = _statusMessage = L.Format("Timing.ImageSaved", target);
        }
        finally { JobDiagnostics.TryCleanup(() => File.Delete(staging), _shell.AppendLog, L.T("Timing.CreatingImage")); }
    });

    private async Task OpenTimingRecordAsync()
    {
        string? path = _shell.Dialogs?.PickOpen(title: L.T("Timing.OpenRecord"), filter: L.T("Timing.RecordFilter"));
        if (path is not null) await LoadTimingRecordFromAsync(path);
    }
    internal Task LoadTimingRecordFromAsync(string path) => RunAsync(L.T("Timing.OpenRecord"), async token =>
    {
        var result = await Task.Run(() => DriveTimingRecord.Load(path), token);
        token.ThrowIfCancellationRequested();
        SelectedDiscType = result.Media == DriveTimingMedia.Cd ? "CD" : "DVD";
        TimingResult = result;
        UseDriveTiming = true;
        TimingStatus = _statusMessage = L.T("Timing.RecordLoaded");
    });
    private void SaveTimingRecord()
    {
        if (TimingResult is not { } result) return;
        string? path = _shell.Dialogs?.PickSave(L.T("Timing.RecordFilter"), ".timing-calibration.json", "measurement.timing-calibration.json");
        if (path is null) return;
        try { DriveTimingRecord.Save(path, result); TimingStatus = _statusMessage = L.Format("Timing.RecordSaved", path); }
        catch (Exception ex) { Report(ex); }
    }
    private void ApplyTimingResult()
    {
        if (!CanApplyTiming() || TimingResult is not { } result) return;
        try
        {
            WriteSetupParameters(result.ToParameters(ReadTimingCapacity()) with
            {
                DvdReadCompatibility = Parameters.DvdReadCompatibility,
            });
            SelectStage(CalibrationStage.Fine);
            TimingStatus = _statusMessage = L.T(result.RequiresFineCalibration ? "Timing.AppliedCd" : "Timing.Applied");
            _shell.AppendLog(_statusMessage + Environment.NewLine);
        }
        catch (Exception ex) { Report(ex); }
    }
    private static string FormatTimingProgress(DriveTimingProgress p) => p.Stage switch
    {
        DriveTimingStage.CheckingCache => L.T("Timing.CheckingCache"),
        DriveTimingStage.CheckingRotation => L.Format("Timing.CheckingRotation", p.Completed + 1, p.Total),
        DriveTimingStage.Settling => L.T("Timing.Settling"),
        DriveTimingStage.Measuring => L.Format("Timing.TargetProgress", p.Completed + 1, p.Total),
        DriveTimingStage.Complete => L.T("Timing.Measured"),
        _ => L.T("Timing.CheckingMedia"),
    };
    private void RefreshTimingProperties()
    {
        foreach (string name in new[] { nameof(TimingTotalSectors), nameof(TimingReadSpeed), nameof(TimingSummary), nameof(TimingResultHint), nameof(HasTimingResult), nameof(CanSelectDriveTiming), nameof(CanEditTiming) })
            OnPropertyChanged(name);
        RefreshTimingCommands();
    }
    private void RefreshTimingCommands()
    {
        OnPropertyChanged(nameof(CanSelectDriveTiming)); OnPropertyChanged(nameof(CanEditTiming));
        SelectDriveTimingCommand?.NotifyCanExecuteChanged(); SelectPatternCalibrationCommand?.NotifyCanExecuteChanged();
        RefreshTimingReadersCommand?.NotifyCanExecuteChanged(); MeasureDriveTimingCommand?.NotifyCanExecuteChanged();
        GenerateTimingImageCommand?.NotifyCanExecuteChanged(); OpenTimingRecordCommand?.NotifyCanExecuteChanged();
        SaveTimingRecordCommand?.NotifyCanExecuteChanged(); ApplyTimingResultCommand?.NotifyCanExecuteChanged();
    }
}
