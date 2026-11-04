using System.Buffers.Binary;
using System.IO.Compression;
using DiscImageStudio.ImageIO;

string directory = Path.Combine(Path.GetTempPath(), "DiscImageStudio-image-limits-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
int tests = 0;
try
{
    Check("PNG writer round trip", () =>
    {
        string path = Path.Combine(directory, "roundtrip.png");
        byte[] pixels = [0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 85, 85, 85, 255];
        RasterCodec.SavePng(path, 2, 2, pixels, grayscale: false);
        Require(RasterCodec.LoadBgra32(path).Pixels.SequenceEqual(pixels), "changed output pixels");
    });

    Check("palette PNG with split IDAT", () =>
    {
        string path = Png("palette.png", 2, 1, 1, 3, [0, 0x40], palette: [255, 0, 0, 0, 255, 0], split: true);
        Require(RasterCodec.LoadBgra32(path).Pixels.SequenceEqual(new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }), "changed palette pixels");
    });

    Check("16-bit RGB PNG", () =>
    {
        string path = Png("rgb16.png", 1, 1, 16, 2, [0, 0xff, 0xff, 0, 0, 0x80, 0x80]);
        Require(RasterCodec.LoadBgra32(path).Pixels.SequenceEqual(new byte[] { 128, 0, 255, 255 }), "changed 16-bit pixels");
    });

    Check("grayscale PNG", () =>
    {
        string path = Png("gray.png", 3, 1, 8, 0, [0, 0, 127, 255]);
        Require(RasterCodec.LoadBgra32(path).Pixels.SequenceEqual(new byte[] { 0, 0, 0, 255, 127, 127, 127, 255, 255, 255, 255, 255 }), "changed grayscale pixels");
    });

    Check("Adam7 odd-size PNG", () =>
    {
        // Independently enumerated 3x3 Adam7 passes (empty passes 2 and 3 omitted).
        byte[] scanlines = [
            0, 0, 0, 80, 255,
            0, 100, 0, 80, 255,
            0, 0, 100, 80, 255, 100, 100, 80, 255,
            0, 50, 0, 80, 255, 0, 50, 100, 80, 255,
            0, 0, 50, 80, 255, 50, 50, 80, 255, 100, 50, 80, 255];
        string path = Png("adam7.png", 3, 3, 8, 6, scanlines, interlaced: true);
        BgraRaster image = RasterCodec.LoadBgra32(path);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Require(image.Pixels.AsSpan((y * 3 + x) * 4, 4).SequenceEqual(new byte[] { 80, (byte)(y * 50), (byte)(x * 50), 255 }), "changed Adam7 pixel position");
    });

    Check("small Adam7 PNG with empty passes", () =>
    {
        string path = Png("adam7-small.png", 1, 1, 8, 6, [0, 9, 8, 7, 255], interlaced: true);
        Require(RasterCodec.LoadBgra32(path).Pixels.SequenceEqual(new byte[] { 7, 8, 9, 255 }), "changed one-pixel Adam7 image");
    });

    Check("CgBI raw-deflate PNG remains supported", () =>
    {
        string path = Png("cgbi.png", 1, 1, 8, 6, [0, 9, 8, 7, 255], iphone: true);
        BgraRaster image = RasterCodec.LoadBgra32(path);
        Require(image.Width == 1 && image.Height == 1 && image.Pixels.Length == 4, "CgBI failed to decode");
    });

    Check("one-pixel inflation budget rejects oversized output", () =>
    {
        // Bounded regression: only 1 MiB inflated; the guard must stop after 6 output bytes.
        string path = Png("oversized.png", 1, 1, 8, 6, new byte[1024 * 1024]);
        Require(new FileInfo(path).Length < 65536, "fixture exceeds encoded budget");
        Reject<InvalidDataException>(() => RasterCodec.LoadBgra32(path, 65536, 1));
        Reject<InvalidDataException>(() => RasterCodec.ValidateSource(path, 65536, 1));
    });

    Check("one extra scanline byte is rejected", () =>
    {
        string path = Png("one-extra.png", 1, 1, 8, 6, new byte[6]);
        Reject<InvalidDataException>(() => RasterCodec.LoadBgra32(path));
    });

    Check("short scanline is rejected", () =>
    {
        string path = Png("short.png", 1, 1, 8, 6, new byte[4]);
        Reject<InvalidDataException>(() => RasterCodec.LoadBgra32(path));
    });

    Check("IDAT chunks must remain consecutive", () =>
    {
        string path = Png("late-idat.png", 1, 1, 8, 6, [0, 0, 0, 0, 255], split: true, interrupted: true);
        Reject<InvalidDataException>(() => RasterCodec.LoadBgra32(path));
    });

    Check("malformed chunk length is rejected", () =>
    {
        string path = Png("chunk-overrun.png", 1, 1, 8, 6, [0, 0, 0, 0, 255]);
        byte[] bytes = File.ReadAllBytes(path);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(33, 4), int.MaxValue);
        File.WriteAllBytes(path, bytes);
        Reject<InvalidDataException>(() => RasterCodec.LoadBgra32(path));
    });

    Check("truncated PNG structure is rejected", () =>
    {
        string path = Png("truncated.png", 1, 1, 8, 6, [0, 0, 0, 0, 255]);
        byte[] bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..^7]);
        Reject<InvalidDataException>(() => RasterCodec.LoadBgra32(path));
    });

    Check("header cannot allocate beyond caller pixel budget", () =>
    {
        string path = Png("pixel-budget.png", 2, 1, 8, 6, new byte[9]);
        Reject<ArgumentException>(() => RasterCodec.LoadBgra32(path, 65536, 1));
    });

    Check("encoded file budget remains enforced", () =>
    {
        string path = Png("file-budget.png", 1, 1, 8, 6, [0, 0, 0, 0, 255]);
        Reject<ArgumentException>(() => RasterCodec.LoadBgra32(path, 1, 1));
    });

    Check("default pixel budget protects callers without overrides", () =>
    {
        string path = Png("default-pixel-budget.png", 8193, 8193, 8, 6, [0, 0, 0, 0, 255]);
        Reject<ArgumentException>(() => RasterCodec.LoadBgra32(path));
    });

    Check("GIF codec remains available", () =>
    {
        string path = Path.Combine(directory, "one.gif");
        File.WriteAllBytes(path, Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"));
        BgraRaster image = RasterCodec.LoadBgra32(path);
        Require(image.Width == 1 && image.Height == 1 && image.Pixels.Length == 4, "GIF failed to decode");
    });

    Check("BMP codec remains available", () =>
    {
        byte[] bitmap = new byte[58];
        bitmap[0] = (byte)'B'; bitmap[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(2), 58);
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(10), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(14), 40);
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bitmap.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bitmap.AsSpan(28), 24);
        bitmap[54] = 7; bitmap[55] = 8; bitmap[56] = 9;
        string path = Path.Combine(directory, "one.bmp");
        File.WriteAllBytes(path, bitmap);
        Require(RasterCodec.LoadBgra32(path).Pixels.SequenceEqual(new byte[] { 7, 8, 9, 255 }), "changed BMP pixels");
    });

    Check("repository image remains valid", () =>
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "DiscImageStudio.slnx")))
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        BgraRaster image = RasterCodec.LoadBgra32(Path.Combine(root, "src/DiscImageStudio.App/Assets/DiscImageStudio-256.png"));
        Require(image.Width == 256 && image.Height == 256, "repository image dimensions changed");
    });

    Console.WriteLine($"image-io: all {tests} tests passed");
}
finally
{
    Directory.Delete(directory, recursive: true);
}

