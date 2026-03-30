using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
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
    private static readonly string ProcessPath = Environment.ProcessPath ?? string.Empty;
    private static readonly string BuildStamp = ResolveBuildStamp();

    private readonly SetupApiBatteryLevelProvider _setupApiProvider;
    private readonly GameInputBatteryProvider _gameInputProvider;
    private readonly LearnedHidBatteryLevelProvider _learnedProvider;
    private readonly SonyHidBatteryLevelProvider _sonyHidProvider;
    private readonly XInputBatteryLevelProvider _xInputProvider;
    private readonly HidFeatureBatteryProvider _hidFeatureProvider;
    private readonly BleBatteryServiceProvider _bleProvider;
    private readonly BatteryEvidenceResolver _evidenceResolver;

    public CompositeBatteryLevelProvider(
        SetupApiBatteryLevelProvider setupApiProvider,
        GameInputBatteryProvider gameInputProvider,
        LearnedHidBatteryLevelProvider learnedProvider,
        SonyHidBatteryLevelProvider sonyHidProvider,
        XInputBatteryLevelProvider xInputProvider,
        HidFeatureBatteryProvider hidFeatureProvider,
        BleBatteryServiceProvider bleProvider,
        BatteryEvidenceResolver evidenceResolver)
    {
        _setupApiProvider = setupApiProvider;
        _gameInputProvider = gameInputProvider;
        _learnedProvider = learnedProvider;
        _sonyHidProvider = sonyHidProvider;
        _xInputProvider = xInputProvider;
        _hidFeatureProvider = hidFeatureProvider;
        _bleProvider = bleProvider;
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

        await Task.WhenAll(setupTask, gameInputTask, learnedTask, xInputTask, sonyTask, hidFeatureTask, bleTask).ConfigureAwait(false);

        var setupResult = await setupTask.ConfigureAwait(false);
        var gameInputResult = await gameInputTask.ConfigureAwait(false);
        var learnedResult = await learnedTask.ConfigureAwait(false);
        var xInputResult = await xInputTask.ConfigureAwait(false);
        var sonyResult = await sonyTask.ConfigureAwait(false);
        var hidFeatureResult = await hidFeatureTask.ConfigureAwait(false);
        var bleResult = await bleTask.ConfigureAwait(false);

        var timeoutHits = new[]
        {
            setupResult,
            gameInputResult,
            learnedResult,
            xInputResult,
            sonyResult,
            hidFeatureResult,
            bleResult
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

        var allCandidates = new List<PnpBatteryReading>(
            setupReadings.Count +
            bleReadings.Count +
            hidFeatureReadings.Count +
            learnedReadings.Count +
            sonyReadings.Count +
            xInputReadings.Count +
            gameInputReadings.Count);

        allCandidates.AddRange(setupReadings);
        allCandidates.AddRange(bleReadings);
        allCandidates.AddRange(hidFeatureReadings);
        allCandidates.AddRange(learnedReadings);
        allCandidates.AddRange(sonyReadings);
        allCandidates.AddRange(xInputReadings);
        allCandidates.AddRange(gameInputReadings);

        var resolved = _evidenceResolver.ResolveAndRecord(allCandidates, DateTimeOffset.Now);
        if (resolved.Count > 0)
        {
            return resolved;
        }

        return BatteryReadingMerger.MergeByAddress(
            setupReadings,
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
