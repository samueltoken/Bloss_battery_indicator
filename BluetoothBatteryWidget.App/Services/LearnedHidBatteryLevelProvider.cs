using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class LearnedHidBatteryLevelProvider
{
    private static readonly int[] ReadTimeouts = [90, 130];
    private const int ReadRetryCount = 1;
    private const int MaxAttempts = 2;
    private const int RevalidationSampleCount = 2;
    private const int RevalidationMinSuccessCount = 1;
    private const int RevalidationSpreadThreshold = 18;
    private const int RevalidationFailureToNaThreshold = 2;
    private const int KeepAliveFeatureRetryCount = 1;

    private readonly GamepadProfileStore _profileStore;

    public LearnedHidBatteryLevelProvider(GamepadProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    public Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<PnpBatteryReading>>(() =>
        {
            var connectedByAddress = connectedDevices
                .Where(device => device.IsConnected)
                .Select(device => new
                {
                    NormalizedAddress = AddressNormalizer.NormalizeAddress(device.Address),
                    Device = device
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.NormalizedAddress))
                .GroupBy(entry => entry.NormalizedAddress, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Device, StringComparer.OrdinalIgnoreCase);

            if (connectedByAddress.Count == 0)
            {
                return [];
            }

            var byAddress = new Dictionary<string, PnpBatteryReading>(StringComparer.OrdinalIgnoreCase);
            var endpoints = HidGamepadAccess.EnumerateBluetoothEndpoints(addressFilter: null, cancellationToken);
            try
            {
                foreach (var endpoint in endpoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var normalizedAddress = AddressNormalizer.NormalizeAddress(endpoint.Address);
                    if (string.IsNullOrWhiteSpace(normalizedAddress) ||
                        !connectedByAddress.TryGetValue(normalizedAddress, out var connected))
                    {
                        continue;
                    }

                    int? batteryPercent = null;
                    var confidence = BatteryConfidence.Estimated;
                    var isSuspect = false;
                    using var handle = HidGamepadAccess.OpenHandle(endpoint.DevicePath);
                    if (handle.IsInvalid)
                    {
                        continue;
                    }

                    var transportVendorId = endpoint.VendorId;
                    var transportProductId = endpoint.ProductId;
                    if (HidGamepadAccess.TryGetDeviceAttributes(handle, out var attrVid, out var attrPid))
                    {
                        transportVendorId = attrVid;
                        transportProductId = attrPid;
                    }

                    var endpointSignature = BatteryModelKeyResolver.BuildEndpointSignature(
                        endpoint.InstanceId,
                        endpoint.DevicePath);
                    var identityKey = BatteryModelKeyResolver.ResolveIdentityKey(
                        endpoint.VendorId,
                        endpoint.ProductId,
                        transportVendorId,
                        transportProductId,
                        normalizedAddress,
                        connected.DisplayName,
                        endpointSignature);

                    if (!TryResolveProfile(
                            identityKey,
                            endpoint.VendorId,
                            endpoint.ProductId,
                            transportVendorId,
                            transportProductId,
                            out var profile))
                    {
                        continue;
                    }

                    var outcome = TryRevalidateProfile(
                        handle,
                        profile,
                        endpoint,
                        connected,
                        normalizedAddress,
                        transportVendorId,
                        transportProductId);

                    if (outcome.Success)
                    {
                        _profileStore.RegisterRevalidationSuccess(profile, DateTimeOffset.Now);
                        if (outcome.LowEvidence)
                        {
                            batteryPercent = null;
                            confidence = BatteryConfidence.Estimated;
                            isSuspect = true;
                        }
                        else
                        {
                            batteryPercent = outcome.BatteryPercent;
                            confidence = profile.Confidence;
                            isSuspect = profile.Confidence != BatteryConfidence.Confirmed;
                        }
                    }
                    else
                    {
                        var transition = _profileStore.RegisterRevalidationFailure(profile, outcome.FailureKind, DateTimeOffset.Now);
                        if (!ShouldEmitNaAfterRevalidationFailure(outcome.FailureKind, transition.Health))
                        {
                            continue;
                        }

                        if (!byAddress.TryGetValue(normalizedAddress, out var existingEntry) ||
                            existingEntry.BatteryPercent is null)
                        {
                            var displayName = string.IsNullOrWhiteSpace(connected.DisplayName)
                                ? endpoint.DisplayName
                                : connected.DisplayName;

                            byAddress[normalizedAddress] = new PnpBatteryReading(
                                InstanceId: endpoint.InstanceId,
                                Address: normalizedAddress,
                                DisplayName: displayName,
                                BatteryPercent: null,
                                BatteryConfidence: BatteryConfidence.Estimated,
                                SourceKind: BatterySourceKind.LearnedHid,
                                RawMetric: null,
                                ModelKey: BatteryModelKeyResolver.ResolveNormalizedModelKey(
                                    endpoint.VendorId,
                                    endpoint.ProductId,
                                    transportVendorId,
                                    transportProductId,
                                    normalizedAddress,
                                    displayName),
                                IsBatterySuspect: true);
                        }

                        continue;
                    }

                    if (!byAddress.TryGetValue(normalizedAddress, out var existing) ||
                        (existing.BatteryPercent is null && batteryPercent is not null))
                    {
                        var displayName = string.IsNullOrWhiteSpace(connected.DisplayName)
                            ? endpoint.DisplayName
                            : connected.DisplayName;

                        byAddress[normalizedAddress] = new PnpBatteryReading(
                            InstanceId: endpoint.InstanceId,
                            Address: normalizedAddress,
                            DisplayName: displayName,
                            BatteryPercent: batteryPercent,
                            BatteryConfidence: confidence,
                            SourceKind: BatterySourceKind.LearnedHid,
                            RawMetric: null,
                            ModelKey: BatteryModelKeyResolver.ResolveNormalizedModelKey(
                                endpoint.VendorId,
                                endpoint.ProductId,
                                transportVendorId,
                                transportProductId,
                                normalizedAddress,
                                displayName),
                            IsBatterySuspect: isSuspect);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Return partial results gathered before cancellation.
            }

            return byAddress.Values.ToList();
        }, cancellationToken);
    }

    private bool TryResolveProfile(
        string identityKey,
        string vendorId,
        string productId,
        string? transportVendorId,
        string? transportProductId,
        out GamepadBatteryProfile profile)
    {
        if (_profileStore.TryGetBestForIdentity(identityKey, vendorId, productId, out profile))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(transportVendorId) &&
            !string.IsNullOrWhiteSpace(transportProductId) &&
            _profileStore.TryGetBestForIdentity(identityKey, transportVendorId, transportProductId, out profile))
        {
            return true;
        }

        var stableIdentityKey = NormalizeIdentityForEndpointDrift(identityKey);
        if (string.IsNullOrWhiteSpace(stableIdentityKey) ||
            string.Equals(stableIdentityKey, "IDENTITY_UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            profile = null!;
            return false;
        }

        var candidates = _profileStore.LoadAll()
            .Where(item =>
                IsSameVendorProduct(item, vendorId, productId) ||
                IsSameVendorProduct(item, transportVendorId, transportProductId))
            .Where(item =>
                string.Equals(
                    NormalizeIdentityForEndpointDrift(item.IdentityKey),
                    stableIdentityKey,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Score)
            .ToList();
        if (candidates.Count == 0)
        {
            profile = null!;
            return false;
        }

        profile = candidates[0];
        return true;
    }

    internal static string NormalizeIdentityForEndpointDrift(string? identityKey)
    {
        if (string.IsNullOrWhiteSpace(identityKey))
        {
            return "IDENTITY_UNKNOWN";
        }

        var parts = identityKey
            .Trim()
            .ToUpperInvariant()
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith("EP=", StringComparison.Ordinal))
            .ToArray();
        if (parts.Length == 0)
        {
            return "IDENTITY_UNKNOWN";
        }

        return string.Join("|", parts);
    }

    internal static bool ShouldEmitNaAfterRevalidationFailure(
        RevalidationFailureKind failureKind,
        GamepadProfileHealthState healthState)
    {
        return failureKind switch
        {
            RevalidationFailureKind.NoSignal => healthState.NoSignalStrike >= RevalidationFailureToNaThreshold,
            RevalidationFailureKind.WeakSignal => healthState.WeakSignalStrike >= RevalidationFailureToNaThreshold,
            RevalidationFailureKind.SpreadOutlier => healthState.WeakSignalStrike >= RevalidationFailureToNaThreshold,
            RevalidationFailureKind.DecodeMismatch => healthState.MismatchStrike >= RevalidationFailureToNaThreshold,
            _ => false
        };
    }

    private static bool IsSameVendorProduct(
        GamepadBatteryProfile profile,
        string? vendorId,
        string? productId)
    {
        if (string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(productId))
        {
            return false;
        }

        return string.Equals(profile.VendorId, vendorId.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(profile.ProductId, productId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadForProfile(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        GamepadBatteryProfile profile,
        HidGamepadEndpoint endpoint,
        ConnectedBluetoothDevice connectedDevice,
        string normalizedAddress,
        string? transportVendorId,
        string? transportProductId,
        out byte[] report)
    {
        report = [];
        if (TryReadByInput(handle, profile, out report))
        {
            return true;
        }

        // Input report can go silent during idle on third-party pads. Kick keepalive and retry.
        SendKeepAlivePackets(
            handle,
            endpoint,
            connectedDevice,
            normalizedAddress,
            transportVendorId,
            transportProductId);
        if (TryReadByInput(handle, profile, out report))
        {
            return true;
        }

        return TryReadByFeature(handle, profile, out report);
    }

    private static bool TryReadByInput(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        GamepadBatteryProfile profile,
        out byte[] report)
    {
        report = [];
        var reportSizes = HidGamepadAccess.BuildProbeReportSizes(handle);
        using var streamContext = HidGamepadAccess.CreateStreamReadContext(handle);

        var attemptsLeft = MaxAttempts;
        foreach (var size in reportSizes)
        {
            foreach (var timeout in ReadTimeouts)
            {
                if (attemptsLeft <= 0)
                {
                    return false;
                }

                if (HidGamepadAccess.TryReadInputReport(
                        handle,
                        profile.ReportId,
                        Math.Max(size, profile.ReportLength),
                        out report,
                        timeout,
                        ReadRetryCount,
                        out var stats,
                        streamContext,
                        attemptsLeft))
                {
                    return true;
                }

                attemptsLeft = Math.Max(0, attemptsLeft - Math.Max(1, stats.AttemptCount));
                if (stats.HardFailCount > 0 && !streamContext.IsFallbackEnabled)
                {
                    break;
                }
            }
        }

        return false;
    }

    private static bool TryReadByFeature(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        GamepadBatteryProfile profile,
        out byte[] report)
    {
        report = [];
        var featureSizes = HidGamepadAccess.BuildProbeFeatureSizes(handle);
        foreach (var size in featureSizes)
        {
            if (HidGamepadAccess.TryReadFeatureReport(
                    handle,
                    profile.ReportId,
                    Math.Max(size, profile.ReportLength),
                    out report,
                    retryCount: KeepAliveFeatureRetryCount))
            {
                return true;
            }
        }

        return false;
    }

    private static void SendKeepAlivePackets(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        HidGamepadEndpoint endpoint,
        ConnectedBluetoothDevice connectedDevice,
        string normalizedAddress,
        string? transportVendorId,
        string? transportProductId)
    {
        var vendorId = !string.IsNullOrWhiteSpace(endpoint.VendorId)
            ? endpoint.VendorId
            : transportVendorId;
        var productId = !string.IsNullOrWhiteSpace(endpoint.ProductId)
            ? endpoint.ProductId
            : transportProductId;
        var endpointSignal = $"{endpoint.InstanceId} {endpoint.DevicePath}";
        var handshake = ThirdPartyHandshakeProfileCatalog.Resolve(
            vendorId,
            productId,
            connectedDevice.DisplayName,
            endpointSignal,
            normalizedAddress);
        foreach (var packet in handshake.InitPackets)
        {
            _ = HidGamepadAccess.TrySendOutputPacket(handle, packet.Payload);
            if (packet.DelayAfterMs > 0)
            {
                Thread.Sleep(packet.DelayAfterMs);
            }
        }
    }

    private static RevalidationOutcome TryRevalidateProfile(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        GamepadBatteryProfile profile,
        HidGamepadEndpoint endpoint,
        ConnectedBluetoothDevice connectedDevice,
        string normalizedAddress,
        string? transportVendorId,
        string? transportProductId)
    {
        var decodedSamples = new List<int>(RevalidationSampleCount);
        var readSampleCount = 0;
        var decodeFailureCount = 0;
        var xboxSuspiciousSampleCount = 0;

        for (var sampleIndex = 0; sampleIndex < RevalidationSampleCount; sampleIndex++)
        {
            if (!TryReadForProfile(
                    handle,
                    profile,
                    endpoint,
                    connectedDevice,
                    normalizedAddress,
                    transportVendorId,
                    transportProductId,
                    out var report))
            {
                continue;
            }

            readSampleCount++;

            if (!GamepadProfileDecoder.TryDecode(profile, report, out var decoded))
            {
                decodeFailureCount++;
                continue;
            }

            if (string.Equals(profile.Decoder, GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags, StringComparison.Ordinal) &&
                XboxBluetoothBatteryDecoder.TryDecode(profile.ReportId, report, out var xboxDecoded, out var onUsb) &&
                xboxDecoded <= 10 &&
                onUsb)
            {
                xboxSuspiciousSampleCount++;
            }

            decodedSamples.Add(decoded);
        }

        if (decodedSamples.Count < RevalidationMinSuccessCount)
        {
            if (readSampleCount == 0)
            {
                return new RevalidationOutcome(false, 0, false, RevalidationFailureKind.NoSignal);
            }

            if (decodeFailureCount >= RevalidationMinSuccessCount)
            {
                return new RevalidationOutcome(false, 0, false, RevalidationFailureKind.DecodeMismatch);
            }

            return new RevalidationOutcome(false, 0, false, RevalidationFailureKind.WeakSignal);
        }

        var min = decodedSamples.Min();
        var max = decodedSamples.Max();
        if (max - min > RevalidationSpreadThreshold)
        {
            return new RevalidationOutcome(false, 0, false, RevalidationFailureKind.SpreadOutlier);
        }

        var isXboxFlags = string.Equals(
            profile.Decoder,
            GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags,
            StringComparison.Ordinal);
        var allLowSamples = decodedSamples.All(sample => sample <= 10);
        var weakEvidenceSamples = decodedSamples.Count <= RevalidationMinSuccessCount;
        var narrowSpread = max - min <= 2;
        if (isXboxFlags &&
            (xboxSuspiciousSampleCount >= RevalidationMinSuccessCount ||
             (allLowSamples && weakEvidenceSamples && narrowSpread)))
        {
            return new RevalidationOutcome(true, 0, true, RevalidationFailureKind.None);
        }

        var percent = (int)Math.Round(decodedSamples.Average(), MidpointRounding.AwayFromZero);
        return new RevalidationOutcome(true, percent, false, RevalidationFailureKind.None);
    }

    private readonly record struct RevalidationOutcome(
        bool Success,
        int BatteryPercent,
        bool LowEvidence,
        RevalidationFailureKind FailureKind);
}
