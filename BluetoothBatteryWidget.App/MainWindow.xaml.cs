using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;
using WpfControls = System.Windows.Controls;
using WpfPopup = System.Windows.Controls.Primitives.Popup;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfThumb = System.Windows.Controls.Primitives.Thumb;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace BluetoothBatteryWidget.App;

public partial class MainWindow : Window
{
    private const double CompactHeightThreshold = 430d;
    private const double StartupSafetyMargin = 8d;
    private const double StartupMaxWorkAreaRatio = 0.85d;
    private const double StartupDefaultWidth = 460d;
    private const double StartupDefaultHeight = 420d;
    private const double StartupMaxAbsoluteWidth = 680d;
    private const double StartupMaxAbsoluteHeight = 760d;
    private const double BatteryGuideTriggerCaptureDesignWidth = 1448d;
    private const double BatteryGuideTriggerCaptureDesignHeight = 1086d;
    private const double BatteryGuideTriggerCaptureMaxWorkAreaRatio = 0.78d;
    private const double BatteryGuideTriggerCaptureMaxScale = 0.88d;
    private static readonly byte[] BatteryGuideChimeWave = BatteryGuideChimeAudio.LoadWave();
    private const double UiScaleStepFactor = 0.08d;
    private const int UiScaleAnimationMilliseconds = 140;
    private const int SettingsAccordionAnimationMilliseconds = 230;
    private const double ResizeGripBaseInset = 4d;
    private const double StatusPanelFallbackHeight = 108d;
    private const double ColorPresetMarqueeGap = 14d;
    private const double ColorPresetMarqueePixelsPerSecond = 22d;
    private const double ColorPresetMarqueeStartDelaySeconds = 0.8d;
    private const double ColorPresetMarqueeEndDelaySeconds = 0.9d;
    private static readonly TimeSpan GuideSoundPreviewSafetyTimeout = TimeSpan.FromMinutes(10);
    private const string GuideSoundPreviewPlayText = "▶";
    private const string GuideSoundPreviewStopText = "■";
    private const string GlassWaveStoryboardKey = "GlassWaveStoryboard";
    private const string DeveloperContactEmail = "lamsaiku65@gmail.com";
    private const string SupportUrl = "https://ko-fi.com/dukduk";
    private const string AppDisplayName = "Bloss";
    private static readonly TimeSpan GuideButtonGlobalDebounce = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan SteamGuideButtonToastCooldown = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan SteamGuideToastConnectionSuppressDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SteamGuideToastRefreshSuppressDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CustomBatteryGuideTriggerToastCooldown = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan SteamCustomBatteryGuideTriggerToastCooldown = TimeSpan.FromMilliseconds(3000);
    private static readonly TimeSpan GamepadActivityDiagnosticInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GamepadActivityRefreshCooldown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DisplayWakePulseCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DisplayOffSettleWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TelemetryPowerIdleUpdateCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NormalGamepadMonitoringGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DisplayWakeRecoveryDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VerifiedInputWakeRecoveryBypassDuration = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan DisplayWakeGuideToastHold = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan SteamRawInputPreferredWindow = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SteamSecondaryFallbackDelay = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan SteamSecondaryFallbackRawHidRecheckDelay = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan SteamSecondaryFallbackRawHidShortTapMaximumWait = TimeSpan.FromMilliseconds(550);
    private static readonly TimeSpan SteamSecondaryFallbackRawHidAmbiguousMaximumWait = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan SteamSecondaryFallbackRawHidHoldSuppressDuration = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan SteamSecondaryFallbackRawHidActivePressFreshWindow = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan SteamSecondaryFallbackRawHidStaleStateAge = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan SteamSecondaryFallbackRawHidPreExistingHoldAge = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan SteamSecondaryFallbackBurstWindow = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan GuideButtonInputReportFallbackSuppressDuration = TimeSpan.FromMilliseconds(1200);
    private const string Ds5DongleLatestReleaseApiUrl = "https://api.github.com/repos/awalol/DS5Dongle/releases/latest";
    private const string Ds5DongleReleasePageUrl = "https://github.com/awalol/DS5Dongle/releases";
    private const long Ds5DongleMaxFirmwareBytes = 16L * 1024 * 1024;
    private static readonly string CustomIconDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "icon-images");
    private static readonly string CustomFontDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "fonts");
    private static readonly string CustomGuideSoundDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "guide-sounds");
    private const string SettingsTitleBrushResourceKey = "SettingsTitleBrush";
    private const string SettingsTextBrushResourceKey = "SettingsTextBrush";
    private static readonly System.Windows.Media.Color FixedSettingsTitleColor =
        System.Windows.Media.Color.FromRgb(0x1D, 0x3E, 0x5B);
    private static readonly System.Windows.Media.Color FixedSettingsTextColor =
        System.Windows.Media.Color.FromRgb(0x35, 0x61, 0x7F);

    private readonly MainViewModel _viewModel;
    private readonly TrayIconService _trayIconService;
    private readonly DrawingIcon _appIcon;

    private bool _isExiting;
    private bool _forceClose;
    private DeviceItemViewModel? _renamingItem;
    private DeviceItemViewModel? _iconEditingItem;
    private bool _isCompactMode;
    private bool _initialBoundsApplied;
    private bool _startHiddenInTrayOnLoad;
    private bool _releaseNotesChecked;
    private bool _isColorPresetSyncing;
    private bool _isLanguageSyncing;
    private bool _isGuideSoundSyncing;
    private bool _isPowerIdlePauseSyncing;
    private bool _isWindowsDisplayOffSyncing;
    private bool _isPaletteDragging;
    private WpfPopup? _draggingPopup;
    private FrameworkElement? _popupDragChrome;
    private System.Windows.Point _popupDragStartScreenPoint;
    private double _popupDragStartHorizontalOffset;
    private double _popupDragStartVerticalOffset;
    private string _selectedColorElementKey = "PrimaryText";
    private double _statusPanelCollapsedHeightDelta;
    private DateTime _lastBoundsSaveAt = DateTime.MinValue;
    private Storyboard? _glassWaveStoryboard;
    private readonly HttpClient _httpClient = new();
    private readonly UpdateService _updateService;
    private readonly GuideButtonMonitorService _guideButtonMonitor = new();
    private readonly SteamControllerRawInputMonitorService _steamRawInputMonitor = new();
    private readonly XInputActivityMonitorService _xInputActivityMonitor = new();
    private readonly DisplayPowerCoordinator _displayPowerCoordinator = new();
    private readonly DisplayIdleCoordinator _displayIdleCoordinator = new();
    private readonly GamepadPresenceService _gamepadPresenceService = new();
    private readonly BatteryGuideChimePlayer _batteryGuideChimePlayer = new(BatteryGuideChimeWave);
    private readonly System.Windows.Threading.DispatcherTimer _guideSoundPreviewResetTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer _settingsAutoCloseTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer _powerIdleMonitorTimer = new();
    private readonly Dictionary<string, System.Windows.Threading.DispatcherTimer> _batteryGuideHideTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastGuideButtonToastByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastSteamRawGuideButtonByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastCustomBatteryGuideTriggerToastByBinding = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _lastBatteryGuideTriggerReportByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _lastPowerIdleInputReportByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _lastDefaultGuideButtonPressedByInputReport = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _defaultGuideNeutralReportCountByInputReport = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _defaultGuidePressedReportKeysByToastKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _customBatteryGuideTriggerPressedReportKeysByBinding = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingSteamSecondaryGuideFallback> _pendingSteamSecondaryGuideFallbackByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _steamSecondaryGuideFallbackBlockedUntilByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _inputReportGuideFallbackSuppressRegularUntilByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FrameworkElement, int> _settingsAccordionAnimationTokens = new();
    private readonly Dictionary<WpfControls.TextBlock, (double FontSize, FontWeight FontWeight)> _settingsTextBlockDefaults = new();
    private readonly Dictionary<WpfControls.Control, (double FontSize, FontWeight FontWeight)> _settingsControlTextDefaults = new();
    private readonly object _guideButtonToastSync = new();
    private readonly HashSet<string> _lowBatteryToastKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _batteryAlertInitializedDeviceKeys = new(StringComparer.OrdinalIgnoreCase);
    private BatteryToastWindow? _activeBatteryToastWindow;
    private LabsWindow? _labsWindow;
    private HwndSource? _windowMessageSource;
    private IntPtr _steamRawInputWindowHandle;
    private bool _isGuideSoundPreviewPlaying;
    private bool _isUpdating;
    private bool _isPicoFirmwareUpdating;
    private bool _isBatteryGuideTriggerCaptureActive;
    private bool _batteryGuideTriggerSelectMouseToggleRequested;
    private readonly Dictionary<string, DateTimeOffset> _lastGamepadActivityDiagnosticAtUtcByKind = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _lastGamepadActivityRefreshRequestedAtUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDisplayWakePulseAtUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDisplayOffStateAtUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastTelemetryPowerIdleUpdateAtUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _wakeRecoveryUntilUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _verifiedInputWakeRecoveryBypassUntilUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _pendingDisplayWakeGuideToastUntilUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _steamGuideToastsSuppressedUntilUtc =
        DateTimeOffset.UtcNow.Add(SteamGuideToastConnectionSuppressDuration);
    private DateTimeOffset _normalGamepadMonitoringAllowedUntilUtc = DateTimeOffset.MinValue;
    private PowerIdleRuntimeMode _powerIdleMode = PowerIdleRuntimeMode.Active;
    private BatteryAlertThresholdsWindow? _batteryAlertThresholdsWindow;
    private BatteryGuideTriggerCaptureWindow? _batteryGuideTriggerCaptureWindow;
    private IconOverrideWindow? _iconOverrideWindow;
    private BatteryGuideTrigger? _pendingBatteryGuideTriggerCapture;
    private GuideButtonPressedEventArgs? _pendingDisplayWakeGuideToast;
    private string? _batteryGuideTriggerCaptureKey;

    private static readonly HashSet<string> ColorElementKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PrimaryText",
        "SecondaryText",
        "BatteryText",
        "WidgetBackground",
        "GlassSurface",
        "CardTint",
        "CardBorder",
        "Track",
        "Panel",
        "SettingsText"
    };

    public MainWindow(MainViewModel viewModel)
    {
        RuntimeDiagnostics.ConfigureForProcess(Environment.ProcessPath);
        _viewModel = viewModel;
        DataContext = _viewModel;
        _updateService = new UpdateService(_httpClient, AppDisplayName, GetDisplayVersion);

        InitializeComponent();
        ColorPresetComboBox.ItemsSource = ColorPresetCatalog.Presets;
        LanguageComboBox.ItemsSource = _viewModel.LanguageOptions;
        LanguageComboBox.DisplayMemberPath = nameof(UiLanguageOption.Label);
        PowerIdlePauseComboBox.ItemsSource = _viewModel.PowerIdlePauseOptions;
        PowerIdlePauseComboBox.DisplayMemberPath = nameof(PowerIdlePauseOption.Label);
        WindowsDisplayOffComboBox.ItemsSource = _viewModel.WindowsDisplayOffOptions;
        WindowsDisplayOffComboBox.DisplayMemberPath = nameof(WindowsDisplayOffOption.Label);
        GuideSoundComboBox.ItemsSource = BatteryGuideSoundCatalog.GetGuideOptions(_viewModel.CustomGuideSoundPath, _viewModel.Language);
        GuideSoundComboBox.DisplayMemberPath = nameof(BatteryGuideSoundOption.DisplayName);
        _appIcon = LoadAppIcon();
        _trayIconService = BuildTrayIconService();
        RefreshTrayMenuTexts();
        UpdateVersionMenuHeader();
        SyncGuideSoundSelection();
        UpdateGuideSoundControls();
        ResetUpdateProgressUi();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Devices.CollectionChanged += Devices_CollectionChanged;
        _guideButtonMonitor.GuideButtonPressed += GuideButtonMonitor_GuideButtonPressed;
        _guideButtonMonitor.InputReportReceived += GuideButtonMonitor_InputReportReceived;
        _guideButtonMonitor.InputActivityReceived += GuideButtonHidMonitor_InputActivityReceived;
        _guideButtonMonitor.SetKnownDeviceProvider(GetGuideButtonKnownDevices);
        _steamRawInputMonitor.GuideButtonPressed += GuideButtonMonitor_GuideButtonPressed;
        _steamRawInputMonitor.InputReportReceived += GuideButtonMonitor_InputReportReceived;
        _steamRawInputMonitor.InputActivityReceived += GuideButtonMonitor_InputActivityReceived;
        _steamRawInputMonitor.GlobalHumanInputReceived += SteamRawInputMonitor_GlobalHumanInputReceived;
        _steamRawInputMonitor.SteamRawHidBaselineReady += SteamRawInputMonitor_SteamRawHidBaselineReady;
        _steamRawInputMonitor.SetKnownDeviceProvider(GetGuideButtonKnownDevices);
        _xInputActivityMonitor.InputActivityReceived += XInputActivityMonitor_InputActivityReceived;
        SyncBatteryGuideTriggerInputInterests();
        _displayPowerCoordinator.StateChanged += DisplayPowerCoordinator_StateChanged;
        _batteryGuideChimePlayer.PlaybackEnded += BatteryGuideChimePlayer_PlaybackEnded;
        _guideSoundPreviewResetTimer.Tick += GuideSoundPreviewResetTimer_Tick;
        _settingsAutoCloseTimer.Interval = TimeSpan.FromMilliseconds(280);
        _settingsAutoCloseTimer.Tick += SettingsAutoCloseTimer_Tick;
        _powerIdleMonitorTimer.Interval = TimeSpan.FromSeconds(1);
        _powerIdleMonitorTimer.Tick += PowerIdleMonitorTimer_Tick;

        LocationChanged += (_, _) =>
        {
            SaveBoundsThrottled();
        };
        SizeChanged += (_, _) =>
        {
            SaveBoundsThrottled();
            UpdateCompactMode();
            UpdateGlassCardClip();
            UpdateResizeGripPlacement();
            UpdateDwmBlurBehindRegion();
        };

        if (GlassCard is not null)
        {
            GlassCard.SizeChanged += GlassCard_SizeChanged;
        }

        SourceInitialized += MainWindow_SourceInitialized;
    }

    public void PrepareStartHiddenInTray()
    {
        _startHiddenInTrayOnLoad = true;
        Opacity = 0d;
        ShowActivated = false;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowBounds();
        _initialBoundsApplied = true;
        UpdateCompactMode();
        UpdateGlassCardClip();
        UpdateDwmBlurBehindRegion();
        ApplyVisualModeState();
        await _viewModel.InitializeAsync().ConfigureAwait(true);
        SyncBatteryGuideTriggerInputInterests();
        ApplyColorPreset(_viewModel.ColorPresetId);
        ApplySettingsTextStyle();
        ApplyCustomFont();
        SyncColorPresetSelection();
        SyncLanguageSelection();
        SyncPowerIdlePauseSelection();
        SyncWindowsDisplayOffSelection();
        SyncGuideSoundSelection();
        UpdateGuideSoundControls();
        ApplyUiScaleStep(_viewModel.UiScaleStep, animate: false);
        RefreshTrayMenuTexts();
        UpdateVersionMenuHeader();
        UpdateResizeGripPlacement();
        UpdateDwmBlurBehindRegion();
        ApplyVisualModeState();
        _steamRawInputMonitor.LogRawDeviceSummary();
        ArmNormalGamepadMonitoring("startup");
        UpdatePowerIdleGuideMonitoring();
        _powerIdleMonitorTimer.Start();

        if (_startHiddenInTrayOnLoad)
        {
            _startHiddenInTrayOnLoad = false;
            Hide();
            WindowState = WindowState.Normal;
            Opacity = 1d;
            ShowActivated = true;
        }

        _ = Dispatcher.BeginInvoke(
            new Action(ShowReleaseNotesIfNeeded),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ShowReleaseNotesIfNeeded()
    {
        if (_releaseNotesChecked)
        {
            return;
        }

        _releaseNotesChecked = true;
        var version = GetDisplayVersion();
        var forceEveryRun = IsPortableTestExecutablePath(Environment.ProcessPath);
        if (!ShouldShowReleaseNotes(_viewModel.LastSeenReleaseNotesVersion, version, forceEveryRun))
        {
            return;
        }

        var releaseNotesWindow = new ReleaseNotesWindow(version, _viewModel.Language);
        releaseNotesWindow.ShowDialog();
        if (!forceEveryRun)
        {
            _viewModel.MarkReleaseNotesSeen(version);
        }
    }

    internal static bool ShouldShowReleaseNotes(string? lastSeenVersion, string? currentVersion, bool forceEveryRun)
    {
        if (forceEveryRun)
        {
            return true;
        }

        var normalizedCurrent = WidgetSettings.NormalizeReleaseNotesVersion(currentVersion);
        if (string.IsNullOrWhiteSpace(normalizedCurrent))
        {
            return false;
        }

        var normalizedSeen = WidgetSettings.NormalizeReleaseNotesVersion(lastSeenVersion);
        return !string.Equals(normalizedSeen, normalizedCurrent, StringComparison.Ordinal);
    }

    internal static bool IsPortableTestExecutablePath(string? processPath)
    {
        return RuntimeDiagnostics.IsPortableTestExecutablePath(processPath);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowBounds();

        if (_forceClose || !_viewModel.CloseToTrayEnabled)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Devices.CollectionChanged -= Devices_CollectionChanged;
        foreach (var item in _viewModel.Devices)
        {
            item.PropertyChanged -= DeviceItem_PropertyChanged;
        }

        _guideButtonMonitor.GuideButtonPressed -= GuideButtonMonitor_GuideButtonPressed;
        _guideButtonMonitor.InputReportReceived -= GuideButtonMonitor_InputReportReceived;
        _guideButtonMonitor.InputActivityReceived -= GuideButtonHidMonitor_InputActivityReceived;
        _guideButtonMonitor.Dispose();
        _steamRawInputMonitor.GuideButtonPressed -= GuideButtonMonitor_GuideButtonPressed;
        _steamRawInputMonitor.InputReportReceived -= GuideButtonMonitor_InputReportReceived;
        _steamRawInputMonitor.InputActivityReceived -= GuideButtonMonitor_InputActivityReceived;
        _steamRawInputMonitor.GlobalHumanInputReceived -= SteamRawInputMonitor_GlobalHumanInputReceived;
        _steamRawInputMonitor.SteamRawHidBaselineReady -= SteamRawInputMonitor_SteamRawHidBaselineReady;
        _steamRawInputMonitor.Dispose();
        _xInputActivityMonitor.InputActivityReceived -= XInputActivityMonitor_InputActivityReceived;
        _xInputActivityMonitor.Dispose();
        _displayPowerCoordinator.StateChanged -= DisplayPowerCoordinator_StateChanged;
        _displayPowerCoordinator.Dispose();
        _batteryGuideChimePlayer.PlaybackEnded -= BatteryGuideChimePlayer_PlaybackEnded;
        _windowMessageSource?.RemoveHook(MainWindow_WndProc);
        _windowMessageSource = null;
        _guideSoundPreviewResetTimer.Stop();
        _settingsAutoCloseTimer.Stop();
        _powerIdleMonitorTimer.Stop();
        StopBatteryGuideTimers();
        CloseActiveBatteryToast();
        _batteryAlertThresholdsWindow?.Close();
        _batteryAlertThresholdsWindow = null;
        CloseBatteryGuideTriggerCaptureWindow();
        _iconOverrideWindow?.Close();
        _iconOverrideWindow = null;
        _labsWindow?.Close();
        _labsWindow = null;
        _batteryGuideChimePlayer.Dispose();
        StopGlassWave();

        if (GlassCard is not null)
        {
            GlassCard.SizeChanged -= GlassCard_SizeChanged;
        }

        HideAndDisposeTrayIcon();
        _appIcon.Dispose();
        _httpClient.Dispose();
        _viewModel.Dispose();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DisplayNameTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        if (TryGetDeviceItem(sender, out var item))
        {
            BeginRename(item);
            e.Handled = true;
        }
    }

    private void NameEditorTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not WpfControls.TextBox textBox || textBox.Visibility != Visibility.Visible)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void NameEditorTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!TryGetDeviceItem(sender, out var item))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitRename(item);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelRename(item);
            e.Handled = true;
        }
    }

    private void NameSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetDeviceItem(sender, out var item))
        {
            CommitRename(item);
        }
    }

    private void NameCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetDeviceItem(sender, out var item))
        {
            CancelRename(item);
        }
    }

    private void NameResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDeviceItem(sender, out var item))
        {
            return;
        }

        _viewModel.RemoveNameOverride(item.Address);
        item.RestoreDefaultDisplayName();
        if (ReferenceEquals(_renamingItem, item))
        {
            _renamingItem = null;
        }
    }

    private void DeviceIconBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        if (!TryGetDeviceItem(sender, out var item))
        {
            return;
        }

        if (item.IsIconEditing)
        {
            CancelIconEdit(item);
            e.Handled = true;
            return;
        }

        BeginIconEdit(item);
        e.Handled = true;
    }

    private void IconPickInlineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDeviceItem(sender, out var item))
        {
            return;
        }

        // File picker opens as a separate window; close inline popup first to avoid overlap.
        CancelIconEdit(item);

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _viewModel.CurrentLanguageText.IconImageSelectTitle,
            Filter = _viewModel.CurrentLanguageText.IconImageFilter,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var adjustedImagePath = OpenIconImageAdjustDialog(dialog.FileName);
        if (string.IsNullOrWhiteSpace(adjustedImagePath))
        {
            return;
        }

        var persistedPath = PersistCustomIconImage(item.Address, adjustedImagePath);
        DeleteGeneratedTemporaryIcon(adjustedImagePath);
        if (string.IsNullOrWhiteSpace(persistedPath))
        {
            CancelIconEdit(item);
            return;
        }

        _viewModel.SetIconImageOverride(item.Address, persistedPath);
        item.ApplyCustomIconImagePath(persistedPath);
        if (ReferenceEquals(_iconEditingItem, item))
        {
            _iconEditingItem = null;
        }
    }

    private void IconResetInlineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDeviceItem(sender, out var item))
        {
            return;
        }

        _viewModel.RemoveIconImageOverride(item.Address);
        _viewModel.RemoveIconOverride(item.Address);
        item.RestoreDefaultIcon();
        if (ReferenceEquals(_iconEditingItem, item))
        {
            _iconEditingItem = null;
        }
    }

    private void IconCancelInlineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDeviceItem(sender, out var item))
        {
            return;
        }

        CancelIconEdit(item);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AnimateSettingsGearClick();

        if (IsSettingsPopupOpen())
        {
            CloseSettingsPopup();
            return;
        }

        OpenSettingsPopup();
    }

    private void EnvironmentSettingsGroupButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleSettingsAccordion(EnvironmentAccordionBody, EnvironmentAccordionArrow);
    }

    private void CustomizeSettingsGroupButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleSettingsAccordion(CustomizeAccordionBody, CustomizeAccordionArrow);
    }

    private void LabsSettingsGroupButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleSettingsAccordion(LabsAccordionBody, LabsAccordionArrow);
    }

    private void StatusPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var collapse = !_viewModel.StatusPanelCollapsed;
        if (collapse)
        {
            CloseSettingsPopup();
            _statusPanelCollapsedHeightDelta = Math.Max(StatusPanelFallbackHeight, GetStatusPanelOccupiedHeight());
        }

        _viewModel.SetStatusPanelCollapsed(collapse);
        Dispatcher.BeginInvoke(
            new Action(() => ApplyStatusPanelHeightAdjustment(collapse)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ResizeGripThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        var targetWidth = Math.Max(MinWidth, Width + e.HorizontalChange);
        var targetHeight = Math.Max(MinHeight, Height + e.VerticalChange);

        var workingAreas = GetWorkingAreas();
        if (workingAreas.Count > 0)
        {
            var current = new WindowBounds
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            };
            var area = SelectBestArea(current, workingAreas);
            var maxWidth = Math.Max(MinWidth, area.Width - StartupSafetyMargin);
            var maxHeight = Math.Max(MinHeight, area.Height - StartupSafetyMargin);
            targetWidth = Math.Min(targetWidth, maxWidth);
            targetHeight = Math.Min(targetHeight, maxHeight);
        }

        Width = targetWidth;
        Height = targetHeight;
        SaveBoundsThrottled();
    }

    private void AutostartToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAutostart(true);
    }

    private void AutostartToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAutostart(false);
    }

    private void StartMinimizedToTrayToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetStartMinimizedToTray(true);
    }

    private void StartMinimizedToTrayToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetStartMinimizedToTray(false);
    }

    private void CloseToTrayToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCloseToTray(true);
    }

    private void CloseToTrayToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetCloseToTray(false);
    }

    private void GuidedProbeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetGuidedProbeEnabled(true);
    }

    private void GuidedProbeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetGuidedProbeEnabled(false);
    }

    private void GuideSoundToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetGuideSoundEnabled(true);
        UpdateGuideSoundControls();
    }

    private void GuideSoundToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetGuideSoundEnabled(false);
        StopGuideSoundPreview();
    }

    private void GuideSoundComboBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (_isGuideSoundSyncing || GuideSoundComboBox.SelectedItem is not BatteryGuideSoundOption selected)
        {
            return;
        }

        _viewModel.SetGuideSoundId(selected.Id);
        StopGuideSoundPreview();
    }

    private void PowerIdlePauseComboBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (_isPowerIdlePauseSyncing || PowerIdlePauseComboBox.SelectedItem is not PowerIdlePauseOption selected)
        {
            return;
        }

        _viewModel.SetPowerIdlePauseMinutes(selected.Minutes);
        UpdatePowerIdleGuideMonitoring();
    }

    private void WindowsDisplayOffComboBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (_isWindowsDisplayOffSyncing || WindowsDisplayOffComboBox.SelectedItem is not WindowsDisplayOffOption selected)
        {
            return;
        }

        if (_viewModel.SetWindowsDisplayOffMinutes(selected.Minutes))
        {
            RefreshWindowsDisplayOffOptions();
            UpdatePowerIdleGuideMonitoring();
        }
        else
        {
            SyncWindowsDisplayOffSelection();
        }
    }

    private void BatteryAlertThresholdsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_batteryAlertThresholdsWindow is not null)
        {
            _batteryAlertThresholdsWindow.CloseWithPopOut();
            return;
        }

        var dialog = new BatteryAlertThresholdsWindow(
            _viewModel.BatteryAlertThresholds,
            BuildBatteryAlertDeviceOptions(),
            _viewModel.Language)
        {
            Owner = this,
            PopInOriginScreenPoint = TryGetElementCenterScreenPoint(BatteryAlertThresholdsButton)
        };
        dialog.Closed += BatteryAlertThresholdsWindow_Closed;
        _batteryAlertThresholdsWindow = dialog;
        PositionBatteryAlertThresholdsWindow(dialog);
        dialog.Show();
        dialog.Activate();
    }

    private void BatteryAlertThresholdsWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not BatteryAlertThresholdsWindow dialog)
        {
            return;
        }

        dialog.Closed -= BatteryAlertThresholdsWindow_Closed;
        if (ReferenceEquals(_batteryAlertThresholdsWindow, dialog))
        {
            _batteryAlertThresholdsWindow = null;
        }

        if (!dialog.WasAccepted)
        {
            return;
        }

        var before = _viewModel.BatteryAlertThresholds;
        _viewModel.SetBatteryAlertThresholds(dialog.SelectedThresholds);
        _viewModel.SetBatteryAlertDeviceEnabled(dialog.SelectedDeviceAlertSettings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase));
        if (!string.Equals(before, _viewModel.BatteryAlertThresholds, StringComparison.Ordinal) ||
            dialog.SelectedDeviceAlertSettings.Count > 0)
        {
            PrimeBatteryAlertToastKeysForCurrentLevels();
        }
    }

    private IReadOnlyList<BatteryAlertDeviceOption> BuildBatteryAlertDeviceOptions()
    {
        return _viewModel.Devices
            .Where(item =>
                item.IsConnected &&
                !item.IsStale &&
                !item.IsBatteryConnecting &&
                item.BatteryPercent is int)
            .Select(item =>
            {
                var key = BuildBatteryAlertDeviceSettingKey(item);
                return string.IsNullOrWhiteSpace(key)
                    ? null
                    : new BatteryAlertDeviceOption(
                        key,
                        item.DisplayName,
                        $"{item.BatteryPercent}%",
                        IsBatteryAlertEnabledForDeviceKey(key));
            })
            .Where(option => option is not null)
            .Cast<BatteryAlertDeviceOption>()
            .GroupBy(option => option.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private void PositionBatteryAlertThresholdsWindow(Window dialog)
    {
        var area = GetWorkingAreaForOwnerWindow();
        var width = Math.Min(
            ResolveDialogLength(dialog.Width, 500d),
            Math.Max(320d, area.Width - (StartupSafetyMargin * 2d)));
        var height = Math.Min(
            ResolveDialogLength(dialog.Height, 430d),
            Math.Max(260d, area.Height - (StartupSafetyMargin * 2d)));

        dialog.Width = Math.Floor(width);
        dialog.Height = Math.Floor(height);
        dialog.Left = area.Left + Math.Max(StartupSafetyMargin, (area.Width - dialog.Width) / 2d);
        dialog.Top = area.Top + Math.Max(StartupSafetyMargin, (area.Height - dialog.Height) / 2d);
    }

    private static double ResolveDialogLength(double value, double fallback)
    {
        return double.IsNaN(value) || double.IsInfinity(value) || value <= 0d
            ? fallback
            : value;
    }

    private void BatteryGuideTriggerSelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_batteryGuideTriggerCaptureWindow is not null)
        {
            if (!_batteryGuideTriggerSelectMouseToggleRequested)
            {
                _batteryGuideTriggerCaptureWindow.Activate();
                _batteryGuideTriggerCaptureWindow.Focus();
                return;
            }

            _batteryGuideTriggerSelectMouseToggleRequested = false;
            CancelBatteryGuideTriggerCapture(closeWindow: true, animateClose: true);
            return;
        }

        _batteryGuideTriggerSelectMouseToggleRequested = false;
        BeginBatteryGuideTriggerCapture();
    }

    private void BatteryGuideTriggerSelectButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _batteryGuideTriggerSelectMouseToggleRequested = true;
    }

    private void BatteryGuideTriggerResetButton_Click(object sender, RoutedEventArgs e)
    {
        SpinBatteryGuideTriggerResetIcon();
        CancelBatteryGuideTriggerCapture(closeWindow: true);
        _viewModel.ResetBatteryGuideTrigger();
    }

    private void SpinBatteryGuideTriggerResetIcon()
    {
        BatteryGuideTriggerResetRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        BatteryGuideTriggerResetRotate.Angle = 0;

        var animation = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(680));
        BatteryGuideTriggerResetRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void BeginBatteryGuideTriggerCapture()
    {
        SetBatteryGuideTriggerCaptureActive(true);
        _pendingBatteryGuideTriggerCapture = null;
        _batteryGuideTriggerCaptureKey = null;
        _lastBatteryGuideTriggerReportByDevice.Clear();
        ClearCustomBatteryGuideTriggerPressState();
        ShowBatteryGuideTriggerCaptureWindow();
    }

    private void CancelBatteryGuideTriggerCapture(bool closeWindow, bool animateClose = false)
    {
        SetBatteryGuideTriggerCaptureActive(false);
        _pendingBatteryGuideTriggerCapture = null;
        _batteryGuideTriggerCaptureKey = null;
        _lastBatteryGuideTriggerReportByDevice.Clear();
        ClearCustomBatteryGuideTriggerPressState();
        if (closeWindow)
        {
            CloseBatteryGuideTriggerCaptureWindow(animateClose);
        }
    }

    private void ShowBatteryGuideTriggerCaptureWindow()
    {
        if (_batteryGuideTriggerCaptureWindow is not null)
        {
            _batteryGuideTriggerCaptureWindow.SetProfiles(
                _viewModel.BatteryGuideTriggerProfiles,
                _viewModel.BatteryGuideTrigger);
            _batteryGuideTriggerCaptureWindow.SetCandidate(_pendingBatteryGuideTriggerCapture);
            _batteryGuideTriggerCaptureWindow.Activate();
            return;
        }

        var captureWindow = new BatteryGuideTriggerCaptureWindow(_viewModel.Language)
        {
            Owner = this,
            PopInOriginScreenPoint = TryGetElementCenterScreenPoint(BatteryGuideTriggerSelectButton)
        };
        captureWindow.SaveRequested += BatteryGuideTriggerCaptureWindow_SaveRequested;
        captureWindow.RetryRequested += BatteryGuideTriggerCaptureWindow_RetryRequested;
        captureWindow.CancelRequested += BatteryGuideTriggerCaptureWindow_CancelRequested;
        captureWindow.Closed += BatteryGuideTriggerCaptureWindow_Closed;
        _batteryGuideTriggerCaptureWindow = captureWindow;
        PositionBatteryGuideTriggerCaptureWindow(captureWindow);
        captureWindow.SetProfiles(
            _viewModel.BatteryGuideTriggerProfiles,
            _viewModel.BatteryGuideTrigger);
        captureWindow.SetCandidate(_pendingBatteryGuideTriggerCapture);
        captureWindow.Show();
        captureWindow.Activate();
    }

    private void PositionBatteryGuideTriggerCaptureWindow(Window captureWindow)
    {
        var area = GetWorkingAreaForOwnerWindow();
        var maxWidth = Math.Max(320d, (area.Width * BatteryGuideTriggerCaptureMaxWorkAreaRatio) - (StartupSafetyMargin * 2d));
        var maxHeight = Math.Max(260d, (area.Height * BatteryGuideTriggerCaptureMaxWorkAreaRatio) - (StartupSafetyMargin * 2d));
        var scale = Math.Min(
            BatteryGuideTriggerCaptureMaxScale,
            Math.Min(
                maxWidth / BatteryGuideTriggerCaptureDesignWidth,
                maxHeight / BatteryGuideTriggerCaptureDesignHeight));
        captureWindow.Width = Math.Floor(BatteryGuideTriggerCaptureDesignWidth * scale);
        captureWindow.Height = Math.Floor(BatteryGuideTriggerCaptureDesignHeight * scale);
        captureWindow.Left = area.Left + Math.Max(StartupSafetyMargin, (area.Width - captureWindow.Width) / 2d);
        captureWindow.Top = area.Top + Math.Max(StartupSafetyMargin, (area.Height - captureWindow.Height) / 2d);
    }

    private WindowBounds GetWorkingAreaForOwnerWindow()
    {
        var width = ActualWidth > 0d ? ActualWidth : Width;
        var height = ActualHeight > 0d ? ActualHeight : Height;
        if (double.IsNaN(Left) ||
            double.IsNaN(Top) ||
            double.IsNaN(width) ||
            double.IsNaN(height) ||
            double.IsInfinity(Left) ||
            double.IsInfinity(Top) ||
            double.IsInfinity(width) ||
            double.IsInfinity(height) ||
            width <= 0d ||
            height <= 0d)
        {
            return GetWorkingAreaFromCurrentCursor();
        }

        var center = new System.Drawing.Point(
            (int)Math.Round(Left + (width / 2d)),
            (int)Math.Round(Top + (height / 2d)));
        return GetWorkingAreaFromScreen(Forms.Screen.FromPoint(center), this);
    }

    private static System.Windows.Point? TryGetElementCenterScreenPoint(FrameworkElement element)
    {
        if (!element.IsLoaded || element.ActualWidth <= 0d || element.ActualHeight <= 0d)
        {
            return null;
        }

        try
        {
            var devicePoint = element.PointToScreen(new System.Windows.Point(
                element.ActualWidth / 2d,
                element.ActualHeight / 2d));
            var transform = PresentationSource.FromVisual(element)?.CompositionTarget?.TransformFromDevice;
            return transform?.Transform(devicePoint) ?? devicePoint;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void BatteryGuideTriggerCaptureWindow_SaveRequested(object? sender, EventArgs e)
    {
        if (_pendingBatteryGuideTriggerCapture is null)
        {
            return;
        }

        _viewModel.SetBatteryGuideTriggerProfile(
            _pendingBatteryGuideTriggerCapture.DeviceKind,
            _pendingBatteryGuideTriggerCapture.ToPersistedString());
        ShowTrayNotification(
            _viewModel.TextBatteryGuideTrigger,
            ExtraText("BatteryGuideTriggerSavedToast"),
            Forms.ToolTipIcon.Info);
        CancelBatteryGuideTriggerCapture(closeWindow: true);
    }

    private void BatteryGuideTriggerCaptureWindow_RetryRequested(object? sender, EventArgs e)
    {
        SetBatteryGuideTriggerCaptureActive(true);
        _pendingBatteryGuideTriggerCapture = null;
        _batteryGuideTriggerCaptureKey = null;
        _lastBatteryGuideTriggerReportByDevice.Clear();
        ClearCustomBatteryGuideTriggerPressState();
        _batteryGuideTriggerCaptureWindow?.SetCandidate(null);
    }

    private void BatteryGuideTriggerCaptureWindow_CancelRequested(object? sender, EventArgs e)
    {
        CancelBatteryGuideTriggerCapture(closeWindow: true);
    }

    private void BatteryGuideTriggerCaptureWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is BatteryGuideTriggerCaptureWindow captureWindow)
        {
            captureWindow.SaveRequested -= BatteryGuideTriggerCaptureWindow_SaveRequested;
            captureWindow.RetryRequested -= BatteryGuideTriggerCaptureWindow_RetryRequested;
            captureWindow.CancelRequested -= BatteryGuideTriggerCaptureWindow_CancelRequested;
            captureWindow.Closed -= BatteryGuideTriggerCaptureWindow_Closed;
        }

        if (ReferenceEquals(_batteryGuideTriggerCaptureWindow, sender))
        {
            _batteryGuideTriggerCaptureWindow = null;
        }

        SetBatteryGuideTriggerCaptureActive(false);
        _pendingBatteryGuideTriggerCapture = null;
        _batteryGuideTriggerCaptureKey = null;
        _lastBatteryGuideTriggerReportByDevice.Clear();
        ClearCustomBatteryGuideTriggerPressState();
    }

    private void SetBatteryGuideTriggerCaptureActive(bool isActive)
    {
        _isBatteryGuideTriggerCaptureActive = isActive;
        _guideButtonMonitor.SetDetailedInputReportMode(isActive);
        _steamRawInputMonitor.SetDetailedInputReportMode(isActive);
        SyncGuideButtonMonitorSteamPolicy();
    }

    private void SyncBatteryGuideTriggerInputInterests()
    {
        var triggers = new Dictionary<GuideButtonDeviceKind, BatteryGuideTrigger>();
        foreach (var deviceKind in new[] { GuideButtonDeviceKind.DualSense, GuideButtonDeviceKind.SteamController })
        {
            if (_viewModel.TryGetBatteryGuideTriggerForDevice(deviceKind, out var persistedTrigger) &&
                BatteryGuideTriggerParser.TryParse(persistedTrigger, out var trigger))
            {
                triggers[deviceKind] = trigger;
            }
        }

        _guideButtonMonitor.SetActiveBatteryGuideTriggers(triggers);
        _steamRawInputMonitor.SetActiveBatteryGuideTriggers(triggers);
    }

    private void SteamRawInputMonitor_SteamRawHidBaselineReady(object? sender, EventArgs e)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(
                    new Action(() => SteamRawInputMonitor_SteamRawHidBaselineReady(sender, e)));
                return;
            }

            SyncGuideButtonMonitorSteamPolicy();
        }
        catch
        {
            // Steam RawInput is an optimization path; never let it close the widget.
        }
    }

    private void SyncGuideButtonMonitorSteamPolicy()
    {
        var rawSteamMonitorActive =
            _steamRawInputMonitor.IsRegistered &&
            (_steamRawInputMonitor.IsNormalMode || _steamRawInputMonitor.IsWakeOnlyMode);
        var allowSteamDirectHid = _isBatteryGuideTriggerCaptureActive || !rawSteamMonitorActive;
        _guideButtonMonitor.SetSteamDirectHidEnabled(allowSteamDirectHid);
    }

    private void CloseBatteryGuideTriggerCaptureWindow(bool animateClose = false)
    {
        try
        {
            if (animateClose)
            {
                _batteryGuideTriggerCaptureWindow?.CloseWithPopOut();
            }
            else
            {
                _batteryGuideTriggerCaptureWindow?.CloseFromOwner();
            }
        }
        catch
        {
            // Capture UI is optional; the widget must keep running.
        }
    }

    private void LoadCustomGuideSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _viewModel.TextCustomGuideSoundSelectTitle,
            Filter = ExtraText("CustomGuideSoundFilter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var persistedPath = PersistCustomGuideSound(dialog.FileName);
        if (string.IsNullOrWhiteSpace(persistedPath))
        {
            ShowTrayNotification(_viewModel.TextGuideSound, _viewModel.TextCustomGuideSoundInvalid, Forms.ToolTipIcon.Warning);
            return;
        }

        _viewModel.SetCustomGuideSoundPath(persistedPath);
        SyncGuideSoundSelection();
        UpdateGuideSoundControls();
    }

    private void ResetCustomGuideSoundButton_Click(object sender, RoutedEventArgs e)
    {
        StopGuideSoundPreview();
        _viewModel.ResetCustomGuideSound();
        SyncGuideSoundSelection();
        UpdateGuideSoundControls();
    }

    private void PreviewGuideSoundButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.GuideSoundEnabled)
        {
            return;
        }

        if (_isGuideSoundPreviewPlaying)
        {
            StopGuideSoundPreview();
            return;
        }

        try
        {
            _batteryGuideChimePlayer.PlayFromStart(BatteryGuideSoundCatalog.ResolveGuideSound(
                _viewModel.GuideSoundId,
                _viewModel.CustomGuideSoundPath));
            SetGuideSoundPreviewPlaying(true);
        }
        catch
        {
            SetGuideSoundPreviewPlaying(false);
            // Preview audio is optional.
        }
    }

    private void GuideSoundPreviewResetTimer_Tick(object? sender, EventArgs e)
    {
        SetGuideSoundPreviewPlaying(false);
    }

    private void BatteryGuideChimePlayer_PlaybackEnded(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => BatteryGuideChimePlayer_PlaybackEnded(sender, e));
            return;
        }

        if (_isGuideSoundPreviewPlaying)
        {
            SetGuideSoundPreviewPlaying(false);
        }
    }

    private void VersionEasterEggButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenLabsWindow();
    }

    private void AggressivePolicyToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetThirdPartyBatteryPolicy(ThirdPartyBatteryPolicy.Aggressive);
    }

    private void AggressivePolicyToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetThirdPartyBatteryPolicy(ThirdPartyBatteryPolicy.Hybrid);
    }

    private async void PicoFirmwareUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPicoFirmwareUpdating)
        {
            return;
        }

        _isPicoFirmwareUpdating = true;
        if (PicoFirmwareUpdateButton is not null)
        {
            PicoFirmwareUpdateButton.IsEnabled = false;
        }

        try
        {
            SetPicoFirmwareUpdateStatus(ExtraText("PicoChecking"));
            var firmware = await TryGetLatestDs5DongleFirmwareAsync().ConfigureAwait(true);
            if (firmware is null)
            {
                SetPicoFirmwareUpdateStatus(ExtraText("PicoReleaseReadFailed"));
                OpenExternalUrl(Ds5DongleReleasePageUrl);
                return;
            }

            var bootDrive = FindPicoBootDrive();
            if (bootDrive is not null)
            {
                var rememberedVersion = UpdateService.NormalizeReleaseVersion(_viewModel.LastDs5DongleFirmwareVersion);
                if (!string.IsNullOrWhiteSpace(rememberedVersion) &&
                    !IsDs5DongleFirmwareUpdateNeeded(rememberedVersion, firmware.Version))
                {
                    SetPicoFirmwareUpdateStatus(ExtraFormat(
                        "PicoBootDriveAlreadyLatestRememberedFormat",
                        rememberedVersion,
                        firmware.Version));
                    return;
                }

                var rememberedDisplay = string.IsNullOrWhiteSpace(rememberedVersion)
                    ? ExtraText("PicoUnknownInstalledVersion")
                    : rememberedVersion;
                var confirmationResult = System.Windows.MessageBox.Show(
                    this,
                    ExtraFormat(
                        "PicoBootDriveConfirmFlashFormat",
                        bootDrive.VolumeLabel,
                        rememberedDisplay,
                        firmware.Version),
                    AppDisplayName,
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (confirmationResult != System.Windows.MessageBoxResult.Yes)
                {
                    SetPicoFirmwareUpdateStatus(ExtraText("PicoCancelled"));
                    return;
                }

                SetPicoFirmwareUpdateStatus(ExtraFormat("PicoBootDriveReadyFormat", bootDrive.VolumeLabel, firmware.Version));
                await FlashDs5DongleFirmwareAsync(firmware, bootDrive).ConfigureAwait(true);
                return;
            }

            SetPicoFirmwareUpdateStatus(ExtraText("PicoCheckingInstalled"));
            var installedFirmwareScan = await Task
                .Run(() => Ds5DongleFirmwareVersionReader.ReadCurrentVersion(CancellationToken.None))
                .ConfigureAwait(true);
            var installedFirmware = installedFirmwareScan.Firmware;
            if (installedFirmware is null)
            {
                var message = ExtraFormat("PicoInstalledReadMissingFormat", firmware.Version);
                var hint = BuildPicoFirmwareReadMissingHint(installedFirmwareScan);
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    message = $"{message}{Environment.NewLine}{hint}";
                }

                SetPicoFirmwareUpdateStatus(message);
                return;
            }

            var installedVersion = UpdateService.NormalizeReleaseVersion(installedFirmware.Version);
            _viewModel.RememberDs5DongleFirmwareVersion(installedVersion);
            if (!IsDs5DongleFirmwareUpdateNeeded(installedVersion, firmware.Version))
            {
                SetPicoFirmwareUpdateStatus(ExtraFormat("PicoAlreadyLatestFormat", installedVersion));
                return;
            }

            var updateMessage = ExtraFormat("PicoUpdateAvailableFormat", installedVersion, firmware.Version);
            SetPicoFirmwareUpdateStatus(updateMessage);
            System.Windows.MessageBox.Show(
                this,
                ExtraFormat("PicoUpdateAvailableMessageFormat", installedVersion, firmware.Version),
                AppDisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? ExtraText("PicoErrorFallback")
                : ex.Message;
            SetPicoFirmwareUpdateStatus(message);
            ShowTrayNotification(ExtraText("PicoToastTitle"), message, Forms.ToolTipIcon.Warning);
        }
        finally
        {
            _isPicoFirmwareUpdating = false;
            if (PicoFirmwareUpdateButton is not null)
            {
                PicoFirmwareUpdateButton.IsEnabled = true;
            }
        }
    }

    private string BuildPicoFirmwareReadMissingHint(Ds5DongleFirmwareVersionScanResult scan)
    {
        return scan.Status switch
        {
            Ds5DongleFirmwareVersionReadStatus.OnlyBluetoothDualSenseEndpoints => ExtraText("PicoReadHintBluetoothOnly"),
            Ds5DongleFirmwareVersionReadStatus.NoUsbDs5DongleEndpoint => ExtraText("PicoReadHintNoUsbDs5Dongle"),
            Ds5DongleFirmwareVersionReadStatus.UsbDs5DongleOpenFailed => ExtraText("PicoReadHintUsbOpenFailed"),
            Ds5DongleFirmwareVersionReadStatus.FirmwareVersionReportUnavailable => ExtraText("PicoReadHintReportUnavailable"),
            _ => string.Empty
        };
    }

    private void ColorPresetComboBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (_isColorPresetSyncing || ColorPresetComboBox.SelectedItem is not ColorPreset selected)
        {
            return;
        }

        ApplySelectedColorPreset(selected);
    }

    private void ColorPresetComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isColorPresetSyncing ||
            !_viewModel.UseCustomColors ||
            ColorPresetComboBox.IsDropDownOpen ||
            ColorPresetComboBox.SelectedItem is not ColorPreset selected)
        {
            return;
        }

        ApplySelectedColorPreset(selected);
    }

    private void ComboBoxMarquee_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is DependencyObject source)
        {
            QueueColorPresetMarqueeUpdate(FindColorPresetMarqueeText(source));
        }
    }

    private void ComboBoxMarquee_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is DependencyObject source)
        {
            QueueColorPresetMarqueeReset(FindColorPresetMarqueeText(source));
        }
    }

    private void ColorPresetComboBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isColorPresetSyncing || sender is not WpfControls.ComboBoxItem { DataContext: ColorPreset selected })
        {
            return;
        }

        ApplySelectedColorPreset(selected);
    }

    private void ColorPresetComboBoxItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isColorPresetSyncing || sender is not WpfControls.ComboBoxItem { DataContext: ColorPreset selected })
        {
            return;
        }

        ApplySelectedColorPreset(selected);
    }

    private void ApplySelectedColorPreset(ColorPreset selected)
    {
        _viewModel.SetColorPreset(selected.Id);
        ApplyColorPreset(selected.Id);
        SyncColorPresetSelection();
    }

    private void CustomTextColorButton_Click(object sender, RoutedEventArgs e)
    {
        var preset = ColorPresetCatalog.GetById(_viewModel.ColorPresetId);
        var currentText = _viewModel.UseCustomColors
            ? _viewModel.CustomTextColor
            : ToRgbHex(preset.PrimaryText);
        var currentBackground = _viewModel.UseCustomColors
            ? _viewModel.CustomBackgroundColor
            : ToRgbHex(preset.CardTint);

        if (!TryPickColor(currentText, out var selectedTextColor))
        {
            return;
        }

        _viewModel.SetCustomColors(selectedTextColor, currentBackground);
        ApplyColorPreset(_viewModel.ColorPresetId);
    }

    private void CustomBackgroundColorButton_Click(object sender, RoutedEventArgs e)
    {
        var preset = ColorPresetCatalog.GetById(_viewModel.ColorPresetId);
        var currentText = _viewModel.UseCustomColors
            ? _viewModel.CustomTextColor
            : ToRgbHex(preset.PrimaryText);
        var currentBackground = _viewModel.UseCustomColors
            ? _viewModel.CustomBackgroundColor
            : ToRgbHex(preset.CardTint);

        if (!TryPickColor(currentBackground, out var selectedBackgroundColor))
        {
            return;
        }

        _viewModel.SetCustomColors(currentText, selectedBackgroundColor);
        ApplyColorPreset(_viewModel.ColorPresetId);
    }

    private void ResetCustomColorsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearCustomColors();
        ApplyColorPreset(_viewModel.ColorPresetId);
        UpdateColorEditorState();
    }

    private void ColorCustomizeButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ColorCustomPopup.IsOpen = !ColorCustomPopup.IsOpen;
        if (ColorCustomPopup.IsOpen)
        {
            UpdateColorEditorState();
        }
    }

    private void ColorCustomCloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ColorCustomPopup.IsOpen = false;
    }

    private void ColorElementButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string elementKey } ||
            !ColorElementKeys.Contains(elementKey))
        {
            return;
        }

        _selectedColorElementKey = elementKey;
        UpdateColorEditorState();
    }

    private void ColorQuickSwatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string hex } ||
            System.Windows.Media.ColorConverter.ConvertFromString(hex) is not System.Windows.Media.Color color)
        {
            return;
        }

        ApplySelectedQuickColor(color);
        e.Handled = true;
    }

    private void PaletteSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPaletteDragging = true;
        PaletteSurface.CaptureMouse();
        UpdateSelectedColorFromPalette(e.GetPosition(PaletteSurface));
        e.Handled = true;
    }

    private void PaletteSurface_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPaletteDragging)
        {
            return;
        }

        UpdateSelectedColorFromPalette(e.GetPosition(PaletteSurface));
        e.Handled = true;
    }

    private void PaletteSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPaletteDragging)
        {
            return;
        }

        UpdateSelectedColorFromPalette(e.GetPosition(PaletteSurface));
        FinishPaletteDrag();
        e.Handled = true;
    }

    private void PaletteSurface_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isPaletteDragging && e.LeftButton != MouseButtonState.Pressed)
        {
            FinishPaletteDrag();
        }
    }

    private void LoadCustomFontButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _viewModel.TextCustomFontSelectTitle,
            Filter = ExtraText("CustomFontFilter"),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var persistedPath = PersistCustomFont(dialog.FileName);
        if (string.IsNullOrWhiteSpace(persistedPath))
        {
            System.Windows.MessageBox.Show(
                this,
                _viewModel.TextCustomFontLoadFailed,
                AppDisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        _viewModel.SetCustomFont(persistedPath);
        ApplyCustomFont();
    }

    private void ResetCustomFontButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearCustomFont();
        ApplyCustomFont();
    }

    private void LanguageComboBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (_isLanguageSyncing || LanguageComboBox.SelectedItem is not UiLanguageOption selected)
        {
            return;
        }

        _viewModel.SetLanguage(selected.Id);
        RefreshTrayMenuTexts();
        UpdateVersionMenuHeader();
    }

    private void UiScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var step = (int)Math.Round(e.NewValue);
        var previousStep = (int)Math.Round(e.OldValue);
        if (step == previousStep)
        {
            return;
        }

        _viewModel.SetUiScaleStep(step);
        ApplyUiScaleStep(_viewModel.UiScaleStep);
    }

    private void SettingsTextFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || SettingsTextFontSizeSlider is null || !SettingsTextFontSizeSlider.IsLoaded)
        {
            return;
        }

        var size = WidgetSettings.NormalizeSettingsTextFontSize(e.NewValue);
        var previousSize = WidgetSettings.NormalizeSettingsTextFontSize(e.OldValue);
        if (Math.Abs(size - previousSize) < 0.01d)
        {
            return;
        }

        if (!_viewModel.UseCustomSettingsTextStyle &&
            Math.Abs(size - WidgetSettings.DefaultSettingsTextFontSize) < 0.01d)
        {
            return;
        }

        _viewModel.SetSettingsTextFontSize(size);
        ApplySettingsTextStyle();
    }

    private void SettingsTextBoldToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetSettingsTextBold(true);
        ApplySettingsTextStyle();
    }

    private void SettingsTextBoldToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.UseCustomSettingsTextStyle && !_viewModel.SettingsTextBold)
        {
            return;
        }

        _viewModel.SetSettingsTextBold(false);
        ApplySettingsTextStyle();
    }

    private void ResetSettingsTextStyleButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearSettingsTextStyle();
        ApplyColorPreset(_viewModel.ColorPresetId);
        ApplySettingsTextStyle();
    }

    private async void ManualRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshFromUserCommandAsync().ConfigureAwait(true);
    }

    private async Task RefreshFromUserCommandAsync()
    {
        SuppressSteamGuideToasts(SteamGuideToastRefreshSuppressDuration, "manual_refresh_started");
        await _viewModel.RefreshAsync(forceFullRefresh: true).ConfigureAwait(true);
        SuppressSteamGuideToasts(SteamGuideToastRefreshSuppressDuration, "manual_refresh_completed");
    }

    private void IconOverridesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_iconOverrideWindow is not null)
        {
            _iconOverrideWindow.CloseWithPopOutAsCancel();
            return;
        }

        var snapshots = _viewModel.GetDeviceSnapshots();
        var existingOverrides = IconOverrideParser.Parse(_viewModel.Settings.IconOverrides);
        var existingImageOverrides = IconImageOverrideParser.Parse(_viewModel.Settings.IconImageOverrides);
        var dialog = new IconOverrideWindow(snapshots, existingOverrides, existingImageOverrides, _viewModel.Language)
        {
            Owner = this
        };
        dialog.Closed += IconOverrideWindow_Closed;
        _iconOverrideWindow = dialog;
        dialog.Show();
        dialog.Activate();
    }

    private async void IconOverrideWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not IconOverrideWindow dialog)
        {
            return;
        }

        dialog.Closed -= IconOverrideWindow_Closed;
        if (ReferenceEquals(_iconOverrideWindow, dialog))
        {
            _iconOverrideWindow = null;
        }

        if (!dialog.WasAccepted)
        {
            return;
        }

        _viewModel.Settings.IconOverrides.Clear();
        foreach (var pair in dialog.SelectedOverrides)
        {
            IconOverrideParser.Set(_viewModel.Settings.IconOverrides, pair.Key, pair.Value);
        }

        _viewModel.Settings.IconImageOverrides.Clear();
        foreach (var pair in dialog.SelectedImageOverrides)
        {
            var persistedPath = PersistCustomIconImage(pair.Key, pair.Value);
            DeleteGeneratedTemporaryIcon(pair.Value);
            if (string.IsNullOrWhiteSpace(persistedPath))
            {
                continue;
            }

            IconImageOverrideParser.Set(_viewModel.Settings.IconImageOverrides, pair.Key, persistedPath);
        }

        _viewModel.SaveSettings();
        await _viewModel.RefreshAsync().ConfigureAwait(true);
    }

    private void DeveloperContactButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(DeveloperContactEmail);
            ShowTrayNotification(
                _viewModel.CurrentLanguageText.DeveloperContactTitle,
                string.Format(_viewModel.CurrentLanguageText.DeveloperContactCopiedFormat, DeveloperContactEmail),
                Forms.ToolTipIcon.Info);
        }
        catch
        {
            ShowTrayNotification(
                _viewModel.CurrentLanguageText.DeveloperContactTitle,
                _viewModel.CurrentLanguageText.DeveloperContactCopyFailed,
                Forms.ToolTipIcon.Warning);
        }
    }

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SupportUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            ShowTrayNotification(
                _viewModel.TextSupport,
                $"Could not open: {SupportUrl}",
                Forms.ToolTipIcon.Warning);
        }
    }

    private UpdateServiceText BuildUpdateServiceText()
    {
        return new UpdateServiceText(
            _viewModel.CurrentLanguageText.UpdateAssetMissing,
            _viewModel.CurrentLanguageText.UpdateReleaseReadFailed,
            _viewModel.CurrentLanguageText.UpdateChecksumMissing,
            _viewModel.CurrentLanguageText.UpdateSourceNotTrusted,
            _viewModel.CurrentLanguageText.UpdateDownloading,
            _viewModel.CurrentLanguageText.UpdateDownloadingFormat,
            _viewModel.CurrentLanguageText.UpdateVerifying,
            _viewModel.CurrentLanguageText.UpdateVerificationFailed);
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        if (UpdateButton is not null)
        {
            UpdateButton.IsEnabled = false;
        }

        try
        {
            SetUpdateProgressUi(_viewModel.CurrentLanguageText.UpdateChecking, 0, isIndeterminate: true);

            var updateText = BuildUpdateServiceText();
            var (releaseInfo, errorMessage) = await _updateService
                .TryGetLatestReleaseAssetAsync(updateText)
                .ConfigureAwait(true);
            if (releaseInfo is null)
            {
                var message = string.IsNullOrWhiteSpace(errorMessage)
                    ? _viewModel.CurrentLanguageText.UpdateReleaseReadFailed
                    : errorMessage;
                SetUpdateProgressUi(message, 0, isIndeterminate: false);
                ShowTrayNotification(_viewModel.TextUpdate, message, Forms.ToolTipIcon.Warning);
                return;
            }

            var currentVersion = GetDisplayVersion();
            if (!UpdateService.IsRemoteVersionNewer(currentVersion, releaseInfo.Version))
            {
                SetUpdateProgressUi(_viewModel.CurrentLanguageText.UpdateNoUpdate, 100, isIndeterminate: false);
                await Task.Delay(1400).ConfigureAwait(true);
                ResetUpdateProgressUi();
                return;
            }

            var setupPath = await _updateService
                .DownloadAndVerifyInstallerAsync(
                    releaseInfo,
                    updateText,
                    progress => SetUpdateProgressUi(progress.Message, progress.ProgressPercent, progress.IsIndeterminate))
                .ConfigureAwait(true);

            SetUpdateProgressUi(_viewModel.CurrentLanguageText.UpdateInstallStarting, 100, isIndeterminate: false);
            _updateService.StartInstallerUpdateAndRestart(
                setupPath,
                releaseInfo.Version,
                AppVersionInfo.FallbackVersion,
                _viewModel.CurrentLanguageText.UpdateInstallLaunchFailed);
            ExitApplication();
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? _viewModel.CurrentLanguageText.UpdateDownloadFailed
                : ex.Message;
            SetUpdateProgressUi(message, 0, isIndeterminate: false);
            ShowTrayNotification(
                _viewModel.TextUpdate,
                message,
                Forms.ToolTipIcon.Warning);
        }
        finally
        {
            _isUpdating = false;
            if (!_isExiting && UpdateButton is not null)
            {
                UpdateButton.IsEnabled = true;
            }
        }
    }

    private async Task<Ds5DongleFirmwareInfo?> TryGetLatestDs5DongleFirmwareAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Ds5DongleLatestReleaseApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppDisplayName, GetDisplayVersion()));

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(true);
            var root = document.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagNameElement))
            {
                return null;
            }

            var latestVersion = UpdateService.NormalizeReleaseVersion(tagNameElement.GetString());
            var releaseUrl = root.TryGetProperty("html_url", out var htmlUrlElement)
                ? htmlUrlElement.GetString() ?? Ds5DongleReleasePageUrl
                : Ds5DongleReleasePageUrl;

            if (!root.TryGetProperty("assets", out var assetsElement) ||
                assetsElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement) ||
                    !asset.TryGetProperty("browser_download_url", out var urlElement))
                {
                    continue;
                }

                var assetName = nameElement.GetString();
                var downloadUrl = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(assetName) ||
                    string.IsNullOrWhiteSpace(downloadUrl) ||
                    !assetName.EndsWith(".uf2", StringComparison.OrdinalIgnoreCase) ||
                    !UpdateService.IsTrustedDownloadUrl(downloadUrl))
                {
                    continue;
                }

                return new Ds5DongleFirmwareInfo(latestVersion, assetName, downloadUrl, releaseUrl);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task DownloadDs5DongleFirmwareAsync(string downloadUrl, string destinationPath)
    {
        try
        {
            if (!UpdateService.IsTrustedDownloadUrl(downloadUrl))
            {
                throw new InvalidOperationException(ExtraText("PicoDownloadUrlUntrusted"));
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppDisplayName, GetDisplayVersion()));

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(true);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > Ds5DongleMaxFirmwareBytes)
            {
                throw new InvalidOperationException(ExtraText("PicoFileTooLarge"));
            }

            await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            var downloadedBytes = 0L;
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(true);
                if (read <= 0)
                {
                    break;
                }

                downloadedBytes += read;
                if (downloadedBytes > Ds5DongleMaxFirmwareBytes)
                {
                    throw new InvalidOperationException(ExtraText("PicoFileTooLarge"));
                }

                await target.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(true);
            }
        }
        catch
        {
            TryDeleteFile(destinationPath);
            throw;
        }
    }

    private async Task FlashDs5DongleFirmwareAsync(Ds5DongleFirmwareInfo firmware, DriveInfo drive)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "Bloss", "firmware", firmware.AssetName);
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        SetPicoFirmwareUpdateStatus(ExtraFormat("PicoDownloadingFormat", firmware.Version));
        await DownloadDs5DongleFirmwareAsync(firmware.DownloadUrl, tempPath).ConfigureAwait(true);

        var destinationPath = Path.Combine(drive.RootDirectory.FullName, firmware.AssetName);
        SetPicoFirmwareUpdateStatus(ExtraFormat("PicoCopyingFormat", drive.VolumeLabel));
        File.Copy(tempPath, destinationPath, overwrite: true);

        SetPicoFirmwareUpdateStatus(ExtraText("PicoCopied"));
        _viewModel.RememberDs5DongleFirmwareVersion(firmware.Version);
        ShowTrayNotification(ExtraText("PicoToastTitle"), ExtraText("PicoToastCopied"), Forms.ToolTipIcon.Info);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static bool IsDs5DongleFirmwareUpdateNeeded(string currentVersionText, string latestVersionText)
    {
        if (UpdateService.TryParseComparableVersion(currentVersionText, out var currentVersion) &&
            UpdateService.TryParseComparableVersion(latestVersionText, out var latestVersion))
        {
            var numericComparison = latestVersion.CompareTo(currentVersion);
            if (numericComparison != 0)
            {
                return numericComparison > 0;
            }
        }

        return !string.Equals(
            UpdateService.NormalizeReleaseVersion(currentVersionText),
            UpdateService.NormalizeReleaseVersion(latestVersionText),
            StringComparison.OrdinalIgnoreCase);
    }

    private void SetUpdateProgressUi(string message, double progressPercent, bool isIndeterminate)
    {
        if (UpdateProgressPanel is null || UpdateProgressBar is null || UpdateProgressTextBlock is null)
        {
            return;
        }

        UpdateProgressPanel.Visibility = Visibility.Visible;
        UpdateProgressBar.IsIndeterminate = isIndeterminate;
        if (!isIndeterminate)
        {
            UpdateProgressBar.Value = Math.Clamp(progressPercent, 0d, 100d);
        }

        UpdateProgressTextBlock.Text = message;
    }

    private void SetPicoFirmwareUpdateStatus(string message)
    {
        if (PicoFirmwareUpdateStatusTextBlock is not null)
        {
            PicoFirmwareUpdateStatusTextBlock.Text = message;
        }
    }

    private string ExtraText(string key)
    {
        return UiLanguageCatalog.GetExtraText(_viewModel.Language, key);
    }

    private string ExtraFormat(string key, params object[] args)
    {
        return string.Format(ExtraText(key), args);
    }

    private static DriveInfo? FindPicoBootDrive()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var label = drive.VolumeLabel?.Trim() ?? string.Empty;
                if (string.Equals(label, "RP2350", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(label, "RPI-RP2", StringComparison.OrdinalIgnoreCase))
                {
                    return drive;
                }
            }
            catch
            {
                // Ignore drives that disappear while Windows is refreshing USB storage.
            }
        }

        return null;
    }

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Optional helper only.
        }
    }

    private void ResetUpdateProgressUi()
    {
        if (UpdateProgressPanel is null || UpdateProgressBar is null || UpdateProgressTextBlock is null)
        {
            return;
        }

        UpdateProgressPanel.Visibility = Visibility.Collapsed;
        UpdateProgressBar.IsIndeterminate = false;
        UpdateProgressBar.Value = 0;
        UpdateProgressTextBlock.Text = string.Empty;
    }

    private void OpenSettingsPopup()
    {
        FinishPopupDrag();
        UpdateSettingsPopupLayout();
        SettingsPopup.HorizontalOffset = 0d;
        SettingsPopup.VerticalOffset = 8d;
        SettingsPopup.IsOpen = true;
        UpdateVersionMenuHeader();
    }

    private void UpdateSettingsPopupLayout()
    {
        // Fixed width: keep settings stable while the main widget is resized.
    }

    private void CloseSettingsPopup()
    {
        FinishPopupDrag();
        _settingsAutoCloseTimer.Stop();
        SettingsPopup.IsOpen = false;
        CloseSettingsAccordions(animate: false);
        ColorCustomPopup.IsOpen = false;
    }

    private bool IsSettingsPopupOpen()
    {
        return SettingsPopup.IsOpen;
    }

    private void ToggleSettingsAccordion(FrameworkElement body, WpfControls.TextBlock arrow)
    {
        var shouldOpen = body.Visibility != Visibility.Visible;
        CloseSettingsAccordions(body, animate: shouldOpen);
        if (shouldOpen)
        {
            OpenSettingsAccordion(body, arrow);
        }
        else
        {
            CloseSettingsAccordion(body, arrow, animate: true);
        }

        QueueSettingsAutoCloseCheck();
    }

    private void CloseSettingsAccordions(FrameworkElement? exceptBody = null, bool animate = false)
    {
        CloseSettingsAccordion(EnvironmentAccordionBody, EnvironmentAccordionArrow, animate, exceptBody);
        CloseSettingsAccordion(CustomizeAccordionBody, CustomizeAccordionArrow, animate, exceptBody);
        CloseSettingsAccordion(LabsAccordionBody, LabsAccordionArrow, animate, exceptBody);
    }

    private void OpenSettingsAccordion(FrameworkElement body, WpfControls.TextBlock arrow)
    {
        var animationToken = NextSettingsAccordionAnimationToken(body);
        body.BeginAnimation(HeightProperty, null);
        body.BeginAnimation(OpacityProperty, null);
        body.Visibility = Visibility.Visible;
        body.Opacity = 0d;
        body.Height = double.NaN;
        body.Measure(new System.Windows.Size(Math.Max(SettingsPopupChrome.ActualWidth - 24d, 320d), double.PositiveInfinity));

        var targetHeight = Math.Max(1d, body.DesiredSize.Height);
        body.Height = 0d;
        var heightAnimation = new DoubleAnimation(0d, targetHeight, TimeSpan.FromMilliseconds(SettingsAccordionAnimationMilliseconds))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        heightAnimation.Completed += (_, _) =>
        {
            if (!IsCurrentSettingsAccordionAnimation(body, animationToken))
            {
                return;
            }

            body.BeginAnimation(HeightProperty, null);
            body.Height = double.NaN;
            body.Opacity = 1d;
        };

        body.BeginAnimation(HeightProperty, heightAnimation);
        body.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        arrow.Text = "⌃";
    }

    private void CloseSettingsAccordion(
        FrameworkElement body,
        WpfControls.TextBlock arrow,
        bool animate,
        FrameworkElement? exceptBody = null)
    {
        if (ReferenceEquals(body, exceptBody))
        {
            return;
        }

        var animationToken = NextSettingsAccordionAnimationToken(body);
        body.BeginAnimation(HeightProperty, null);
        body.BeginAnimation(OpacityProperty, null);
        arrow.Text = "⌄";

        if (body.Visibility != Visibility.Visible || !animate)
        {
            body.Visibility = Visibility.Collapsed;
            body.Height = double.NaN;
            body.Opacity = 1d;
            return;
        }

        body.Measure(new System.Windows.Size(Math.Max(SettingsPopupChrome.ActualWidth - 24d, 320d), double.PositiveInfinity));
        var startHeight = Math.Max(body.ActualHeight, body.DesiredSize.Height);
        if (startHeight <= 1d)
        {
            body.Visibility = Visibility.Collapsed;
            body.Height = double.NaN;
            body.Opacity = 1d;
            return;
        }

        body.Height = startHeight;
        var heightAnimation = new DoubleAnimation(startHeight, 0d, TimeSpan.FromMilliseconds(SettingsAccordionAnimationMilliseconds - 35))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        };
        heightAnimation.Completed += (_, _) =>
        {
            if (!IsCurrentSettingsAccordionAnimation(body, animationToken))
            {
                return;
            }

            body.BeginAnimation(HeightProperty, null);
            body.Visibility = Visibility.Collapsed;
            body.Height = double.NaN;
            body.Opacity = 1d;
        };

        body.BeginAnimation(HeightProperty, heightAnimation);
        body.BeginAnimation(OpacityProperty, new DoubleAnimation(1d, 0d, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        });
    }

    private int NextSettingsAccordionAnimationToken(FrameworkElement body)
    {
        _settingsAccordionAnimationTokens.TryGetValue(body, out var token);
        token++;
        _settingsAccordionAnimationTokens[body] = token;
        return token;
    }

    private bool IsCurrentSettingsAccordionAnimation(FrameworkElement body, int token)
    {
        return _settingsAccordionAnimationTokens.TryGetValue(body, out var currentToken) &&
               currentToken == token;
    }

    private void SettingsPopupArea_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _settingsAutoCloseTimer.Stop();
    }

    private void SettingsPopupArea_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        QueueSettingsAutoCloseCheck();
    }

    private void QueueSettingsAutoCloseCheck()
    {
        _settingsAutoCloseTimer.Stop();
        _settingsAutoCloseTimer.Start();
    }

    private void SettingsAutoCloseTimer_Tick(object? sender, EventArgs e)
    {
        _settingsAutoCloseTimer.Stop();
        if (IsMouseOverSettingsSurface() || IsAnySettingsDropDownOpen())
        {
            QueueSettingsAutoCloseCheck();
            return;
        }

        CloseSettingsPopup();
    }

    private bool IsMouseOverSettingsSurface()
    {
        return SettingsPopupChrome.IsMouseOver ||
               ColorPopupChrome.IsMouseOver;
    }

    private bool IsAnySettingsDropDownOpen()
    {
        return ColorPresetComboBox.IsDropDownOpen ||
               GuideSoundComboBox.IsDropDownOpen ||
               LanguageComboBox.IsDropDownOpen ||
               ColorCustomPopup.IsOpen && ColorPopupChrome.IsMouseOver;
    }

    private void AnimateSettingsGearClick()
    {
        var currentAngle = SettingsGearRotateTransform.Angle;
        var animation = new DoubleAnimation
        {
            From = currentAngle,
            To = currentAngle + 180d,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        SettingsGearRotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _viewModel.MarkUserActivity();
        ArmNormalGamepadMonitoring("local_mouse_input", requireActiveMode: false);
        UpdatePowerIdleGuideMonitoring();

        var originalSource = e.OriginalSource as DependencyObject;
        if (IsSettingsPopupOpen())
        {
            if (IsInsidePopupChrome(originalSource))
            {
                if (!IsPopupDragBlocked(originalSource))
                {
                    e.Handled = true;
                }

                return;
            }

            if (IsInteractiveElement(originalSource))
            {
                return;
            }

            e.Handled = true;
            return;
        }

        if (IsInsidePopupChrome(originalSource))
        {
            if (!IsPopupDragBlocked(originalSource))
            {
                e.Handled = true;
            }

            return;
        }

        if (IsInteractiveElement(originalSource))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove throws if mouse is released during call.
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _viewModel.MarkUserActivity();
        ArmNormalGamepadMonitoring("local_keyboard_input", requireActiveMode: false);
        UpdatePowerIdleGuideMonitoring();
    }

    private void PopupChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not FrameworkElement chrome ||
            IsPopupDragBlocked(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var popup = GetPopupForChrome(chrome);
        if (popup is null || !popup.IsOpen)
        {
            return;
        }

        _draggingPopup = popup;
        _popupDragChrome = chrome;
        _popupDragStartScreenPoint = chrome.PointToScreen(e.GetPosition(chrome));
        _popupDragStartHorizontalOffset = popup.HorizontalOffset;
        _popupDragStartVerticalOffset = popup.VerticalOffset;
        Mouse.Capture(chrome, CaptureMode.SubTree);
        e.Handled = true;
    }

    private void PopupChrome_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingPopup is null ||
            _popupDragChrome is null ||
            !ReferenceEquals(sender, _popupDragChrome))
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FinishPopupDrag();
            return;
        }

        var currentPoint = _popupDragChrome.PointToScreen(e.GetPosition(_popupDragChrome));
        _draggingPopup.HorizontalOffset = _popupDragStartHorizontalOffset + currentPoint.X - _popupDragStartScreenPoint.X;
        _draggingPopup.VerticalOffset = _popupDragStartVerticalOffset + currentPoint.Y - _popupDragStartScreenPoint.Y;
        e.Handled = true;
    }

    private void PopupChrome_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPopup is null ||
            _popupDragChrome is null ||
            !ReferenceEquals(sender, _popupDragChrome))
        {
            return;
        }

        FinishPopupDrag();
        e.Handled = true;
    }

    private void PopupChrome_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_popupDragChrome is not null && ReferenceEquals(sender, _popupDragChrome))
        {
            FinishPopupDrag();
        }
    }

    private void ColorPopupDragThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        ColorCustomPopup.HorizontalOffset += e.HorizontalChange;
        ColorCustomPopup.VerticalOffset += e.VerticalChange;
        e.Handled = true;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyToolWindowStyle();
        EnableDwmBlurBehindWindow();
        _windowMessageSource = PresentationSource.FromVisual(this) as HwndSource;
        if (_windowMessageSource is not null)
        {
            _windowMessageSource.AddHook(MainWindow_WndProc);
            _steamRawInputWindowHandle = _windowMessageSource.Handle;
            _displayPowerCoordinator.Register(_steamRawInputWindowHandle);
        }

        UpdateCompactMode();
        UpdateGlassCardClip();
        UpdateDwmBlurBehindRegion();
    }

    private IntPtr MainWindow_WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var powerResult = _displayPowerCoordinator.HandleWindowMessage(hwnd, msg, wParam, lParam, ref handled);
        if (handled)
        {
            return powerResult;
        }

        return _steamRawInputMonitor.HandleWindowMessage(hwnd, msg, wParam, lParam, ref handled);
    }

    private void DisplayPowerCoordinator_StateChanged(object? sender, DisplayPowerStateChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(new Action(() => DisplayPowerCoordinator_StateChanged(sender, e)));
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (e.CurrentState == DisplayPowerState.Off)
        {
            _lastDisplayOffStateAtUtc = now;
            ClearPendingDisplayWakeGuideToast();
            ApplyPowerIdleMonitorState(PowerIdleRuntimeMode.DisplayOffWakeOnly, shouldPause: true);
            GuideButtonEventLog.Write(
                "display_state_changed",
                "PowerIdle",
                string.Empty,
                AppDisplayName,
                $"Windows display state changed to Off. previous={e.PreviousState}; wake-only input monitoring enabled.");
            return;
        }

        if (e.CurrentState == DisplayPowerState.On)
        {
            _viewModel.MarkUserActivity();
            ArmNormalGamepadMonitoring("display_state_on", requireActiveMode: false);
            if (ShouldBypassWakeRecoveryAfterVerifiedInput(now))
            {
                _wakeRecoveryUntilUtc = DateTimeOffset.MinValue;
                _verifiedInputWakeRecoveryBypassUntilUtc = DateTimeOffset.MinValue;
                ApplyPowerIdleMonitorState(PowerIdleRuntimeMode.Active, shouldPause: false);
                GuideButtonEventLog.Write(
                    "display_state_changed",
                    "PowerIdle",
                    string.Empty,
                    AppDisplayName,
                    $"Windows display state changed to On. previous={e.PreviousState}; wake recovery skipped after verified gamepad input.");
                FlushPendingDisplayWakeGuideToast(now);
                _ = Dispatcher.BeginInvoke(
                    new Action(UpdatePowerIdleGuideMonitoring),
                    System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            _wakeRecoveryUntilUtc = now + DisplayWakeRecoveryDuration;
            ApplyPowerIdleMonitorState(PowerIdleRuntimeMode.WakeRecovery, shouldPause: true);
            GuideButtonEventLog.Write(
                "display_state_changed",
                "PowerIdle",
                string.Empty,
                AppDisplayName,
                $"Windows display state changed to On. previous={e.PreviousState}; wake recovery started.");
            FlushPendingDisplayWakeGuideToast(now);
            _ = Dispatcher.BeginInvoke(
                new Action(UpdatePowerIdleGuideMonitoring),
                System.Windows.Threading.DispatcherPriority.Background);
            return;
        }

        GuideButtonEventLog.Write(
            "display_state_changed",
            "PowerIdle",
            string.Empty,
            AppDisplayName,
            $"Windows display state changed to {e.CurrentState}. previous={e.PreviousState}.");
    }

    private void PowerIdleMonitorTimer_Tick(object? sender, EventArgs e)
    {
        UpdatePowerIdleGuideMonitoring();
    }

    private void UpdatePowerIdleGuideMonitoring()
    {
        var now = DateTimeOffset.UtcNow;
        var displayTimeout = SystemDisplayIdleTimeout.GetCurrentDisplayOrSleepTimeout();
        var systemIdle = SystemIdleMonitor.GetIdleDuration();
        var localIdle = _viewModel.GetLocalIdleDuration();
        var gamepadIdle = _viewModel.GetGamepadIdleDuration();
        var delay = (TimeSpan?)null;
        var shouldPause = false;
        _gamepadPresenceService.Refresh(_viewModel.Devices.Select(device => device.Snapshot));
        SyncGuideButtonMonitorSteamPolicy();

        var mode = _displayIdleCoordinator.ResolveMode(
            now,
            _displayPowerCoordinator.CurrentState,
            _wakeRecoveryUntilUtc,
            displayTimeout,
            systemIdle,
            localIdle,
            _isBatteryGuideTriggerCaptureActive,
            _viewModel.IsRefreshRunning,
            _viewModel.IsAnyProbeRunning,
            _gamepadPresenceService.HasConnectedGamepad);
        ApplyPowerIdleMonitorState(mode, shouldPause);
        if (RuntimeDiagnostics.IsFileLoggingEnabled)
        {
            PowerIdleDebugLog.Write(
                mode.ToString(),
                delay,
                displayTimeout,
                systemIdle,
                localIdle,
                gamepadIdle,
                shouldPause,
                _viewModel.IsRefreshRunning,
                _viewModel.IsAnyProbeRunning,
                _isBatteryGuideTriggerCaptureActive,
                _guideButtonMonitor.IsRunning,
                _guideButtonMonitor.IsPowerIdlePollingPausedForDiagnostics,
                _guideButtonMonitor.AllowsInitialPressedPowerIdleInputForDiagnostics,
                _steamRawInputMonitor.IsRegistered,
                _xInputActivityMonitor.IsRunning,
                GetRawInputModeForDiagnostics(),
                GetXInputModeForDiagnostics(),
                GetNormalGamepadMonitoringRemaining(now));
        }
    }

    private string GetRawInputModeForDiagnostics()
    {
        if (!_steamRawInputMonitor.IsRegistered)
        {
            return "off";
        }

        if (_steamRawInputMonitor.IsNormalMode)
        {
            return "normal";
        }

        if (_steamRawInputMonitor.IsWakeOnlyMode)
        {
            return "wake";
        }

        if (_steamRawInputMonitor.IsHumanInputOnlyMode)
        {
            return "human";
        }

        return "unknown";
    }

    private string GetXInputModeForDiagnostics()
    {
        if (!_xInputActivityMonitor.IsRunning)
        {
            return "off";
        }

        return _xInputActivityMonitor.IsWakeOnlyMode ? "wake" : "normal";
    }

    private TimeSpan GetNormalGamepadMonitoringRemaining(DateTimeOffset now)
    {
        if (_normalGamepadMonitoringAllowedUntilUtc <= now)
        {
            return TimeSpan.Zero;
        }

        return _normalGamepadMonitoringAllowedUntilUtc - now;
    }

    private void ApplyPowerIdleMonitorState(PowerIdleRuntimeMode mode, bool shouldPause)
    {
        var previousMode = _powerIdleMode;
        var isModeChanged = previousMode != mode;
        if (isModeChanged)
        {
            _powerIdleMode = mode;
            GuideButtonEventLog.Write(
                "power_idle_mode_changed",
                "PowerIdle",
                string.Empty,
                AppDisplayName,
                $"Power idle monitor mode changed. previous={previousMode}; current={mode}; shouldPause={shouldPause}.");
        }

        if (mode == PowerIdleRuntimeMode.DisplayIdleQuiet)
        {
            _displayIdleCoordinator.Acquire("display_idle_quiet");
        }
        else
        {
            _displayIdleCoordinator.Release(mode.ToString());
        }

        switch (mode)
        {
            case PowerIdleRuntimeMode.Active:
                _viewModel.SetDisplaySleepPreparationActive(false);
                if (shouldPause)
                {
                    StartActiveGuideButtonMonitor();
                    StopNormalNonGuideInputMonitors();
                }
                else
                {
                    StartNormalInputMonitors();
                }

                StopWakeOnlyInputMonitors();
                break;
            case PowerIdleRuntimeMode.DisplayIdleQuiet:
                _viewModel.SetDisplaySleepPreparationActive(true);
                StopNormalInputMonitors(waitForExit: true);
                StopWakeOnlyInputMonitors(waitForExit: true);
                break;
            case PowerIdleRuntimeMode.DisplayOffWakeOnly:
                _viewModel.SetDisplaySleepPreparationActive(false);
                if (isModeChanged)
                {
                    StopNormalInputMonitors();
                }

                StartWakeOnlyInputMonitors();
                break;
            case PowerIdleRuntimeMode.WakeRecovery:
                _viewModel.SetDisplaySleepPreparationActive(false);
                if (isModeChanged)
                {
                    StartPowerIdleGuideOnlyMonitor(allowInitialPressedInput: false);
                    StopNormalNonGuideInputMonitors();
                    StopWakeOnlyNonGuideInputMonitors();
                }

                break;
        }
    }

    private void StartNormalInputMonitors()
    {
        StartActiveGuideButtonMonitor();
        if (!ShouldRunNormalGamepadMonitoring(DateTimeOffset.UtcNow))
        {
            StopNormalNonGuideInputMonitors();
            return;
        }

        StartSteamRawInputPrimaryMonitor();
        if (!_xInputActivityMonitor.IsRunning || _xInputActivityMonitor.IsWakeOnlyMode)
        {
            _xInputActivityMonitor.Start();
        }
    }

    private void StartSteamRawInputPrimaryMonitor()
    {
        if (_displayPowerCoordinator.CurrentState is DisplayPowerState.Off or DisplayPowerState.Dimmed)
        {
            return;
        }

        if (_steamRawInputWindowHandle != IntPtr.Zero &&
            (!_steamRawInputMonitor.IsRegistered || !_steamRawInputMonitor.IsNormalMode))
        {
            _steamRawInputMonitor.Start(_steamRawInputWindowHandle);
        }

        SyncGuideButtonMonitorSteamPolicy();
    }

    private bool ShouldRunNormalGamepadMonitoring(DateTimeOffset now)
    {
        if (_displayPowerCoordinator.CurrentState is DisplayPowerState.Off or DisplayPowerState.Dimmed)
        {
            return false;
        }

        return _isBatteryGuideTriggerCaptureActive ||
               (_powerIdleMode == PowerIdleRuntimeMode.Active &&
                now <= _normalGamepadMonitoringAllowedUntilUtc);
    }

    private bool IsAnyNormalGamepadMonitorRunning()
    {
        return (_guideButtonMonitor.IsRunning && !_guideButtonMonitor.IsPowerIdlePollingPausedForDiagnostics) ||
               (_steamRawInputMonitor.IsRegistered && _steamRawInputMonitor.IsNormalMode) ||
               (_xInputActivityMonitor.IsRunning && !_xInputActivityMonitor.IsWakeOnlyMode);
    }

    private void StartActiveGuideButtonMonitor()
    {
        if (_displayPowerCoordinator.CurrentState is DisplayPowerState.Off or DisplayPowerState.Dimmed)
        {
            return;
        }

        SyncGuideButtonMonitorSteamPolicy();
        _guideButtonMonitor.SetPowerIdlePollingPaused(false);
        if (!_guideButtonMonitor.IsRunning)
        {
            _guideButtonMonitor.Start();
        }
    }

    private void ArmNormalGamepadMonitoring(string reason, bool requireActiveMode = true)
    {
        if (requireActiveMode && _powerIdleMode is not PowerIdleRuntimeMode.Active)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var previousAllowedUntil = _normalGamepadMonitoringAllowedUntilUtc;
        var nextAllowedUntil = now + NormalGamepadMonitoringGrace;
        if (nextAllowedUntil <= previousAllowedUntil)
        {
            return;
        }

        _normalGamepadMonitoringAllowedUntilUtc = nextAllowedUntil;
        if (previousAllowedUntil > now)
        {
            return;
        }

        GuideButtonEventLog.Write(
            "normal_gamepad_monitoring_armed",
            "PowerIdle",
            string.Empty,
            AppDisplayName,
            $"Normal gamepad monitoring armed briefly. reason={reason}; durationMs={(int)NormalGamepadMonitoringGrace.TotalMilliseconds}.");
    }

    private void StopNormalInputMonitors(bool waitForExit = false)
    {
        if (!_guideButtonMonitor.IsRunning &&
            !_steamRawInputMonitor.IsRegistered &&
            !_xInputActivityMonitor.IsRunning)
        {
            return;
        }

        _guideButtonMonitor.SetPowerIdlePollingPaused(true);
        if (_guideButtonMonitor.IsRunning)
        {
            if (waitForExit)
            {
                _guideButtonMonitor.StopForPowerIdle();
            }
            else
            {
                _guideButtonMonitor.Stop();
            }
        }

        if (_steamRawInputMonitor.IsRegistered && _steamRawInputMonitor.IsNormalMode)
        {
            _steamRawInputMonitor.Stop();
        }

        if (_xInputActivityMonitor.IsRunning && !_xInputActivityMonitor.IsWakeOnlyMode)
        {
            if (waitForExit)
            {
                _xInputActivityMonitor.StopForPowerIdle();
            }
            else
            {
                _xInputActivityMonitor.Stop();
            }
        }

        SyncGuideButtonMonitorSteamPolicy();
    }

    private void StartPowerIdleGuideOnlyMonitor(bool allowInitialPressedInput)
    {
        SyncGuideButtonMonitorSteamPolicy();
        _guideButtonMonitor.SetPowerIdlePollingPaused(true, allowInitialPressedInput);
        if (!_guideButtonMonitor.IsRunning)
        {
            _guideButtonMonitor.Start();
        }
    }

    private void StopPowerIdleGuideOnlyMonitor()
    {
        if (_guideButtonMonitor.IsRunning)
        {
            _guideButtonMonitor.Stop();
        }
    }

    private void StopNormalNonGuideInputMonitors()
    {
        if (_steamRawInputMonitor.IsRegistered && _steamRawInputMonitor.IsNormalMode)
        {
            _steamRawInputMonitor.Stop();
        }

        if (_xInputActivityMonitor.IsRunning && !_xInputActivityMonitor.IsWakeOnlyMode)
        {
            _xInputActivityMonitor.Stop();
        }

        SyncGuideButtonMonitorSteamPolicy();
    }

    private void StartPowerIdleXInputActivityMonitor()
    {
        if (!_xInputActivityMonitor.IsRunning || !_xInputActivityMonitor.IsWakeOnlyMode)
        {
            _xInputActivityMonitor.StartWakeOnly();
        }
    }

    private void StartPowerIdleRawInputActivityMonitor()
    {
        if (_steamRawInputWindowHandle != IntPtr.Zero &&
            (!_steamRawInputMonitor.IsRegistered || !_steamRawInputMonitor.IsWakeOnlyMode))
        {
            _steamRawInputMonitor.StartWakeOnly(_steamRawInputWindowHandle);
        }

        SyncGuideButtonMonitorSteamPolicy();
    }

    private void StartWakeOnlyInputMonitors()
    {
        StartPowerIdleGuideOnlyMonitor(allowInitialPressedInput: true);
        StartPowerIdleRawInputActivityMonitor();
        StartPowerIdleXInputActivityMonitor();
    }

    private void StopWakeOnlyInputMonitors(bool waitForExit = false)
    {
        if (!_guideButtonMonitor.IsRunning &&
            !_steamRawInputMonitor.IsRegistered &&
            !_xInputActivityMonitor.IsRunning)
        {
            return;
        }

        if (_guideButtonMonitor.IsRunning && _guideButtonMonitor.IsPowerIdlePollingPausedForDiagnostics)
        {
            if (waitForExit)
            {
                _guideButtonMonitor.StopForPowerIdle();
            }
            else
            {
                _guideButtonMonitor.Stop();
            }
        }

        if (_steamRawInputMonitor.IsRegistered && _steamRawInputMonitor.IsWakeOnlyMode)
        {
            _steamRawInputMonitor.Stop();
        }

        if (_xInputActivityMonitor.IsRunning && _xInputActivityMonitor.IsWakeOnlyMode)
        {
            if (waitForExit)
            {
                _xInputActivityMonitor.StopForPowerIdle();
            }
            else
            {
                _xInputActivityMonitor.Stop();
            }
        }

        SyncGuideButtonMonitorSteamPolicy();
    }

    private void StopWakeOnlyNonGuideInputMonitors()
    {
        if (_steamRawInputMonitor.IsRegistered && _steamRawInputMonitor.IsWakeOnlyMode)
        {
            _steamRawInputMonitor.Stop();
        }

        if (_xInputActivityMonitor.IsRunning && _xInputActivityMonitor.IsWakeOnlyMode)
        {
            _xInputActivityMonitor.Stop();
        }

        SyncGuideButtonMonitorSteamPolicy();
    }

    private void TryWakeDisplayAfterVerifiedInput(string reason)
    {
        if (!ShouldSendDisplayWakeForInput(reason))
        {
            return;
        }

        ArmWakeRecoveryBypassAfterVerifiedInput(reason);

        var now = DateTimeOffset.UtcNow;
        if (now - _lastDisplayWakePulseAtUtc < DisplayWakePulseCooldown)
        {
            return;
        }

        _lastDisplayWakePulseAtUtc = now;
        var success = SystemDisplayPower.TryTurnDisplayOn(ResolveDisplayWakeWindowHandle());
        GuideButtonEventLog.Write(
            success ? "display_on_fallback_sent" : "display_on_fallback_failed",
            "PowerIdle",
            string.Empty,
            AppDisplayName,
            $"Display-on fallback {(success ? "sent" : "failed")}. reason={reason}.");
    }

    private IntPtr ResolveDisplayWakeWindowHandle()
    {
        if (_steamRawInputWindowHandle != IntPtr.Zero)
        {
            return _steamRawInputWindowHandle;
        }

        var handle = new WindowInteropHelper(this).Handle;
        return handle == IntPtr.Zero ? IntPtr.Zero : handle;
    }

    private bool ShouldSendDisplayWakeForInput(string reason)
    {
        var currentState = _displayPowerCoordinator.CurrentState;
        if (currentState is not (DisplayPowerState.Off or DisplayPowerState.Dimmed))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastDisplayOffStateAtUtc < DisplayOffSettleWindow)
        {
            GuideButtonEventLog.Write(
                "display_on_fallback_suppressed",
                "PowerIdle",
                string.Empty,
                AppDisplayName,
                $"Display-on fallback suppressed while display-off command settles. reason={reason}.");
            return false;
        }

        return true;
    }

    private void ArmWakeRecoveryBypassAfterVerifiedInput(string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var until = now + VerifiedInputWakeRecoveryBypassDuration;
        if (_verifiedInputWakeRecoveryBypassUntilUtc > now)
        {
            if (until > _verifiedInputWakeRecoveryBypassUntilUtc)
            {
                _verifiedInputWakeRecoveryBypassUntilUtc = until;
            }

            return;
        }

        _verifiedInputWakeRecoveryBypassUntilUtc = until;
        GuideButtonEventLog.Write(
            "wake_recovery_bypass_armed",
            "PowerIdle",
            string.Empty,
            AppDisplayName,
            $"Wake recovery bypass armed after verified gamepad input. reason={reason}.");
    }

    private bool ShouldBypassWakeRecoveryAfterVerifiedInput(DateTimeOffset now)
    {
        return now <= _verifiedInputWakeRecoveryBypassUntilUtc;
    }

    private bool ShouldDeferBatteryGuideToastUntilDisplayWake()
    {
        return _displayPowerCoordinator.CurrentState is DisplayPowerState.Off or DisplayPowerState.Dimmed ||
               _powerIdleMode == PowerIdleRuntimeMode.DisplayOffWakeOnly;
    }

    private void QueueBatteryGuideToastAfterDisplayWake(GuideButtonPressedEventArgs e)
    {
        _pendingDisplayWakeGuideToast = e;
        _pendingDisplayWakeGuideToastUntilUtc = DateTimeOffset.UtcNow + DisplayWakeGuideToastHold;
        GuideButtonEventLog.Write(
            "guide_toast_deferred_until_display_wake",
            e.DeviceKind.ToString(),
            e.Address,
            e.DisplayName,
            "Guide-button toast deferred until the display reports that it is on.");
        FlushPendingDisplayWakeGuideToast(DateTimeOffset.UtcNow);
    }

    private void FlushPendingDisplayWakeGuideToast(DateTimeOffset now)
    {
        if (_pendingDisplayWakeGuideToast is not { } pending)
        {
            return;
        }

        if (_displayPowerCoordinator.CurrentState != DisplayPowerState.On)
        {
            return;
        }

        if (now > _pendingDisplayWakeGuideToastUntilUtc)
        {
            ClearPendingDisplayWakeGuideToast();
            GuideButtonEventLog.Write(
                "guide_toast_deferred_expired",
                pending.DeviceKind.ToString(),
                pending.Address,
                pending.DisplayName,
                "Deferred guide-button toast expired before the display reported that it was on.");
            return;
        }

        ClearPendingDisplayWakeGuideToast();
        _ = Dispatcher.BeginInvoke(
            new Action(() => _ = ShowBatteryGuideAfterGamepadActivityRefreshAsync(pending)),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ClearPendingDisplayWakeGuideToast()
    {
        _pendingDisplayWakeGuideToast = null;
        _pendingDisplayWakeGuideToastUntilUtc = DateTimeOffset.MinValue;
    }

    private static string FormatPowerIdleSeconds(TimeSpan? value)
    {
        return value is null ? "null" : FormatPowerIdleSeconds(value.Value);
    }

    private static string FormatPowerIdleSeconds(TimeSpan value)
    {
        return value == TimeSpan.MaxValue
            ? "max"
            : Math.Round(value.TotalSeconds, 1, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) + "s";
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.WindowsDisplayOffOptions))
        {
            if (Dispatcher.CheckAccess())
            {
                RefreshWindowsDisplayOffOptions();
            }
            else
            {
                Dispatcher.Invoke(RefreshWindowsDisplayOffOptions);
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.PowerIdlePauseOptions))
        {
            if (Dispatcher.CheckAccess())
            {
                RefreshPowerIdlePauseOptions();
            }
            else
            {
                Dispatcher.Invoke(RefreshPowerIdlePauseOptions);
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.PowerIdlePauseMinutes))
        {
            if (Dispatcher.CheckAccess())
            {
                SyncPowerIdlePauseSelection();
                UpdatePowerIdleGuideMonitoring();
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    SyncPowerIdlePauseSelection();
                    UpdatePowerIdleGuideMonitoring();
                });
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.BatteryGuideTrigger) or
            nameof(MainViewModel.BatteryGuideTriggerProfiles))
        {
            SyncBatteryGuideTriggerInputInterests();
            ClearCustomBatteryGuideTriggerPressState();
            _lastCustomBatteryGuideTriggerToastByBinding.Clear();
            _batteryGuideTriggerCaptureWindow?.SetProfiles(
                _viewModel.BatteryGuideTriggerProfiles,
                _viewModel.BatteryGuideTrigger);
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.BatteryAlertThresholds))
        {
            PrimeBatteryAlertToastKeysForCurrentLevels();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.ColorPresetId))
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyColorPreset(_viewModel.ColorPresetId);
                SyncColorPresetSelection();
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    ApplyColorPreset(_viewModel.ColorPresetId);
                    SyncColorPresetSelection();
                });
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.UseCustomColors)
            or nameof(MainViewModel.CustomTextColor)
            or nameof(MainViewModel.CustomBackgroundColor)
            or nameof(MainViewModel.CustomElementColors))
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyColorPreset(_viewModel.ColorPresetId);
            }
            else
            {
                Dispatcher.Invoke(() => ApplyColorPreset(_viewModel.ColorPresetId));
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.UseCustomFont)
            or nameof(MainViewModel.CustomFontPath))
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyCustomFont();
            }
            else
            {
                Dispatcher.Invoke(ApplyCustomFont);
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.Language))
        {
            if (Dispatcher.CheckAccess())
            {
                SyncLanguageSelection();
                RefreshTrayMenuTexts();
                UpdateVersionMenuHeader();
                UpdateGuideSoundControls();
                RefreshPowerIdlePauseOptions();
                RefreshWindowsDisplayOffOptions();
                UpdateColorEditorState();
                _batteryGuideTriggerCaptureWindow?.ApplyLocalizedText(_viewModel.Language);
                if (!_isPicoFirmwareUpdating)
                {
                    SetPicoFirmwareUpdateStatus(_viewModel.TextPicoFirmwareReady);
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    SyncLanguageSelection();
                    RefreshTrayMenuTexts();
                    UpdateVersionMenuHeader();
                    UpdateGuideSoundControls();
                    RefreshPowerIdlePauseOptions();
                    RefreshWindowsDisplayOffOptions();
                    UpdateColorEditorState();
                    _batteryGuideTriggerCaptureWindow?.ApplyLocalizedText(_viewModel.Language);
                    if (!_isPicoFirmwareUpdating)
                    {
                        SetPicoFirmwareUpdateStatus(_viewModel.TextPicoFirmwareReady);
                    }
                });
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.GuideSoundEnabled)
            or nameof(MainViewModel.GuideSoundId)
            or nameof(MainViewModel.CustomGuideSoundPath))
        {
            if (Dispatcher.CheckAccess())
            {
                SyncGuideSoundSelection();
                UpdateGuideSoundControls();
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    SyncGuideSoundSelection();
                    UpdateGuideSoundControls();
                });
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.AutostartEnabled)
            or nameof(MainViewModel.StartMinimizedToTrayEnabled))
        {
            if (Dispatcher.CheckAccess())
            {
                RefreshTrayMenuTexts();
            }
            else
            {
                Dispatcher.Invoke(RefreshTrayMenuTexts);
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.StatusPanelCollapsed))
        {
            if (_viewModel.StatusPanelCollapsed)
            {
                CloseSettingsPopup();
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.UiScaleStep))
        {
            ApplyUiScaleStep(_viewModel.UiScaleStep);
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.UseCustomSettingsTextStyle)
            or nameof(MainViewModel.SettingsTextFontSize)
            or nameof(MainViewModel.SettingsTextBold))
        {
            if (Dispatcher.CheckAccess())
            {
                ApplySettingsTextStyle();
            }
            else
            {
                Dispatcher.Invoke(ApplySettingsTextStyle);
            }

            return;
        }

        if (e.PropertyName is not nameof(MainViewModel.IsLiteVisualMode) and not nameof(MainViewModel.VisualMode))
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            ApplyVisualModeState();
            return;
        }

        Dispatcher.Invoke(ApplyVisualModeState);
    }

    private void SyncColorPresetSelection()
    {
        if (ColorPresetComboBox is null)
        {
            return;
        }

        var normalized = WidgetSettings.NormalizeColorPresetId(_viewModel.ColorPresetId);
        var selected = ColorPresetCatalog.Presets
            .FirstOrDefault(preset => string.Equals(preset.Id, normalized, StringComparison.Ordinal));
        if (selected is null)
        {
            return;
        }

        _isColorPresetSyncing = true;
        try
        {
            ColorPresetComboBox.SelectedItem = selected;
        }
        finally
        {
            _isColorPresetSyncing = false;
        }
    }

    private void ColorPresetMarqueeText_Loaded(object sender, RoutedEventArgs e)
    {
        QueueColorPresetMarqueeReset(sender as FrameworkElement);
    }

    private void ColorPresetMarqueeText_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueColorPresetMarqueeReset(sender as FrameworkElement);
    }

    private void ColorPresetMarqueeViewport_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject source)
        {
            QueueColorPresetMarqueeReset(FindColorPresetMarqueeText(source));
        }
    }

    private void ColorPresetMarqueeViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is DependencyObject source)
        {
            QueueColorPresetMarqueeReset(FindColorPresetMarqueeText(source));
        }
    }

    private void ColorPresetMarqueeViewport_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is DependencyObject source)
        {
            QueueColorPresetMarqueeUpdate(FindColorPresetMarqueeText(source));
        }
    }

    private void ColorPresetMarqueeViewport_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is DependencyObject source)
        {
            QueueColorPresetMarqueeReset(FindColorPresetMarqueeText(source));
        }
    }

    private static void QueueColorPresetMarqueeUpdate(FrameworkElement? marqueeElement)
    {
        if (marqueeElement is null || marqueeElement.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        marqueeElement.Dispatcher.BeginInvoke(
            () =>
            {
                try
                {
                    UpdateColorPresetMarquee(marqueeElement);
                }
                catch (InvalidOperationException)
                {
                    ResetColorPresetMarqueeTransform(marqueeElement);
                }
            },
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void QueueColorPresetMarqueeReset(FrameworkElement? marqueeElement)
    {
        if (marqueeElement is null || marqueeElement.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        marqueeElement.Dispatcher.BeginInvoke(
            () =>
            {
                try
                {
                    ResetColorPresetMarqueeTransform(marqueeElement);
                }
                catch (InvalidOperationException)
                {
                    // Decorative text reset should never terminate the widget.
                }
            },
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void UpdateColorPresetMarquee(FrameworkElement marqueeElement)
    {
        var transform = marqueeElement.RenderTransform as TranslateTransform;
        if (transform is null || transform.IsFrozen)
        {
            transform = ResetColorPresetMarqueeTransform(marqueeElement);
        }
        else
        {
            try
            {
                transform.BeginAnimation(TranslateTransform.XProperty, null);
            }
            catch (InvalidOperationException)
            {
                transform = ResetColorPresetMarqueeTransform(marqueeElement);
            }
        }

        var viewport = FindColorPresetMarqueeViewport(marqueeElement);
        if (viewport is null || viewport.ActualWidth <= 1d)
        {
            transform.X = 0d;
            return;
        }

        var restingX = GetMarqueeRestingX(marqueeElement, viewport);
        transform.X = restingX;

        if (marqueeElement.ActualWidth <= viewport.ActualWidth + 1d)
        {
            return;
        }

        var overflow = Math.Ceiling(marqueeElement.ActualWidth - viewport.ActualWidth + ColorPresetMarqueeGap);
        var scrollSeconds = Math.Max(
            overflow / ColorPresetMarqueePixelsPerSecond,
            2.4d);
        var totalSeconds = ColorPresetMarqueeStartDelaySeconds + scrollSeconds + ColorPresetMarqueeEndDelaySeconds;

        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(totalSeconds),
            RepeatBehavior = RepeatBehavior.Forever
        };

        animation.KeyFrames.Add(new LinearDoubleKeyFrame(restingX, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(restingX, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(ColorPresetMarqueeStartDelaySeconds))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(ColorPresetMarqueeStartDelaySeconds + scrollSeconds))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(totalSeconds))));

        try
        {
            transform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
        catch (InvalidOperationException)
        {
            ResetColorPresetMarqueeTransform(marqueeElement);
        }
    }

    private static TranslateTransform ResetColorPresetMarqueeTransform(FrameworkElement marqueeElement)
    {
        var transform = new TranslateTransform();
        marqueeElement.RenderTransform = transform;
        var viewport = FindColorPresetMarqueeViewport(marqueeElement);
        transform.X = viewport is null ? 0d : GetMarqueeRestingX(marqueeElement, viewport);
        return transform;
    }

    private static double GetMarqueeRestingX(FrameworkElement marqueeElement, FrameworkElement viewport)
    {
        if (viewport.ActualWidth <= 1d ||
            marqueeElement.ActualWidth <= 1d ||
            marqueeElement.ActualWidth > viewport.ActualWidth + 1d)
        {
            return 0d;
        }

        var current = GetDependencyParent(viewport);
        while (current is not null)
        {
            if (current is WpfControls.ComboBox comboBox)
            {
                return comboBox.HorizontalContentAlignment == System.Windows.HorizontalAlignment.Center
                    ? Math.Max(0d, (viewport.ActualWidth - marqueeElement.ActualWidth) / 2d)
                    : 0d;
            }

            current = GetDependencyParent(current);
        }

        return 0d;
    }

    private static FrameworkElement? FindColorPresetMarqueeViewport(DependencyObject source)
    {
        var current = GetDependencyParent(source);
        while (current is not null)
        {
            if (current is FrameworkElement viewport &&
                (string.Equals(viewport.Name, "ColorPresetMarqueeViewport", StringComparison.Ordinal) ||
                 string.Equals(viewport.Name, "ComboMarqueeViewport", StringComparison.Ordinal)))
            {
                return viewport;
            }

            current = GetDependencyParent(current);
        }

        return null;
    }

    private static FrameworkElement? FindColorPresetMarqueeText(DependencyObject source)
    {
        if (source is FrameworkElement marqueeElement &&
            (string.Equals(marqueeElement.Name, "ColorPresetMarqueeText", StringComparison.Ordinal) ||
             string.Equals(marqueeElement.Name, "ComboMarqueeText", StringComparison.Ordinal)))
        {
            return marqueeElement;
        }

        var childCount = 0;
        try
        {
            childCount = VisualTreeHelper.GetChildrenCount(source);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(source, i);
            var found = FindColorPresetMarqueeText(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void SyncLanguageSelection()
    {
        if (LanguageComboBox is null)
        {
            return;
        }

        var normalized = WidgetSettings.NormalizeLanguage(_viewModel.Language);
        var selected = _viewModel.LanguageOptions
            .FirstOrDefault(option => string.Equals(option.Id, normalized, StringComparison.Ordinal));
        if (selected is null)
        {
            return;
        }

        _isLanguageSyncing = true;
        try
        {
            LanguageComboBox.SelectedItem = selected;
        }
        finally
        {
            _isLanguageSyncing = false;
        }
    }

    private void SyncPowerIdlePauseSelection()
    {
        if (PowerIdlePauseComboBox is null)
        {
            return;
        }

        var normalized = WidgetSettings.NormalizePowerIdlePauseMinutes(_viewModel.PowerIdlePauseMinutes);
        var selected = _viewModel.PowerIdlePauseOptions
            .FirstOrDefault(option => option.Minutes == normalized);
        selected ??= _viewModel.PowerIdlePauseOptions
            .OrderBy(option => Math.Abs(option.Minutes - normalized))
            .FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        _isPowerIdlePauseSyncing = true;
        try
        {
            PowerIdlePauseComboBox.SelectedItem = selected;
        }
        finally
        {
            _isPowerIdlePauseSyncing = false;
        }
    }

    private void RefreshPowerIdlePauseOptions()
    {
        if (PowerIdlePauseComboBox is null)
        {
            return;
        }

        PowerIdlePauseComboBox.ItemsSource = _viewModel.PowerIdlePauseOptions;
        SyncPowerIdlePauseSelection();
    }

    private void SyncWindowsDisplayOffSelection()
    {
        if (WindowsDisplayOffComboBox is null)
        {
            return;
        }

        var currentMinutes = _viewModel.GetCurrentWindowsDisplayOffMinutes();
        var selected = _viewModel.WindowsDisplayOffOptions
            .FirstOrDefault(option => option.Minutes == currentMinutes);
        if (selected is null)
        {
            return;
        }

        _isWindowsDisplayOffSyncing = true;
        try
        {
            WindowsDisplayOffComboBox.SelectedItem = selected;
        }
        finally
        {
            _isWindowsDisplayOffSyncing = false;
        }
    }

    private void RefreshWindowsDisplayOffOptions()
    {
        if (WindowsDisplayOffComboBox is null)
        {
            return;
        }

        WindowsDisplayOffComboBox.ItemsSource = _viewModel.WindowsDisplayOffOptions;
        SyncWindowsDisplayOffSelection();
    }

    private void SyncGuideSoundSelection()
    {
        if (GuideSoundComboBox is null)
        {
            return;
        }

        var options = BatteryGuideSoundCatalog.GetGuideOptions(_viewModel.CustomGuideSoundPath, _viewModel.Language);
        var selected = BatteryGuideSoundCatalog.ResolveGuideSound(
            _viewModel.GuideSoundId,
            _viewModel.CustomGuideSoundPath,
            _viewModel.Language);
        _isGuideSoundSyncing = true;
        try
        {
            GuideSoundComboBox.ItemsSource = options;
            GuideSoundComboBox.SelectedItem = selected;
        }
        finally
        {
            _isGuideSoundSyncing = false;
        }
    }

    private void UpdateGuideSoundControls()
    {
        if (GuideSoundComboBox is not null)
        {
            GuideSoundComboBox.IsEnabled = _viewModel.GuideSoundEnabled;
            GuideSoundComboBox.Opacity = _viewModel.GuideSoundEnabled ? 1.0 : 0.52;
        }

        if (PreviewGuideSoundButton is not null)
        {
            PreviewGuideSoundButton.IsEnabled = _viewModel.GuideSoundEnabled;
            PreviewGuideSoundButton.Opacity = _viewModel.GuideSoundEnabled ? 1.0 : 0.52;
            PreviewGuideSoundButton.Content = _isGuideSoundPreviewPlaying
                ? GuideSoundPreviewStopText
                : GuideSoundPreviewPlayText;
            PreviewGuideSoundButton.ToolTip = _isGuideSoundPreviewPlaying
                ? _viewModel.TextGuideSoundPreviewStopTooltip
                : _viewModel.TextGuideSoundPreviewTooltip;
        }
    }

    private void SetGuideSoundPreviewPlaying(bool isPlaying)
    {
        _guideSoundPreviewResetTimer.Stop();
        _isGuideSoundPreviewPlaying = isPlaying && _viewModel.GuideSoundEnabled;

        if (_isGuideSoundPreviewPlaying)
        {
            _guideSoundPreviewResetTimer.Interval = GuideSoundPreviewSafetyTimeout;
            _guideSoundPreviewResetTimer.Start();
        }

        UpdateGuideSoundControls();
    }

    private void StopGuideSoundPreview()
    {
        StopBatteryGuideChime();
        SetGuideSoundPreviewPlaying(false);
    }

    private void ApplyUiScaleStep(int step, bool animate = true)
    {
        var normalizedStep = WidgetSettings.NormalizeUiScaleStep(step);
        var scale = 1d + (normalizedStep * UiScaleStepFactor);
        if (UiScaleTransform is not null)
        {
            ApplyScaleTransform(UiScaleTransform, scale, animate);
        }

        UpdateResizeGripPlacement(scale);
    }

    private void UpdateResizeGripPlacement(double? targetScale = null)
    {
        if (ResizeGripThumb is null || GlassCard is null)
        {
            return;
        }

        var scale = targetScale ?? UiScaleTransform?.ScaleX ?? 1d;
        if (double.IsNaN(scale) || double.IsInfinity(scale))
        {
            scale = 1d;
        }

        var shrinkFactor = Math.Max(0d, 1d - scale);
        var rightInset = ResizeGripBaseInset + (GlassCard.ActualWidth * shrinkFactor);
        var bottomInset = ResizeGripBaseInset + (GlassCard.ActualHeight * shrinkFactor);
        ResizeGripThumb.Margin = new Thickness(0, 0, rightInset, bottomInset);
    }

    private static void ApplyScaleTransform(ScaleTransform transform, double scale, bool animate)
    {
        if (!animate)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transform.ScaleX = scale;
            transform.ScaleY = scale;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(UiScaleAnimationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scaleXAnimation = new DoubleAnimation(scale, duration) { EasingFunction = easing };
        var scaleYAnimation = new DoubleAnimation(scale, duration) { EasingFunction = easing };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation, HandoffBehavior.SnapshotAndReplace);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private double GetStatusPanelOccupiedHeight()
    {
        if (StatusPanelBorder is null)
        {
            return StatusPanelFallbackHeight;
        }

        var height = StatusPanelBorder.ActualHeight + StatusPanelBorder.Margin.Top;
        return height > 0 ? height : StatusPanelFallbackHeight;
    }

    private void ApplyStatusPanelHeightAdjustment(bool collapsed)
    {
        if (!_initialBoundsApplied || WindowState != WindowState.Normal)
        {
            return;
        }

        var delta = _statusPanelCollapsedHeightDelta > 0
            ? _statusPanelCollapsedHeightDelta
            : StatusPanelFallbackHeight;

        if (collapsed)
        {
            var targetHeight = Math.Max(MinHeight, Height - delta);
            if (targetHeight < Height)
            {
                Height = targetHeight;
                SaveWindowBounds();
            }

            return;
        }

        var workingAreas = GetWorkingAreas();
        var currentBounds = new WindowBounds
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };
        var area = workingAreas.Count > 0
            ? SelectBestArea(currentBounds, workingAreas)
            : new WindowBounds
            {
                Left = SystemParameters.WorkArea.Left,
                Top = SystemParameters.WorkArea.Top,
                Width = SystemParameters.WorkArea.Width,
                Height = SystemParameters.WorkArea.Height
            };
        var maxHeight = Math.Max(MinHeight, Math.Min(area.Height - StartupSafetyMargin, StartupMaxAbsoluteHeight));
        var targetExpanded = Math.Min(maxHeight, Height + delta);
        if (targetExpanded > Height)
        {
            Height = targetExpanded;
            SaveWindowBounds();
        }
    }

    private void ApplyColorPreset(string presetId)
    {
        var preset = ColorPresetCatalog.GetById(presetId);
        var useExactWhiteBlue = string.Equals(preset.Id, WidgetSettings.WhiteBluePreset, StringComparison.Ordinal);

        SetResourceColor("PrimaryTextBrush", preset.PrimaryText);
        SetResourceColor("SecondaryTextBrush", preset.SecondaryText);
        SetResourceColor("BatteryTextBrush", preset.BatteryText);
        ApplyFixedSettingsTextResources();
        SetResourceColor("CardTintBrush", useExactWhiteBlue ? preset.CardTint : EnhanceThemeColor(preset.CardTint, 1.28d, 1.15d, 1.08d));
        SetResourceColor("CardBorderBrush", useExactWhiteBlue ? preset.CardBorder : EnhanceThemeColor(preset.CardBorder, 1.34d, 1.12d, 1.10d));
        SetResourceColor("TrackBrush", useExactWhiteBlue ? preset.Track : EnhanceThemeColor(preset.Track, 1.30d, 1.12d, 1.10d));
        SetResourceColor("IconBackBrush", useExactWhiteBlue ? preset.IconBack : EnhanceThemeColor(preset.IconBack, 1.20d, 1.08d, 1.05d));
        SetResourceColor("IconBorderBrush", useExactWhiteBlue ? preset.IconBorder : EnhanceThemeColor(preset.IconBorder, 1.24d, 1.10d, 1.08d));
        SetResourceColor("ActionButtonBackBrush", EnsureMinimumLuminance(
            useExactWhiteBlue ? preset.ActionButtonBack : EnhanceThemeColor(preset.ActionButtonBack, 1.22d, 1.12d, 1.10d),
            0.56d));
        SetResourceColor("ActionButtonBorderBrush", EnsureMinimumLuminance(
            useExactWhiteBlue ? preset.ActionButtonBorder : EnhanceThemeColor(preset.ActionButtonBorder, 1.26d, 1.10d, 1.10d),
            0.48d));

        if (ListTopStop is not null)
        {
            ListTopStop.Color = useExactWhiteBlue ? preset.ListTop : EnhanceThemeColor(preset.ListTop, 1.24d, 1.10d, 1.08d);
        }

        if (ListBottomStop is not null)
        {
            ListBottomStop.Color = useExactWhiteBlue ? preset.ListBottom : EnhanceThemeColor(preset.ListBottom, 1.28d, 1.12d, 1.10d);
        }

        if (FooterTopStop is not null)
        {
            FooterTopStop.Color = useExactWhiteBlue ? preset.FooterTop : EnhanceThemeColor(preset.FooterTop, 1.22d, 1.10d, 1.08d);
        }

        if (FooterBottomStop is not null)
        {
            FooterBottomStop.Color = useExactWhiteBlue ? preset.FooterBottom : EnhanceThemeColor(preset.FooterBottom, 1.26d, 1.12d, 1.10d);
        }

        if (useExactWhiteBlue)
        {
            ApplyWhiteBlueGlassDefaults();
        }
        else
        {
            SetResourceColor("GlassSurfaceBrush", WithAlpha(
                EnhanceThemeColor(BlendColors(preset.ListTop, preset.CardTint, 0.22d), 1.12d, 1.08d, 1.04d),
                232));
            ApplyGlassAtmosphere(preset);
        }

        if (_viewModel.UseCustomColors)
        {
            foreach (var pair in _viewModel.CustomElementColors)
            {
                if (TryParseWpfColor(pair.Value, out var color))
                {
                    ApplyCustomElementColorResource(pair.Key, color);
                }
            }
        }

        ApplyFixedSettingsTextResources();
        UpdateColorEditorState();
    }

    private void ApplyFixedSettingsTextResources()
    {
        var titleColor = FixedSettingsTitleColor;
        var textColor = FixedSettingsTextColor;
        if (_viewModel.UseCustomSettingsTextStyle &&
            _viewModel.CustomElementColors.TryGetValue("SettingsText", out var configuredColor) &&
            TryParseWpfColor(configuredColor, out var parsedColor))
        {
            titleColor = WithAlpha(parsedColor, byte.MaxValue);
            textColor = titleColor;
        }

        SetResourceColor(SettingsTitleBrushResourceKey, titleColor);
        SetResourceColor(SettingsTextBrushResourceKey, textColor);
    }

    private void ApplyWhiteBlueGlassDefaults()
    {
        if (GsTop is not null)
        {
            GsTop.Color = System.Windows.Media.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
        }

        if (GsMid is not null)
        {
            GsMid.Color = System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
        }

        if (GsBottom is not null)
        {
            GsBottom.Color = System.Windows.Media.Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
        }

        SetResourceColor("WidgetBackgroundBrush", System.Windows.Media.Color.FromRgb(0xEC, 0xF5, 0xFF));
        SetResourceColor("GlassSurfaceBrush", System.Windows.Media.Color.FromArgb(0xF2, 0xEA, 0xF6, 0xFF));
        ApplyGlassTexture(
            System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF),
            System.Windows.Media.Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF),
            System.Windows.Media.Color.FromArgb(0x24, 0xCF, 0xEA, 0xFF),
            System.Windows.Media.Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF),
            System.Windows.Media.Color.FromArgb(0x1E, 0x9B, 0xC4, 0xDF));
    }

    private void ApplyGlassAtmosphere(ColorPreset preset)
    {
        SetResourceColor("WidgetBackgroundBrush", BlendColors(preset.FooterBottom, preset.ListBottom, 0.32d));

        if (GsTop is not null)
        {
            GsTop.Color = WithAlpha(BlendColors(preset.CardTint, System.Windows.Media.Colors.White, 0.12d), 42);
        }

        if (GsMid is not null)
        {
            GsMid.Color = WithAlpha(BlendColors(preset.ListBottom, preset.BatteryText, 0.08d), 62);
        }

        if (GsBottom is not null)
        {
            GsBottom.Color = WithAlpha(BlendColors(preset.FooterBottom, preset.BatteryText, 0.10d), 88);
        }

        ApplyGlassTexture(
            WithAlpha(BlendColors(preset.CardTint, System.Windows.Media.Colors.White, 0.20d), 46),
            WithAlpha(BlendColors(preset.ListTop, preset.BatteryText, 0.08d), 22),
            WithAlpha(BlendColors(preset.FooterBottom, preset.BatteryText, 0.18d), 54),
            WithAlpha(BlendColors(preset.CardBorder, System.Windows.Media.Colors.White, 0.18d), 36),
            WithAlpha(BlendColors(preset.ListBottom, preset.BatteryText, 0.20d), 44));
    }

    private void ApplyGlassTexture(
        System.Windows.Media.Color sheenTop,
        System.Windows.Media.Color sheenMiddle,
        System.Windows.Media.Color sheenBottom,
        System.Windows.Media.Color depthTop,
        System.Windows.Media.Color depthBottom)
    {
        if (GlassSheenTop is not null)
        {
            GlassSheenTop.Color = sheenTop;
        }

        if (GlassSheenMiddle is not null)
        {
            GlassSheenMiddle.Color = sheenMiddle;
        }

        if (GlassSheenBottom is not null)
        {
            GlassSheenBottom.Color = sheenBottom;
        }

        if (GlassDepthTop is not null)
        {
            GlassDepthTop.Color = depthTop;
        }

        if (GlassDepthBottom is not null)
        {
            GlassDepthBottom.Color = depthBottom;
        }
    }

    private void ApplyCustomColorResources(System.Windows.Media.Color textColor, System.Windows.Media.Color backgroundColor)
    {
        var text = WithAlpha(textColor, byte.MaxValue);
        var background = WithAlpha(backgroundColor, byte.MaxValue);
        var secondaryText = BlendColors(text, background, 0.28d);
        var surfaceBlend = GetRelativeSimpleLuminance(background) < 0.45d
            ? System.Windows.Media.Colors.White
            : System.Windows.Media.Colors.Black;
        var softSurface = BlendColors(background, surfaceBlend, 0.10d);
        var softerSurface = BlendColors(background, surfaceBlend, 0.18d);
        var lineSurface = BlendColors(background, text, 0.24d);

        SetResourceColor("PrimaryTextBrush", text);
        SetResourceColor("SecondaryTextBrush", secondaryText);
        SetResourceColor("BatteryTextBrush", text);
        ApplyFixedSettingsTextResources();
        SetResourceColor("CardTintBrush", WithAlpha(background, 238));
        SetResourceColor("CardBorderBrush", WithAlpha(lineSurface, 218));
        SetResourceColor("TrackBrush", WithAlpha(BlendColors(background, text, 0.12d), 142));
        SetResourceColor("IconBackBrush", WithAlpha(softerSurface, 232));
        SetResourceColor("IconBorderBrush", WithAlpha(lineSurface, 170));
        SetResourceColor("ActionButtonBackBrush", WithAlpha(softSurface, 218));
        SetResourceColor("ActionButtonBorderBrush", WithAlpha(lineSurface, 160));
        ApplyGlassSurfaceColor(softerSurface);

        if (ListTopStop is not null)
        {
            ListTopStop.Color = WithAlpha(softerSurface, 224);
        }

        if (ListBottomStop is not null)
        {
            ListBottomStop.Color = WithAlpha(background, 196);
        }

        if (FooterTopStop is not null)
        {
            FooterTopStop.Color = WithAlpha(softSurface, 214);
        }

        if (FooterBottomStop is not null)
        {
            FooterBottomStop.Color = WithAlpha(background, 186);
        }

        ApplyWidgetBackgroundColor(background);
    }

    private void ApplyCustomElementColorResource(string elementKey, System.Windows.Media.Color color)
    {
        var opaqueColor = WithAlpha(color, byte.MaxValue);
        switch (elementKey)
        {
            case "PrimaryText":
                SetResourceColor("PrimaryTextBrush", opaqueColor);
                break;
            case "SecondaryText":
                SetResourceColor("SecondaryTextBrush", opaqueColor);
                break;
            case "BatteryText":
                SetResourceColor("BatteryTextBrush", opaqueColor);
                break;
            case "WidgetBackground":
                ApplyWidgetBackgroundColor(opaqueColor);
                break;
            case "GlassSurface":
                ApplyGlassSurfaceColor(opaqueColor);
                break;
            case "CardTint":
                SetResourceColor("CardTintBrush", WithAlpha(opaqueColor, 238));
                break;
            case "CardBorder":
                SetResourceColor("CardBorderBrush", WithAlpha(opaqueColor, 230));
                SetResourceColor("IconBorderBrush", WithAlpha(opaqueColor, 210));
                break;
            case "Track":
                SetResourceColor("TrackBrush", WithAlpha(opaqueColor, 154));
                break;
            case "Panel":
                SetResourceColor("ActionButtonBackBrush", WithAlpha(opaqueColor, 218));
                SetResourceColor("ActionButtonBorderBrush", WithAlpha(BlendColors(opaqueColor, System.Windows.Media.Colors.Black, 0.18d), 172));
                SetResourceColor("IconBackBrush", WithAlpha(BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.14d), 226));
                if (FooterTopStop is not null)
                {
                    FooterTopStop.Color = WithAlpha(BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.18d), 218);
                }

                if (FooterBottomStop is not null)
                {
                    FooterBottomStop.Color = WithAlpha(opaqueColor, 190);
                }

                break;
            case "SettingsText":
                ApplyFixedSettingsTextResources();
                break;
        }
    }

    private void ApplyWidgetBackgroundColor(System.Windows.Media.Color color)
    {
        var opaqueColor = WithAlpha(color, byte.MaxValue);
        var brightColor = BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.28d);
        var softColor = BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.14d);
        var depthColor = BlendColors(opaqueColor, System.Windows.Media.Colors.Black, 0.12d);

        SetResourceColor("WidgetBackgroundBrush", opaqueColor);

        if (GsTop is not null)
        {
            GsTop.Color = WithAlpha(BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.42d), 50);
        }

        if (GsMid is not null)
        {
            GsMid.Color = WithAlpha(brightColor, 72);
        }

        if (GsBottom is not null)
        {
            GsBottom.Color = WithAlpha(opaqueColor, 122);
        }

        ApplyGlassTexture(
            WithAlpha(BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.38d), 50),
            WithAlpha(softColor, 24),
            WithAlpha(BlendColors(opaqueColor, System.Windows.Media.Colors.Black, 0.04d), 62),
            WithAlpha(brightColor, 36),
            WithAlpha(depthColor, 52));
    }

    private void ApplyGlassSurfaceColor(System.Windows.Media.Color color)
    {
        var opaqueColor = WithAlpha(color, byte.MaxValue);
        var surface = BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.18d);
        var top = BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.26d);
        var bottom = BlendColors(opaqueColor, System.Windows.Media.Colors.White, 0.10d);

        SetResourceColor("GlassSurfaceBrush", WithAlpha(surface, 232));

        if (ListTopStop is not null)
        {
            ListTopStop.Color = WithAlpha(top, 222);
        }

        if (ListBottomStop is not null)
        {
            ListBottomStop.Color = WithAlpha(bottom, 198);
        }
    }

    private void ApplyCustomFont()
    {
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        if (!_viewModel.UseCustomFont || string.IsNullOrWhiteSpace(_viewModel.CustomFontPath))
        {
            return;
        }

        try
        {
            if (!File.Exists(_viewModel.CustomFontPath))
            {
                return;
            }

            var fontFamily = System.Windows.Media.Fonts
                .GetFontFamilies(_viewModel.CustomFontPath)
                .FirstOrDefault();
            if (fontFamily is not null)
            {
                FontFamily = fontFamily;
            }
        }
        catch
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        }
    }

    private void ApplySettingsTextStyle()
    {
        ApplyFixedSettingsTextResources();

        if (SettingsPopupChrome is null)
        {
            return;
        }

        if (!_viewModel.UseCustomSettingsTextStyle)
        {
            RestoreDefaultSettingsTextStyles();
            return;
        }

        var fontSize = WidgetSettings.NormalizeSettingsTextFontSize(_viewModel.SettingsTextFontSize);
        var fontWeight = _viewModel.SettingsTextBold ? FontWeights.Bold : FontWeights.Normal;
        ApplySettingsTextStyle(SettingsPopupChrome, fontSize, fontWeight);
    }

    private void RestoreDefaultSettingsTextStyles()
    {
        foreach (var pair in _settingsTextBlockDefaults.ToArray())
        {
            pair.Key.FontSize = pair.Value.FontSize;
            pair.Key.FontWeight = pair.Value.FontWeight;
        }

        foreach (var pair in _settingsControlTextDefaults.ToArray())
        {
            pair.Key.FontSize = pair.Value.FontSize;
            pair.Key.FontWeight = pair.Value.FontWeight;
        }
    }

    private void ApplySettingsTextStyle(DependencyObject root, double fontSize, FontWeight fontWeight)
    {
        var childrenCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childrenCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is WpfControls.TextBlock textBlock)
            {
                ApplySettingsTextBlockStyle(textBlock, fontSize, fontWeight);
            }
            else if (child is WpfControls.Control control)
            {
                ApplySettingsControlTextStyle(control, fontSize, fontWeight);
            }

            ApplySettingsTextStyle(child, fontSize, fontWeight);
        }
    }

    private void ApplySettingsTextBlockStyle(WpfControls.TextBlock textBlock, double fontSize, FontWeight fontWeight)
    {
        if (IsSettingsGlyphText(textBlock) ||
            IsInsideSettingsTextStyleExcludedControl(textBlock))
        {
            return;
        }

        _settingsTextBlockDefaults.TryAdd(textBlock, (textBlock.FontSize, textBlock.FontWeight));
        textBlock.FontSize = fontSize;
        textBlock.FontWeight = fontWeight;
    }

    private void ApplySettingsControlTextStyle(WpfControls.Control control, double fontSize, FontWeight fontWeight)
    {
        if (control is WpfControls.Slider or WpfControls.ProgressBar or WpfThumb or WpfToggleButton ||
            IsSettingsTextStyleExcludedControl(control) ||
            IsInsideSettingsTextStyleExcludedControl(control))
        {
            return;
        }

        _settingsControlTextDefaults.TryAdd(control, (control.FontSize, control.FontWeight));
        control.FontSize = fontSize;
        control.FontWeight = fontWeight;
    }

    private static bool IsSettingsGlyphText(WpfControls.TextBlock textBlock)
    {
        var name = textBlock.Name ?? string.Empty;
        return name.EndsWith("Arrow", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(textBlock.FontFamily?.Source, "Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSettingsTextStyleExcludedControl(WpfControls.Control control)
    {
        return control is WpfControls.ComboBox or WpfControls.ComboBoxItem;
    }

    private static bool IsInsideSettingsTextStyleExcludedControl(DependencyObject element)
    {
        for (var parent = GetVisualOrLogicalParent(element); parent is not null; parent = GetVisualOrLogicalParent(parent))
        {
            if (parent is WpfControls.ComboBox or WpfControls.ComboBoxItem)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject element)
    {
        var visualParent = element is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(element)
            : null;

        return visualParent ?? LogicalTreeHelper.GetParent(element);
    }

    private void UpdateSelectedColorFromPalette(System.Windows.Point point)
    {
        var width = Math.Max(1d, PaletteSurface.ActualWidth);
        var height = Math.Max(1d, PaletteSurface.ActualHeight);
        var x = Math.Clamp(point.X, 0d, width);
        var y = Math.Clamp(point.Y, 0d, height);
        var color = GetPaletteColor(x, y, width, height);
        var hex = ToRgbHex(color);

        PaletteCursor.Margin = new Thickness(x - 8d, y - 8d, 0d, 0d);
        _viewModel.SetCustomElementColor(_selectedColorElementKey, hex, save: false);
        ApplyCustomElementColorResource(_selectedColorElementKey, color);
        UpdateColorEditorState();
    }

    private void ApplySelectedQuickColor(System.Windows.Media.Color color)
    {
        var hex = ToRgbHex(color);
        _viewModel.SetCustomElementColor(_selectedColorElementKey, hex);
        ApplyCustomElementColorResource(_selectedColorElementKey, color);
        UpdateColorEditorState();
    }

    private void FinishPaletteDrag()
    {
        _isPaletteDragging = false;
        PaletteSurface.ReleaseMouseCapture();
        _viewModel.SaveSettings();
    }

    private void UpdateColorEditorState()
    {
        if (PrimaryTextSwatch is null)
        {
            return;
        }

        SetSwatch(PrimaryTextSwatch, "PrimaryTextBrush");
        SetSwatch(SecondaryTextSwatch, "SecondaryTextBrush");
        SetSwatch(BatteryTextSwatch, "BatteryTextBrush");
        SetSwatch(WidgetBackgroundSwatch, "WidgetBackgroundBrush");
        SetSwatch(GlassSurfaceSwatch, "GlassSurfaceBrush");
        SetSwatch(CardTintSwatch, "CardTintBrush");
        SetSwatch(CardBorderSwatch, "CardBorderBrush");
        SetSwatch(TrackSwatch, "TrackBrush");
        SetSwatch(PanelSwatch, "ActionButtonBackBrush");
        SetSwatch(SettingsTextSwatch, SettingsTextBrushResourceKey);
        SetColorElementButtonState(PrimaryTextColorButton, "PrimaryText");
        SetColorElementButtonState(SecondaryTextColorButton, "SecondaryText");
        SetColorElementButtonState(BatteryTextColorButton, "BatteryText");
        SetColorElementButtonState(WidgetBackgroundColorButton, "WidgetBackground");
        SetColorElementButtonState(GlassSurfaceColorButton, "GlassSurface");
        SetColorElementButtonState(CardTintColorButton, "CardTint");
        SetColorElementButtonState(CardBorderColorButton, "CardBorder");
        SetColorElementButtonState(TrackColorButton, "Track");
        SetColorElementButtonState(PanelColorButton, "Panel");
        SetColorElementButtonState(SettingsTextColorButton, "SettingsText");

        SelectedColorNameText.Text = GetColorElementLabel(_selectedColorElementKey);
        if (TryGetElementCurrentColor(_selectedColorElementKey, out var selectedColor))
        {
            SelectedColorPreviewBorder.Background = new SolidColorBrush(selectedColor);
            SelectedColorHexText.Text = ToRgbHex(selectedColor);
        }
    }

    private string GetColorElementLabel(string elementKey)
    {
        return elementKey switch
        {
            "PrimaryText" => _viewModel.TextColorTargetPrimaryText,
            "SecondaryText" => _viewModel.TextColorTargetSecondaryText,
            "BatteryText" => _viewModel.TextColorTargetBatteryText,
            "WidgetBackground" => _viewModel.TextColorTargetWidgetBackground,
            "GlassSurface" => _viewModel.TextColorTargetGlassSurface,
            "CardTint" => _viewModel.TextColorTargetCardTint,
            "CardBorder" => _viewModel.TextColorTargetCardBorder,
            "Track" => _viewModel.TextColorTargetTrack,
            "Panel" => _viewModel.TextColorTargetPanel,
            "SettingsText" => _viewModel.TextColorTargetSettingsText,
            _ => _viewModel.TextColorTargetPrimaryText
        };
    }

    private void SetColorElementButtonState(WpfControls.Button button, string elementKey)
    {
        var selected = string.Equals(_selectedColorElementKey, elementKey, StringComparison.OrdinalIgnoreCase);
        button.Background = new SolidColorBrush(selected
            ? System.Windows.Media.Color.FromRgb(221, 241, 255)
            : System.Windows.Media.Color.FromRgb(236, 247, 251));
        button.BorderBrush = new SolidColorBrush(selected
            ? System.Windows.Media.Color.FromRgb(68, 133, 208)
            : System.Windows.Media.Color.FromRgb(136, 189, 214));
    }

    private void SetSwatch(WpfControls.Border swatch, string resourceKey)
    {
        if (TryGetResourceColor(resourceKey, out var color))
        {
            swatch.Background = new SolidColorBrush(color);
        }
    }

    private bool TryGetElementCurrentColor(string elementKey, out System.Windows.Media.Color color)
    {
        var resourceKey = elementKey switch
        {
            "PrimaryText" => "PrimaryTextBrush",
            "SecondaryText" => "SecondaryTextBrush",
            "BatteryText" => "BatteryTextBrush",
            "WidgetBackground" => "WidgetBackgroundBrush",
            "GlassSurface" => "GlassSurfaceBrush",
            "CardTint" => "CardTintBrush",
            "CardBorder" => "CardBorderBrush",
            "Track" => "TrackBrush",
            "Panel" => "ActionButtonBackBrush",
            "SettingsText" => SettingsTextBrushResourceKey,
            _ => "PrimaryTextBrush"
        };

        return TryGetResourceColor(resourceKey, out color);
    }

    private bool TryGetResourceColor(string resourceKey, out System.Windows.Media.Color color)
    {
        if (TryFindResource(resourceKey) is SolidColorBrush brush)
        {
            color = brush.Color;
            return true;
        }

        color = default;
        return false;
    }

    private void SetResourceColor(string key, System.Windows.Media.Color color)
    {
        if (Resources[key] is SolidColorBrush existingBrush && !existingBrush.IsFrozen)
        {
            existingBrush.Color = color;
            return;
        }

        Resources[key] = new SolidColorBrush(color);
    }

    private static bool TryPickColor(string initialHex, out string selectedHex)
    {
        selectedHex = string.Empty;
        var initial = TryParseWpfColor(initialHex, out var parsed)
            ? parsed
            : System.Windows.Media.Colors.White;

        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(initial.R, initial.G, initial.B)
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return false;
        }

        selectedHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        return true;
    }

    private static bool TryParseWpfColor(string? value, out System.Windows.Media.Color color)
    {
        color = default;
        var normalized = WidgetSettings.NormalizeOptionalHexColor(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        try
        {
            color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(normalized);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ToRgbHex(System.Windows.Media.Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static System.Windows.Media.Color WithAlpha(System.Windows.Media.Color color, byte alpha)
    {
        return System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static System.Windows.Media.Color BlendColors(
        System.Windows.Media.Color first,
        System.Windows.Media.Color second,
        double secondAmount)
    {
        var amount = Math.Clamp(secondAmount, 0d, 1d);
        var firstAmount = 1d - amount;
        return System.Windows.Media.Color.FromArgb(
            (byte)Math.Clamp(Math.Round((first.A * firstAmount) + (second.A * amount)), 0d, 255d),
            (byte)Math.Clamp(Math.Round((first.R * firstAmount) + (second.R * amount)), 0d, 255d),
            (byte)Math.Clamp(Math.Round((first.G * firstAmount) + (second.G * amount)), 0d, 255d),
            (byte)Math.Clamp(Math.Round((first.B * firstAmount) + (second.B * amount)), 0d, 255d));
    }

    private static System.Windows.Media.Color GetPaletteColor(double x, double y, double width, double height)
    {
        var hue = Math.Clamp(x / Math.Max(1d, width), 0d, 1d) * 360d;
        var vertical = Math.Clamp(y / Math.Max(1d, height), 0d, 1d);
        var darkFade = Math.Clamp((vertical - 0.82d) / 0.18d, 0d, 1d);
        var saturation = 0.82d * (1d - darkFade);
        var lightness = (0.92d - (Math.Min(vertical, 0.82d) * 0.62d)) * (1d - darkFade);
        return ColorFromHsl(hue, saturation, lightness);
    }

    private static System.Windows.Media.Color ColorFromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1d - Math.Abs((2d * lightness) - 1d)) * saturation;
        var huePrime = hue / 60d;
        var x = chroma * (1d - Math.Abs((huePrime % 2d) - 1d));
        var match = lightness - (chroma / 2d);

        var (red, green, blue) = huePrime switch
        {
            >= 0d and < 1d => (chroma, x, 0d),
            >= 1d and < 2d => (x, chroma, 0d),
            >= 2d and < 3d => (0d, chroma, x),
            >= 3d and < 4d => (0d, x, chroma),
            >= 4d and < 5d => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return System.Windows.Media.Color.FromRgb(
            (byte)Math.Clamp(Math.Round((red + match) * 255d), 0d, 255d),
            (byte)Math.Clamp(Math.Round((green + match) * 255d), 0d, 255d),
            (byte)Math.Clamp(Math.Round((blue + match) * 255d), 0d, 255d));
    }

    private static double GetRelativeSimpleLuminance(System.Windows.Media.Color color)
    {
        return ((0.2126d * color.R) + (0.7152d * color.G) + (0.0722d * color.B)) / 255d;
    }

    private static System.Windows.Media.Color EnsureMinimumLuminance(System.Windows.Media.Color color, double minimumLuminance)
    {
        var target = Math.Clamp(minimumLuminance, 0d, 1d);
        var luminance = ((0.2126d * color.R) + (0.7152d * color.G) + (0.0722d * color.B)) / 255d;
        if (luminance >= target)
        {
            return color;
        }

        var blend = (target - luminance) / Math.Max(0.0001d, 1d - luminance);
        var red = (byte)Math.Clamp(Math.Round(color.R + ((255d - color.R) * blend)), 0d, 255d);
        var green = (byte)Math.Clamp(Math.Round(color.G + ((255d - color.G) * blend)), 0d, 255d);
        var blue = (byte)Math.Clamp(Math.Round(color.B + ((255d - color.B) * blend)), 0d, 255d);
        return System.Windows.Media.Color.FromArgb(color.A, red, green, blue);
    }

    private static System.Windows.Media.Color EnhanceThemeColor(
        System.Windows.Media.Color color,
        double saturationBoost,
        double alphaBoost,
        double contrastBoost)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;

        var gray = (0.299d * red) + (0.587d * green) + (0.114d * blue);
        red = gray + ((red - gray) * saturationBoost);
        green = gray + ((green - gray) * saturationBoost);
        blue = gray + ((blue - gray) * saturationBoost);

        red = ((red - 0.5d) * contrastBoost) + 0.5d;
        green = ((green - 0.5d) * contrastBoost) + 0.5d;
        blue = ((blue - 0.5d) * contrastBoost) + 0.5d;

        var enhancedAlpha = (byte)Math.Clamp(Math.Round(color.A * alphaBoost), 0d, 255d);
        var enhancedRed = (byte)Math.Clamp(Math.Round(red * 255d), 0d, 255d);
        var enhancedGreen = (byte)Math.Clamp(Math.Round(green * 255d), 0d, 255d);
        var enhancedBlue = (byte)Math.Clamp(Math.Round(blue * 255d), 0d, 255d);

        return System.Windows.Media.Color.FromArgb(enhancedAlpha, enhancedRed, enhancedGreen, enhancedBlue);
    }

    private void ApplyVisualModeState()
    {
        if (_viewModel.IsLiteVisualMode)
        {
            StopGlassWave();
            return;
        }

        StartGlassWave();
    }

    private void StartGlassWave()
    {
        if (_glassWaveStoryboard is null)
        {
            _glassWaveStoryboard = TryFindResource(GlassWaveStoryboardKey) as Storyboard;
        }

        _glassWaveStoryboard?.Begin(this, true);
    }

    private void StopGlassWave()
    {
        _glassWaveStoryboard?.Stop(this);
    }

    private void GlassCard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGlassCardClip();
        UpdateDwmBlurBehindRegion();
        UpdateSettingsPopupLayout();
    }

    private void UpdateGlassCardClip()
    {
        if (GlassCard is null || GlassCardContent is null)
        {
            return;
        }

        var width = GlassCardContent.ActualWidth;
        var height = GlassCardContent.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            GlassCardContent.Clip = null;
            return;
        }

        var borderInset = Math.Max(GlassCard.BorderThickness.Left, GlassCard.BorderThickness.Top);
        var radius = Math.Max(0d, GlassCard.CornerRadius.TopLeft - borderInset);
        GlassCardContent.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
    }

    private TrayIconService BuildTrayIconService()
    {
        return new TrayIconService(
            _appIcon,
            AppDisplayName,
            () => Dispatcher.Invoke(ShowWidgetFromTray),
            () => Dispatcher.Invoke(() => _ = RefreshFromUserCommandAsync()),
            () => Dispatcher.Invoke(ResetWidgetPositionToCurrentMonitor),
            () => Dispatcher.Invoke(ToggleAutostartFromTray),
            () => Dispatcher.Invoke(ToggleStartMinimizedToTrayFromTray),
            () => Dispatcher.Invoke(ExitApplication));
    }

    private static DrawingIcon LoadAppIcon()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                var extracted = DrawingIcon.ExtractAssociatedIcon(processPath);
                if (extracted is not null)
                {
                    return (DrawingIcon)extracted.Clone();
                }
            }
        }
        catch
        {
            // Ignore and fallback.
        }

        try
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(candidate))
            {
                return new DrawingIcon(candidate);
            }
        }
        catch
        {
            // Ignore and fallback.
        }

        return (DrawingIcon)SystemIcons.Information.Clone();
    }

    public void ShowWidgetFromTray()
    {
        WindowState = WindowState.Normal;
        Opacity = 1d;

        if (!IsVisible)
        {
            Show();
        }

        EnsureWidgetVisibleOnConnectedMonitor();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void ResetWidgetPositionToCurrentMonitor()
    {
        WindowState = WindowState.Normal;
        Opacity = 1d;

        if (!IsVisible)
        {
            Show();
        }

        var area = GetWorkingAreaFromCurrentCursor();
        CenterWindowInArea(area);
        SaveWindowBounds();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void ToggleAutostartFromTray()
    {
        _viewModel.SetAutostart(!_viewModel.AutostartEnabled);
        RefreshTrayMenuTexts();
    }

    private void ToggleStartMinimizedToTrayFromTray()
    {
        _viewModel.SetStartMinimizedToTray(!_viewModel.StartMinimizedToTrayEnabled);
        RefreshTrayMenuTexts();
    }

    private void EnsureWidgetVisibleOnConnectedMonitor()
    {
        var workingAreas = GetWorkingAreas();
        if (workingAreas.Count == 0)
        {
            return;
        }

        var currentBounds = new WindowBounds
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };

        if (HasMeaningfulVisibleArea(currentBounds, workingAreas))
        {
            return;
        }

        CenterWindowInArea(GetWorkingAreaFromCurrentCursor());
        SaveWindowBounds();
    }

    private void CenterWindowInArea(WindowBounds area)
    {
        var width = Math.Clamp(ActualWidth > 0d ? ActualWidth : Width, MinWidth, Math.Max(MinWidth, area.Width - StartupSafetyMargin * 2d));
        var height = Math.Clamp(ActualHeight > 0d ? ActualHeight : Height, MinHeight, Math.Max(MinHeight, area.Height - StartupSafetyMargin * 2d));

        Width = width;
        Height = height;
        Left = area.Left + Math.Max(StartupSafetyMargin, (area.Width - width) / 2d);
        Top = area.Top + Math.Max(StartupSafetyMargin, (area.Height - height) / 2d);
    }

    private static WindowBounds GetWorkingAreaFromCurrentCursor()
    {
        return GetWorkingAreaFromScreen(Forms.Screen.FromPoint(Forms.Cursor.Position), null);
    }

    private static WindowBounds GetWorkingAreaFromScreen(Forms.Screen screen, Visual? dpiSource)
    {
        var workingArea = screen.WorkingArea;
        if (dpiSource is not null)
        {
            var presentationSource = PresentationSource.FromVisual(dpiSource);
            var transform = presentationSource?.CompositionTarget?.TransformFromDevice;
            if (transform is not null)
            {
                var topLeft = transform.Value.Transform(new System.Windows.Point(workingArea.Left, workingArea.Top));
                var bottomRight = transform.Value.Transform(new System.Windows.Point(workingArea.Right, workingArea.Bottom));
                return new WindowBounds
                {
                    Left = topLeft.X,
                    Top = topLeft.Y,
                    Width = Math.Max(1d, bottomRight.X - topLeft.X),
                    Height = Math.Max(1d, bottomRight.Y - topLeft.Y)
                };
            }
        }

        return new WindowBounds
        {
            Left = workingArea.Left,
            Top = workingArea.Top,
            Width = workingArea.Width,
            Height = workingArea.Height
        };
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _forceClose = true;
        HideAndDisposeTrayIcon();
        Close();
        System.Windows.Application.Current.Shutdown();

        // Safety net: if the app somehow fails to exit cleanly, force process termination.
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500).ConfigureAwait(false);
            try
            {
                Environment.Exit(0);
            }
            catch
            {
                // Ignore forced-exit failures.
            }
        });
    }

    private void HideAndDisposeTrayIcon()
    {
        _trayIconService.Dispose();
    }

    private void ApplyWindowBounds()
    {
        var workingAreas = GetWorkingAreas();
        var primaryArea = workingAreas.Count > 0
            ? workingAreas[0]
            : new WindowBounds
            {
                Left = SystemParameters.WorkArea.Left,
                Top = SystemParameters.WorkArea.Top,
                Width = SystemParameters.WorkArea.Width,
                Height = SystemParameters.WorkArea.Height
            };

        var bounds = _viewModel.Settings.WindowBounds;
        if (bounds is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            ApplySafeStartupBounds(primaryArea);
            return;
        }

        var normalizedBounds = WindowBoundsNormalizer.Normalize(bounds, workingAreas, out var wasAdjusted);
        var clampedBounds = ClampToAccessibleBounds(normalizedBounds, workingAreas);

        Left = clampedBounds.Left;
        Top = clampedBounds.Top;
        Width = clampedBounds.Width;
        Height = clampedBounds.Height;

        if (wasAdjusted || !AreClose(normalizedBounds, clampedBounds))
        {
            _viewModel.SetWindowBounds(new Rect(
                clampedBounds.Left,
                clampedBounds.Top,
                clampedBounds.Width,
                clampedBounds.Height));
        }
    }

    private void ApplySafeStartupBounds(WindowBounds area)
    {
        var maxWidth = Math.Min(area.Width * StartupMaxWorkAreaRatio, StartupMaxAbsoluteWidth);
        var maxHeight = Math.Min(area.Height * StartupMaxWorkAreaRatio, StartupMaxAbsoluteHeight);
        var width = Math.Clamp(StartupDefaultWidth, MinWidth, Math.Max(MinWidth, maxWidth));
        var height = Math.Clamp(StartupDefaultHeight, MinHeight, Math.Max(MinHeight, maxHeight));

        Width = width;
        Height = height;

        var left = area.Left + Math.Max(StartupSafetyMargin, (area.Width - width) / 2d);
        var top = area.Top + Math.Max(StartupSafetyMargin, (area.Height - height) / 2d);
        Left = left;
        Top = top;
    }

    private static WindowBounds ClampToAccessibleBounds(WindowBounds source, IReadOnlyList<WindowBounds> workingAreas)
    {
        if (workingAreas.Count == 0)
        {
            return source;
        }

        var area = SelectBestArea(source, workingAreas);
        var maxWidth = Math.Max(360d, Math.Min(area.Width * StartupMaxWorkAreaRatio, StartupMaxAbsoluteWidth));
        var maxHeight = Math.Max(250d, Math.Min(area.Height * StartupMaxWorkAreaRatio, StartupMaxAbsoluteHeight));

        var width = Math.Clamp(source.Width, 360d, maxWidth);
        var height = Math.Clamp(source.Height, 250d, maxHeight);

        var minLeft = area.Left + StartupSafetyMargin;
        var minTop = area.Top + StartupSafetyMargin;
        var maxLeft = area.Left + area.Width - width - StartupSafetyMargin;
        var maxTop = area.Top + area.Height - height - StartupSafetyMargin;

        var left = maxLeft < minLeft ? area.Left : Math.Clamp(source.Left, minLeft, maxLeft);
        var top = maxTop < minTop ? area.Top : Math.Clamp(source.Top, minTop, maxTop);

        return new WindowBounds
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height
        };
    }

    private static WindowBounds SelectBestArea(WindowBounds source, IReadOnlyList<WindowBounds> workingAreas)
    {
        var bestArea = workingAreas[0];
        var bestOverlap = -1d;

        foreach (var area in workingAreas)
        {
            var overlap = CalculateOverlapArea(source, area);
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestArea = area;
            }
        }

        return bestArea;
    }

    private static double CalculateOverlapArea(WindowBounds first, WindowBounds second)
    {
        var left = Math.Max(first.Left, second.Left);
        var top = Math.Max(first.Top, second.Top);
        var right = Math.Min(first.Left + first.Width, second.Left + second.Width);
        var bottom = Math.Min(first.Top + first.Height, second.Top + second.Height);
        var width = right - left;
        var height = bottom - top;

        if (width <= 0 || height <= 0)
        {
            return 0d;
        }

        return width * height;
    }

    private static bool HasMeaningfulVisibleArea(WindowBounds source, IReadOnlyList<WindowBounds> workingAreas)
    {
        if (workingAreas.Count == 0)
        {
            return false;
        }

        var overlapArea = CalculateOverlapArea(source, SelectBestArea(source, workingAreas));
        if (overlapArea <= 0d)
        {
            return false;
        }

        var windowArea = Math.Max(1d, source.Width * source.Height);
        var requiredVisibleArea = Math.Min(windowArea * 0.25d, 120d * 120d);
        return overlapArea >= requiredVisibleArea;
    }

    private static bool AreClose(WindowBounds left, WindowBounds right)
    {
        const double epsilon = 0.01d;
        return Math.Abs(left.Left - right.Left) < epsilon &&
               Math.Abs(left.Top - right.Top) < epsilon &&
               Math.Abs(left.Width - right.Width) < epsilon &&
               Math.Abs(left.Height - right.Height) < epsilon;
    }

    private void SaveBoundsThrottled()
    {
        if (!_initialBoundsApplied)
        {
            return;
        }

        if (DateTime.UtcNow - _lastBoundsSaveAt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        SaveWindowBounds();
        _lastBoundsSaveAt = DateTime.UtcNow;
    }

    private void SaveWindowBounds()
    {
        var bounds = new Rect(Left, Top, Width, Height);
        _viewModel.SetWindowBounds(bounds);
    }

    private static IReadOnlyList<WindowBounds> GetWorkingAreas()
    {
        return Forms.Screen.AllScreens
            .OrderByDescending(screen => screen.Primary)
            .Select(screen => new WindowBounds
            {
                Left = screen.WorkingArea.Left,
                Top = screen.WorkingArea.Top,
                Width = screen.WorkingArea.Width,
                Height = screen.WorkingArea.Height
            })
            .ToList();
    }

    private void ApplyToolWindowStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(handle, GwlExStyle);
        var updated = (nint)((style.ToInt64() | WsExToolWindow) & ~WsExAppWindow);
        if (updated != style)
        {
            SetWindowLongPtr(handle, GwlExStyle, updated);
        }
    }

    private void EnableDwmBlurBehindWindow()
    {
        UpdateDwmBlurBehindRegion();
    }

    private void UpdateDwmBlurBehindRegion()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || GlassCard is null)
        {
            return;
        }

        var width = GlassCard.ActualWidth;
        var height = GlassCard.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Rect bounds;
        try
        {
            var transform = GlassCard.TransformToAncestor(this);
            bounds = transform.TransformBounds(new Rect(0, 0, width, height));
        }
        catch
        {
            return;
        }

        var left = (int)Math.Floor(bounds.Left);
        var top = (int)Math.Floor(bounds.Top);
        var right = (int)Math.Ceiling(bounds.Right);
        var bottom = (int)Math.Ceiling(bounds.Bottom);
        if (right <= left || bottom <= top)
        {
            return;
        }

        var borderInset = Math.Max(GlassCard.BorderThickness.Left, GlassCard.BorderThickness.Top);
        var cornerRadius = Math.Max(0d, GlassCard.CornerRadius.TopLeft - borderInset);
        var ellipseDiameter = Math.Max(2, (int)Math.Round(cornerRadius * 2d));

        var region = IntPtr.Zero;
        try
        {
            region = CreateRoundRectRgn(left, top, right, bottom, ellipseDiameter, ellipseDiameter);
            if (region == IntPtr.Zero)
            {
                return;
            }

            var blur = new DwmBlurBehind
            {
                DwFlags = DwmBbEnable | DwmBbBlurRegion,
                FEnable = true,
                HRgnBlur = region,
                FTransitionOnMaximized = false
            };

            _ = DwmEnableBlurBehindWindow(handle, ref blur);
        }
        catch
        {
            // 요청안 고정: DWM만 시도하고 실패 시 추가 폴백 없이 유지.
        }
        finally
        {
            if (region != IntPtr.Zero)
            {
                _ = DeleteObject(region);
            }
        }
    }

    private void UpdateCompactMode()
    {
        if (DeviceListBox is null)
        {
            return;
        }

        var isCompactNow = ActualHeight <= CompactHeightThreshold;
        if (isCompactNow == _isCompactMode)
        {
            return;
        }

        _isCompactMode = isCompactNow;
        WpfControls.ScrollViewer.SetVerticalScrollBarVisibility(
            DeviceListBox,
            WpfControls.ScrollBarVisibility.Hidden);
    }

    private void UpdateVersionMenuHeader()
    {
        if (VersionTextBlock is null)
        {
            return;
        }

        VersionTextBlock.Text = $"{_viewModel.TextVersionPrefix} {GetDisplayVersion()}";
    }

    private void RefreshTrayMenuTexts()
    {
        _trayIconService.RefreshTexts(new TrayIconTexts(
            _viewModel.TextTrayOpenWidget,
            _viewModel.TextTrayRefreshNow,
            _viewModel.TextTrayResetPosition,
            _viewModel.TextAutostart,
            _viewModel.AutostartEnabled,
            _viewModel.TextStartMinimizedToTray,
            _viewModel.StartMinimizedToTrayEnabled,
            _viewModel.TextTrayExit));
    }

    private static string GetDisplayVersion()
    {
        return AppVersionInfo.DisplayVersion;
    }

    private void GuideButtonMonitor_GuideButtonPressed(object? sender, GuideButtonPressedEventArgs e)
    {
        try
        {
            var shouldDeferToastUntilDisplayWake = ShouldDeferBatteryGuideToastUntilDisplayWake();
            if (e.DeviceKind != GuideButtonDeviceKind.SteamController || shouldDeferToastUntilDisplayWake)
            {
                MarkIntentionalGamepadInputAndExitQuietMode("guide_button_press");
                TryWakeDisplayAfterVerifiedInput("guide_button_press");
            }

            if (_isBatteryGuideTriggerCaptureActive)
            {
                return;
            }

            if (ShouldSuppressSteamGuideToast(e.DeviceKind, e.Address, e.DisplayName, "guide_button_press"))
            {
                return;
            }

            if (_viewModel.HasCustomBatteryGuideTriggerForDevice(e.DeviceKind))
            {
                return;
            }

            if (ShouldSuppressSecondarySteamGuideButtonPath(sender, e))
            {
                return;
            }

            if (ShouldSuppressGuideButtonPressAfterInputReportFallback(e))
            {
                return;
            }

            if (ShouldSuppressDuplicateGuideButtonToast(e))
            {
                return;
            }

            if (shouldDeferToastUntilDisplayWake)
            {
                QueueBatteryGuideToastAfterDisplayWake(e);
                return;
            }

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.DeviceKind == GuideButtonDeviceKind.SteamController)
                {
                    ShowBatteryGuide(e);
                    return;
                }

                _ = ShowBatteryGuideAfterGamepadActivityRefreshAsync(e);
            }));
        }
        catch
        {
            // Ignore shutdown races while the background monitor is stopping.
        }
    }

    private void GuideButtonMonitor_InputReportReceived(object? sender, GuideButtonInputReportEventArgs e)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(() => GuideButtonMonitor_InputReportReceived(sender, e)));
                return;
            }

            if (TryHandleDefaultGuideButtonToastFromInputReport(e))
            {
                return;
            }

            if (ShouldTreatInputReportAsIntentionalGamepadActivity(e))
            {
                var isDisplayOffWake = _powerIdleMode == PowerIdleRuntimeMode.DisplayOffWakeOnly ||
                    _powerIdleMode == PowerIdleRuntimeMode.WakeRecovery;
                MarkIntentionalGamepadInputAndExitQuietMode("hid_button_input");
                TryWakeDisplayAfterVerifiedInput("hid_button_input");
                if (!isDisplayOffWake)
                {
                    RequestRefreshAfterGamepadActivity();
                }

                WriteGamepadActivityDiagnosticIfNeeded(
                    "hid_button_input",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    "Intentional HID controller button input refreshed Bloss local idle tracking.");
            }

            if (_powerIdleMode != PowerIdleRuntimeMode.DisplayOffWakeOnly &&
                _powerIdleMode != PowerIdleRuntimeMode.WakeRecovery)
            {
                HandleBatteryGuideInputReport(e);
            }
        }
        catch
        {
            // Input reports are best-effort; battery display must keep running.
        }
    }

    private void GuideButtonHidMonitor_InputActivityReceived(object? sender, GuideButtonActivityEventArgs e)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(() => GuideButtonHidMonitor_InputActivityReceived(sender, e)));
                return;
            }

            var classification = GamepadInputClassifier.ClassifyHidActivity(e);
            if (classification.CountsAsUserActivity)
            {
                MarkIntentionalGamepadInputAndExitQuietMode("hid_button_activity");
                if (_powerIdleMode == PowerIdleRuntimeMode.Active)
                {
                    RequestRefreshAfterGamepadActivity();
                }
            }
            else
            {
                _viewModel.MarkGamepadTelemetryActivity();
                if (_powerIdleMode == PowerIdleRuntimeMode.Active)
                {
                    RequestTelemetryRefreshIfAllowed();
                }
            }

            if (classification.IsWakeEligible &&
                (_powerIdleMode == PowerIdleRuntimeMode.DisplayOffWakeOnly ||
                 _powerIdleMode == PowerIdleRuntimeMode.WakeRecovery))
            {
                TryWakeDisplayAfterVerifiedInput("hid_button_activity");
            }

            WriteGamepadActivityDiagnosticIfNeeded(
                classification.EventName,
                e.DeviceKind.ToString(),
                e.Address,
                e.DisplayName,
                classification.DiagnosticMessage);
        }
        catch
        {
            // Activity refresh is best-effort; battery display must keep running.
        }
    }

    private void GuideButtonMonitor_InputActivityReceived(object? sender, GuideButtonActivityEventArgs e)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(() => GuideButtonMonitor_InputActivityReceived(sender, e)));
                return;
            }

            var classification = GamepadInputClassifier.ClassifySteamRawInputActivity(e);
            if (classification.CountsAsUserActivity)
            {
                MarkIntentionalGamepadInputAndExitQuietMode("steam_raw_input_activity");
                if (_powerIdleMode == PowerIdleRuntimeMode.Active)
                {
                    RequestRefreshAfterGamepadActivity();
                }
            }
            else
            {
                _viewModel.MarkGamepadTelemetryActivity();
                if (_powerIdleMode == PowerIdleRuntimeMode.Active)
                {
                    RequestTelemetryRefreshIfAllowed();
                }
            }

            if (classification.IsWakeEligible &&
                (_powerIdleMode == PowerIdleRuntimeMode.DisplayOffWakeOnly ||
                 _powerIdleMode == PowerIdleRuntimeMode.WakeRecovery))
            {
                TryWakeDisplayAfterVerifiedInput("steam_raw_input_activity");
            }

            WriteGamepadActivityDiagnosticIfNeeded(
                classification.EventName,
                e.DeviceKind.ToString(),
                e.Address,
                e.DisplayName,
                classification.DiagnosticMessage);
        }
        catch
        {
            // Activity refresh is best-effort; battery display must keep running.
        }
    }

    private void XInputActivityMonitor_InputActivityReceived(object? sender, GamepadWakeInputEventArgs e)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(() => XInputActivityMonitor_InputActivityReceived(sender, e)));
                return;
            }

            var isDisplayOffWake = _powerIdleMode == PowerIdleRuntimeMode.DisplayOffWakeOnly ||
                _powerIdleMode == PowerIdleRuntimeMode.WakeRecovery;
            var classification = GamepadInputClassifier.ClassifyXInputActivity(e);
            var eventName = classification.EventName;
            if (classification.CountsAsUserActivity)
            {
                MarkIntentionalGamepadInputAndExitQuietMode(eventName);
            }
            else
            {
                _viewModel.MarkGamepadTelemetryActivity();
            }

            if (classification.IsWakeEligible)
            {
                TryWakeDisplayAfterVerifiedInput(eventName);
            }

            if (classification.CountsAsUserActivity)
            {
                if (!isDisplayOffWake)
                {
                    RequestRefreshAfterGamepadActivity();
                }
            }
            else if (_powerIdleMode == PowerIdleRuntimeMode.Active)
            {
                RequestTelemetryRefreshIfAllowed();
            }

            WriteGamepadActivityDiagnosticIfNeeded(
                eventName,
                "XInput",
                string.Empty,
                "XInput Gamepad",
                classification.DiagnosticMessage);
        }
        catch
        {
            // XInput activity is best-effort; battery display must keep running.
        }
    }

    private void MarkIntentionalGamepadInputAndExitQuietMode(string reason)
    {
        _viewModel.MarkIntentionalGamepadActivity();
        ArmNormalGamepadMonitoring(reason, requireActiveMode: false);

        if (_displayPowerCoordinator.CurrentState is DisplayPowerState.Off or DisplayPowerState.Dimmed ||
            _powerIdleMode == PowerIdleRuntimeMode.DisplayOffWakeOnly)
        {
            ArmWakeRecoveryBypassAfterVerifiedInput(reason);
            return;
        }

        if (_powerIdleMode != PowerIdleRuntimeMode.WakeRecovery)
        {
            return;
        }

        _wakeRecoveryUntilUtc = DateTimeOffset.MinValue;
        _verifiedInputWakeRecoveryBypassUntilUtc = DateTimeOffset.MinValue;
        ApplyPowerIdleMonitorState(PowerIdleRuntimeMode.Active, shouldPause: false);
        GuideButtonEventLog.Write(
            "intentional_gamepad_input",
            "PowerIdle",
            string.Empty,
            AppDisplayName,
            $"Intentional gamepad input restored active monitoring. reason={reason}.");
    }

    private void SteamRawInputMonitor_GlobalHumanInputReceived(object? sender, GlobalHumanInputEventArgs e)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(() => SteamRawInputMonitor_GlobalHumanInputReceived(sender, e)));
                return;
            }

            if (e.CountsAsUserActivity)
            {
                _viewModel.MarkUserActivity();
                ArmNormalGamepadMonitoring(e.Source, requireActiveMode: false);
                if (_powerIdleMode != PowerIdleRuntimeMode.DisplayOffWakeOnly &&
                    _powerIdleMode != PowerIdleRuntimeMode.WakeRecovery)
                {
                    UpdatePowerIdleGuideMonitoring();
                }
            }

            if (e.IsWakeEligible)
            {
                TryWakeDisplayAfterVerifiedInput(e.Source);
            }
        }
        catch
        {
            // Global input tracking is best-effort; battery display must keep running.
        }
    }

    private void RequestRefreshAfterGamepadActivity()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastGamepadActivityRefreshRequestedAtUtc < GamepadActivityRefreshCooldown)
        {
            RequestTelemetryRefreshIfAllowed();
            return;
        }

        UpdatePowerIdleGuideMonitoring();
        _ = RefreshAfterGamepadActivityAsync(force: false);
    }

    private void RequestTelemetryRefreshIfAllowed()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastTelemetryPowerIdleUpdateAtUtc < TelemetryPowerIdleUpdateCooldown)
        {
            return;
        }

        _lastTelemetryPowerIdleUpdateAtUtc = now;
        UpdatePowerIdleGuideMonitoring();
    }

    private async Task RefreshAfterGamepadActivityAsync(bool force)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastGamepadActivityRefreshRequestedAtUtc < GamepadActivityRefreshCooldown)
        {
            return;
        }

        _lastGamepadActivityRefreshRequestedAtUtc = now;
        try
        {
            await _viewModel.RefreshAsync().ConfigureAwait(true);
        }
        catch
        {
            // Gamepad activity refresh is best-effort; input handling must keep running.
        }
    }

    private async Task ShowBatteryGuideAfterGamepadActivityRefreshAsync(GuideButtonPressedEventArgs e)
    {
        try
        {
            if (FindGuideButtonDevice(e) is null)
            {
                await RefreshAfterGamepadActivityAsync(force: true).ConfigureAwait(true);
            }

            ShowBatteryGuide(e);
        }
        catch
        {
            // Guide-button toast is best-effort; input monitoring must keep running.
        }
    }

    private bool ShouldTreatInputReportAsIntentionalGamepadActivity(GuideButtonInputReportEventArgs e)
    {
        if (_isBatteryGuideTriggerCaptureActive)
        {
            return true;
        }

        var key = BuildBatteryGuideInputReportKey(e);
        if (!_lastPowerIdleInputReportByDevice.TryGetValue(key, out var previousReport))
        {
            _lastPowerIdleInputReportByDevice[key] = e.Report.ToArray();
            return false;
        }

        _lastPowerIdleInputReportByDevice[key] = e.Report.ToArray();
        return BatteryGuideTriggerParser.HasButtonDownEdgeForPowerIdleActivity(
            e.DeviceKind,
            previousReport,
            e.Report);
    }

    private bool TryHandleDefaultGuideButtonToastFromInputReport(GuideButtonInputReportEventArgs e)
    {
        if (!_isBatteryGuideTriggerCaptureActive &&
            !_viewModel.HasCustomBatteryGuideTriggerForDevice(e.DeviceKind) &&
            ShouldTreatInputReportAsDefaultGuideButtonPress(e))
        {
            GuideButtonEventLog.Write(
                "guide_input_report_fallback_disabled",
                e.DeviceKind.ToString(),
                e.Address,
                e.DisplayName,
                "Default guide input-report fallback was ignored; the regular guide-button press path must own the toast.");
        }

        return false;
    }

    private bool ShouldTreatInputReportAsDefaultGuideButtonPress(GuideButtonInputReportEventArgs e)
    {
        if (!GuideButtonReportParser.TryParseGuideButton(e.DeviceKind, e.Report, out var isPressed))
        {
            return false;
        }

        var reportKey = BuildBatteryGuideInputReportKey(e);
        var toastKey = BuildGuideButtonToastKey(new GuideButtonPressedEventArgs(
            e.Address,
            e.DisplayName,
            e.DeviceKind,
            GuideButtonGesture.ShortPress));
        if (!_lastDefaultGuideButtonPressedByInputReport.TryGetValue(reportKey, out var wasPressed))
        {
            _lastDefaultGuideButtonPressedByInputReport[reportKey] = isPressed;
            UpdateDefaultGuideNeutralReportCount(reportKey, isPressed);
            UpdateDefaultGuidePressedReportKey(toastKey, reportKey, isPressed);
            return false;
        }

        _lastDefaultGuideButtonPressedByInputReport[reportKey] = isPressed;
        var neutralReportCount = UpdateDefaultGuideNeutralReportCount(reportKey, isPressed);
        lock (_guideButtonToastSync)
        {
            if (!_defaultGuidePressedReportKeysByToastKey.TryGetValue(toastKey, out var pressedReportKeys))
            {
                pressedReportKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _defaultGuidePressedReportKeysByToastKey[toastKey] = pressedReportKeys;
            }

            if (!isPressed)
            {
                pressedReportKeys.Remove(reportKey);
                if (pressedReportKeys.Count == 0)
                {
                    _defaultGuidePressedReportKeysByToastKey.Remove(toastKey);
                }

                return false;
            }

            if (wasPressed)
            {
                pressedReportKeys.Add(reportKey);
                return false;
            }

            if (neutralReportCount < 2)
            {
                pressedReportKeys.Add(reportKey);
                return false;
            }

            var isFirstPressedReportForToast = pressedReportKeys.Count == 0;
            pressedReportKeys.Add(reportKey);
            return isFirstPressedReportForToast;
        }
    }

    private int UpdateDefaultGuideNeutralReportCount(string reportKey, bool isPressed)
    {
        if (isPressed)
        {
            return _defaultGuideNeutralReportCountByInputReport.TryGetValue(reportKey, out var count)
                ? count
                : 0;
        }

        var nextCount = _defaultGuideNeutralReportCountByInputReport.TryGetValue(reportKey, out var previousCount)
            ? Math.Min(2, previousCount + 1)
            : 1;
        _defaultGuideNeutralReportCountByInputReport[reportKey] = nextCount;
        return nextCount;
    }

    private void UpdateDefaultGuidePressedReportKey(string toastKey, string reportKey, bool isPressed)
    {
        lock (_guideButtonToastSync)
        {
            if (!_defaultGuidePressedReportKeysByToastKey.TryGetValue(toastKey, out var pressedReportKeys))
            {
                if (!isPressed)
                {
                    return;
                }

                pressedReportKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _defaultGuidePressedReportKeysByToastKey[toastKey] = pressedReportKeys;
            }

            if (isPressed)
            {
                pressedReportKeys.Add(reportKey);
                return;
            }

            pressedReportKeys.Remove(reportKey);
            if (pressedReportKeys.Count == 0)
            {
                _defaultGuidePressedReportKeysByToastKey.Remove(toastKey);
            }
        }
    }

    private void WriteGamepadActivityDiagnosticIfNeeded(
        string eventName,
        string deviceKind,
        string address,
        string displayName,
        string message)
    {
        if (!RuntimeDiagnostics.IsFileLoggingEnabled)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var key = $"{eventName}:{deviceKind}:{AddressNormalizer.NormalizeAddress(address)}:{displayName}";
        if (_lastGamepadActivityDiagnosticAtUtcByKind.TryGetValue(key, out var previous) &&
            now - previous < GamepadActivityDiagnosticInterval)
        {
            return;
        }

        _lastGamepadActivityDiagnosticAtUtcByKind[key] = now;
        GuideButtonEventLog.Write(
            eventName,
            deviceKind,
            address,
            displayName,
            message);
    }

    private void HandleBatteryGuideInputReport(GuideButtonInputReportEventArgs e)
    {
        if (e.Report.Length == 0)
        {
            return;
        }

        var key = BuildBatteryGuideInputReportKey(e);
        if (!_lastBatteryGuideTriggerReportByDevice.TryGetValue(key, out var previousReport))
        {
            if (_isBatteryGuideTriggerCaptureActive)
            {
                var neutralReport = BatteryGuideTriggerParser.CreateNeutralReportForCapture(e.DeviceKind, e.Report);
                if (TryUpdateBatteryGuideTriggerCapture(e, neutralReport, key))
                {
                    _lastBatteryGuideTriggerReportByDevice[key] = neutralReport;
                    return;
                }
            }

            _lastBatteryGuideTriggerReportByDevice[key] = e.Report.ToArray();
            return;
        }

        if (_isBatteryGuideTriggerCaptureActive && TryUpdateBatteryGuideTriggerCapture(e, previousReport, key))
        {
            return;
        }

        _lastBatteryGuideTriggerReportByDevice[key] = e.Report.ToArray();

        if (!_viewModel.TryGetBatteryGuideTriggerForDevice(e.DeviceKind, out var persistedTrigger) ||
            !BatteryGuideTriggerParser.TryParse(persistedTrigger, out var trigger))
        {
            RemoveCustomBatteryGuideTriggerReportKey(key);
            return;
        }

        var isPressed = BatteryGuideTriggerParser.IsMatch(trigger, e.DeviceKind, e.Report);
        var hasAnyTriggerBitPressed = isPressed ||
                                      BatteryGuideTriggerParser.HasAnyTriggerBitPressed(trigger, e.DeviceKind, e.Report);
        var bindingKey = BuildCustomBatteryGuideTriggerToastKey(trigger);
        if (!ShouldShowCustomBatteryGuideTriggerOnStateChange(
                bindingKey,
                key,
                isPressed,
                hasAnyTriggerBitPressed,
                _customBatteryGuideTriggerPressedReportKeysByBinding))
        {
            return;
        }

        if (ShouldSuppressCustomBatteryGuideTriggerToast(bindingKey, trigger))
        {
            return;
        }

        var args = new GuideButtonPressedEventArgs(
            e.Address,
            e.DisplayName,
            e.DeviceKind,
            GuideButtonGesture.ShortPress);
        if (ShouldSuppressDuplicateGuideButtonToast(args))
        {
            return;
        }

        GuideButtonEventLog.Write(
            "custom_trigger_toast_shown",
            trigger.DeviceKind.ToString(),
            string.Empty,
            trigger.DisplayName,
            "Custom notification-button trigger showed the battery guide.");
        ShowBatteryGuide(args);
    }

    private bool TryUpdateBatteryGuideTriggerCapture(
        GuideButtonInputReportEventArgs e,
        ReadOnlySpan<byte> previousReport,
        string key)
    {
        if (_batteryGuideTriggerCaptureKey is not null &&
            !string.Equals(_batteryGuideTriggerCaptureKey, key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!BatteryGuideTriggerParser.TryCapture(e.DeviceKind, previousReport, e.Report, out var captured))
        {
            return false;
        }

        if (!ShouldReplacePendingBatteryGuideTriggerCapture(_pendingBatteryGuideTriggerCapture, captured))
        {
            return true;
        }

        _batteryGuideTriggerCaptureKey = key;
        _pendingBatteryGuideTriggerCapture = captured;
        _batteryGuideTriggerCaptureWindow?.SetCandidate(captured);
        GuideButtonEventLog.Write(
            "custom_trigger_capture_candidate",
            captured.DeviceKind.ToString(),
            string.Empty,
            captured.DisplayName,
            $"Custom notification-button candidate captured. reportId=0x{captured.ReportId:X2}; buttons={captured.Bits.Count}; bits={FormatBatteryGuideTriggerBits(captured)}.");
        return true;
    }

    private static string FormatBatteryGuideTriggerBits(BatteryGuideTrigger trigger)
    {
        return string.Join(
            ",",
            trigger.Bits
                .OrderBy(bit => bit.Offset)
                .ThenBy(bit => bit.Mask)
                .Select(bit => $"{bit.Offset:X2}:{bit.Mask:X2}"));
    }

    internal static bool ShouldReplacePendingBatteryGuideTriggerCapture(
        BatteryGuideTrigger? pending,
        BatteryGuideTrigger captured)
    {
        if (pending is null)
        {
            return true;
        }

        if (pending.DeviceKind != captured.DeviceKind || pending.ReportId != captured.ReportId)
        {
            return true;
        }

        if (captured.Bits.Count > pending.Bits.Count)
        {
            return true;
        }

        if (captured.Bits.Count < pending.Bits.Count)
        {
            return false;
        }

        var pendingBits = pending.Bits
            .OrderBy(bit => bit.Offset)
            .ThenBy(bit => bit.Mask)
            .ToArray();
        var capturedBits = captured.Bits
            .OrderBy(bit => bit.Offset)
            .ThenBy(bit => bit.Mask)
            .ToArray();

        return !pendingBits.SequenceEqual(capturedBits);
    }

    private static string BuildBatteryGuideInputReportKey(GuideButtonInputReportEventArgs e)
    {
        var address = string.IsNullOrWhiteSpace(e.Address)
            ? "UNKNOWN"
            : e.Address;
        var reportId = e.Report.Length == 0 ? 0 : e.Report[0];
        return $"{e.DeviceKind}:{address}:RID_{reportId:X2}";
    }

    internal static bool ShouldShowCustomBatteryGuideTriggerOnStateChange(
        string bindingKey,
        string reportKey,
        bool isPressed,
        bool hasAnyTriggerBitPressed,
        IDictionary<string, HashSet<string>> pressedReportKeysByBinding)
    {
        if (string.IsNullOrWhiteSpace(bindingKey) || string.IsNullOrWhiteSpace(reportKey))
        {
            return false;
        }

        if (!pressedReportKeysByBinding.TryGetValue(bindingKey, out var pressedReportKeys))
        {
            if (!isPressed)
            {
                return false;
            }

            pressedReportKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pressedReportKeysByBinding[bindingKey] = pressedReportKeys;
        }

        var wasPressedAnywhere = pressedReportKeys.Count > 0;
        if (isPressed)
        {
            pressedReportKeys.Add(reportKey);
            return !wasPressedAnywhere;
        }

        if (hasAnyTriggerBitPressed)
        {
            return false;
        }

        pressedReportKeys.Remove(reportKey);
        if (pressedReportKeys.Count == 0)
        {
            pressedReportKeysByBinding.Remove(bindingKey);
        }

        return false;
    }

    private void RemoveCustomBatteryGuideTriggerReportKey(string reportKey)
    {
        foreach (var bindingKey in _customBatteryGuideTriggerPressedReportKeysByBinding.Keys.ToArray())
        {
            var reportKeys = _customBatteryGuideTriggerPressedReportKeysByBinding[bindingKey];
            reportKeys.Remove(reportKey);
            if (reportKeys.Count == 0)
            {
                _customBatteryGuideTriggerPressedReportKeysByBinding.Remove(bindingKey);
            }
        }
    }

    private void ClearCustomBatteryGuideTriggerPressState()
    {
        _customBatteryGuideTriggerPressedReportKeysByBinding.Clear();
    }

    private bool ShouldSuppressCustomBatteryGuideTriggerToast(string key, BatteryGuideTrigger trigger)
    {
        var now = DateTimeOffset.Now;
        lock (_guideButtonToastSync)
        {
            var lastSeen = _lastCustomBatteryGuideTriggerToastByBinding.TryGetValue(key, out var recordedLastSeen)
                ? recordedLastSeen
                : (DateTimeOffset?)null;

            var cooldown = GetCustomBatteryGuideTriggerToastCooldown(trigger.DeviceKind);
            if (ShouldSuppressCustomBatteryGuideTriggerToast(lastSeen, now, trigger.DeviceKind))
            {
                GuideButtonEventLog.Write(
                    "custom_trigger_duplicate_suppressed",
                    trigger.DeviceKind.ToString(),
                    string.Empty,
                    trigger.DisplayName,
                    $"Duplicate custom battery-guide trigger was ignored. cooldownMs={(int)cooldown.TotalMilliseconds}.");
                return true;
            }

            _lastCustomBatteryGuideTriggerToastByBinding[key] = now;
            return false;
        }
    }

    internal static bool ShouldSuppressCustomBatteryGuideTriggerToast(DateTimeOffset? lastSeen, DateTimeOffset now)
    {
        return lastSeen.HasValue && now - lastSeen.Value <= CustomBatteryGuideTriggerToastCooldown;
    }

    internal static bool ShouldSuppressCustomBatteryGuideTriggerToast(
        DateTimeOffset? lastSeen,
        DateTimeOffset now,
        GuideButtonDeviceKind deviceKind)
    {
        return lastSeen.HasValue && now - lastSeen.Value <= GetCustomBatteryGuideTriggerToastCooldown(deviceKind);
    }

    internal static TimeSpan GetCustomBatteryGuideTriggerToastCooldown(GuideButtonDeviceKind deviceKind)
    {
        return deviceKind == GuideButtonDeviceKind.SteamController
            ? SteamCustomBatteryGuideTriggerToastCooldown
            : CustomBatteryGuideTriggerToastCooldown;
    }

    private static string BuildCustomBatteryGuideTriggerToastKey(BatteryGuideTrigger trigger)
    {
        var bits = string.Join(
            ',',
            trigger.Bits
                .OrderBy(bit => bit.Offset)
                .ThenBy(bit => bit.Mask)
                .Select(bit => $"{bit.Offset:X2}:{bit.Mask:X2}"));
        return $"{trigger.DeviceKind}:{trigger.ReportId:X2}:{bits}";
    }

    private void SuppressSteamGuideToasts(TimeSpan duration, string reason)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var suppressUntil = now + duration;
        lock (_guideButtonToastSync)
        {
            if (suppressUntil > _steamGuideToastsSuppressedUntilUtc)
            {
                _steamGuideToastsSuppressedUntilUtc = suppressUntil;
            }
        }

        GuideButtonEventLog.Write(
            "steam_guide_toast_guard_armed",
            GuideButtonDeviceKind.SteamController.ToString(),
            string.Empty,
            "Steam Controller",
            $"Steam Controller guide toast guard armed. reason={reason}; durationMs={(int)duration.TotalMilliseconds}.");
    }

    private bool ShouldSuppressSteamGuideToast(
        GuideButtonDeviceKind deviceKind,
        string address,
        string displayName,
        string source)
    {
        if (deviceKind != GuideButtonDeviceKind.SteamController)
        {
            return false;
        }

        DateTimeOffset suppressUntil;
        var now = DateTimeOffset.UtcNow;
        lock (_guideButtonToastSync)
        {
            suppressUntil = _steamGuideToastsSuppressedUntilUtc;
        }

        var suppressForRefresh = _viewModel.IsRefreshRunning;
        if (!suppressForRefresh && now >= suppressUntil)
        {
            return false;
        }

        GuideButtonEventLog.Write(
            suppressForRefresh ? "steam_guide_toast_refresh_suppressed" : "steam_guide_toast_startup_suppressed",
            deviceKind.ToString(),
            address,
            string.IsNullOrWhiteSpace(displayName) ? "Steam Controller" : displayName,
            $"Steam Controller guide toast was ignored during connection/refresh settling. source={source}; remainingMs={(int)Math.Max(0, (suppressUntil - now).TotalMilliseconds)}.");
        return true;
    }

    private bool ShouldSuppressSecondarySteamGuideButtonPath(object? sender, GuideButtonPressedEventArgs e)
    {
        if (e.DeviceKind != GuideButtonDeviceKind.SteamController)
        {
            return false;
        }

        var key = BuildGuideButtonToastKey(e);
        var now = DateTimeOffset.Now;
        lock (_guideButtonToastSync)
        {
            if (sender is SteamControllerRawInputMonitorService)
            {
                CancelPendingSteamSecondaryGuideFallbackLocked(key);
                _lastSteamRawGuideButtonByDevice[key] = now;
                if (e.Gesture == GuideButtonGesture.LongPress)
                {
                    _steamSecondaryGuideFallbackBlockedUntilByDevice[key] = now + SteamSecondaryFallbackBurstWindow;
                    GuideButtonEventLog.Write(
                        "raw_long_press_secondary_blocked",
                        e.DeviceKind.ToString(),
                        e.Address,
                        e.DisplayName,
                        "RawInput classified this Steam-button sequence as a long hold, so secondary fallback is temporarily blocked.");
                    return true;
                }

                return false;
            }

            if (sender is not GuideButtonMonitorService)
            {
                return false;
            }

            if (e.Gesture != GuideButtonGesture.ShortPress)
            {
                GuideButtonEventLog.Write(
                    "secondary_press_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    "Secondary Steam HID monitor press-start event was ignored so long-press power-off does not show a toast.");
                return true;
            }

            if (_lastSteamRawGuideButtonByDevice.TryGetValue(key, out var lastRawPress) &&
                now - lastRawPress <= SteamRawInputPreferredWindow)
            {
                GuideButtonEventLog.Write(
                    "secondary_press_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    "Secondary Steam HID monitor event was ignored because RawInput already handled this Steam button press.");
                return true;
            }

            if (_steamSecondaryGuideFallbackBlockedUntilByDevice.TryGetValue(key, out var blockedUntil) &&
                now <= blockedUntil)
            {
                GuideButtonEventLog.Write(
                    "secondary_press_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    "Secondary Steam HID monitor event was ignored during the fixed Steam power-off guard window.");
                return true;
            }

            if (_steamSecondaryGuideFallbackBlockedUntilByDevice.ContainsKey(key))
            {
                _steamSecondaryGuideFallbackBlockedUntilByDevice.Remove(key);
            }

            if (!_steamRawInputMonitor.HasStableNeutralGuideBaseline(e.Address))
            {
                GuideButtonEventLog.Write(
                    "secondary_fallback_neutral_baseline_missing",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    "Secondary Steam HID monitor event was ignored because Raw HID has not seen a stable neutral guide-button baseline yet.");
                return true;
            }

            if (_pendingSteamSecondaryGuideFallbackByDevice.ContainsKey(key))
            {
                GuideButtonEventLog.Write(
                    "secondary_duplicate_pending_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    "Secondary Steam HID monitor duplicate was ignored while the first fallback event was still pending.");
                return true;
            }

            var cts = new CancellationTokenSource();
            _pendingSteamSecondaryGuideFallbackByDevice[key] = new PendingSteamSecondaryGuideFallback(e, cts);
            _ = CompleteSteamSecondaryGuideFallbackAsync(key, e, cts);
            GuideButtonEventLog.Write(
                "secondary_fallback_pending",
                e.DeviceKind.ToString(),
                e.Address,
                e.DisplayName,
                "Secondary Steam HID monitor event will be used if no repeated hold signal arrives.");
            return true;
        }
    }

    private async Task CompleteSteamSecondaryGuideFallbackAsync(
        string key,
        GuideButtonPressedEventArgs e,
        CancellationTokenSource cts)
    {
        var startedAt = DateTimeOffset.Now;
        var delay = SteamSecondaryFallbackDelay;
        var rawHidWaitLogged = false;
        var rawHidWasFreshForThisFallback = false;

        while (true)
        {
            try
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            var now = DateTimeOffset.Now;
            lock (_guideButtonToastSync)
            {
                if (!_pendingSteamSecondaryGuideFallbackByDevice.TryGetValue(key, out var pending) ||
                    !ReferenceEquals(pending.Cancellation, cts))
                {
                    return;
                }

                if (_lastSteamRawGuideButtonByDevice.TryGetValue(key, out var lastRawPress) &&
                    now - lastRawPress <= SteamRawInputPreferredWindow)
                {
                    _pendingSteamSecondaryGuideFallbackByDevice.Remove(key);
                    cts.Dispose();
                    return;
                }

                if (_steamSecondaryGuideFallbackBlockedUntilByDevice.TryGetValue(key, out var blockedUntil) &&
                    now <= blockedUntil)
                {
                    _pendingSteamSecondaryGuideFallbackByDevice.Remove(key);
                    cts.Dispose();
                    return;
                }

                if (_steamSecondaryGuideFallbackBlockedUntilByDevice.ContainsKey(key))
                {
                    _steamSecondaryGuideFallbackBlockedUntilByDevice.Remove(key);
                }
            }

            var rawHidActivity = _steamRawInputMonitor.GetGuideButtonActivity(e.Address);
            if (rawHidActivity.IsPressed)
            {
                if (!rawHidWasFreshForThisFallback &&
                    rawHidActivity.PressedDuration >= SteamSecondaryFallbackRawHidPreExistingHoldAge)
                {
                    _steamRawInputMonitor.ClearGuideButtonActivity(e.Address);
                    GuideButtonEventLog.Write(
                        "secondary_fallback_stale_raw_hid_ignored",
                        e.DeviceKind.ToString(),
                        e.Address,
                        e.DisplayName,
                        $"Secondary Steam HID monitor ignored a pre-existing Raw HID pressed-state so a fresh short tap can still show. rawHeldMs={(int)rawHidActivity.PressedDuration.TotalMilliseconds}; rawLastStateAgeMs={(int)rawHidActivity.LastStateAge.TotalMilliseconds}.");
                }
                else
                {
                    var rawHidLooksActiveHold =
                        rawHidActivity.LastStateAge <= SteamSecondaryFallbackRawHidActivePressFreshWindow;

                    rawHidWasFreshForThisFallback =
                        rawHidWasFreshForThisFallback ||
                        rawHidActivity.PressedDuration < SteamSecondaryFallbackRawHidHoldSuppressDuration ||
                        rawHidLooksActiveHold;

                    if (rawHidActivity.PressedDuration >= SteamSecondaryFallbackRawHidHoldSuppressDuration &&
                        !rawHidLooksActiveHold)
                    {
                        _steamRawInputMonitor.ClearGuideButtonActivity(e.Address);
                        GuideButtonEventLog.Write(
                            "secondary_fallback_stale_raw_hid_ignored",
                            e.DeviceKind.ToString(),
                            e.Address,
                            e.DisplayName,
                            $"Secondary Steam HID monitor ignored an old Raw HID pressed-state so a fresh short tap can still show. rawHeldMs={(int)rawHidActivity.PressedDuration.TotalMilliseconds}; rawLastStateAgeMs={(int)rawHidActivity.LastStateAge.TotalMilliseconds}.");
                        rawHidActivity = SteamRawHidGuideButtonActivity.None;
                    }
                    else if ((rawHidActivity.PressedDuration >= SteamSecondaryFallbackRawHidHoldSuppressDuration &&
                              rawHidLooksActiveHold) ||
                             (now - startedAt >= SteamSecondaryFallbackRawHidAmbiguousMaximumWait &&
                              rawHidLooksActiveHold))
                    {
                        lock (_guideButtonToastSync)
                        {
                            if (_pendingSteamSecondaryGuideFallbackByDevice.TryGetValue(key, out var pending) &&
                                ReferenceEquals(pending.Cancellation, cts))
                            {
                                _pendingSteamSecondaryGuideFallbackByDevice.Remove(key);
                                cts.Dispose();
                            }
                        }

                        GuideButtonEventLog.Write(
                            "secondary_fallback_raw_hid_hold_suppressed",
                            e.DeviceKind.ToString(),
                            e.Address,
                            e.DisplayName,
                            $"Secondary Steam HID monitor event was ignored because Raw HID still sees a fresh Steam-button hold. rawHeldMs={(int)rawHidActivity.PressedDuration.TotalMilliseconds}; rawLastStateAgeMs={(int)rawHidActivity.LastStateAge.TotalMilliseconds}.");
                        return;
                    }

                    if (rawHidActivity.IsPressed)
                    {
                        if (!rawHidWaitLogged)
                        {
                            rawHidWaitLogged = true;
                            GuideButtonEventLog.Write(
                                "secondary_fallback_waiting_for_raw_hid",
                                e.DeviceKind.ToString(),
                                e.Address,
                                e.DisplayName,
                                "Secondary Steam HID monitor event is waiting for Raw HID to decide whether this is a short tap or a power-off hold.");
                        }

                        delay = SteamSecondaryFallbackRawHidRecheckDelay;
                        continue;
                    }
                }
            }

            if (rawHidActivity.HasPendingRelease &&
                rawHidActivity.PendingPressDuration >= SteamSecondaryFallbackRawHidHoldSuppressDuration &&
                rawHidWasFreshForThisFallback)
            {
                lock (_guideButtonToastSync)
                {
                    if (_pendingSteamSecondaryGuideFallbackByDevice.TryGetValue(key, out var pending) &&
                        ReferenceEquals(pending.Cancellation, cts))
                    {
                        _pendingSteamSecondaryGuideFallbackByDevice.Remove(key);
                        _steamSecondaryGuideFallbackBlockedUntilByDevice[key] = now + SteamSecondaryFallbackBurstWindow;
                        cts.Dispose();
                    }
                }

                GuideButtonEventLog.Write(
                    "secondary_fallback_raw_hid_pending_hold_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    $"Secondary Steam HID monitor event was ignored because Raw HID release validation came from a long hold. rawPressMs={(int)rawHidActivity.PendingPressDuration.TotalMilliseconds}.");
                return;
            }

            if (rawHidActivity.HasPendingRelease &&
                rawHidActivity.PendingPressDuration >= SteamSecondaryFallbackRawHidHoldSuppressDuration)
            {
                _steamRawInputMonitor.ClearGuideButtonActivity(e.Address);
                GuideButtonEventLog.Write(
                    "secondary_fallback_stale_raw_hid_ignored",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    $"Secondary Steam HID monitor ignored an old Raw HID release validation so a fresh short tap can still show. rawPressMs={(int)rawHidActivity.PendingPressDuration.TotalMilliseconds}; rawLastPressedAgeMs={(int)rawHidActivity.PendingLastPressedAge.TotalMilliseconds}.");
                rawHidActivity = SteamRawHidGuideButtonActivity.None;
            }

            if (rawHidActivity.HasPendingRelease &&
                now - startedAt < SteamSecondaryFallbackRawHidShortTapMaximumWait)
            {
                if (!rawHidWaitLogged)
                {
                    rawHidWaitLogged = true;
                    GuideButtonEventLog.Write(
                        "secondary_fallback_waiting_for_raw_hid",
                        e.DeviceKind.ToString(),
                        e.Address,
                        e.DisplayName,
                        "Secondary Steam HID monitor event is waiting for Raw HID to finish release validation.");
                }

                delay = SteamSecondaryFallbackRawHidRecheckDelay;
                continue;
            }

            if (rawHidActivity.HasPendingRelease &&
                rawHidActivity.PendingReleaseAge >= SteamSecondaryFallbackRawHidAmbiguousMaximumWait)
            {
                lock (_guideButtonToastSync)
                {
                    if (_pendingSteamSecondaryGuideFallbackByDevice.TryGetValue(key, out var pending) &&
                        ReferenceEquals(pending.Cancellation, cts))
                    {
                        _pendingSteamSecondaryGuideFallbackByDevice.Remove(key);
                        _steamSecondaryGuideFallbackBlockedUntilByDevice[key] = now + SteamSecondaryFallbackBurstWindow;
                        cts.Dispose();
                    }
                }

                GuideButtonEventLog.Write(
                    "secondary_fallback_raw_hid_pending_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    $"Secondary Steam HID monitor event was ignored because Raw HID did not finish release validation in time. pendingReleaseMs={(int)rawHidActivity.PendingReleaseAge.TotalMilliseconds}.");
                return;
            }

            lock (_guideButtonToastSync)
            {
                if (!_pendingSteamSecondaryGuideFallbackByDevice.TryGetValue(key, out var pending) ||
                    !ReferenceEquals(pending.Cancellation, cts))
                {
                    return;
                }

                _pendingSteamSecondaryGuideFallbackByDevice.Remove(key);
                cts.Dispose();

                now = DateTimeOffset.Now;
                if (_lastSteamRawGuideButtonByDevice.TryGetValue(key, out var lastRawPress) &&
                    now - lastRawPress <= SteamRawInputPreferredWindow)
                {
                    return;
                }

                if (_steamSecondaryGuideFallbackBlockedUntilByDevice.TryGetValue(key, out var blockedUntil) &&
                    now <= blockedUntil)
                {
                    return;
                }

                if (_steamSecondaryGuideFallbackBlockedUntilByDevice.ContainsKey(key))
                {
                    _steamSecondaryGuideFallbackBlockedUntilByDevice.Remove(key);
                }
            }

            break;
        }

        if (ShouldSuppressDuplicateGuideButtonToast(e))
        {
            return;
        }

        if (ShouldSuppressSteamGuideToast(e.DeviceKind, e.Address, e.DisplayName, "secondary_fallback"))
        {
            return;
        }

        if (ShouldSuppressSteamSecondaryFallbackForCustomBatteryGuideTrigger(e))
        {
            return;
        }

        GuideButtonEventLog.Write(
            "secondary_fallback_accepted",
            e.DeviceKind.ToString(),
            e.Address,
            e.DisplayName,
            "Secondary Steam HID monitor event was used because RawInput did not produce a toast for this press.");

        _ = Dispatcher.BeginInvoke(new Action(() => ShowBatteryGuide(e)));
    }

    private bool ShouldSuppressSteamSecondaryFallbackForCustomBatteryGuideTrigger(GuideButtonPressedEventArgs e)
    {
        if (!ShouldCustomBatteryGuideTriggerOwnSteamSecondaryFallback(
                _viewModel.HasCustomBatteryGuideTriggerForDevice(e.DeviceKind)))
        {
            return false;
        }

        GuideButtonEventLog.Write(
            "secondary_fallback_custom_trigger_suppressed",
            e.DeviceKind.ToString(),
            e.Address,
            e.DisplayName,
            "Secondary Steam HID monitor event was ignored because a custom notification-button binding owns the Steam guide path.");
        return true;
    }

    internal static bool ShouldCustomBatteryGuideTriggerOwnSteamSecondaryFallback(bool hasCustomBatteryGuideTrigger)
    {
        return hasCustomBatteryGuideTrigger;
    }

    private void CancelPendingSteamSecondaryGuideFallbackLocked(string key)
    {
        if (!_pendingSteamSecondaryGuideFallbackByDevice.Remove(key, out var pending))
        {
            return;
        }

        try
        {
            pending.Cancellation.Cancel();
        }
        catch
        {
            // Ignore cancellation races.
        }
        finally
        {
            pending.Cancellation.Dispose();
        }
    }

    private bool ShouldSuppressDuplicateGuideButtonToast(GuideButtonPressedEventArgs e)
    {
        var key = BuildGuideButtonToastKey(e);
        var now = DateTimeOffset.Now;
        var cooldown = GetGuideButtonToastCooldown(e.DeviceKind);
        lock (_guideButtonToastSync)
        {
            var lastSeen = _lastGuideButtonToastByDevice.TryGetValue(key, out var recordedLastSeen)
                ? recordedLastSeen
                : (DateTimeOffset?)null;

            if (ShouldSuppressGuideButtonToast(lastSeen, now, e.DeviceKind))
            {
                GuideButtonEventLog.Write(
                    "duplicate_press_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    $"Duplicate guide-button event was ignored because another input path or recent toast already showed the toast. cooldownMs={(int)cooldown.TotalMilliseconds}.");
                return true;
            }

            _lastGuideButtonToastByDevice[key] = now;
            return false;
        }
    }

    private void MarkGuideButtonInputReportFallbackHandled(GuideButtonPressedEventArgs e)
    {
        var key = BuildGuideButtonToastKey(e);
        var suppressUntil = DateTimeOffset.Now + GuideButtonInputReportFallbackSuppressDuration;
        lock (_guideButtonToastSync)
        {
            _inputReportGuideFallbackSuppressRegularUntilByDevice[key] = suppressUntil;
        }
    }

    private bool ShouldSuppressGuideButtonPressAfterInputReportFallback(GuideButtonPressedEventArgs e)
    {
        var key = BuildGuideButtonToastKey(e);
        var now = DateTimeOffset.Now;
        lock (_guideButtonToastSync)
        {
            if (!_inputReportGuideFallbackSuppressRegularUntilByDevice.TryGetValue(key, out var suppressUntil))
            {
                return false;
            }

            if (now > suppressUntil)
            {
                _inputReportGuideFallbackSuppressRegularUntilByDevice.Remove(key);
                return false;
            }
        }

        GuideButtonEventLog.Write(
            "input_report_fallback_press_suppressed",
            e.DeviceKind.ToString(),
            e.Address,
            e.DisplayName,
            "Regular guide-button press event was ignored because the input-report fallback already showed the battery guide toast.");
        return true;
    }

    internal static bool ShouldSuppressGuideButtonToast(DateTimeOffset? lastSeen, DateTimeOffset now, GuideButtonDeviceKind deviceKind)
    {
        return lastSeen.HasValue && now - lastSeen.Value <= GetGuideButtonToastCooldown(deviceKind);
    }

    internal static TimeSpan GetGuideButtonToastCooldown(GuideButtonDeviceKind deviceKind)
    {
        return deviceKind == GuideButtonDeviceKind.SteamController
            ? SteamGuideButtonToastCooldown
            : GuideButtonGlobalDebounce;
    }

    internal static TimeSpan GetSteamSecondaryFallbackBurstWindow()
    {
        return SteamSecondaryFallbackBurstWindow;
    }

    internal static TimeSpan GetSteamSecondaryFallbackDelay()
    {
        return SteamSecondaryFallbackDelay;
    }

    internal static TimeSpan GetSteamRawInputPreferredWindow()
    {
        return SteamRawInputPreferredWindow;
    }

    private static string BuildGuideButtonToastKey(GuideButtonPressedEventArgs e)
    {
        if (e.DeviceKind == GuideButtonDeviceKind.DualSense)
        {
            return $"{e.DeviceKind}:DEFAULT";
        }

        var address = AddressNormalizer.NormalizeAddress(e.Address);
        return string.IsNullOrWhiteSpace(address)
            ? $"{e.DeviceKind}:{e.DisplayName}"
            : $"{e.DeviceKind}:{address}";
    }

    private void ShowBatteryGuide(GuideButtonPressedEventArgs e)
    {
        var item = FindGuideButtonDevice(e);
        var message = item is null
            ? BuildMissingGuideDeviceMessage(e)
            : BatteryGuideMessageBuilder.Build(item.Snapshot, _viewModel.Language);

        if (item is not null)
        {
            ShowBatteryToast(item.Snapshot, automatic: false);
            GuideButtonEventLog.Write(
                "popup_shown",
                e.DeviceKind.ToString(),
                e.Address,
                e.DisplayName,
                "Battery guide toast was shown at the bottom-right of the screen.");
        }
        else
        {
            GuideButtonEventLog.Write(
                "popup_missing_device",
                e.DeviceKind.ToString(),
                e.Address,
                e.DisplayName,
                "Guide button was detected, but no matching visible device card was found.");
        }

        if (item is null)
        {
            ShowTrayNotification(AppDisplayName, message, Forms.ToolTipIcon.Info);
            GuideButtonEventLog.Write(
                "tray_fallback",
                e.DeviceKind.ToString(),
                e.Address,
                e.DisplayName,
                "Battery guide was shown through the tray notification fallback.");
        }

        if (item is null)
        {
            RestartBatteryGuideChime();
        }
    }

    private void Devices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DeviceItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= DeviceItem_PropertyChanged;
                RemoveBatteryAlertToastKeysForDevice(item);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (DeviceItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += DeviceItem_PropertyChanged;
                PrimeBatteryAlertToastKeyForNewDevice(item);
            }
        }
    }

    private void DeviceItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DeviceItemViewModel item)
        {
            return;
        }

        if (e.PropertyName is nameof(DeviceItemViewModel.BatteryPercent) or
            nameof(DeviceItemViewModel.IsConnected) or
            nameof(DeviceItemViewModel.IsBatteryConnecting) or
            nameof(DeviceItemViewModel.IsStale))
        {
            CheckLowBatteryToast(item);
        }
    }

    private void CheckLowBatteryToast(DeviceItemViewModel item)
    {
        if (_viewModel.IsDisplaySleepPreparationActive)
        {
            return;
        }

        if (!item.IsConnected || item.IsStale || item.IsBatteryConnecting || item.BatteryPercent is not int percent)
        {
            RemoveBatteryAlertToastKeysForDevice(item);
            return;
        }

        if (!IsBatteryAlertEnabledForDevice(item))
        {
            RemoveBatteryAlertToastKeysForDevice(item);
            _batteryAlertInitializedDeviceKeys.Add(BuildBatteryToastKey(item));
            return;
        }

        var thresholds = BuildBatteryAlertThresholds(_viewModel.BatteryAlertThresholds);
        foreach (var threshold in thresholds.Where(threshold => percent > threshold))
        {
            _lowBatteryToastKeys.Remove(BuildBatteryAlertToastKey(item, threshold));
        }

        var targetThreshold = ResolveBatteryAlertThresholdToShow(percent, thresholds);
        if (targetThreshold <= 0)
        {
            _batteryAlertInitializedDeviceKeys.Add(BuildBatteryToastKey(item));
            return;
        }

        var deviceKey = BuildBatteryToastKey(item);
        var key = BuildBatteryAlertToastKey(item, targetThreshold);
        var isFirstValidBatteryReading = _batteryAlertInitializedDeviceKeys.Add(deviceKey);
        if (isFirstValidBatteryReading &&
            targetThreshold > WidgetSettings.ForcedBatteryAlertThresholdPercent)
        {
            _lowBatteryToastKeys.Add(key);
            GuideButtonEventLog.Write(
                "automatic_battery_toast_baselined",
                item.Category.ToString(),
                item.Address,
                item.DisplayName,
                $"Automatic battery toast was baselined without showing on first sight. percent={percent}; threshold={targetThreshold}.");
            return;
        }

        if (!_lowBatteryToastKeys.Add(key))
        {
            return;
        }

        ShowBatteryToast(item.Snapshot, automatic: true);
        GuideButtonEventLog.Write(
            "automatic_battery_toast_shown",
            item.Category.ToString(),
            item.Address,
            item.DisplayName,
            $"Automatic battery toast was shown. percent={percent}; threshold={targetThreshold}.");
    }

    private void PrimeBatteryAlertToastKeyForNewDevice(DeviceItemViewModel item)
    {
        if (_viewModel.IsDisplaySleepPreparationActive)
        {
            return;
        }

        if (!item.IsConnected || item.IsStale || item.IsBatteryConnecting || item.BatteryPercent is not int percent)
        {
            return;
        }

        if (!IsBatteryAlertEnabledForDevice(item))
        {
            RemoveBatteryAlertToastKeysForDevice(item);
            _batteryAlertInitializedDeviceKeys.Add(BuildBatteryToastKey(item));
            return;
        }

        var thresholds = BuildBatteryAlertThresholds(_viewModel.BatteryAlertThresholds);
        var targetThreshold = ResolveBatteryAlertThresholdToShow(percent, thresholds);
        _batteryAlertInitializedDeviceKeys.Add(BuildBatteryToastKey(item));

        if (targetThreshold <= 0)
        {
            return;
        }

        if (targetThreshold <= WidgetSettings.ForcedBatteryAlertThresholdPercent)
        {
            CheckLowBatteryToast(item);
            return;
        }

        _lowBatteryToastKeys.Add(BuildBatteryAlertToastKey(item, targetThreshold));
        GuideButtonEventLog.Write(
            "automatic_battery_toast_baselined",
            item.Category.ToString(),
            item.Address,
            item.DisplayName,
            $"Automatic battery toast was baselined for a newly visible device. percent={percent}; threshold={targetThreshold}.");
    }

    private void ResetBatteryAlertToastKeys()
    {
        _lowBatteryToastKeys.Clear();
        _batteryAlertInitializedDeviceKeys.Clear();
    }

    private void CheckAllLowBatteryToasts()
    {
        foreach (var item in _viewModel.Devices)
        {
            CheckLowBatteryToast(item);
        }
    }

    private void PrimeBatteryAlertToastKeysForCurrentLevels()
    {
        ResetBatteryAlertToastKeys();
        var thresholds = BuildBatteryAlertThresholds(_viewModel.BatteryAlertThresholds);
        foreach (var item in _viewModel.Devices)
        {
            if (!item.IsConnected || item.IsStale || item.IsBatteryConnecting || item.BatteryPercent is not int percent)
            {
                continue;
            }

            if (!IsBatteryAlertEnabledForDevice(item))
            {
                _batteryAlertInitializedDeviceKeys.Add(BuildBatteryToastKey(item));
                continue;
            }

            var targetThreshold = ResolveBatteryAlertThresholdToShow(percent, thresholds);
            if (targetThreshold > 0)
            {
                _lowBatteryToastKeys.Add(BuildBatteryAlertToastKey(item, targetThreshold));
            }

            _batteryAlertInitializedDeviceKeys.Add(BuildBatteryToastKey(item));
        }
    }

    private void RemoveBatteryAlertToastKeysForDevice(DeviceItemViewModel item)
    {
        var prefix = BuildBatteryToastKey(item) + "|";
        _lowBatteryToastKeys.RemoveWhere(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        _batteryAlertInitializedDeviceKeys.Remove(BuildBatteryToastKey(item));
    }

    internal static IReadOnlyList<int> BuildBatteryAlertThresholds(string? customThresholds)
    {
        return WidgetSettings.GetBatteryAlertThresholdPercents(customThresholds)
            .Append(WidgetSettings.ForcedBatteryAlertThresholdPercent)
            .Distinct()
            .OrderBy(threshold => threshold)
            .ToArray();
    }

    internal static int ResolveBatteryAlertThresholdToShow(int percent, IReadOnlyList<int> thresholds)
    {
        return thresholds.FirstOrDefault(threshold => percent <= threshold);
    }

    private static string BuildBatteryAlertToastKey(DeviceItemViewModel item, int threshold)
    {
        return $"{BuildBatteryToastKey(item)}|T{threshold:D2}";
    }

    private bool IsBatteryAlertEnabledForDevice(DeviceItemViewModel item)
    {
        var key = BuildBatteryAlertDeviceSettingKey(item);
        return string.IsNullOrWhiteSpace(key) || IsBatteryAlertEnabledForDeviceKey(key);
    }

    private bool IsBatteryAlertEnabledForDeviceKey(string key)
    {
        return !_viewModel.BatteryAlertDeviceEnabled.TryGetValue(key, out var isEnabled) || isEnabled;
    }

    internal static string BuildBatteryAlertDeviceSettingKey(DeviceItemViewModel item)
    {
        var address = AddressNormalizer.NormalizeAddress(item.Address);
        if (!string.IsNullOrWhiteSpace(address))
        {
            return WidgetSettings.NormalizeBatteryAlertDeviceKey($"A:{address}");
        }

        if (!string.IsNullOrWhiteSpace(item.ModelKey))
        {
            return WidgetSettings.NormalizeBatteryAlertDeviceKey($"M:{item.ModelKey}");
        }

        return WidgetSettings.NormalizeBatteryAlertDeviceKey($"D:{item.DeviceId}");
    }

    private void ShowBatteryToast(DeviceBatterySnapshot snapshot, bool automatic)
    {
        if (snapshot.BatteryPercent is not int percent)
        {
            return;
        }

        CloseActiveBatteryToast();
        var severity = BatteryToastStyle.ResolveSeverity(percent);
        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, _viewModel.Language, automatic);
        var toast = new BatteryToastWindow(snapshot.DisplayName, percent, subtitle, severity);
        _activeBatteryToastWindow = toast;
        toast.Closed += (_, _) =>
        {
            if (ReferenceEquals(_activeBatteryToastWindow, toast))
            {
                _activeBatteryToastWindow = null;
                StopBatteryGuideChime();
            }
        };
        toast.ShowAtBottomRight();
        RestartBatteryGuideChime();
    }

    private void CloseActiveBatteryToast()
    {
        try
        {
            _activeBatteryToastWindow?.Close();
        }
        catch
        {
            // Ignore toast shutdown races.
        }
        finally
        {
            _activeBatteryToastWindow = null;
            StopBatteryGuideChime();
        }
    }

    private static string BuildBatteryToastKey(DeviceItemViewModel item)
    {
        var address = AddressNormalizer.NormalizeAddress(item.Address);
        return string.IsNullOrWhiteSpace(address) ? item.DeviceId : address;
    }

    private void RestartBatteryGuideChime()
    {
        if (!_viewModel.GuideSoundEnabled)
        {
            return;
        }

        try
        {
            _batteryGuideChimePlayer.PlayFromStart(BatteryGuideSoundCatalog.ResolveGuideSound(
                _viewModel.GuideSoundId,
                _viewModel.CustomGuideSoundPath));
        }
        catch
        {
            // Ignore shutdown races.
        }
    }

    private void StopBatteryGuideChime()
    {
        try
        {
            _batteryGuideChimePlayer.Stop();
        }
        catch
        {
            // Ignore shutdown races.
        }
    }

    private void OpenLabsWindow()
    {
        if (_labsWindow is not null)
        {
            _labsWindow.Activate();
            return;
        }

        var labsWindow = new LabsWindow
        {
            Owner = this
        };
        _labsWindow = labsWindow;
        labsWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_labsWindow, labsWindow))
            {
                _labsWindow = null;
            }
        };

        labsWindow.Show();
    }

    private DeviceItemViewModel? FindGuideButtonDevice(GuideButtonPressedEventArgs e)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(e.Address);
        if (!string.IsNullOrWhiteSpace(normalizedAddress))
        {
            var byAddress = _viewModel.Devices.FirstOrDefault(device =>
                string.Equals(
                    AddressNormalizer.NormalizeAddress(device.Address),
                    normalizedAddress,
                    StringComparison.OrdinalIgnoreCase));
            if (byAddress is not null)
            {
                return byAddress;
            }
        }

        return e.DeviceKind switch
        {
            GuideButtonDeviceKind.DualSense => _viewModel.Devices.FirstOrDefault(device =>
                device.DisplayName.Contains("DualSense", StringComparison.OrdinalIgnoreCase) ||
                device.BaseDisplayName.Contains("DualSense", StringComparison.OrdinalIgnoreCase) ||
                (device.ModelKey?.Contains("VID_054C", StringComparison.OrdinalIgnoreCase) ?? false)),
            GuideButtonDeviceKind.SteamController => _viewModel.Devices.FirstOrDefault(device =>
                IsSteamControllerDevice(device)),
            _ => null
        };
    }

    private IReadOnlyList<GuideButtonKnownDevice> GetGuideButtonKnownDevices()
    {
        if (!Dispatcher.CheckAccess())
        {
            return Dispatcher.Invoke(GetGuideButtonKnownDevices);
        }

        return _viewModel.Devices
            .Where(IsSteamControllerDevice)
            .Select(device => new GuideButtonKnownDevice(
                device.Address,
                device.DisplayName,
                GuideButtonDeviceKind.SteamController))
            .GroupBy(device => AddressNormalizer.NormalizeAddress(device.Address), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group => group.First())
            .ToList();
    }

    private static bool IsSteamControllerDevice(DeviceItemViewModel device)
    {
        return device.SourceKind == BatterySourceKind.SteamHid ||
               device.DeviceId.StartsWith("steam-triton:", StringComparison.OrdinalIgnoreCase) ||
               device.DisplayName.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) ||
               device.DisplayName.Contains("Steam Ctrl", StringComparison.OrdinalIgnoreCase) ||
               device.BaseDisplayName.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) ||
               device.BaseDisplayName.Contains("Steam Ctrl", StringComparison.OrdinalIgnoreCase) ||
               (device.ModelKey?.Contains("VID_28DE", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string BuildMissingGuideDeviceMessage(GuideButtonPressedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(e.DisplayName)
            ? ExtraText("MissingGuideDeviceFallbackName")
            : e.DisplayName.Trim();

        return ExtraFormat("MissingGuideDeviceMessageFormat", name);
    }

    private void RestartBatteryGuideHideTimer(DeviceItemViewModel item)
    {
        var key = AddressNormalizer.NormalizeAddress(item.Address);
        if (string.IsNullOrWhiteSpace(key))
        {
            key = item.DeviceId;
        }

        if (_batteryGuideHideTimers.TryGetValue(key, out var existing))
        {
            existing.Stop();
            _batteryGuideHideTimers.Remove(key);
        }

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3.2)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _batteryGuideHideTimers.Remove(key);
            item.HideBatteryGuide();
        };

        _batteryGuideHideTimers[key] = timer;
        timer.Start();
    }

    private void StopBatteryGuideTimers()
    {
        foreach (var timer in _batteryGuideHideTimers.Values)
        {
            timer.Stop();
        }

        _batteryGuideHideTimers.Clear();
    }

    private void ShowTrayNotification(string title, string message, Forms.ToolTipIcon icon)
    {
        _trayIconService.ShowNotification(title, message, icon);
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfControls.Button ||
                source is WpfToggleButton ||
                source is WpfControls.TextBox ||
                source is WpfControls.ComboBox ||
                source is WpfControls.Slider ||
                source is WpfScrollBar ||
                source is WpfControls.ListBoxItem ||
                source is WpfThumb ||
                source is WpfControls.TextBlock textBlock && textBlock.Name == "DisplayNameTextBlock" ||
                source is FrameworkElement frameworkElement && IsNamedInteractiveElement(frameworkElement.Name))
            {
                return true;
            }

            source = GetDependencyParent(source);
        }

        return false;
    }

    private static bool IsPopupDragBlocked(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfControls.Button ||
                source is WpfToggleButton ||
                source is WpfControls.TextBox ||
                source is WpfControls.ComboBox ||
                source is WpfControls.Slider ||
                source is WpfScrollBar ||
                source is WpfControls.ListBoxItem ||
                source is WpfThumb ||
                source is FrameworkElement frameworkElement && IsNamedPopupInteractiveElement(frameworkElement.Name))
            {
                return true;
            }

            source = GetDependencyParent(source);
        }

        return false;
    }

    private bool IsInsidePopupChrome(DependencyObject? source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, SettingsPopupChrome) ||
                ReferenceEquals(source, ColorPopupChrome))
            {
                return true;
            }

            source = GetDependencyParent(source);
        }

        return false;
    }

    private WpfPopup? GetPopupForChrome(FrameworkElement chrome)
    {
        if (ReferenceEquals(chrome, SettingsPopupChrome))
        {
            return SettingsPopup;
        }

        if (ReferenceEquals(chrome, ColorPopupChrome))
        {
            return ColorCustomPopup;
        }

        return null;
    }

    private void FinishPopupDrag()
    {
        var chrome = _popupDragChrome;
        _draggingPopup = null;
        _popupDragChrome = null;

        if (chrome?.IsMouseCaptured == true)
        {
            chrome.ReleaseMouseCapture();
        }
    }

    private static DependencyObject? GetDependencyParent(DependencyObject source)
    {
        try
        {
            return System.Windows.Media.VisualTreeHelper.GetParent(source) ??
                   LogicalTreeHelper.GetParent(source);
        }
        catch (InvalidOperationException)
        {
            return LogicalTreeHelper.GetParent(source);
        }
    }

    private static bool IsNamedInteractiveElement(string name)
    {
        return name is "DeviceIconHitArea"
            or "ColorCustomizeButton"
            or "SettingsPopupChrome"
            or "ColorPopupChrome"
            or "ColorCustomPopup"
            or "PaletteSurface"
            or "PaletteCursor"
            or "SelectedColorPreviewBorder"
            or "SelectedColorHexText"
            or "PrimaryTextColorButton"
            or "SecondaryTextColorButton"
            or "BatteryTextColorButton"
            or "GlassSurfaceColorButton"
            or "CardTintColorButton"
            or "CardBorderColorButton"
            or "TrackColorButton"
            or "PanelColorButton";
    }

    private static bool IsNamedPopupInteractiveElement(string name)
    {
        return name is "PaletteSurface"
            or "PaletteCursor"
            or "SelectedColorPreviewBorder"
            or "SelectedColorHexText"
            or "PrimaryTextColorButton"
            or "SecondaryTextColorButton"
            or "BatteryTextColorButton"
            or "GlassSurfaceColorButton"
            or "CardTintColorButton"
            or "CardBorderColorButton"
            or "TrackColorButton"
            or "PanelColorButton";
    }

    private void BeginRename(DeviceItemViewModel item)
    {
        if (_renamingItem is not null && !ReferenceEquals(_renamingItem, item))
        {
            _renamingItem.CancelRename();
        }

        item.BeginRename();
        _renamingItem = item;
    }

    private void CancelRename(DeviceItemViewModel item)
    {
        item.CancelRename();
        if (ReferenceEquals(_renamingItem, item))
        {
            _renamingItem = null;
        }
    }

    private void CommitRename(DeviceItemViewModel item)
    {
        var trimmed = item.EditableName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            item.CancelRename();
            if (ReferenceEquals(_renamingItem, item))
            {
                _renamingItem = null;
            }

            return;
        }

        _viewModel.SetNameOverride(item.Address, trimmed);
        item.ApplyRenamedDisplayName(trimmed);
        if (ReferenceEquals(_renamingItem, item))
        {
            _renamingItem = null;
        }
    }

    private void BeginIconEdit(DeviceItemViewModel item)
    {
        if (_iconEditingItem is not null && !ReferenceEquals(_iconEditingItem, item))
        {
            _iconEditingItem.CancelIconEdit();
        }

        item.BeginIconEdit();
        _iconEditingItem = item;
    }

    private void CancelIconEdit(DeviceItemViewModel item)
    {
        item.CancelIconEdit();
        if (ReferenceEquals(_iconEditingItem, item))
        {
            _iconEditingItem = null;
        }
    }

    private static bool TryGetDeviceItem(object sender, out DeviceItemViewModel item)
    {
        item = null!;
        if (sender is not FrameworkElement element || element.DataContext is not DeviceItemViewModel dataContext)
        {
            return false;
        }

        item = dataContext;
        return true;
    }

    private static string? PersistCustomIconImage(string address, string sourcePath)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        var trimmedPath = sourcePath?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAddress) || string.IsNullOrWhiteSpace(trimmedPath))
        {
            return null;
        }

        try
        {
            if (!File.Exists(trimmedPath))
            {
                return null;
            }

            Directory.CreateDirectory(CustomIconDirectory);

            var fileStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var targetPath = Path.Combine(CustomIconDirectory, $"{normalizedAddress}_{fileStamp}.png");
            foreach (var existingFile in Directory.GetFiles(CustomIconDirectory, $"{normalizedAddress}*.*"))
            {
                if (!string.Equals(existingFile, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(existingFile);
                    }
                    catch
                    {
                        // Ignore cleanup failures.
                    }
                }
            }

            return CustomIconImageProcessor.TryCreateSoftRoundIcon(trimmedPath, targetPath)
                ? targetPath
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? PersistCustomFont(string sourcePath)
    {
        var trimmedPath = sourcePath?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            return null;
        }

        try
        {
            if (!File.Exists(trimmedPath))
            {
                return null;
            }

            var extension = Path.GetExtension(trimmedPath).ToLowerInvariant();
            if (extension is not ".ttf" and not ".otf")
            {
                return null;
            }

            Directory.CreateDirectory(CustomFontDirectory);

            var baseName = Path.GetFileNameWithoutExtension(trimmedPath);
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(invalidChar, '_');
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "custom-font";
            }

            var fileStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var targetPath = Path.Combine(CustomFontDirectory, $"{baseName}_{fileStamp}{extension}");
            File.Copy(trimmedPath, targetPath, overwrite: false);
            return targetPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? PersistCustomGuideSound(string sourcePath)
    {
        var trimmedPath = sourcePath?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            return null;
        }

        try
        {
            if (!File.Exists(trimmedPath))
            {
                return null;
            }

            var extension = Path.GetExtension(trimmedPath).ToLowerInvariant();
            if (extension is not ".wav" and not ".mp3" and not ".wma" and not ".m4a")
            {
                return null;
            }

            Directory.CreateDirectory(CustomGuideSoundDirectory);

            var baseName = Path.GetFileNameWithoutExtension(trimmedPath);
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(invalidChar, '_');
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "custom-guide-sound";
            }

            foreach (var existingFile in Directory.GetFiles(CustomGuideSoundDirectory, "custom-guide-sound_*.*"))
            {
                TryDeleteFile(existingFile);
            }

            var fileStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var targetPath = Path.Combine(CustomGuideSoundDirectory, $"custom-guide-sound_{baseName}_{fileStamp}{extension}");
            File.Copy(trimmedPath, targetPath, overwrite: false);
            return targetPath;
        }
        catch
        {
            return null;
        }
    }

    private string? OpenIconImageAdjustDialog(string sourcePath)
    {
        try
        {
            var adjustWindow = new IconImageAdjustWindow(sourcePath)
            {
                Owner = this
            };

            if (adjustWindow.ShowDialog() != true)
            {
                return null;
            }

            return adjustWindow.ResultImagePath;
        }
        catch
        {
            System.Windows.MessageBox.Show(
                this,
                _viewModel.CurrentLanguageText.IconAdjustFallbackMessage,
                _viewModel.CurrentLanguageText.IconAdjustFallbackTitle,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return sourcePath;
        }
    }

    private static void DeleteGeneratedTemporaryIcon(string? path)
    {
        if (!IconImageAdjustWindow.IsGeneratedTempPath(path))
        {
            return;
        }

        try
        {
            File.Delete(path!);
        }
        catch
        {
            // Ignore temporary file cleanup failures.
        }
    }

    private sealed record Ds5DongleFirmwareInfo(
        string Version,
        string AssetName,
        string DownloadUrl,
        string ReleaseUrl);

    private sealed record PendingSteamSecondaryGuideFallback(
        GuideButtonPressedEventArgs EventArgs,
        CancellationTokenSource Cancellation);

    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;

    private const uint DwmBbEnable = 0x00000001;
    private const uint DwmBbBlurRegion = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmBlurBehind
    {
        public uint DwFlags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool FEnable;

        public IntPtr HRgnBlur;

        [MarshalAs(UnmanagedType.Bool)]
        public bool FTransitionOnMaximized;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DwmBlurBehind pBlurBehind);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(
        int nLeftRect,
        int nTopRect,
        int nRightRect,
        int nBottomRect,
        int nWidthEllipse,
        int nHeightEllipse);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
