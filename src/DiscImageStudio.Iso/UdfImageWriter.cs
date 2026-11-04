using DiscImageStudio.Localization;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace DiscImageStudio.Iso;

/// <summary>
/// Plans a UDF 2.50 sequential volume with a Virtual Allocation Table (VAT).
/// Metadata uses virtual addresses; ordinary file data uses the physical partition.
/// ECMA-167 (3rd edition), ECMA TR/112-3 (UDF 2.50, including errata).
/// </summary>
public static class UdfImageWriter
{
    public const int LogicalSectorBytes = 2048;
    public const uint MinimumVolumeSectors = 640;
    public const ushort UdfRevision = 0x0250;
    private const uint MainVdsLba = 32;
    private const uint VdsSectors = 16;
    private const uint IntegrityLba = 48;
    private const uint ReserveVdsLba = 64;
    private const uint FirstPartitionLba = 288;
    private const uint MaximumExtentBytes = 0x3ffff800;
    private const string ImplementationId = "*DiscImageStudio";

    public sealed record SequentialSnapshot(uint PartitionStartLba, uint CapacitySectors,
        uint RecordedSectors, string VolumeLabel, uint VatIcb, ulong LastUniqueId, uint ReserveVdsLba);

    /// <summary>Reads only committed Disc Image Studio VAT volumes. The caller supplies
    /// exact 2048-byte sectors and the drive's last recorded address while holding its lock.</summary>
    public static SequentialSnapshot ReadSequentialSnapshot(Func<uint, byte[]> readSector, uint lastRecordedLba)
    {
        ArgumentNullException.ThrowIfNull(readSector);
        byte[] anchor = Read(256, 2, 256);
        uint sequence = Read32(anchor, 20), count = Read32(anchor, 16) / LogicalSectorBytes;
        if (count is < 6 or > 256 || lastRecordedLba < MinimumVolumeSectors - 1)
            throw InvalidVolume();
        byte[]? partition = null;
        byte[]? logical = null;
        for (uint index = 0; index < count; index++)
        {
            byte[] descriptor = readSector(checked(sequence + index));
            if (descriptor.Length != LogicalSectorBytes) throw InvalidVolume();
            ushort type = Read16(descriptor, 0);
            Read(checked(sequence + index), type, checked(sequence + index));
            if (type == 5) partition = descriptor;
            if (type == 6) logical = descriptor;
            if (type == 8) break;
        }
        if (partition is null || logical is null || Read32(partition, 184) != 2
            || Encoding.ASCII.GetString(partition, 197, 23).TrimEnd('\0') != ImplementationId
            || Read32(logical, 212) != LogicalSectorBytes || Read16(logical, 240) != UdfRevision
            || Read32(logical, 264) != 70 || Read32(logical, 268) != 2
            || logical[446] != 2 || logical[447] != 64
            || Encoding.ASCII.GetString(logical, 451, 23).TrimEnd('\0') != "*UDF Virtual Partition")
            throw InvalidVolume();
        uint start = Read32(partition, 188);
        uint capacity = checked(start + Read32(partition, 192));
        if (start < FirstPartitionLba || lastRecordedLba < start || lastRecordedLba >= capacity)
            throw InvalidVolume();
        byte[] entry = Read(lastRecordedLba, 261, lastRecordedLba - start);
        ulong length = BinaryPrimitives.ReadUInt64LittleEndian(entry.AsSpan(56, 8));
        uint position = Read32(entry, 180);
        if (entry[27] != 248 || Read16(entry, 34) != 0 || Read32(entry, 168) != 0
            || Read32(entry, 172) != 8 || length < 164 || length > MaximumExtentBytes
            || Read32(entry, 176) != length || (ulong)start + position + SectorCount((long)length) > lastRecordedLba)
            throw InvalidVolume();
        byte[] header = readSector(checked(start + position));
        if (header.Length != LogicalSectorBytes || Read16(header, 0) != 152 || Read16(header, 2) != 0
            || (length - 152) % 4 != 0 || Read16(header, 144) != UdfRevision
            || Read16(header, 146) != UdfRevision || Read16(header, 148) != UdfRevision)
            throw InvalidVolume();
        int labelLength = header[131];
        if (labelLength is < 2 or > 127 || header[4] is not (8 or 16)) throw InvalidVolume();
        string label = header[4] == 8 ? Encoding.Latin1.GetString(header, 5, labelLength - 1)
            : Encoding.BigEndianUnicode.GetString(header, 5, labelLength - 1);
        return new(start, capacity, checked(lastRecordedLba + 1), label, lastRecordedLba - start,
            BinaryPrimitives.ReadUInt64LittleEndian(entry.AsSpan(160, 8)), Read32(anchor, 28));

        byte[] Read(uint sector, ushort type, uint location)
        {
            if (sector > lastRecordedLba) throw InvalidVolume();
            byte[] bytes = readSector(sector);
            if (bytes.Length != LogicalSectorBytes || Read16(bytes, 0) != type || Read16(bytes, 2) != 3
                || Read32(bytes, 12) != location || Read16(bytes, 10) > LogicalSectorBytes - 16)
                throw InvalidVolume();
            int checksum = 0;
            for (int index = 0; index < 16; index++) if (index != 4) checksum += bytes[index];
            if ((byte)checksum != bytes[4] || Crc(bytes.AsSpan(16, Read16(bytes, 10))) != Read16(bytes, 8))
                throw InvalidVolume();
            return bytes;
        }
    }

