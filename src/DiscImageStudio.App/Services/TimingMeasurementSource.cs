using System.IO;
using DiscImageStudio.Burning;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Iso;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Services;

/// <summary>The ordinary data image selected for a timing measurement burn.
/// Capture before navigation so later calibration edits cannot change its bytes or family.</summary>
internal sealed record TimingMeasurementSource(DriveTimingMedia Media, uint Sectors, DateTimeOffset Timestamp)
{
    internal OpticalBurnMediaKind MediaKind => Media switch
    {
        DriveTimingMedia.Cd => OpticalBurnMediaKind.CdData,
        DriveTimingMedia.Dvd => OpticalBurnMediaKind.DvdData,
        _ => throw new ArgumentException(L.T("Timing.UnsupportedMedia")),
    };

    internal string FamilyLabel => MediaKind == OpticalBurnMediaKind.CdData ? "CD" : "DVD";
    internal long ContentLength => CalibrationDataImage.GetContentLength(Sectors);

    internal void Write(Stream output, CancellationToken cancellationToken,
        IProgress<double>? progress = null)
        => CalibrationDataImage.Write(output, Sectors, progress: progress,
            cancellationToken: cancellationToken, timestamp: Timestamp);

    internal OpticalBurnRequest CreateBurnRequest(string deviceId, OpticalWriteSpeed? writeSpeed,
        long writableSectors)
    {
        uint sectors = checked((uint)StreamBurnCapacity.Resolve(Sectors, writableSectors, MediaKind));
        var source = this with { Sectors = sectors };
        return new OpticalBurnRequest(deviceId, MediaKind, source.ContentLength,
            (output, token) => source.Write(output, token), writeSpeed);
    }
}
