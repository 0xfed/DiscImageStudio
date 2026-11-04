using System.Buffers.Binary;
using System.IO.Compression;
using DiscImageStudio.Localization;

namespace DiscImageStudio.ImageIO;

public static partial class RasterCodec
{
    private const uint PngHeader = 0x49484452;
    private const uint PngData = 0x49444154;
    private const uint PngEnd = 0x49454e44;
    private const uint PngPalette = 0x504c5445;
    private const uint PngCgbi = 0x43674249;

    // Header pixel limits alone do not bound stb's expandable PNG inflater.
    // Validate its complete scanline stream with a fixed-size buffer first.
    // This also protects the header-only ValidateSource entry point.
    private static void ValidatePngInflation(Stream stream, long maxPixels)
    {
        long originalPosition = stream.Position;
        try
        {
            Span<byte> signature = stackalloc byte[8];
            ReadOnlySpan<byte> pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
            if (stream.Length - stream.Position < signature.Length)
                return;
            stream.ReadExactly(signature);
            if (!signature.SequenceEqual(pngSignature))
                return;

            PngChunk chunk = ReadPngChunk(stream);
            bool rawDeflate = chunk.Type == PngCgbi;
            if (rawDeflate)
            {
                SkipPngChunk(stream, chunk);
                chunk = ReadPngChunk(stream);
            }
            if (chunk.Type != PngHeader || chunk.Length != 13)
                throw InvalidPng();

            Span<byte> header = stackalloc byte[13];
            stream.ReadExactly(header);
            uint width = BinaryPrimitives.ReadUInt32BigEndian(header);
            uint height = BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
            if (width > int.MaxValue || height > int.MaxValue)
                throw InvalidPng();
            CheckDimensions((int)width, (int)height, maxPixels);
            int depth = header[8];
            int color = header[9];
            int components = color switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
            bool validDepth = color switch
            {
                0 => depth is 1 or 2 or 4 or 8 or 16,
                3 => depth is 1 or 2 or 4 or 8,
                2 or 4 or 6 => depth is 8 or 16,
                _ => false,
            };
            if (components == 0 || !validDepth || header[10] != 0 || header[11] != 0 || header[12] > 1)
                throw InvalidPng();
            stream.Seek(4, SeekOrigin.Current); // IHDR CRC; lengths were checked above.

            long expected = PngScanlineBytes((int)width, (int)height, components * depth, header[12] == 1);
            // stb's inflater and PNG reconstruction use signed 32-bit lengths.
            if (expected > int.MaxValue)
                throw InvalidPng();

            while (true)
            {
                chunk = ReadPngChunk(stream);
                if (chunk.Type == PngData)
                    break;
                if (chunk.Type is PngHeader or PngEnd or PngCgbi
                    || IsCriticalPngChunk(chunk.Type) && chunk.Type != PngPalette)
                    throw InvalidPng();
                SkipPngChunk(stream, chunk);
            }

            using PngDataStream data = new(stream, chunk);
            using (Stream inflater = rawDeflate
                ? new DeflateStream(data, CompressionMode.Decompress, leaveOpen: true)
                : new ZLibStream(data, CompressionMode.Decompress, leaveOpen: true))
            {
                Span<byte> buffer = stackalloc byte[8192];
                long remaining = expected;
                while (remaining > 0)
                {
                    int read = inflater.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
                    if (read == 0)
                        throw InvalidPng();
                    remaining -= read;
                }
                // A single extra output byte proves the allocation budget would
                // be exceeded. Do not drain the rest of an oversized stream.
                if (inflater.ReadByte() != -1)
                    throw InvalidPng();
            }
            data.ValidateEnd();
        }
        catch (EndOfStreamException)
        {
            throw InvalidPng();
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static long PngScanlineBytes(int width, int height, int bitsPerPixel, bool interlaced)
    {
        if (!interlaced)
            return (((long)width * bitsPerPixel + 7) / 8 + 1) * height;

        ReadOnlySpan<int> startX = [0, 4, 0, 2, 0, 1, 0];
        ReadOnlySpan<int> startY = [0, 0, 4, 0, 2, 0, 1];
        ReadOnlySpan<int> stepX = [8, 8, 4, 4, 2, 2, 1];
        ReadOnlySpan<int> stepY = [8, 8, 8, 4, 4, 2, 2];
        long total = 0;
        for (int pass = 0; pass < 7; pass++)
        {
            long passWidth = Math.Max(0, ((long)width - startX[pass] + stepX[pass] - 1) / stepX[pass]);
            long passHeight = Math.Max(0, ((long)height - startY[pass] + stepY[pass] - 1) / stepY[pass]);
            if (passWidth != 0 && passHeight != 0)
                total += ((passWidth * bitsPerPixel + 7) / 8 + 1) * passHeight;
        }
        return total;
    }

    private static PngChunk ReadPngChunk(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        stream.ReadExactly(header);
        uint length = BinaryPrimitives.ReadUInt32BigEndian(header);
        uint type = BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
        if (length > int.MaxValue || (long)length + 4 > stream.Length - stream.Position)
            throw InvalidPng();
        return new(type, (int)length);
    }

    private static void SkipPngChunk(Stream stream, PngChunk chunk) => stream.Seek((long)chunk.Length + 4, SeekOrigin.Current);

    private static bool IsCriticalPngChunk(uint type) => (type & 0x20000000) == 0;

    private static InvalidDataException InvalidPng() => new(L.T("Unsupported or invalid image. Use a PNG, JPEG, BMP or GIF."));

    private readonly record struct PngChunk(uint Type, int Length);

    // Presents consecutive IDAT payloads as one stream without collecting chunk
    // offsets or copying attacker-sized compressed/decompressed buffers.
    private sealed class PngDataStream(Stream source, PngChunk first) : Stream
    {
        private int remaining = first.Length;
        private PngChunk next;
        private bool ended;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
                return 0;
            while (!ended && remaining == 0)
                Advance();
            if (ended)
                return 0;
            int read = source.Read(buffer[..Math.Min(buffer.Length, remaining)]);
            if (read == 0)
                throw InvalidPng();
            remaining -= read;
            return read;
        }

        private void Advance()
        {
            source.Seek(4, SeekOrigin.Current); // CRC of the completed IDAT.
            next = ReadPngChunk(source);
            ended = next.Type != PngData;
            if (!ended)
                remaining = next.Length;
        }

        public void ValidateEnd()
        {
            // An inflater may stop before consuming all compressed input. Skip
            // the rest without inflating it and validate the remaining grammar.
            while (!ended)
            {
                source.Seek(remaining, SeekOrigin.Current);
                remaining = 0;
                Advance();
            }
            while (next.Type != PngEnd)
            {
                if (next.Type is PngHeader or PngData or PngCgbi || IsCriticalPngChunk(next.Type))
                    throw InvalidPng();
                SkipPngChunk(source, next);
                next = ReadPngChunk(source);
            }
            if (next.Length != 0)
                throw InvalidPng();
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
