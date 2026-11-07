namespace DiscImageStudio.Burning;

public sealed partial class WindowsImapiBurner
{
    public Task<T> ReadDiscAsync<T>(string deviceId, ushort readSpeedKbps,
        Func<IOpticalReadSession, T> measure, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(measure);
        return _operationGate.RunAsync(() => RunOnStaThread(() =>
        {
            dynamic recorder = CreateRecorder(deviceId);
            bool acquired = false;
            Exception? failure = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                recorder.AcquireExclusiveAccess(false, "Disc Image Studio calibration");
                acquired = true;
                string? productName = Convert.ToString(recorder.ProductId)?.Trim();
                using var session = new OpticalReadSession(new ImapiMmcCommandTransport((object)recorder),
                    string.IsNullOrWhiteSpace(productName) ? deviceId : productName, readSpeedKbps, cancellationToken);
                try { return measure(session); }
                catch (Exception ex)
                {
                    ex.Data["Timing.RequestedReadSpeedKbps"] = readSpeedKbps;
                    ex.Data["Timing.ReadOpcode"] = $"0x{session.ReadOpcode:X2}";
                    ex.Data["Timing.MediaProfile"] = $"0x{session.CurrentProfile:X4}";
                    if (session.SpeedRequestResult is { } speedRequest)
                    {
                        ex.Data["Timing.SpeedRequestHResult"] = $"0x{speedRequest.HResult:X8}";
                        if (speedRequest.HResult < 0 || speedRequest.HResult == BluRayMmcCommands.CommandHasSenseData)
                            ex.Data["Timing.SpeedRequestDiagnostic"] = new MmcCommandException(0xbb, speedRequest).Message;
                    }
                    if (session.LastReadDiagnostic is { } diagnostic) ex.Data["LastReadDiagnostic"] = diagnostic;
                    throw;
                }
            }
            catch (Exception ex) { failure = ex; throw; }
            finally
            {
                try
                {
                    if (acquired)
                    {
                        try { recorder.ReleaseExclusiveAccess(); }
                        catch (Exception cleanup) when (failure is not null) { failure.Data["ReadSessionRelease"] = cleanup.ToString(); }
                    }
                }
                finally { ReleaseComObject((object)recorder); }
            }
        }), cancellationToken);
    }
}
