using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfBitmapImage = System.Windows.Media.Imaging.BitmapImage;
using WpfBitmapSource = System.Windows.Media.Imaging.BitmapSource;
using WpfFormatConvertedBitmap = System.Windows.Media.Imaging.FormatConvertedBitmap;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseButtonState = System.Windows.Input.MouseButtonState;
using WpfPixelFormats = System.Windows.Media.PixelFormats;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace BluetoothBatteryWidget.App;

public partial class BatteryGuideTriggerCaptureWindow : Window
{
    private const int WmClose = 0x0010;
    private const int WmSysCommand = 0x0112;
    private const int ScClose = 0xF060;
    private const int SysCommandMask = 0xFFF0;
    private const string ConfiguredProfileTabTag = "Configured";
    private const string SteamProfileTabTag = "Steam";
    private const string SteamConfiguredProfileTabTag = "SteamConfigured";

    private static readonly string[] OriginalBlueprintImageFileNames =
    [
        "controller-guide-blueprint.png",
        "controller-guide-blueprint.jpg",
        "controller-guide-blueprint.jpeg",
        "battery-guide-trigger-blueprint.png",
        "battery-guide-trigger-blueprint.jpg",
        "battery-guide-trigger-blueprint.jpeg"
    ];

    private static readonly WpfBrush DefaultFill = CreateFrozenBrush(WpfColor.FromArgb(0x00, 0x00, 0x00, 0x00));
    private static readonly WpfBrush DefaultBorder = CreateFrozenBrush(WpfColor.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
    private static readonly WpfBrush HighlightFill = CreateFrozenBrush(WpfColor.FromArgb(0xE6, 0x1E, 0x78, 0xFF));
    private static readonly WpfBrush HighlightBorder = CreateFrozenBrush(WpfColor.FromArgb(0xFF, 0x9D, 0xC3, 0xFF));
    private static readonly WpfColor CaptureNeutralThemeColor = WpfColor.FromRgb(0x12, 0x16, 0x1C);
    private static readonly WpfColor CaptureBlueThemeColor = WpfColor.FromRgb(0x22, 0x58, 0xC8);
    private static readonly WpfColor CaptureRedThemeColor = WpfColor.FromRgb(0x8A, 0x12, 0x2A);
    private static readonly WpfColor CaptureGreenThemeColor = WpfColor.FromRgb(0x12, 0x74, 0x48);

    private readonly Dictionary<string, Border> _buttonSurfaces;
    private string _language;
    private BatteryGuideTrigger? _currentCandidate;
    private IReadOnlyDictionary<string, string> _profileTriggers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private string _legacyTrigger = string.Empty;
    private WpfBitmapSource? _originalBlueprintSource;
    private WpfColor _currentBlueprintLineColor;
    private bool _isClosingWithPopOut;
    private bool _allowClose;
    private HwndSource? _windowMessageSource;

    public BatteryGuideTriggerCaptureWindow(string? language = null)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        InitializeComponent();
        ApplyLocalizedText(_language);
        TryLoadOriginalBlueprintImage();
        _buttonSurfaces = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = AKey,
            ["B"] = BKey,
            ["X"] = XKey,
            ["Y"] = YKey,
            ["LB"] = LBKey,
            ["RB"] = RBKey,
            ["LT"] = LTKey,
            ["RT"] = RTKey,
            ["Guide"] = GuideKey,
            ["QuickAccess"] = QuickAccessKey,
            ["Mic"] = QuickAccessKey,
            ["View"] = ViewKey,
            ["Menu"] = MenuKey,
            ["Up"] = UpKey,
            ["Down"] = DownKey,
            ["Left"] = LeftKey,
            ["Right"] = RightKey,
            ["LeftPad"] = LeftPadKey,
            ["RightPad"] = RightPadKey
        };

