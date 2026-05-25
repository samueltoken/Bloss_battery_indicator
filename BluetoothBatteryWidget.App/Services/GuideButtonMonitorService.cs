using BluetoothBatteryWidget.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class GuideButtonPressedEventArgs : EventArgs
{
    public GuideButtonPressedEventArgs(
        string address,
        string displayName,
        GuideButtonDeviceKind deviceKind,
        GuideButtonGesture gesture = GuideButtonGesture.Pressed)
    {
        Address = AddressNormalizer.NormalizeAddress(address);
        DisplayName = displayName;
        DeviceKind = deviceKind;
        Gesture = gesture;
    }

    public string Address { get; }

    public string DisplayName { get; }

    public GuideButtonDeviceKind DeviceKind { get; }

    public GuideButtonGesture Gesture { get; }
}

internal sealed record GuideButtonKnownDevice(
    string Address,
    string DisplayName,
    GuideButtonDeviceKind DeviceKind);

internal enum GuideButtonGesture
{
    Pressed = 0,
    ShortPress = 1,
    LongPress = 2
}

internal sealed class GuideButtonMonitorService : IDisposable
{
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PressDebounce = TimeSpan.FromMilliseconds(350);
    private const int StreamReadTimeoutMs = 650;
    private const int DualSenseMinimumReportSize = 78;
    private const int SteamMinimumReportSize = 64;
    private const int SteamFeatureReportSize = 65;
    private static readonly TimeSpan ShortPressMinimumDuration = TimeSpan.FromMilliseconds(35);
    private static readonly TimeSpan ShortPressMaximumDuration = TimeSpan.FromMilliseconds(900);
    private static readonly byte[] SteamSnapshotReportIds = [0x42, 0x00, 0x01];

    private readonly object _sync = new();
    private readonly Dictionary<string, Task> _endpointTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastPressByAddress = new(StringComparer.OrdinalIgnoreCase);
    private Func<IReadOnlyList<GuideButtonKnownDevice>> _knownDeviceProvider = static () => [];
    private CancellationTokenSource? _cts;
    private Task? _supervisorTask;
    private bool _disposed;
    private string _lastDiscoveryLogKey = string.Empty;

    public event EventHandler<GuideButtonPressedEventArgs>? GuideButtonPressed;

