using System.Buffers.Binary;
using DiscImageStudio.Burning;

internal static class OpticalReadSessionTests
{
    internal static void Run()
    {
        foreach (int profile in new[] { 0x09, 0x11 })
        {
            var transport = new Transport(profile);
            using var session = new OpticalReadSession(transport, "Read test", 5540, CancellationToken.None);
            Require(session.Media.MediaKind == (profile == 0x09 ? OpticalBurnMediaKind.CdData : OpticalBurnMediaKind.DvdData), "loaded media family");
            var read = session.Read(32, 2, true);
            Require(read.Succeeded && read.EndTicks >= read.StartTicks, "successful timestamped read");
            var cdb = transport.Commands.Last();
            Require(cdb[0] == 0xa8 && cdb[1] == 8 && BinaryPrimitives.ReadUInt32BigEndian(cdb.AsSpan(2)) == 32
                && BinaryPrimitives.ReadUInt32BigEndian(cdb.AsSpan(6)) == 2, "READ(12) address, transfer length and FUA");
            Require(session.FlushReadCache(32) && transport.Commands.Last()[0] == 0xa8
                && BinaryPrimitives.ReadUInt32BigEndian(transport.Commands.Last().AsSpan(6)) == 0, "cache flush is a zero-length read");
            Require(transport.Commands.All(c => c[0] is 0x00 or 0x25 or 0x46 or 0xbb or 0xa8), "measurement never emits recording, format or close commands");
            Require(transport.Commands.Single(c => c[0] == 0xbb)[4..6].SequenceEqual(new byte[] { 0xff, 0xff }), "write speed is left unchanged");
            int count = transport.Commands.Count;
            Throws<ArgumentOutOfRangeException>(() => session.Read((uint)session.Media.RecordedSectors, 1, false));
            Throws<ArgumentOutOfRangeException>(() => session.Read(0, session.MaximumReadSectors + 1, false));
            Require(transport.Commands.Count == count, "invalid bounds never reach the device");
            session.Dispose();
            Throws<ObjectDisposedException>(() => session.Read(0, 1, false));
        }
        var unsupported = new Transport(0x41);
        Throws<NotSupportedException>(() => new OpticalReadSession(unsupported, "BD", 0, CancellationToken.None));
        var fallback = new Transport(0x11) { RejectRead12 = true };
        using (var session = new OpticalReadSession(fallback, "Fallback", 0, CancellationToken.None))
        {
            Require(session.Read(100, 1, true).Succeeded && fallback.Commands.Last()[0] == 0x28, "unsupported READ(12) falls back to READ(10)");
        }
        var mediumError = new Transport(0x11) { MediumError = true };
        using (var session = new OpticalReadSession(mediumError, "Unreadable", 0, CancellationToken.None))
        {
            Require(!session.Read(0, 1, false).Succeeded && !mediumError.Commands.Any(c => c[0] == 0x28), "medium errors do not switch the read command");
            Require(session.LastReadDiagnostic?.Contains("03/11/00", StringComparison.Ordinal) == true, "native failure details retained for the task log");
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var untouched = new Transport(0x11);
        Throws<OperationCanceledException>(() => new OpticalReadSession(untouched, "Cancelled", 0, cancelled.Token));
        Require(untouched.Commands.Count == 0, "cancellation before opening sends no commands");
        Console.WriteLine("optical-read-session: passed (read-only CDBs, monotonic timestamps, FUA, fallback, capacity bounds, cancellation and disposal)");
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Read session: " + message); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }

    private sealed class Transport(int profile) : IMmcCommandTransport
    {
        internal readonly List<byte[]> Commands = [];
        internal bool RejectRead12 { get; init; }
        internal bool MediumError { get; init; }
        public MmcWriteTransferLimits GetWriteTransferLimits() => new(0, 32 * 2048);
        public MmcCommandResult Execute(byte[] cdb, int allocationLength, uint timeoutSeconds)
        {
            Commands.Add((byte[])cdb.Clone());
            byte[] data = new byte[allocationLength];
            if (cdb[0] == 0x46)
            {
                BinaryPrimitives.WriteUInt32BigEndian(data, 4);
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6), (ushort)profile);
            }
            else if (cdb[0] == 0x25)
            {
                BinaryPrimitives.WriteUInt32BigEndian(data, profile == 0x09 ? 359_848u : 2_295_103u);
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), 2048);
            }
            else if (cdb[0] is 0x28 or 0xa8 && (MediumError || RejectRead12 && cdb[0] == 0xa8))
            {
                var sense = new byte[18]; sense[0] = 0x70; sense[2] = MediumError ? (byte)3 : (byte)5; sense[12] = MediumError ? (byte)0x11 : (byte)0x20;
                return new(BluRayMmcCommands.CommandHasSenseData, sense, []);
            }
            return new(0, [], data);
        }
    }
}