        SetCandidate(null);
    }

    public event EventHandler? SaveRequested;

    public event EventHandler? RetryRequested;

    public event EventHandler? CancelRequested;

    internal System.Windows.Point? PopInOriginScreenPoint { get; set; }

    internal void ApplyLocalizedText(string? language)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        Title = UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCaptureTitle");
        PromptTextBlock.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCapturePrompt");
        CustomTabItem.Header = GetLocalizedCustomTabText(_language);
        PlayStationTabItem.Header = "PS";
        SteamControllerTabItem.Header = "STEAMCON";
        SaveButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCaptureSave");
        RetryButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCaptureRetry");
        CancelButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCaptureCancel");

        if (_currentCandidate is null)
        {
            CandidateTextBlock.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCaptureWaiting");
        }

        RefreshProfileSummaryText();
        UpdateVisibleTabContent();
    }

    internal void SetCandidate(BatteryGuideTrigger? trigger)
    {
        _currentCandidate = trigger;
        if (trigger is null)
        {
            CandidateTextBlock.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCaptureWaiting");
            SaveButton.IsEnabled = false;
            UpdateVisibleTabContent();
            return;
        }

        if (!CustomTabItem.IsSelected)
        {
            CustomTabItem.IsSelected = true;
        }

        CandidateTextBlock.Text = trigger.DisplayName;
        SaveButton.IsEnabled = true;
        UpdateVisibleTabContent();
    }

    internal void SetProfiles(IReadOnlyDictionary<string, string>? profiles, string? legacyTrigger)
    {
        _profileTriggers = profiles is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(profiles, StringComparer.OrdinalIgnoreCase);
        _legacyTrigger = legacyTrigger ?? string.Empty;
        RefreshProfileSummaryText();
    }

    private void RefreshProfileSummaryText()
    {
        UpdateVisibleTabContent();
    }

    private void CaptureTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, CaptureTabControl))
        {
            return;
        }

        UpdateVisibleTabContent();
        var selectedTab = GetSelectedCaptureTabItem();
        BeginCaptureThemeWaterEffect(GetCaptureThemeColor(selectedTab), selectedTab);
    }

    private void CaptureTabControl_PreviewMouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        if (FindAncestorTabItem(e.OriginalSource as DependencyObject) is not { } tabItem)
        {
            return;
        }

        UpdateConfiguredProfileTabTags();
        if (tabItem.IsSelected)
        {
            BeginCaptureThemeWaterEffect(GetCaptureThemeColor(tabItem), tabItem);
        }
    }

    private void UpdateVisibleTabContent()
    {
        if (CandidateTextBlock is null ||
            ProfileDetailTextBlock is null ||
            SaveButton is null ||
            RetryButton is null)
        {
            return;
        }

        UpdateConfiguredProfileTabTags();
        ResetButtonSurfaces();
        ApplyCaptureThemeTint(GetCaptureThemeColor(GetSelectedCaptureTabItem()), animate: false);

        var selectedProfileKind = GetSelectedProfileDeviceKind();
        if (selectedProfileKind is null)
        {
            CandidateTextBlock.Visibility = Visibility.Visible;
            ProfileDetailTextBlock.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            RetryButton.Visibility = Visibility.Visible;
            CandidateTextBlock.Text = _currentCandidate?.DisplayName ??
                                      UiLanguageCatalog.GetExtraText(_language, "BatteryGuideCaptureWaiting");
            SaveButton.IsEnabled = _currentCandidate is not null;
            ApplyTriggerHighlight(_currentCandidate);
            return;
        }

        CandidateTextBlock.Visibility = Visibility.Collapsed;
        ProfileDetailTextBlock.Visibility = Visibility.Visible;
        SaveButton.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        var profileKey = GetProfileKey(selectedProfileKind.Value);
        ProfileDetailTextBlock.Text = BuildProfileLine(selectedProfileKind.Value, profileKey);
        if (TryResolveProfileTrigger(selectedProfileKind.Value, profileKey, out var trigger))
        {
            ApplyTriggerHighlight(trigger);
        }
    }

    private string BuildProfileLine(GuideButtonDeviceKind deviceKind, string profileKey)
    {
        return $"{GetProfileDisplayName(deviceKind)}: {ResolveProfileTriggerDisplayName(deviceKind, profileKey)}";
    }

    private string ResolveProfileTriggerDisplayName(GuideButtonDeviceKind deviceKind, string profileKey)
    {
        if (TryResolveProfileTrigger(deviceKind, profileKey, out var trigger))
        {
            return trigger.DisplayName;
        }

        return GetLocalizedDefaultGuideButtonText(_language);
    }

    private bool TryResolveProfileTrigger(
        GuideButtonDeviceKind deviceKind,
        string profileKey,
        out BatteryGuideTrigger trigger)
    {
        if (_profileTriggers.TryGetValue(profileKey, out var persistedTrigger) &&
            BatteryGuideTriggerParser.TryParse(persistedTrigger, out trigger) &&
            trigger.DeviceKind == deviceKind)
        {
            return true;
        }

        if (BatteryGuideTriggerParser.TryParse(_legacyTrigger, out var legacyTrigger) &&
            legacyTrigger.DeviceKind == deviceKind)
        {
            trigger = legacyTrigger;
            return true;
        }

        trigger = null!;
        return false;
    }

    private void UpdateConfiguredProfileTabTags()
    {
        ApplyConfiguredProfileTabTag(PlayStationTabItem, GuideButtonDeviceKind.DualSense);
        ApplyConfiguredProfileTabTag(SteamControllerTabItem, GuideButtonDeviceKind.SteamController);
    }

    private void ApplyConfiguredProfileTabTag(TabItem? tabItem, GuideButtonDeviceKind deviceKind)
    {
        if (tabItem is null)
        {
            return;
        }

        var isConfigured = TryResolveProfileTrigger(deviceKind, GetProfileKey(deviceKind), out _);
        if (deviceKind == GuideButtonDeviceKind.SteamController)
        {
            tabItem.Tag = isConfigured
                ? SteamConfiguredProfileTabTag
                : SteamProfileTabTag;
            return;
        }

        tabItem.Tag = isConfigured ? ConfiguredProfileTabTag : null;
    }

    private TabItem? GetSelectedCaptureTabItem()
    {
        return CaptureTabControl?.SelectedItem as TabItem;
    }

    private WpfColor GetCaptureThemeColor(TabItem? tabItem)
    {
        if (ReferenceEquals(tabItem, CustomTabItem))
        {
            return CaptureNeutralThemeColor;
        }

        if (IsSteamCaptureTab(tabItem))
        {
            return CaptureGreenThemeColor;
        }

        return string.Equals(tabItem?.Tag as string, ConfiguredProfileTabTag, StringComparison.OrdinalIgnoreCase)
            ? CaptureRedThemeColor
            : CaptureBlueThemeColor;
    }

    private bool IsSteamCaptureTab(TabItem? tabItem)
    {
        var tag = tabItem?.Tag as string;
        return ReferenceEquals(tabItem, SteamControllerTabItem) ||
               string.Equals(tag, SteamProfileTabTag, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tag, SteamConfiguredProfileTabTag, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyCaptureThemeTint(WpfColor color, bool animate)
    {
        if (CaptureThemeBaseTintBrush is null || CaptureThemeBaseTint is null)
        {
            return;
        }

        var opacity = GetCaptureThemeBaseOpacity(color);
        var blueprintOpacity = 0d;
        var surfaceColor = CreateCaptureSurfaceBaseColor(color);
        var blueprintLineColor = CreateCaptureBlueprintLineColor(color);
        if (animate)
        {
            WindowSurfaceBackgroundBrush?.BeginAnimation(
                WpfSolidColorBrush.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation
                {
                    To = surfaceColor,
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                });
            CaptureThemeBaseTintBrush.BeginAnimation(
                WpfSolidColorBrush.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation
                {
                    To = color,
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                });
            if (CaptureThemeFloodWashBrush is not null)
            {
                CaptureThemeFloodWashBrush.BeginAnimation(
                    WpfSolidColorBrush.ColorProperty,
                    new System.Windows.Media.Animation.ColorAnimation
                    {
                        To = color,
                        Duration = TimeSpan.FromMilliseconds(180),
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                        }
                    });
            }

            if (BlueprintThemeWashBrush is not null)
            {
                BlueprintThemeWashBrush.BeginAnimation(
                    WpfSolidColorBrush.ColorProperty,
                    new System.Windows.Media.Animation.ColorAnimation
                    {
                        To = color,
                        Duration = TimeSpan.FromMilliseconds(260),
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                        }
                    });
            }

            CaptureThemeBaseTint.BeginAnimation(
                OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = opacity,
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                });
            ApplyBlueprintLineTheme(blueprintLineColor);
            BlueprintThemeWash?.BeginAnimation(
                OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = blueprintOpacity,
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                });
            return;
        }

        if (WindowSurfaceBackgroundBrush is not null)
        {
            WindowSurfaceBackgroundBrush.Color = surfaceColor;
        }

        CaptureThemeBaseTintBrush.Color = color;
        CaptureThemeBaseTint.Opacity = opacity;
        ApplyBlueprintLineTheme(blueprintLineColor);

        if (BlueprintThemeWashBrush is not null)
        {
            BlueprintThemeWashBrush.Color = color;
        }

        if (CaptureThemeFloodWashBrush is not null)
        {
            CaptureThemeFloodWashBrush.Color = color;
        }

        if (BlueprintThemeWash is not null)
        {
            BlueprintThemeWash.Opacity = blueprintOpacity;
        }
    }

    private void BeginCaptureThemeWaterEffect(WpfColor color, TabItem? originTab)
    {
        if (CaptureThemeRipple is null ||
            CaptureThemeRippleScale is null ||
            CaptureThemeRippleCanvas is null)
        {
            return;
        }

        ApplyCaptureThemeTint(color, animate: true);
        BeginCaptureThemeFloodWash(color);
        CaptureThemeRipple.Fill = CreateCaptureThemeRippleBrush(color);
        var origin = GetThemeRippleOrigin(originTab, CaptureThemeRippleCanvas);
        BeginCaptureThemeRipple(
            CaptureThemeRippleCanvas,
            CaptureThemeRipple,
            CaptureThemeRippleScale,
            origin,
            GetCaptureThemeRippleOpacity(color, surfaceLayer: false),
            3.45d,
            1080);

        if (CaptureThemeSurfaceRipple is not null &&
            CaptureThemeSurfaceRippleScale is not null &&
            CaptureThemeSurfaceRippleCanvas is not null)
        {
            CaptureThemeSurfaceRipple.Fill = CreateCaptureThemeRippleBrush(color);
            BeginCaptureThemeRipple(
                CaptureThemeSurfaceRippleCanvas,
                CaptureThemeSurfaceRipple,
                CaptureThemeSurfaceRippleScale,
                GetThemeRippleOrigin(originTab, CaptureThemeSurfaceRippleCanvas),
                GetCaptureThemeRippleOpacity(color, surfaceLayer: true),
                4.65d,
                1160);
        }
    }

    private void BeginCaptureThemeFloodWash(WpfColor color)
    {
        if (CaptureThemeFloodWash is null || CaptureThemeFloodWashBrush is null)
        {
            return;
        }

        CaptureThemeFloodWashBrush.Color = color;
        var peakOpacity = GetCaptureThemeFloodPeakOpacity(color);
        var keyFrames = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(1050)
        };
        keyFrames.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            0d,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
        keyFrames.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            peakOpacity,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        });
        keyFrames.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            GetCaptureThemeFloodRestOpacity(color),
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(760)))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        });
        keyFrames.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            0d,
            System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050)))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        });

        CaptureThemeFloodWash.BeginAnimation(OpacityProperty, keyFrames);
    }

    private static void BeginCaptureThemeRipple(
        Canvas canvas,
        FrameworkElement ripple,
        System.Windows.Media.ScaleTransform rippleScale,
        System.Windows.Point origin,
        double opacityFrom,
        double scaleMultiplier,
        int durationMilliseconds)
    {
        Canvas.SetLeft(ripple, origin.X - ripple.Width / 2d);
        Canvas.SetTop(ripple, origin.Y - ripple.Height / 2d);

        var scaleTo = Math.Max(
            1.15d,
            Math.Max(canvas.ActualWidth, canvas.ActualHeight) /
            Math.Max(1d, ripple.Width) * scaleMultiplier);
        var easing = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };

        rippleScale.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.035d, scaleTo, TimeSpan.FromMilliseconds(durationMilliseconds))
            {
                EasingFunction = easing
            });
        rippleScale.BeginAnimation(
            System.Windows.Media.ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0.035d, scaleTo, TimeSpan.FromMilliseconds(durationMilliseconds))
            {
                EasingFunction = easing
            });
        ripple.BeginAnimation(
            OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(opacityFrom, 0d, TimeSpan.FromMilliseconds(durationMilliseconds))
            {
                EasingFunction = easing
            });
    }

    private static System.Windows.Point GetThemeRippleOrigin(TabItem? originTab, FrameworkElement relativeTo)
    {
        if (originTab is not null &&
            originTab.ActualWidth > 0d &&
            originTab.ActualHeight > 0d)
        {
            try
            {
                return originTab.TranslatePoint(
                    new System.Windows.Point(originTab.ActualWidth / 2d, originTab.ActualHeight / 2d),
                    relativeTo);
            }
            catch (InvalidOperationException)
            {
                // Fall back to the top center while layout is still settling.
            }
        }

        return new System.Windows.Point(
            Math.Max(1d, relativeTo.ActualWidth) / 2d,
            72d);
    }

    private static WpfBrush CreateCaptureThemeRippleBrush(WpfColor color)
    {
        return new System.Windows.Media.RadialGradientBrush(
            new System.Windows.Media.GradientStopCollection
            {
                new(WithAlpha(color, 0xE4), 0d),
                new(WithAlpha(color, 0xA8), 0.38d),
                new(WithAlpha(color, 0x4C), 0.72d),
                new(WithAlpha(color, 0x00), 1d)
            });
    }

    private static WpfColor CreateCaptureSurfaceBaseColor(WpfColor color)
    {
        if (IsNeutralCaptureTheme(color))
        {
            return WpfColor.FromRgb(0x0B, 0x0E, 0x12);
        }

        if (IsGreenCaptureTheme(color))
        {
            return WpfColor.FromRgb(0x06, 0x35, 0x25);
        }

        return IsRedCaptureTheme(color)
            ? WpfColor.FromRgb(0x54, 0x05, 0x16)
            : WpfColor.FromRgb(0x0F, 0x2A, 0x5C);
    }

    private static WpfColor CreateCaptureBlueprintLineColor(WpfColor color)
    {
        if (IsNeutralCaptureTheme(color))
        {
            return WpfColor.FromRgb(0xE7, 0xEC, 0xF4);
        }

        if (IsGreenCaptureTheme(color))
        {
            return WpfColor.FromRgb(0xB8, 0xFF, 0xD6);
        }

        return IsRedCaptureTheme(color)
            ? WpfColor.FromRgb(0xFF, 0xB4, 0xC1)
            : WpfColor.FromRgb(0xD7, 0xE8, 0xFF);
    }

    private static double GetCaptureThemeBaseOpacity(WpfColor color)
    {
        if (IsNeutralCaptureTheme(color))
        {
            return 0.18d;
        }

        if (IsGreenCaptureTheme(color))
        {
            return 0.36d;
        }

        return IsRedCaptureTheme(color) ? 0.38d : 0.32d;
    }

    private static double GetCaptureThemeRippleOpacity(WpfColor color, bool surfaceLayer)
    {
        if (IsNeutralCaptureTheme(color))
        {
            return surfaceLayer ? 0.34d : 0.48d;
        }

        if (IsGreenCaptureTheme(color))
        {
            return surfaceLayer ? 0.66d : 0.92d;
        }

        return IsRedCaptureTheme(color)
            ? surfaceLayer ? 0.70d : 0.98d
            : surfaceLayer ? 0.60d : 0.86d;
    }

    private static double GetCaptureThemeFloodPeakOpacity(WpfColor color)
    {
        if (IsNeutralCaptureTheme(color))
        {
            return 0.30d;
        }

        if (IsGreenCaptureTheme(color))
        {
            return 0.58d;
        }

        return IsRedCaptureTheme(color) ? 0.62d : 0.52d;
    }

    private static double GetCaptureThemeFloodRestOpacity(WpfColor color)
    {
        if (IsNeutralCaptureTheme(color))
        {
            return 0.08d;
        }

        if (IsGreenCaptureTheme(color))
        {
            return 0.16d;
        }

        return IsRedCaptureTheme(color) ? 0.18d : 0.14d;
    }

    private static WpfColor WithAlpha(WpfColor color, byte alpha)
    {
        return WpfColor.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static bool IsRedCaptureTheme(WpfColor color)
    {
        return color.R > color.B;
    }

    private static bool IsNeutralCaptureTheme(WpfColor color)
    {
        return color.Equals(CaptureNeutralThemeColor);
    }

    private static bool IsGreenCaptureTheme(WpfColor color)
    {
        return color.G > color.R && color.G > color.B;
    }

    private static TabItem? FindAncestorTabItem(DependencyObject? source)
    {
        for (var current = source; current is not null; current = GetVisualOrLogicalParent(current))
        {
            if (current is TabItem tabItem)
            {
                return tabItem;
            }
        }

        return null;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject source)
    {
        var visualParent = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(source)
            : null;

        return visualParent ?? LogicalTreeHelper.GetParent(source);
    }

    private static string GetProfileDisplayName(GuideButtonDeviceKind deviceKind)
    {
        return deviceKind switch
        {
            GuideButtonDeviceKind.DualSense => "DualSense",
            GuideButtonDeviceKind.SteamController => "Steam Controller",
            _ => deviceKind.ToString()
        };
    }

    private GuideButtonDeviceKind? GetSelectedProfileDeviceKind()
    {
        if (PlayStationTabItem?.IsSelected == true)
        {
            return GuideButtonDeviceKind.DualSense;
        }

        if (SteamControllerTabItem?.IsSelected == true)
        {
            return GuideButtonDeviceKind.SteamController;
        }

        return null;
    }

    private static string GetProfileKey(GuideButtonDeviceKind deviceKind)
    {
        return deviceKind switch
        {
            GuideButtonDeviceKind.DualSense => WidgetSettings.DualSenseBatteryGuideProfileKey,
            GuideButtonDeviceKind.SteamController => WidgetSettings.SteamControllerBatteryGuideProfileKey,
            _ => string.Empty
        };
    }

    private static string GetLocalizedCustomTabText(string? language)
    {
        return WidgetSettings.NormalizeLanguage(language) switch
        {
            WidgetSettings.KoreanLanguage => "사용자정의",
            WidgetSettings.JapaneseLanguage => "カスタム",
            WidgetSettings.ChineseSimplifiedLanguage => "自定义",
            WidgetSettings.ChineseTraditionalLanguage => "自訂",
            WidgetSettings.LatinLanguage => "Custom",
            WidgetSettings.FrenchLanguage => "Personnalisé",
            _ => "Custom"
        };
    }

    private static string GetLocalizedDefaultGuideButtonText(string? language)
    {
        return WidgetSettings.NormalizeLanguage(language) switch
        {
            WidgetSettings.KoreanLanguage => "기본 가이드 버튼",
            WidgetSettings.JapaneseLanguage => "既定のガイドボタン",
            WidgetSettings.ChineseSimplifiedLanguage => "默认指南按钮",
            WidgetSettings.ChineseTraditionalLanguage => "預設指南按鈕",
            WidgetSettings.LatinLanguage => "Bulla ducis praevalens",
            WidgetSettings.FrenchLanguage => "Bouton guide par défaut",
            _ => "Default guide button"
        };
    }

    private void TryLoadOriginalBlueprintImage()
    {
        foreach (var path in GetOriginalBlueprintImageCandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var image = new WpfBitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                UseOriginalBlueprintImage(image);
                return;
            }
            catch
            {
                // Fall back to the built-in vector blueprint if the optional image cannot be loaded.
            }
        }

        TryLoadEmbeddedOriginalBlueprintImage();
    }

    private void TryLoadEmbeddedOriginalBlueprintImage()
    {
        try
        {
            var assemblyName = typeof(BatteryGuideTriggerCaptureWindow).Assembly.GetName().Name;
            var image = new WpfBitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(
                $"pack://application:,,,/{assemblyName};component/Assets/controller-guide-blueprint.png",
                UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            UseOriginalBlueprintImage(image);
        }
        catch
        {
            UseFallbackVectorBlueprint();
        }
    }

    private void UseOriginalBlueprintImage(WpfBitmapSource image)
    {
        _originalBlueprintSource = image;
        _currentBlueprintLineColor = default;

        OriginalBlueprintImage.Source = image;
        OriginalBlueprintImage.Opacity = 0d;
        OriginalBlueprintImage.Visibility = Visibility.Visible;
        BlueprintImageLineLayer.Visibility = Visibility.Visible;
        VectorBlueprintLayer.Visibility = Visibility.Collapsed;

        ApplyBlueprintLineTheme(CreateCaptureBlueprintLineColor(GetCaptureThemeColor(GetSelectedCaptureTabItem())));
    }

    private void UseFallbackVectorBlueprint()
    {
        _originalBlueprintSource = null;
        OriginalBlueprintImage.Visibility = Visibility.Collapsed;
        BlueprintImageLineLayer.Source = null;
        BlueprintImageLineLayer.Visibility = Visibility.Collapsed;
        VectorBlueprintLayer.Visibility = Visibility.Visible;
    }

    private void ApplyBlueprintLineTheme(WpfColor lineColor)
    {
        if (_originalBlueprintSource is null || BlueprintImageLineLayer is null)
        {
            return;
        }

        if (_currentBlueprintLineColor.Equals(lineColor) && BlueprintImageLineLayer.Source is not null)
        {
            return;
        }

        _currentBlueprintLineColor = lineColor;
        BlueprintImageLineLayer.Source = CreateTransparentBlueprintLineBitmap(_originalBlueprintSource, lineColor);
    }

    private static WpfBitmapSource CreateTransparentBlueprintLineBitmap(WpfBitmapSource source, WpfColor lineColor)
    {
        var converted = new WpfFormatConvertedBitmap(source, WpfPixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var blue = pixels[i];
            var green = pixels[i + 1];
            var red = pixels[i + 2];
            var alpha = pixels[i + 3];
            var luminance = ((red * 299d) + (green * 587d) + (blue * 114d)) / 1000d;
            var visibleLine = Math.Clamp((luminance * alpha / 255d - 8d) / 247d, 0d, 1d);
            var boostedLine = Math.Pow(visibleLine, 0.72d);
            var lineAlpha = (byte)Math.Clamp(boostedLine * 245d, 0d, 245d);

            pixels[i] = lineColor.B;
            pixels[i + 1] = lineColor.G;
            pixels[i + 2] = lineColor.R;
            pixels[i + 3] = lineAlpha < 4 ? (byte)0 : lineAlpha;
        }

        var transparentBlueprint = WpfBitmapSource.Create(
            width,
            height,
            source.DpiX,
            source.DpiY,
            WpfPixelFormats.Bgra32,
            null,
            pixels,
            stride);
        transparentBlueprint.Freeze();
        return transparentBlueprint;
    }

    internal static IReadOnlyList<string> GetOriginalBlueprintImageCandidatePaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return OriginalBlueprintImageFileNames
            .SelectMany(fileName => new[]
            {
                Path.Combine(baseDirectory, fileName),
                Path.Combine(baseDirectory, "Assets", fileName)
            })
            .ToArray();
    }

    private static WpfBrush CreateFrozenBrush(WpfColor color)
    {
        var brush = new WpfSolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void ResetButtonSurfaces()
    {
        if (_buttonSurfaces is null)
        {
            return;
        }

        foreach (var surface in _buttonSurfaces.Values)
        {
            surface.Background = DefaultFill;
            surface.BorderBrush = DefaultBorder;
        }
    }

    private void ApplyTriggerHighlight(BatteryGuideTrigger? trigger)
    {
        if (trigger is null || _buttonSurfaces is null)
        {
            return;
        }

        foreach (var key in BatteryGuideTriggerParser.GetVisualButtonKeys(trigger))
        {
            if (_buttonSurfaces.TryGetValue(key, out var surface))
            {
                surface.Background = HighlightFill;
                surface.BorderBrush = HighlightBorder;
            }
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ClipWindowSurfaceCorners();
        WindowPopInAnimator.Begin(
            this,
            WindowSurface,
            WindowSurfaceScale,
            WindowSurfaceSkew,
            WindowSurfaceTranslate,
            PopInOriginScreenPoint);
        Focus();
    }

    private void WindowSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClipWindowSurfaceCorners();
    }

    private void ClipWindowSurfaceCorners()
    {
        if (WindowSurface is null ||
            WindowSurface.ActualWidth <= 0d ||
            WindowSurface.ActualHeight <= 0d)
        {
            return;
        }

        var radius = Math.Max(0d, WindowSurface.CornerRadius.TopLeft);
        WindowSurface.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0d, 0d, WindowSurface.ActualWidth, WindowSurface.ActualHeight),
            radius,
            radius);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowMessageSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowMessageSource?.AddHook(WindowMessageHook);
    }

    protected override void OnClosed(EventArgs e)
    {
        _windowMessageSource?.RemoveHook(WindowMessageHook);
        _windowMessageSource = null;
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    internal void CloseFromOwner()
    {
        _allowClose = true;
        Close();
    }

    internal void CloseWithPopOut()
    {
        if (_isClosingWithPopOut)
        {
            return;
        }

        _isClosingWithPopOut = true;
        WindowPopInAnimator.BeginClose(
            this,
            WindowSurface,
            WindowSurfaceScale,
            WindowSurfaceSkew,
            WindowSurfaceTranslate,
            PopInOriginScreenPoint,
            CloseFromOwner);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_allowClose &&
            (msg == WmClose ||
             (msg == WmSysCommand &&
              ((wParam.ToInt64() & SysCommandMask) == ScClose))))
        {
            handled = true;
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private void Window_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;
    }

    private void HeaderDragArea_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        if (e.ButtonState != WpfMouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The drag gesture may be interrupted if the window is closing.
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
