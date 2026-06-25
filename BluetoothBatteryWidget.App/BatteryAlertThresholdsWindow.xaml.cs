using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace BluetoothBatteryWidget.App;

public partial class BatteryAlertThresholdsWindow : Window
{
    private string _language;
    private bool _isClosingWithPopOut;
    private readonly IReadOnlyList<BatteryAlertDeviceOption> _deviceOptions;

    public BatteryAlertThresholdsWindow(
        string currentThresholds,
        IEnumerable<BatteryAlertDeviceOption>? deviceOptions = null,
        string? language = null)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        _deviceOptions = deviceOptions?.ToArray() ?? [];
        InitializeComponent();
        ApplyLocalizedText(_language);
        BuildThresholdOptions(currentThresholds);
        BuildDeviceOptions();
    }

    public string SelectedThresholds { get; private set; } = string.Empty;

    public IReadOnlyDictionary<string, bool> SelectedDeviceAlertSettings { get; private set; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    public bool WasAccepted { get; private set; }

    internal System.Windows.Point? PopInOriginScreenPoint { get; set; }

    internal static IReadOnlyList<int> SelectableThresholds { get; } = [30, 40, 50, 60, 70, 80];

    internal void ApplyLocalizedText(string? language)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        Title = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsWindowTitle");
        HeadingTextBlock.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsHeading");
        DescriptionLine1Run.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsDescriptionLine1");
        DescriptionLine2Run.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsDescriptionLine2");
        ForcedThresholdCheckBox.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsForced");
        DeviceSectionHeadingTextBlock.Text = GetDeviceSectionHeadingText(_language);
        ClearButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsClear");
        SaveButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsSave");
        CancelButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsCancel");
    }

    private void BuildThresholdOptions(string currentThresholds)
    {
        var selected = WidgetSettings.GetBatteryAlertThresholdPercents(currentThresholds).ToHashSet();
        foreach (var threshold in SelectableThresholds)
        {
            var checkBox = new WpfToggleButton
            {
                Content = $"{threshold}%",
                Tag = threshold,
                IsChecked = selected.Contains(threshold),
                Style = (Style)FindResource("AlertChipToggleButtonStyle")
            };
            checkBox.Click += ThresholdToggle_Click;
            ThresholdPanel.Children.Add(checkBox);
        }
    }

    private void BuildDeviceOptions()
    {
        DeviceAlertPanel.Children.Clear();
        if (_deviceOptions.Count == 0)
        {
            DeviceAlertPanel.Children.Add(new TextBlock
            {
                Text = GetNoDeviceText(_language),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xA8, 0xD2, 0xE4, 0xF5)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 2, 2, 0)
            });
            return;
        }

        foreach (var option in _deviceOptions)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Margin = new Thickness(0, 0, 12, 0)
            };
            textPanel.Children.Add(new TextBlock
            {
                Text = option.DisplayName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (!string.IsNullOrWhiteSpace(option.DetailText))
            {
                textPanel.Children.Add(new TextBlock
                {
                    Text = option.DetailText,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x9E, 0xD2, 0xE4, 0xF5)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            var toggle = new WpfToggleButton
            {
                Tag = option.Key,
                IsChecked = option.IsEnabled,
                Style = (Style)FindResource("AlertDeviceToggleButtonStyle"),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(textPanel, 0);
            Grid.SetColumn(toggle, 1);
            row.Children.Add(textPanel);
            row.Children.Add(toggle);
            DeviceAlertPanel.Children.Add(row);
        }
    }

    private void ThresholdToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfToggleButton toggleButton)
        {
            return;
        }

        try
        {
            toggleButton.ApplyTemplate();
            if (toggleButton.Template.FindName("ChipClickPulse", toggleButton) is not FrameworkElement pulse ||
                toggleButton.Template.FindName("ChipClickPulseScale", toggleButton) is not ScaleTransform pulseScale ||
                toggleButton.Template.FindName("ChipRootScale", toggleButton) is not ScaleTransform rootScale)
            {
                return;
            }

            var isChecked = toggleButton.IsChecked == true;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var pulseDuration = TimeSpan.FromMilliseconds(isChecked ? 560 : 390);
            var pulseScaleAnimation = new DoubleAnimation(0.82d, isChecked ? 1.24d : 1.12d, pulseDuration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            var opacityAnimation = new DoubleAnimation(
                isChecked ? 0.96d : 0.72d,
                0d,
                pulseDuration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            var rootScaleAnimation = new DoubleAnimation(0.945d, 1d, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };

            pulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            pulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            pulse.BeginAnimation(OpacityProperty, null);
            pulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseScaleAnimation);
            pulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseScaleAnimation.Clone());
            rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, rootScaleAnimation);
            rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, rootScaleAnimation.Clone());
            pulse.BeginAnimation(OpacityProperty, opacityAnimation);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // The selection itself is more important than the optional click animation.
        }
    }

    private IReadOnlyList<int> GetSelectedThresholds()
    {
        return ThresholdPanel.Children
            .OfType<WpfToggleButton>()
            .Where(checkBox => checkBox.IsChecked == true && checkBox.Tag is int)
            .Select(checkBox => (int)checkBox.Tag)
            .OrderBy(threshold => threshold)
            .ToArray();
    }

    private IReadOnlyDictionary<string, bool> GetSelectedDeviceAlertSettings()
    {
        return DeviceAlertPanel.Children
            .OfType<Grid>()
            .SelectMany(row => row.Children.OfType<WpfToggleButton>())
            .Where(toggle => toggle.Tag is string key && !string.IsNullOrWhiteSpace(key))
            .ToDictionary(
                toggle => (string)toggle.Tag,
                toggle => toggle.IsChecked == true,
                StringComparer.OrdinalIgnoreCase);
    }

    private void HeaderDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Drag can be interrupted while the modal window is closing.
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowPopInAnimator.Begin(
            this,
            WindowSurface,
            WindowSurfaceScale,
            WindowSurfaceSkew,
            WindowSurfaceTranslate,
            PopInOriginScreenPoint);
    }

    internal void CloseWithPopOut()
    {
        if (_isClosingWithPopOut)
        {
            return;
        }

        _isClosingWithPopOut = true;
        WasAccepted = false;
        WindowPopInAnimator.BeginClose(
            this,
            WindowSurface,
            WindowSurfaceScale,
            WindowSurfaceSkew,
            WindowSurfaceTranslate,
            PopInOriginScreenPoint,
            Close);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var checkBox in ThresholdPanel.Children.OfType<WpfToggleButton>())
        {
            checkBox.IsChecked = false;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedThresholds = WidgetSettings.NormalizeBatteryAlertThresholds(string.Join(", ", GetSelectedThresholds()));
        SelectedDeviceAlertSettings = GetSelectedDeviceAlertSettings();
        CloseWithResult(accepted: true);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(accepted: false);
    }

    private void CloseWithResult(bool accepted)
    {
        WasAccepted = accepted;
        try
        {
            DialogResult = accepted;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }

    private static string GetDeviceSectionHeadingText(string language)
    {
        return WidgetSettings.NormalizeLanguage(language) switch
        {
            WidgetSettings.KoreanLanguage => "자동알림 받을 기기",
            WidgetSettings.JapaneseLanguage => "通知するデバイス",
            WidgetSettings.ChineseSimplifiedLanguage => "接收自动通知的设备",
            WidgetSettings.ChineseTraditionalLanguage => "接收自動通知的裝置",
            WidgetSettings.FrenchLanguage => "Appareils avec alerte",
            WidgetSettings.LatinLanguage => "Instrumenta nuntianda",
            _ => "Alert devices"
        };
    }

    private static string GetNoDeviceText(string language)
    {
        return WidgetSettings.NormalizeLanguage(language) switch
        {
            WidgetSettings.KoreanLanguage => "현재 자동알림을 설정할 수 있는 연결 기기가 없습니다.",
            WidgetSettings.JapaneseLanguage => "現在、通知を設定できる接続デバイスはありません。",
            WidgetSettings.ChineseSimplifiedLanguage => "当前没有可设置自动通知的已连接设备。",
            WidgetSettings.ChineseTraditionalLanguage => "目前沒有可設定自動通知的已連接裝置。",
            WidgetSettings.FrenchLanguage => "Aucun appareil connecté ne peut recevoir une alerte.",
            WidgetSettings.LatinLanguage => "Nullum instrumentum coniunctum nuntiari potest.",
            _ => "No connected alert-capable device is available."
        };
    }
}

public sealed record BatteryAlertDeviceOption(
    string Key,
    string DisplayName,
    string DetailText,
    bool IsEnabled);