    public void SetKnownDeviceProvider(Func<IReadOnlyList<GuideButtonKnownDevice>> provider)
    {
        lock (_sync)
        {
            _knownDeviceProvider = provider ?? (static () => []);
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_disposed || _supervisorTask is { IsCompleted: false })
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _supervisorTask = Task.Run(() => RunSupervisorAsync(_cts.Token));
            WriteGuideButtonEvent(
                "service_started",
                deviceKind: string.Empty,
                address: string.Empty,
                displayName: string.Empty,
                message: "Guide button monitor service started.");
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cts = _cts;
            _cts = null;
        }

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // Ignore shutdown races.
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private async Task RunSupervisorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                PruneCompletedEndpointTasks();
                var knownDevices = ReadKnownDevices();
                var endpoints = DiscoverEndpoints(cancellationToken, knownDevices);
                WriteDiscoverySummary(endpoints, knownDevices);
                foreach (var endpoint in endpoints)
                {
                    StartEndpointMonitor(endpoint, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // The monitor is best-effort; battery display must keep running even if input watching fails.
            }

            try
            {
                await Task.Delay(DiscoveryInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private IReadOnlyList<GuideButtonKnownDevice> ReadKnownDevices()
    {
        Func<IReadOnlyList<GuideButtonKnownDevice>> provider;
        lock (_sync)
        {
            provider = _knownDeviceProvider;
        }

        try
        {
            return provider();
        }
        catch
        {
            return [];
        }
    }

    private void PruneCompletedEndpointTasks()
    {
        lock (_sync)
        {
            foreach (var pair in _endpointTasks.Where(pair => pair.Value.IsCompleted).ToList())
            {
                _endpointTasks.Remove(pair.Key);
            }
        }
    }

    private void StartEndpointMonitor(GuideButtonEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint.DevicePath) || string.IsNullOrWhiteSpace(endpoint.Address))
        {
            return;
        }

        lock (_sync)
        {
            if (_endpointTasks.TryGetValue(endpoint.DevicePath, out var existing) && !existing.IsCompleted)
            {
                return;
            }

            _endpointTasks[endpoint.DevicePath] = Task.Run(
                () => MonitorEndpointAsync(endpoint, cancellationToken),
                cancellationToken);
        }
    }

    private void MonitorEndpointAsync(GuideButtonEndpoint endpoint, CancellationToken cancellationToken)
    {
        var minimumReportSize = endpoint.DeviceKind == GuideButtonDeviceKind.DualSense
            ? DualSenseMinimumReportSize
            : SteamMinimumReportSize;
        var lastPressed = false;
        DateTimeOffset? pressedAt = null;
        var emptyReadCount = 0;

        using var handle = HidGamepadAccess.OpenHandle(endpoint.DevicePath);
        if (handle.IsInvalid)
        {
            WriteGuideButtonEvent("open_failed", endpoint, "HID handle could not be opened.");
            return;
        }

        using var session = new HidInputStreamSession(handle);
        if (!session.IsAvailable)
        {
            WriteGuideButtonEvent("stream_unavailable", endpoint, "HID input stream is not available.");
            return;
        }

        WriteGuideButtonEvent("monitor_started", endpoint, "Guide button monitor started.");
        var snapshotFallbackLogged = false;
        var snapshotUnavailableLogged = false;
        var snapshotReportLogged = false;
        var unparsedReportLogged = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshotDiagnostic = string.Empty;
            if (endpoint.DeviceKind == GuideButtonDeviceKind.SteamController &&
                TryReadSteamGuideSnapshot(handle, out var polledPressed, out snapshotDiagnostic))
            {
                if (!snapshotFallbackLogged)
                {
                    snapshotFallbackLogged = true;
                    WriteGuideButtonEvent(
                        "snapshot_poll_active",
                        endpoint,
                        "Steam Controller latest input-state polling is active.");
                }

                HandleGuideButtonState(endpoint, polledPressed, ref lastPressed, ref pressedAt);
            }
            else if (endpoint.DeviceKind == GuideButtonDeviceKind.SteamController &&
                     !string.IsNullOrWhiteSpace(snapshotDiagnostic) &&
                     !snapshotReportLogged)
            {
                snapshotReportLogged = true;
                WriteGuideButtonEvent(
                    "snapshot_report_unparsed",
                    endpoint,
                    $"Steam Controller snapshot polling returned a report, but not the guide state. {snapshotDiagnostic}");
            }

            if (!session.TryReadReport(
                    0x00,
                    minimumReportSize,
                    StreamReadTimeoutMs,
                    out var frame,
                    out _))
            {
                if (endpoint.DeviceKind == GuideButtonDeviceKind.SteamController &&
                    TryReadSteamGuideSnapshot(handle, out var snapshotPressed, out snapshotDiagnostic))
                {
                    emptyReadCount = 0;
                    if (!snapshotFallbackLogged)
                    {
                        snapshotFallbackLogged = true;
                        WriteGuideButtonEvent(
                            "snapshot_poll_active",
                            endpoint,
                            "Steam Controller stream was quiet; polling the latest input state instead.");
                    }

                    HandleGuideButtonState(endpoint, snapshotPressed, ref lastPressed, ref pressedAt);
                    continue;
                }

                if (endpoint.DeviceKind == GuideButtonDeviceKind.SteamController &&
                    !snapshotUnavailableLogged)
                {
                    snapshotUnavailableLogged = true;
                    WriteGuideButtonEvent(
                        "snapshot_poll_unavailable",
                        endpoint,
                        "Steam Controller stream was quiet and latest input-state polling did not return a recognizable report.");
                }

                emptyReadCount++;
                if (emptyReadCount >= 12)
                {
                    WriteGuideButtonEvent("monitor_idle_timeout", endpoint, "No HID input report was received for this endpoint.");
                    return;
                }

                continue;
            }

            emptyReadCount = 0;
            if (!GuideButtonReportParser.TryParseGuideButton(endpoint.DeviceKind, frame.Data, out var pressed))
            {
                if (endpoint.DeviceKind == GuideButtonDeviceKind.SteamController &&
                    !unparsedReportLogged)
                {
                    unparsedReportLogged = true;
                    WriteGuideButtonEvent(
                        "steam_unparsed_report",
                        endpoint,
                        $"Steam Controller HID report was seen but not recognized as the guide button. {BuildReportSignature(frame.Data)}");
                }

                continue;
            }

            HandleGuideButtonState(endpoint, pressed, ref lastPressed, ref pressedAt);
        }
    }

    private void HandleGuideButtonState(
        GuideButtonEndpoint endpoint,
        bool pressed,
        ref bool lastPressed,
        ref DateTimeOffset? pressedAt)
    {
        var now = DateTimeOffset.Now;
        if (pressed && !lastPressed)
        {
            pressedAt = now;
        }
        else if (!pressed && lastPressed)
        {
            var duration = pressedAt.HasValue ? now - pressedAt.Value : TimeSpan.Zero;
            if (duration >= ShortPressMinimumDuration && duration <= ShortPressMaximumDuration)
            {
                RaiseGuideButtonPressed(endpoint, GuideButtonGesture.ShortPress);
            }

            pressedAt = null;
        }

        lastPressed = pressed;
    }

