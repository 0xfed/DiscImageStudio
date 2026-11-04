using DiscImageStudio.Localization;
using DiscImageStudio.Iso;

namespace DvdImageSolver;

internal sealed record HybridIsoWriteSummary(
    string Mode,
    uint VolumeSectors,
    int LogicalSectorBytes,
    string SourceDirectory,
    int FileCount,
    int DirectoryCount,
    long FileBytes,
    uint DataEndLbaExclusive,
    bool SparseFile,
    bool Iso9660,
    bool Joliet,
    bool FilesAtOuterRing = false,
    uint MetadataEndLbaExclusive = 0,
    uint FileDataStartLba = 0);

/// <summary>Compatibility facade for the media-independent ISO9660/Joliet writer.</summary>
public static class HybridIsoImageWriter
{
    public sealed class LayoutPlan
    {
        internal LayoutPlan(Iso9660ImageWriter.LayoutPlan layout) => Layout = layout;
        internal LayoutPlan(UdfImageWriter.LayoutPlan layout) => UdfLayout = layout;
        internal Iso9660ImageWriter.LayoutPlan? Layout { get; }
        internal UdfImageWriter.LayoutPlan? UdfLayout { get; }
        public bool UseUdf250 => UdfLayout is not null;
        public string SourceDirectory => UdfLayout?.SourceDirectory ?? Layout!.SourceDirectory;
        public uint VolumeSectors => UdfLayout?.CapacitySectors ?? Layout!.VolumeSectors;
        public uint RecordedSectors => UdfLayout?.VolumeSectors ?? Layout!.VolumeSectors;
        public int FileCount => UdfLayout?.FileCount ?? Layout!.FileCount;
        public int DirectoryCount => UdfLayout?.DirectoryCount ?? Layout!.DirectoryCount;
        public long FileBytes => UdfLayout?.FileBytes ?? Layout!.FileBytes;
        public uint DataEndLbaExclusive => UdfLayout?.DataEndLbaExclusive ?? Layout!.DataEndLbaExclusive;
        public uint MetadataEndLbaExclusive => UdfLayout?.DrawingStartLba ?? Layout!.MetadataEndLbaExclusive;
        public uint FileDataStartLba => UdfLayout?.DrawingEndLbaExclusive ?? Layout!.FileDataStartLba;
        public uint DrawingStartLba => UdfLayout?.DrawingStartLba
            ?? (FilesAtOuterRing ? Layout!.MetadataEndLbaExclusive : Layout!.DataEndLbaExclusive);
        public uint DrawingEndLbaExclusive => UdfLayout?.DrawingEndLbaExclusive
            ?? (FilesAtOuterRing ? Layout!.FileDataStartLba : Layout!.VolumeSectors);
        public bool FilesAtOuterRing => UdfLayout?.FilesAtOuterRing ?? Layout!.FilesAtOuterRing;
    }

    public static LayoutPlan Plan(string sourceDirectory, uint volumeSectors, string volumeLabel,
        bool filesAtOuterRing = false, bool useUdf250 = false, uint updateReserveSectors = 0)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory);
        if (updateReserveSectors >= volumeSectors || updateReserveSectors % 16 != 0
            || (!useUdf250 && updateReserveSectors != 0))
            throw new ArgumentOutOfRangeException(nameof(updateReserveSectors));
        if (useUdf250)
            return new(UdfImageWriter.Plan(sourceDirectory, volumeSectors - updateReserveSectors, volumeLabel,
                filesAtOuterRing: filesAtOuterRing, capacitySectors: volumeSectors, blockAlignment: 16));
        return new(Iso9660ImageWriter.Plan(sourceDirectory, volumeSectors, volumeLabel,
            filesAtOuterRing: filesAtOuterRing));
    }

    public static void WriteOuterRingPrefix(LayoutPlan plan, Stream output, uint endLbaExclusive,
        CancellationToken cancellationToken = default)
        => WriteSequentialPrefix(plan, output, endLbaExclusive, cancellationToken);

    public static void WriteOuterRingSuffix(LayoutPlan plan, Stream output, uint startLba,
        CancellationToken cancellationToken = default)
        {
        if (plan.UdfLayout is { } udf)
            UdfImageWriter.WriteSequentialSuffix(udf, output, startLba, cancellationToken);
        else Iso9660ImageWriter.WriteOuterRingSuffix(plan.Layout!, output, startLba, cancellationToken);
    }

    public static void WriteSequentialPrefix(LayoutPlan plan, Stream output, uint endLbaExclusive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.UdfLayout is { } udf)
            UdfImageWriter.WriteSequentialPrefix(udf, output, endLbaExclusive, cancellationToken);
        else if (plan.FilesAtOuterRing)
            Iso9660ImageWriter.WriteOuterRingPrefix(plan.Layout!, output, endLbaExclusive, cancellationToken);
        else Iso9660ImageWriter.WriteSequentialPrefix(plan.Layout!, output, endLbaExclusive, cancellationToken);
    }

    internal static HybridIsoWriteSummary Create(string sourceDirectory, string outputPath,
        uint volumeSectors, string volumeLabel)
        => Create(Plan(sourceDirectory, volumeSectors, volumeLabel), outputPath);

    internal static HybridIsoWriteSummary Create(LayoutPlan plan, string outputPath)
    {
        string sourceFullPath = plan.SourceDirectory;
        uint volumeSectors = plan.VolumeSectors;
        string outputFullPath = Path.GetFullPath(outputPath);
        if (Iso9660ImageWriter.IsWithinDirectory(outputFullPath, sourceFullPath))
            throw new ArgumentException(L.T("--iso-output must be outside --data-dir so the generated image cannot include itself."));
        string outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException(L.T("ISO output path has no parent directory."), nameof(outputPath));
        Directory.CreateDirectory(outputDirectory);
        string temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(outputFullPath)}.{Guid.NewGuid():N}.tmp");
        bool sparse = false;
        try
        {
            using (FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, bufferSize: 128 * 1024, FileOptions.SequentialScan))
            {
                sparse = IsoImageWriter.TryMarkSparse(output.SafeFileHandle);
                output.SetLength(checked((long)volumeSectors * IsoImageWriter.LogicalSectorBytes));
                if (plan.UseUdf250)
                {
                    WriteSequentialPrefix(plan, output, plan.DrawingStartLba);
                    output.Position = checked((long)plan.DrawingEndLbaExclusive * IsoImageWriter.LogicalSectorBytes);
                    WriteOuterRingSuffix(plan, output, plan.DrawingEndLbaExclusive);
                }
                else if (plan.FilesAtOuterRing)
                {
                    WriteOuterRingPrefix(plan, output, plan.MetadataEndLbaExclusive);
                    output.Position = checked((long)plan.FileDataStartLba * IsoImageWriter.LogicalSectorBytes);
                    WriteOuterRingSuffix(plan, output, plan.FileDataStartLba);
                }
                else
                {
                    WriteSequentialPrefix(plan, output, plan.DataEndLbaExclusive);
                }
                output.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, outputFullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return new(plan.UseUdf250 ? "hybrid-udf250-vat" : "hybrid-iso9660-joliet", volumeSectors, IsoImageWriter.LogicalSectorBytes,
            sourceFullPath, plan.FileCount, plan.DirectoryCount, plan.FileBytes,
            plan.DataEndLbaExclusive, sparse, Iso9660: !plan.UseUdf250, Joliet: !plan.UseUdf250,
            plan.FilesAtOuterRing, plan.MetadataEndLbaExclusive, plan.FileDataStartLba);
    }
}
