using System.IO;
using DiscImageStudio.Burning;
using DiscImageStudio.Iso;
using DiscImageStudio.Localization;

namespace DiscImageStudio.ViewModels;

public partial class BurnViewModel
{
    internal static FileUpdateContent PrepareFileUpdate(string sourceDirectory, FileUpdateContext context, CancellationToken cancellationToken = default)
    {
        UdfImageWriter.SequentialSnapshot previous = UdfImageWriter.ReadSequentialSnapshot(
            context.ReadSector, context.LastRecordedLba, context.BlockAlignment, context.ReadSectors);
        if (previous.CapacitySectors > context.CapacitySectors)
            throw new InvalidDataException(L.T("The recorded volume exceeds the current disc capacity."));
        UdfImageWriter.LayoutPlan plan = UdfImageWriter.PlanAppend(sourceDirectory, previous,
            context.NextWritableLba, newSession: context.StartsNewSession, cancellationToken: cancellationToken);
        return new((long)(plan.VolumeSectors - plan.StartLba) * 2048,
            (output, token) => UdfImageWriter.WriteAppend(plan, output, token));
    }

    private async Task UpdateDiscFilesAsync()
    {
        if (!CanUpdateDiscFiles() || SelectedDevice is not { } device) return;
        string source = Path.GetFullPath(FileUpdateSourceDirectory);
        OpticalBurnMediaKind kind = CurrentMediaKind;
        int revision = _burnPreparationRevision;
        OpticalWriteSpeed? speed = SelectedSpeed?.Speed;
        bool ownsBusyState = false;
        _isPreparingBurn = true;
        OnPropertyChanged(nameof(IsSetupEnabled));
        UpdateActionState();
        try
        {
            bool confirmed = await _shell.ConfirmAsync(L.T("Update files on this disc?"),
                L.Format("The selected folder becomes the complete file list on {0}:\n{1}\n\nFiles missing from this folder will disappear from the disc's current file list. Old data stays recorded and its space cannot be reclaimed. The original drawing is preserved.",
                    device.DisplayName, source), L.T("Update disc files"));
            if (!confirmed || _shell.IsBusy || revision != _burnPreparationRevision) return;
            ownsBusyState = true;
            _shell.SetBusy(true);
            IsBurnRunning = true;
            using CancellationTokenSource cancellation = new();
            _burnCancellation = cancellation;
            _shell.BeginBurnDockJob(kind, 1);
            _shell.ReportDockJob(L.T("Updating disc files…"), _shell.JobProgressValue);
            bool progressInitialized = false;
            Progress<OpticalBurnProgress> progress = new(value =>
            {
                if (IsBurnRunning && ReferenceEquals(_burnCancellation, cancellation))
                {
                    if (!progressInitialized && value.TotalBytes > 0)
                    {
                        _shell.BeginBurnDockJob(kind, value.TotalBytes);
                        progressInitialized = true;
                    }
                    _shell.ReportBurnProgress(value, cancellationRequested: cancellation.IsCancellationRequested);
                }
            });
            OpticalBurnResult result = await _opticalDiscBurner.UpdateFilesAsync(device.Id, kind,
                context => PrepareFileUpdate(source, context, cancellation.Token), speed, progress, cancellation.Token);
            _shell.ReportDockJob(L.T("Files updated. The disc remains open for further updates."), _shell.JobProgressValue);
            _shell.CompleteDockJob(L.T("Disc files updated"));
            _shell.AppendLog(L.Format("Disc file update completed: {0:N0} new bytes recorded.\n", result.BytesWritten));
            _shell.ShowToast(L.T("Disc files updated"),
                L.T("Files updated. The disc remains open for further updates."), ToastKind.Success);
        }
        catch (OperationCanceledException)
        {
            _shell.ReportDockJob(L.T("已请求取消；光驱可能仍需一段时间才能停止。"), _shell.JobProgressValue);
            _shell.AbortDockJob(L.T("File update cancelled"), expandLog: true);
        }
        catch (Exception exception)
        {
            _shell.ReportDockJob(L.T("刻录未完成，请查看错误信息。"), _shell.JobProgressValue);
            _shell.AbortDockJob(L.T("File update failed"), expandLog: true);
            _shell.AppendLog(exception.Message + "\n");
            _shell.ShowToast(L.T("File update failed"), exception.Message, ToastKind.Error);
        }
        finally
        {
            _burnCancellation = null;
            IsBurnRunning = false;
            _isPreparingBurn = false;
            if (ownsBusyState) _shell.SetBusy(false);
            InvalidateConfirmation();
            OnPropertyChanged(nameof(IsSetupEnabled));
            UpdateActionState();
        }
    }
}
