using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DiscImageStudio.Burning;
using DiscImageStudio.Localization;

internal static class BurningTranslationTests
{
    private static readonly OpticalBurnBufferOptions Buffers = new(1024 * 1024, 256 * 1024);
    private static readonly Dictionary<string, byte[]> Baselines = new();
    private const string Detail = "device detail: İ-日本語-原始错误";

    internal static void Run()
    {
        TestValidationAndStreams();
        TestCd(OpticalBurnMediaKind.CdAudio);
        TestCd(OpticalBurnMediaKind.CdData);
        TestBluRay();
        TestCancellationAndFailure();
        TestCleanup();
        TestDeviceErrors();
    }

    private static void TestValidationAndStreams()
    {
        ArgumentOutOfRangeException capacity = Throws<ArgumentOutOfRangeException>(() => new OpticalBurnBufferOptions(1, 1).Validate());
        Check(capacity.ParamName == "CapacityBytes" && capacity.Message.StartsWith(L.T("Buffer capacity must be a positive multiple of 256 KiB."), StringComparison.Ordinal), "buffer validation language/parameter");
        OpticalBurnRequest bad = Request(OpticalBurnMediaKind.CdAudio, [1]);
        ArgumentOutOfRangeException length = Throws<ArgumentOutOfRangeException>(bad.Validate);
        Check(length.ParamName == "ContentLength" && length.Message.StartsWith(L.Format("Stream length must be a positive multiple of {0} bytes.", 2352), StringComparison.Ordinal), "stream validation language/parameter");
        ArgumentException payload = Throws<ArgumentException>(() => CdMode1SectorEncoder.Encode([1], 0, new byte[2352]));
        Check(payload.ParamName == "payload" && payload.Message.StartsWith(L.T("A Mode 1 payload must contain exactly 2048 bytes."), StringComparison.Ordinal), "Mode 1 validation language/parameter");
        byte[] sector = new byte[2352];
        CdMode1SectorEncoder.Encode(Data(2048), 1234, sector);
        SameBytes("mode1", sector);
        using GeneratedContentComStream stream = Content(Request(OpticalBurnMediaKind.BluRayData, Data(2048)));
        COMException readOnly = Throws<COMException>(() => stream.SetSize(1));
        Check(readOnly.HResult == unchecked((int)0x80030001) && readOnly.Message == L.T("The generated burn stream is read-only."), "COM read-only code and translated message");
        using GeneratedContentComStream shortStream = new(2048, (output, _) => output.Write(new byte[10]), null, CancellationToken.None, Buffers);
        InvalidDataException shortOutput = Throws<InvalidDataException>(() => shortStream.WaitUntilPrebuffered(CancellationToken.None));
        Check(shortOutput.Message == L.Format("Generator produced {0} bytes; expected {1} bytes.", 10, 2048), "producer-thread language propagation");
    }

    private static void TestCd(OpticalBurnMediaKind kind)
    {
        byte[] bytes = Data(300 * (kind == OpticalBurnMediaKind.CdAudio ? 2352 : 2048));
        OpticalBurnRequest request = Request(kind, bytes) with { WriteSpeed = new(1200, false) };
        using GeneratedContentComStream content = Content(request);
        CdSession session = new();
        using CdImage image = new();
        List<OpticalBurnOperation> operations = [];
        OpticalWriteSpeed speed = WindowsImapiBurner.BurnCdDao(session, image, content, request,
            CancellationToken.None, operations.Add);
        Check(session.Calls.SequenceEqual(new[] { "prepare", "sector:2", "speed:1200:False", "write", "release" }), "CD device command order");
        Check(speed == new OpticalWriteSpeed(1200, false) && image.Created, "CD speed and image creation");
        Check(session.Bytes.AsSpan().SequenceEqual(bytes), "CD data supplied to the recorder");
        SameBytes(kind.ToString(), session.Bytes);
        Check(operations.Count(operation => operation.IsFinalizing) == 1 && operations[^1].IsFinalizing, "CD finalization flag follows state, not translated text");
        Check(operations[^1].Message == L.T("关闭 CD 刻录会话"), "translated CD close operation");
        string track = L.T(kind == OpticalBurnMediaKind.CdData ? "数据轨" : "音轨");
        Check(operations.Any(operation => operation.Message == L.Format("写入 CD DAO {0}", track)), "translated CD track description");
        using GeneratedContentComStream rejected = Content(request);
        CdSession small = new() { LeadoutLimit = 296 };
        using CdImage sameImage = new();
        InvalidOperationException overburn = Throws<InvalidOperationException>(() => WindowsImapiBurner.BurnCdDao(small, sameImage, rejected, request, CancellationToken.None));
        Check(overburn.Message == L.Format("CD DAO lead-out 超出盘片边界 {0} 个扇区；最多允许 {1} 个 DAO 扇区的受限 overburn。", 4, 3), "localized unchanged overburn limit");
        Check(!small.Calls.Contains("write") && small.Calls[^1] == "release", "CD rejection before write with cleanup");
    }

