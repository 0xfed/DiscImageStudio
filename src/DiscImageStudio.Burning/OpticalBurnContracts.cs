using DiscImageStudio.Localization;
namespace DiscImageStudio.Burning;

public enum OpticalBurnMediaKind
{
    CdAudio,
    DvdData,
    BluRayData,
    CdData,
}

internal static class OpticalBurnMedia
{
    internal static bool UsesCdDao(OpticalBurnMediaKind mediaKind)
        => mediaKind is OpticalBurnMediaKind.CdAudio or OpticalBurnMediaKind.CdData;

    internal static int GetSectorBytes(OpticalBurnMediaKind mediaKind)
        => mediaKind switch
        {
            OpticalBurnMediaKind.CdAudio => 2352,
            OpticalBurnMediaKind.CdData or OpticalBurnMediaKind.DvdData or OpticalBurnMediaKind.BluRayData => 2048,
            _ => throw new ArgumentOutOfRangeException(nameof(mediaKind), mediaKind, L.T("Unknown optical media kind.")),
        };
}

public sealed record OpticalBurnDevice(
    string Id,
    string DisplayName,
    IReadOnlyList<string> VolumePaths);

public sealed record OpticalWriteSpeed(
    int SectorsPerSecond,
    bool RotationTypeIsPureCav)
{
    private const double DvdBytesPerSecondAtOneX = 1_385_000.0;
    private const double BluRayBytesPerSecondAtOneX = 4_500_000.0;
    private const double CdSectorsPerSecondAtOneX = 75.0;

    public double GetMultiplier(OpticalBurnMediaKind mediaKind)
        => mediaKind switch
        {
            OpticalBurnMediaKind.CdAudio or OpticalBurnMediaKind.CdData => SectorsPerSecond / CdSectorsPerSecondAtOneX,
            OpticalBurnMediaKind.DvdData => SectorsPerSecond * 2048.0 / DvdBytesPerSecondAtOneX,
            OpticalBurnMediaKind.BluRayData => SectorsPerSecond * 2048.0 / BluRayBytesPerSecondAtOneX,
            _ => throw new ArgumentOutOfRangeException(nameof(mediaKind), mediaKind, L.T("Unknown optical media kind.")),
        };

    public double GetMegabytesPerSecond(OpticalBurnMediaKind mediaKind)
    {
        int sectorBytes = OpticalBurnMedia.GetSectorBytes(mediaKind);
        return SectorsPerSecond * (double)sectorBytes / 1_000_000.0;
    }

    internal void Validate()
    {
        if (SectorsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SectorsPerSecond),
                L.T("Write speed must be greater than zero sectors per second."));
        }
    }
}

public sealed record OpticalBurnProgress(
    string Stage,
    string Message,
    long CompletedBytes,
    long TotalBytes,
    double? BytesPerSecond = null)
{
    public double Fraction => TotalBytes == 0 ? 0 : (double)CompletedBytes / TotalBytes;
}

// Workflow state is independent of the translated operation description.
internal sealed record OpticalBurnOperation(string Message, bool IsFinalizing = false);

/// <summary>
/// CD audio producers supply headerless 44.1 kHz, 16-bit stereo little-endian PCM,
/// as required by IMAPI_CD_SECTOR_AUDIO. ContentLength excludes any WAV header.
/// CD data, DVD and Blu-ray producers supply complete 2048-byte logical data sectors.
/// The recording backend adds physical encoding and error correction.
/// </summary>
public sealed record OpticalBurnRequest(
    string DeviceId,
    OpticalBurnMediaKind MediaKind,
    long ContentLength,
    Action<Stream, CancellationToken> ProduceContent,
    OpticalWriteSpeed? WriteSpeed = null,
    OpticalBurnBufferOptions? BufferOptions = null,
    bool LeaveDiscOpen = false)
{
    public OpticalBurnBufferOptions EffectiveBufferOptions => BufferOptions
        ?? (MediaKind == OpticalBurnMediaKind.BluRayData
            ? OpticalBurnBufferOptions.BluRayDefault : OpticalBurnBufferOptions.Default);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        ArgumentNullException.ThrowIfNull(ProduceContent);
        if (LeaveDiscOpen && MediaKind != OpticalBurnMediaKind.BluRayData)
            throw new ArgumentException(L.T("Only Blu-ray VAT recordings can be left open for file updates."));
        int sectorBytes = OpticalBurnMedia.GetSectorBytes(MediaKind);
        if (ContentLength <= 0 || ContentLength % sectorBytes != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ContentLength),
                L.F($"Stream length must be a positive multiple of {sectorBytes} bytes."));
        }

        WriteSpeed?.Validate();
        EffectiveBufferOptions.Validate();
    }
}

public sealed record OpticalBurnResult(
    string DeviceId,
    OpticalBurnMediaKind MediaKind,
    long BytesWritten,
    TimeSpan Elapsed,
    OpticalWriteSpeed? ActualWriteSpeed = null);

public interface IOpticalDiscBurner
{
    Task<OpticalBurnResult> AppendBluRayAsync(string deviceId,
        Func<BluRayAppendContext, BluRayAppendContent> prepareContent,
        OpticalWriteSpeed? writeSpeed = null, IProgress<OpticalBurnProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException(L.T("This burner does not support Blu-ray file updates."));

    Task<IReadOnlyList<OpticalBurnDevice>> GetDevicesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpticalWriteSpeed>> GetSupportedWriteSpeedsAsync(
        string deviceId,
        OpticalBurnMediaKind mediaKind,
        CancellationToken cancellationToken = default);

    Task<OpticalBurnResult> BurnAsync(
        OpticalBurnRequest request,
        IProgress<OpticalBurnProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record BluRayAppendContext(uint NextWritableLba, uint LastRecordedLba,
    uint CapacitySectors, Func<uint, byte[]> ReadSector);

public sealed record BluRayAppendContent(long ContentLength, Action<Stream, CancellationToken> ProduceContent);
