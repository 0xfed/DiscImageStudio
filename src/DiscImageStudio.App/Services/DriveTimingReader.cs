using DiscImageStudio.Burning;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Services;

internal sealed class DriveTimingReader(IOpticalReadSession session) : IDriveTimingReader
{
    public DriveTimingDisc Disc { get; } = new(
        session.Media.MediaKind switch
        {
            OpticalBurnMediaKind.CdData => DriveTimingMedia.Cd,
            OpticalBurnMediaKind.DvdData => DriveTimingMedia.Dvd,
            _ => throw new NotSupportedException(L.T("Timing.UnsupportedMedia")),
        },
        session.Media.RecordedSectors, session.Media.DeviceName);
    public long TimestampFrequency => session.TimestampFrequency;
    public int MaximumReadSectors => session.MaximumReadSectors;
    public DriveTimingRead Read(uint lba, int sectors, bool forceUnitAccess)
    {
        var read = session.Read(lba, sectors, forceUnitAccess);
        return new(read.StartTicks, read.EndTicks, read.Succeeded, read.SenseKey);
    }
    public bool FlushReadCache(uint lba) => session.FlushReadCache(lba);
    public void Pause(TimeSpan duration, CancellationToken cancellationToken)
    {
        if (cancellationToken.WaitHandle.WaitOne(duration)) cancellationToken.ThrowIfCancellationRequested();
    }
}