void Check(string name, Action action)
{
    try { action(); tests++; }
    catch (Exception e) { throw new InvalidOperationException(name, e); }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Reject<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Expected rejection: " + typeof(T).Name);
}

string Png(string name, uint width, uint height, byte depth, byte color, byte[] scanlines,
    bool interlaced = false, byte[]? palette = null, bool split = false, bool interrupted = false, bool iphone = false)
{
    using MemoryStream compressed = new();
    using (Stream compressor = iphone
        ? new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)
        : new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        compressor.Write(scanlines);
    using MemoryStream png = new();
    png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
    byte[] header = new byte[13];
    BinaryPrimitives.WriteUInt32BigEndian(header, width);
    BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), height);
    header[8] = depth; header[9] = color; header[12] = interlaced ? (byte)1 : (byte)0;
    if (iphone) Chunk(png, "CgBI"u8, []);
    Chunk(png, "IHDR"u8, header);
    if (palette is not null) Chunk(png, "PLTE"u8, palette);
    byte[] data = compressed.ToArray();
    if (split)
    {
        Chunk(png, "IDAT"u8, data.AsSpan(0, 2));
        if (interrupted) Chunk(png, "tEXt"u8, "key\0value"u8);
        Chunk(png, "IDAT"u8, data.AsSpan(2));
    }
    else Chunk(png, "IDAT"u8, data);
    Chunk(png, "IEND"u8, []);
    string path = Path.Combine(directory, name);
    File.WriteAllBytes(path, png.ToArray());
    return path;
}

static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
{
    Span<byte> number = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(number, (uint)payload.Length);
    stream.Write(number); stream.Write(type); stream.Write(payload);
    uint crc = 0xffffffff;
    foreach (byte b in type) crc = Add(crc, b);
    foreach (byte b in payload) crc = Add(crc, b);
    BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
    stream.Write(number);
    static uint Add(uint crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        return crc;
    }
}