    private static bool TryReadSteamGuideSnapshot(
        SafeFileHandle handle,
        out bool isPressed,
        out string diagnostic)
    {
        isPressed = false;
        diagnostic = string.Empty;
        if (HidGamepadAccess.TryReadFeatureReport(
                handle,
                0x00,
                SteamFeatureReportSize,
                out var featureReport,
                retryCount: 0) &&
            GuideButtonReportParser.TryParseGuideButton(
                GuideButtonDeviceKind.SteamController,
                featureReport,
                out isPressed))
        {
            return true;
        }

        if (featureReport.Length > 0)
        {
            diagnostic = $"feature: {BuildReportSignature(featureReport)}";
        }

        foreach (var reportId in SteamSnapshotReportIds)
        {
            if (!HidGamepadAccess.TryReadInputReportSnapshot(
                    handle,
                    reportId,
                    SteamMinimumReportSize,
                    out var report,
                    out _))
            {
                continue;
            }

            diagnostic = $"inputReport=0x{reportId:X2}: {BuildReportSignature(report)}";
            if (GuideButtonReportParser.TryParseGuideButton(
                    GuideButtonDeviceKind.SteamController,
                    report,
                    out isPressed))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildReportSignature(ReadOnlySpan<byte> report)
    {
        if (report.Length == 0)
        {
            return "len=0.";
        }

        var sampleLength = Math.Min(12, report.Length);
        var parts = new string[sampleLength];
        for (var index = 0; index < sampleLength; index++)
        {
            parts[index] = report[index].ToString("X2");
        }

        return $"len={report.Length}, first={string.Join('-', parts)}.";
    }

    private void RaiseGuideButtonPressed(GuideButtonEndpoint endpoint, GuideButtonGesture gesture)
    {
        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            if (_lastPressByAddress.TryGetValue(endpoint.Address, out var lastPress) &&
                now - lastPress <= PressDebounce)
            {
                return;
            }

            _lastPressByAddress[endpoint.Address] = now;
        }

        GuideButtonPressed?.Invoke(
            this,
            new GuideButtonPressedEventArgs(endpoint.Address, endpoint.DisplayName, endpoint.DeviceKind, gesture));
        WriteGuideButtonEvent("pressed", endpoint, $"Guide button {gesture} detected.");
    }

    private void WriteDiscoverySummary(
        IReadOnlyList<GuideButtonEndpoint> endpoints,
        IReadOnlyList<GuideButtonKnownDevice> knownDevices)
    {
        var knownSteamCount = knownDevices.Count(device => device.DeviceKind == GuideButtonDeviceKind.SteamController);
        var key = endpoints.Count == 0
            ? $"none:knownSteam={knownSteamCount}"
            : string.Join(
                '|',
                endpoints
                    .OrderBy(endpoint => endpoint.DeviceKind)
                    .ThenBy(endpoint => endpoint.Address, StringComparer.OrdinalIgnoreCase)
                    .Select(endpoint => $"{endpoint.DeviceKind}:{endpoint.Address}")) +
              $"|knownSteam={knownSteamCount}";

        lock (_sync)
        {
            if (string.Equals(_lastDiscoveryLogKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _lastDiscoveryLogKey = key;
        }

        if (endpoints.Count == 0)
        {
            var message = knownSteamCount > 0
                ? $"No guide-button HID endpoint is currently visible, but {knownSteamCount} Steam Controller device(s) are present in the battery list."
                : "No DualSense or Steam Controller HID endpoint is currently visible.";
            WriteGuideButtonEvent(
                "discovery_no_endpoints",
                deviceKind: string.Empty,
                address: string.Empty,
                displayName: string.Empty,
                message: message);
            return;
        }

        var endpointBreakdown = string.Join(
            ", ",
            endpoints
                .GroupBy(endpoint => endpoint.DeviceKind)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}={group.Count()}"));
        WriteGuideButtonEvent(
            "discovery_endpoints",
            deviceKind: string.Empty,
            address: string.Empty,
            displayName: string.Empty,
            message: $"Visible guide-button endpoints: {endpoints.Count} ({endpointBreakdown}); known Steam devices: {knownSteamCount}.");
    }

    private void WriteGuideButtonEvent(string eventName, GuideButtonEndpoint endpoint, string message)
    {
        WriteGuideButtonEvent(
            eventName,
            endpoint.DeviceKind.ToString(),
            endpoint.Address,
            endpoint.DisplayName,
            message);
    }

    private void WriteGuideButtonEvent(
        string eventName,
        string deviceKind,
        string address,
        string displayName,
        string message)
    {
        GuideButtonEventLog.Write(eventName, deviceKind, address, displayName, message);
    }

    private static IReadOnlyList<GuideButtonEndpoint> DiscoverEndpoints(
        CancellationToken cancellationToken,
        IReadOnlyList<GuideButtonKnownDevice> knownDevices)
    {
        var results = new List<GuideButtonEndpoint>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in HidGamepadAccess.EnumerateProbeEndpoints(null, HidEndpointDiscoveryStage.GlobalAggressive, cancellationToken))
        {
            AddIfSupported(endpoint, results, paths);
        }

        foreach (var endpoint in HidGamepadAccess.EnumerateSteamControllerTritonEndpoints(cancellationToken))
        {
            AddSteamEndpoint(endpoint, results, paths);
        }

        foreach (var knownDevice in knownDevices)
        {
            if (knownDevice.DeviceKind != GuideButtonDeviceKind.SteamController)
            {
                continue;
            }

            var knownAddress = AddressNormalizer.NormalizeAddress(knownDevice.Address);
            if (string.IsNullOrWhiteSpace(knownAddress))
            {
                continue;
            }

            foreach (var endpoint in HidGamepadAccess.EnumerateProbeEndpoints(
                         knownAddress,
                         HidEndpointDiscoveryStage.Relaxed,
                         cancellationToken))
            {
                AddSteamEndpoint(
                    endpoint,
                    results,
                    paths,
                    string.IsNullOrWhiteSpace(knownDevice.DisplayName)
                        ? "Steam Controller"
                        : knownDevice.DisplayName);
            }
        }

        return results;
    }

    private static void AddIfSupported(
        HidGamepadEndpoint endpoint,
        List<GuideButtonEndpoint> results,
        HashSet<string> paths)
    {
        if (string.IsNullOrWhiteSpace(endpoint.DevicePath) || paths.Contains(endpoint.DevicePath))
        {
            return;
        }

        if (PlayStationUsbBridgeSupport.IsSupportedVidPid(endpoint.VendorId, endpoint.ProductId))
        {
            var address = AddressNormalizer.NormalizeAddress(endpoint.Address);
            if (string.IsNullOrWhiteSpace(address))
            {
                address = PlayStationUsbBridgeSupport.BuildSyntheticAddress(endpoint.InstanceId, endpoint.DevicePath);
            }

            if (!string.IsNullOrWhiteSpace(address))
            {
                paths.Add(endpoint.DevicePath);
                results.Add(new GuideButtonEndpoint(
                    endpoint.DevicePath,
                    address,
                    string.IsNullOrWhiteSpace(endpoint.DisplayName)
                        ? PlayStationUsbBridgeSupport.GetDisplayName(endpoint.ProductId)
                        : endpoint.DisplayName,
                    GuideButtonDeviceKind.DualSense));
            }

            return;
        }

        if (IsSteamControllerVidPid(endpoint.VendorId, endpoint.ProductId) ||
            endpoint.DisplayName.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) ||
            endpoint.DisplayName.Contains("Steam Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            AddSteamEndpoint(endpoint, results, paths);
        }
    }

    private static void AddSteamEndpoint(
        HidGamepadEndpoint endpoint,
        List<GuideButtonEndpoint> results,
        HashSet<string> paths,
        string? displayNameOverride = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint.DevicePath) || paths.Contains(endpoint.DevicePath))
        {
            return;
        }

        var address = AddressNormalizer.NormalizeAddress(endpoint.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            address = PlayStationUsbBridgeSupport.BuildSyntheticAddress(endpoint.InstanceId, endpoint.DevicePath);
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        paths.Add(endpoint.DevicePath);
        results.Add(new GuideButtonEndpoint(
            endpoint.DevicePath,
            address,
            !string.IsNullOrWhiteSpace(displayNameOverride)
                ? displayNameOverride
                : string.IsNullOrWhiteSpace(endpoint.DisplayName) ? "Steam Controller" : endpoint.DisplayName,
            GuideButtonDeviceKind.SteamController));
    }

    private static bool IsSteamControllerVidPid(string? vendorId, string? productId)
    {
        if (!string.Equals(vendorId, "28DE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(productId, "1303", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1304", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1305", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1142", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GuideButtonEndpoint(
        string DevicePath,
        string Address,
        string DisplayName,
        GuideButtonDeviceKind DeviceKind);
}