    /// <summary>The source folder is the complete desired file tree. Old sectors,
    /// including the original drawing, are preserved; removed files consume no new data.</summary>
    public static LayoutPlan PlanAppend(string sourceDirectory, SequentialSnapshot previous,
        uint nextWritableLba, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (nextWritableLba < previous.RecordedSectors || nextWritableLba % 32 != 0
            || nextWritableLba >= previous.CapacitySectors)
            throw new ArgumentOutOfRangeException(nameof(nextWritableLba));
        LayoutPlan scanned = Plan(sourceDirectory, previous.CapacitySectors, previous.VolumeLabel, timestamp);
        uint shift = checked(nextWritableLba - FirstPartitionLba);
        foreach (Node node in scanned.Nodes)
        {
            node.EntryLba = checked(node.EntryLba + shift);
            node.DataLba = checked(node.DataLba + shift);
            if (node.UniqueId != 0)
                node.UniqueId = checked(previous.LastUniqueId + node.UniqueId - 15);
        }
        uint dataEnd = checked(scanned.DataEndLbaExclusive + shift);
        uint end = checked((dataEnd + scanned.VatSectors + 257 + 31) / 32 * 32);
        if (end > previous.CapacitySectors)
            throw new ArgumentException(L.T("Not enough unrecorded disc space for the files and VAT update."));
        return new LayoutPlan(scanned.SourceDirectory, end, previous.VolumeLabel, scanned.Timestamp,
            scanned.Nodes, previous.PartitionStartLba, dataEnd, dataEnd, dataEnd, false,
            scanned.MetadataSectors, previous.CapacitySectors)
        {
            StartLba = nextWritableLba,
            MetadataStartLba = nextWritableLba,
            PreviousVatIcb = previous.VatIcb,
            PreviousLastUniqueId = previous.LastUniqueId,
            OriginalReserveVdsLba = previous.ReserveVdsLba,
        };
    }

    public static void WriteAppend(LayoutPlan plan, Stream output, CancellationToken cancellationToken = default)
    {
        ValidateOutput(plan, output);
        if (plan.StartLba == 0) throw new ArgumentException(nameof(plan));
        ValidateSources(plan, cancellationToken);
        SequentialWriter writer = new(output, plan.StartLba, cancellationToken);
        WritePartition(plan, writer);
        writer.WriteAtLba(plan.VatDataLba, VirtualAllocationTable(plan));
        writer.WriteAtLba(plan.VolumeSectors - 257, Anchor(plan, plan.VolumeSectors - 257));
        ValidateSources(plan, cancellationToken);
        writer.WriteAtLba(plan.VolumeSectors - 1, VatFileEntry(plan));
        writer.PadToLba(plan.VolumeSectors);
    }

    private static ushort Read16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
    private static uint Read32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    private static InvalidDataException InvalidVolume() => new(L.T("The disc is not a committed Disc Image Studio UDF 2.50 VAT volume."));

    public sealed class LayoutPlan
    {
        internal LayoutPlan(string sourceDirectory, uint volumeSectors, string volumeLabel,
            DateTimeOffset timestamp, List<Node> nodes, uint partitionStart, uint dataEnd,
            uint drawingStart, uint drawingEnd, bool filesAtOuterRing, uint metadataSectors,
            uint capacitySectors)
        {
            SourceDirectory = sourceDirectory;
            VolumeSectors = volumeSectors;
            VolumeLabel = volumeLabel;
            Timestamp = timestamp;
            Nodes = nodes;
            PartitionStartLba = partitionStart;
            DataEndLbaExclusive = dataEnd;
            DrawingStartLba = drawingStart;
            DrawingEndLbaExclusive = drawingEnd;
            FilesAtOuterRing = filesAtOuterRing;
            MetadataStartLba = partitionStart;
            MetadataSectors = metadataSectors;
            CapacitySectors = capacitySectors;
            FileCount = nodes.Count(node => !node.IsDirectory);
            DirectoryCount = nodes.Count - FileCount;
            FileBytes = nodes.Where(node => !node.IsDirectory).Sum(node => node.DataLength);
        }

        public string SourceDirectory { get; }
        public uint VolumeSectors { get; internal set; }
        public uint CapacitySectors { get; }
        public uint StartLba { get; internal set; }
        public uint PreviousVatIcb { get; internal set; } = uint.MaxValue;
        internal ulong PreviousLastUniqueId { get; set; } = 15;
        internal uint? OriginalReserveVdsLba { get; set; }
        public int FileCount { get; }
        public int DirectoryCount { get; }
        public long FileBytes { get; }
        public uint DataEndLbaExclusive { get; internal set; }
        public uint DrawingStartLba { get; }
        public uint DrawingEndLbaExclusive { get; }
        public bool FilesAtOuterRing { get; }
        public string VolumeLabel { get; }
        public DateTimeOffset Timestamp { get; }
        internal List<Node> Nodes { get; }
        internal uint ReserveVdsLba => OriginalReserveVdsLba ?? UdfImageWriter.ReserveVdsLba;
        internal uint PartitionStartLba { get; }
        internal uint PartitionSectors => CapacitySectors - PartitionStartLba;
        internal uint MetadataStartLba { get; set; }
        internal uint MetadataSectors { get; }
        internal uint VatSectors => SectorCount(checked(152L + 4L * MetadataSectors));
        internal uint VatDataLba => VolumeSectors - 257 - VatSectors;
    }

