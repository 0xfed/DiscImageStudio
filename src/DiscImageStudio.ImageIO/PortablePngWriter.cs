using System.Buffers.Binary;
using System.IO.Compression;

namespace DiscImageStudio.ImageIO;

/// <summary>Writes the two PNG formats exposed by RasterCodec without another package.</summary>
internal static class PortablePngWriter
{
    private static readonly uint[] CrcTable = CreateCrcTable();

    public static void Write(Stream output, int width, int height, ReadOnlySpan<byte> pixels, bool grayscale)
    {
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = grayscale ? (byte)0 : (byte)6;
        WriteChunk(output, "IHDR"u8, header);

        using IdatStream chunks = new(output);
        using (ZLibStream compressor = new(chunks, CompressionLevel.Optimal, leaveOpen: true))
        {
            ReadOnlySpan<byte> filter = [0];
            Span<byte> rgba = stackalloc byte[4096];
            int stride = checked(width * (grayscale ? 1 : 4));
            for (int y = 0; y < height; y++)
            {
                compressor.Write(filter);
                ReadOnlySpan<byte> row = pixels.Slice(y * stride, stride);
                if (grayscale)
                {
                    compressor.Write(row);
                    continue;
                }
                // Convert only a small part of the current row. The caller's
                // BGRA buffer stays unchanged, even for large previews.
                while (!row.IsEmpty)
                {
                    int count = Math.Min(rgba.Length, row.Length);
                    for (int i = 0; i < count; i += 4)
                    {
                        rgba[i] = row[i + 2];
                        rgba[i + 1] = row[i + 1];
                        rgba[i + 2] = row[i];
                        rgba[i + 3] = row[i + 3];
                    }
                    compressor.Write(rgba[..count]);
                    row = row[count..];
                }
            }
        }
        chunks.Complete();
        WriteChunk(output, "IEND"u8, []);
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        type.CopyTo(header[4..]);
        output.Write(header);
        output.Write(payload);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ~UpdateCrc(UpdateCrc(uint.MaxValue, type), payload));
        output.Write(crc);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
            crc = CrcTable[(byte)(crc ^ value)] ^ (crc >> 8);
        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
            table[i] = crc;
        }
        return table;
    }

    // Compression writes a single zlib stream through consecutive bounded
    // chunks. Neither the whole compressed image nor a full row is buffered.
    private sealed class IdatStream(Stream output) : Stream
    {
        private readonly byte[] buffer = new byte[32 * 1024];
        private int count;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] source, int offset, int length) => Write(source.AsSpan(offset, length));

        public override void Write(ReadOnlySpan<byte> source)
        {
            while (!source.IsEmpty)
            {
                int length = Math.Min(source.Length, buffer.Length - count);
                source[..length].CopyTo(buffer.AsSpan(count));
                count += length;
                source = source[length..];
                if (count == buffer.Length)
                    Complete();
            }
        }

        public void Complete()
        {
            if (count == 0)
                return;
            WriteChunk(output, "IDAT"u8, buffer.AsSpan(0, count));
            count = 0;
        }

        public override void Flush()
        {
            Complete();
            output.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
