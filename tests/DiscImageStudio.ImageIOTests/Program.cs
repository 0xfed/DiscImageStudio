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

    Check("grayscale writer emits valid Gray8 scanlines and chunk CRCs", () =>
    {
        string path = Path.Combine(directory, "written-gray.png");
        byte[] pixels = [0, 85, 170, 255, 0, 127];
        RasterCodec.SavePng(path, 3, 2, pixels, grayscale: true);
        byte[] scanlines = ReadWrittenPng(path, 3, 2, grayscale: true, out _);
        Require(scanlines.SequenceEqual(new byte[] { 0, 0, 85, 170, 0, 255, 0, 127 }), "changed Gray8 scanlines");
        BgraRaster image = RasterCodec.LoadBgra32(path);
        for (int i = 0; i < pixels.Length; i++)
            Require(image.Pixels.AsSpan(i * 4, 4).SequenceEqual(new byte[] { pixels[i], pixels[i], pixels[i], 255 }), "changed grayscale round trip");
    });

    Check("RGBA writer preserves alpha and never changes the BGRA input", () =>
    {
        string path = Path.Combine(directory, "written-rgba.png");
        byte[] pixels = [3, 2, 1, 0, 6, 5, 4, 1, 9, 8, 7, 128, 12, 11, 10, 255];
        byte[] original = (byte[])pixels.Clone();
        RasterCodec.SavePng(path, 2, 2, pixels, grayscale: false);
        byte[] scanlines = ReadWrittenPng(path, 2, 2, grayscale: false, out _);
        Require(scanlines.SequenceEqual(new byte[] { 0, 1, 2, 3, 0, 4, 5, 6, 1, 0, 7, 8, 9, 128, 10, 11, 12, 255 }), "changed RGBA scanlines");
        Require(pixels.SequenceEqual(original), "writer mutated the BGRA input");
        Require(RasterCodec.LoadBgra32(path).Pixels.SequenceEqual(original), "changed alpha round trip");
    });

    Check("large grayscale and RGBA writes use consecutive bounded IDAT chunks", () =>
    {
        foreach ((int width, int height, bool grayscale) in new[] { (65537, 1, true), (1301, 37, false) })
        {
            byte[] pixels = new byte[width * height * (grayscale ? 1 : 4)];
            new Random(8675309).NextBytes(pixels);
            string path = Path.Combine(directory, grayscale ? "many-gray-idats.png" : "many-rgba-idats.png");
            RasterCodec.SavePng(path, width, height, pixels, grayscale);
            byte[] scanlines = ReadWrittenPng(path, width, height, grayscale, out int[] lengths);
            Require(lengths.Length >= 2, "fixture did not exercise multiple IDAT chunks");
            Require(lengths.All(length => length is > 0 and <= 32 * 1024), "IDAT buffer exceeded its bound or emitted an empty chunk");
            int stride = width * (grayscale ? 1 : 4);
            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<byte> row = scanlines.AsSpan(y * (stride + 1) + 1, stride);
                if (grayscale)
                    Require(row.SequenceEqual(pixels.AsSpan(y * stride, stride)), "changed wide grayscale row");
                else
                    for (int i = 0; i < row.Length; i += 4)
                    {
                        int source = y * stride + i;
                        Require(row.Slice(i, 4).SequenceEqual(new byte[] { pixels[source + 2], pixels[source + 1], pixels[source], pixels[source + 3] }), "changed converted row at a buffer boundary");
                    }
            }
            BgraRaster decoded = RasterCodec.LoadBgra32(path);
            if (!grayscale)
                Require(decoded.Pixels.SequenceEqual(pixels), "changed multi-IDAT RGBA round trip");
            else
                for (int i = 0; i < pixels.Length; i++)
                    Require(decoded.Pixels.AsSpan(i * 4, 4).SequenceEqual(new byte[] { pixels[i], pixels[i], pixels[i], 255 }), "changed multi-IDAT grayscale round trip");
        }
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

static byte[] ReadWrittenPng(string path, int width, int height, bool grayscale, out int[] lengths)
{
    // An independent framing/bitwise-CRC reader checks writer output without
    // relying only on the production decoder accepting its own files.
    byte[] png = File.ReadAllBytes(path);
    ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
    Require(png.AsSpan(0, 8).SequenceEqual(signature), "invalid PNG signature");
    using MemoryStream compressed = new();
    List<int> chunks = [];
    bool header = false, end = false;
    int position = 8;
    while (position < png.Length)
    {
        Require(png.Length - position >= 12, "truncated chunk header");
        int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position, 4)));
        Require(length <= png.Length - position - 12, "chunk length exceeded file size");
        ReadOnlySpan<byte> type = png.AsSpan(position + 4, 4);
        ReadOnlySpan<byte> payload = png.AsSpan(position + 8, length);
        uint crc = uint.MaxValue;
        foreach (byte value in type) crc = Add(crc, value);
        foreach (byte value in payload) crc = Add(crc, value);
        Require(~crc == BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + 8 + length, 4)), "invalid chunk CRC");
        if (type.SequenceEqual("IHDR"u8))
        {
            Require(position == 8 && !header && length == 13, "invalid IHDR placement or length");
            Require(BinaryPrimitives.ReadInt32BigEndian(payload) == width && BinaryPrimitives.ReadInt32BigEndian(payload[4..]) == height, "changed image dimensions");
            Require(payload[8] == 8 && payload[9] == (grayscale ? 0 : 6) && payload[10] == 0 && payload[11] == 0 && payload[12] == 0, "unexpected color/compression/filter/interlace format");
            header = true;
        }
        else if (type.SequenceEqual("IDAT"u8))
        {
            Require(header && !end, "invalid IDAT placement");
            chunks.Add(length);
            compressed.Write(payload);
        }
        else
        {
            Require(type.SequenceEqual("IEND"u8) && length == 0 && header && chunks.Count != 0, "unexpected PNG chunk");
            end = true;
        }
        position += length + 12;
        if (end) break;
    }
    Require(end && position == png.Length, "missing IEND or trailing data");
    compressed.Position = 0;
    using ZLibStream inflater = new(compressed, CompressionMode.Decompress);
    using MemoryStream raw = new();
    inflater.CopyTo(raw);
    byte[] result = raw.ToArray();
    int stride = width * (grayscale ? 1 : 4);
    Require(result.Length == (stride + 1) * height, "unexpected decompressed scanline size");
    for (int y = 0; y < height; y++)
        Require(result[y * (stride + 1)] == 0, "unexpected scanline filter");
    lengths = chunks.ToArray();
    return result;

    static uint Add(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        return crc;
    }
}