    public static LayoutPlan Plan(string? sourceDirectory, uint volumeSectors, string volumeLabel,
        DateTimeOffset? timestamp = null, bool filesAtOuterRing = false, uint? capacitySectors = null)
    {
        if (volumeSectors < MinimumVolumeSectors || volumeSectors % 32 != 0)
            throw new ArgumentOutOfRangeException(nameof(volumeSectors),
                L.F($"UDF drawing volumes need at least {MinimumVolumeSectors} sectors and a multiple of 32 sectors."));
        uint capacity = capacitySectors ?? volumeSectors;
        if (capacity < volumeSectors || capacity % 32 != 0)
            throw new ArgumentOutOfRangeException(nameof(capacitySectors));
        ArgumentNullException.ThrowIfNull(volumeLabel);
        DateTimeOffset recorded = timestamp ?? DateTimeOffset.UtcNow;
        string sourcePath = sourceDirectory is null ? string.Empty : Path.GetFullPath(sourceDirectory);
        if (sourceDirectory is not null && !Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException(L.F($"Data directory does not exist: {sourcePath}"));
        if (sourceDirectory is not null)
            RejectReparsePoint(sourcePath);

        Node root = new(sourcePath, string.Empty, true, null)
        {
            Timestamp = sourceDirectory is null ? recorded : Directory.GetLastWriteTimeUtc(sourcePath),
        };
        List<Node> nodes = [root];
        // Iterative traversal avoids a call-stack dependency on the depth of the source tree.
        for (int index = 0; index < nodes.Count; index++)
        {
            Node directory = nodes[index];
            if (!directory.IsDirectory || directory.SourcePath.Length == 0)
                continue;
            foreach (FileSystemInfo entry in new DirectoryInfo(directory.SourcePath)
                         .EnumerateFileSystemInfos().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                RejectReparsePoint(entry.FullName);
                bool isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
                Node child = new(entry.FullName, entry.Name, isDirectory, directory)
                {
                    Timestamp = entry.LastWriteTimeUtc,
                    Identifier = EncodeIdentifier(entry.Name),
                    DataLength = isDirectory ? 0 : ((FileInfo)entry).Length,
                    UniqueId = checked((ulong)nodes.Count + 15),
                };
                directory.Children.Add(child);
                nodes.Add(child);
            }
        }

        uint cursor = checked(FirstPartitionLba + 2);
        foreach (Node node in nodes)
        {
            node.EntryLba = cursor++;
            if (node.IsDirectory)
            {
                node.DataLength = FileIdentifierLength(0, 0);
                foreach (Node child in node.Children)
                    node.DataLength = checked(node.DataLength
                        + FileIdentifierLength(child.Identifier.Length, node.DataLength));
            }
            // A single-layer Blu-ray needs at most 24 descriptors. Fail explicitly if this
            // generic writer is ever used beyond one FE's capacity instead of truncating it.
            long extentSize = node.IsDirectory ? LogicalSectorBytes : MaximumExtentBytes;
            if ((node.DataLength + extentSize - 1) / extentSize
                > (LogicalSectorBytes - 176) / (node.IsDirectory ? 8 : 16))
                throw new ArgumentException(L.F($"File needs more allocation descriptors than one UDF file entry supports: {node.SourcePath}"));
            if (node.Children.Count(child => child.IsDirectory) >= ushort.MaxValue)
                throw new ArgumentException(L.F($"Too many child directories for a UDF link count: {node.SourcePath}"));
        }
        foreach (Node node in nodes.Where(node => node.IsDirectory))
        {
            node.DataLba = cursor;
            cursor = checked(cursor + SectorCount(node.DataLength));
        }
        uint metadataSectors = cursor - FirstPartitionLba;
        foreach (Node node in nodes.Where(node => !node.IsDirectory))
        {
            node.DataLba = cursor;
            cursor = checked(cursor + SectorCount(node.DataLength));
        }
        uint vatSectors = SectorCount(checked(152L + 4L * metadataSectors));
        uint drawingEnd = (volumeSectors - 257 - vatSectors) / 32 * 32;
        uint drawingStart = checked((cursor + 31) / 32 * 32);
        if (drawingStart >= drawingEnd)
            throw new ArgumentException(L.F($"Files and UDF metadata need {cursor} sectors, leaving no complete drawing cluster before the backup metadata at sector {drawingEnd}."));
        uint partitionStart = FirstPartitionLba;
        if (filesAtOuterRing)
        {
            // Relocate the entire physical partition, keeping picture bytes outside it.
            // Its relative addresses stay identical; only the descriptor's partition
            // origin and the absolute sequential write positions change.
            uint partitionSectors = cursor - FirstPartitionLba;
            partitionStart = (drawingEnd - partitionSectors) / 32 * 32;
            uint shift = partitionStart - FirstPartitionLba;
            foreach (Node node in nodes)
            {
                node.EntryLba = checked(node.EntryLba + shift);
                node.DataLba = checked(node.DataLba + shift);
            }
            cursor = checked(cursor + shift);
            drawingStart = FirstPartitionLba;
            drawingEnd = partitionStart;
        }
        string label = string.IsNullOrWhiteSpace(volumeLabel) ? "BD_IMAGE" : volumeLabel;
        // All descriptors use precisely the same logical volume identifier.
        label = FitDstring(label, 128);
        return new LayoutPlan(sourcePath, volumeSectors, label, recorded, nodes, partitionStart,
            cursor, drawingStart, drawingEnd, filesAtOuterRing, metadataSectors, capacity);
    }

    public static void WriteSequentialPrefix(LayoutPlan plan, Stream output, uint endLbaExclusive,
        CancellationToken cancellationToken = default)
    {
        ValidateOutput(plan, output);
        uint minimumEnd = plan.FilesAtOuterRing ? plan.DrawingStartLba : plan.DataEndLbaExclusive;
        if (endLbaExclusive < minimumEnd || endLbaExclusive > plan.DrawingEndLbaExclusive)
            throw new ArgumentOutOfRangeException(nameof(endLbaExclusive));
        ValidateSources(plan, cancellationToken);
        SequentialWriter writer = new(output, 0, cancellationToken);
        writer.WriteAtLba(16, VolumeRecognition("BEA01"));
        writer.WriteAtLba(17, VolumeRecognition("NSR03"));
        writer.WriteAtLba(18, VolumeRecognition("TEA01"));
        WriteVolumeDescriptors(plan, writer, MainVdsLba);
        writer.WriteAtLba(IntegrityLba, IntegrityDescriptor(plan));
        writer.WriteAtLba(IntegrityLba + 1, Descriptor(8, IntegrityLba + 1, 512));
        WriteVolumeDescriptors(plan, writer, plan.ReserveVdsLba);
        writer.WriteAtLba(256, Anchor(plan, 256));
        if (!plan.FilesAtOuterRing)
            WritePartition(plan, writer);
        writer.PadToLba(endLbaExclusive);
        ValidateSources(plan, cancellationToken);
    }

    private static void WritePartition(LayoutPlan plan, SequentialWriter writer)
    {
        writer.WriteAtLba(plan.MetadataStartLba, FileSetDescriptor(plan));
        writer.WriteAtLba(plan.MetadataStartLba + 1, Descriptor(8, 1, 512));
        foreach (Node node in plan.Nodes)
            writer.WriteAtLba(node.EntryLba, FileEntry(plan, node));
        foreach (Node directory in plan.Nodes.Where(node => node.IsDirectory))
        {
            writer.PadToLba(directory.DataLba);
            long directoryOffset = 0;
            WriteIdentifier(directory.Parent ?? directory, [], parent: true);
            foreach (Node child in directory.Children)
                WriteIdentifier(child, child.Identifier, parent: false);

            void WriteIdentifier(Node target, byte[] identifier, bool parent)
            {
                uint location = checked(directory.DataLba - plan.MetadataStartLba
                    + (uint)(directoryOffset / LogicalSectorBytes));
                byte[] record = FileIdentifier(plan, target, identifier, parent, location, directoryOffset);
                writer.Write(record);
                directoryOffset += record.Length;
            }
        }
        foreach (Node file in plan.Nodes.Where(node => !node.IsDirectory))
            writer.WriteFile(file);
    }

    /// <summary>Appends the reserved tail after the caller has written the drawing range.</summary>
    public static void WriteSequentialSuffix(LayoutPlan plan, Stream output, uint startLba,
        CancellationToken cancellationToken = default)
    {
        ValidateOutput(plan, output);
        if (startLba != plan.DrawingEndLbaExclusive)
            throw new ArgumentOutOfRangeException(nameof(startLba), L.T("The UDF suffix must start at DrawingEndLbaExclusive."));
        cancellationToken.ThrowIfCancellationRequested();
        SequentialWriter writer = new(output, startLba, cancellationToken);
        if (plan.FilesAtOuterRing)
        {
            ValidateSources(plan, cancellationToken);
            WritePartition(plan, writer);
            ValidateSources(plan, cancellationToken);
        }
        writer.WriteAtLba(plan.VatDataLba, VirtualAllocationTable(plan));
        writer.WriteAtLba(plan.VolumeSectors - 257, Anchor(plan, plan.VolumeSectors - 257));
        writer.WriteAtLba(plan.VolumeSectors - 1, VatFileEntry(plan));
        writer.PadToLba(plan.VolumeSectors);
    }

    private static void WriteVolumeDescriptors(LayoutPlan plan, SequentialWriter writer, uint start)
    {
        writer.WriteAtLba(start, PrimaryDescriptor(plan, start));
        writer.WriteAtLba(start + 1, ImplementationDescriptor(plan, start + 1));
        writer.WriteAtLba(start + 2, PartitionDescriptor(plan, start + 2));
        writer.WriteAtLba(start + 3, LogicalVolumeDescriptor(plan, start + 3));
        byte[] unallocated = new byte[LogicalSectorBytes];
        U32(unallocated, 16, 5);
        // Zero free extents: pattern bytes outside the partition are reserved volume space.
        SetTag(unallocated, 7, start + 4, 24);
        writer.WriteAtLba(start + 4, unallocated);
        writer.WriteAtLba(start + 5, Descriptor(8, start + 5, 512));
        writer.PadToLba(start + VdsSectors);
    }

    private static byte[] VolumeRecognition(string identifier)
    {
        byte[] sector = new byte[LogicalSectorBytes];
        Encoding.ASCII.GetBytes(identifier, sector.AsSpan(1, 5));
        sector[6] = 1;
        return sector;
    }

    private static byte[] PrimaryDescriptor(LayoutPlan plan, uint location)
    {
        byte[] data = new byte[LogicalSectorBytes];
        U32(data, 16, 1);
        Dstring(data.AsSpan(24, 32), FitDstring(plan.VolumeLabel, 32));
        U16(data, 56, 1);
        U16(data, 58, 1);
        U16(data, 60, 2);
        U16(data, 62, 3);
        U32(data, 64, 1);
        U32(data, 68, 1);
        string volumeSet = unchecked((uint)plan.Timestamp.ToUnixTimeSeconds()).ToString("X8", CultureInfo.InvariantCulture)
            + "00000000";
        Dstring(data.AsSpan(72, 128), volumeSet);
        Charspec(data.AsSpan(200, 64));
        Charspec(data.AsSpan(264, 64));
        Entity(data.AsSpan(344, 32), ImplementationId);
        Timestamp(data.AsSpan(376, 12), plan.Timestamp);
        Entity(data.AsSpan(388, 32), ImplementationId);
        U16(data, 488, 1);
        SetTag(data, 1, location, 512);
        return data;
    }

    private static byte[] ImplementationDescriptor(LayoutPlan plan, uint location)
    {
        byte[] data = new byte[LogicalSectorBytes];
        U32(data, 16, 2);
        Entity(data.AsSpan(20, 32), "*UDF LV Info", udfRevision: true);
        Charspec(data.AsSpan(52, 64));
        Dstring(data.AsSpan(116, 128), plan.VolumeLabel);
        Entity(data.AsSpan(352, 32), ImplementationId);
        SetTag(data, 4, location, 512);
        return data;
    }

    private static byte[] PartitionDescriptor(LayoutPlan plan, uint location)
    {
        byte[] data = new byte[LogicalSectorBytes];
        U32(data, 16, 3);
        U16(data, 20, 1);
        Entity(data.AsSpan(24, 32), "+NSR03");
        U32(data, 184, 2); // Write-once physical partition; free space is reported by the drive.
        U32(data, 188, plan.PartitionStartLba);
        U32(data, 192, plan.PartitionSectors);
        Entity(data.AsSpan(196, 32), ImplementationId);
        SetTag(data, 5, location, 512);
        return data;
    }

    private static byte[] LogicalVolumeDescriptor(LayoutPlan plan, uint location)
    {
        byte[] data = new byte[LogicalSectorBytes];
        U32(data, 16, 4);
        Charspec(data.AsSpan(20, 64));
        Dstring(data.AsSpan(84, 128), plan.VolumeLabel);
        U32(data, 212, LogicalSectorBytes);
        Entity(data.AsSpan(216, 32), "*OSTA UDF Compliant", domain: true);
        LongAd(data.AsSpan(248, 16), LogicalSectorBytes * 2, 0, partition: 1);
        U32(data, 264, 70);
        U32(data, 268, 2);
        Entity(data.AsSpan(272, 32), ImplementationId);
        Extent(data.AsSpan(432, 8), LogicalSectorBytes * 4, IntegrityLba);
        data[440] = 1;
        data[441] = 6;
        U16(data, 442, 1); // Volume sequence number; partition number is zero.
        data[446] = 2;
        data[447] = 64;
        Entity(data.AsSpan(450, 32), "*UDF Virtual Partition", udfRevision: true);
        U16(data, 482, 1);
        SetTag(data, 6, location, 510);
        return data;
    }

    private static byte[] IntegrityDescriptor(LayoutPlan plan)
    {
        byte[] data = new byte[LogicalSectorBytes];
        Timestamp(data.AsSpan(16, 12), plan.Timestamp);
        U32(data, 28, 0); // Sequential volumes stay open; the final VAT commits each transaction.
        U64(data, 40, checked((ulong)plan.Nodes.Count + 15));
        U32(data, 72, 2);
        U32(data, 76, 46);
        U32(data, 80, plan.CapacitySectors - plan.VolumeSectors);
        U32(data, 84, uint.MaxValue);
        U32(data, 88, plan.PartitionSectors);
        U32(data, 92, uint.MaxValue);
        Entity(data.AsSpan(96, 32), ImplementationId);
        U32(data, 128, checked((uint)plan.FileCount));
        U32(data, 132, checked((uint)plan.DirectoryCount));
        U16(data, 136, UdfRevision);
        U16(data, 138, UdfRevision);
        U16(data, 140, UdfRevision);
        SetTag(data, 9, IntegrityLba, 142);
        return data;
    }

    private static byte[] Anchor(LayoutPlan plan, uint location)
    {
        byte[] data = new byte[LogicalSectorBytes];
        Extent(data.AsSpan(16, 8), VdsSectors * LogicalSectorBytes, MainVdsLba);
        Extent(data.AsSpan(24, 8), VdsSectors * LogicalSectorBytes, plan.ReserveVdsLba);
        SetTag(data, 2, location, 512);
        return data;
    }

    private static byte[] FileSetDescriptor(LayoutPlan plan)
    {
        byte[] data = new byte[LogicalSectorBytes];
        Timestamp(data.AsSpan(16, 12), plan.Timestamp);
        U16(data, 28, 3);
        U16(data, 30, 3);
        U32(data, 32, 1);
        U32(data, 36, 1);
        Charspec(data.AsSpan(48, 64));
        Dstring(data.AsSpan(112, 128), plan.VolumeLabel);
        Charspec(data.AsSpan(240, 64));
        Dstring(data.AsSpan(304, 32), FitDstring(plan.VolumeLabel, 32));
        LongAd(data.AsSpan(400, 16), LogicalSectorBytes, plan.Nodes[0].EntryLba - plan.MetadataStartLba, partition: 1);
        Entity(data.AsSpan(416, 32), "*OSTA UDF Compliant", domain: true);
        SetTag(data, 256, 0, 512);
        return data;
    }

    private static byte[] FileEntry(LayoutPlan plan, Node node)
    {
        byte[] data = new byte[LogicalSectorBytes];
        U16(data, 20, 4); // ICB strategy type 4, short allocation descriptors.
        U16(data, 24, 1);
        data[27] = node.IsDirectory ? (byte)4 : (byte)5;
        // Parent ICB location is unused by strategy 4 (UDF 2.50 errata 7.23).
        U16(data, 34, node.IsDirectory ? (ushort)0 : (ushort)1);
        U32(data, 36, uint.MaxValue);
        U32(data, 40, uint.MaxValue);
        U32(data, 44, node.IsDirectory ? 0x14a5U : 0x1084U); // Read (and directory traverse).
        U16(data, 48, checked((ushort)(node.IsDirectory
            ? 1 + node.Children.Count(child => child.IsDirectory) : 1)));
        U64(data, 56, checked((ulong)node.DataLength));
        U64(data, 64, SectorCount(node.DataLength));
        Timestamp(data.AsSpan(72, 12), node.Timestamp);
        Timestamp(data.AsSpan(84, 12), node.Timestamp);
        Timestamp(data.AsSpan(96, 12), node.Timestamp);
        U32(data, 108, 1);
        Entity(data.AsSpan(128, 32), ImplementationId);
        U64(data, 160, node.UniqueId);
        int offset = 176;
        long remaining = node.DataLength;
        uint lbn = node.DataLba - (node.IsDirectory ? plan.MetadataStartLba : plan.PartitionStartLba);
        while (remaining > 0)
        {
            uint length = (uint)Math.Min(remaining, node.IsDirectory ? LogicalSectorBytes : MaximumExtentBytes);
            U32(data, offset, length);
            U32(data, offset + 4, lbn);
            remaining -= length;
            lbn = checked(lbn + SectorCount(length));
            offset += node.IsDirectory ? 8 : 16; // long_ad partition zero is already zero-filled.
        }
        U32(data, 172, checked((uint)(offset - 176)));
        SetTag(data, 261, node.EntryLba - plan.MetadataStartLba, offset);
        return data;
    }

    private static byte[] VirtualAllocationTable(LayoutPlan plan)
    {
        byte[] data = new byte[checked(152 + 4 * (int)plan.MetadataSectors)];
        U16(data, 0, 152);
        Dstring(data.AsSpan(4, 128), plan.VolumeLabel);
        U32(data, 132, plan.PreviousVatIcb);
        U32(data, 136, checked((uint)plan.FileCount));
        U32(data, 140, checked((uint)plan.DirectoryCount));
        U16(data, 144, UdfRevision);
        U16(data, 146, UdfRevision);
        U16(data, 148, UdfRevision);
        for (uint block = 0; block < plan.MetadataSectors; block++)
            U32(data, checked(152 + (int)block * 4), plan.MetadataStartLba - plan.PartitionStartLba + block);
        return data;
    }

    private static byte[] VatFileEntry(LayoutPlan plan)
    {
        byte[] data = new byte[LogicalSectorBytes];
        U16(data, 20, 4);
        U16(data, 24, 1);
        data[27] = 248;
        U32(data, 36, uint.MaxValue);
        U32(data, 40, uint.MaxValue);
        U16(data, 48, 1);
        U64(data, 56, checked(152UL + 4UL * plan.MetadataSectors));
        U64(data, 64, plan.VatSectors);
        Timestamp(data.AsSpan(72, 12), plan.Timestamp);
        Timestamp(data.AsSpan(84, 12), plan.Timestamp);
        Timestamp(data.AsSpan(96, 12), plan.Timestamp);
        U32(data, 108, 1);
        Entity(data.AsSpan(128, 32), ImplementationId);
        // For VAT volumes this is the last allocated UniqueID (UDF 3.2.1.1).
        U64(data, 160, checked(Math.Max(plan.Nodes.Max(node => node.UniqueId), plan.PreviousLastUniqueId) + 1));
        U32(data, 172, 8);
        U32(data, 176, checked(152 + 4 * plan.MetadataSectors));
        U32(data, 180, plan.VatDataLba - plan.PartitionStartLba);
        SetTag(data, 261, plan.VolumeSectors - 1 - plan.PartitionStartLba, 184);
        return data;
    }

    private static byte[] FileIdentifier(LayoutPlan plan, Node target, byte[] identifier, bool parent, uint location,
        long directoryOffset)
    {
        byte[] data = new byte[FileIdentifierLength(identifier.Length, directoryOffset)];
        int implementationBytes = data.Length - ((38 + identifier.Length + 3) / 4 * 4);
        U16(data, 16, 1);
        data[18] = parent ? (byte)10 : target.IsDirectory ? (byte)2 : (byte)0;
        data[19] = checked((byte)identifier.Length);
        LongAd(data.AsSpan(20, 16), LogicalSectorBytes, target.EntryLba - plan.MetadataStartLba, partition: 1);
        // long_ad ImplementationUse: 2 flag bytes followed by the UDF unique ID.
        U32(data, 32, checked((uint)target.UniqueId));
        U16(data, 36, checked((ushort)implementationBytes));
        if (implementationBytes > 0)
            Entity(data.AsSpan(38, 32), ImplementationId);
        identifier.CopyTo(data, 38 + implementationBytes);
        SetTag(data, 257, location, data.Length);
        return data;
    }

    private static int FileIdentifierLength(int identifierBytes, long directoryOffset)
    {
        int length = (38 + identifierBytes + 3) / 4 * 4;
        long nextOffset = (directoryOffset + length) % LogicalSectorBytes;
        // UDF 2.50 section 2.3.4.4: on write-once media the next FID's 16-byte
        // descriptor tag cannot straddle a block. A nonempty IU field is at least32 bytes.
        return nextOffset > LogicalSectorBytes - 16 ? length + 32 : length;
    }
    private static uint SectorCount(long length) => checked((uint)((length + LogicalSectorBytes - 1) / LogicalSectorBytes));

    private static byte[] EncodeIdentifier(string text)
    {
        if (text.Any(IsUnsupportedCharacter))
            throw new ArgumentException(L.F($"UDF 2.50 filenames must use CS0 Unicode BMP characters without byte-order marks: {text}"));
        bool narrow = text.All(character => character <= byte.MaxValue);
        int length = checked(1 + text.Length * (narrow ? 1 : 2));
        if (length > byte.MaxValue)
            throw new ArgumentException(L.F($"Filename exceeds the UDF CS0 identifier limit (254 single-byte or 127 two-byte characters): {text}"));
        byte[] bytes = new byte[length];
        bytes[0] = narrow ? (byte)8 : (byte)16;
        if (narrow)
        {
            for (int i = 0; i < text.Length; i++)
                bytes[i + 1] = (byte)text[i];
        }
        else
            Encoding.BigEndianUnicode.GetBytes(text, bytes.AsSpan(1));
        return bytes;
    }

    private static string FitDstring(string text, int fieldBytes)
    {
        if (text.Any(IsUnsupportedCharacter))
            throw new ArgumentException(L.T("UDF volume labels must use CS0 Unicode BMP characters without byte-order marks."));
        int characters = text.All(character => character <= byte.MaxValue)
            ? fieldBytes - 2 : (fieldBytes - 2) / 2;
        return text.Length <= characters ? text : text[..characters];
    }

    private static void Dstring(Span<byte> field, string text)
    {
        if (text.Length == 0)
            return;
        byte[] bytes = EncodeIdentifier(text);
        if (bytes.Length >= field.Length)
            throw new ArgumentException(L.T("UDF dstring does not fit its descriptor field."));
        bytes.CopyTo(field);
        field[^1] = checked((byte)bytes.Length);
    }

    private static bool IsUnsupportedCharacter(char character)
        => char.IsSurrogate(character) || character is '\0' or '\uFEFF' or '\uFFFE';

    private static void Charspec(Span<byte> field)
        => Encoding.ASCII.GetBytes("OSTA Compressed Unicode", field[1..]);

    private static void Entity(Span<byte> field, string id, bool domain = false, bool udfRevision = false)
    {
        Encoding.ASCII.GetBytes(id, field.Slice(1, 23));
        if (domain || udfRevision)
            BinaryPrimitives.WriteUInt16LittleEndian(field.Slice(24, 2), UdfRevision);
    }

    private static void Timestamp(Span<byte> field, DateTimeOffset timestamp)
    {
        DateTime utc = timestamp.UtcDateTime;
        BinaryPrimitives.WriteUInt16LittleEndian(field, 0x1000); // Local time with UTC offset zero.
        BinaryPrimitives.WriteUInt16LittleEndian(field[2..], checked((ushort)utc.Year));
        field[4] = (byte)utc.Month;
        field[5] = (byte)utc.Day;
        field[6] = (byte)utc.Hour;
        field[7] = (byte)utc.Minute;
        field[8] = (byte)utc.Second;
        field[9] = (byte)(utc.Millisecond / 10);
        field[10] = (byte)((utc.Ticks / 1000) % 100);
        field[11] = (byte)((utc.Ticks / 10) % 100);
    }

    private static void Extent(Span<byte> data, uint length, uint lba)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(data, length);
        BinaryPrimitives.WriteUInt32LittleEndian(data[4..], lba);
    }

