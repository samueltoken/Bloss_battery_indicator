using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private const double CpuTargetPercent = 0.5;
    private const double RamTargetMb = 50.0;
    private const double PrivateTrimTargetMb = 180.0;

    private static readonly TimeSpan ManagedTrimMinInterval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ActivePerfInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan IdlePerfInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan IdleGracePeriod = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleManagedTrimDelay = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ProbeStateTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProbeProgressUiThrottle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan AutoProbeCooldown = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan PendingProbeFollowUpDelay = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ProbeFailureUiDebounce = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan NoSignalFailureMinCooldown = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan WeakSignalFailureMinCooldown = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan RefreshOperationTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan DualShock4InitialLowReadingWindow = TimeSpan.FromSeconds(60);
    private const int AutoProbeMaxBackoffExponent = 4;
    private const int MaxPendingProbeFollowUps = 2;
    private const int MinimumMissingRefreshesBeforeDisconnect = 1;
    private const int DualShock4InitialLowPercentThreshold = 6;

    private static readonly string ProbeErrorLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "probe-errors.log");

    private readonly IConnectedDeviceProvider _connectedDeviceProvider;
    private readonly IBatteryLevelProvider _batteryLevelProvider;
    private readonly DeviceSnapshotComposer _snapshotComposer;
    private readonly WidgetSettingsStore _settingsStore;
    private readonly AutostartService _autostartService;
    private readonly GamepadProbeService _gamepadProbeService;
    private readonly CalibrationStore _calibrationStore;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _performanceTimer;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private readonly Process _ownProcess;
    private readonly SynchronizationContext _uiContext;
    private readonly Dictionary<string, ConnectedBluetoothDevice> _lastConnectedDevicesByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PnpBatteryReading> _lastBatteryReadingsByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProbeUiState> _probeStateByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _autoProbeNextAllowedByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _autoProbeFailureCountByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _pendingProbeFollowUpCountByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProbeFailureUiState> _lastProbeFailureUiByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _missingDeviceSinceByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _missingDeviceMissCountByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingBatteryDropState> _pendingBatteryDropByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _connectedSinceByAddress = new(StringComparer.OrdinalIgnoreCase);

    private WidgetSettings _settings = new();
    private string _statusText = "Initializing...";
    private string _diagnosticsText = "Connected: 0 | Battery read: 0 | Matched: 0 | Unsupported: 0";
    private string _performanceText = "CPU -- | RAM -- | Private --";
    private int _connectedCount;
    private int _batteryReadCount;
    private int _matchedCount;
    private int _naCount;
    private int _limitExceededStreak;
    private TimeSpan _lastCpuSample;
    private DateTime _lastPerfSampleAtUtc;
    private DateTime _lastTrimAtUtc;
    private DateTime _lastManagedTrimAtUtc;
    private DateTime _lastActivityAtUtc;
    private bool _initialized;
    private bool _settingsLoaded;
    private bool _disposed;
    private bool _isAnyProbeRunning;
    private bool _isRefreshRunning;
    private double _lastCpuPercent;
    private double _lastRamMb;
    private double _lastPrivateMb;

    public MainViewModel(
        IConnectedDeviceProvider connectedDeviceProvider,
        IBatteryLevelProvider batteryLevelProvider,
        DeviceSnapshotComposer snapshotComposer,
        WidgetSettingsStore settingsStore,
        AutostartService autostartService,
        GamepadProbeService gamepadProbeService,
        CalibrationStore calibrationStore)
    {
        _connectedDeviceProvider = connectedDeviceProvider;
        _batteryLevelProvider = batteryLevelProvider;
        _snapshotComposer = snapshotComposer;
        _settingsStore = settingsStore;
        _autostartService = autostartService;
        _gamepadProbeService = gamepadProbeService;
        _calibrationStore = calibrationStore;

        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _ownProcess = Process.GetCurrentProcess();
        _lastCpuSample = _ownProcess.TotalProcessorTime;
        _lastPerfSampleAtUtc = DateTime.UtcNow;
        _lastActivityAtUtc = DateTime.UtcNow;

        Devices = new ObservableCollection<DeviceItemViewModel>();
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background);
        _refreshTimer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);

        _performanceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = ActivePerfInterval
        };
        _performanceTimer.Tick += (_, _) => MonitorPerformance();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceItemViewModel> Devices { get; }

    public WidgetSettings Settings
    {
        get => _settings;
        private set
        {
            _settings = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutostartEnabled));
            OnPropertyChanged(nameof(StartMinimizedToTrayEnabled));
            OnPropertyChanged(nameof(CloseToTrayEnabled));
            OnPropertyChanged(nameof(GuidedProbeEnabled));
            OnPropertyChanged(nameof(GuideSoundEnabled));
            OnPropertyChanged(nameof(GuideSoundId));
            OnPropertyChanged(nameof(CustomGuideSoundPath));
            OnPropertyChanged(nameof(LastDs5DongleFirmwareVersion));
            OnPropertyChanged(nameof(VisualMode));
            OnPropertyChanged(nameof(IsLiteVisualMode));
            OnPropertyChanged(nameof(ColorPresetId));
            OnPropertyChanged(nameof(UseCustomColors));
            OnPropertyChanged(nameof(CustomTextColor));
            OnPropertyChanged(nameof(CustomBackgroundColor));
            OnPropertyChanged(nameof(CustomElementColors));
            OnPropertyChanged(nameof(UseCustomFont));
            OnPropertyChanged(nameof(CustomFontPath));
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(StatusPanelCollapsed));
            OnPropertyChanged(nameof(UiScaleStep));
            OnPropertyChanged(nameof(UseCustomSettingsTextStyle));
            OnPropertyChanged(nameof(SettingsTextFontSize));
            OnPropertyChanged(nameof(SettingsTextBold));
            OnPropertyChanged(nameof(ThirdPartyBatteryPolicy));
            OnPropertyChanged(nameof(IsAggressiveThirdPartyPolicy));
            OnPropertyChanged(nameof(LanguageOptions));
            RaiseLocalizedTextPropertyChanges();
        }
    }

    public bool AutostartEnabled => Settings.Autostart;

    public bool StartMinimizedToTrayEnabled => Settings.StartMinimizedToTray;

    public bool CloseToTrayEnabled => Settings.CloseToTray;

    public bool GuidedProbeEnabled => Settings.GuidedProbeEnabled;

    public bool GuideSoundEnabled => Settings.GuideSoundEnabled;

    public string GuideSoundId => Settings.GuideSoundId;

    public string CustomGuideSoundPath => Settings.CustomGuideSoundPath;

    public string LastDs5DongleFirmwareVersion => Settings.LastDs5DongleFirmwareVersion;

    public string VisualMode => Settings.VisualMode;

    public bool IsLiteVisualMode => string.Equals(Settings.VisualMode, WidgetSettings.LiteGlassMode, StringComparison.Ordinal);

    public string ColorPresetId => Settings.ColorPresetId;

    public bool UseCustomColors => Settings.UseCustomColors;

    public string CustomTextColor => Settings.CustomTextColor;

    public string CustomBackgroundColor => Settings.CustomBackgroundColor;

    public IReadOnlyDictionary<string, string> CustomElementColors => Settings.CustomElementColors;

    public bool UseCustomFont => Settings.UseCustomFont;

    public string CustomFontPath => Settings.CustomFontPath;

    public string Language => Settings.Language;

    public IReadOnlyList<UiLanguageOption> LanguageOptions => UiLanguageCatalog.Options;

    public bool StatusPanelCollapsed => Settings.StatusPanelCollapsed;

    public int UiScaleStep => Settings.UiScaleStep;

    public bool UseCustomSettingsTextStyle => Settings.UseCustomSettingsTextStyle;

    public double SettingsTextFontSize => Settings.SettingsTextFontSize;

    public bool SettingsTextBold => Settings.SettingsTextBold;

    public ThirdPartyBatteryPolicy ThirdPartyBatteryPolicy => Settings.ThirdPartyBatteryPolicy;

    public bool IsAggressiveThirdPartyPolicy => Settings.ThirdPartyBatteryPolicy == ThirdPartyBatteryPolicy.Aggressive;

    public string TextSettingsTitle => CurrentLanguageText.SettingsTitle;

    public string TextAutostart => CurrentLanguageText.AutostartLabel;

    public string TextCloseToTray => CurrentLanguageText.CloseToTrayLabel;

    public string TextGuidedProbe => CurrentLanguageText.GuidedProbeLabel;

    public string TextAggressivePolicy => CurrentLanguageText.AggressivePolicyLabel;

    public string TextUiScale => CurrentLanguageText.UiScaleLabel;

    public string TextColorPreset => CurrentLanguageText.ColorPresetLabel;

    public string TextEnvironmentSettingsGroup => CurrentLanguageText.EnvironmentSettingsGroup;

    public string TextCustomizeSettingsGroup => CurrentLanguageText.CustomizeSettingsGroup;

    public string TextLabsSettingsGroup => CurrentLanguageText.LabsSettingsGroup;

    public string TextStartMinimizedToTray => CurrentLanguageText.StartMinimizedToTrayLabel;

    public string TextGuideSound => CurrentLanguageText.GuideSoundLabel;

    public string TextColorCustomize => CurrentLanguageText.ColorCustomizeButton;

    public string TextGuideSoundSelectorTooltip => CurrentLanguageText.GuideSoundSelectorTooltip;

    public string TextGuideSoundPreviewTooltip => CurrentLanguageText.GuideSoundPreviewTooltip;

    public string TextUserFont => CurrentLanguageText.UserFontLabel;

    public string TextLoadCustomFont => CurrentLanguageText.LoadCustomFontButton;

    public string TextResetCustomFont => CurrentLanguageText.ResetCustomFontButton;

    public string TextLanguage => "Language";

    public string TextCustomGuideSound => UiLanguageCatalog.GetExtraText(Settings.Language, "CustomGuideSoundLabel");

    public string TextColorSelect => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorSelect");

    public string TextColorTargetPrimaryText => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorPrimaryText");

    public string TextColorTargetSecondaryText => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorSecondaryText");

    public string TextColorTargetBatteryText => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorBatteryText");

    public string TextColorTargetWidgetBackground => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorWidgetBackground");

    public string TextColorTargetGlassSurface => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorGlassSurface");

    public string TextColorTargetCardTint => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorCardTint");

    public string TextColorTargetCardBorder => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorCardBorder");

    public string TextColorTargetTrack => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorTrack");

    public string TextColorTargetPanel => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorPanel");

    public string TextColorTargetSettingsText => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorSettingsText");

    public string TextSettingsTextStyle => UiLanguageCatalog.GetExtraText(Settings.Language, "SettingsTextStyle");

    public string TextSettingsTextFontSize => UiLanguageCatalog.GetExtraText(Settings.Language, "SettingsTextFontSize");

    public string TextSettingsTextBold => UiLanguageCatalog.GetExtraText(Settings.Language, "SettingsTextBold");

    public string TextSettingsTextReset => UiLanguageCatalog.GetExtraText(Settings.Language, "SettingsTextReset");

    public string TextColorReset => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorReset");

    public string TextColorDragHint => UiLanguageCatalog.GetExtraText(Settings.Language, "ColorDragHint");

    public string TextGuideSoundPreviewStopTooltip => UiLanguageCatalog.GetExtraText(Settings.Language, "GuideSoundPreviewStopTooltip");

    public string TextTrayResetPosition => UiLanguageCatalog.GetExtraText(Settings.Language, "TrayResetPosition");

    public string TextCustomGuideSoundSelectTitle => UiLanguageCatalog.GetExtraText(Settings.Language, "CustomGuideSoundSelectTitle");

    public string TextCustomGuideSoundInvalid => UiLanguageCatalog.GetExtraText(Settings.Language, "CustomGuideSoundInvalid");

    public string TextCustomFontSelectTitle => UiLanguageCatalog.GetExtraText(Settings.Language, "CustomFontSelectTitle");

    public string TextCustomFontLoadFailed => UiLanguageCatalog.GetExtraText(Settings.Language, "CustomFontLoadFailed");

    public string TextPicoFirmwareButton => UiLanguageCatalog.GetExtraText(Settings.Language, "PicoFirmwareButton");

    public string TextPicoFirmwareReady => UiLanguageCatalog.GetExtraText(Settings.Language, "PicoFirmwareReady");

    public string TextRefreshNow => CurrentLanguageText.RefreshNowButton;

    public string TextDeveloperContact => CurrentLanguageText.DeveloperContactButton;

    public string TextSupport => CurrentLanguageText.SupportButtonLabel;

    public string TextUpdate => CurrentLanguageText.UpdateButton;

    public string TextExit => CurrentLanguageText.ExitButton;

    public string TextStatusPanelToggleTooltip => CurrentLanguageText.StatusPanelToggleTooltip;

    public string TextStatusPanelCollapseTooltip => CurrentLanguageText.StatusPanelCollapseTooltip;

    public string TextSettingsTooltip => CurrentLanguageText.SettingsTooltip;

    public string TextCloseTooltip => CurrentLanguageText.CloseTooltip;

    public string TextTrayOpenWidget => CurrentLanguageText.OpenWidgetTray;

    public string TextTrayRefreshNow => CurrentLanguageText.RefreshNowTray;

    public string TextTrayExit => CurrentLanguageText.ExitTray;

    public string TextVersionPrefix => CurrentLanguageText.VersionPrefix;

    public string TextIconChange => CurrentLanguageText.InlineIconChange;

    public string TextRestoreDefault => CurrentLanguageText.InlineRestoreDefault;

    public string TextInlineCancel => CurrentLanguageText.InlineCancel;

    public string TextInlineSave => CurrentLanguageText.InlineSave;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        private set
        {
            if (_diagnosticsText == value)
            {
                return;
            }

            _diagnosticsText = value;
            OnPropertyChanged();
        }
    }

    public string PerformanceText
    {
        get => _performanceText;
        private set
        {
            if (_performanceText == value)
            {
                return;
            }

            _performanceText = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        LoadSettingsOnce();
        Settings.RefreshSeconds = 30;
        _autostartService.Apply(Settings.Autostart, Settings.StartMinimizedToTray);
        SaveSettings();

        _refreshTimer.Interval = TimeSpan.FromSeconds(Settings.RefreshSeconds);

        await RefreshAsync().ConfigureAwait(true);

        _refreshTimer.Start();
        _performanceTimer.Start();
        _initialized = true;
    }

    public void LoadSettingsOnce()
    {
        if (_settingsLoaded)
        {
            return;
        }

        Settings = _settingsStore.Load();
        _settingsLoaded = true;
    }

    public async Task RefreshAsync()
    {
        string? autoProbeTarget = null;
        var refreshTimedOut = false;

        if (!await _refreshLock.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        _isRefreshRunning = true;
        MarkActivity();

        try
        {
            StatusText = CurrentLanguageText.StatusRefreshing;
            using var refreshCts = new CancellationTokenSource(RefreshOperationTimeout);
            var refreshToken = refreshCts.Token;

            var connectedDevices = await _connectedDeviceProvider.GetConnectedDevicesAsync(refreshToken).ConfigureAwait(true);
            IReadOnlyList<PnpBatteryReading> rawBatteryLevels = [];
            if (connectedDevices.Count > 0)
            {
                rawBatteryLevels = await _batteryLevelProvider
                    .GetBatteryLevelsAsync(connectedDevices, refreshToken)
                    .ConfigureAwait(true);
            }
            var batteryLevels = ApplyBatteryJumpGuard(rawBatteryLevels, DateTimeOffset.Now);
            var overrides = IconOverrideParser.Parse(Settings.IconOverrides);
            var imageOverrides = IconImageOverrideParser.Parse(Settings.IconImageOverrides);
            var nameOverrides = NameOverrideParser.Parse(Settings.NameOverrides);
            var snapshots = _snapshotComposer.Compose(
                connectedDevices,
                batteryLevels,
                overrides,
                imageOverrides,
                nameOverrides,
                DateTimeOffset.Now);

            _lastConnectedDevicesByAddress.Clear();
            foreach (var connected in connectedDevices)
            {
                var normalized = AddressNormalizer.NormalizeAddress(connected.Address);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _lastConnectedDevicesByAddress[normalized] = connected;
                }
            }

            _lastBatteryReadingsByAddress.Clear();
            foreach (var reading in batteryLevels)
            {
                var normalizedAddress = AddressNormalizer.NormalizeAddress(reading.Address);
                if (string.IsNullOrWhiteSpace(normalizedAddress))
                {
                    continue;
                }

                if (!_lastBatteryReadingsByAddress.TryGetValue(normalizedAddress, out var existing) ||
                    (existing.BatteryPercent is null && reading.BatteryPercent is not null))
                {
                    _lastBatteryReadingsByAddress[normalizedAddress] = reading with { Address = normalizedAddress };
                }
            }

            ReconcileDeviceItems(snapshots);

            _connectedCount = connectedDevices.Count;
            _batteryReadCount = batteryLevels.Count(level => level.BatteryPercent is not null);
            _matchedCount = snapshots.Count(snapshot => snapshot.BatteryPercent is not null);
            _naCount = snapshots.Count - _matchedCount;
            DiagnosticsText = UiLanguageCatalog.BuildDiagnosticsSummary(
                Settings.Language,
                _connectedCount,
                _batteryReadCount,
                _matchedCount,
                _naCount);

            StatusText = UiLanguageCatalog.BuildUpdatedStatus(Settings.Language, DateTime.Now);
            ProcessMemoryTrimmer.TryTrim(_ownProcess);
            autoProbeTarget = SelectAutoProbeTarget(DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            refreshTimedOut = true;
            StatusText = CurrentLanguageText.StatusRefreshTimeout;
        }
        catch
        {
            StatusText = CurrentLanguageText.StatusRefreshFailed;
        }
        finally
        {
            _isRefreshRunning = false;
            _refreshLock.Release();
        }

        if (!string.IsNullOrWhiteSpace(autoProbeTarget))
        {
            StartAutoProbe(autoProbeTarget);
        }

        if (refreshTimedOut)
        {
            DiagnosticsText = CurrentLanguageText.DiagnosticsRefreshSkipped;
        }
    }

    private IReadOnlyList<PnpBatteryReading> ApplyBatteryJumpGuard(
        IReadOnlyList<PnpBatteryReading> rawBatteryLevels,
        DateTimeOffset now)
    {
        if (rawBatteryLevels.Count == 0)
        {
            return rawBatteryLevels;
        }

        var guarded = new List<PnpBatteryReading>(rawBatteryLevels.Count);
        var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reading in rawBatteryLevels)
        {
            var normalizedAddress = AddressNormalizer.NormalizeAddress(reading.Address);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                guarded.Add(reading);
                continue;
            }

            seenAddresses.Add(normalizedAddress);

            if (reading.BatteryPercent is null)
            {
                guarded.Add(reading with { Address = normalizedAddress });
                continue;
            }

            if (!_lastBatteryReadingsByAddress.TryGetValue(normalizedAddress, out var previous) ||
                previous.BatteryPercent is null)
            {
                _pendingBatteryDropByAddress.Remove(normalizedAddress);
                guarded.Add(reading with { Address = normalizedAddress });
                continue;
            }

            var previousPercent = previous.BatteryPercent.Value;
            var currentPercent = reading.BatteryPercent.Value;
            if (!ShouldHoldSuddenDrop(previousPercent, currentPercent, reading.SourceKind))
            {
                _pendingBatteryDropByAddress.Remove(normalizedAddress);
                guarded.Add(reading with { Address = normalizedAddress });
                continue;
            }

            var requiredConfirmations = GetRequiredDropConfirmations(Settings.ThirdPartyBatteryPolicy);
            var nextCount = 1;
            if (_pendingBatteryDropByAddress.TryGetValue(normalizedAddress, out var pending) &&
                Math.Abs(pending.CandidatePercent - currentPercent) <= 4 &&
                now - pending.FirstObservedAt <= TimeSpan.FromMinutes(4))
            {
                nextCount = pending.Confirmations + 1;
            }

            _pendingBatteryDropByAddress[normalizedAddress] = new PendingBatteryDropState(
                CandidatePercent: currentPercent,
                Confirmations: nextCount,
                FirstObservedAt: now);

            if (nextCount < requiredConfirmations)
            {
                guarded.Add(reading with
                {
                    Address = normalizedAddress,
                    BatteryPercent = null,
                    BatteryConfidence = BatteryConfidence.Estimated,
                    IsBatterySuspect = true
                });
                continue;
            }

            _pendingBatteryDropByAddress.Remove(normalizedAddress);
            guarded.Add(reading with { Address = normalizedAddress });
        }

        var staleKeys = _pendingBatteryDropByAddress
            .Where(pair => !seenAddresses.Contains(pair.Key) || now - pair.Value.FirstObservedAt > TimeSpan.FromMinutes(6))
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in staleKeys)
        {
            _pendingBatteryDropByAddress.Remove(key);
        }

        return guarded;
    }

    private static bool ShouldHoldSuddenDrop(int previousPercent, int currentPercent, BatterySourceKind sourceKind)
    {
        if (sourceKind is not (
            BatterySourceKind.LearnedHid or
            BatterySourceKind.GameInput or
            BatterySourceKind.SetupApi or
            BatterySourceKind.HidFeature or
            BatterySourceKind.XInput))
        {
            return false;
        }

        if (previousPercent < 85 || currentPercent > 20)
        {
            return false;
        }

        return previousPercent - currentPercent >= 60;
    }

    private TimeSpan GetDeviceDisconnectGrace()
    {
        var configuredSeconds = WidgetSettings.NormalizeGamepadDisconnectGraceSeconds(Settings.GamepadDisconnectGraceSeconds);
        return TimeSpan.FromSeconds(Math.Max(0, configuredSeconds));
    }

    private TimeSpan GetBatteryHoldDuration()
    {
        var configuredSeconds = WidgetSettings.NormalizeBatteryHoldSeconds(Settings.BatteryHoldSeconds);
        return TimeSpan.FromSeconds(configuredSeconds);
    }

    internal static bool ShouldKeepMissingDevice(DateTime nowUtc, DateTime missingSinceUtc, TimeSpan disconnectGrace)
    {
        return ShouldKeepMissingDevice(
            nowUtc,
            missingSinceUtc,
            disconnectGrace,
            missingCount: 1,
            minimumMissingCount: 1);
    }

    internal static bool ShouldKeepMissingDevice(
        DateTime nowUtc,
        DateTime missingSinceUtc,
        TimeSpan disconnectGrace,
        int missingCount,
        int minimumMissingCount)
    {
        if (missingCount < Math.Max(1, minimumMissingCount))
        {
            return true;
        }

        if (disconnectGrace <= TimeSpan.Zero)
        {
            return false;
        }

        return nowUtc - missingSinceUtc < disconnectGrace;
    }

    private static int GetRequiredDropConfirmations(ThirdPartyBatteryPolicy policy)
    {
        return policy switch
        {
            ThirdPartyBatteryPolicy.Conservative => 3,
            ThirdPartyBatteryPolicy.Hybrid => 2,
            _ => 2
        };
    }

    public async Task ProbeUnsupportedGamepadAsync(string address)
    {
        string? pendingFollowUpAddress = null;
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return;
        }

        var target = Devices.FirstOrDefault(device => string.Equals(device.Address, normalizedAddress, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        if (!target.IsProbeEligible)
        {
            ClearPendingProbeFollowUp(normalizedAddress);
            target.CompleteProbe(CurrentLanguageText.ProbeAlreadyHasBattery, 0);
            return;
        }

        if (!_lastConnectedDevicesByAddress.TryGetValue(normalizedAddress, out var connectedDevice))
        {
            ClearPendingProbeFollowUp(normalizedAddress);
            target.CompleteProbe(CurrentLanguageText.ProbeConnectionInfoMissing, 0);
            return;
        }

        if (!await _probeLock.WaitAsync(0).ConfigureAwait(true))
        {
            target.CompleteProbe(CurrentLanguageText.ProbeOtherProbeRunning, target.ProbeProgress);
            return;
        }

        MarkActivity();
        RegisterAutoProbeAttempt(normalizedAddress, DateTime.UtcNow);
        _isAnyProbeRunning = true;
        target.BeginProbe(CurrentLanguageText.ProbeStart);
        SetProbeState(normalizedAddress, isRunning: true, progress: 0, status: CurrentLanguageText.ProbeStart);
        ApplyProbeActionAvailability();

        try
        {
            if (GuidedProbeEnabled)
            {
                var guidedMessage = CurrentLanguageText.ProbeGuided;
                target.UpdateProbeProgress(0, guidedMessage);
                SetProbeState(normalizedAddress, isRunning: true, progress: 0, status: guidedMessage, forceUiStamp: true);
                await Task.Delay(TimeSpan.FromSeconds(2.3)).ConfigureAwait(true);
                MarkActivity();
            }

            var result = await _gamepadProbeService
                .ProbeAsync(
                    connectedDevice,
                    progress => PostToUi(() => ApplyProbeProgress(normalizedAddress, progress)),
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (result.Success)
            {
                var successStatus = string.IsNullOrWhiteSpace(result.Message)
                    ? CurrentLanguageText.ProbeSuccessDefault
                    : result.Message;

                SetProbeState(normalizedAddress, isRunning: false, progress: 100, status: successStatus, forceUiStamp: true);
                target.CompleteProbe(successStatus, 100);
                MarkActivity();
                var nowUtc = DateTime.UtcNow;
                if (result.IsPending)
                {
                    RegisterAutoProbePending(normalizedAddress, nowUtc);
                    if (TrySchedulePendingProbeFollowUp(normalizedAddress))
                    {
                        pendingFollowUpAddress = normalizedAddress;
                    }
                    else
                    {
                        _autoProbeNextAllowedByAddress[normalizedAddress] = nowUtc + AutoProbeCooldown;
                    }
                }
                else
                {
                    RegisterAutoProbeSuccess(normalizedAddress, nowUtc);
                    ClearPendingProbeFollowUp(normalizedAddress);
                    await RefreshAsync().ConfigureAwait(true);
                }
            }
            else
            {
                var failureStatus = BuildProbeFailureStatus(result, Settings.Language);
                var nowUtc = DateTime.UtcNow;
                var failureUiKey = BuildProbeFailureUiKey(result);
                RegisterAutoProbeFailure(normalizedAddress, nowUtc, result);
                ClearPendingProbeFollowUp(normalizedAddress);
                if (!ShouldSuppressFailureUiUpdate(normalizedAddress, failureUiKey, nowUtc))
                {
                    SetProbeState(normalizedAddress, isRunning: false, progress: 0, status: failureStatus, forceUiStamp: true);
                    target.CompleteProbe(failureStatus, 0);
                    AppendProbeErrorLog(normalizedAddress, connectedDevice.DisplayName, result);
                }
                else
                {
                    var keepStatus = string.IsNullOrWhiteSpace(target.ProbeStatus) ? CurrentLanguageText.ProbeFailureDefault : target.ProbeStatus;
                    SetProbeState(normalizedAddress, isRunning: false, progress: target.ProbeProgress, status: keepStatus, forceUiStamp: true);
                    target.CompleteProbe(keepStatus, target.ProbeProgress);
                }
            }
        }
        catch (OperationCanceledException)
        {
            SetProbeState(normalizedAddress, isRunning: false, progress: 0, status: CurrentLanguageText.ProbeCancelled, forceUiStamp: true);
            target.CompleteProbe(CurrentLanguageText.ProbeCancelled, 0);
            RegisterAutoProbeFailure(normalizedAddress, DateTime.UtcNow, null);
            ClearPendingProbeFollowUp(normalizedAddress);
        }
        catch (Exception ex)
        {
            var exceptionResult = new ProbeResult(
                Success: false,
                BatteryPercent: null,
                Message: CurrentLanguageText.ProbeException,
                Profile: null,
                ErrorDetail: new ProbeErrorDetail(
                    Stage: ProbeStage.None,
                    ExceptionType: ex.GetType().Name,
                    ExceptionMessage: ex.Message,
                    DiagnosticsText: "viewModel=ProbeUnsupportedGamepadAsync",
                    Timestamp: DateTimeOffset.Now,
                    Context: "viewModel=ProbeUnsupportedGamepadAsync"));
            var failureStatus = BuildProbeFailureStatus(exceptionResult, Settings.Language);
            var nowUtc = DateTime.UtcNow;
            var failureUiKey = BuildProbeFailureUiKey(exceptionResult);
            RegisterAutoProbeFailure(normalizedAddress, nowUtc, exceptionResult);
            ClearPendingProbeFollowUp(normalizedAddress);
            if (!ShouldSuppressFailureUiUpdate(normalizedAddress, failureUiKey, nowUtc))
            {
                SetProbeState(normalizedAddress, isRunning: false, progress: 0, status: failureStatus, forceUiStamp: true);
                target.CompleteProbe(failureStatus, 0);
                AppendProbeErrorLog(normalizedAddress, connectedDevice.DisplayName, exceptionResult);
            }
            else
            {
                    var keepStatus = string.IsNullOrWhiteSpace(target.ProbeStatus) ? CurrentLanguageText.ProbeFailureDefault : target.ProbeStatus;
                SetProbeState(normalizedAddress, isRunning: false, progress: target.ProbeProgress, status: keepStatus, forceUiStamp: true);
                target.CompleteProbe(keepStatus, target.ProbeProgress);
            }
        }
        finally
        {
            MarkActivity();
            _isAnyProbeRunning = false;
            ApplyProbeActionAvailability();
            _probeLock.Release();
        }

        if (!string.IsNullOrWhiteSpace(pendingFollowUpAddress))
        {
            StartPendingProbeFollowUp(pendingFollowUpAddress);
        }
    }

    public async Task CalibrateDeviceFullAsync(string address)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return;
        }

        var target = Devices.FirstOrDefault(device => string.Equals(device.Address, normalizedAddress, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        if (!_lastBatteryReadingsByAddress.TryGetValue(normalizedAddress, out var reading))
        {
            target.CompleteProbe(CurrentLanguageText.ProbeCalibrationRawMissing, 0);
            return;
        }

        if (string.IsNullOrWhiteSpace(reading.ModelKey) || reading.RawMetric is null || reading.RawMetric <= 0)
        {
            target.CompleteProbe(CurrentLanguageText.ProbeCalibrationNotPossible, 0);
            return;
        }

        _calibrationStore.UpsertFullAnchor(reading.ModelKey, reading.RawMetric.Value, DateTimeOffset.Now);
        target.CompleteProbe(CurrentLanguageText.ProbeCalibrationSaved, 100);
        MarkActivity();
        await RefreshAsync().ConfigureAwait(true);
    }

    public IReadOnlyList<DeviceBatterySnapshot> GetDeviceSnapshots()
    {
        return Devices.Select(item => item.Snapshot).ToList();
    }

    public void SetAutostart(bool enabled)
    {
        Settings.Autostart = enabled;
        _autostartService.Apply(enabled, Settings.StartMinimizedToTray);
        SaveSettings();
        OnPropertyChanged(nameof(AutostartEnabled));
    }

    public void SetStartMinimizedToTray(bool enabled)
    {
        if (Settings.StartMinimizedToTray == enabled)
        {
            return;
        }

        Settings.StartMinimizedToTray = enabled;
        _autostartService.Apply(Settings.Autostart, enabled);
        SaveSettings();
        OnPropertyChanged(nameof(StartMinimizedToTrayEnabled));
    }

    public void SetCloseToTray(bool enabled)
    {
        Settings.CloseToTray = enabled;
        SaveSettings();
        OnPropertyChanged(nameof(CloseToTrayEnabled));
    }

    public void SetGuidedProbeEnabled(bool enabled)
    {
        if (Settings.GuidedProbeEnabled == enabled)
        {
            return;
        }

        Settings.GuidedProbeEnabled = enabled;
        SaveSettings();
        OnPropertyChanged(nameof(GuidedProbeEnabled));
    }

    public void SetGuideSoundEnabled(bool enabled)
    {
        if (Settings.GuideSoundEnabled == enabled)
        {
            return;
        }

        Settings.GuideSoundEnabled = enabled;
        SaveSettings();
        OnPropertyChanged(nameof(GuideSoundEnabled));
    }

    public void SetGuideSoundId(string soundId)
    {
        var normalized = WidgetSettings.NormalizeGuideSoundId(soundId);
        if (normalized == WidgetSettings.GuideSoundCustomFile &&
            string.IsNullOrWhiteSpace(Settings.CustomGuideSoundPath))
        {
            normalized = WidgetSettings.DefaultGuideSoundId;
        }

        if (string.Equals(Settings.GuideSoundId, normalized, StringComparison.Ordinal))
        {
            return;
        }

        Settings.GuideSoundId = normalized;
        SaveSettings();
        OnPropertyChanged(nameof(GuideSoundId));
    }

    public void SetCustomGuideSoundPath(string path)
    {
        var normalized = WidgetSettings.NormalizeOptionalAudioPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (string.Equals(Settings.CustomGuideSoundPath, normalized, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Settings.GuideSoundId, WidgetSettings.GuideSoundCustomFile, StringComparison.Ordinal))
        {
            return;
        }

        Settings.CustomGuideSoundPath = normalized;
        Settings.GuideSoundId = WidgetSettings.GuideSoundCustomFile;
        SaveSettings();
        OnPropertyChanged(nameof(CustomGuideSoundPath));
        OnPropertyChanged(nameof(GuideSoundId));
    }

    public void ResetCustomGuideSound()
    {
        var changed = !string.IsNullOrWhiteSpace(Settings.CustomGuideSoundPath) ||
                      string.Equals(Settings.GuideSoundId, WidgetSettings.GuideSoundCustomFile, StringComparison.Ordinal);
        if (!changed)
        {
            return;
        }

        Settings.CustomGuideSoundPath = string.Empty;
        Settings.GuideSoundId = WidgetSettings.DefaultGuideSoundId;
        SaveSettings();
        OnPropertyChanged(nameof(CustomGuideSoundPath));
        OnPropertyChanged(nameof(GuideSoundId));
    }

    public void RememberDs5DongleFirmwareVersion(string? version)
    {
        var normalized = WidgetSettings.NormalizeFirmwareVersionText(version);
        if (string.IsNullOrWhiteSpace(normalized) ||
            string.Equals(Settings.LastDs5DongleFirmwareVersion, normalized, StringComparison.Ordinal))
        {
            return;
        }

        Settings.LastDs5DongleFirmwareVersion = normalized;
        SaveSettings();
        OnPropertyChanged(nameof(LastDs5DongleFirmwareVersion));
    }

    public void SetStatusPanelCollapsed(bool collapsed)
    {
        if (Settings.StatusPanelCollapsed == collapsed)
        {
            return;
        }

        Settings.StatusPanelCollapsed = collapsed;
        SaveSettings();
        OnPropertyChanged(nameof(StatusPanelCollapsed));
    }

    public void SetUiScaleStep(int step)
    {
        var normalized = WidgetSettings.NormalizeUiScaleStep(step);
        if (Settings.UiScaleStep == normalized)
        {
            return;
        }

        Settings.UiScaleStep = normalized;
        SaveSettings();
        OnPropertyChanged(nameof(UiScaleStep));
    }

    public void SetThirdPartyBatteryPolicy(ThirdPartyBatteryPolicy policy)
    {
        if (!Enum.IsDefined(policy))
        {
            policy = ThirdPartyBatteryPolicy.Aggressive;
        }

        if (Settings.ThirdPartyBatteryPolicy == policy)
        {
            return;
        }

        Settings.ThirdPartyBatteryPolicy = policy;
        SaveSettings();
        OnPropertyChanged(nameof(ThirdPartyBatteryPolicy));
        OnPropertyChanged(nameof(IsAggressiveThirdPartyPolicy));
    }

    public void SetVisualMode(string mode)
    {
        if (!string.Equals(mode, WidgetSettings.NormalGlassMode, StringComparison.Ordinal) &&
            !string.Equals(mode, WidgetSettings.LiteGlassMode, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(Settings.VisualMode, mode, StringComparison.Ordinal))
        {
            return;
        }

        Settings.VisualMode = mode;
        SaveSettings();
        OnPropertyChanged(nameof(VisualMode));
        OnPropertyChanged(nameof(IsLiteVisualMode));
        if (_lastCpuPercent > 0 || _lastRamMb > 0 || _lastPrivateMb > 0)
        {
            PerformanceText = BuildPerformanceText(_lastCpuPercent, _lastRamMb, _lastPrivateMb);
        }
    }

    public void SetColorPreset(string presetId)
    {
        var normalized = WidgetSettings.NormalizeColorPresetId(presetId);
        var changed = !string.Equals(Settings.ColorPresetId, normalized, StringComparison.Ordinal);
        var hadCustomColors = Settings.UseCustomColors ||
                              !string.IsNullOrWhiteSpace(Settings.CustomTextColor) ||
                              !string.IsNullOrWhiteSpace(Settings.CustomBackgroundColor) ||
                              Settings.CustomElementColors.Count > 0;
        if (!changed && !hadCustomColors)
        {
            return;
        }

        Settings.ColorPresetId = normalized;
        Settings.UseCustomColors = false;
        Settings.CustomTextColor = string.Empty;
        Settings.CustomBackgroundColor = string.Empty;
        Settings.CustomElementColors.Clear();
        SaveSettings();
        if (changed)
        {
            OnPropertyChanged(nameof(ColorPresetId));
        }

        if (hadCustomColors)
        {
            OnPropertyChanged(nameof(UseCustomColors));
            OnPropertyChanged(nameof(CustomTextColor));
            OnPropertyChanged(nameof(CustomBackgroundColor));
            OnPropertyChanged(nameof(CustomElementColors));
        }
    }

    public void SetCustomColors(string textColor, string backgroundColor)
    {
        var normalizedText = WidgetSettings.NormalizeOptionalHexColor(textColor);
        var normalizedBackground = WidgetSettings.NormalizeOptionalHexColor(backgroundColor);
        if (string.IsNullOrWhiteSpace(normalizedText) || string.IsNullOrWhiteSpace(normalizedBackground))
        {
            return;
        }

        if (Settings.UseCustomColors &&
            string.Equals(Settings.CustomTextColor, normalizedText, StringComparison.Ordinal) &&
            string.Equals(Settings.CustomBackgroundColor, normalizedBackground, StringComparison.Ordinal))
        {
            return;
        }

        Settings.UseCustomColors = true;
        Settings.CustomTextColor = normalizedText;
        Settings.CustomBackgroundColor = normalizedBackground;
        Settings.CustomElementColors["PrimaryText"] = normalizedText;
        Settings.CustomElementColors["CardTint"] = normalizedBackground;
        SaveSettings();
        OnPropertyChanged(nameof(UseCustomColors));
        OnPropertyChanged(nameof(CustomTextColor));
        OnPropertyChanged(nameof(CustomBackgroundColor));
        OnPropertyChanged(nameof(CustomElementColors));
    }

    public void SetCustomElementColor(string elementKey, string color, bool save = true)
    {
        var normalizedKey = elementKey?.Trim() ?? string.Empty;
        var normalizedColor = WidgetSettings.NormalizeOptionalHexColor(color);
        if (string.IsNullOrWhiteSpace(normalizedKey) || string.IsNullOrWhiteSpace(normalizedColor))
        {
            return;
        }

        if (Settings.UseCustomColors &&
            Settings.CustomElementColors.TryGetValue(normalizedKey, out var current) &&
            string.Equals(current, normalizedColor, StringComparison.Ordinal))
        {
            return;
        }

        Settings.UseCustomColors = true;
        Settings.CustomElementColors[normalizedKey] = normalizedColor;
        var changedSettingsTextStyle = false;
        if (string.Equals(normalizedKey, "SettingsText", StringComparison.OrdinalIgnoreCase) &&
            !Settings.UseCustomSettingsTextStyle)
        {
            Settings.UseCustomSettingsTextStyle = true;
            changedSettingsTextStyle = true;
        }

        if (string.Equals(normalizedKey, "PrimaryText", StringComparison.OrdinalIgnoreCase))
        {
            Settings.CustomTextColor = normalizedColor;
            OnPropertyChanged(nameof(CustomTextColor));
        }
        else if (string.Equals(normalizedKey, "CardTint", StringComparison.OrdinalIgnoreCase))
        {
            Settings.CustomBackgroundColor = normalizedColor;
            OnPropertyChanged(nameof(CustomBackgroundColor));
        }

        if (save)
        {
            SaveSettings();
        }

        OnPropertyChanged(nameof(UseCustomColors));
        OnPropertyChanged(nameof(CustomElementColors));
        if (changedSettingsTextStyle)
        {
            OnPropertyChanged(nameof(UseCustomSettingsTextStyle));
        }
    }

    public void ClearCustomColors()
    {
        if (!Settings.UseCustomColors &&
            string.IsNullOrWhiteSpace(Settings.CustomTextColor) &&
            string.IsNullOrWhiteSpace(Settings.CustomBackgroundColor) &&
            Settings.CustomElementColors.Count == 0)
        {
            return;
        }

        Settings.UseCustomColors = false;
        Settings.CustomTextColor = string.Empty;
        Settings.CustomBackgroundColor = string.Empty;
        Settings.CustomElementColors.Clear();
        SaveSettings();
        OnPropertyChanged(nameof(UseCustomColors));
        OnPropertyChanged(nameof(CustomTextColor));
        OnPropertyChanged(nameof(CustomBackgroundColor));
        OnPropertyChanged(nameof(CustomElementColors));
    }

    public void SetCustomFont(string fontPath)
    {
        var normalizedPath = fontPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        if (Settings.UseCustomFont &&
            string.Equals(Settings.CustomFontPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Settings.UseCustomFont = true;
        Settings.CustomFontPath = normalizedPath;
        SaveSettings();
        OnPropertyChanged(nameof(UseCustomFont));
        OnPropertyChanged(nameof(CustomFontPath));
    }

    public void SetSettingsTextFontSize(double fontSize)
    {
        var normalized = WidgetSettings.NormalizeSettingsTextFontSize(fontSize);
        if (Settings.UseCustomSettingsTextStyle &&
            Math.Abs(Settings.SettingsTextFontSize - normalized) < 0.01d)
        {
            return;
        }

        Settings.UseCustomSettingsTextStyle = true;
        Settings.SettingsTextFontSize = normalized;
        SaveSettings();
        OnPropertyChanged(nameof(UseCustomSettingsTextStyle));
        OnPropertyChanged(nameof(SettingsTextFontSize));
    }

    public void SetSettingsTextBold(bool isBold)
    {
        if (Settings.UseCustomSettingsTextStyle && Settings.SettingsTextBold == isBold)
        {
            return;
        }

        Settings.UseCustomSettingsTextStyle = true;
        Settings.SettingsTextBold = isBold;
        SaveSettings();
        OnPropertyChanged(nameof(UseCustomSettingsTextStyle));
        OnPropertyChanged(nameof(SettingsTextBold));
    }

    public void ClearSettingsTextStyle()
    {
        var removedColor = Settings.CustomElementColors.Remove("SettingsText");
        var changed = Settings.UseCustomSettingsTextStyle ||
                      Math.Abs(Settings.SettingsTextFontSize - WidgetSettings.DefaultSettingsTextFontSize) > 0.01d ||
                      Settings.SettingsTextBold ||
                      removedColor;
        if (!changed)
        {
            return;
        }

        Settings.UseCustomSettingsTextStyle = false;
        Settings.SettingsTextFontSize = WidgetSettings.DefaultSettingsTextFontSize;
        Settings.SettingsTextBold = false;
        Settings.UseCustomColors = Settings.CustomElementColors.Count > 0;
        SaveSettings();
        OnPropertyChanged(nameof(UseCustomSettingsTextStyle));
        OnPropertyChanged(nameof(SettingsTextFontSize));
        OnPropertyChanged(nameof(SettingsTextBold));
        OnPropertyChanged(nameof(UseCustomColors));
        OnPropertyChanged(nameof(CustomElementColors));
    }

    public void ClearCustomFont()
    {
        if (!Settings.UseCustomFont && string.IsNullOrWhiteSpace(Settings.CustomFontPath))
        {
            return;
        }

        Settings.UseCustomFont = false;
        Settings.CustomFontPath = string.Empty;
        SaveSettings();
        OnPropertyChanged(nameof(UseCustomFont));
        OnPropertyChanged(nameof(CustomFontPath));
    }

    public void SetLanguage(string language)
    {
        var normalized = WidgetSettings.NormalizeLanguage(language);
        if (string.Equals(Settings.Language, normalized, StringComparison.Ordinal))
        {
            return;
        }

        Settings.Language = normalized;
        SaveSettings();
        OnPropertyChanged(nameof(Language));
        RaiseLocalizedTextPropertyChanges();
        RefreshLocalizedSummaryTexts();
    }

    public void SetWindowBounds(Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || double.IsNaN(bounds.Left) || double.IsNaN(bounds.Top))
        {
            return;
        }

        Settings.WindowBounds = new WindowBounds
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height
        };

        SaveSettings();
    }

    public void SaveSettings()
    {
        _settingsStore.Save(Settings);
    }

    public bool HasNameOverride(string address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return Settings.NameOverrides.ContainsKey(normalized);
    }

    public void SetNameOverride(string address, string displayName)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        var trimmed = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        NameOverrideParser.Set(Settings.NameOverrides, normalized, trimmed);
        SaveSettings();
    }

    public void RemoveNameOverride(string address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        NameOverrideParser.Remove(Settings.NameOverrides, normalized);
        SaveSettings();
    }

    public void SetIconOverride(string address, IconKey iconKey)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (iconKey == IconKey.Unknown)
        {
            IconOverrideParser.Remove(Settings.IconOverrides, normalized);
        }
        else
        {
            IconOverrideParser.Set(Settings.IconOverrides, normalized, iconKey);
        }

        SaveSettings();
    }

    public void RemoveIconOverride(string address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        IconOverrideParser.Remove(Settings.IconOverrides, normalized);
        SaveSettings();
    }

    public void SetIconImageOverride(string address, string iconImagePath)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        var trimmedPath = iconImagePath?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || string.IsNullOrWhiteSpace(trimmedPath))
        {
            return;
        }

        IconImageOverrideParser.Set(Settings.IconImageOverrides, normalized, trimmedPath);
        SaveSettings();
    }

    public void RemoveIconImageOverride(string address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        IconImageOverrideParser.Remove(Settings.IconImageOverrides, normalized);
        SaveSettings();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _refreshTimer.Stop();
        _performanceTimer.Stop();
        _refreshLock.Dispose();
        _probeLock.Dispose();
        _ownProcess.Dispose();
        _disposed = true;
    }

    private void ReconcileDeviceItems(IReadOnlyList<DeviceBatterySnapshot> snapshots)
    {
        var nowUtc = DateTime.UtcNow;
        var now = DateTimeOffset.Now;
        var disconnectGrace = GetDeviceDisconnectGrace();
        var batteryHoldDuration = GetBatteryHoldDuration();
        CleanupProbeStateCache(nowUtc);

        var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingByAddress = Devices.ToDictionary(device => device.Address, StringComparer.OrdinalIgnoreCase);
        var nextItems = new List<DeviceItemViewModel>(snapshots.Count + existingByAddress.Count);

        foreach (var snapshot in snapshots)
        {
            seenAddresses.Add(snapshot.Address);
            _missingDeviceSinceByAddress.Remove(snapshot.Address);
            _missingDeviceMissCountByAddress.Remove(snapshot.Address);
            var appliedSnapshot = snapshot;
            DateTimeOffset connectedSince;
            if (!_connectedSinceByAddress.TryGetValue(snapshot.Address, out connectedSince))
            {
                connectedSince = now;
                _connectedSinceByAddress[snapshot.Address] = connectedSince;
            }

            if (!existingByAddress.TryGetValue(snapshot.Address, out var item))
            {
                appliedSnapshot = ApplyInitialDualShock4ConnectingSnapshot(snapshot, connectedSince, now);
                item = new DeviceItemViewModel(appliedSnapshot);
            }
            else
            {
                if (!item.Snapshot.IsConnected && snapshot.IsConnected)
                {
                    connectedSince = now;
                    _connectedSinceByAddress[snapshot.Address] = connectedSince;
                }

                appliedSnapshot = ResolveBatteryHoldSnapshot(
                    item.Snapshot,
                    snapshot,
                    batteryHoldDuration,
                    now);
                appliedSnapshot = ApplyInitialDualShock4ConnectingSnapshot(appliedSnapshot, connectedSince, now);
                item.UpdateSnapshot(appliedSnapshot);
            }

            RestoreProbeState(item, appliedSnapshot);
            nextItems.Add(item);
        }

        foreach (var pair in existingByAddress)
        {
            if (seenAddresses.Contains(pair.Key))
            {
                continue;
            }

            _connectedSinceByAddress.Remove(pair.Key);

            if (!_missingDeviceSinceByAddress.TryGetValue(pair.Key, out var missingSince))
            {
                missingSince = nowUtc;
                _missingDeviceSinceByAddress[pair.Key] = missingSince;
            }

            var missingCount = _missingDeviceMissCountByAddress.TryGetValue(pair.Key, out var existingMissingCount)
                ? existingMissingCount + 1
                : 1;
            _missingDeviceMissCountByAddress[pair.Key] = missingCount;

            if (!ShouldKeepMissingDevice(
                    nowUtc,
                    missingSince,
                    disconnectGrace,
                    missingCount,
                    MinimumMissingRefreshesBeforeDisconnect))
            {
                continue;
            }

            var staleSnapshot = pair.Value.Snapshot with
            {
                IsConnected = pair.Value.Snapshot.IsConnected,
                IsStale = true,
                DisplayState = pair.Value.Snapshot.BatteryPercent is null
                    ? BatteryDisplayState.NA
                    : pair.Value.Snapshot.DisplayState
            };
            pair.Value.UpdateSnapshot(staleSnapshot);
            RestoreProbeState(pair.Value, staleSnapshot);
            nextItems.Add(pair.Value);
        }

        var nextAddresses = nextItems
            .Select(item => item.Address)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staleMissingKeys = _missingDeviceSinceByAddress
            .Where(pair => seenAddresses.Contains(pair.Key) || !nextAddresses.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in staleMissingKeys)
        {
            _missingDeviceSinceByAddress.Remove(key);
            _missingDeviceMissCountByAddress.Remove(key);
            _connectedSinceByAddress.Remove(key);
        }

        for (var index = Devices.Count - 1; index >= 0; index--)
        {
            if (!nextItems.Any(item => string.Equals(item.Address, Devices[index].Address, StringComparison.OrdinalIgnoreCase)))
            {
                Devices.RemoveAt(index);
            }
        }

        for (var targetIndex = 0; targetIndex < nextItems.Count; targetIndex++)
        {
            var desired = nextItems[targetIndex];
            if (targetIndex < Devices.Count && ReferenceEquals(Devices[targetIndex], desired))
            {
                continue;
            }

            var existingIndex = Devices.IndexOf(desired);
            if (existingIndex >= 0)
            {
                Devices.Move(existingIndex, targetIndex);
                continue;
            }

            Devices.Insert(targetIndex, desired);
        }

        while (Devices.Count > nextItems.Count)
        {
            Devices.RemoveAt(Devices.Count - 1);
        }

        ApplyProbeActionAvailability();
    }

    internal static DeviceBatterySnapshot ResolveBatteryHoldSnapshot(
        DeviceBatterySnapshot previous,
        DeviceBatterySnapshot current,
        TimeSpan holdDuration,
        DateTimeOffset now)
    {
        if (current.BatteryPercent is not null)
        {
            return current with { IsStale = false };
        }

        if (!current.IsConnected || previous.BatteryPercent is null || previous.LastUpdated == default)
        {
            return current;
        }

        if (ShouldDiscardPreviousForCurrentReading(previous, current))
        {
            return current;
        }

        if (holdDuration <= TimeSpan.Zero)
        {
            return current with { IsStale = true };
        }

        var age = now - previous.LastUpdated;
        if (age > holdDuration)
        {
            return current with
            {
                IsStale = true,
                DisplayState = BatteryDisplayState.NA
            };
        }

        return current with
        {
            BatteryPercent = previous.BatteryPercent,
            BatteryConfidence = previous.BatteryConfidence,
            SourceKind = previous.SourceKind,
            ModelKey = previous.ModelKey,
            IsBatterySuspect = previous.IsBatterySuspect,
            IsStale = true,
            IsBatteryConnecting = false,
            IsChargeComplete = previous.IsChargeComplete,
            DisplayState = previous.DisplayState == BatteryDisplayState.Unknown
                ? BatteryDisplayState.Estimated
                : previous.DisplayState,
            LastUpdated = previous.LastUpdated
        };
    }

    internal static bool ShouldDiscardPreviousForCurrentReading(
        DeviceBatterySnapshot previous,
        DeviceBatterySnapshot current)
    {
        return previous.BatteryPercent == 100 &&
               current.SourceKind == BatterySourceKind.SteamHid &&
               current.IsCharging &&
               current.IsBatterySuspect &&
               current.BatteryPercent is null;
    }

    internal static bool ShouldTreatDualShock4InitialLowAsConnecting(
        DeviceBatterySnapshot snapshot,
        DateTimeOffset connectedSince,
        DateTimeOffset now)
    {
        if (!snapshot.IsConnected ||
            snapshot.BatteryPercent is null ||
            snapshot.BatteryPercent.Value > DualShock4InitialLowPercentThreshold)
        {
            return false;
        }

        if (snapshot.SourceKind is not (BatterySourceKind.SonyHid or BatterySourceKind.HidFeature))
        {
            return false;
        }

        if (!IsDualShock4Model(snapshot.ModelKey))
        {
            return false;
        }

        return now - connectedSince <= DualShock4InitialLowReadingWindow;
    }

    private static bool IsDualShock4Model(string? modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return false;
        }

        return modelKey.IndexOf("VID_054C|PID_09CC", StringComparison.OrdinalIgnoreCase) >= 0 ||
               modelKey.IndexOf("VID_054C|PID_05C4", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static DeviceBatterySnapshot ApplyInitialDualShock4ConnectingSnapshot(
        DeviceBatterySnapshot snapshot,
        DateTimeOffset connectedSince,
        DateTimeOffset now)
    {
        if (!ShouldTreatDualShock4InitialLowAsConnecting(snapshot, connectedSince, now))
        {
            return snapshot with { IsBatteryConnecting = false };
        }

        return snapshot with
        {
            BatteryPercent = null,
            BatteryConfidence = BatteryConfidence.Estimated,
            IsBatterySuspect = true,
            IsStale = false,
            IsBatteryConnecting = true,
            DisplayState = BatteryDisplayState.NA
        };
    }

    private void RestoreProbeState(DeviceItemViewModel item, DeviceBatterySnapshot snapshot)
    {
        if (_probeStateByAddress.TryGetValue(snapshot.Address, out var probeState))
        {
            if (!probeState.IsRunning && snapshot.BatteryPercent is not null)
            {
                _probeStateByAddress.Remove(snapshot.Address);
                item.RestoreProbeState(false, 0, string.Empty);
                return;
            }

            item.RestoreProbeState(probeState.IsRunning, probeState.Progress, probeState.Status);
            return;
        }

        item.RestoreProbeState(false, 0, string.Empty);
    }

    private void CleanupProbeStateCache(DateTime nowUtc)
    {
        var staleKeys = _probeStateByAddress
            .Where(pair => ShouldExpireProbeState(pair.Value.IsRunning, pair.Value.LastUpdatedUtc, nowUtc, ProbeStateTtl))
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _probeStateByAddress.Remove(key);
        }

        var knownAddresses = Devices
            .Select(device => device.Address)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var autoProbeKeys = _autoProbeNextAllowedByAddress
            .Where(pair => !knownAddresses.Contains(pair.Key) || pair.Value <= nowUtc)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in autoProbeKeys)
        {
            _autoProbeNextAllowedByAddress.Remove(key);
            _autoProbeFailureCountByAddress.Remove(key);
            _pendingProbeFollowUpCountByAddress.Remove(key);
            _lastProbeFailureUiByAddress.Remove(key);
            _missingDeviceSinceByAddress.Remove(key);
            _missingDeviceMissCountByAddress.Remove(key);
            _pendingBatteryDropByAddress.Remove(key);
        }
    }

    internal static bool ShouldExpireProbeState(bool isRunning, DateTime updatedAtUtc, DateTime nowUtc, TimeSpan ttl)
    {
        if (isRunning)
        {
            return false;
        }

        if (updatedAtUtc == DateTime.MinValue)
        {
            return true;
        }

        return nowUtc - updatedAtUtc >= ttl;
    }

    private void SetProbeState(string address, bool isRunning, int progress, string status, bool forceUiStamp = false)
    {
        var nowUtc = DateTime.UtcNow;
        var uiStamp = nowUtc;

        if (!forceUiStamp &&
            _probeStateByAddress.TryGetValue(address, out var existing) &&
            !isRunning)
        {
            uiStamp = existing.LastUiPushAtUtc;
        }

        _probeStateByAddress[address] = new ProbeUiState(
            IsRunning: isRunning,
            Progress: Math.Clamp(progress, 0, 100),
            Status: status ?? string.Empty,
            LastUpdatedUtc: nowUtc,
            LastUiPushAtUtc: uiStamp);
    }

    private void ApplyProbeProgress(string address, ProbeProgress progress)
    {
        MarkActivity();

        var nowUtc = DateTime.UtcNow;
        var isTerminal = progress.Stage is ProbeStage.Completed or ProbeStage.Failed;
        if (_probeStateByAddress.TryGetValue(address, out var current))
        {
            var unchanged = progress.Percent <= current.Progress &&
                            string.Equals(progress.Status, current.Status, StringComparison.Ordinal);
            var tooFrequent = nowUtc - current.LastUiPushAtUtc < ProbeProgressUiThrottle;

            if (!isTerminal && unchanged && tooFrequent)
            {
                _probeStateByAddress[address] = current with { LastUpdatedUtc = nowUtc };
                return;
            }
        }

        _probeStateByAddress[address] = new ProbeUiState(
            IsRunning: !isTerminal,
            Progress: progress.Percent,
            Status: progress.Status ?? string.Empty,
            LastUpdatedUtc: nowUtc,
            LastUiPushAtUtc: nowUtc);

        var item = Devices.FirstOrDefault(device => string.Equals(device.Address, address, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        item.UpdateProbeProgress(progress.Percent, progress.Status ?? string.Empty);
    }

    private void ApplyProbeActionAvailability()
    {
        foreach (var item in Devices)
        {
            item.SetProbeActionEnabled(!_isAnyProbeRunning || item.IsProbing);
        }
    }

    private void PostToUi(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Post(_ => action(), null);
    }

    private void MonitorPerformance()
    {
        try
        {
            _ownProcess.Refresh();
            var now = DateTime.UtcNow;
            var idleMode = IsIdleMode(now);
            UpdatePerformanceSamplingInterval(idleMode);

            var currentCpu = _ownProcess.TotalProcessorTime;
            var elapsedMilliseconds = (now - _lastPerfSampleAtUtc).TotalMilliseconds;
            if (elapsedMilliseconds <= 1)
            {
                return;
            }

            var cpuMilliseconds = (currentCpu - _lastCpuSample).TotalMilliseconds;
            var cpuPercent = Math.Max(0d, cpuMilliseconds / (elapsedMilliseconds * Environment.ProcessorCount) * 100.0d);
            var ramMb = _ownProcess.WorkingSet64 / (1024d * 1024d);
            var privateMb = _ownProcess.PrivateMemorySize64 / (1024d * 1024d);

            _lastCpuSample = currentCpu;
            _lastPerfSampleAtUtc = now;

            if (ramMb > RamTargetMb && now - _lastTrimAtUtc > TimeSpan.FromSeconds(20))
            {
                ProcessMemoryTrimmer.TryTrim(_ownProcess);
                _ownProcess.Refresh();
                ramMb = _ownProcess.WorkingSet64 / (1024d * 1024d);
                privateMb = _ownProcess.PrivateMemorySize64 / (1024d * 1024d);
                _lastTrimAtUtc = now;
            }

            var shouldRunIdleTrim = idleMode &&
                                    now - _lastActivityAtUtc >= IdleManagedTrimDelay &&
                                    now - _lastManagedTrimAtUtc >= ManagedTrimMinInterval;

            var shouldRunThresholdTrim = ManagedTrimPolicy.ShouldRunManagedTrim(
                privateMb,
                PrivateTrimTargetMb,
                now,
                _lastManagedTrimAtUtc,
                ManagedTrimMinInterval);

            if (shouldRunIdleTrim || shouldRunThresholdTrim)
            {
                ProcessMemoryTrimmer.TryManagedTrim(_ownProcess);
                _ownProcess.Refresh();
                ramMb = _ownProcess.WorkingSet64 / (1024d * 1024d);
                privateMb = _ownProcess.PrivateMemorySize64 / (1024d * 1024d);
                _lastManagedTrimAtUtc = now;
            }

            _lastCpuPercent = cpuPercent;
            _lastRamMb = ramMb;
            _lastPrivateMb = privateMb;
            PerformanceText = BuildPerformanceText(cpuPercent, ramMb, privateMb);

            if (cpuPercent > CpuTargetPercent || ramMb > RamTargetMb || privateMb > PrivateTrimTargetMb)
            {
                _limitExceededStreak++;
            }
            else
            {
                _limitExceededStreak = 0;
            }

            if (_limitExceededStreak >= 3 && !IsLiteVisualMode)
            {
                SetVisualMode(WidgetSettings.LiteGlassMode);
                StatusText = CurrentLanguageText.PerformanceModeLiteStatus;
                _limitExceededStreak = 0;
            }
        }
        catch
        {
            // Ignore sampling failures.
        }
    }

    private bool IsIdleMode(DateTime nowUtc)
    {
        if (_isAnyProbeRunning || _isRefreshRunning)
        {
            return false;
        }

        return nowUtc - _lastActivityAtUtc >= IdleGracePeriod;
    }

    private void UpdatePerformanceSamplingInterval(bool idleMode)
    {
        var target = idleMode ? IdlePerfInterval : ActivePerfInterval;
        if (_performanceTimer.Interval != target)
        {
            _performanceTimer.Interval = target;
        }
    }

    private void MarkActivity()
    {
        _lastActivityAtUtc = DateTime.UtcNow;
    }

    private string? SelectAutoProbeTarget(DateTime nowUtc)
    {
        if (_isAnyProbeRunning)
        {
            return null;
        }

        foreach (var item in Devices)
        {
            if (!item.IsProbeEligible || item.IsProbing)
            {
                continue;
            }

            if (item.BatteryPercent is not null)
            {
                continue;
            }

            var address = AddressNormalizer.NormalizeAddress(item.Address);
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            if (!_lastConnectedDevicesByAddress.ContainsKey(address))
            {
                continue;
            }

            if (_autoProbeNextAllowedByAddress.TryGetValue(address, out var nextAllowed) &&
                nextAllowed > nowUtc)
            {
                continue;
            }

            return address;
        }

        return null;
    }

    private void StartAutoProbe(string address)
    {
        RegisterAutoProbeAttempt(address, DateTime.UtcNow);
        _ = ProbeUnsupportedGamepadAsync(address);
    }

    private void RegisterAutoProbeAttempt(string address, DateTime nowUtc)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _autoProbeNextAllowedByAddress[normalized] = nowUtc + AutoProbeCooldown;
    }

    private void RegisterAutoProbeSuccess(string address, DateTime nowUtc)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _autoProbeFailureCountByAddress.Remove(normalized);
        _lastProbeFailureUiByAddress.Remove(normalized);
        _autoProbeNextAllowedByAddress[normalized] = nowUtc + AutoProbeCooldown;
    }

    private void RegisterAutoProbePending(string address, DateTime nowUtc)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _autoProbeFailureCountByAddress.Remove(normalized);
        _autoProbeNextAllowedByAddress[normalized] = nowUtc + PendingProbeFollowUpDelay;
    }

    private void RegisterAutoProbeFailure(string address, DateTime nowUtc, ProbeResult? failureResult)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var failureCount = _autoProbeFailureCountByAddress.TryGetValue(normalized, out var existing)
            ? existing + 1
            : 1;
        _autoProbeFailureCountByAddress[normalized] = failureCount;

        var backoff = ComputeAutoProbeBackoff(AutoProbeCooldown, failureCount, AutoProbeMaxBackoffExponent);
        backoff = ApplyNoSignalFailureBackoff(backoff, IsNoSignalProbeFailure(failureResult));
        backoff = ApplyWeakSignalFailureBackoff(backoff, IsWeakSignalProbeFailure(failureResult));
        backoff = ApplyBackoffJitter(backoff, normalized, failureCount);

        _autoProbeNextAllowedByAddress[normalized] = nowUtc + backoff;
    }

    private bool TrySchedulePendingProbeFollowUp(string address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var nextCount = _pendingProbeFollowUpCountByAddress.TryGetValue(normalized, out var existing)
            ? existing + 1
            : 1;
        _pendingProbeFollowUpCountByAddress[normalized] = nextCount;
        return nextCount <= MaxPendingProbeFollowUps;
    }

    private void ClearPendingProbeFollowUp(string address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _pendingProbeFollowUpCountByAddress.Remove(normalized);
    }

    private void StartPendingProbeFollowUp(string address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(PendingProbeFollowUpDelay).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (_disposed)
            {
                return;
            }

            PostToUi(() => _ = ProbeUnsupportedGamepadAsync(normalized));
        });
    }

    private bool ShouldSuppressFailureUiUpdate(string address, string failureUiKey, DateTime nowUtc)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (!_lastProbeFailureUiByAddress.TryGetValue(normalized, out var existing))
        {
            _lastProbeFailureUiByAddress[normalized] = new ProbeFailureUiState(failureUiKey, nowUtc);
            return false;
        }

        var sameFailure = string.Equals(existing.Message, failureUiKey, StringComparison.Ordinal);
        var withinDebounce = nowUtc - existing.TimestampUtc < ProbeFailureUiDebounce;
        if (sameFailure && withinDebounce)
        {
            return true;
        }

        _lastProbeFailureUiByAddress[normalized] = new ProbeFailureUiState(failureUiKey, nowUtc);
        return false;
    }

    private static string BuildProbeFailureUiKey(ProbeResult result)
    {
        var baseMessage = string.IsNullOrWhiteSpace(result.Message)
            ? BuildProbeFailureStatus(result)
            : result.Message.Trim();
        var stage = result.ErrorDetail?.Stage ?? ProbeStage.None;
        var failureKind = result.ErrorDetail?.FailureKind ?? ProbeFailureKind.Unknown;
        var exceptionType = result.ErrorDetail?.ExceptionType ?? string.Empty;
        var blockReason = result.ErrorDetail?.BlockReason ?? string.Empty;
        var suppressionReason = result.ErrorDetail?.SuppressionReason ?? string.Empty;
        return $"{baseMessage}|{stage}|{failureKind}|{exceptionType}|{blockReason}|{suppressionReason}";
    }

    private static bool IsNoSignalProbeFailure(ProbeResult? result)
    {
        if (result is null || result.Success || result.ErrorDetail is null)
        {
            return false;
        }

        if (result.ErrorDetail.FailureKind == ProbeFailureKind.NoSignal)
        {
            return true;
        }

        if (result.ErrorDetail.ReadSuccessCount > 0)
        {
            return false;
        }

        var text = $"{result.Message} {result.ErrorDetail.DiagnosticsText}";
        return text.IndexOf("bestScore 0", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsWeakSignalProbeFailure(ProbeResult? result)
    {
        if (result is null || result.Success || result.ErrorDetail is null)
        {
            return false;
        }

        if (result.ErrorDetail.FailureKind == ProbeFailureKind.WeakSignal)
        {
            return true;
        }

        if (result.ErrorDetail.ReadSuccessCount <= 0)
        {
            return false;
        }

        var text = $"{result.Message} {result.ErrorDetail.DiagnosticsText}";
        return text.IndexOf("bestScore 0", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static TimeSpan ComputeAutoProbeBackoff(TimeSpan baseCooldown, int failureCount, int maxExponent)
    {
        if (baseCooldown <= TimeSpan.Zero || failureCount <= 1)
        {
            return baseCooldown;
        }

        var exponent = Math.Clamp(failureCount - 1, 0, Math.Max(0, maxExponent));
        var factor = Math.Pow(2d, exponent);
        return TimeSpan.FromSeconds(Math.Min(baseCooldown.TotalSeconds * factor, 3600d));
    }

    internal static TimeSpan ApplyNoSignalFailureBackoff(TimeSpan backoff, bool isNoSignal)
    {
        if (!isNoSignal)
        {
            return backoff;
        }

        var boostedSeconds = Math.Min(backoff.TotalSeconds * 2d, 3600d);
        var boosted = TimeSpan.FromSeconds(boostedSeconds);
        return boosted < NoSignalFailureMinCooldown ? NoSignalFailureMinCooldown : boosted;
    }

    internal static TimeSpan ApplyWeakSignalFailureBackoff(TimeSpan backoff, bool isWeakSignal)
    {
        if (!isWeakSignal)
        {
            return backoff;
        }

        var boostedSeconds = Math.Min(backoff.TotalSeconds * 1.35d, 3600d);
        var boosted = TimeSpan.FromSeconds(boostedSeconds);
        return boosted < WeakSignalFailureMinCooldown ? WeakSignalFailureMinCooldown : boosted;
    }

    internal static TimeSpan ApplyBackoffJitter(TimeSpan backoff, string key, int failureCount)
    {
        if (backoff <= TimeSpan.Zero || string.IsNullOrWhiteSpace(key))
        {
            return backoff;
        }

        var seed = HashCode.Combine(key.ToUpperInvariant(), failureCount);
        var normalized = Math.Abs(seed % 1000) / 1000d;
        var factor = 0.88d + (normalized * 0.24d);
        var jitteredSeconds = Math.Clamp(backoff.TotalSeconds * factor, 1d, 3600d);
        return TimeSpan.FromSeconds(jitteredSeconds);
    }

    public UiLanguageText CurrentLanguageText => UiLanguageCatalog.Get(Settings.Language);

    private string BuildPerformanceText(double cpuPercent, double ramMb, double privateMb)
    {
        var visualModeText = IsLiteVisualMode ? CurrentLanguageText.VisualModeLite : CurrentLanguageText.VisualModeNormal;
        return $"CPU {cpuPercent:0.00}% | RAM {ramMb:0.0} MB | Private {privateMb:0.0} MB | {visualModeText}";
    }

    private void RefreshLocalizedSummaryTexts()
    {
        DiagnosticsText = UiLanguageCatalog.BuildDiagnosticsSummary(
            Settings.Language,
            _connectedCount,
            _batteryReadCount,
            _matchedCount,
            _naCount);

        if (_lastCpuPercent > 0 || _lastRamMb > 0 || _lastPrivateMb > 0)
        {
            PerformanceText = BuildPerformanceText(_lastCpuPercent, _lastRamMb, _lastPrivateMb);
        }
    }

    private void RaiseLocalizedTextPropertyChanges()
    {
        OnPropertyChanged(nameof(TextSettingsTitle));
        OnPropertyChanged(nameof(TextAutostart));
        OnPropertyChanged(nameof(TextCloseToTray));
        OnPropertyChanged(nameof(TextGuidedProbe));
        OnPropertyChanged(nameof(GuideSoundEnabled));
        OnPropertyChanged(nameof(GuideSoundId));
        OnPropertyChanged(nameof(CustomGuideSoundPath));
        OnPropertyChanged(nameof(TextAggressivePolicy));
        OnPropertyChanged(nameof(TextUiScale));
        OnPropertyChanged(nameof(TextColorPreset));
        OnPropertyChanged(nameof(TextEnvironmentSettingsGroup));
        OnPropertyChanged(nameof(TextCustomizeSettingsGroup));
        OnPropertyChanged(nameof(TextLabsSettingsGroup));
        OnPropertyChanged(nameof(TextStartMinimizedToTray));
        OnPropertyChanged(nameof(TextGuideSound));
        OnPropertyChanged(nameof(TextColorCustomize));
        OnPropertyChanged(nameof(TextGuideSoundSelectorTooltip));
        OnPropertyChanged(nameof(TextGuideSoundPreviewTooltip));
        OnPropertyChanged(nameof(TextUserFont));
        OnPropertyChanged(nameof(TextLoadCustomFont));
        OnPropertyChanged(nameof(TextResetCustomFont));
        OnPropertyChanged(nameof(TextLanguage));
        OnPropertyChanged(nameof(TextCustomGuideSound));
        OnPropertyChanged(nameof(TextColorSelect));
        OnPropertyChanged(nameof(TextColorTargetPrimaryText));
        OnPropertyChanged(nameof(TextColorTargetSecondaryText));
        OnPropertyChanged(nameof(TextColorTargetBatteryText));
        OnPropertyChanged(nameof(TextColorTargetWidgetBackground));
        OnPropertyChanged(nameof(TextColorTargetGlassSurface));
        OnPropertyChanged(nameof(TextColorTargetCardTint));
        OnPropertyChanged(nameof(TextColorTargetCardBorder));
        OnPropertyChanged(nameof(TextColorTargetTrack));
        OnPropertyChanged(nameof(TextColorTargetPanel));
        OnPropertyChanged(nameof(TextColorTargetSettingsText));
        OnPropertyChanged(nameof(TextSettingsTextStyle));
        OnPropertyChanged(nameof(TextSettingsTextFontSize));
        OnPropertyChanged(nameof(TextSettingsTextBold));
        OnPropertyChanged(nameof(TextSettingsTextReset));
        OnPropertyChanged(nameof(TextColorReset));
        OnPropertyChanged(nameof(TextColorDragHint));
        OnPropertyChanged(nameof(TextGuideSoundPreviewStopTooltip));
        OnPropertyChanged(nameof(TextTrayResetPosition));
        OnPropertyChanged(nameof(TextCustomGuideSoundSelectTitle));
        OnPropertyChanged(nameof(TextCustomGuideSoundInvalid));
        OnPropertyChanged(nameof(TextCustomFontSelectTitle));
        OnPropertyChanged(nameof(TextCustomFontLoadFailed));
        OnPropertyChanged(nameof(TextPicoFirmwareButton));
        OnPropertyChanged(nameof(TextPicoFirmwareReady));
        OnPropertyChanged(nameof(TextRefreshNow));
        OnPropertyChanged(nameof(TextDeveloperContact));
        OnPropertyChanged(nameof(TextSupport));
        OnPropertyChanged(nameof(TextUpdate));
        OnPropertyChanged(nameof(TextExit));
        OnPropertyChanged(nameof(TextStatusPanelToggleTooltip));
        OnPropertyChanged(nameof(TextStatusPanelCollapseTooltip));
        OnPropertyChanged(nameof(TextSettingsTooltip));
        OnPropertyChanged(nameof(TextCloseTooltip));
        OnPropertyChanged(nameof(TextTrayOpenWidget));
        OnPropertyChanged(nameof(TextTrayRefreshNow));
        OnPropertyChanged(nameof(TextTrayExit));
        OnPropertyChanged(nameof(TextVersionPrefix));
        OnPropertyChanged(nameof(TextIconChange));
        OnPropertyChanged(nameof(TextRestoreDefault));
        OnPropertyChanged(nameof(TextInlineCancel));
        OnPropertyChanged(nameof(TextInlineSave));
    }

    internal static string BuildProbeFailureStatus(ProbeResult result, string? language = null)
    {
        var localized = UiLanguageCatalog.Get(language);
        var baseMessage = string.IsNullOrWhiteSpace(result.Message)
            ? localized.ProbeFailureDefault
            : result.Message.Trim();

        if (result.ErrorDetail is null)
        {
            return baseMessage;
        }

        var detail = result.ErrorDetail;
        var stage = detail.Stage == ProbeStage.None ? "unknown" : detail.Stage.ToString();
        var exceptionType = string.IsNullOrWhiteSpace(detail.ExceptionType) ? "none" : detail.ExceptionType;
        var line2 = $"stage: {stage} | ex: {exceptionType}";
        var line3 = $"openOk={detail.OpenSuccessCount}, openFail={detail.OpenFailureCount}, readOk={detail.ReadSuccessCount}, readFail={detail.ReadFailureCount}";
        return $"{baseMessage}\n{line2}\n{line3}";
    }

    private static void AppendProbeErrorLog(string address, string displayName, ProbeResult result)
    {
        if (result.Success)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(ProbeErrorLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var detail = result.ErrorDetail;
            var stage = detail?.Stage.ToString() ?? "None";
            var failureKind = detail?.FailureKind.ToString() ?? "Unknown";
            var exceptionType = detail?.ExceptionType ?? string.Empty;
            var exceptionMessage = detail?.ExceptionMessage ?? string.Empty;
            var diagnostics = detail?.DiagnosticsText ?? string.Empty;
            var context = detail?.Context ?? string.Empty;
            var timestamp = detail?.Timestamp ?? DateTimeOffset.Now;
            var exceptionToken = string.IsNullOrWhiteSpace(exceptionType)
                ? "-"
                : $"{SanitizeSingleLine(exceptionType)}:{SanitizeSingleLine(exceptionMessage)}";

            var line = new StringBuilder();
            line.Append('[').Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz")).Append("] ");
            line.Append("address=").Append(SanitizeSingleLine(address)).Append(' ');
            line.Append("device=").Append(SanitizeSingleLine(displayName)).Append(' ');
            line.Append("stage=").Append(SanitizeSingleLine(stage)).Append(' ');
            line.Append("failureKind=").Append(SanitizeSingleLine(failureKind)).Append(' ');
            line.Append("message=").Append(SanitizeSingleLine(result.Message)).Append(' ');
            line.Append("exception=").Append(exceptionToken).Append(' ');
            line.Append("diag=").Append(SanitizeSingleLine(diagnostics)).Append(' ');
            line.Append("context=").Append(SanitizeSingleLine(context));

            File.AppendAllText(ProbeErrorLogPath, line.ToString().TrimEnd() + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private static string SanitizeSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record ProbeUiState(
        bool IsRunning,
        int Progress,
        string Status,
        DateTime LastUpdatedUtc,
        DateTime LastUiPushAtUtc);

    private sealed record ProbeFailureUiState(
        string Message,
        DateTime TimestampUtc);

    private sealed record PendingBatteryDropState(
        int CandidatePercent,
        int Confirmations,
        DateTimeOffset FirstObservedAt);
}
