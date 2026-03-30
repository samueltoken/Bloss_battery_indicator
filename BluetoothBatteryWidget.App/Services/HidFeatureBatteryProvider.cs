using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class HidFeatureBatteryProvider
{
    private static readonly byte[] DefaultFeatureReportIds = [0x02, 0x03, 0x05, 0x11, 0x21, 0x31, 0x81, 0x82];
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(45);
    private const int MaxFeatureReadAttemptsPerEndpoint = 12;
    private const int MaxFallbackReadAttemptsPerEndpoint = 6;
    private static readonly object CooldownSync = new();
    private static readonly Dictionary<string, DateTimeOffset> CooldownByEndpoint = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<PnpBatteryReading>>(() =>
        {
            var connectedGamepads = connectedDevices
                .Where(device => device.IsConnected)
                .Select(device => new
                {
                    Device = device,
                    Address = AddressNormalizer.NormalizeAddress(device.Address),
                    Category = DeviceCategoryClassifier.Classify(device.DisplayName, device.CategoryHint)
                })
                .Where(item =>
                    item.Category == DeviceCategory.Gamepad &&
                    !string.IsNullOrWhiteSpace(item.Address))
                .ToDictionary(item => item.Address, item => item.Device, StringComparer.OrdinalIgnoreCase);

            if (connectedGamepads.Count == 0)
            {
                return [];
            }

            var byAddress = new Dictionary<string, (PnpBatteryReading Reading, int Score)>(StringComparer.OrdinalIgnoreCase);
            var endpoints = HidGamepadAccess.EnumerateProbeEndpoints(
                addressFilter: null,
                HidEndpointDiscoveryStage.Strict,
                cancellationToken);

            try
            {
                foreach (var endpoint in endpoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var endpointAddress = AddressNormalizer.NormalizeAddress(endpoint.Address);
                    if (string.IsNullOrWhiteSpace(endpointAddress) || !connectedGamepads.TryGetValue(endpointAddress, out var connected))
                    {
                        continue;
                    }

                    var cooldownKey = $"{endpointAddress}|{endpoint.DevicePath}";
                    if (IsInCooldown(cooldownKey, DateTimeOffset.Now))
                    {
                        continue;
                    }

                    using var handle = HidGamepadAccess.OpenHandle(endpoint.DevicePath);
                    if (handle.IsInvalid)
                    {
                        RegisterFailure(cooldownKey, DateTimeOffset.Now);
                        continue;
                    }

                    var identityVendorId = endpoint.VendorId;
                    var identityProductId = endpoint.ProductId;
                    var transportVendorId = endpoint.VendorId;
                    var transportProductId = endpoint.ProductId;
                    if (HidGamepadAccess.TryGetDeviceAttributes(handle, out var attrVid, out var attrPid))
                    {
                        transportVendorId = attrVid;
                        transportProductId = attrPid;
                    }

                    var vendorId = !string.IsNullOrWhiteSpace(identityVendorId) ? identityVendorId : transportVendorId;
                    var productId = !string.IsNullOrWhiteSpace(identityProductId) ? identityProductId : transportProductId;

                    var endpointSignal = $"{endpoint.InstanceId} {endpoint.DevicePath}";
                    var profile = ThirdPartyHandshakeProfileCatalog.Resolve(
                        vendorId,
                        productId,
                        connected.DisplayName,
                        endpointSignal);
                    foreach (var packet in profile.InitPackets)
                    {
                        _ = HidGamepadAccess.TrySendOutputPacket(handle, packet.Payload);
                        if (packet.DelayAfterMs > 0)
                        {
                            Thread.Sleep(packet.DelayAfterMs);
                        }
                    }

                    var reports = ReadFeatureReports(handle, profile, cancellationToken);
                    if (reports.Count == 0)
                    {
                        RegisterFailure(cooldownKey, DateTimeOffset.Now);
                        continue;
                    }

                    var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);
                    var winner = selection.Winner;
                    if (winner is null || winner.Score < 55)
                    {
                        RegisterFailure(cooldownKey, DateTimeOffset.Now);
                        continue;
                    }

                    var confidence = winner.Score >= 70
                        ? BatteryConfidence.Confirmed
                        : BatteryConfidence.Estimated;
                    var modelKey = BatteryModelKeyResolver.ResolveNormalizedModelKey(
                        identityVendorId,
                        identityProductId,
                        transportVendorId,
                        transportProductId,
                        endpointAddress,
                        connected.DisplayName);
                    var rawMetric = TryResolveRawMetric(reports, winner);
                    var reading = new PnpBatteryReading(
                        InstanceId: endpoint.InstanceId,
                        Address: endpointAddress,
                        DisplayName: connected.DisplayName,
                        BatteryPercent: winner.BatteryPercent,
                        BatteryConfidence: confidence,
                        SourceKind: BatterySourceKind.HidFeature,
                        RawMetric: rawMetric,
                        ModelKey: modelKey,
                        SuggestCalibration: false,
                        ObservedAt: DateTimeOffset.Now);

                    if (!byAddress.TryGetValue(endpointAddress, out var existing) || winner.Score > existing.Score)
                    {
                        byAddress[endpointAddress] = (reading, winner.Score);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Return partial results gathered before cancellation.
            }

            return byAddress.Values.Select(item => item.Reading).ToList();
        }, cancellationToken);
    }

    private static Dictionary<byte, byte[]> ReadFeatureReports(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        ThirdPartyHandshakeProfile profile,
        CancellationToken cancellationToken)
    {
        var reportIds = profile.FeatureReportIds.Count > 0
            ? profile.FeatureReportIds
            : DefaultFeatureReportIds;
        var sizes = HidGamepadAccess.BuildProbeFeatureSizes(handle);
        var result = new Dictionary<byte, byte[]>();
        var attemptCount = 0;

        foreach (var reportId in reportIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attemptCount >= MaxFeatureReadAttemptsPerEndpoint)
            {
                break;
            }

            foreach (var size in sizes)
            {
                if (attemptCount >= MaxFeatureReadAttemptsPerEndpoint)
                {
                    break;
                }

                attemptCount++;
                if (HidGamepadAccess.TryReadFeatureReport(handle, reportId, Math.Max(size, profile.MinimumReportSize), out var report, retryCount: 1))
                {
                    result[reportId] = report;
                    break;
                }
            }
        }

        if (result.Count == 0)
        {
            var fallbackAttemptCount = 0;
            foreach (var fallbackReportId in DefaultFeatureReportIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (fallbackAttemptCount >= MaxFallbackReadAttemptsPerEndpoint)
                {
                    break;
                }

                foreach (var size in sizes)
                {
                    if (fallbackAttemptCount >= MaxFallbackReadAttemptsPerEndpoint)
                    {
                        break;
                    }

                    fallbackAttemptCount++;
                    if (HidGamepadAccess.TryReadFeatureReport(handle, fallbackReportId, size, out var report, retryCount: 0))
                    {
                        result[fallbackReportId] = report;
                        break;
                    }
                }
            }
        }

        return result;
    }

    private static double? TryResolveRawMetric(
        IReadOnlyDictionary<byte, byte[]> reports,
        GamepadBatteryCandidate winner)
    {
        if (!reports.TryGetValue(winner.ReportId, out var report))
        {
            return null;
        }

        if (winner.Offset < 0 || winner.Offset >= report.Length)
        {
            return null;
        }

        return report[winner.Offset];
    }

    private static bool IsInCooldown(string key, DateTimeOffset now)
    {
        lock (CooldownSync)
        {
            if (!CooldownByEndpoint.TryGetValue(key, out var until))
            {
                return false;
            }

            if (until > now)
            {
                return true;
            }

            CooldownByEndpoint.Remove(key);
            return false;
        }
    }

    private static void RegisterFailure(string key, DateTimeOffset now)
    {
        lock (CooldownSync)
        {
            CooldownByEndpoint[key] = now + FailureCooldown;
        }
    }
}
