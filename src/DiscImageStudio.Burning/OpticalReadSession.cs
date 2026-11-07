using System.Buffers.Binary;
using System.Diagnostics;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Burning;

public sealed record OpticalReadMedia(OpticalBurnMediaKind MediaKind, long RecordedSectors, string DeviceName);
public readonly record struct OpticalReadTiming(long StartTicks, long EndTicks, bool Succeeded, int SenseKey);

/// <summary>Valid only during ReadDiscAsync's callback. No recording or arbitrary command API is exposed.</summary>
public interface IOpticalReadSession
{
    OpticalReadMedia Media { get; }
    long TimestampFrequency { get; }
    int MaximumReadSectors { get; }
    string? LastReadDiagnostic { get; }
    OpticalReadTiming Read(uint lba, int sectors, bool forceUnitAccess);
    bool FlushReadCache(uint lba);
}

internal sealed class OpticalReadSession : IOpticalReadSession, IDisposable
{
    private readonly IMmcCommandTransport _transport;
    private readonly CancellationToken _token;
    private byte _readOpcode = 0xa8;
    private bool _disposed;
    public OpticalReadMedia Media { get; }
    public long TimestampFrequency => Stopwatch.Frequency;
    public int MaximumReadSectors { get; }
    public string? LastReadDiagnostic { get; private set; }
    internal int CurrentProfile { get; }
    internal byte ReadOpcode => _readOpcode;
    internal MmcCommandResult? SpeedRequestResult { get; }

    internal OpticalReadSession(IMmcCommandTransport transport, string name, ushort readSpeedKbps, CancellationToken token)
    {
        _transport = transport; _token = token;
        token.ThrowIfCancellationRequested();
        Query(new byte[6], 0);
        byte[] configuration = new byte[10]; configuration[0] = 0x46; configuration[8] = 8;
        byte[] profile = Query(configuration, 8);
        if (profile.Length < 8) throw new IOException(L.T("Timing.InvalidMedia"));
        int kind = BinaryPrimitives.ReadUInt16BigEndian(profile.AsSpan(6));
        CurrentProfile = kind;
        OpticalBurnMediaKind media = kind switch
        {
            >= 0x08 and <= 0x0a => OpticalBurnMediaKind.CdData,
            0x10 or 0x11 or 0x12 or 0x13 or 0x14 or 0x1a or 0x1b => OpticalBurnMediaKind.DvdData,
            _ => throw new NotSupportedException(L.T("Timing.UnsupportedMedia")),
        };
        byte[] capacity = new byte[10]; capacity[0] = 0x25;
        byte[] response = Query(capacity, 8);
        if (response.Length != 8 || BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(4)) != 2048)
            throw new IOException(L.T("Timing.InvalidMedia"));
        long sectors = BinaryPrimitives.ReadUInt32BigEndian(response) + 1L;
        if (sectors is < 8192 or > 3_000_000 || (media == OpticalBurnMediaKind.CdData && sectors > 450_000))
            throw new IOException(L.T("Timing.InvalidMedia"));
        Media = new(media, sectors, name);
        MaximumReadSectors = (int)Math.Min(32, transport.GetWriteTransferLimits().MaximumPageAlignedBytes / 2048);
        if (MaximumReadSectors < 1) throw new IOException(L.T("Timing.InvalidMedia"));
        if (readSpeedKbps != 0)
        {
            byte[] speed = new byte[12]; speed[0] = 0xbb;
            BinaryPrimitives.WriteUInt16BigEndian(speed.AsSpan(2), readSpeedKbps);
            speed[4] = speed[5] = 0xff;
            // Some firmware ignores this request. Measured rotation, not this setting,
            // determines whether the timing geometry is usable.
            SpeedRequestResult = transport.Execute(speed, 0, 30);
        }
        var test = Read(0, 1, false);
        if (!test.Succeeded && test.SenseKey == 5)
        {
            _readOpcode = 0x28;
            _ = Read(0, 1, false);
        }
    }

    public OpticalReadTiming Read(uint lba, int sectors, bool forceUnitAccess)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _token.ThrowIfCancellationRequested();
        if (sectors is < 1 || sectors > MaximumReadSectors || (long)lba + sectors > Media.RecordedSectors)
            throw new ArgumentOutOfRangeException(nameof(lba));
        byte[] cdb = new byte[_readOpcode == 0xa8 ? 12 : 10];
        cdb[0] = _readOpcode; cdb[1] = forceUnitAccess ? (byte)8 : (byte)0;
        BinaryPrimitives.WriteUInt32BigEndian(cdb.AsSpan(2), lba);
        if (_readOpcode == 0xa8) BinaryPrimitives.WriteUInt32BigEndian(cdb.AsSpan(6), (uint)sectors);
        else BinaryPrimitives.WriteUInt16BigEndian(cdb.AsSpan(7), (ushort)sectors);
        var read = _transport.ReadTiming(cdb, sectors * 2048, 30);
        bool succeeded = read.Result.HResult >= 0 && read.Result.HResult != BluRayMmcCommands.CommandHasSenseData
            && read.BytesFetched == sectors * 2048;
        int senseKey = 0;
        if (!succeeded)
        {
            var error = new MmcCommandException(_readOpcode, read.Result);
            senseKey = error.SenseKey;
            LastReadDiagnostic = error.Message;
        }
        _token.ThrowIfCancellationRequested();
        return new(read.StartTicks, read.EndTicks, succeeded, senseKey);
    }

    public bool FlushReadCache(uint lba)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _token.ThrowIfCancellationRequested();
        if (lba >= Media.RecordedSectors) throw new ArgumentOutOfRangeException(nameof(lba));
        byte[] read = new byte[12]; read[0] = 0xa8; read[1] = 8;
        BinaryPrimitives.WriteUInt32BigEndian(read.AsSpan(2), lba);
        // READ(12), transfer length zero and FUA: a read-cache request, never SYNCHRONIZE CACHE.
        var result = _transport.Execute(read, 0, 30);
        return result.HResult >= 0 && result.HResult != BluRayMmcCommands.CommandHasSenseData;
    }

    private byte[] Query(byte[] cdb, int allocation)
    {
        _token.ThrowIfCancellationRequested();
        var result = _transport.Execute(cdb, allocation, 30);
        if (result.HResult < 0 || result.HResult == BluRayMmcCommands.CommandHasSenseData)
            throw new MmcCommandException(cdb[0], result);
        return result.Data;
    }
    public void Dispose() => _disposed = true;
}