    private static void TestBluRay()
    {
        byte[] bytes = Data(64 * 2048);
        OpticalBurnRequest request = Request(OpticalBurnMediaKind.BluRayData, bytes);
        using GeneratedContentComStream content = Content(request);
        using BdSession session = new();
        List<OpticalBurnOperation> operations = [];
        OpticalWriteSpeed speed = WindowsImapiBurner.BurnBluRay(session, content, request,
            CancellationToken.None, operations.Add);
        Check(session.Calls.SequenceEqual(new[] { "acquire", "media", "configure", "write", "flush", "close-track:1", "close-disc", "release" }), "BD device sequence");
        Check(speed == new OpticalWriteSpeed(4500, false) && session.Sectors == 64, "BD speed and sector count");
        Check(session.Transport.Commands.Select(Convert.ToHexString).SequenceEqual(new[] { "2A000000000000002000", "2A000000002000002000" }), "independent literal WRITE(10) vectors");
        Check(session.Transport.Timeouts.All(timeout => timeout == 200), "unchanged WRITE timeout");
        byte[] written = session.Transport.Payloads.SelectMany(value => value).ToArray();
        Check(written.AsSpan().SequenceEqual(bytes), "BD payload unchanged");
        SameBytes("bd", written);
        Check(operations.Count(operation => operation.IsFinalizing) == 1 && operations[^1].IsFinalizing, "BD finalization flag follows state, not translated text");
        Check(operations[^1].Message == L.T("刷新 BD-R 缓存并封盘（请等待完成）"), "translated BD finalization operation");
        Check(operations[3].Message == L.Format("无备用区顺序写入 BD-R：{0:N0} 扇区", 64), "translated BD write operation");
        Check(session.Progress[^1].Stage == "write" && session.Progress[^1].CompletedBytes == bytes.Length
              && session.Progress[^1].BytesPerSecond > 0, "device-accepted progress and rate");
        Check(session.Progress[^1].Message.StartsWith(L.T("刻录机已接收"), StringComparison.Ordinal), "translated device progress");
        string[] diagnostics = session.Diagnostics.ToArray();
        Check(diagnostics.Length == 1 && diagnostics[0].Contains(L.T("（最后窗口）"), StringComparison.Ordinal), "translated final diagnostic window");
        Check(diagnostics[0] == L.Format("BD 写入诊断{0}：窗口 {1:F3} 秒，设备接受速度={2}；源流读取（含阻塞/拷贝）={3:F3} 秒（{4}），WRITE（含传输/设备等待）={5:F3} 秒（{6}）；累计接受={7:N0} 字节。源流读取耗时不是纯 CPU 生成耗时。",
            L.T("（最后窗口）"), 2.2, $"{bytes.Length / 2.2 / 1_000_000.0:F3} MB/s", 0d, $"{0d:F1}%", 2.2, $"{100d:F1}%", bytes.Length), "complete translated timing explanation and preserved numeric culture");
    }

    private static void TestCancellationAndFailure()
    {
        OpticalBurnRequest request = Request(OpticalBurnMediaKind.BluRayData, Data(64 * 2048));
        using CancellationTokenSource cancel = new();
        using GeneratedContentComStream cancelledContent = Content(request);
        using BdSession cancelled = new() { AfterWrite = cancel.Cancel };
        List<OpticalBurnOperation> cancelledOperations = [];
        OperationCanceledException cancellation = Throws<OperationCanceledException>(() => WindowsImapiBurner.BurnBluRay(cancelled, cancelledContent, request, cancel.Token, cancelledOperations.Add));
        Check(cancellation.CancellationToken == cancel.Token, "original cancellation token");
        Check(cancelled.Calls.SequenceEqual(new[] { "acquire", "media", "configure", "write", "cancel", "flush", "release" })
              && cancelledOperations.All(operation => !operation.IsFinalizing), "cancelled image is flushed and released without finalization");
        using GeneratedContentComStream failedContent = Content(request);
        IOException cause = new(Detail);
        using BdSession failed = new() { WriteFailure = cause, FlushFailure = new COMException(Detail, unchecked((int)0x8007048f)) };
        List<string> warnings = [];
        IOException observed = Throws<IOException>(() => WindowsImapiBurner.BurnBluRay(failed, failedContent, request,
            CancellationToken.None, reportCleanupWarning: warnings.Add));
        Check(ReferenceEquals(cause, observed) && failed.Calls[^1] == "release" && !failed.Calls.Contains("close-disc"), "original failure and release retained");
        Check(warnings.Count == 1 && warnings[0].Contains("0x8007048F", StringComparison.Ordinal)
              && warnings[0].EndsWith(Detail, StringComparison.Ordinal), "secondary HRESULT and original device text");
    }

