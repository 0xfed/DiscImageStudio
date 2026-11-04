using System.Diagnostics;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Burning;

public sealed partial class WindowsImapiBurner
{
    public Task<OpticalBurnResult> AppendBluRayAsync(string deviceId,
        Func<BluRayAppendContext, BluRayAppendContent> prepareContent,
        OpticalWriteSpeed? writeSpeed = null, IProgress<OpticalBurnProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(prepareContent);
        writeSpeed?.Validate();
        return _operationGate.RunAsync(() => RunOnStaThread(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            dynamic recorder = CreateRecorder(deviceId);
            dynamic? format = null;
            List<string> cleanupWarnings = [];
            Exception? failure = null;
            OpticalBurnResult? result = null;
            try
            {
                format = CreateComObject("IMAPI2.MsftDiscFormat2Data");
                format.Recorder = recorder;
                format.ClientName = "Disc Image Studio";
                ValidateRequestedMedia(OpticalBurnMediaKind.BluRayData, recorder, format);
                OpticalWriteSpeed? speed = ResolveWriteSpeed(writeSpeed, ReadSupportedWriteSpeeds(format));
                using DynamicBluRaySequentialSession session = new((object)recorder, (object)format,
                    (completed, total) => progress?.Report(new("write", L.T("Updating Blu-ray files…"), completed, total)),
                    cleanupWarnings.Add,
                    message => progress?.Report(new("diagnostic", message, 0, 0)), forceExclusive: true);
                result = BurnBluRayAppend(session, deviceId, prepareContent, speed, progress, cancellationToken);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                CompleteBurnCleanup(failure, cleanupWarnings,
                    () => { if (format is not null) ReleaseComObject((object)format); },
                    () => ReleaseComObject((object)recorder));
            }
            return result!;
        }), cancellationToken);
    }

    internal static OpticalBurnResult BurnBluRayAppend(IBluRaySequentialSession session, string deviceId,
        Func<BluRayAppendContext, BluRayAppendContent> prepareContent,
        OpticalWriteSpeed? writeSpeed = null, IProgress<OpticalBurnProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        bool acquired = false, writeStarted = false, flushed = false;
        Exception? failure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.Acquire();
            acquired = true;
            BluRaySequentialMediaInfo media = session.ReadMedia();
            if (media.Profile != BluRaySequentialProfile || media.DiscIsBlank || media.DiscIsClosed || media.TrackIsBlank
                || media.FormatStatus is not (1 or 2)
                || !media.NextWritableAddressValid || media.LastRecordedAddress is not { } last
                || last >= media.NextWritableAddress || media.NextWritableAddress % 32 != 0
                || media.NextWritableAddress >= media.MaximumBlocks
                || media.MaximumBlocks != BluRaySingleLayerSectors || media.TrackNumber != 1
                || media.TrackStartAddress != 0 || media.TrackSize != media.MaximumBlocks
                || media.FreeBlocks > media.MaximumBlocks - media.NextWritableAddress)
                throw new InvalidOperationException(L.T("Insert an open single-layer BD-R VAT disc with unrecorded space."));
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new("media", L.T("Preparing file update…"), 0, 0));
            BluRayAppendContent prepared = prepareContent(new(media.NextWritableAddress, last,
                media.MaximumBlocks, session.ReadSector, session.ReadSectors));
            OpticalBurnRequest request = new(deviceId, OpticalBurnMediaKind.BluRayData,
                prepared.ContentLength, prepared.ProduceContent, writeSpeed, LeaveDiscOpen: true);
            request.Validate();
            progress?.Report(new("media", L.T("Preparing file update…"), 0, request.ContentLength));
            long sectors = request.ContentLength / 2048;
            if (sectors % 32 != 0 || sectors > media.FreeBlocks
                || (ulong)media.NextWritableAddress + (ulong)sectors > media.MaximumBlocks)
                throw new InvalidOperationException(L.T("Not enough unrecorded disc space for the files and VAT update."));
            OpticalWriteSpeed actual = session.ConfigureWrite(writeSpeed);
            actual.Validate();
            using GeneratedContentComStream content = new(request.ContentLength, request.ProduceContent,
                (done, total) => progress?.Report(new("transfer", L.T("Preparing file update…"), done, total)),
                cancellationToken, request.EffectiveBufferOptions);
            content.WaitUntilPrebuffered(cancellationToken);
            using (cancellationToken.Register(session.CancelWrite))
            {
                cancellationToken.ThrowIfCancellationRequested();
                writeStarted = true;
                session.WriteAppend(content, checked((uint)sectors), media.NextWritableAddress);
            }
            cancellationToken.ThrowIfCancellationRequested();
            session.Flush();
            flushed = true;
            progress?.Report(new("complete", L.T("Files updated. The disc remains open for further updates."),
                request.ContentLength, request.ContentLength));
            return new(deviceId, OpticalBurnMediaKind.BluRayData, request.ContentLength, elapsed.Elapsed, actual);
        }
        catch (Exception exception)
        {
            failure = exception;
            if (cancellationToken.IsCancellationRequested && exception is not OperationCanceledException)
                throw new OperationCanceledException(exception.Message, exception, cancellationToken);
            throw;
        }
        finally
        {
            if (acquired)
            {
                if (failure is not null)
                {
                    if (writeStarted && !flushed)
                    {
                        try { session.Flush(); }
                        catch (Exception exception)
                        {
                            try { progress?.Report(new("diagnostic", exception.Message, 0, 0)); }
                            catch { }
                        }
                    }
                    try { session.Release(); } catch { }
                }
                else session.Release();
            }
        }
    }
}
