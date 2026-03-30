using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    private const double UiScaleStepFactor = 0.08d;
    private const int UiScaleAnimationMilliseconds = 140;
    private const double ResizeGripBaseInset = 4d;
    private const double StatusPanelFallbackHeight = 108d;
    private const string GlassWaveStoryboardKey = "GlassWaveStoryboard";
    private const string DeveloperContactEmail = "lamsaiku65@gmail.com";
    private const string SupportUrl = "https://ko-fi.com/dukduk";
    private const string AppDisplayName = "Bloss";
    private const string FallbackVersion = "1.0.3";
    private const string UpdateLatestReleaseApiUrl = "https://api.github.com/repos/samueltoken/Bloss_battery_indicator/releases/latest";
    private const string UpdateExpectedAssetName = "setup.exe";
    private const string UpdateExpectedChecksumAssetName = "setup.exe.sha256";
    private const string UpdateExpectedSignerName = "samueltoken";
    private const long UpdateMaxInstallerBytes = 250L * 1024 * 1024;
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

    private readonly MainViewModel _viewModel;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly DrawingIcon _appIcon;

    private bool _isExiting;
    private bool _forceClose;
    private DeviceItemViewModel? _renamingItem;
    private DeviceItemViewModel? _iconEditingItem;
    private bool _isCompactMode;
    private bool _initialBoundsApplied;
    private bool _isColorPresetSyncing;
    private bool _isLanguageSyncing;
    private double _statusPanelCollapsedHeightDelta;
    private DateTime _lastBoundsSaveAt = DateTime.MinValue;
    private Storyboard? _glassWaveStoryboard;
    private Forms.ToolStripMenuItem? _trayOpenMenuItem;
    private Forms.ToolStripMenuItem? _trayRefreshMenuItem;
    private Forms.ToolStripMenuItem? _trayExitMenuItem;
    private readonly HttpClient _httpClient = new();
    private bool _isUpdating;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        InitializeComponent();
        RefreshColorPresetOptions();
        LanguageComboBox.ItemsSource = _viewModel.LanguageOptions;
        LanguageComboBox.DisplayMemberPath = nameof(UiLanguageOption.Label);
        _appIcon = LoadAppIcon();
        _trayIcon = BuildTrayIcon();
        RefreshTrayMenuTexts();
        UpdateVersionMenuHeader();
        ResetUpdateProgressUi();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        LocationChanged += (_, _) => SaveBoundsThrottled();
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
        SyncColorPresetSelection();
        SyncLanguageSelection();
        ApplyUiScaleStep(_viewModel.UiScaleStep, animate: false);
        RefreshTrayMenuTexts();
        UpdateVersionMenuHeader();
        UpdateResizeGripPlacement();
        UpdateDwmBlurBehindRegion();
        ApplyVisualModeState();
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
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
        UpdateVersionMenuHeader();
    }

    private void StatusPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var collapse = !_viewModel.StatusPanelCollapsed;
        if (collapse)
        {
            SettingsPopup.IsOpen = false;
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

    private void AggressivePolicyToggle_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetThirdPartyBatteryPolicy(ThirdPartyBatteryPolicy.Aggressive);
    }

    private void AggressivePolicyToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.SetThirdPartyBatteryPolicy(ThirdPartyBatteryPolicy.Hybrid);
    }

    private void ColorPresetComboBox_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (_isColorPresetSyncing || ColorPresetComboBox.SelectedItem is not ColorPresetOption selected)
        {
            return;
        }

        _viewModel.SetColorPreset(selected.Id);
        ApplyColorPreset(selected.Id);
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

            if (!IsInstallerSignatureTrusted(setupPath, UpdateExpectedSignerName))
            {
                TryDeleteFile(setupPath);
                throw new InvalidOperationException(_viewModel.CurrentLanguageText.UpdateSignatureFailed);
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

    private static bool IsInstallerSignatureTrusted(string installerPath, string expectedSignerName)
    {
        try
        {
            using var signerCertificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(installerPath));
            if (signerCertificate.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
                signerCertificate.NotAfter.ToUniversalTime() < DateTime.UtcNow)
            {
                return false;
            }

            if (!signerCertificate.Subject.Contains(expectedSignerName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.VerificationTime = DateTime.UtcNow;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(15);

            return chain.Build(signerCertificate);
        }
        catch
        {
            return false;
        }
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

        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
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

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyToolWindowStyle();
        EnableDwmBlurBehindWindow();
        UpdateCompactMode();
        UpdateGlassCardClip();
        UpdateDwmBlurBehindRegion();
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

        if (e.PropertyName is nameof(MainViewModel.Language))
        {
            if (Dispatcher.CheckAccess())
            {
                RefreshColorPresetOptions();
                SyncColorPresetSelection();
                SyncLanguageSelection();
                RefreshTrayMenuTexts();
                UpdateVersionMenuHeader();
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshColorPresetOptions();
                    SyncColorPresetSelection();
                    SyncLanguageSelection();
                    RefreshTrayMenuTexts();
                    UpdateVersionMenuHeader();
                });
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.StatusPanelCollapsed))
        {
            if (_viewModel.StatusPanelCollapsed)
            {
                SettingsPopup.IsOpen = false;
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.UiScaleStep))
        {
            ApplyUiScaleStep(_viewModel.UiScaleStep);
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
        var selected = (ColorPresetComboBox.ItemsSource as IEnumerable<ColorPresetOption>)
            ?.FirstOrDefault(preset => string.Equals(preset.Id, normalized, StringComparison.Ordinal));
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

    private void RefreshColorPresetOptions()
    {
        if (ColorPresetComboBox is null)
        {
            return;
        }

        var options = ColorPresetCatalog.GetLocalizedOptions(_viewModel.Language);
        _isColorPresetSyncing = true;
        try
        {
            ColorPresetComboBox.ItemsSource = options;
            ColorPresetComboBox.DisplayMemberPath = nameof(ColorPresetOption.Label);
        }
        finally
        {
            _isColorPresetSyncing = false;
        }
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
        SetResourceColor("PrimaryTextBrush", preset.PrimaryText);
        SetResourceColor("SecondaryTextBrush", preset.SecondaryText);
        SetResourceColor("BatteryTextBrush", preset.BatteryText);
        SetResourceColor("CardTintBrush", EnhanceThemeColor(preset.CardTint, 1.28d, 1.15d, 1.08d));
        SetResourceColor("CardBorderBrush", EnhanceThemeColor(preset.CardBorder, 1.34d, 1.12d, 1.10d));
        SetResourceColor("TrackBrush", EnhanceThemeColor(preset.Track, 1.30d, 1.12d, 1.10d));
        SetResourceColor("IconBackBrush", EnhanceThemeColor(preset.IconBack, 1.20d, 1.08d, 1.05d));
        SetResourceColor("IconBorderBrush", EnhanceThemeColor(preset.IconBorder, 1.24d, 1.10d, 1.08d));
        SetResourceColor("ActionButtonBackBrush", EnsureMinimumLuminance(
            EnhanceThemeColor(preset.ActionButtonBack, 1.22d, 1.12d, 1.10d),
            0.56d));
        SetResourceColor("ActionButtonBorderBrush", EnsureMinimumLuminance(
            EnhanceThemeColor(preset.ActionButtonBorder, 1.26d, 1.10d, 1.10d),
            0.48d));

        if (ListTopStop is not null)
        {
            ListTopStop.Color = EnhanceThemeColor(preset.ListTop, 1.24d, 1.10d, 1.08d);
        }

        if (ListBottomStop is not null)
        {
            ListBottomStop.Color = EnhanceThemeColor(preset.ListBottom, 1.28d, 1.12d, 1.10d);
        }

        if (FooterTopStop is not null)
        {
            FooterTopStop.Color = EnhanceThemeColor(preset.FooterTop, 1.22d, 1.10d, 1.08d);
        }

        if (FooterBottomStop is not null)
        {
            FooterBottomStop.Color = EnhanceThemeColor(preset.FooterBottom, 1.26d, 1.12d, 1.10d);
        }
    }

    private void SetResourceColor(string key, System.Windows.Media.Color color)
    {
        if (TryFindResource(key) is not SolidColorBrush existing)
        {
            Resources[key] = new SolidColorBrush(color);
            return;
        }

        if (existing.IsFrozen || existing.IsSealed || existing.Dispatcher is null)
        {
            Resources[key] = new SolidColorBrush(color);
            return;
        }

        existing.Color = color;
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

    private void ShowWidgetFromTray()
    {
        if (IsVisible)
        {
            Activate();
            return;
        }

        Show();
        WindowState = WindowState.Normal;
        Activate();
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
                source is FrameworkElement frameworkElement && frameworkElement.Name == "DeviceIconHitArea")
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
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

            var extension = Path.GetExtension(trimmedPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var normalizedExtension = extension.ToLowerInvariant();
            var fileStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var targetPath = Path.Combine(CustomIconDirectory, $"{normalizedAddress}_{fileStamp}{normalizedExtension}");
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
