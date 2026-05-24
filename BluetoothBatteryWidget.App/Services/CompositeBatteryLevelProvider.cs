using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BluetoothBatteryWidget.App.Services;

public sealed class CompositeBatteryLevelProvider : IBatteryLevelProvider
{
    private static readonly TimeSpan FastProviderTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StandardProviderTimeout = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan SlowProviderTimeout = TimeSpan.FromSeconds(9);
    private static readonly string ProviderTraceLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "provider-traces.jsonl");
    private static readonly string SteamTritonTraceLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "steam-triton-traces.jsonl");
    private static readonly string ProcessPath = Environment.ProcessPath ?? string.Empty;
    private static readonly string BuildStamp = ResolveBuildStamp();
    private static readonly object SteamTritonTraceStateSync = new();
    private static string? LastSteamTritonTraceFingerprint;
    private const string SteamTritonDockedFullPendingReason = "steam_triton_docked_full_pending";
    private const string SteamTritonRecentNonFullHoldReason = "steam_triton_recent_nonfull_hold";
    private const string SteamTritonVoltageEstimatedChargingReason = "steam_triton_voltage_estimated_charging";
    private const string SteamTritonChargeCompleteLatchedReason = "steam_triton_charge_complete_latched";
    private const string SteamControllerBluetoothChargeCompleteLatchedReason = "steam_controller_bluetooth_charge_complete_latched";
    private const string SteamTritonPuckModelMarker = "STEAM_TRITON_PUCK";

    private readonly SetupApiBatteryLevelProvider _setupApiProvider;
    private readonly GameInputBatteryProvider _gameInputProvider;
    private readonly LearnedHidBatteryLevelProvider _learnedProvider;
    private readonly SonyHidBatteryLevelProvider _sonyHidProvider;
    private readonly XInputBatteryLevelProvider _xInputProvider;
    private readonly HidFeatureBatteryProvider _hidFeatureProvider;
    private readonly BleBatteryServiceProvider _bleProvider;
    private readonly SteamControllerTritonBatteryProvider _steamTritonProvider;
    private readonly BatteryEvidenceResolver _evidenceResolver;

    public CompositeBatteryLevelProvider(
        SetupApiBatteryLevelProvider setupApiProvider,
        GameInputBatteryProvider gameInputProvider,
        LearnedHidBatteryLevelProvider learnedProvider,
        SonyHidBatteryLevelProvider sonyHidProvider,
        XInputBatteryLevelProvider xInputProvider,
        HidFeatureBatteryProvider hidFeatureProvider,
        BleBatteryServiceProvider bleProvider,
        SteamControllerTritonBatteryProvider steamTritonProvider,
        BatteryEvidenceResolver evidenceResolver)
    {
        _setupApiProvider = setupApiProvider;
        _gameInputProvider = gameInputProvider;
        _learnedProvider = learnedProvider;
        _sonyHidProvider = sonyHidProvider;
        _xInputProvider = xInputProvider;
        _hidFeatureProvider = hidFeatureProvider;
        _bleProvider = bleProvider;
        _steamTritonProvider = steamTritonProvider;
        _evidenceResolver = evidenceResolver;
    }

    public async Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        var setupTask = RunProviderSafelyAsync(
            "setupApi",
            FastProviderTimeout,
            token => _setupApiProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);
        var gameInputTask = RunProviderSafelyAsync(
            "gameInput",
            FastProviderTimeout,
            token => _gameInputProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);
        var learnedTask = RunProviderSafelyAsync(
            "learnedHid",
            SlowProviderTimeout,
            token => _learnedProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);
        var xInputTask = RunProviderSafelyAsync(
            "xInput",
            FastProviderTimeout,
            token => _xInputProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);
        var sonyTask = RunProviderSafelyAsync(
            "sonyHid",
            StandardProviderTimeout,
            token => _sonyHidProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);
        var hidFeatureTask = RunProviderSafelyAsync(
            "hidFeature",
            StandardProviderTimeout,
            token => _hidFeatureProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);
        var bleTask = RunProviderSafelyAsync(
            "bleGatt",
            SlowProviderTimeout,
            token => _bleProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);
        var steamTritonTask = RunProviderSafelyAsync(
            "steamTriton",
            SlowProviderTimeout,
            token => _steamTritonProvider.GetBatteryLevelsAsync(connectedDevices, token),
            cancellationToken);

        await Task.WhenAll(setupTask, gameInputTask, learnedTask, xInputTask, sonyTask, hidFeatureTask, bleTask, steamTritonTask).ConfigureAwait(false);

        var setupResult = await setupTask.ConfigureAwait(false);
        var gameInputResult = await gameInputTask.ConfigureAwait(false);
        var learnedResult = await learnedTask.ConfigureAwait(false);
        var xInputResult = await xInputTask.ConfigureAwait(false);
        var sonyResult = await sonyTask.ConfigureAwait(false);
        var hidFeatureResult = await hidFeatureTask.ConfigureAwait(false);
        var bleResult = await bleTask.ConfigureAwait(false);
        var steamTritonResult = await steamTritonTask.ConfigureAwait(false);

        var timeoutHits = new[]
        {
            setupResult,
            gameInputResult,
            learnedResult,
            xInputResult,
            sonyResult,
            hidFeatureResult,
            bleResult,
            steamTritonResult
        }
            .Where(result => result.TimedOut)
            .Select(result => result.ProviderName)
            .ToList();
        if (timeoutHits.Count > 0)
        {
            AppendProviderTrace(
                providerTimeoutHit: timeoutHits,
                connectedCount: connectedDevices.Count);
        }

        var setupReadings = setupResult.Readings;
        var gameInputReadings = gameInputResult.Readings;
        var learnedReadings = learnedResult.Readings;
        var xInputReadings = xInputResult.Readings;
        var sonyReadings = sonyResult.Readings;
        var hidFeatureReadings = hidFeatureResult.Readings;
        var bleReadings = bleResult.Readings;
        var steamTritonReadings = steamTritonResult.Readings;

        var allCandidates = new List<PnpBatteryReading>(
            setupReadings.Count +
            steamTritonReadings.Count +
            bleReadings.Count +
            hidFeatureReadings.Count +
            learnedReadings.Count +
            sonyReadings.Count +
            xInputReadings.Count +
            gameInputReadings.Count);

        allCandidates.AddRange(setupReadings);
        allCandidates.AddRange(steamTritonReadings);
        allCandidates.AddRange(bleReadings);
        allCandidates.AddRange(hidFeatureReadings);
        allCandidates.AddRange(learnedReadings);
        allCandidates.AddRange(sonyReadings);
        allCandidates.AddRange(xInputReadings);
        allCandidates.AddRange(gameInputReadings);

        var resolved = _evidenceResolver.ResolveAndRecord(allCandidates, DateTimeOffset.Now);
        AppendSteamTritonTraceIfUseful(allCandidates, resolved);
        if (resolved.Count > 0)
        {
            return resolved;
        }

        return BatteryReadingMerger.MergeByAddress(
            setupReadings,
            steamTritonReadings,
            bleReadings,
            hidFeatureReadings,
            learnedReadings,
            sonyReadings,
            xInputReadings,
            gameInputReadings);
    }

    internal static async Task<ProviderExecutionResult> RunProviderSafelyAsync(
        string providerName,
        TimeSpan timeout,
        Func<CancellationToken, Task<IReadOnlyList<PnpBatteryReading>>> provider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var providerTask = provider(linkedCts.Token);

        try
        {
            var readings = await providerTask
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            return new ProviderExecutionResult(providerName, readings, TimedOut: false);
        }
        catch (TimeoutException)
        {
            linkedCts.Cancel();
            var partialReadings = await TryAwaitCanceledProviderResultAsync(providerTask).ConfigureAwait(false);
            return new ProviderExecutionResult(providerName, partialReadings ?? [], TimedOut: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var partialReadings = await TryAwaitCanceledProviderResultAsync(providerTask).ConfigureAwait(false);
            return new ProviderExecutionResult(providerName, partialReadings ?? [], TimedOut: true);
        }
        catch
        {
            return new ProviderExecutionResult(providerName, [], TimedOut: false);
        }
    }

    private static async Task<IReadOnlyList<PnpBatteryReading>?> TryAwaitCanceledProviderResultAsync(
        Task<IReadOnlyList<PnpBatteryReading>> providerTask)
    {
        try
        {
            var completed = await Task.WhenAny(providerTask, Task.Delay(250)).ConfigureAwait(false);
            if (completed != providerTask)
            {
                return null;
            }

            return await providerTask.ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static void AppendProviderTrace(
        IReadOnlyList<string> providerTimeoutHit,
        int connectedCount)
    {
        try
        {
            var directory = Path.GetDirectoryName(ProviderTraceLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = new
            {
                ts = DateTimeOffset.Now,
                processPath = ProcessPath,
                buildStamp = BuildStamp,
                providerTimeoutHit,
                connectedCount
            };
            File.AppendAllText(
                ProviderTraceLogPath,
                JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // Ignore trace logging failures.
        }
    }

    internal static bool ShouldTraceSteamTritonReadings(
        IReadOnlyList<PnpBatteryReading> rawCandidates,
        IReadOnlyList<PnpBatteryReading> resolvedReadings)
    {
        return rawCandidates.Any(IsSuspiciousSteamTritonRawReading) ||
               resolvedReadings.Any(IsGuardedSteamTritonResolvedReading);
    }

    private static bool IsSuspiciousSteamTritonRawReading(PnpBatteryReading reading)
    {
        return reading.SourceKind == BatterySourceKind.SteamHid &&
               reading.BatteryPercent == 100 &&
               reading.IsCharging;
    }

    private static bool IsGuardedSteamTritonResolvedReading(PnpBatteryReading reading)
    {
        return string.Equals(reading.ReasonCode, SteamTritonDockedFullPendingReason, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reading.ReasonCode, SteamTritonRecentNonFullHoldReason, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reading.ReasonCode, SteamTritonVoltageEstimatedChargingReason, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reading.ReasonCode, SteamTritonChargeCompleteLatchedReason, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reading.ReasonCode, SteamControllerBluetoothChargeCompleteLatchedReason, StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildSteamTritonTraceFingerprint(
        IReadOnlyList<PnpBatteryReading> rawCandidates,
        IReadOnlyList<PnpBatteryReading> resolvedReadings)
    {
        var relevantAddresses = GetSteamTritonRelevantAddresses(rawCandidates, resolvedReadings);
        if (relevantAddresses.Count == 0)
        {
            return string.Empty;
        }

        var rows = rawCandidates
            .Where(reading => relevantAddresses.Contains(AddressNormalizer.NormalizeAddress(reading.Address)))
            .Select(reading => ToTraceFingerprintRow("raw", reading))
            .Concat(resolvedReadings
                .Where(reading => relevantAddresses.Contains(AddressNormalizer.NormalizeAddress(reading.Address)))
                .Select(reading => ToTraceFingerprintRow("resolved", reading)))
            .Order(StringComparer.Ordinal)
            .ToList();

        return string.Join(Environment.NewLine, rows);
    }

    internal static bool HasSteamTritonTraceStateChanged(string? previousFingerprint, string currentFingerprint)
    {
        return !string.IsNullOrWhiteSpace(currentFingerprint) &&
               !string.Equals(previousFingerprint ?? string.Empty, currentFingerprint, StringComparison.Ordinal);
    }

    private static void AppendSteamTritonTraceIfUseful(
        IReadOnlyList<PnpBatteryReading> rawCandidates,
        IReadOnlyList<PnpBatteryReading> resolvedReadings)
    {
        var fingerprint = BuildSteamTritonTraceFingerprint(rawCandidates, resolvedReadings);
        bool stateChanged;
        lock (SteamTritonTraceStateSync)
        {
            stateChanged = HasSteamTritonTraceStateChanged(LastSteamTritonTraceFingerprint, fingerprint);
            if (!ShouldTraceSteamTritonReadings(rawCandidates, resolvedReadings) && !stateChanged)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                LastSteamTritonTraceFingerprint = fingerprint;
            }
        }

        var relevantAddresses = GetSteamTritonRelevantAddresses(rawCandidates, resolvedReadings);
        if (relevantAddresses.Count == 0)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(SteamTritonTraceLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = new
            {
                ts = DateTimeOffset.Now,
                processPath = ProcessPath,
                buildStamp = BuildStamp,
                traceReason = ResolveSteamTritonTraceReason(rawCandidates, resolvedReadings, stateChanged),
                raw = rawCandidates
                    .Where(reading => relevantAddresses.Contains(AddressNormalizer.NormalizeAddress(reading.Address)))
                    .Select(ToTraceShape)
                    .ToList(),
                resolved = resolvedReadings
                    .Where(reading => relevantAddresses.Contains(AddressNormalizer.NormalizeAddress(reading.Address)))
                    .Select(ToTraceShape)
                    .ToList()
            };

            File.AppendAllText(
                SteamTritonTraceLogPath,
                JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // Ignore trace logging failures.
        }
    }

    private static HashSet<string> GetSteamTritonRelevantAddresses(
        IReadOnlyList<PnpBatteryReading> rawCandidates,
        IReadOnlyList<PnpBatteryReading> resolvedReadings)
    {
        return rawCandidates
            .Concat(resolvedReadings)
            .Where(reading => IsSteamTritonTraceReading(reading) || IsGuardedSteamTritonResolvedReading(reading))
            .Select(reading => AddressNormalizer.NormalizeAddress(reading.Address))
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSteamTritonTraceReading(PnpBatteryReading reading)
    {
        return reading.SourceKind == BatterySourceKind.SteamHid ||
               reading.InstanceId.StartsWith("steam-triton:", StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(reading.ModelKey) &&
                reading.ModelKey.Contains(SteamTritonPuckModelMarker, StringComparison.OrdinalIgnoreCase)) ||
               IsSteamControllerBluetoothTraceReading(reading);
    }

    private static bool IsSteamControllerBluetoothTraceReading(PnpBatteryReading reading)
    {
        return ContainsSteamControllerBluetoothVidPid(reading.ModelKey) ||
               ContainsSteamControllerBluetoothVidPid(reading.InstanceId) ||
               reading.DisplayName.Contains("Steam Ctrl (BT)", StringComparison.OrdinalIgnoreCase) ||
               (reading.DisplayName.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) &&
                reading.DisplayName.Contains("(BT)", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSteamControllerBluetoothVidPid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hasSteamVendor = value.Contains("VID_28DE", StringComparison.OrdinalIgnoreCase) ||
                             value.Contains("VID&0228DE", StringComparison.OrdinalIgnoreCase) ||
                             value.Contains("VID=28DE", StringComparison.OrdinalIgnoreCase) ||
                             value.Contains("VID=0228DE", StringComparison.OrdinalIgnoreCase);
        var hasBluetoothPid = value.Contains("PID_1303", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("PID&1303", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("PID=1303", StringComparison.OrdinalIgnoreCase);

        return hasSteamVendor && hasBluetoothPid;
    }

    private static string ResolveSteamTritonTraceReason(
        IReadOnlyList<PnpBatteryReading> rawCandidates,
        IReadOnlyList<PnpBatteryReading> resolvedReadings,
        bool stateChanged)
    {
        var hasSuspiciousRaw = rawCandidates.Any(IsSuspiciousSteamTritonRawReading);
        var hasGuardedResolved = resolvedReadings.Any(IsGuardedSteamTritonResolvedReading);
        if (hasSuspiciousRaw && stateChanged)
        {
            return "steam_triton_suspicious_full_state_changed";
        }

        if (hasSuspiciousRaw)
        {
            return "steam_triton_suspicious_full";
        }

        if (hasGuardedResolved && stateChanged)
        {
            return "steam_triton_guarded_state_changed";
        }

        if (hasGuardedResolved)
        {
            return "steam_triton_guarded";
        }

        return "steam_triton_state_changed";
    }

    private static string ToTraceFingerprintRow(string stage, PnpBatteryReading reading)
    {
        return string.Join(
            "|",
            stage,
            AddressNormalizer.NormalizeAddress(reading.Address),
            reading.SourceKind.ToString(),
            reading.BatteryPercent?.ToString(CultureInfo.InvariantCulture) ?? "null",
            reading.RawMetric?.ToString("R", CultureInfo.InvariantCulture) ?? "null",
            reading.IsCharging.ToString(CultureInfo.InvariantCulture),
            reading.IsChargeComplete.ToString(CultureInfo.InvariantCulture),
            reading.IsBatterySuspect.ToString(CultureInfo.InvariantCulture),
            reading.BatteryConfidence.ToString(),
            NormalizeTraceText(reading.ReasonCode),
            NormalizeTraceText(reading.ActiveSource),
            NormalizeTraceText(reading.PathType),
            NormalizeTraceText(reading.ModelKey));
    }

    private static string NormalizeTraceText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static object ToTraceShape(PnpBatteryReading reading)
    {
        return new
        {
            address = AddressNormalizer.NormalizeAddress(reading.Address),
            sourceKind = reading.SourceKind.ToString(),
            percent = reading.BatteryPercent,
            rawMetric = reading.RawMetric,
            isCharging = reading.IsCharging,
            isChargeComplete = reading.IsChargeComplete,
            isBatterySuspect = reading.IsBatterySuspect,
            confidence = reading.BatteryConfidence.ToString(),
            reasonCode = reading.ReasonCode,
            activeSource = reading.ActiveSource,
            pathType = reading.PathType,
            modelKey = reading.ModelKey
        };
    }

    private static string ResolveBuildStamp()
    {
        try
        {
            var assemblyVersion = typeof(CompositeBatteryLevelProvider).Assembly.GetName().Version?.ToString();
            if (!string.IsNullOrWhiteSpace(assemblyVersion))
            {
                return assemblyVersion;
            }
        }
        catch
        {
            // Ignore and continue to fallback.
        }

        return "unknown";
    }

    internal readonly record struct ProviderExecutionResult(
        string ProviderName,
        IReadOnlyList<PnpBatteryReading> Readings,
        bool TimedOut);
}
