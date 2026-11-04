using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Burning;

internal sealed record SequentialFileMedia(int Status, uint NextWritableLba, uint LastRecordedLba,
    uint CapacitySectors, uint FreeSectors);

internal interface ISequentialFileUpdateSession
{
    void Acquire();
    SequentialFileMedia ReadMedia();
    byte[] ReadSector(uint lba);
    byte[] ReadSectors(uint lba, ushort count)
        => SequentialSectorReader.Read(ReadSector, lba, count);
    OpticalWriteSpeed ConfigureWrite(OpticalWriteSpeed? requested);
    void Write(IStream content);
    void CancelWrite();
    void Release();
}

public sealed partial class WindowsImapiBurner
{
    public Task<OpticalBurnResult> UpdateFilesAsync(string deviceId, OpticalBurnMediaKind mediaKind,
        Func<FileUpdateContext, FileUpdateContent> prepareContent,
        OpticalWriteSpeed? writeSpeed = null, IProgress<OpticalBurnProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(prepareContent);
        writeSpeed?.Validate();
        if (mediaKind == OpticalBurnMediaKind.BluRayData)
            return AppendBluRayAsync(deviceId, context =>
            {
                FileUpdateContent content = prepareContent(new(context.NextWritableLba, context.LastRecordedLba,
                    context.CapacitySectors, context.ReadSector, 32, false, context.ReadSectors));
                return new(content.ContentLength, content.ProduceContent);
            }, writeSpeed, progress, cancellationToken);
        if (mediaKind is not (OpticalBurnMediaKind.CdData or OpticalBurnMediaKind.DvdData))
            throw new ArgumentException(L.T("File updates require a data disc with UDF 2.50 VAT."));
        return _operationGate.RunAsync(() => RunOnStaThread(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            dynamic recorder = CreateRecorder(deviceId);
            dynamic? format = null;
            Exception? failure = null;
            List<string> warnings = [];
            OpticalBurnResult? result = null;
            try
            {
                format = CreateComObject("IMAPI2.MsftDiscFormat2Data");
                format.Recorder = recorder;
                format.ClientName = "Disc Image Studio";
                if (!(bool)format.IsRecorderSupported(recorder) || !(bool)format.IsCurrentMediaSupported(recorder))
                    throw new InvalidOperationException(L.T("The recorder cannot update this disc."));
                OpticalWriteSpeed? speed = ResolveWriteSpeed(writeSpeed, ReadSupportedWriteSpeeds(format));
                using DynamicFileUpdateSession session = new((object)recorder, (object)format, mediaKind, warnings.Add);
                result = BurnFileUpdate(session, deviceId, mediaKind, prepareContent, speed, progress, cancellationToken);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                CompleteBurnCleanup(failure, warnings,
                    () => { if (format is not null) ReleaseComObject((object)format); },
                    () => ReleaseComObject((object)recorder));
            }
            return result!;
        }), cancellationToken);
    }

    internal static OpticalBurnResult BurnFileUpdate(ISequentialFileUpdateSession session, string deviceId,
        OpticalBurnMediaKind kind, Func<FileUpdateContext, FileUpdateContent> prepareContent,
        OpticalWriteSpeed? speed = null, IProgress<OpticalBurnProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        bool acquired = false;
        Exception? failure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.Acquire();
            acquired = true;
            SequentialFileMedia media = session.ReadMedia();
            uint alignment = kind == OpticalBurnMediaKind.CdData ? 1u : 16u;
            // APPENDABLE without BLANK, FINALIZED, WRITE_PROTECTED, DAMAGED,
            // ERASE_REQUIRED, NON_EMPTY_SESSION or UNSUPPORTED. Native Write
            // starts a fresh session, so interrupted open sessions are rejected.
            const int invalid = 2 | 8 | 0x400 | 0x800 | 0x1000 | 0x2000 | 0x4000 | 0x8000;
            if ((media.Status & 4) == 0 || (media.Status & invalid) != 0
                || media.LastRecordedLba >= media.NextWritableLba || media.NextWritableLba % alignment != 0
                || media.NextWritableLba >= media.CapacitySectors
                || media.FreeSectors > media.CapacitySectors - media.NextWritableLba)
                throw new InvalidOperationException(L.T("Insert an open sequential UDF 2.50 VAT disc with unrecorded space."));
            progress?.Report(new("media", L.T("Preparing file update…"), 0, 0));
            FileUpdateContent prepared = prepareContent(new(media.NextWritableLba, media.LastRecordedLba,
                media.CapacitySectors, session.ReadSector, alignment, true, session.ReadSectors));
            OpticalBurnRequest request = new(deviceId, kind, prepared.ContentLength, prepared.ProduceContent,
                speed, LeaveDiscOpen: true, UseUdf250: true);
            request.Validate();
            long sectors = prepared.ContentLength / 2048;
            long required = sectors + (kind == OpticalBurnMediaKind.CdData ? 150 : 0);
            if (sectors % alignment != 0 || required > media.FreeSectors
                || (ulong)media.NextWritableLba + (ulong)required > media.CapacitySectors)
                throw new InvalidOperationException(L.T("Not enough unrecorded disc space for the files and VAT update."));
            OpticalWriteSpeed actual = session.ConfigureWrite(speed);
            actual.Validate();
            using GeneratedContentComStream content = new(prepared.ContentLength, prepared.ProduceContent,
                (done, total) => progress?.Report(new("transfer", L.T("Preparing file update…"), done, total)),
                cancellationToken, request.EffectiveBufferOptions);
            content.WaitUntilPrebuffered(cancellationToken);
            SequentialFileMedia current = session.ReadMedia();
            // Disabling the automatic CD postgap can change the usable free
            // count. Recorded addresses must stay fixed, and capacity must
            // still cover the update after applying recording options.
            if (current with { FreeSectors = media.FreeSectors } != media
                || required > current.FreeSectors
                || current.FreeSectors > current.CapacitySectors - current.NextWritableLba)
                throw new InvalidOperationException(L.T("The disc changed while preparing the file update."));
            using (cancellationToken.Register(session.CancelWrite))
            {
                cancellationToken.ThrowIfCancellationRequested();
                session.Write(content);
            }
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new("complete", L.T("Files updated. The disc remains open for further updates."),
                prepared.ContentLength, prepared.ContentLength));
            return new(deviceId, kind, prepared.ContentLength, elapsed.Elapsed, actual);
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
                if (failure is null) session.Release();
                else { try { session.Release(); } catch { /* Preserve the write error. */ } }
            }
        }
    }

    internal static void ValidateVatMedia(OpticalBurnMediaKind kind, int physicalType, IReadOnlyCollection<int> profiles)
    {
        bool supported = kind switch
        {
            OpticalBurnMediaKind.CdData => physicalType == 2 && profiles.Contains(0x09),
            OpticalBurnMediaKind.DvdData => physicalType switch
            {
                6 => profiles.Contains(0x1b), // DVD+R, including 4.7 GB M-DISC
                9 => profiles.Contains(0x11), // DVD-R
                _ => false,
            },
            _ => false,
        };
        if (!supported)
            throw new InvalidOperationException(L.T("VAT file updates require CD-R or single-layer DVD-R/+R media. Rewritable media are not supported by this update path."));
    }

    private static int[] ReadCurrentProfiles(object recorder)
    {
        object profiles = ((dynamic)recorder).CurrentProfiles;
        return profiles is Array values ? values.Cast<object>().Select(Convert.ToInt32).ToArray() : [];
    }

    private sealed class DynamicFileUpdateSession : ISequentialFileUpdateSession, IDisposable
    {
        private readonly dynamic _recorder;
        private readonly dynamic _format;
        private readonly OpticalBurnMediaKind _kind;
        private readonly BluRayMmcCommands _commands;
        private readonly BluRaySessionCleanup _cleanup;

        internal DynamicFileUpdateSession(object recorder, object format, OpticalBurnMediaKind kind, Action<string> warning)
        {
            _recorder = recorder;
            _format = format;
            _kind = kind;
            _commands = new(new ImapiMmcCommandTransport(recorder));
            _cleanup = new(
                acquireExclusive: () => _recorder.AcquireExclusiveAccess(true, "Disc Image Studio"),
                disableMcn: () => _recorder.DisableMcn(),
                preventRemoval: () => _commands.PreventRemoval(true),
                allowRemoval: () => _commands.PreventRemoval(false),
                enableMcn: () => _recorder.EnableMcn(),
                releaseExclusive: () => _recorder.ReleaseExclusiveAccess(),
                releaseWriteEngine: () => { }, reportCleanupWarning: warning);
        }

        public void Acquire() => _cleanup.Acquire();
        public void Release() => _cleanup.Release();
        public void Dispose() => _cleanup.Dispose();
        public SequentialFileMedia ReadMedia()
        {
            ValidateVatMedia(_kind, Convert.ToInt32(_format.CurrentPhysicalMediaType), ReadCurrentProfiles((object)_recorder));
            return new(Convert.ToInt32(_format.CurrentMediaStatus), Convert.ToUInt32(_format.NextWritableAddress),
                Convert.ToUInt32(_format.LastWrittenAddressOfPreviousSession), Convert.ToUInt32(_format.TotalSectorsOnMedia),
                Convert.ToUInt32(_format.FreeSectorsOnMedia));
        }
        public byte[] ReadSector(uint lba) => _commands.ReadSector(lba);
        public byte[] ReadSectors(uint lba, ushort count) => _commands.ReadSectors(lba, count);
        public OpticalWriteSpeed ConfigureWrite(OpticalWriteSpeed? requested)
        {
            ConfigureDataFormat((object)_format, leaveOpen: true, udfCd: _kind == OpticalBurnMediaKind.CdData);
            if (requested is not null) _format.SetWriteSpeed(requested.SectorsPerSecond, requested.RotationTypeIsPureCav);
            return new(Convert.ToInt32(_format.CurrentWriteSpeed), Convert.ToBoolean(_format.CurrentRotationTypeIsPureCAV));
        }
        public void Write(IStream content) => _format.Write(content);
        public void CancelWrite() => TryCancel(_format, cdAudio: false);
    }
}