    private static void LongAd(Span<byte> data, uint length, uint lbn, ushort partition = 0)
    {
        Extent(data, length, lbn);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Slice(8, 2), partition);
    }
    private static void U16(byte[] data, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
    private static void U32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);
    private static void U64(byte[] data, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, 8), value);

    private static byte[] Descriptor(ushort id, uint location, int descriptorLength)
    {
        byte[] data = new byte[LogicalSectorBytes];
        SetTag(data, id, location, descriptorLength);
        return data;
    }

    private static void SetTag(byte[] data, ushort id, uint location, int descriptorLength)
    {
        U16(data, 0, id);
        U16(data, 2, 3); // ECMA-167 third edition.
        U16(data, 6, 1);
        U16(data, 8, Crc(data.AsSpan(16, descriptorLength - 16)));
        U16(data, 10, checked((ushort)(descriptorLength - 16)));
        U32(data, 12, location);
        int checksum = 0;
        for (int i = 0; i < 16; i++)
            if (i != 4)
                checksum += data[i];
        data[4] = unchecked((byte)checksum);
    }

    private static ushort Crc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (byte value in data)
        {
            crc ^= (ushort)(value << 8);
            for (int bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    private static void ValidateOutput(LayoutPlan plan, Stream output)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
            throw new ArgumentException(L.T("UDF output stream must be writable."), nameof(output));
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException(L.F($"Reparse points are not supported in the UDF data directory: {path}"));
    }

    private static void ValidateSources(LayoutPlan plan, CancellationToken cancellationToken)
    {
        foreach (Node node in plan.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.SourcePath.Length == 0)
                continue;
            if (node.IsDirectory)
            {
                RejectReparsePoint(node.SourcePath);
                if (!Directory.Exists(node.SourcePath))
                    throw new IOException(L.F($"Source directory changed after UDF planning: {node.SourcePath}"));
                // NTFS can return a stale last-write timestamp from parent enumeration.
                // Check the actual children instead, which also catches additions and removals.
                Dictionary<string, Node> expected = node.Children.ToDictionary(child => child.Name, StringComparer.Ordinal);
                int count = 0;
                foreach (FileSystemInfo entry in new DirectoryInfo(node.SourcePath).EnumerateFileSystemInfos())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0
                        || !expected.TryGetValue(entry.Name, out Node? child)
                        || ((entry.Attributes & FileAttributes.Directory) != 0) != child.IsDirectory)
                        throw new IOException(L.F($"Source directory changed after UDF planning: {node.SourcePath}"));
                    count++;
                }
                if (count != expected.Count)
                    throw new IOException(L.F($"Source directory changed after UDF planning: {node.SourcePath}"));
            }
            else
                ValidateFile(node);
        }
    }

    private static void ValidateFile(Node file)
    {
        for (Node? directory = file.Parent; directory is not null; directory = directory.Parent)
            RejectReparsePoint(directory.SourcePath);
        FileInfo current = new(file.SourcePath);
        if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0
            || current.Length != file.DataLength || current.LastWriteTimeUtc != file.Timestamp.UtcDateTime)
            throw new IOException(L.F($"Source file changed after UDF planning: {file.SourcePath}"));
    }

    private sealed class SequentialWriter(Stream output, uint startLba, CancellationToken cancellationToken)
    {
        private readonly byte[] _buffer = new byte[128 * 1024];
        private readonly byte[] _zeroBuffer = new byte[128 * 1024];
        private long _position = checked((long)startLba * LogicalSectorBytes);

        internal void WriteAtLba(uint lba, ReadOnlySpan<byte> data)
        {
            PadToLba(lba);
            Write(data);
        }

        internal void Write(ReadOnlySpan<byte> data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(data);
            _position = checked(_position + data.Length);
        }

        internal void WriteFile(Node file)
        {
            PadToLba(file.DataLba);
            ValidateFile(file);
            using FileStream input = new(file.SourcePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, _buffer.Length, FileOptions.SequentialScan);
            if (input.Length != file.DataLength)
                throw new IOException(L.F($"Source file size changed while streaming UDF: {file.SourcePath}"));
            long remaining = file.DataLength;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = input.Read(_buffer, 0, (int)Math.Min(remaining, _buffer.Length));
                if (count == 0)
                    throw new EndOfStreamException(L.F($"Source file ended early while streaming UDF: {file.SourcePath}"));
                Write(_buffer.AsSpan(0, count));
                remaining -= count;
            }
            if (input.Length != file.DataLength)
                throw new IOException(L.F($"Source file size changed while streaming UDF: {file.SourcePath}"));
            ValidateFile(file);
        }

        internal void PadToLba(uint lba)
        {
            long target = checked((long)lba * LogicalSectorBytes);
            if (target < _position)
                throw new InvalidOperationException(L.F($"Sequential UDF layout moved backwards from {_position} to {target}."));
            while (_position < target)
                Write(_zeroBuffer.AsSpan(0, (int)Math.Min(target - _position, _zeroBuffer.Length)));
        }
    }

    internal sealed class Node(string sourcePath, string name, bool isDirectory, Node? parent)
    {
        internal string SourcePath { get; } = sourcePath;
        internal string Name { get; } = name;
        internal bool IsDirectory { get; } = isDirectory;
        internal Node? Parent { get; } = parent;
        internal List<Node> Children { get; } = [];
        internal byte[] Identifier { get; init; } = [];
        internal DateTimeOffset Timestamp { get; init; }
        internal ulong UniqueId { get; set; }
        internal uint EntryLba { get; set; }
        internal uint DataLba { get; set; }
        internal long DataLength { get; set; }
    }
}
