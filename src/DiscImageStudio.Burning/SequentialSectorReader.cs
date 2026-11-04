using DiscImageStudio.Localization;

namespace DiscImageStudio.Burning;

internal static class SequentialSectorReader
{
    internal static byte[] Read(Func<uint, byte[]> readSector, uint lba, ushort count)
    {
        if (count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(count));
        byte[] data = new byte[count * 2048];
        for (uint index = 0; index < count; index++)
        {
            byte[] sector = readSector(checked(lba + index));
            if (sector.Length != 2048) throw new InvalidDataException(L.T("The disc is not a committed Disc Image Studio UDF 2.50 VAT volume."));
            sector.CopyTo(data, checked((int)index * 2048));
        }
        return data;
    }
}
