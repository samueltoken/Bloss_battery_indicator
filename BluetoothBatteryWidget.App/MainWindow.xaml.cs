using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
    private const string FallbackVersion = "1.0.5";
    private static readonly TimeSpan GuideButtonGlobalDebounce = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan SteamGuideButtonToastCooldown = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SteamRawInputPreferredWindow = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan SteamSecondaryFallbackDelay = TimeSpan.FromMilliseconds(380);
    private static readonly TimeSpan SteamSecondaryFallbackBurstWindow = TimeSpan.FromSeconds(6);
    private const string UpdateLatestReleaseApiUrl = "https://api.github.com/repos/samueltoken/Bloss_battery_indicator/releases/latest";
    private const string Ds5DongleLatestReleaseApiUrl = "https://api.github.com/repos/awalol/DS5Dongle/releases/latest";
    private const string Ds5DongleReleasePageUrl = "https://github.com/awalol/DS5Dongle/releases";
    private const string UpdateExpectedAssetName = "setup.exe";
    private const string UpdateExpectedChecksumAssetName = "setup.exe.sha256";
    private const long UpdateMaxInstallerBytes = 250L * 1024 * 1024;
    private const long Ds5DongleMaxFirmwareBytes = 16L * 1024 * 1024;
    private const int UpdateMaxChecksumBytes = 16 * 1024;
    private static readonly string[] UpdateTrustedDownloadHosts =
    [
        "github.com",
        "objects.githubusercontent.com",
        "github-releases.githubusercontent.com",
        "release-assets.githubusercontent.com"
    ];
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
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly DrawingIcon _appIcon;

    private bool _isExiting;
    private bool _forceClose;
    private DeviceItemViewModel? _renamingItem;
    private DeviceItemViewModel? _iconEditingItem;
    private bool _isCompactMode;
    private bool _initialBoundsApplied;
    private bool _startHiddenInTrayOnLoad;
    private bool _isColorPresetSyncing;
    private bool _isLanguageSyncing;
    private bool _isGuideSoundSyncing;
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
    private Forms.ToolStripMenuItem? _trayOpenMenuItem;
    private Forms.ToolStripMenuItem? _trayRefreshMenuItem;
    private Forms.ToolStripMenuItem? _trayResetPositionMenuItem;
    private Forms.ToolStripMenuItem? _trayAutostartMenuItem;
    private Forms.ToolStripMenuItem? _trayStartMinimizedToTrayMenuItem;
    private Forms.ToolStripMenuItem? _trayExitMenuItem;
    private readonly HttpClient _httpClient = new();
    private readonly GuideButtonMonitorService _guideButtonMonitor = new();
    private readonly SteamControllerRawInputMonitorService _steamRawInputMonitor = new();
    private readonly BatteryGuideChimePlayer _batteryGuideChimePlayer = new(BatteryGuideChimeWave);
    private readonly System.Windows.Threading.DispatcherTimer _guideSoundPreviewResetTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer _settingsAutoCloseTimer = new();
    private readonly Dictionary<string, System.Windows.Threading.DispatcherTimer> _batteryGuideHideTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastGuideButtonToastByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastSteamRawGuideButtonByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingSteamSecondaryGuideFallback> _pendingSteamSecondaryGuideFallbackByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _steamSecondaryGuideFallbackBlockedUntilByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FrameworkElement, int> _settingsAccordionAnimationTokens = new();
    private readonly Dictionary<WpfControls.TextBlock, (double FontSize, FontWeight FontWeight)> _settingsTextBlockDefaults = new();
    private readonly Dictionary<WpfControls.Control, (double FontSize, FontWeight FontWeight)> _settingsControlTextDefaults = new();
    private readonly object _guideButtonToastSync = new();
    private readonly HashSet<string> _lowBatteryToastKeys = new(StringComparer.OrdinalIgnoreCase);
    private BatteryToastWindow? _activeBatteryToastWindow;
    private LabsWindow? _labsWindow;
    private HwndSource? _windowMessageSource;
    private bool _isGuideSoundPreviewPlaying;
    private bool _isUpdating;
    private bool _isPicoFirmwareUpdating;

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
        _viewModel = viewModel;
        DataContext = _viewModel;

        InitializeComponent();
        ColorPresetComboBox.ItemsSource = ColorPresetCatalog.Presets;
        LanguageComboBox.ItemsSource = _viewModel.LanguageOptions;
        LanguageComboBox.DisplayMemberPath = nameof(UiLanguageOption.Label);
        GuideSoundComboBox.ItemsSource = BatteryGuideSoundCatalog.GetGuideOptions(_viewModel.CustomGuideSoundPath);
        GuideSoundComboBox.DisplayMemberPath = nameof(BatteryGuideSoundOption.DisplayName);
        _appIcon = LoadAppIcon();
        _trayIcon = BuildTrayIcon();
        RefreshTrayMenuTexts();
        UpdateVersionMenuHeader();
        SyncGuideSoundSelection();
        UpdateGuideSoundControls();
        ResetUpdateProgressUi();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Devices.CollectionChanged += Devices_CollectionChanged;
        _guideButtonMonitor.GuideButtonPressed += GuideButtonMonitor_GuideButtonPressed;
        _guideButtonMonitor.SetKnownDeviceProvider(GetGuideButtonKnownDevices);
        _steamRawInputMonitor.GuideButtonPressed += GuideButtonMonitor_GuideButtonPressed;
        _steamRawInputMonitor.SetKnownDeviceProvider(GetGuideButtonKnownDevices);
        _batteryGuideChimePlayer.PlaybackEnded += BatteryGuideChimePlayer_PlaybackEnded;
        _guideSoundPreviewResetTimer.Tick += GuideSoundPreviewResetTimer_Tick;
        _settingsAutoCloseTimer.Interval = TimeSpan.FromMilliseconds(280);
        _settingsAutoCloseTimer.Tick += SettingsAutoCloseTimer_Tick;

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
        ApplyColorPreset(_viewModel.ColorPresetId);
        ApplySettingsTextStyle();
        ApplyCustomFont();
        SyncColorPresetSelection();
        SyncLanguageSelection();
        SyncGuideSoundSelection();
        UpdateGuideSoundControls();
        ApplyUiScaleStep(_viewModel.UiScaleStep, animate: false);
        RefreshTrayMenuTexts();
        UpdateVersionMenuHeader();
        UpdateResizeGripPlacement();
        UpdateDwmBlurBehindRegion();
        ApplyVisualModeState();
        _guideButtonMonitor.Start();
        _steamRawInputMonitor.LogRawDeviceSummary();

        if (_startHiddenInTrayOnLoad)
        {
            _startHiddenInTrayOnLoad = false;
            Hide();
            WindowState = WindowState.Normal;
            Opacity = 1d;
            ShowActivated = true;
        }
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
        _guideButtonMonitor.Dispose();
        _steamRawInputMonitor.GuideButtonPressed -= GuideButtonMonitor_GuideButtonPressed;
        _steamRawInputMonitor.Dispose();
        _batteryGuideChimePlayer.PlaybackEnded -= BatteryGuideChimePlayer_PlaybackEnded;
        _windowMessageSource?.RemoveHook(MainWindow_WndProc);
        _windowMessageSource = null;
        _guideSoundPreviewResetTimer.Stop();
        _settingsAutoCloseTimer.Stop();
        StopBatteryGuideTimers();
        CloseActiveBatteryToast();
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

    private void LoadCustomGuideSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _viewModel.TextCustomGuideSoundSelectTitle,
            Filter = "Audio files (*.wav;*.mp3;*.wma;*.m4a)|*.wav;*.mp3;*.wma;*.m4a|All files (*.*)|*.*",
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
                var rememberedVersion = NormalizeReleaseVersion(_viewModel.LastDs5DongleFirmwareVersion);
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

            var installedVersion = NormalizeReleaseVersion(installedFirmware.Version);
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
        var isKorean = string.Equals(_viewModel.Language, WidgetSettings.KoreanLanguage, StringComparison.OrdinalIgnoreCase);
        return scan.Status switch
        {
            Ds5DongleFirmwareVersionReadStatus.OnlyBluetoothDualSenseEndpoints => isKorean
                ? "상세: 현재 Windows에는 Bluetooth DualSense만 보이고, USB DS5Dongle 장치는 보이지 않습니다. DS5Dongle은 컨트롤러가 Pico2W에 연결된 뒤에야 USB 장치로 보일 수 있습니다."
                : "Detail: Windows currently shows Bluetooth DualSense endpoints, but no USB DS5Dongle endpoint. DS5Dongle may appear as a USB device only after the controller connects through Pico2W.",
            Ds5DongleFirmwareVersionReadStatus.NoUsbDs5DongleEndpoint => isKorean
                ? "상세: 일반 USB DS5Dongle HID 장치를 찾지 못했습니다. Pico2W가 USB로 연결되어 있고, DualSense가 Pico2W를 통해 연결된 상태인지 확인해 주세요."
                : "Detail: No normal USB DS5Dongle HID endpoint was found. Check that Pico2W is connected over USB and DualSense is connected through Pico2W.",
            Ds5DongleFirmwareVersionReadStatus.UsbDs5DongleOpenFailed => isKorean
                ? "상세: USB DS5Dongle 후보는 보였지만 앱이 읽기용으로 열지 못했습니다. 브라우저 설정 페이지나 다른 앱이 잡고 있으면 닫고 다시 시도해 주세요."
                : "Detail: A USB DS5Dongle candidate was found, but the app could not open it. Close the browser config page or other apps and try again.",
            Ds5DongleFirmwareVersionReadStatus.FirmwareVersionReportUnavailable => isKorean
                ? "상세: USB DS5Dongle 후보는 열렸지만 펌웨어 버전 리포트 0xF8을 받지 못했습니다. v0.6.0-hotfix 이전 펌웨어일 수 있습니다."
                : "Detail: A USB DS5Dongle candidate opened, but report 0xF8 did not return a firmware version. The firmware may be older than v0.6.0-hotfix.",
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

    private void ColorPresetComboBox_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        QueueColorPresetMarqueeUpdate(FindColorPresetMarqueeText(ColorPresetComboBox));
    }

    private void ColorPresetComboBox_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        QueueColorPresetMarqueeReset(FindColorPresetMarqueeText(ColorPresetComboBox));
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
            Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf|All files (*.*)|*.*",
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
        await _viewModel.RefreshAsync().ConfigureAwait(true);
    }

    private async void IconOverridesButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshots = _viewModel.GetDeviceSnapshots();
        var existingOverrides = IconOverrideParser.Parse(_viewModel.Settings.IconOverrides);
        var existingImageOverrides = IconImageOverrideParser.Parse(_viewModel.Settings.IconImageOverrides);
        var dialog = new IconOverrideWindow(snapshots, existingOverrides, existingImageOverrides)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
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

            var (releaseInfo, errorMessage) = await TryGetLatestReleaseAssetAsync().ConfigureAwait(true);
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
            if (!IsRemoteVersionNewer(currentVersion, releaseInfo.Version))
            {
                SetUpdateProgressUi(_viewModel.CurrentLanguageText.UpdateNoUpdate, 100, isIndeterminate: false);
                await Task.Delay(1400).ConfigureAwait(true);
                ResetUpdateProgressUi();
                return;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "Bloss", "updates");
            Directory.CreateDirectory(tempRoot);
            var setupPath = Path.Combine(
                tempRoot,
                $"setup-{releaseInfo.Version}-{DateTime.UtcNow:yyyyMMddHHmmss}.exe");

            await DownloadUpdateAssetAsync(releaseInfo.SetupDownloadUrl, setupPath).ConfigureAwait(true);

            SetUpdateProgressUi(_viewModel.CurrentLanguageText.UpdateVerifying, 100, isIndeterminate: true);
            var checksumContent = await DownloadUpdateChecksumAssetAsync(releaseInfo.ChecksumDownloadUrl).ConfigureAwait(true);
            if (!TryExtractSha256Hash(checksumContent, out var expectedHash))
            {
                TryDeleteFile(setupPath);
                throw new InvalidOperationException(_viewModel.CurrentLanguageText.UpdateVerificationFailed);
            }

            var downloadedHash = ComputeFileSha256(setupPath);
            if (!string.Equals(downloadedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(setupPath);
                throw new InvalidOperationException(_viewModel.CurrentLanguageText.UpdateVerificationFailed);
            }

            SetUpdateProgressUi(_viewModel.CurrentLanguageText.UpdateInstallStarting, 100, isIndeterminate: false);
            StartInstallerUpdateAndRestart(setupPath);
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

    private async Task<(UpdateReleaseAssetInfo? Release, string? ErrorMessage)> TryGetLatestReleaseAssetAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UpdateLatestReleaseApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppDisplayName, GetDisplayVersion()));

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(true);

            if (!response.IsSuccessStatusCode)
            {
                return (null, _viewModel.CurrentLanguageText.UpdateReleaseReadFailed);
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(true);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagNameElement))
            {
                return (null, _viewModel.CurrentLanguageText.UpdateReleaseReadFailed);
            }

            if (root.TryGetProperty("draft", out var draftElement) &&
                draftElement.ValueKind is JsonValueKind.True &&
                draftElement.GetBoolean())
            {
                return (null, _viewModel.CurrentLanguageText.UpdateReleaseReadFailed);
            }

            if (root.TryGetProperty("prerelease", out var prereleaseElement) &&
                prereleaseElement.ValueKind is JsonValueKind.True &&
                prereleaseElement.GetBoolean())
            {
                return (null, _viewModel.CurrentLanguageText.UpdateReleaseReadFailed);
            }

            var latestVersion = NormalizeReleaseVersion(tagNameElement.GetString());
            if (!root.TryGetProperty("assets", out var assetsElement) ||
                assetsElement.ValueKind != JsonValueKind.Array)
            {
                return (null, _viewModel.CurrentLanguageText.UpdateAssetMissing);
            }

            string? setupDownloadUrl = null;
            string? checksumDownloadUrl = null;
            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement) ||
                    !asset.TryGetProperty("browser_download_url", out var urlElement))
                {
                    continue;
                }

                var assetName = nameElement.GetString();
                var downloadUrl = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(downloadUrl))
                {
                    continue;
                }

                if (string.Equals(assetName, UpdateExpectedAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    setupDownloadUrl = downloadUrl;
                    continue;
                }

                if (string.Equals(assetName, UpdateExpectedChecksumAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    checksumDownloadUrl = downloadUrl;
                }
            }

            if (string.IsNullOrWhiteSpace(setupDownloadUrl))
            {
                return (null, _viewModel.CurrentLanguageText.UpdateAssetMissing);
            }

            if (string.IsNullOrWhiteSpace(checksumDownloadUrl))
            {
                return (null, _viewModel.CurrentLanguageText.UpdateChecksumMissing);
            }

            if (!IsTrustedUpdateDownloadUrl(setupDownloadUrl) ||
                !IsTrustedUpdateDownloadUrl(checksumDownloadUrl))
            {
                return (null, _viewModel.CurrentLanguageText.UpdateSourceNotTrusted);
            }

            return (new UpdateReleaseAssetInfo(latestVersion, setupDownloadUrl, checksumDownloadUrl), null);
        }
        catch
        {
            return (null, _viewModel.CurrentLanguageText.UpdateReleaseReadFailed);
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

            var latestVersion = NormalizeReleaseVersion(tagNameElement.GetString());
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
                    !IsTrustedUpdateDownloadUrl(downloadUrl))
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

    private async Task DownloadUpdateAssetAsync(string downloadUrl, string destinationPath)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppDisplayName, GetDisplayVersion()));

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(true);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            if (totalBytes is > UpdateMaxInstallerBytes)
            {
                throw new InvalidOperationException(_viewModel.CurrentLanguageText.UpdateVerificationFailed);
            }

            var downloadedBytes = 0L;

            await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(true);
                if (read <= 0)
                {
                    break;
                }

                await target.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(true);
                downloadedBytes += read;
                if (downloadedBytes > UpdateMaxInstallerBytes)
                {
                    throw new InvalidOperationException(_viewModel.CurrentLanguageText.UpdateVerificationFailed);
                }

                if (totalBytes is > 0)
                {
                    var percent = Math.Clamp(downloadedBytes * 100d / totalBytes.Value, 0d, 100d);
                    var message = string.Format(
                        _viewModel.CurrentLanguageText.UpdateDownloadingFormat,
                        Math.Round(percent, 0));
                    SetUpdateProgressUi(message, percent, isIndeterminate: false);
                }
                else
                {
                    SetUpdateProgressUi(_viewModel.CurrentLanguageText.UpdateDownloading, 0, isIndeterminate: true);
                }
            }
        }
        catch
        {
            TryDeleteFile(destinationPath);
            throw;
        }
    }

    private async Task DownloadDs5DongleFirmwareAsync(string downloadUrl, string destinationPath)
    {
        try
        {
            if (!IsTrustedUpdateDownloadUrl(downloadUrl))
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

    private async Task<string> DownloadUpdateChecksumAssetAsync(string downloadUrl)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppDisplayName, GetDisplayVersion()));

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(true);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > UpdateMaxChecksumBytes)
        {
            throw new InvalidOperationException(_viewModel.CurrentLanguageText.UpdateVerificationFailed);
        }

        await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
        using var memory = new MemoryStream();
        var buffer = new byte[2048];

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(true);
            if (read <= 0)
            {
                break;
            }

            await memory.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(true);
            if (memory.Length > UpdateMaxChecksumBytes)
            {
                throw new InvalidOperationException(_viewModel.CurrentLanguageText.UpdateVerificationFailed);
            }
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static bool TryExtractSha256Hash(string checksumContent, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(checksumContent))
        {
            return false;
        }

        foreach (var rawLine in checksumContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var hashCandidate = line;
            var separator = line.IndexOfAny([' ', '\t']);
            if (separator > 0)
            {
                hashCandidate = line[..separator];
            }

            if (hashCandidate.Length != 64)
            {
                continue;
            }

            var allHex = true;
            for (var i = 0; i < hashCandidate.Length; i++)
            {
                var ch = hashCandidate[i];
                var isHex = (ch >= '0' && ch <= '9') ||
                            (ch >= 'a' && ch <= 'f') ||
                            (ch >= 'A' && ch <= 'F');
                if (!isHex)
                {
                    allHex = false;
                    break;
                }
            }

            if (!allHex)
            {
                continue;
            }

            hash = hashCandidate.ToUpperInvariant();
            return true;
        }

        return false;
    }

    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static bool IsTrustedUpdateDownloadUrl(string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var trustedHost in UpdateTrustedDownloadHosts)
        {
            if (string.Equals(uri.Host, trustedHost, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static bool IsRemoteVersionNewer(string currentVersionText, string remoteVersionText)
    {
        if (TryParseComparableVersion(currentVersionText, out var currentVersion) &&
            TryParseComparableVersion(remoteVersionText, out var remoteVersion))
        {
            return remoteVersion > currentVersion;
        }

        return !string.Equals(
            NormalizeReleaseVersion(currentVersionText),
            NormalizeReleaseVersion(remoteVersionText),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDs5DongleFirmwareUpdateNeeded(string currentVersionText, string latestVersionText)
    {
        if (TryParseComparableVersion(currentVersionText, out var currentVersion) &&
            TryParseComparableVersion(latestVersionText, out var latestVersion))
        {
            var numericComparison = latestVersion.CompareTo(currentVersion);
            if (numericComparison != 0)
            {
                return numericComparison > 0;
            }
        }

        return !string.Equals(
            NormalizeReleaseVersion(currentVersionText),
            NormalizeReleaseVersion(latestVersionText),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseComparableVersion(string? rawVersion, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return false;
        }

        var normalized = NormalizeReleaseVersion(rawVersion);
        var separatorIndex = normalized.IndexOfAny(['-', '+']);
        if (separatorIndex >= 0)
        {
            normalized = normalized[..separatorIndex];
        }

        var parsed = Version.TryParse(normalized, out var parsedVersion);
        version = parsedVersion ?? new Version(0, 0, 0, 0);
        return parsed;
    }

    private static string NormalizeReleaseVersion(string? rawVersion)
    {
        var text = rawVersion?.Trim() ?? string.Empty;
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        return text.Trim();
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

    private void StartInstallerUpdateAndRestart(string setupPath)
    {
        if (!File.Exists(setupPath))
        {
            throw new FileNotFoundException("Downloaded setup file was not found.", setupPath);
        }

        var currentProcessPath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(currentProcessPath))
        {
            currentProcessPath = Path.Combine(AppContext.BaseDirectory, "Bloss.exe");
        }

        if (string.IsNullOrWhiteSpace(currentProcessPath) || !File.Exists(currentProcessPath))
        {
            throw new FileNotFoundException(_viewModel.CurrentLanguageText.UpdateInstallLaunchFailed);
        }

        var scriptPath = Path.Combine(Path.GetTempPath(), $"bloss-updater-{Guid.NewGuid():N}.ps1");
        var escapedSetupPath = EscapePowerShellSingleQuotedString(setupPath);
        var escapedAppPath = EscapePowerShellSingleQuotedString(currentProcessPath);
        var currentPid = Environment.ProcessId;

        var scriptBuilder = new StringBuilder();
        scriptBuilder.AppendLine($"$setupPath = '{escapedSetupPath}'");
        scriptBuilder.AppendLine($"$appPath = '{escapedAppPath}'");
        scriptBuilder.AppendLine($"$oldPid = {currentPid}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("while (Get-Process -Id $oldPid -ErrorAction SilentlyContinue) {");
        scriptBuilder.AppendLine("    Start-Sleep -Milliseconds 600");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("Start-Process -FilePath $setupPath -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("if (Test-Path -LiteralPath $appPath) {");
        scriptBuilder.AppendLine("    Start-Process -FilePath $appPath");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue");
        var scriptContent = scriptBuilder.ToString();

        File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static string EscapePowerShellSingleQuotedString(string path)
    {
        return path.Replace("'", "''");
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
            _steamRawInputMonitor.Start(_windowMessageSource.Handle);
        }

        UpdateCompactMode();
        UpdateGlassCardClip();
        UpdateDwmBlurBehindRegion();
    }

    private IntPtr MainWindow_WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        return _steamRawInputMonitor.HandleWindowMessage(hwnd, msg, wParam, lParam, ref handled);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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
                UpdateColorEditorState();
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
                    UpdateColorEditorState();
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
        QueueColorPresetMarqueeReset(sender as WpfControls.TextBlock);
    }

    private void ColorPresetMarqueeText_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueColorPresetMarqueeReset(sender as WpfControls.TextBlock);
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

    private static void QueueColorPresetMarqueeUpdate(WpfControls.TextBlock? textBlock)
    {
        if (textBlock is null || textBlock.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        textBlock.Dispatcher.BeginInvoke(
            () =>
            {
                try
                {
                    UpdateColorPresetMarquee(textBlock);
                }
                catch (InvalidOperationException)
                {
                    ResetColorPresetMarqueeTransform(textBlock);
                }
            },
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void QueueColorPresetMarqueeReset(WpfControls.TextBlock? textBlock)
    {
        if (textBlock is null || textBlock.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        textBlock.Dispatcher.BeginInvoke(
            () =>
            {
                try
                {
                    ResetColorPresetMarqueeTransform(textBlock);
                }
                catch (InvalidOperationException)
                {
                    // Decorative text reset should never terminate the widget.
                }
            },
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void UpdateColorPresetMarquee(WpfControls.TextBlock textBlock)
    {
        var transform = textBlock.RenderTransform as TranslateTransform;
        if (transform is null || transform.IsFrozen)
        {
            transform = ResetColorPresetMarqueeTransform(textBlock);
        }
        else
        {
            try
            {
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                transform.X = 0d;
            }
            catch (InvalidOperationException)
            {
                transform = ResetColorPresetMarqueeTransform(textBlock);
            }
        }

        var viewport = FindColorPresetMarqueeViewport(textBlock);
        if (viewport is null ||
            viewport.ActualWidth <= 1d ||
            textBlock.ActualWidth <= viewport.ActualWidth + 1d)
        {
            return;
        }

        var overflow = Math.Ceiling(textBlock.ActualWidth - viewport.ActualWidth + ColorPresetMarqueeGap);
        var scrollSeconds = Math.Clamp(
            overflow / ColorPresetMarqueePixelsPerSecond,
            2.4d,
            8.0d);
        var totalSeconds = ColorPresetMarqueeStartDelaySeconds + scrollSeconds + ColorPresetMarqueeEndDelaySeconds;

        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(totalSeconds),
            RepeatBehavior = RepeatBehavior.Forever
        };

        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(ColorPresetMarqueeStartDelaySeconds))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(ColorPresetMarqueeStartDelaySeconds + scrollSeconds))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(totalSeconds))));

        try
        {
            transform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
        catch (InvalidOperationException)
        {
            ResetColorPresetMarqueeTransform(textBlock);
        }
    }

    private static TranslateTransform ResetColorPresetMarqueeTransform(WpfControls.TextBlock textBlock)
    {
        var transform = new TranslateTransform();
        textBlock.RenderTransform = transform;
        transform.X = 0d;
        return transform;
    }

    private static FrameworkElement? FindColorPresetMarqueeViewport(DependencyObject source)
    {
        var current = GetDependencyParent(source);
        while (current is not null)
        {
            if (current is FrameworkElement { Name: "ColorPresetMarqueeViewport" } viewport)
            {
                return viewport;
            }

            current = GetDependencyParent(current);
        }

        return null;
    }

    private static WpfControls.TextBlock? FindColorPresetMarqueeText(DependencyObject source)
    {
        if (source is WpfControls.TextBlock { Name: "ColorPresetMarqueeText" } textBlock)
        {
            return textBlock;
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

    private void SyncGuideSoundSelection()
    {
        if (GuideSoundComboBox is null)
        {
            return;
        }

        var options = BatteryGuideSoundCatalog.GetGuideOptions(_viewModel.CustomGuideSoundPath);
        var selected = BatteryGuideSoundCatalog.ResolveGuideSound(
            _viewModel.GuideSoundId,
            _viewModel.CustomGuideSoundPath);
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
        if (IsSettingsGlyphText(textBlock))
        {
            return;
        }

        _settingsTextBlockDefaults.TryAdd(textBlock, (textBlock.FontSize, textBlock.FontWeight));
        textBlock.FontSize = fontSize;
        textBlock.FontWeight = fontWeight;
    }

    private void ApplySettingsControlTextStyle(WpfControls.Control control, double fontSize, FontWeight fontWeight)
    {
        if (control is WpfControls.Slider or WpfControls.ProgressBar or WpfThumb or WpfToggleButton)
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
        var saturation = 0.76d;
        var lightness = 0.90d - (vertical * 0.62d);
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

    private Forms.NotifyIcon BuildTrayIcon()
    {
        var contextMenu = new Forms.ContextMenuStrip();
        _trayOpenMenuItem = new Forms.ToolStripMenuItem(_viewModel.TextTrayOpenWidget);
        _trayOpenMenuItem.Click += (_, _) => Dispatcher.Invoke(ShowWidgetFromTray);
        contextMenu.Items.Add(_trayOpenMenuItem);

        _trayRefreshMenuItem = new Forms.ToolStripMenuItem(_viewModel.TextTrayRefreshNow);
        _trayRefreshMenuItem.Click += (_, _) => Dispatcher.Invoke(() => _ = _viewModel.RefreshAsync());
        contextMenu.Items.Add(_trayRefreshMenuItem);

        _trayResetPositionMenuItem = new Forms.ToolStripMenuItem(_viewModel.TextTrayResetPosition);
        _trayResetPositionMenuItem.Click += (_, _) => Dispatcher.Invoke(ResetWidgetPositionToCurrentMonitor);
        contextMenu.Items.Add(_trayResetPositionMenuItem);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayAutostartMenuItem = new Forms.ToolStripMenuItem(_viewModel.TextAutostart);
        _trayAutostartMenuItem.Click += (_, _) => Dispatcher.Invoke(ToggleAutostartFromTray);
        contextMenu.Items.Add(_trayAutostartMenuItem);

        _trayStartMinimizedToTrayMenuItem = new Forms.ToolStripMenuItem(_viewModel.TextStartMinimizedToTray);
        _trayStartMinimizedToTrayMenuItem.Click += (_, _) => Dispatcher.Invoke(ToggleStartMinimizedToTrayFromTray);
        contextMenu.Items.Add(_trayStartMinimizedToTrayMenuItem);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayExitMenuItem = new Forms.ToolStripMenuItem(_viewModel.TextTrayExit);
        _trayExitMenuItem.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        contextMenu.Items.Add(_trayExitMenuItem);

        var trayIcon = new Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = AppDisplayName,
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWidgetFromTray);
        return trayIcon;
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
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var workingArea = screen.WorkingArea;
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
        try
        {
            _trayIcon.Visible = false;
        }
        catch
        {
            // Ignore tray visibility failures during shutdown.
        }

        try
        {
            _trayIcon.Dispose();
        }
        catch
        {
            // Ignore tray dispose failures during shutdown.
        }
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
        if (_trayOpenMenuItem is not null)
        {
            _trayOpenMenuItem.Text = _viewModel.TextTrayOpenWidget;
        }

        if (_trayRefreshMenuItem is not null)
        {
            _trayRefreshMenuItem.Text = _viewModel.TextTrayRefreshNow;
        }

        if (_trayResetPositionMenuItem is not null)
        {
            _trayResetPositionMenuItem.Text = _viewModel.TextTrayResetPosition;
        }

        if (_trayAutostartMenuItem is not null)
        {
            _trayAutostartMenuItem.Text = _viewModel.TextAutostart;
            _trayAutostartMenuItem.Checked = _viewModel.AutostartEnabled;
        }

        if (_trayStartMinimizedToTrayMenuItem is not null)
        {
            _trayStartMinimizedToTrayMenuItem.Text = _viewModel.TextStartMinimizedToTray;
            _trayStartMinimizedToTrayMenuItem.Checked = _viewModel.StartMinimizedToTrayEnabled;
        }

        if (_trayExitMenuItem is not null)
        {
            _trayExitMenuItem.Text = _viewModel.TextTrayExit;
        }
    }

    private static string GetDisplayVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+');
            return plusIndex > 0 ? informational[..plusIndex] : informational;
        }

        var version = assembly.GetName().Version;
        if (version is not null)
        {
            return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }

        return FallbackVersion;
    }

    private void GuideButtonMonitor_GuideButtonPressed(object? sender, GuideButtonPressedEventArgs e)
    {
        try
        {
            if (ShouldSuppressSecondarySteamGuideButtonPath(sender, e))
            {
                return;
            }

            if (ShouldSuppressDuplicateGuideButtonToast(e))
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => ShowBatteryGuide(e)));
        }
        catch
        {
            // Ignore shutdown races while the background monitor is stopping.
        }
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
                    "Secondary Steam HID monitor event was ignored because the current Steam-button sequence looks like a hold.");
                return true;
            }

            if (_pendingSteamSecondaryGuideFallbackByDevice.ContainsKey(key))
            {
                CancelPendingSteamSecondaryGuideFallbackLocked(key);
                _steamSecondaryGuideFallbackBlockedUntilByDevice[key] = now + SteamSecondaryFallbackBurstWindow;
                GuideButtonEventLog.Write(
                    "secondary_press_suppressed",
                    e.DeviceKind.ToString(),
                    e.Address,
                    e.DisplayName,
                    "Secondary Steam HID monitor event was ignored because repeated fallback events look like a hold.");
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
        try
        {
            await Task.Delay(SteamSecondaryFallbackDelay, cts.Token).ConfigureAwait(false);
        }
        catch
        {
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

            var now = DateTimeOffset.Now;
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
        }

        if (ShouldSuppressDuplicateGuideButtonToast(e))
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

    private static string BuildGuideButtonToastKey(GuideButtonPressedEventArgs e)
    {
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
                _lowBatteryToastKeys.Remove(BuildBatteryToastKey(item));
            }
        }

        if (e.NewItems is not null)
        {
            foreach (DeviceItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += DeviceItem_PropertyChanged;
                CheckLowBatteryToast(item);
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
        var key = BuildBatteryToastKey(item);
        if (!item.IsConnected || item.IsStale || item.IsBatteryConnecting || item.BatteryPercent is not int percent)
        {
            _lowBatteryToastKeys.Remove(key);
            return;
        }

        if (percent >= 30)
        {
            _lowBatteryToastKeys.Remove(key);
            return;
        }

        if (!_lowBatteryToastKeys.Add(key))
        {
            return;
        }

        ShowBatteryToast(item.Snapshot, automatic: true);
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
                (device.ModelKey?.Contains("VID_054C", StringComparison.OrdinalIgnoreCase) ?? false)),
            GuideButtonDeviceKind.SteamController => _viewModel.Devices.FirstOrDefault(device =>
                device.DisplayName.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) ||
                device.DisplayName.Contains("Steam Ctrl", StringComparison.OrdinalIgnoreCase) ||
                (device.ModelKey?.Contains("VID_28DE", StringComparison.OrdinalIgnoreCase) ?? false)),
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
               (device.ModelKey?.Contains("VID_28DE", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string BuildMissingGuideDeviceMessage(GuideButtonPressedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(e.DisplayName)
            ? "Controller"
            : e.DisplayName.Trim();

        return string.Equals(_viewModel.Language, "ko", StringComparison.OrdinalIgnoreCase)
            ? $"{name}: 배터리 정보를 아직 화면 목록에서 찾지 못했습니다."
            : $"{name}: battery info is not in the list yet.";
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
        try
        {
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = message;
            _trayIcon.BalloonTipIcon = icon;
            _trayIcon.ShowBalloonTip(2500);
        }
        catch
        {
            // Ignore tray notification failures.
        }
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

    private sealed record UpdateReleaseAssetInfo(
        string Version,
        string SetupDownloadUrl,
        string ChecksumDownloadUrl);

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
