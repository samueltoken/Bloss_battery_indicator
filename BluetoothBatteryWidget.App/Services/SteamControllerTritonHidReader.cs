using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class SteamControllerTritonHidReader
{
    private const byte BatteryReportId = 0x43;
    private const int BatteryReportSize = 17;
    private const int AnyReportSize = 64;
    private const int QuickReadTimeoutMs = 120;
    private const int EndpointDiscoveryTimeoutMs = 320;
    private const int ActiveBatteryReadTimeoutMs = 2200;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    private readonly object _cacheLock = new();
    private IReadOnlyList<SteamControllerTritonSnapshot> _cachedSnapshots = [];
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public IReadOnlyList<SteamControllerTritonSnapshot> ReadSnapshots(
        CancellationToken cancellationToken,
        bool waitForBattery = true)
    {
        if (waitForBattery && TryReadCachedSnapshots(out var cachedSnapshots))
        {
            return cachedSnapshots;
        }

        var endpoints = HidGamepadAccess.EnumerateSteamControllerTritonEndpoints(cancellationToken);
        if (endpoints.Count == 0)
        {
            return [];
        }

        var groups = endpoints
            .Where(endpoint => !string.IsNullOrWhiteSpace(AddressNormalizer.NormalizeAddress(endpoint.Address)))
            .GroupBy(endpoint => AddressNormalizer.NormalizeAddress(endpoint.Address), StringComparer.OrdinalIgnoreCase);

        var snapshots = new List<SteamControllerTritonSnapshot>();
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var orderedEndpoints = group
                .OrderBy(endpoint => endpoint.InstanceId.Contains("COL03", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(endpoint => endpoint.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SteamControllerBatteryStatus? batteryStatus = null;
            var sawAnyBatteryStatus = false;
            var sawConnectedSignal = false;
            var sawDisconnectedSignal = false;
            var firstEndpoint = orderedEndpoints[0];

            foreach (var endpoint in orderedEndpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var handle = HidGamepadAccess.OpenHandle(endpoint.DevicePath);
                if (handle.IsInvalid)
                {
                    continue;
                }

                if (TryReadBatteryStatusSnapshot(handle, out var status))
                {
                    sawAnyBatteryStatus = true;
                    if (IsDisplayableControllerBattery(status))
                    {
                        batteryStatus = ChoosePreferredBatteryStatus(batteryStatus, status);
                        sawConnectedSignal = true;
                        if (!IsSuspiciousDockedFullBattery(status))
                        {
                            break;
                        }
                    }
                }

                var endpointResult = ReadEndpointReports(handle, waitForBattery, cancellationToken);
                if (endpointResult.BatteryStatus is not null)
                {
                    sawAnyBatteryStatus = true;
                    if (IsDisplayableControllerBattery(endpointResult.BatteryStatus))
                    {
                        batteryStatus = ChoosePreferredBatteryStatus(batteryStatus, endpointResult.BatteryStatus);
                        sawConnectedSignal = true;
                        if (!IsSuspiciousDockedFullBattery(endpointResult.BatteryStatus))
                        {
                            break;
                        }
                    }
                }

                sawConnectedSignal |= endpointResult.ConnectionSignal == TritonConnectionSignal.Connected;
                sawDisconnectedSignal |= endpointResult.ConnectionSignal == TritonConnectionSignal.Disconnected;
            }

            var isConnected = ShouldExposeController(
                batteryStatus,
                sawAnyBatteryStatus,
                sawConnectedSignal,
                sawDisconnectedSignal);
            if (!isConnected)
            {
                continue;
            }

            snapshots.Add(new SteamControllerTritonSnapshot(
                DeviceId: $"steam-triton:{group.Key}",
                Address: group.Key,
                DisplayName: "Steam Controller",
                ProductId: firstEndpoint.ProductId,
                ModelKey: $"USB\\VID_28DE&PID_{firstEndpoint.ProductId}\\STEAM_TRITON_PUCK",
                IsConnected: isConnected,
                BatteryStatus: batteryStatus,
                EndpointCount: orderedEndpoints.Count));
        }

        if (waitForBattery)
        {
            StoreCacheIfUseful(snapshots);
        }

        return snapshots;
    }

    private bool TryReadCachedSnapshots(out IReadOnlyList<SteamControllerTritonSnapshot> snapshots)
    {
        lock (_cacheLock)
        {
            if (_cachedSnapshots.Count > 0 &&
                _cachedSnapshots.Any(snapshot => snapshot.BatteryStatus is not null) &&
                DateTimeOffset.Now - _cachedAt <= CacheTtl)
            {
                snapshots = _cachedSnapshots;
                return true;
            }
        }

        snapshots = [];
        return false;
    }

    private void StoreCacheIfUseful(IReadOnlyList<SteamControllerTritonSnapshot> snapshots)
    {
        if (snapshots.Count == 0 || snapshots.All(snapshot => snapshot.BatteryStatus is null))
        {
            return;
        }

        lock (_cacheLock)
        {
            _cachedSnapshots = snapshots;
            _cachedAt = DateTimeOffset.Now;
        }
    }

    private static bool TryReadBatteryStatusSnapshot(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out SteamControllerBatteryStatus status)
    {
        status = null!;
        return HidGamepadAccess.TryReadInputReportSnapshot(handle, BatteryReportId, BatteryReportSize, out var snapshotReport, out _) &&
               GamepadBatteryParser.TryParseSteamTritonBatteryStatus(snapshotReport, out status);
    }

    internal static bool IsDisplayableControllerBattery(SteamControllerBatteryStatus status)
    {
        return status.IsCharging || status.BatteryPercent > 0;
    }

    internal static bool IsSuspiciousDockedFullBattery(SteamControllerBatteryStatus status)
    {
        return status.BatteryPercent == 100 &&
               status.IsCharging &&
               !status.IsChargeComplete;
    }

    internal static SteamControllerBatteryStatus ChoosePreferredBatteryStatus(
        SteamControllerBatteryStatus? current,
        SteamControllerBatteryStatus candidate)
    {
        if (current is null)
        {
            return candidate;
        }

        if (IsSuspiciousDockedFullBattery(current) && !IsSuspiciousDockedFullBattery(candidate))
        {
            return candidate;
        }

        return current;
    }

    internal static bool ShouldExposeController(
        SteamControllerBatteryStatus? displayableBatteryStatus,
        bool sawAnyBatteryStatus,
        bool sawConnectedSignal,
        bool sawDisconnectedSignal)
    {
        if (displayableBatteryStatus is not null)
        {
            return true;
        }

        if (sawAnyBatteryStatus)
        {
            return false;
        }

        if (sawDisconnectedSignal)
        {
            return false;
        }

        return sawConnectedSignal;
    }

    private static EndpointReadResult ReadEndpointReports(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        bool waitForBattery,
        CancellationToken cancellationToken)
    {
        using var session = new HidInputStreamSession(handle);
        if (!session.IsAvailable)
        {
            return new EndpointReadResult(TritonConnectionSignal.Unknown, BatteryStatus: null);
        }

        var signal = TritonConnectionSignal.Unknown;
        SteamControllerBatteryStatus? bestBatteryStatus = null;
        var discoveryDeadline = DateTime.UtcNow.AddMilliseconds(waitForBattery ? EndpointDiscoveryTimeoutMs : QuickReadTimeoutMs);
        while (DateTime.UtcNow < discoveryDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingMs = Math.Max(30, (int)Math.Ceiling((discoveryDeadline - DateTime.UtcNow).TotalMilliseconds));
            if (!session.TryReadReport(0x00, AnyReportSize, Math.Min(QuickReadTimeoutMs, remainingMs), out var frame, out _))
            {
                continue;
            }

            if (GamepadBatteryParser.TryParseSteamTritonBatteryStatus(frame.Data, out var status))
            {
                signal = TritonConnectionSignal.Connected;
                bestBatteryStatus = ChoosePreferredBatteryStatus(bestBatteryStatus, status);
                if (!IsSuspiciousDockedFullBattery(status))
                {
                    return new EndpointReadResult(TritonConnectionSignal.Connected, status);
                }

                continue;
            }

            if (GamepadBatteryParser.TryParseSteamTritonWirelessConnected(frame.Data, out var connected))
            {
                signal = connected ? TritonConnectionSignal.Connected : TritonConnectionSignal.Disconnected;
                continue;
            }

            if (frame.Data.Length > 0 && frame.Data[0] is 0x42 or 0x45)
            {
                signal = TritonConnectionSignal.Connected;
            }

            if (!waitForBattery && signal != TritonConnectionSignal.Unknown)
            {
                break;
            }
        }

        if (waitForBattery && signal == TritonConnectionSignal.Connected)
        {
            var batteryDeadline = DateTime.UtcNow.AddMilliseconds(ActiveBatteryReadTimeoutMs);
            while (DateTime.UtcNow < batteryDeadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remainingMs = Math.Max(30, (int)Math.Ceiling((batteryDeadline - DateTime.UtcNow).TotalMilliseconds));
                if (!session.TryReadReport(0x00, AnyReportSize, Math.Min(QuickReadTimeoutMs, remainingMs), out var frame, out _))
                {
                    continue;
                }

                if (GamepadBatteryParser.TryParseSteamTritonBatteryStatus(frame.Data, out var status))
                {
                    bestBatteryStatus = ChoosePreferredBatteryStatus(bestBatteryStatus, status);
                    if (!IsSuspiciousDockedFullBattery(status))
                    {
                        return new EndpointReadResult(TritonConnectionSignal.Connected, status);
                    }

                    continue;
                }

                if (GamepadBatteryParser.TryParseSteamTritonWirelessConnected(frame.Data, out var connected))
                {
                    signal = connected ? TritonConnectionSignal.Connected : TritonConnectionSignal.Disconnected;
                    if (!connected)
                    {
                        break;
                    }
                }
            }
        }

        return new EndpointReadResult(signal, bestBatteryStatus);
    }

    private enum TritonConnectionSignal
    {
        Unknown,
        Connected,
        Disconnected
    }

    private sealed record EndpointReadResult(
        TritonConnectionSignal ConnectionSignal,
        SteamControllerBatteryStatus? BatteryStatus);
}

public sealed record SteamControllerTritonSnapshot(
    string DeviceId,
    string Address,
    string DisplayName,
    string ProductId,
    string ModelKey,
    bool IsConnected,
    SteamControllerBatteryStatus? BatteryStatus,
    int EndpointCount);