    private static void TestCleanup()
    {
        COMException cause = new(Detail, unchecked((int)0x800700aa));
        int unlocks = 0, releases = 0;
        List<string> warnings = [];
        using (BluRaySessionCleanup cleanup = new(() => { }, () => { }, () => { },
                   () => { if (++unlocks == 1) throw cause; }, () => { }, () => { }, () => { }, warnings.Add))
        {
            cleanup.Acquire();
            Check(ReferenceEquals(Throws<COMException>(cleanup.Release), cause), "cleanup preserves device exception identity");
        }
        Check(unlocks == 2 && warnings.SequenceEqual(new[] { L.Format("蓝光清理：{0}失败（HRESULT 0x{1:X8}）：{2}", L.T("解除盘片弹出保护"), cause.HResult, Detail) }), "localized cleanup and bounded retry");
        InvalidOperationException combined = Throws<InvalidOperationException>(() => WindowsImapiBurner.CompleteBurnCleanup(null, [],
            () => { releases++; throw cause; }, () => releases++));
        string warning = L.Format("{0}失败（HRESULT 0x{1:X8}）：{2}", L.T("释放刻录格式 COM 对象"), cause.HResult, Detail);
        string details = L.Format("\n设备释放期间还出现以下问题（已尝试后续清理）：\n{0}", warning);
        Check(releases == 2 && ReferenceEquals(combined.InnerException, cause)
              && combined.Message == L.Format("刻录流程已完成，但设备未能正常释放。{0}", details), "complete translated cleanup chain and both releases");
    }

    private static void TestDeviceErrors()
    {
        MmcCommandResult result = new(unchecked((int)0x8007048f), [], []);
        MmcCommandException command = new(0x2a, result);
        Check(!command.HasValidSense && command.HResult == result.HResult
              && command.Message == L.Format("蓝光命令 0x{0:X2} 失败（HRESULT 0x{1:X8}，{2}{3}）。", 0x2a, result.HResult,
                  L.T("未返回有效的设备 Sense 数据"), L.T("，Windows 报告设备未连接")), "MMC status data and full nested translation");
        BluRayWriteDiagnostics diagnostic = new(65536, 1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3),
            [new(0, 32, TimeSpan.FromSeconds(.1), TimeSpan.FromSeconds(1))], new(32, 32, TimeSpan.FromSeconds(.2), TimeSpan.FromSeconds(2)));
        BluRayWriteException write = new(32, 32, 200, TimeSpan.FromSeconds(2), result, diagnostics: diagnostic);
        Check(write.StartLba == 32 && write.SectorCount == 32 && write.TimeoutSeconds == 200 && write.HResult == result.HResult
              && write.Sense.Length == 0 && ReferenceEquals(write.Diagnostics, diagnostic), "WRITE failure fields");
        Check(write.Message == L.Format("蓝光 WRITE(10) 0x2A 失败：LBA={0:N0}，扇区={1}，超时设置={2} 秒，命令耗时={3:F3} 秒；{4}{5} 未自动重试此写入命令。{6}",
            32, 32, 200, 2d, command.Message, L.T(" 无返回 Sense 数据。"), diagnostic.Describe()), "full nested WRITE failure translation");
    }

    private static OpticalBurnRequest Request(OpticalBurnMediaKind kind, byte[] bytes) =>
        new("fake-İ-日本語", kind, bytes.Length, (stream, token) => { token.ThrowIfCancellationRequested(); stream.Write(bytes); }, BufferOptions: Buffers);

    private static GeneratedContentComStream Content(OpticalBurnRequest request)
    {
        GeneratedContentComStream stream = new(request.ContentLength, request.ProduceContent, null, CancellationToken.None, Buffers);
        stream.WaitUntilPrebuffered(CancellationToken.None);
        return stream;
    }

    private static byte[] Data(int length) => Enumerable.Range(0, length).Select(index => (byte)(index * 37 + index / 97)).ToArray();
    private static void SameBytes(string name, byte[] actual)
    {
        if (Baselines.TryGetValue(name, out byte[]? expected)) Check(actual.AsSpan().SequenceEqual(expected), "language changed " + name + " bytes");
        else Baselines[name] = actual;
    }

    private static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T exception) { return exception; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(L.CurrentLanguage + ": " + message);
    }

    private sealed class Transport : IMmcCommandTransport
    {
        internal readonly List<byte[]> Commands = [];
        internal readonly List<byte[]> Payloads = [];
        internal readonly List<uint> Timeouts = [];
        internal TimeSpan Clock;
        public MmcWriteTransferLimits GetWriteTransferLimits() => new(0, 65536);
        public MmcCommandResult Execute(byte[] cdb, int length, uint timeout) => throw new InvalidOperationException("Unexpected device query");
        public MmcCommandResult SendData(byte[] cdb, IntPtr buffer, int length, uint timeout)
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(buffer, bytes, 0, length);
            Commands.Add(cdb.ToArray()); Payloads.Add(bytes); Timeouts.Add(timeout);
            Clock += TimeSpan.FromMilliseconds(1100);
            return new(0, [], []);
        }
    }

    private sealed class BdSession : IBluRaySequentialSession
    {
        internal readonly List<string> Calls = [];
        internal readonly Transport Transport = new();
        internal readonly List<OpticalBurnProgress> Progress = [];
        internal readonly List<string> Diagnostics = [];
        internal uint Sectors;
        internal Action? AfterWrite;
        internal Exception? WriteFailure, FlushFailure;
        public void Acquire() => Calls.Add("acquire");
        public BluRaySequentialMediaInfo ReadMedia()
        {
            Calls.Add("media");
            return new(0x41, true, 1, true, true, 0, 0, 12_219_392, 12_219_392, 12_219_392, 1);
        }
        public OpticalWriteSpeed ConfigureWrite(OpticalWriteSpeed? speed) { Calls.Add("configure"); return new(4500, false); }
        public void Write(IStream content, uint sectors)
        {
            Calls.Add("write"); Sectors = sectors;
            if (WriteFailure is not null) throw WriteFailure;
            ImapiWriteProgressReporter reporter = new(OpticalBurnMediaKind.BluRayData, sectors * 2048L, Progress.Add, () => Transport.Clock);
            BluRaySequentialWriter.Write(Transport, content, sectors, reporter.ReportAcceptedBytes,
                elapsed: () => Transport.Clock, reportDiagnostics: Diagnostics.Add);
            AfterWrite?.Invoke();
        }
        public void CancelWrite() => Calls.Add("cancel");
        public void Flush() { Calls.Add("flush"); if (FlushFailure is not null) throw FlushFailure; }
        public void CloseTrack(int track) => Calls.Add("close-track:" + track.ToString(CultureInfo.InvariantCulture));
        public void CloseDisc() => Calls.Add("close-disc");
        public void Release() => Calls.Add("release");
        public void Dispose() { }
    }

    private sealed class CdImage : ICdRawImageSession
    {
        internal bool Created;
        public long StartOfLeadout => 300;
        public IStream CreateImage(IStream content) { Created = true; return content; }
        public void Dispose() { }
    }

    private sealed class CdSession : ICdRawSession
    {
        internal readonly List<string> Calls = [];
        internal long LeadoutLimit = 300;
        internal byte[] Bytes = [];
        public bool MediaPhysicallyBlank => true;
        public long LastPossibleStartOfLeadout => LeadoutLimit;
        public IReadOnlyList<int> SupportedSectorTypes => [2];
        public int CurrentWriteSpeed => 1200;
        public bool CurrentRotationTypeIsPureCav => false;
        public void PrepareMedia() => Calls.Add("prepare");
        public void SetRequestedSectorType(int type) => Calls.Add("sector:" + type);
        public void SetWriteSpeed(int speed, bool cav) => Calls.Add($"speed:{speed}:{cav}");
        public void WriteMedia(IStream content)
        {
            Calls.Add("write");
            content.Stat(out STATSTG stat, 1);
            Bytes = new byte[checked((int)stat.cbSize)];
            IntPtr count = Marshal.AllocHGlobal(4);
            try
            {
                content.Read(Bytes, Bytes.Length, count);
                Check(Marshal.ReadInt32(count) == Bytes.Length, "complete CD data supplied");
            }
            finally { Marshal.FreeHGlobal(count); }
        }
        public void CancelWrite() => Calls.Add("cancel");
        public void ReleaseMedia() => Calls.Add("release");
    }
}
