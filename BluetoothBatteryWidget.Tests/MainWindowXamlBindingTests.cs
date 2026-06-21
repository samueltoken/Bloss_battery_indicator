using BluetoothBatteryWidget.App;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace BluetoothBatteryWidget.Tests;

public sealed class MainWindowXamlBindingTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void MainWindowXaml_HexBrushTokensUseValidWpfLengths()
    {
        var xaml = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        var invalidTokens = Regex
            .Matches(xaml, "#[0-9A-Fa-f]+")
            .Select(match => match.Value)
            .Where(token =>
            {
                var hexLength = token.Length - 1;
                return hexLength is not (3 or 4 or 6 or 8);
            })
            .Distinct()
            .ToArray();

        Assert.Empty(invalidTokens);
    }

    [Fact]
    public void ReleaseNotesWindow_UsesCustomAnimatedBackgroundAndBlossUpdateText()
    {
        var xaml = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "ReleaseNotesWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "ReleaseNotesWindow.xaml.cs"));
        Assert.Contains("Title=\"Bloss 업데이트 안내\"", xaml);
        Assert.Contains("RoundedContentRoot", xaml);
        Assert.Contains("RectangleGeometry", code);
        Assert.Contains("AbstractSignalBackdrop", xaml);
        Assert.Contains("HeroBackdropScale", xaml);
        Assert.Contains("TileLayer", xaml);
        Assert.Contains("SoftSweepTranslate", xaml);
        Assert.Contains("BitmapCache", xaml);
        Assert.Contains("BrandCoreGlow", xaml);
        Assert.Contains("BrandRing", xaml);
        Assert.Contains("BLoss", xaml);
        Assert.Contains("업데이트 내역", xaml);
        Assert.DoesNotContain("이번 업데이트", xaml);
        Assert.Contains("절전모드/화면꺼짐 관련 구조개선", xaml);
        Assert.Contains("기타 자잘한 버그 수정", xaml);
        Assert.Contains("SetReleaseNoteText", code);
        Assert.Contains("ReleaseNoteConfirmButtonStyle", xaml);
        Assert.Contains("ReleaseNoteCloseButtonStyle", xaml);
        Assert.Contains("CornerRadius=\"13\"", xaml);
        Assert.Contains("CornerRadius=\"10\"", xaml);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", xaml);
        Assert.Contains("Text=\"&#xE711;\"", xaml);
        Assert.Contains("UiLanguageCatalog.GetExtraText(language, \"ReleaseNotesHeading\")", code);
        Assert.Contains("ReleaseNotesWindow(string version, string? language = null)", code);
        Assert.Contains("AppVersionInfo.DisplayVersion", code);
        Assert.DoesNotContain("Bloss 1.0.8", xaml);
        Assert.Equal("Update details", UiLanguageCatalog.GetExtraText(WidgetSettings.EnglishLanguage, "ReleaseNotesHeading"));
        Assert.Equal("업데이트 내역", UiLanguageCatalog.GetExtraText(WidgetSettings.KoreanLanguage, "ReleaseNotesHeading"));
        Assert.Contains("BuildQuietTiles", code);
        Assert.Contains("BeginAmbientAnimations", code);
        Assert.Contains("SoftSweepTranslate.BeginAnimation", code);
        Assert.Contains("CreateBreathingDoubleAnimation", code);
        Assert.Contains("RepeatBehavior.Forever", code);
        Assert.Contains("AutomationProperties.SetName", code);
        Assert.DoesNotContain("HeroBackdropScale.BeginAnimation", code);
        Assert.DoesNotContain("TileLayer.BeginAnimation", code);
        Assert.DoesNotContain("BrandPulseScale", xaml);
        Assert.DoesNotContain("BrandRingRotate", xaml);
        Assert.DoesNotContain("BeginTilePulse", code);
        Assert.DoesNotContain("DoubleAnimationUsingKeyFrames", code);
        Assert.DoesNotContain("release-notes-oracus", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORACUS", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "Assets",
            "release-notes-oracus.png")));
    }

    [Fact]
    public void SettingsComboBoxes_ScrollLongSelectedTitlesOnHover()
    {
        var xaml = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("x:Key=\"GlassScrollingComboBoxStyle\"", xaml);
        Assert.Contains("x:Name=\"ComboMarqueeViewport\"", xaml);
        Assert.Contains("x:Name=\"ComboMarqueeText\"", xaml);
        Assert.Contains("Text=\"{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}\"", xaml);
        Assert.Contains("Tag=\"{Binding SelectedItem.Label, RelativeSource={RelativeSource Self}}\"", xaml);
        Assert.Contains("Tag=\"{Binding SelectedItem.DisplayName, RelativeSource={RelativeSource Self}}\"", xaml);
        Assert.Contains("Padding\" Value=\"8,4,2,4\"", xaml);
        Assert.DoesNotContain("Padding\" Value=\"10,4,30,4\"", xaml);
        Assert.Contains("x:Name=\"PowerIdlePauseComboBox\"", xaml);
        Assert.Contains("x:Name=\"WindowsDisplayOffComboBox\"", xaml);
        Assert.Contains("x:Name=\"GuideSoundComboBox\"", xaml);
        Assert.Contains("x:Name=\"LanguageComboBox\"", xaml);
        Assert.Contains("HorizontalContentAlignment=\"Center\"", xaml);
        Assert.Equal(4, Regex.Matches(xaml, "Style=\"\\{StaticResource GlassScrollingComboBoxStyle\\}\"").Count);
        Assert.Equal(4, Regex.Matches(xaml, "MouseEnter=\"ComboBoxMarquee_MouseEnter\"").Count);
        Assert.Equal(4, Regex.Matches(xaml, "MouseLeave=\"ComboBoxMarquee_MouseLeave\"").Count);
        Assert.Contains("private void ComboBoxMarquee_MouseEnter", code);
        Assert.Contains("private void ComboBoxMarquee_MouseLeave", code);
        Assert.Contains("ComboMarqueeViewport", code);
        Assert.Contains("ComboMarqueeText", code);
        Assert.Contains("GetMarqueeRestingX", code);
        Assert.Contains("HorizontalContentAlignment == System.Windows.HorizontalAlignment.Center", code);
        Assert.Contains("Math.Max(", code);
        Assert.DoesNotContain("8.0d);", code);
    }

    [Fact]
    public void BatteryGuideProfileTabs_ShowConfiguredStateAndSavedButtonHighlight()
    {
        var xaml = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "BatteryGuideTriggerCaptureWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "BatteryGuideTriggerCaptureWindow.xaml.cs"));

        Assert.Contains("x:Name=\"ConfiguredFillLayer\"", xaml);
        Assert.Contains("CornerRadius=\"10\"", xaml);
        Assert.Contains("x:Name=\"TabPressScale\"", xaml);
        Assert.Contains("x:Name=\"WindowSurfaceBackgroundBrush\"", xaml);
        Assert.Contains("CornerRadius=\"14\"", xaml);
        Assert.Contains("SizeChanged=\"WindowSurface_SizeChanged\"", xaml);
        Assert.Contains("x:Name=\"WindowSurfaceRoot\"", xaml);
        Assert.Contains("x:Name=\"CaptureContentLayer\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeLayer\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeBaseTint\"", xaml);
        Assert.Contains("x:Name=\"BlueprintImageLineLayer\"", xaml);
        Assert.Contains("x:Name=\"BlueprintThemeWash\"", xaml);
        Assert.Contains("x:Name=\"BlueprintThemeWashBrush\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeRipple\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeRippleScale\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeSurfaceRippleCanvas\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", xaml);
        Assert.Contains("VerticalAlignment=\"Stretch\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeFloodWash\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeFloodWashBrush\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeSurfaceRipple\"", xaml);
        Assert.Contains("x:Name=\"CaptureThemeSurfaceRippleScale\"", xaml);
        Assert.Contains("PreviewMouseLeftButtonDown=\"CaptureTabControl_PreviewMouseLeftButtonDown\"", xaml);
        Assert.Contains("RoutedEvent=\"UIElement.PreviewMouseLeftButtonDown\"", xaml);
        Assert.Contains("RoutedEvent=\"UIElement.PreviewMouseLeftButtonUp\"", xaml);
        Assert.Contains("<Trigger Property=\"Tag\" Value=\"Configured\">", xaml);
        Assert.Contains("<Trigger Property=\"Tag\" Value=\"Steam\">", xaml);
        Assert.Contains("<Trigger Property=\"Tag\" Value=\"SteamConfigured\">", xaml);
        Assert.Contains("TargetName=\"WaterPulse\" Property=\"Fill\" Value=\"#A820D67A\"", xaml);
        Assert.Contains("TargetName=\"ConfiguredFillLayer\" Property=\"Opacity\" Value=\"1\"", xaml);
        Assert.Contains("TargetName=\"WaterPulse\" Property=\"Fill\" Value=\"#99FF4054\"", xaml);
        Assert.Contains("<MultiTrigger>", xaml);
        Assert.Contains("TargetName=\"TabChrome\" Property=\"Background\" Value=\"#A52258C8\"", xaml);
        Assert.Contains("TargetName=\"ConfiguredFillLayer\" Property=\"Background\" Value=\"#F0631426\"", xaml);
        Assert.DoesNotContain("x:Name=\"ConfiguredGlow\"", xaml);
        Assert.Contains("ConfiguredProfileTabTag", code);
        Assert.Contains("SteamProfileTabTag", code);
        Assert.Contains("SteamConfiguredProfileTabTag", code);
        Assert.Contains("CaptureBlueThemeColor", code);
        Assert.Contains("CaptureRedThemeColor", code);
        Assert.Contains("CaptureGreenThemeColor", code);
        Assert.Contains("BeginCaptureThemeWaterEffect", code);
        Assert.Contains("BeginCaptureThemeFloodWash", code);
        Assert.Contains("BeginCaptureThemeRipple", code);
        Assert.Contains("ApplyCaptureThemeTint", code);
        Assert.Contains("CreateCaptureThemeRippleBrush", code);
        Assert.Contains("CreateCaptureSurfaceBaseColor", code);
        Assert.Contains("CreateCaptureBlueprintLineColor", code);
        Assert.Contains("ClipWindowSurfaceCorners", code);
        Assert.Contains("RectangleGeometry", code);
        Assert.Contains("IsGreenCaptureTheme", code);
        Assert.Contains("UseOriginalBlueprintImage", code);
        Assert.Contains("ApplyBlueprintLineTheme", code);
        Assert.Contains("CreateTransparentBlueprintLineBitmap", code);
        Assert.Contains("WpfPixelFormats.Bgra32", code);
        Assert.Contains("0.38d", code);
        Assert.Contains("0.62d", code);
        Assert.Contains("4.65d", code);
        Assert.Contains("FindAncestorTabItem", code);
        Assert.Contains("UpdateConfiguredProfileTabTags", code);
        Assert.Contains("ApplyConfiguredProfileTabTag", code);
        Assert.Contains("TryResolveProfileTrigger", code);
        Assert.Contains("ApplyTriggerHighlight", code);
        Assert.Contains("CaptureNeutralThemeColor", code);
        Assert.Contains("ReferenceEquals(tabItem, CustomTabItem)", code);
        Assert.Contains("IsNeutralCaptureTheme", code);
        Assert.Contains("WpfColor.FromRgb(0x0B, 0x0E, 0x12)", code);
    }

    [Fact]
    public void SettingsLanguageSelection_LocalizesLabelsAndSecondaryIconWindows()
    {
        var appRoot = Path.Combine(ProjectRoot, "BluetoothBatteryWidget.App");
        var mainWindowXaml = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(appRoot, "ViewModels", "MainViewModel.cs"));
        var languageCatalogCode = File.ReadAllText(Path.Combine(appRoot, "Services", "UiLanguageCatalog.cs"));
        var iconOverrideXaml = File.ReadAllText(Path.Combine(appRoot, "IconOverrideWindow.xaml"));
        var iconOverrideCode = File.ReadAllText(Path.Combine(appRoot, "IconOverrideWindow.xaml.cs"));
        var iconImageAdjustXaml = File.ReadAllText(Path.Combine(appRoot, "IconImageAdjustWindow.xaml"));
        var iconImageAdjustCode = File.ReadAllText(Path.Combine(appRoot, "IconImageAdjustWindow.xaml.cs"));

        Assert.Contains("TextLanguage => CurrentLanguageText.LanguageLabel", viewModelCode);
        Assert.Contains("TextResizeTooltip => UiLanguageCatalog.GetExtraText(Settings.Language, \"ResizeTooltip\")", viewModelCode);
        Assert.Contains("OnPropertyChanged(nameof(TextResizeTooltip))", viewModelCode);
        Assert.Contains("Text=\"{Binding TextLanguage}\"", mainWindowXaml);
        Assert.Contains("ToolTip=\"{Binding TextResizeTooltip}\"", mainWindowXaml);
        Assert.DoesNotContain("Text=\"Language\"", mainWindowXaml);
        Assert.DoesNotContain("ToolTip=\"Resize\"", mainWindowXaml);

        Assert.Contains("new IconOverrideWindow(snapshots, existingOverrides, existingImageOverrides, _viewModel.Language)", mainWindowCode);
        Assert.Contains("ExtraText(\"BatteryGuideTriggerSavedToast\")", mainWindowCode);
        Assert.Contains("Filter = ExtraText(\"CustomGuideSoundFilter\")", mainWindowCode);
        Assert.Contains("Filter = ExtraText(\"CustomFontFilter\")", mainWindowCode);
        Assert.Contains("ExtraText(\"MissingGuideDeviceFallbackName\")", mainWindowCode);
        Assert.Contains("ExtraFormat(\"MissingGuideDeviceMessageFormat\", name)", mainWindowCode);
        Assert.DoesNotContain("알림버튼 사용자 키가 저장되었습니다.", mainWindowCode);
        Assert.DoesNotContain("Audio files (*.wav;*.mp3;*.wma;*.m4a)", mainWindowCode);
        Assert.DoesNotContain("Font files (*.ttf;*.otf)", mainWindowCode);
        Assert.Contains("string? language = null", iconOverrideCode);
        Assert.Contains("ApplyLocalizedText(_language)", iconOverrideCode);
        Assert.Contains("BuildIconChoices(_language)", iconOverrideCode);
        Assert.Contains("UiLanguageCatalog.GetExtraText(_language, \"IconOverrideImageSelectTitle\")", iconOverrideCode);
        Assert.Contains("new IconImageAdjustWindow(dialog.FileName, _language)", iconOverrideCode);
        Assert.Contains("Content=\"{Binding DataContext.TextIconOverrideChooseImage", iconOverrideXaml);
        Assert.Contains("Content=\"{Binding DataContext.TextIconOverrideClearImage", iconOverrideXaml);
        Assert.DoesNotContain("Title = \"아이콘 이미지 선택\"", iconOverrideCode);
        Assert.DoesNotContain("new IconImageAdjustWindow(dialog.FileName)", iconOverrideCode);

        Assert.Contains("IconImageAdjustWindow(string sourceImagePath, string? language = null)", iconImageAdjustCode);
        Assert.Contains("ApplyLocalizedText(_language)", iconImageAdjustCode);
        Assert.Contains("IconImageAdjustZoomFormat", iconImageAdjustCode);
        Assert.Contains("BuildZoomText()", iconImageAdjustCode);
        Assert.Contains("x:Name=\"HeadingTextBlock\"", iconImageAdjustXaml);
        Assert.Contains("x:Name=\"HintTextBlock\"", iconImageAdjustXaml);
        Assert.Contains("x:Name=\"GuideThicknessLabelTextBlock\"", iconImageAdjustXaml);
        Assert.Contains("x:Name=\"WarningTextBlock\"", iconImageAdjustXaml);

        Assert.Contains("\"IconOverrideWindowTitle\"", languageCatalogCode);
        Assert.Contains("\"IconImageAdjustWindowTitle\"", languageCatalogCode);
        Assert.Equal("언어", UiLanguageCatalog.Get(WidgetSettings.KoreanLanguage).LanguageLabel);
        Assert.Equal("Language", UiLanguageCatalog.Get(WidgetSettings.EnglishLanguage).LanguageLabel);
        Assert.Equal("크기 조절", UiLanguageCatalog.GetExtraText(WidgetSettings.KoreanLanguage, "ResizeTooltip"));
        Assert.Equal("Resize", UiLanguageCatalog.GetExtraText(WidgetSettings.EnglishLanguage, "ResizeTooltip"));
        Assert.Equal("スリープ保護", UiLanguageCatalog.GetExtraText(WidgetSettings.JapaneseLanguage, "PowerIdlePause"));
        Assert.Equal("設定文字", UiLanguageCatalog.GetExtraText(WidgetSettings.JapaneseLanguage, "ColorSettingsText"));
        Assert.Equal("Pico2W ファームウェア更新", UiLanguageCatalog.GetExtraText(WidgetSettings.JapaneseLanguage, "PicoFirmwareButton"));
        Assert.Equal("環境設定", UiLanguageCatalog.Get(WidgetSettings.JapaneseLanguage).EnvironmentSettingsGroup);
        Assert.Equal("通知音", UiLanguageCatalog.Get(WidgetSettings.JapaneseLanguage).GuideSoundLabel);
        Assert.Equal("更新", UiLanguageCatalog.Get(WidgetSettings.JapaneseLanguage).UpdateButton);
        Assert.Equal("自定义颜色", UiLanguageCatalog.Get(WidgetSettings.ChineseSimplifiedLanguage).ColorCustomizeButton);
        Assert.Equal("Son de notification", UiLanguageCatalog.Get(WidgetSettings.FrenchLanguage).GuideSoundLabel);
        Assert.Equal("Préécouter", UiLanguageCatalog.Get(WidgetSettings.FrenchLanguage).GuideSoundPreviewTooltip);
        Assert.Contains("音声ファイル", UiLanguageCatalog.GetExtraText(WidgetSettings.JapaneseLanguage, "CustomGuideSoundFilter"));
        Assert.Contains("Fichiers de police", UiLanguageCatalog.GetExtraText(WidgetSettings.FrenchLanguage, "CustomFontFilter"));
        Assert.Equal(
            "通知ボタンのユーザーキーを保存しました。",
            UiLanguageCatalog.GetExtraText(WidgetSettings.JapaneseLanguage, "BatteryGuideTriggerSavedToast"));
        Assert.Equal("Adjust icon image", UiLanguageCatalog.GetExtraText(WidgetSettings.EnglishLanguage, "IconImageAdjustWindowTitle"));
        Assert.Equal("아이콘 이미지 맞춤", UiLanguageCatalog.GetExtraText(WidgetSettings.KoreanLanguage, "IconImageAdjustWindowTitle"));
    }

    [Fact]
    public void ReleaseNotes_ShowOnceForReleaseButEveryRunForTestExe()
    {
        Assert.True(MainWindow.ShouldShowReleaseNotes("", "1.0.8", forceEveryRun: false));
        Assert.True(MainWindow.ShouldShowReleaseNotes(null, "1.0.8", forceEveryRun: false));
        Assert.True(MainWindow.ShouldShowReleaseNotes("1.0.7", "1.0.8", forceEveryRun: false));
        Assert.False(MainWindow.ShouldShowReleaseNotes("1.0.8", "1.0.8", forceEveryRun: false));
        Assert.False(MainWindow.ShouldShowReleaseNotes(" 1.0.8\r\n", "1.0.8", forceEveryRun: false));
        Assert.False(MainWindow.ShouldShowReleaseNotes("1.0.8", "", forceEveryRun: false));
        Assert.True(MainWindow.ShouldShowReleaseNotes("1.0.8", "1.0.8", forceEveryRun: true));
        Assert.True(MainWindow.ShouldShowReleaseNotes("1.0.8", "", forceEveryRun: true));
        Assert.True(MainWindow.IsPortableTestExecutablePath(@"C:\temp\test.exe"));
        Assert.True(MainWindow.IsPortableTestExecutablePath(@"C:\temp\TEST.EXE"));
        Assert.False(MainWindow.IsPortableTestExecutablePath(@"C:\temp\Bloss.exe"));
        Assert.True(AutostartService.IsPortableTestExecutablePath(@"C:\temp\test.exe"));
        Assert.True(AutostartService.IsPortableTestExecutablePath(@"C:\temp\TEST.EXE"));
        Assert.False(AutostartService.IsPortableTestExecutablePath(@"C:\temp\Bloss.exe"));

        var mainWindowCode = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));
        var releaseNotesBlock = Regex.Match(
            mainWindowCode,
            @"var releaseNotesWindow = new ReleaseNotesWindow\(version, _viewModel\.Language\);[\s\S]*?releaseNotesWindow\.ShowDialog\(\);",
            RegexOptions.CultureInvariant);

        Assert.True(releaseNotesBlock.Success);
        Assert.Contains("_viewModel.Language", releaseNotesBlock.Value);
        Assert.DoesNotContain("CenterOwner", releaseNotesBlock.Value);
    }

    [Fact]
    public void SecondaryWindows_UseSmoothPopInAnimationWithoutReferenceImage()
    {
        var animator = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "WindowPopInAnimator.cs"));
        var releaseNotesCode = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "ReleaseNotesWindow.xaml.cs"));
        var iconOverrideCode = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "IconOverrideWindow.xaml.cs"));
        var iconImageAdjustCode = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "IconImageAdjustWindow.xaml.cs"));
        var batteryAlertCode = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "BatteryAlertThresholdsWindow.xaml.cs"));
        var batteryAlertXaml = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "BatteryAlertThresholdsWindow.xaml"));
        var guideCaptureCode = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "BatteryGuideTriggerCaptureWindow.xaml.cs"));
        var guideCaptureXaml = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "BatteryGuideTriggerCaptureWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("GenieStartScaleX = 0.42d", animator);
        Assert.Contains("GenieStartScaleY = 0.24d", animator);
        Assert.Contains("SettleDuration = TimeSpan.FromMilliseconds(700)", animator);
        Assert.Contains("BeginClose(", animator);
        Assert.Contains("BeginCloseCentered(", animator);
        Assert.Contains("QuinticEase", animator);
        Assert.DoesNotContain("DoubleAnimationUsingKeyFrames", animator);
        Assert.DoesNotContain("AttachCentered", releaseNotesCode);
        Assert.Contains("AttachCentered", iconOverrideCode);
        Assert.Contains("CloseWithPopOutAsCancel", iconOverrideCode);
        Assert.Contains("AttachCentered", iconImageAdjustCode);
        Assert.Contains("WindowPopInAnimator.Begin(", batteryAlertCode);
        Assert.Contains("CloseWithPopOut", batteryAlertCode);
        Assert.Contains("WindowPopInAnimator.BeginClose(", batteryAlertCode);
        Assert.Contains("WindowPopInAnimator.Begin(", guideCaptureCode);
        Assert.Contains("CloseWithPopOut", guideCaptureCode);
        Assert.Contains("WindowPopInAnimator.BeginClose(", guideCaptureCode);
        Assert.Contains("_batteryAlertThresholdsWindow.CloseWithPopOut();", mainWindowCode);
        Assert.Contains("CancelBatteryGuideTriggerCapture(closeWindow: true, animateClose: true);", mainWindowCode);
        Assert.Contains("_iconOverrideWindow.CloseWithPopOutAsCancel();", mainWindowCode);
        Assert.DoesNotContain("dialog.ShowDialog() != true", mainWindowCode);
        Assert.Contains("Loaded=\"Window_Loaded\"", batteryAlertXaml);
        Assert.Contains("Loaded=\"Window_Loaded\"", guideCaptureXaml);
        foreach (var xaml in new[] { batteryAlertXaml, guideCaptureXaml })
        {
            Assert.Contains("x:Name=\"WindowSurface\"", xaml);
            Assert.Contains("x:Name=\"WindowSurfaceScale\"", xaml);
            Assert.Contains("x:Name=\"WindowSurfaceSkew\"", xaml);
            Assert.Contains("x:Name=\"WindowSurfaceTranslate\"", xaml);
            Assert.Contains("RenderTransformOrigin=\"0.5,0.5\"", xaml);
        }
    }

    [Fact]
    public void BatteryGuidePopup_UsesOneWayBindingForReadOnlyVisibility()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        Assert.Contains("IsOpen=\"{Binding IsBatteryGuideVisible, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void BatteryGuidePopup_UsesLargeReadableText()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        Assert.Contains("MinWidth=\"250\"", xaml);
        Assert.Contains("MaxWidth=\"390\"", xaml);
        Assert.Contains("FontSize=\"16\"", xaml);
    }

    [Fact]
    public void BatteryToast_UsesRuntimeSubtitleBuilder()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, _viewModel.Language, automatic)", source);
    }

    [Fact]
    public void SettingsPopup_SeparatesColorCustomizeAndGuideSoundRowsWithPreview()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        Assert.Contains("x:Name=\"GuideSoundToggle\"", xaml);
        Assert.Contains("x:Name=\"ColorCustomizeButton\"", xaml);
        Assert.Contains("x:Name=\"GuideSoundSelectorRowGrid\"", xaml);
        Assert.Contains("x:Name=\"GuideSoundSelectorLabelTextBlock\"", xaml);
        Assert.Contains("x:Name=\"GuideSoundComboBox\"", xaml);
        Assert.Contains("x:Name=\"PreviewGuideSoundButton\"", xaml);
        Assert.Contains("x:Name=\"CustomGuideSoundFileRowGrid\"", xaml);
        Assert.Contains("x:Name=\"LoadCustomGuideSoundButton\"", xaml);
        Assert.Contains("x:Name=\"ResetCustomGuideSoundButton\"", xaml);
        Assert.Contains("Click=\"PreviewGuideSoundButton_Click\"", xaml);
        Assert.Contains("Click=\"LoadCustomGuideSoundButton_Click\"", xaml);
        Assert.Contains("Click=\"ResetCustomGuideSoundButton_Click\"", xaml);
        Assert.Contains("Text=\"{Binding TextGuideSound}\"", xaml);
        Assert.DoesNotContain("x:Name=\"AppearanceAndSoundGrid\"", xaml);
        Assert.DoesNotContain("x:Name=\"LabsToggle\"", xaml);
    }

    [Fact]
    public void SettingsPopup_UsesInlineAccordionsInRequestedOrder()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        var environmentIndex = xaml.IndexOf("x:Name=\"EnvironmentSettingsGroupButton\"", StringComparison.Ordinal);
        var customizeIndex = xaml.IndexOf("x:Name=\"CustomizeSettingsGroupButton\"", StringComparison.Ordinal);
        var labsIndex = xaml.IndexOf("x:Name=\"LabsSettingsGroupButton\"", StringComparison.Ordinal);

        Assert.True(environmentIndex >= 0);
        Assert.True(customizeIndex > environmentIndex);
        Assert.True(labsIndex > customizeIndex);
        Assert.Contains("x:Name=\"EnvironmentAccordionBody\"", xaml);
        Assert.Contains("x:Name=\"CustomizeAccordionBody\"", xaml);
        Assert.Contains("x:Name=\"LabsAccordionBody\"", xaml);
        Assert.DoesNotContain("x:Name=\"EnvironmentSettingsPopup\"", xaml);
        Assert.DoesNotContain("x:Name=\"CustomizeSettingsPopup\"", xaml);
        Assert.DoesNotContain("x:Name=\"LabsSettingsPopup\"", xaml);
        Assert.Contains("x:Name=\"StartMinimizedToTrayToggle\"", xaml);
        Assert.Contains("MouseLeave=\"SettingsPopupArea_MouseLeave\"", xaml);
    }

    [Fact]
    public void TrayMenu_ProvidesPositionResetAndStartupTrayArgument()
    {
        var mainWindowSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));
        var appSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "App.xaml.cs"));
        var autostartSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "AutostartService.cs"));
        var trayIconServiceSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "TrayIconService.cs"));

        Assert.Contains("_resetPositionMenuItem", trayIconServiceSource);
        Assert.Contains("TrayIconService", mainWindowSource);
        Assert.Contains("ResetWidgetPositionToCurrentMonitor", mainWindowSource);
        Assert.Contains("CenterWindowInArea(GetWorkingAreaFromCurrentCursor())", mainWindowSource);
        Assert.Contains("Forms.Screen.FromPoint(Forms.Cursor.Position)", mainWindowSource);
        Assert.Contains("HasMeaningfulVisibleArea", mainWindowSource);
        Assert.Contains("PrepareStartHiddenInTray", mainWindowSource);
        Assert.Contains("--start-in-tray", appSource);
        Assert.Contains("--start-in-tray", autostartSource);
        Assert.Contains("IsPortableTestExecutablePath(Environment.ProcessPath)", autostartSource);
        Assert.Contains("return null;", autostartSource);
    }

    [Fact]
    public void SettingsPopup_UsesWideMenuAccordionAndCustomScaleSlider()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("PlacementTarget=\"{Binding ElementName=GlassCard}\"", xaml);
        Assert.Contains("Width=\"348\"", xaml);
        Assert.Contains("x:Key=\"SettingsMenuButtonStyle\"", xaml);
        Assert.Contains("Style=\"{StaticResource SettingsMenuButtonStyle}\"", xaml);
        Assert.Contains("x:Key=\"UiScaleSliderStyle\"", xaml);
        Assert.Contains("x:Key=\"UiScaleSliderTrackButtonStyle\"", xaml);
        Assert.Contains("x:Name=\"UiScaleSlider\"", xaml);
        Assert.Contains("Track x:Name=\"PART_Track\"", xaml);
        Assert.Contains("ValueChanged=\"UiScaleSlider_ValueChanged\"", xaml);
        Assert.Contains("ClipToBounds=\"False\"", xaml);
        Assert.DoesNotContain("x:Name=\"UiScaleStepSelector\"", xaml);
        Assert.DoesNotContain("x:Key=\"UiScaleStepButtonStyle\"", xaml);
        Assert.Contains("x:Name=\"EnvironmentAccordionArrow\"", xaml);
        Assert.DoesNotContain("HorizontalOffset=\"24\"", xaml);
        Assert.Contains("UpdateSettingsPopupLayout", source);
        Assert.Contains("Fixed width: keep settings stable while the main widget is resized", source);
        Assert.Contains("SettingsAccordionAnimationMilliseconds", source);
        Assert.Contains("OpenSettingsAccordion", source);
        Assert.Contains("QuarticEase", source);
        Assert.Contains("NextSettingsAccordionAnimationToken", source);
    }

    [Fact]
    public void TrayMenu_CanToggleAutostartAndStartMinimized()
    {
        var mainWindowSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));
        var trayIconServiceSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "TrayIconService.cs"));

        Assert.Contains("_autostartMenuItem", trayIconServiceSource);
        Assert.Contains("_startMinimizedToTrayMenuItem", trayIconServiceSource);
        Assert.Contains("ToggleAutostartFromTray", mainWindowSource);
        Assert.Contains("ToggleStartMinimizedToTrayFromTray", mainWindowSource);
        Assert.Contains("_autostartMenuItem.Checked = texts.AutostartEnabled", trayIconServiceSource);
        Assert.Contains("_startMinimizedToTrayMenuItem.Checked = texts.StartMinimizedToTrayEnabled", trayIconServiceSource);
    }

    [Fact]
    public void GuideSoundPlayback_UsesSelectedSettingAndRespectsMute()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("if (!_viewModel.GuideSoundEnabled)", source);
        Assert.Contains("BatteryGuideSoundCatalog.ResolveGuideSound(", source);
        Assert.Contains("_viewModel.CustomGuideSoundPath", source);
        Assert.Contains("StopBatteryGuideChime();", source);
        Assert.Contains("GuideSoundPreviewStopText", source);
        Assert.Contains("SetGuideSoundPreviewPlaying(true)", source);
        Assert.Contains("PlaybackEnded += BatteryGuideChimePlayer_PlaybackEnded", source);
        Assert.Contains("GuideSoundPreviewSafetyTimeout", source);
        Assert.DoesNotContain("GetGuideSoundPreviewDuration", source);
    }

    [Fact]
    public void SettingsPopup_AlignsPresetLanguageAndFontRows()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        Assert.Contains("x:Name=\"ColorPresetLabelTextBlock\"", xaml);
        Assert.Contains("x:Name=\"LanguageLabelTextBlock\"", xaml);
        Assert.Contains("x:Name=\"UserFontLabelTextBlock\"", xaml);
        Assert.Contains("x:Name=\"UserFontRowGrid\"", xaml);
        Assert.Contains("x:Name=\"CustomGuideSoundFileLabelTextBlock\"", xaml);
        Assert.Contains("x:Name=\"ColorCustomizeRowGrid\"", xaml);
        Assert.Contains("Width=\"160\"", xaml);
        Assert.Contains("Height=\"32\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
        Assert.DoesNotContain("Margin=\"0,0,10,6\"", xaml);
    }

    [Fact]
    public void SettingsPopup_GroupIconsUseAnimatedLineArtwork()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        Assert.Contains("x:Name=\"EnvironmentSettingsIconRoot\"", xaml);
        Assert.Contains("x:Name=\"CustomizeSettingsIconRoot\"", xaml);
        Assert.Contains("x:Name=\"LabsSettingsIconRoot\"", xaml);
        Assert.Contains("BeginStoryboard", xaml);
        Assert.Contains("StrokeLineJoin=\"Round\"", xaml);
        Assert.Contains("<Viewbox x:Name=\"EnvironmentSettingsIconRoot\"", xaml);
        Assert.Contains("<Viewbox x:Name=\"CustomizeSettingsIconRoot\"", xaml);
        Assert.Contains("<Viewbox x:Name=\"LabsSettingsIconRoot\"", xaml);
        Assert.Contains("x:Name=\"EnvironmentIconRotate\"", xaml);
        Assert.Contains("x:Name=\"CustomizeIconRotate\"", xaml);
        Assert.Contains("x:Name=\"LabsIconRotate\"", xaml);
        Assert.Contains("x:Name=\"EnvironmentIconScale\"", xaml);
        Assert.Contains("x:Name=\"CustomizeIconScale\"", xaml);
        Assert.Contains("x:Name=\"LabsIconScale\"", xaml);
        Assert.Contains("ClipToBounds=\"True\"", xaml);
        Assert.Contains("To=\"360\"", xaml);
        Assert.Contains("Duration=\"0:0:18\"", xaml);
        Assert.Contains("Duration=\"0:0:20\"", xaml);
        Assert.Contains("Duration=\"0:0:16\"", xaml);
        Assert.Contains("To=\"1.07\"", xaml);
        Assert.Contains("To=\"-0.8\"", xaml);
        Assert.Contains("AutoReverse=\"True\"", xaml);
        Assert.Contains("SineEase EasingMode=\"EaseInOut\"", xaml);
        Assert.DoesNotContain("EnvironmentIconGearRotate", xaml);
        Assert.DoesNotContain("CustomizeIconPenRotate", xaml);
        Assert.DoesNotContain("LabsIconLiquidShift", xaml);
        Assert.DoesNotContain("LabsIconRightBubbleShift", xaml);
        Assert.DoesNotContain("To=\"-1.2\"", xaml);
        Assert.DoesNotContain("To=\"-1.1\"", xaml);
    }

    [Fact]
    public void EnvironmentAccordion_TogglesUseSharedAlignment()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        Assert.Contains("x:Name=\"AutostartToggle\"", xaml);
        Assert.Contains("x:Name=\"StartMinimizedToTrayToggle\"", xaml);
        Assert.Contains("x:Name=\"CloseToTrayToggle\"", xaml);
        Assert.Contains("x:Name=\"GuideSoundToggle\"", xaml);
        Assert.True(xaml.Split("ColumnDefinition Width=\"56\"").Length >= 5);
        Assert.True(xaml.Split("HorizontalAlignment=\"Center\"").Length >= 5);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);
        Assert.Contains("UseLayoutRounding=\"True\"", xaml);
    }

    [Fact]
    public void SettingsPopup_ProvidesPicoFirmwareUpdateFromLabs()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"PicoFirmwareUpdateRowGrid\"", xaml);
        Assert.Contains("x:Name=\"PicoFirmwareUpdateButton\"", xaml);
        Assert.Contains("Content=\"{Binding TextPicoFirmwareButton}\"", xaml);
        Assert.Contains("Click=\"PicoFirmwareUpdateButton_Click\"", xaml);
        Assert.Contains("x:Name=\"PicoFirmwareUpdateStatusTextBlock\"", xaml);
        Assert.Contains("Text=\"{Binding TextPicoFirmwareReady}\"", xaml);
        Assert.Contains("https://api.github.com/repos/awalol/DS5Dongle/releases/latest", source);
        Assert.Contains(".uf2", source);
        Assert.Contains("RP2350", source);
        Assert.Contains("RPI-RP2", source);
        Assert.Contains("FindPicoBootDrive()", source);
        Assert.Contains("Ds5DongleFirmwareVersionReader.ReadCurrentVersion", source);
        Assert.Contains("LastDs5DongleFirmwareVersion", source);
        Assert.Contains("RememberDs5DongleFirmwareVersion", source);
        Assert.Contains("PicoBootDriveAlreadyLatestRememberedFormat", source);
        Assert.Contains("PicoBootDriveConfirmFlashFormat", source);
        Assert.Contains("MessageBoxButton.YesNo", source);
        Assert.Contains("EnumeratePresentHidEndpoints", File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "Ds5DongleFirmwareVersionReader.cs")));
        Assert.Contains("TryReadFeatureReportExact", File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "Ds5DongleFirmwareVersionReader.cs")));
        Assert.Contains("IsDs5DongleFirmwareUpdateNeeded", source);
        Assert.Contains("ExtraText(\"PicoReadHintNoUsbDs5Dongle\")", source);
        Assert.Contains("PicoUpdateAvailableMessageFormat", source);
        Assert.Contains("FlashDs5DongleFirmwareAsync", source);
        Assert.Contains("File.Copy(tempPath, destinationPath, overwrite: true)", source);

        var languageSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "UiLanguageCatalog.cs"));
        Assert.Contains("DualSense가 Pico2W를 통해 연결된 상태", languageSource);
        Assert.Contains("PicoReadHintNoUsbDs5Dongle", languageSource);
        Assert.DoesNotContain("copies the latest firmware automatically", languageSource);
    }

    [Fact]
    public void Scripts_ProvideDs5DongleFirmwareDiagnosticProbe()
    {
        var script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "build",
            "scripts",
            "show-ds5dongle-firmware.ps1"));

        Assert.Contains("VID_054C PID_0CE6/0DF2", script);
        Assert.Contains("Feature Report 0xF8", script);
        Assert.Contains("HidD_GetFeature", script);
        Assert.Contains("RP2350", script);
        Assert.Contains("RPI-RP2", script);
        Assert.Contains("through Pico2W", script);
    }

    [Fact]
    public void SettingsPopup_GroupAndNestedLabelsUseLocalizedBindings()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "ViewModels",
            "MainViewModel.cs"));
        var languageCatalog = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "UiLanguageCatalog.cs"));

        Assert.Contains("Text=\"{Binding TextEnvironmentSettingsGroup}\"", xaml);
        Assert.Contains("Text=\"{Binding TextCustomizeSettingsGroup}\"", xaml);
        Assert.Contains("Text=\"{Binding TextLabsSettingsGroup}\"", xaml);
        Assert.Contains("Text=\"{Binding TextStartMinimizedToTray}\"", xaml);
        Assert.Contains("Text=\"{Binding TextGuideSound}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorCustomize}\"", xaml);
        Assert.Contains("Text=\"{Binding TextUserFont}\"", xaml);
        Assert.Contains("Content=\"{Binding TextLoadCustomFont}\"", xaml);
        Assert.Contains("Content=\"{Binding TextResetCustomFont}\"", xaml);
        Assert.Contains("HorizontalContentAlignment=\"Center\"", xaml);
        Assert.DoesNotContain("Text=\"환경설정\"", xaml);
        Assert.DoesNotContain("Text=\"커스터마이즈\"", xaml);
        Assert.DoesNotContain("Text=\"실험실\"", xaml);

        Assert.Contains("TextEnvironmentSettingsGroup", viewModel);
        Assert.Contains("OnPropertyChanged(nameof(TextEnvironmentSettingsGroup))", viewModel);
        Assert.Contains("EnvironmentSettingsGroup", languageCatalog);
        Assert.Contains("EnvironmentSettingsGroup: \"Environment\"", languageCatalog);
        Assert.Contains("EnvironmentSettingsGroup = \"환경설정\"", languageCatalog);
    }

    [Fact]
    public void VersionText_IsHiddenOuterSpaceEasterEgg()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml"));

        Assert.Contains("x:Name=\"VersionEasterEggButton\"", xaml);
        Assert.Contains("Click=\"VersionEasterEggButton_Click\"", xaml);
        Assert.Contains("x:Name=\"VersionTextBlock\"", xaml);
    }

    [Fact]
    public void LabsWindow_UsesCodeCityAnimationAndAutoClose()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "LabsWindow.cs"));

        Assert.Contains("CodeCityDuration", source);
        Assert.Contains("BatteryGuideSoundCatalog.Version107DigitalCitySound", source);
        Assert.Contains("CodeCityView", source);
        Assert.Contains("CompositionTarget.Rendering", source);
        Assert.Contains("DrawTunnel", source);
        Assert.Contains("DrawCityDrive", source);
        Assert.Contains("DrawFarCity", source);
        Assert.Contains("DrawBuildingCanyon", source);
        Assert.Contains("DrawRoadReflections", source);
        Assert.Contains("DrawCityAtmosphere", source);
        Assert.Contains("BeginAnimation", source);
        Assert.Contains("CodeMarkCount", source);
        Assert.Contains("TunnelGreen", source);
        Assert.Contains("CityTeal", source);
        Assert.Contains("brush.Freeze()", source);
        Assert.Contains("CreateSolidBrush", source);
        Assert.Contains("SmoothSeconds", source);
        Assert.Contains("CityAmber", source);
        Assert.Contains("RoadBlue", source);
        Assert.Contains("DrawStreetLights", source);
        Assert.Contains("BuildCodeMarks", source);
        Assert.DoesNotContain("EnergyPalette", source);
        Assert.DoesNotContain("DrawAccretionDisk", source);
        Assert.DoesNotContain("BuildPaintStrokeLayer", source);
        Assert.DoesNotContain("DropShadowEffect", source);
    }

    [Fact]
    public void SteamSecondaryGuideFallback_IsNotSuppressedUnconditionally()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("secondary_fallback_pending", source);
        Assert.Contains("secondary_fallback_accepted", source);
        Assert.Contains("Secondary Steam HID monitor event was used because RawInput did not produce a toast", source);
    }

    [Fact]
    public void GuideButtonKnownDevices_UseBaseDisplayNameAfterUserRename()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("device.BaseDisplayName.Contains(\"Steam Controller\"", source);
        Assert.Contains("device.BaseDisplayName.Contains(\"Steam Ctrl\"", source);
        Assert.Contains("device.BaseDisplayName.Contains(\"DualSense\"", source);
        Assert.Contains("GuideButtonDeviceKind.SteamController => _viewModel.Devices.FirstOrDefault(device =>", source);
        Assert.Contains("IsSteamControllerDevice(device)", source);
    }

    [Fact]
    public void SteamControllerGuideButtonToastCooldown_KeepsRepeatedTapsResponsive()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(350), MainWindow.GetGuideButtonToastCooldown(GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void SteamControllerGuideButtonToastCooldown_AllowsNextIntentBeforeToastFullyExpires()
    {
        var firstToast = new DateTimeOffset(2026, 5, 26, 1, 20, 0, TimeSpan.Zero);

        Assert.True(MainWindow.ShouldSuppressGuideButtonToast(
            firstToast,
            firstToast + TimeSpan.FromMilliseconds(250),
            GuideButtonDeviceKind.SteamController));
        Assert.False(MainWindow.ShouldSuppressGuideButtonToast(
            firstToast,
            firstToast + TimeSpan.FromMilliseconds(450),
            GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void SteamSecondaryFallbackBurstWindow_OnlyBlocksImmediatePowerOffEcho()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(450), MainWindow.GetSteamSecondaryFallbackBurstWindow());
        Assert.True(MainWindow.GetSteamSecondaryFallbackBurstWindow() > MainWindow.GetGuideButtonToastCooldown(GuideButtonDeviceKind.SteamController));
        Assert.True(MainWindow.GetSteamSecondaryFallbackBurstWindow() < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SteamSecondaryFallbackDelay_IsShortButStillCatchesRepeatedHoldSignals()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(120), MainWindow.GetSteamSecondaryFallbackDelay());
        Assert.True(MainWindow.GetSteamSecondaryFallbackDelay() > TimeSpan.FromMilliseconds(80));
        Assert.True(MainWindow.GetSteamSecondaryFallbackDelay() < TimeSpan.FromMilliseconds(180));
    }

    [Fact]
    public void SteamRawInputPreferredWindow_DoesNotSwallowRapidRepeatTaps()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(300), MainWindow.GetSteamRawInputPreferredWindow());
        Assert.True(MainWindow.GetSteamRawInputPreferredWindow() < MainWindow.GetGuideButtonToastCooldown(GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void SteamRawInputLongPress_BlocksSecondaryFallbackInsteadOfShowingToast()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("e.Gesture == GuideButtonGesture.LongPress", source);
        Assert.Contains("raw_long_press_secondary_blocked", source);
        Assert.Contains("_steamSecondaryGuideFallbackBlockedUntilByDevice[key] = now + SteamSecondaryFallbackBurstWindow", source);
        Assert.Contains("secondary_duplicate_pending_suppressed", source);
        Assert.Contains("secondary_fallback_waiting_for_raw_hid", source);
        Assert.Contains("secondary_fallback_raw_hid_hold_suppressed", source);
        Assert.Contains("secondary_fallback_raw_hid_pending_suppressed", source);
        Assert.Contains("secondary_fallback_stale_raw_hid_ignored", source);
        Assert.Contains("fixed Steam power-off guard window", source);
        Assert.Contains("GetGuideButtonActivity(e.Address)", source);
        Assert.Contains("SteamSecondaryFallbackRawHidAmbiguousMaximumWait = TimeSpan.FromMilliseconds(2500)", source);
        Assert.Contains("SteamSecondaryFallbackRawHidStaleStateAge = TimeSpan.FromMilliseconds(1200)", source);
        Assert.Contains("SteamSecondaryFallbackRawHidPreExistingHoldAge = TimeSpan.FromMilliseconds(2500)", source);
        Assert.DoesNotContain("Secondary Steam HID monitor event was ignored because repeated fallback events look like a hold.", source);
    }

    [Fact]
    public void SteamRawInputStatusReportsReleaseOnlyExistingPressedState()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "SteamControllerRawInputMonitorService.cs"));

        Assert.Contains("raw_hid_status_release_hint", source);
        Assert.Contains("raw_hid_status_release_skipped", source);
        Assert.Contains("raw_hid_guide_state", source);
        Assert.Contains("raw_hid_stale_state_cleared", source);
        Assert.Contains("raw_hid_input_guard_armed", source);
        Assert.Contains("raw_hid_input_guard_suppressed", source);
        Assert.Contains("SuppressGuideInputForKnownDevices(TimeSpan duration, string reason)", source);
        Assert.Contains("activity.IsPressed", source);
        Assert.Contains("IsSteamControllerStatusReport(report)", source);
        Assert.DoesNotContain("raw_hid_battery_release_hint", source);
        Assert.DoesNotContain("_rawHidGuideButtonStateTracker.ClearStalePressedSession(address, now);", source);
    }

    [Fact]
    public void SteamGuideMonitorWaitsForNeutralStateBeforeShortPress()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "GuideButtonMonitorService.cs"));

        Assert.Contains("var hasSeenNeutralState = endpoint.DeviceKind != GuideButtonDeviceKind.SteamController;", source);
        Assert.Contains("ref bool hasSeenNeutralState", source);
        Assert.Contains("if (!hasSeenNeutralState)", source);
        Assert.Contains("pressedAt = null;", source);
    }

    [Fact]
    public void BatteryGuideTriggerCapture_UsesCenteredOverlayAndConfirmedSave()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var captureXaml = File.ReadAllText(Path.Combine(appRoot, "BatteryGuideTriggerCaptureWindow.xaml"));
        var captureSource = File.ReadAllText(Path.Combine(appRoot, "BatteryGuideTriggerCaptureWindow.xaml.cs"));
        var mainWindowSource = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml.cs"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml"));

        Assert.Contains("x:Class=\"BluetoothBatteryWidget.App.BatteryGuideTriggerCaptureWindow\"", captureXaml);
        Assert.Contains("Topmost=\"True\"", captureXaml);
        Assert.Contains("Width=\"1448\"", captureXaml);
        Assert.Contains("Height=\"1086\"", captureXaml);
        Assert.Contains("Loaded=\"Window_Loaded\"", captureXaml);
        Assert.Contains("x:Name=\"WindowSurface\"", captureXaml);
        Assert.Contains("x:Name=\"WindowSurfaceScale\"", captureXaml);
        Assert.Contains("x:Name=\"WindowSurfaceSkew\"", captureXaml);
        Assert.Contains("x:Name=\"WindowSurfaceTranslate\"", captureXaml);
        Assert.Contains("RenderTransformOrigin=\"0.5,0.5\"", captureXaml);
        Assert.Contains("x:Name=\"PromptTextBlock\"", captureXaml);
        Assert.Contains("조합할 2개의 버튼을 눌러주세요", captureXaml);
        Assert.DoesNotContain("알림 버튼 또는 조합을 눌러주세요", captureXaml);
        Assert.Contains("BlueprintLine", captureXaml);
        Assert.Contains("Canvas Width=\"1448\"", captureXaml);
        Assert.Contains("Height=\"1086\"", captureXaml);
        Assert.Contains("OriginalBlueprintImage", captureXaml);
        Assert.Contains("VectorBlueprintLayer", captureXaml);
        Assert.Contains("x:Name=\"VectorBlueprintLayer\"", captureXaml);
        Assert.Contains("Visibility=\"Collapsed\"", captureXaml);
        Assert.Contains("RenderOptions.BitmapScalingMode=\"HighQuality\"", captureXaml);
        Assert.Contains("HotspotSurfaceStyle", captureXaml);
        Assert.Contains("Cursor=\"SizeAll\"", captureXaml);
        Assert.Contains("MouseLeftButtonDown=\"HeaderDragArea_MouseLeftButtonDown\"", captureXaml);
        Assert.Contains("PreviewKeyDown=\"Window_PreviewKeyDown\"", captureXaml);
        Assert.DoesNotContain("KeyDown=\"Window_KeyDown\"", captureXaml);
        Assert.Contains("<Setter Property=\"Focusable\" Value=\"False\" />", captureXaml);
        Assert.Contains("<Setter Property=\"IsTabStop\" Value=\"False\" />", captureXaml);
        Assert.Contains("LTKey", captureXaml);
        Assert.Contains("RBKey", captureXaml);
        Assert.Contains("AKey", captureXaml);
        Assert.Contains("GuideKey", captureXaml);
        Assert.Contains("QuickAccessKey", captureXaml);
        Assert.Contains("Canvas.Left=\"696\" Canvas.Top=\"602\" Width=\"58\" Height=\"58\"", captureXaml);
        Assert.Contains("[\"QuickAccess\"] = QuickAccessKey", captureSource);
        Assert.Contains("[\"Mic\"] = QuickAccessKey", captureSource);
        Assert.Contains("x:Name=\"LeftKey\" Canvas.Left=\"268\" Canvas.Top=\"416\" Width=\"75\" Height=\"83\"", captureXaml);
        Assert.Contains("x:Name=\"RightKey\" Canvas.Left=\"424\" Canvas.Top=\"416\" Width=\"74\" Height=\"83\"", captureXaml);
        Assert.Contains("x:Name=\"UpKey\" Canvas.Left=\"341\" Canvas.Top=\"343\" Width=\"83\" Height=\"73\"", captureXaml);
        Assert.Contains("x:Name=\"DownKey\" Canvas.Left=\"341\" Canvas.Top=\"499\" Width=\"83\" Height=\"75\"", captureXaml);
        Assert.Contains("Content=\"저장하기\"", captureXaml);
        Assert.Contains("Content=\"다시 설정하기\"", captureXaml);
        Assert.Contains("ApplyLocalizedText", captureSource);
        Assert.Contains("BatteryGuideCapturePrompt", captureSource);
        Assert.Contains("BatteryGuideCaptureWaiting", captureSource);
        Assert.Contains("BatteryGuideTriggerCaptureWindow(_viewModel.Language)", mainWindowSource);
        Assert.Contains("PopInOriginScreenPoint = TryGetElementCenterScreenPoint(BatteryGuideTriggerSelectButton)", mainWindowSource);
        Assert.Contains("SetCandidate(BatteryGuideTrigger? trigger)", captureSource);
        Assert.Contains("TryLoadOriginalBlueprintImage", captureSource);
        Assert.Contains("TryLoadEmbeddedOriginalBlueprintImage", captureSource);
        Assert.Contains("VectorBlueprintLayer.Visibility = Visibility.Visible", captureSource);
        Assert.Contains("typeof(BatteryGuideTriggerCaptureWindow).Assembly.GetName().Name", captureSource);
        Assert.Contains("component/Assets/controller-guide-blueprint.png", captureSource);
        Assert.Contains("controller-guide-blueprint.png", captureSource);
        Assert.Contains("controller-guide-blueprint.jpg", captureSource);
        Assert.Contains("BatteryGuideTriggerParser.GetVisualButtonKeys(trigger)", captureSource);
        Assert.Contains("HeaderDragArea_MouseLeftButtonDown", captureSource);
        Assert.Contains("DragMove()", captureSource);
        Assert.Contains("Window_PreviewKeyDown", captureSource);
        Assert.Contains("e.Handled = true;", captureSource);
        Assert.Contains("WmClose", captureSource);
        Assert.Contains("WmSysCommand", captureSource);
        Assert.Contains("ScClose", captureSource);
        Assert.Contains("_allowClose", captureSource);
        Assert.Contains("OnClosing(CancelEventArgs e)", captureSource);
        Assert.Contains("e.Cancel = true;", captureSource);
        Assert.Contains("CloseFromOwner()", captureSource);
        Assert.Contains("_allowClose = true;", captureSource);
        Assert.Contains("WindowMessageHook", captureSource);
        Assert.Contains("HwndSource.FromHwnd", captureSource);
        Assert.Contains("AddHook(WindowMessageHook)", captureSource);
        Assert.Contains("RemoveHook(WindowMessageHook)", captureSource);
        Assert.Contains("handled = true;", captureSource);
        Assert.Contains("msg == WmClose", captureSource);
        Assert.Contains("CloseFromOwner);", captureSource);
        Assert.Contains("PopInOriginScreenPoint", captureSource);
        Assert.Contains("WindowSurfaceSkew", captureSource);
        Assert.Contains("WindowPopInAnimator.Begin(", captureSource);
        Assert.Contains("WindowSurfaceScale,", captureSource);
        Assert.Contains("WindowSurfaceSkew,", captureSource);
        Assert.Contains("WindowSurfaceTranslate,", captureSource);
        Assert.DoesNotContain("private void Window_KeyDown", captureSource);
        Assert.Contains("_pendingBatteryGuideTriggerCapture", mainWindowSource);
        Assert.Contains("BatteryGuideTriggerCaptureDesignWidth = 1448d", mainWindowSource);
        Assert.Contains("BatteryGuideTriggerCaptureDesignHeight = 1086d", mainWindowSource);
        Assert.Contains("BatteryGuideTriggerCaptureMaxWorkAreaRatio = 0.78d", mainWindowSource);
        Assert.Contains("BatteryGuideTriggerCaptureMaxScale = 0.88d", mainWindowSource);
        Assert.Contains("TryUpdateBatteryGuideTriggerCapture", mainWindowSource);
        Assert.Contains("BatteryGuideTriggerCaptureWindow_SaveRequested", mainWindowSource);
        Assert.Contains("_batteryGuideTriggerCaptureWindow?.CloseFromOwner();", mainWindowSource);
        Assert.DoesNotContain("_batteryGuideTriggerCaptureWindow?.Close();", mainWindowSource);
        Assert.Contains("PositionBatteryGuideTriggerCaptureWindow", mainWindowSource);
        Assert.Contains("GetWorkingAreaForOwnerWindow", mainWindowSource);
        Assert.Contains("GetWorkingAreaFromScreen", mainWindowSource);
        Assert.Contains("TransformFromDevice", mainWindowSource);
        Assert.DoesNotContain("TrySaveBatteryGuideTriggerCapture", mainWindowSource);
        Assert.DoesNotContain("Content=\"↺\"", mainWindowXaml);
        Assert.Contains("BatteryGuideTriggerResetButtonStyle", mainWindowXaml);
        Assert.Contains("reset-button-blue.png", mainWindowXaml);
        Assert.Contains("ResetCircleImage", mainWindowXaml);
        Assert.Contains("ResetWhiteCircle", mainWindowXaml);
        Assert.Contains("ResetInnerPulseScale", mainWindowXaml);
        Assert.Contains("ResetCircleScale", mainWindowXaml);
        Assert.Contains("ClipToBounds=\"True\"", mainWindowXaml);
        Assert.Contains("HasCustomBatteryGuideTrigger", mainWindowXaml);
        Assert.DoesNotContain("BatteryGuideTriggerProfileSummaryTextBlock", mainWindowXaml);
        Assert.DoesNotContain("Text=\"{Binding BatteryGuideTriggerProfileSummary}\"", mainWindowXaml);
        Assert.Contains("CustomTabItem", captureXaml);
        Assert.Contains("PlayStationTabItem", captureXaml);
        Assert.Contains("SteamControllerTabItem", captureXaml);
        Assert.Contains("CaptureTopTabItemStyle", captureXaml);
        Assert.Contains("WaterPulse", captureXaml);
        Assert.Contains("ContentSource=\"Header\"", captureXaml);
        Assert.Contains("TextElement.Foreground=\"{TemplateBinding Foreground}\"", captureXaml);
        Assert.Contains("TextElement.FontSize=\"{TemplateBinding FontSize}\"", captureXaml);
        Assert.Contains("TextElement.FontWeight=\"{TemplateBinding FontWeight}\"", captureXaml);
        Assert.Contains("Margin=\"{TemplateBinding Padding}\"", captureXaml);
        Assert.Contains("ClipToBounds=\"False\"", captureXaml);
        Assert.Contains("ProfileDetailTextBlock", captureXaml);
        Assert.Contains("SetProfiles(IReadOnlyDictionary<string, string>? profiles, string? legacyTrigger)", captureSource);
        Assert.Contains("GetLocalizedCustomTabText", captureSource);
        Assert.Contains("BuildProfileLine(", captureSource);
        Assert.Contains("GuideButtonDeviceKind.DualSense", captureSource);
        Assert.DoesNotContain("ProfilesTabItem", captureXaml);
        Assert.DoesNotContain("ProfileSummaryTextBlock", captureXaml);
        Assert.Contains("<Setter Property=\"Foreground\" Value=\"#10243C\" />", mainWindowXaml);
        Assert.Contains("<Setter Property=\"Foreground\" Value=\"#FFFFFFFF\" />", mainWindowXaml);
        Assert.Contains("RoutedEvent=\"Button.Click\"", mainWindowXaml);
        Assert.Contains("BatteryGuideTriggerResetPath", mainWindowXaml);
        Assert.Contains("BatteryGuideTriggerResetRotate", mainWindowXaml);
        Assert.Contains("ToolTip=\"{Binding TextRestoreDefault}\"", mainWindowXaml);
        Assert.DoesNotContain("ToolTip=\"기본 알림 버튼으로 되돌리기\"", mainWindowXaml);
        Assert.Contains("Padding=\"0\"", mainWindowXaml);
        Assert.Contains("Width=\"18\"", mainWindowXaml);
        Assert.Contains("Height=\"18\"", mainWindowXaml);
        Assert.Contains("Canvas Width=\"24\"", mainWindowXaml);
        Assert.Contains("CenterX=\"12\"", mainWindowXaml);
        Assert.Contains("M17.65,6.35C16.2,4.9", mainWindowXaml);
        Assert.DoesNotContain("BatteryGuideTriggerResetArrowHeadPath", mainWindowXaml);
        Assert.Contains("BatteryGuideTriggerSelectButtonStyle", mainWindowXaml);
        Assert.Contains("Focusable=\"False\"", mainWindowXaml);
        Assert.Contains("IsTabStop=\"False\"", mainWindowXaml);
        Assert.Contains("PreviewMouseLeftButtonDown=\"BatteryGuideTriggerSelectButton_PreviewMouseLeftButtonDown\"", mainWindowXaml);
        Assert.Contains("HasCustomBatteryGuideTrigger", mainWindowXaml);
        Assert.Contains("ActiveGuideTriggerBorder", mainWindowXaml);
        Assert.Contains("GuideActiveStopA", mainWindowXaml);
        Assert.DoesNotContain("Burgundy", mainWindowXaml);
        Assert.Contains("BatteryGuideTriggerActivePulse", mainWindowXaml);
        Assert.Contains("BeginBatteryGuideTriggerCapture();", mainWindowSource);
        Assert.Contains("_batteryGuideTriggerSelectMouseToggleRequested", mainWindowSource);
        Assert.Contains("BatteryGuideTriggerSelectButton_PreviewMouseLeftButtonDown", mainWindowSource);
        Assert.Contains("_batteryGuideTriggerCaptureWindow.Activate();", mainWindowSource);
        Assert.DoesNotContain(
            "_viewModel.ResetBatteryGuideTrigger();",
            mainWindowSource[
                mainWindowSource.IndexOf("private void BatteryGuideTriggerSelectButton_Click", StringComparison.Ordinal)..
                mainWindowSource.IndexOf("private void BatteryGuideTriggerResetButton_Click", StringComparison.Ordinal)]);
        Assert.Contains("SpinBatteryGuideTriggerResetIcon", mainWindowSource);
        Assert.Contains("BeginAnimation(RotateTransform.AngleProperty", mainWindowSource);
        var resetSpinMethod = mainWindowSource[
            mainWindowSource.IndexOf("private void SpinBatteryGuideTriggerResetIcon", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private void BeginBatteryGuideTriggerCapture", StringComparison.Ordinal)];
        Assert.Contains("new DoubleAnimation(0, 360", resetSpinMethod);
        Assert.DoesNotContain("EasingFunction", resetSpinMethod);
    }

    [Fact]
    public void PowerIdlePause_UsesLocalizedSleepGuardText()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var viewModelSource = File.ReadAllText(Path.Combine(appRoot, "ViewModels", "MainViewModel.cs"));
        var languageCatalogSource = File.ReadAllText(Path.Combine(appRoot, "Services", "UiLanguageCatalog.cs"));
        var systemDisplayIdleTimeoutSource = File.ReadAllText(Path.Combine(appRoot, "Services", "SystemDisplayIdleTimeout.cs"));
        var mainWindowSource = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml.cs"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml"));
        var compositionSource = File.ReadAllText(Path.Combine(appRoot, "AppCompositionRoot.cs"));

        Assert.Contains("TextPowerIdlePause => UiLanguageCatalog.GetExtraText(Settings.Language, \"PowerIdlePause\")", viewModelSource);
        Assert.Contains("TextWindowsDisplayOff => UiLanguageCatalog.GetExtraText(Settings.Language, \"WindowsDisplayOff\")", viewModelSource);
        Assert.Contains("StatusText = connectionOnlyForPowerIdle", viewModelSource);
        Assert.Contains("UiLanguageCatalog.GetExtraText(Settings.Language, \"PowerIdlePauseActive\")", viewModelSource);
        Assert.Contains("\"PowerIdlePause\" => \"절전 보호\"", languageCatalogSource);
        Assert.Contains("\"PowerIdlePauseActive\" => \"절전 보호 활성화\"", languageCatalogSource);
        Assert.Contains("\"PowerIdlePauseAuto\" => \"자동(Windows 설정)\"", languageCatalogSource);
        Assert.Contains("\"PowerIdlePauseOff\" => \"끔\"", languageCatalogSource);
        Assert.Contains("\"PowerIdlePauseMinutesFormat\" => \"{0}분\"", languageCatalogSource);
        Assert.Contains("\"PowerIdlePauseOneHour\" => \"1시간\"", languageCatalogSource);
        Assert.Contains("\"PowerIdlePauseHoursFormat\" => \"{0}시간\"", languageCatalogSource);
        Assert.Contains("\"WindowsDisplayOff\" => \"Windows 화면 꺼짐\"", languageCatalogSource);
        Assert.Contains("\"WindowsDisplayOffNever\" => \"안 끔\"", languageCatalogSource);
        Assert.Contains("\"WindowsDisplayOffOneHour\" => \"1시간\"", languageCatalogSource);
        Assert.Contains("\"WindowsDisplayOffHoursFormat\" => \"{0}시간\"", languageCatalogSource);
        Assert.Contains("\"PowerIdlePause\" => \"Sleep guard\"", languageCatalogSource);
        Assert.Contains("\"WindowsDisplayOff\" => \"Windows display off\"", languageCatalogSource);
        Assert.Contains("PowerIdlePauseOptions => BuildPowerIdlePauseOptions(Settings.Language)", viewModelSource);
        Assert.Contains("BuildWindowsDisplayOffOptions(Settings.Language, GetCurrentWindowsDisplayOffMinutes())", viewModelSource);
        Assert.Contains("new[] { 1, 5, 10, 15, 30, 45, 60, 120, 180, 240, 300 }", viewModelSource);
        Assert.Contains("FormatPowerDurationLabel", viewModelSource);
        Assert.Contains("GetLocalIdleDuration()", viewModelSource);
        Assert.Contains("GetGamepadIdleDuration()", viewModelSource);
        Assert.Contains("GetGamepadTelemetryIdleDuration()", viewModelSource);
        Assert.Contains("GetLocalOrGamepadIdleDuration()", viewModelSource);
        Assert.Contains("public void MarkUserActivity()", viewModelSource);
        Assert.Contains("public void MarkGamepadActivity()", viewModelSource);
        Assert.Contains("public void MarkIntentionalGamepadActivity()", viewModelSource);
        Assert.Contains("public void MarkGamepadTelemetryActivity()", viewModelSource);
        Assert.Contains("ProbeUnsupportedGamepadAsync(string address, bool markUserActivity = true)", viewModelSource);
        Assert.Contains("ApplyProbeProgress(string address, ProbeProgress progress, bool markUserActivity = true)", viewModelSource);
        Assert.Contains("ApplyProbeProgress(normalizedAddress, progress, markUserActivity)", viewModelSource);
        Assert.Contains("MarkProbeActivityIfNeeded(markUserActivity)", viewModelSource);
        Assert.Contains("ShouldMarkProbeAsUserActivity(markUserActivity)", viewModelSource);
        Assert.Contains("ShouldStartBackgroundProbe(ShouldPauseBackgroundPollingForPowerIdle())", viewModelSource);
        Assert.Contains("_ = ProbeUnsupportedGamepadAsync(address, markUserActivity: false);", viewModelSource);
        Assert.Contains("_ = ProbeUnsupportedGamepadAsync(normalized, markUserActivity: false);", viewModelSource);
        Assert.Contains("SystemDisplayIdleTimeout.GetCurrentDisplayOrSleepTimeout()", viewModelSource);
        Assert.Contains("RefreshAsync(bool forceFullRefresh = false)", viewModelSource);
        Assert.Contains("connectionOnlyForPowerIdle", viewModelSource);
        Assert.Contains("connectedDevices = BuildPowerIdleConnectedDevicesSnapshot();", viewModelSource);
        Assert.Contains("connectedDevices = await _connectedDeviceProvider", viewModelSource);
        Assert.Contains("BuildPowerIdleConnectedDevicesSnapshot()", viewModelSource);
        Assert.Contains("PowerIdleCachedDeviceIdPrefix", viewModelSource);
        Assert.Contains("if (!connectionOnlyForPowerIdle && connectedDevices.Count > 0)", viewModelSource);
        Assert.Contains("if (!connectionOnlyForPowerIdle &&", viewModelSource);
        Assert.Contains("autoProbeTarget = connectionOnlyForPowerIdle", viewModelSource);
        Assert.Contains("var winRtConnectedDeviceProvider = new WinRtConnectedDeviceProvider();", compositionSource);
        Assert.Contains("winRtConnectedDeviceProvider,", compositionSource);
        Assert.Contains("OnPropertyChanged(nameof(PowerIdlePauseOptions))", viewModelSource);
        Assert.Contains("OnPropertyChanged(nameof(WindowsDisplayOffOptions))", viewModelSource);
        Assert.Contains("RefreshPowerIdlePauseOptions", mainWindowSource);
        Assert.Contains("WindowsDisplayOffComboBox_SelectionChanged", mainWindowSource);
        Assert.Contains("RefreshWindowsDisplayOffOptions", mainWindowSource);
        Assert.Contains("public bool IsAnyProbeRunning => _isAnyProbeRunning;", viewModelSource);
        Assert.Contains("public bool IsRefreshRunning => _isRefreshRunning;", viewModelSource);
        Assert.Contains("public bool IsDisplaySleepPreparationActive => _isDisplaySleepPreparationActive;", viewModelSource);
        Assert.Contains("public void SetDisplaySleepPreparationActive(bool isActive)", viewModelSource);
        Assert.Contains("if (_isDisplaySleepPreparationActive)", viewModelSource);
        Assert.Contains("_viewModel.IsAnyProbeRunning || _isBatteryGuideTriggerCaptureActive", mainWindowSource);
        Assert.Contains("_viewModel.IsRefreshRunning", mainWindowSource);
        Assert.Contains("_powerIdleMonitorTimer.Interval = TimeSpan.FromSeconds(1)", mainWindowSource);
        Assert.Contains("_viewModel.GetGamepadIdleDuration()", mainWindowSource);
        Assert.Contains("_viewModel.GetLocalOrGamepadIdleDuration()", mainWindowSource);
        Assert.Contains("_viewModel.MarkUserActivity();", mainWindowSource);
        Assert.Contains("_viewModel.MarkIntentionalGamepadActivity();", mainWindowSource);
        Assert.Contains("_viewModel.MarkGamepadTelemetryActivity();", mainWindowSource);
        Assert.Contains("isRefreshRunning: false);", mainWindowSource);
        Assert.Contains("SystemDisplayIdleTimeout.GetCurrentDisplayOrSleepTimeout()", mainWindowSource);
        Assert.DoesNotContain("shouldForceDisplayOff", mainWindowSource);
        Assert.DoesNotContain("forceDisplayOff=", File.ReadAllText(Path.Combine(
            appRoot,
            "Services",
            "PowerIdleDebugLog.cs")));
        Assert.Contains("PowerIdleRuntimeMode.PreDisplaySleep", mainWindowSource);
        Assert.Contains("PowerIdleRuntimeMode.DisplayOffWakeOnly", mainWindowSource);
        Assert.Contains("PowerIdleRuntimeMode.WakeRecovery", mainWindowSource);
        Assert.Contains("PowerIdleRuntimeMode.ExternalInputBlocked", mainWindowSource);
        Assert.Contains("ResolvePowerIdleRuntimeMode(", mainWindowSource);
        Assert.DoesNotContain("TryRestoreActiveFromExternalSystemInput(", mainWindowSource);
        Assert.DoesNotContain("external_system_input_restored_active", mainWindowSource);
        Assert.Contains("ShouldEnterPreDisplaySleep(", mainWindowSource);
        Assert.Contains("ResolveQuietPreparationStart(", mainWindowSource);
        Assert.Contains("QuietPreparationStartMinimum", mainWindowSource);
        Assert.Contains("QuietPreparationStartMaximum", mainWindowSource);
        Assert.Contains("ShortDisplayTimeoutQuietPreparationStart", mainWindowSource);
        Assert.Contains("ShortDisplayTimeoutQuietPreparationLead", mainWindowSource);
        Assert.Contains("NormalGamepadMonitoringGrace", mainWindowSource);
        Assert.Contains("ExternalInputBlockProbeWindow", mainWindowSource);
        Assert.Contains("ShortDisplayTimeoutExternalInputProbeWindow", mainWindowSource);
        Assert.Contains("LongDisplayTimeoutExternalInputProbeWindowMaximum", mainWindowSource);
        Assert.Contains("ResolveExternalInputBlockProbeWindow(", mainWindowSource);
        Assert.Contains("ExternalInputBlockRetryCooldown", mainWindowSource);
        Assert.DoesNotContain("PreDisplaySleepQuietLeadTime", mainWindowSource);
        Assert.DoesNotContain("MinimumPreDisplaySleepQuietPoint", mainWindowSource);
        var updatePowerIdleMethodStart = mainWindowSource.IndexOf("private void UpdatePowerIdleGuideMonitoring()", StringComparison.Ordinal);
        var updatePowerIdleMethodEnd = mainWindowSource.IndexOf("private PowerIdleRuntimeMode ResolvePowerIdleRuntimeMode", updatePowerIdleMethodStart, StringComparison.Ordinal);
        Assert.True(updatePowerIdleMethodStart >= 0);
        Assert.True(updatePowerIdleMethodEnd > updatePowerIdleMethodStart);
        var updatePowerIdleMethod = mainWindowSource[updatePowerIdleMethodStart..updatePowerIdleMethodEnd];
        Assert.DoesNotContain("PowerIdlePolicy.ShouldForceDisplayOffFallback(", updatePowerIdleMethod);
        Assert.DoesNotContain("TryForceDisplayOffFallback(", updatePowerIdleMethod);
        Assert.DoesNotContain("TryTurnDisplayOff(", mainWindowSource);
        Assert.DoesNotContain("display_off_fallback", mainWindowSource);
        Assert.Contains("SystemDisplayPower.TryTurnDisplayOn(_steamRawInputWindowHandle)", mainWindowSource);
        Assert.Contains("DisplayPowerCoordinator", mainWindowSource);
        Assert.Contains("_displayPowerCoordinator.Register(_steamRawInputWindowHandle)", mainWindowSource);
        Assert.Contains("DisplayPowerCoordinator_StateChanged", mainWindowSource);
        Assert.Contains("display_on_fallback_sent", mainWindowSource);
        Assert.Contains("ShouldSendDisplayWakeForInput(reason)", mainWindowSource);
        var displayStateChangedMethod = mainWindowSource[
            mainWindowSource.IndexOf("private void DisplayPowerCoordinator_StateChanged", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private void PowerIdleMonitorTimer_Tick", StringComparison.Ordinal)];
        Assert.Contains("_viewModel.MarkUserActivity();", displayStateChangedMethod);
        Assert.Contains("ArmNormalGamepadMonitoring(\"display_state_on\", requireActiveMode: false);", displayStateChangedMethod);
        Assert.True(
            displayStateChangedMethod.IndexOf("_viewModel.MarkUserActivity();", StringComparison.Ordinal) <
            displayStateChangedMethod.IndexOf("if (ShouldBypassWakeRecoveryAfterVerifiedInput(now))", StringComparison.Ordinal));
        Assert.Contains("VerifiedInputWakeRecoveryBypassDuration", mainWindowSource);
        Assert.Contains("_verifiedInputWakeRecoveryBypassUntilUtc", mainWindowSource);
        Assert.Contains("ArmWakeRecoveryBypassAfterVerifiedInput(reason)", mainWindowSource);
        Assert.Contains("ShouldBypassWakeRecoveryAfterVerifiedInput(now)", mainWindowSource);
        Assert.Contains("wake recovery skipped after verified gamepad input", mainWindowSource);
        Assert.Contains("DisplayWakeGuideToastHold", mainWindowSource);
        Assert.Contains("QueueBatteryGuideToastAfterDisplayWake(e)", mainWindowSource);
        Assert.Contains("FlushPendingDisplayWakeGuideToast(now)", mainWindowSource);
        Assert.Contains("guide_toast_deferred_until_display_wake", mainWindowSource);
        Assert.Contains("DisplayOffSettleWindow", mainWindowSource);
        Assert.Contains("_lastDisplayOffStateAtUtc", mainWindowSource);
        Assert.Contains("currentState is not (DisplayPowerState.Off or DisplayPowerState.Dimmed)", mainWindowSource);
        Assert.Contains("ApplyPowerIdleMonitorState(mode, shouldPause)", mainWindowSource);
        Assert.Contains("ApplyPowerIdleMonitorState(PowerIdleRuntimeMode.DisplayOffWakeOnly, shouldPause: true)", mainWindowSource);
        Assert.Contains("ApplyPowerIdleMonitorState(PowerIdleRuntimeMode.WakeRecovery, shouldPause: true)", mainWindowSource);
        Assert.Contains("StopPowerIdleGuideOnlyMonitor();", mainWindowSource);
        Assert.Contains("StartPowerIdleRawInputActivityMonitor();", mainWindowSource);
        Assert.Contains("ApplyPreDisplaySleepQuietMonitorState();", mainWindowSource);
        Assert.Contains("StartScreenOnXInputActivityMonitor();", mainWindowSource);
        Assert.Contains("StopNormalHidRawInputMonitors();", mainWindowSource);
        Assert.Contains("_viewModel.SetDisplaySleepPreparationActive(shouldPause);", mainWindowSource);
        Assert.Contains("_viewModel.SetDisplaySleepPreparationActive(true);", mainWindowSource);
        Assert.Contains("EnterPreDisplaySleepQuietMode();", mainWindowSource);
        Assert.Contains("private void EnterPreDisplaySleepQuietMode()", mainWindowSource);
        Assert.DoesNotContain("StartPreDisplaySleepGuideOnlyMonitors", mainWindowSource);
        var preDisplayQuietMethod = mainWindowSource[
            mainWindowSource.IndexOf("private void EnterPreDisplaySleepQuietMode()", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private void EnterExternalInputBlockedQuietMode()", StringComparison.Ordinal)];
        Assert.Contains("ApplyPreDisplaySleepQuietMonitorState();", preDisplayQuietMethod);
        Assert.DoesNotContain("ShouldRunPowerIdleGuideOnlyMonitoring(DateTimeOffset.UtcNow)", mainWindowSource);
        Assert.DoesNotContain("StartPowerIdleGuideOnlyMonitor();", preDisplayQuietMethod);
        Assert.Contains("now <= _normalGamepadMonitoringAllowedUntilUtc", mainWindowSource);
        Assert.Contains("StartPowerIdleXInputActivityMonitor();", mainWindowSource);
        Assert.Contains("StartPowerIdleRawInputActivityMonitor();", mainWindowSource);
        var preDisplaySleepQuietMethod = mainWindowSource[
            mainWindowSource.IndexOf("private void ApplyPreDisplaySleepQuietMonitorState()", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private void StartPowerIdleGuideOnlyMonitor()", StringComparison.Ordinal)];
        Assert.Contains("StopPowerIdleGuideOnlyMonitor();", preDisplaySleepQuietMethod);
        Assert.Contains("StopNormalInputMonitors();", preDisplaySleepQuietMethod);
        Assert.Contains("StopWakeOnlyInputMonitors();", preDisplaySleepQuietMethod);
        Assert.DoesNotContain("StartPowerIdleGuideOnlyMonitor();", preDisplaySleepQuietMethod);
        Assert.DoesNotContain("StartPowerIdleRawInputActivityMonitor();", preDisplaySleepQuietMethod);
        Assert.DoesNotContain("StartPowerIdleXInputActivityMonitor();", preDisplaySleepQuietMethod);
        var powerIdleXInputMethod = mainWindowSource[
            mainWindowSource.IndexOf("private void StartPowerIdleXInputActivityMonitor()", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private void StartPowerIdleRawInputActivityMonitor()", StringComparison.Ordinal)];
        Assert.Contains("_xInputActivityMonitor.StartWakeOnly();", powerIdleXInputMethod);
        Assert.DoesNotContain("_xInputActivityMonitor.Start();", powerIdleXInputMethod);
        var shouldRunNormalMethod = mainWindowSource[
            mainWindowSource.IndexOf("private bool ShouldRunNormalGamepadMonitoring", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private bool IsAnyNormalGamepadMonitorRunning", StringComparison.Ordinal)];
        Assert.Contains("_powerIdleMode == PowerIdleRuntimeMode.Active", shouldRunNormalMethod);
        Assert.Contains("now <= _normalGamepadMonitoringAllowedUntilUtc", shouldRunNormalMethod);
        Assert.DoesNotContain("return _powerIdleMode == PowerIdleRuntimeMode.Active ||", shouldRunNormalMethod);
        var activeModeBranch = mainWindowSource[
            mainWindowSource.IndexOf("case PowerIdleRuntimeMode.Active:", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("case PowerIdleRuntimeMode.PreDisplaySleep:", StringComparison.Ordinal)];
        Assert.Contains("StopNormalHidRawInputMonitors();", activeModeBranch);
        Assert.Contains("StartScreenOnXInputActivityMonitor();", activeModeBranch);
        Assert.DoesNotContain("ApplyPreDisplaySleepQuietMonitorState();", activeModeBranch);
        var startNormalInputMonitorsMethod = mainWindowSource[
            mainWindowSource.IndexOf("private void StartNormalInputMonitors()", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private void StartScreenOnXInputActivityMonitor()", StringComparison.Ordinal)];
        Assert.Contains("StopNormalHidRawInputMonitors();", startNormalInputMonitorsMethod);
        Assert.Contains("StartScreenOnXInputActivityMonitor();", startNormalInputMonitorsMethod);
        Assert.DoesNotContain("StopNormalInputMonitors();", startNormalInputMonitorsMethod);
        Assert.DoesNotContain("StopNormalInputMonitors(waitForExit: true);", preDisplayQuietMethod);
        Assert.DoesNotContain("StopWakeOnlyInputMonitors(waitForExit: true);", preDisplayQuietMethod);
        Assert.Contains("EnterExternalInputBlockedQuietMode();", mainWindowSource);
        Assert.Contains("private void EnterExternalInputBlockedQuietMode()", mainWindowSource);
        var externalInputBlockedQuietMethod = mainWindowSource[
            mainWindowSource.IndexOf("private void EnterExternalInputBlockedQuietMode()", StringComparison.Ordinal)..
            mainWindowSource.IndexOf("private void StopNormalInputMonitors(", StringComparison.Ordinal)];
        Assert.Contains("StopNormalInputMonitors(waitForExit: true);", externalInputBlockedQuietMethod);
        Assert.Contains("StopWakeOnlyInputMonitors(waitForExit: true);", externalInputBlockedQuietMethod);
        Assert.Contains("_guideButtonMonitor.SetPowerIdlePollingPaused(false)", mainWindowSource);
        Assert.Contains("_guideButtonMonitor.SetPowerIdlePollingPaused(true)", mainWindowSource);
        Assert.Contains("_guideButtonMonitor.Start();", mainWindowSource);
        Assert.Contains("ArmNormalGamepadMonitoring(\"startup\")", mainWindowSource);
        Assert.Contains("ArmNormalGamepadMonitoring(\"local_mouse_input\", requireActiveMode: false)", mainWindowSource);
        Assert.Contains("ArmNormalGamepadMonitoring(\"local_keyboard_input\", requireActiveMode: false)", mainWindowSource);
        Assert.DoesNotContain("StartPowerIdleHumanInputMonitor();", mainWindowSource);
        Assert.Contains("IsHumanInputOnlyMode", File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs")));
        Assert.Contains("BuildHumanInputOnlyRawInputDevices", File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs")));
        Assert.DoesNotContain("ArmNormalGamepadMonitoring(\"external_system_input\")", mainWindowSource);
        Assert.DoesNotContain("TryArmNormalMonitoringFromExternalSystemInput(systemIdle, now)", mainWindowSource);
        Assert.DoesNotContain("IsAnyDisplaySleepSensitiveNormalMonitorRunning()", mainWindowSource);
        Assert.Contains("PreviewKeyDown=\"Window_PreviewKeyDown\"", mainWindowXaml);
        Assert.DoesNotContain("ArmNormalGamepadMonitoring(\"display_on\")", mainWindowSource);
        Assert.DoesNotContain("ArmNormalGamepadMonitoringFromSystemInputIfNeeded(systemIdle, DateTimeOffset.UtcNow)", mainWindowSource);
        Assert.Contains("ShouldRunNormalGamepadMonitoring(DateTimeOffset.UtcNow)", mainWindowSource);
        Assert.Contains("_steamRawInputMonitor.Start(_steamRawInputWindowHandle)", mainWindowSource);
        Assert.Contains("_steamRawInputMonitor.StartWakeOnly(_steamRawInputWindowHandle)", mainWindowSource);
        Assert.Contains("_xInputActivityMonitor.StartWakeOnly();", mainWindowSource);
        Assert.Contains("!_xInputActivityMonitor.IsRunning || _xInputActivityMonitor.IsWakeOnlyMode", mainWindowSource);
        Assert.Contains("_xInputActivityMonitor.IsRunning && !_xInputActivityMonitor.IsWakeOnlyMode", mainWindowSource);
        Assert.Contains("_xInputActivityMonitor.IsRunning", mainWindowSource);
        Assert.Contains("GlobalHumanInputReceived += SteamRawInputMonitor_GlobalHumanInputReceived", mainWindowSource);
        Assert.Contains("GlobalHumanInputReceived -= SteamRawInputMonitor_GlobalHumanInputReceived", mainWindowSource);
        Assert.Contains("private void SteamRawInputMonitor_GlobalHumanInputReceived", mainWindowSource);
        Assert.Contains("ArmNormalGamepadMonitoring(e.Source, requireActiveMode: false)", mainWindowSource);
        Assert.Contains("_guideButtonMonitor.Stop();", mainWindowSource);
        Assert.Contains("_guideButtonMonitor.StopForPowerIdle();", mainWindowSource);
        Assert.Contains("_steamRawInputMonitor.Stop();", mainWindowSource);
        Assert.Contains("_xInputActivityMonitor.Stop();", mainWindowSource);
        Assert.Contains("_xInputActivityMonitor.StopForPowerIdle();", mainWindowSource);
        Assert.DoesNotContain("_steamRawInputMonitor.Start(_windowMessageSource.Handle);", mainWindowSource);
        Assert.DoesNotContain("_guideButtonMonitor.Start();\r\n        _xInputActivityMonitor.Start();", mainWindowSource);
        Assert.DoesNotContain("case PowerIdleRuntimeMode.ExternalInputBlocked:\r\n                _viewModel.SetDisplaySleepPreparationActive(false);", mainWindowSource);
        Assert.DoesNotContain("StartNormalInputMonitors(shouldPause: false);", mainWindowSource);
        Assert.Contains("display_sleep_external_input_suspected", mainWindowSource);
        Assert.Contains("display_sleep_external_input_blocked", mainWindowSource);
        Assert.Contains("PowerIdleDebugLog.Write(", mainWindowSource);
        Assert.Contains("GetRawInputModeForDiagnostics()", mainWindowSource);
        Assert.Contains("GetXInputModeForDiagnostics()", mainWindowSource);
        Assert.Contains("GetNormalGamepadMonitoringRemaining(now)", mainWindowSource);
        Assert.Contains("rawInputMode=", File.ReadAllText(Path.Combine(
            appRoot,
            "Services",
            "PowerIdleDebugLog.cs")));
        Assert.Contains("xInputMode=", File.ReadAllText(Path.Combine(
            appRoot,
            "Services",
            "PowerIdleDebugLog.cs")));
        Assert.Contains("normalMonitorRemaining=", File.ReadAllText(Path.Combine(
            appRoot,
            "Services",
            "PowerIdleDebugLog.cs")));
        Assert.Contains("IsRegistered", mainWindowSource);
        Assert.Contains("IsWakeOnlyMode", mainWindowSource);
        Assert.Contains("IsPowerIdlePollingPausedForDiagnostics", mainWindowSource);
        Assert.DoesNotContain("PausePowerIdleHidMonitoring()", mainWindowSource);
        Assert.DoesNotContain("ResumePowerIdleHidMonitoring()", mainWindowSource);
        Assert.DoesNotContain("PausePowerIdleInputMonitoring()", mainWindowSource);
        Assert.DoesNotContain("ResumePowerIdleInputMonitoring()", mainWindowSource);
        Assert.Contains("GetCurrentSleepTimeout", systemDisplayIdleTimeoutSource);
        Assert.Contains("GetCurrentDisplayOrSleepTimeout", systemDisplayIdleTimeoutSource);
        Assert.Contains("PowerWriteACValueIndex", systemDisplayIdleTimeoutSource);
        Assert.Contains("PowerWriteDCValueIndex", systemDisplayIdleTimeoutSource);
        Assert.Contains("PowerSetActiveScheme", systemDisplayIdleTimeoutSource);
        Assert.DoesNotContain(
            "SystemIdleMonitor.GetIdleDuration(),\r\n            isProbeRunning: false,\r\n            isRefreshRunning: false",
            mainWindowSource);
        Assert.DoesNotContain("_isPowerIdleInputMonitoringPaused", mainWindowSource);
    }

    [Fact]
    public void BatteryGuideTriggerCapture_OptionallyLoadsOriginalBlueprintImageAsset()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var projectFile = File.ReadAllText(Path.Combine(appRoot, "BluetoothBatteryWidget.App.csproj"));
        var assetPath = Path.Combine(appRoot, "Assets", "controller-guide-blueprint.png");
        var resetButtonAssetPath = Path.Combine(appRoot, "Assets", "reset-button-blue.png");

        Assert.Contains("<Resource Include=\"Assets\\controller-guide-blueprint.png\"", projectFile);
        Assert.Contains("<Resource Include=\"Assets\\reset-button-blue.png\"", projectFile);
        Assert.Contains("Assets\\controller-guide-blueprint.jpg", projectFile);
        Assert.Contains("Assets\\controller-guide-blueprint.jpeg", projectFile);
        Assert.Contains("CopyToOutputDirectory=\"PreserveNewest\"", projectFile);
        Assert.Contains("Condition=\"Exists('Assets\\controller-guide-blueprint.png')\"", projectFile);
        Assert.Contains(
            Path.Combine(AppContext.BaseDirectory, "Assets", "controller-guide-blueprint.png"),
            BatteryGuideTriggerCaptureWindow.GetOriginalBlueprintImageCandidatePaths());
        Assert.Contains(
            Path.Combine(AppContext.BaseDirectory, "Assets", "controller-guide-blueprint.jpg"),
            BatteryGuideTriggerCaptureWindow.GetOriginalBlueprintImageCandidatePaths());

        Assert.True(File.Exists(assetPath));
        using var stream = File.OpenRead(assetPath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.Equal(1448, decoder.Frames[0].PixelWidth);
        Assert.Equal(1086, decoder.Frames[0].PixelHeight);

        Assert.True(File.Exists(resetButtonAssetPath));
        using var resetStream = File.OpenRead(resetButtonAssetPath);
        var resetDecoder = BitmapDecoder.Create(resetStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.Equal(220, resetDecoder.Frames[0].PixelWidth);
        Assert.Equal(220, resetDecoder.Frames[0].PixelHeight);
    }

    [Fact]
    public void BatteryGuideTriggerCaptureWindow_LoadsOriginalBlueprintAndHighlightsCombo()
    {
        Exception? threadException = null;
        var imageLoaded = false;
        var imageHiddenAsRawBitmap = false;
        var transparentLineLayerVisible = false;
        var transparentLineSourceLoaded = false;
        var vectorHidden = false;
        var candidateText = string.Empty;
        var saveEnabled = false;
        var rbHighlight = string.Empty;
        var guideHighlight = string.Empty;
        var quickAccessHighlight = string.Empty;
        var rtHighlight = string.Empty;
        var rightPadAfterRt = string.Empty;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new BatteryGuideTriggerCaptureWindow();
                try
                {
                    imageLoaded =
                        window.OriginalBlueprintImage.Visibility == System.Windows.Visibility.Visible &&
                        window.OriginalBlueprintImage.Source is BitmapSource { PixelWidth: 1448, PixelHeight: 1086 };
                    imageHiddenAsRawBitmap = window.OriginalBlueprintImage.Opacity == 0d;
                    transparentLineLayerVisible = window.BlueprintImageLineLayer.Visibility == System.Windows.Visibility.Visible;
                    transparentLineSourceLoaded = window.BlueprintImageLineLayer.Source is BitmapSource
                    {
                        PixelWidth: 1448,
                        PixelHeight: 1086
                    };
                    vectorHidden = window.VectorBlueprintLayer.Visibility == System.Windows.Visibility.Collapsed;

                    var trigger = new BatteryGuideTrigger(
                        GuideButtonDeviceKind.SteamController,
                        0x45,
                        [new BatteryGuideTriggerBit(3, 0x02), new BatteryGuideTriggerBit(4, 0x01)],
                        "RB + Guide");
                    window.SetCandidate(trigger);

                    candidateText = window.CandidateTextBlock.Text;
                    saveEnabled = window.SaveButton.IsEnabled;
                    rbHighlight = window.RBKey.Background.ToString();
                    guideHighlight = window.GuideKey.Background.ToString();
                    quickAccessHighlight = window.QuickAccessKey.Background.ToString();

                    var rtTrigger = new BatteryGuideTrigger(
                        GuideButtonDeviceKind.SteamController,
                        0x45,
                        [new BatteryGuideTriggerBit(4, 0x80)],
                        "RT");
                    window.SetCandidate(rtTrigger);

                    rtHighlight = window.RTKey.Background.ToString();
                    rightPadAfterRt = window.RightPadKey.Background.ToString();
                }
                finally
                {
                    window.Close();
                    window.Dispatcher.InvokeShutdown();
                }
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.True(imageLoaded);
        Assert.True(imageHiddenAsRawBitmap);
        Assert.True(transparentLineLayerVisible);
        Assert.True(transparentLineSourceLoaded);
        Assert.True(vectorHidden);
        Assert.Equal("RB + Guide", candidateText);
        Assert.True(saveEnabled);
        Assert.Equal("#E61E78FF", rbHighlight);
        Assert.Equal("#E61E78FF", guideHighlight);
        Assert.Equal("#00000000", quickAccessHighlight);
        Assert.Equal("#E61E78FF", rtHighlight);
        Assert.Equal("#00000000", rightPadAfterRt);
    }

    [Fact]
    public void BatteryGuideTrigger_DebouncesSameSavedCombo()
    {
        var now = DateTimeOffset.Parse("2026-06-01T12:00:00+09:00");

        Assert.Equal(TimeSpan.FromMilliseconds(1500), MainWindow.GetCustomBatteryGuideTriggerToastCooldown(GuideButtonDeviceKind.DualSense));
        Assert.Equal(TimeSpan.FromMilliseconds(3000), MainWindow.GetCustomBatteryGuideTriggerToastCooldown(GuideButtonDeviceKind.SteamController));
        Assert.False(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(null, now));
        Assert.True(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(now, now.AddMilliseconds(500)));
        Assert.True(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(now, now.AddMilliseconds(1000)));
        Assert.False(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(now, now.AddMilliseconds(1501)));
        Assert.True(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(
            now,
            now.AddMilliseconds(2500),
            GuideButtonDeviceKind.SteamController));
        Assert.False(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(
            now,
            now.AddMilliseconds(3001),
            GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void BatteryGuideTrigger_OnlyShowsOnceWhileSameSavedComboIsHeldAcrossInputPaths()
    {
        const string bindingKey = "SteamController:45:03:02,04:01";
        var pressedReportKeysByBinding = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            "SteamController:RAW:RID_45",
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            "SteamController:HID:RID_45",
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            "SteamController:RAW:RID_45",
            isPressed: false,
            hasAnyTriggerBitPressed: false,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            "SteamController:HID:RID_45",
            isPressed: false,
            hasAnyTriggerBitPressed: false,
            pressedReportKeysByBinding));
        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            "SteamController:RAW:RID_45",
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
    }

    [Fact]
    public void BatteryGuideTrigger_OnlyShowsOnceWhileSameSavedComboIsHeldOnSameInputPath()
    {
        const string bindingKey = "SteamController:45:03:02,04:01";
        const string reportKey = "SteamController:RAW:RID_45";
        var pressedReportKeysByBinding = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: false,
            hasAnyTriggerBitPressed: false,
            pressedReportKeysByBinding));
        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
    }

    [Fact]
    public void BatteryGuideTrigger_PartialComboReportDoesNotResetHeldSteamCombo()
    {
        const string bindingKey = "SteamController:45:03:40,04:80";
        const string reportKey = "SteamController:STEAMCON:RID_45";
        var pressedReportKeysByBinding = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: false,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: false,
            hasAnyTriggerBitPressed: false,
            pressedReportKeysByBinding));
        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            reportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
    }

    [Fact]
    public void BatteryGuideTrigger_SteamRbGuideReportSequenceShowsOncePerPhysicalPress()
    {
        var neutral = new byte[54];
        neutral[0] = 0x45;
        var pressed = neutral.ToArray();
        pressed[3] = 0x02;
        pressed[4] = 0x01;

        Assert.True(BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            neutral,
            pressed,
            out var trigger));
        Assert.Equal("RB + Guide", trigger.DisplayName);

        const string bindingKey = "SteamController:45:03:02,04:01";
        const string rawReportKey = "SteamController:RAW:RID_45";
        const string hidReportKey = "SteamController:HID:RID_45";
        var pressedReportKeysByBinding = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, pressed));
        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            rawReportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            rawReportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            hidReportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));

        Assert.False(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, neutral));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            rawReportKey,
            isPressed: false,
            hasAnyTriggerBitPressed: false,
            pressedReportKeysByBinding));
        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            hidReportKey,
            isPressed: false,
            hasAnyTriggerBitPressed: false,
            pressedReportKeysByBinding));

        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            rawReportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
    }

    [Fact]
    public void BatteryGuideTrigger_CooldownSuppressesLateSecondInputPathAfterEarlyRelease()
    {
        const string bindingKey = "SteamController:45:03:02,04:01";
        const string rawReportKey = "SteamController:RAW:RID_45";
        const string hidReportKey = "SteamController:HID:RID_45";
        var pressedReportKeysByBinding = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var firstShownAt = DateTimeOffset.Parse("2026-06-01T12:00:00+09:00");

        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            rawReportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));

        Assert.False(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            rawReportKey,
            isPressed: false,
            hasAnyTriggerBitPressed: false,
            pressedReportKeysByBinding));

        Assert.True(MainWindow.ShouldShowCustomBatteryGuideTriggerOnStateChange(
            bindingKey,
            hidReportKey,
            isPressed: true,
            hasAnyTriggerBitPressed: true,
            pressedReportKeysByBinding));
        Assert.True(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(
            firstShownAt,
            firstShownAt.AddMilliseconds(500),
            GuideButtonDeviceKind.SteamController));
        Assert.True(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(
            firstShownAt,
            firstShownAt.AddMilliseconds(2500),
            GuideButtonDeviceKind.SteamController));

        Assert.False(MainWindow.ShouldSuppressCustomBatteryGuideTriggerToast(
            firstShownAt,
            firstShownAt.AddMilliseconds(3001),
            GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void BatteryGuideTrigger_CustomBindingOwnsDefaultGuidePressPath()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        var guidePressedMethod = source[
            source.IndexOf("private void GuideButtonMonitor_GuideButtonPressed", StringComparison.Ordinal)..
            source.IndexOf("private void GuideButtonMonitor_InputReportReceived", StringComparison.Ordinal)];

        Assert.Contains("if (_viewModel.HasCustomBatteryGuideTriggerForDevice(e.DeviceKind))", guidePressedMethod);
        Assert.Contains("return;", guidePressedMethod);
        Assert.DoesNotContain("ShowBatteryGuide(e)", guidePressedMethod[
            guidePressedMethod.IndexOf("if (_viewModel.HasCustomBatteryGuideTriggerForDevice(e.DeviceKind))", StringComparison.Ordinal)..
            guidePressedMethod.IndexOf("if (ShouldSuppressSecondarySteamGuideButtonPath", StringComparison.Ordinal)]);

        var secondaryFallbackMethod = source[
            source.IndexOf("private async Task CompleteSteamSecondaryGuideFallbackAsync", StringComparison.Ordinal)..
            source.IndexOf("private void CancelPendingSteamSecondaryGuideFallbackLocked", StringComparison.Ordinal)];

        var suppressionIndex = secondaryFallbackMethod.IndexOf("ShouldSuppressSteamSecondaryFallbackForCustomBatteryGuideTrigger(e)", StringComparison.Ordinal);
        var acceptedIndex = secondaryFallbackMethod.IndexOf("\"secondary_fallback_accepted\"", StringComparison.Ordinal);

        Assert.True(suppressionIndex >= 0);
        Assert.True(acceptedIndex >= 0);
        Assert.True(suppressionIndex < acceptedIndex);
        Assert.Contains("secondary_fallback_custom_trigger_suppressed", source);
        Assert.Contains("TryGetBatteryGuideTriggerForDevice(e.DeviceKind", source);
        Assert.Contains("SetBatteryGuideTriggerProfile(", source);
        Assert.True(MainWindow.ShouldCustomBatteryGuideTriggerOwnSteamSecondaryFallback(true));
        Assert.False(MainWindow.ShouldCustomBatteryGuideTriggerOwnSteamSecondaryFallback(false));
    }

    [Fact]
    public void GamepadInput_MarksLocalActivityBeforeGuideSuppressionPaths()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        var guidePressedMethod = source[
            source.IndexOf("private void GuideButtonMonitor_GuideButtonPressed", StringComparison.Ordinal)..
            source.IndexOf("private void GuideButtonMonitor_InputReportReceived", StringComparison.Ordinal)];
        var inputReportMethod = source[
            source.IndexOf("private void GuideButtonMonitor_InputReportReceived", StringComparison.Ordinal)..
            source.IndexOf("private void GuideButtonHidMonitor_InputActivityReceived", StringComparison.Ordinal)];
        var hidInputActivityMethod = source[
            source.IndexOf("private void GuideButtonHidMonitor_InputActivityReceived", StringComparison.Ordinal)..
            source.IndexOf("private void GuideButtonMonitor_InputActivityReceived", StringComparison.Ordinal)];
        var inputActivityMethod = source[
            source.IndexOf("private void GuideButtonMonitor_InputActivityReceived", StringComparison.Ordinal)..
            source.IndexOf("private void XInputActivityMonitor_InputActivityReceived", StringComparison.Ordinal)];
        var xInputActivityMethod = source[
            source.IndexOf("private void XInputActivityMonitor_InputActivityReceived", StringComparison.Ordinal)..
            source.IndexOf("private void HandleBatteryGuideInputReport", StringComparison.Ordinal)];
        var shouldTreatInputReportMethod = source[
            source.IndexOf("private bool ShouldTreatInputReportAsIntentionalGamepadActivity", StringComparison.Ordinal)..
            source.IndexOf("private bool TryHandleDefaultGuideButtonToastFromInputReport", StringComparison.Ordinal)];
        var defaultGuideInputReportMethod = source[
            source.IndexOf("private bool TryHandleDefaultGuideButtonToastFromInputReport", StringComparison.Ordinal)..
            source.IndexOf("private void WriteGamepadActivityDiagnosticIfNeeded", StringComparison.Ordinal)];

        Assert.Contains(
            "_steamRawInputMonitor.InputActivityReceived += GuideButtonMonitor_InputActivityReceived",
            source);
        Assert.Contains(
            "_guideButtonMonitor.InputActivityReceived += GuideButtonHidMonitor_InputActivityReceived",
            source);
        Assert.Contains(
            "_steamRawInputMonitor.InputActivityReceived -= GuideButtonMonitor_InputActivityReceived",
            source);
        Assert.Contains(
            "_guideButtonMonitor.InputActivityReceived -= GuideButtonHidMonitor_InputActivityReceived",
            source);
        Assert.Contains("_xInputActivityMonitor.Start();", source);
        Assert.Contains(
            "_xInputActivityMonitor.InputActivityReceived += XInputActivityMonitor_InputActivityReceived",
            source);
        Assert.Contains(
            "_xInputActivityMonitor.InputActivityReceived -= XInputActivityMonitor_InputActivityReceived",
            source);
        Assert.Contains("_xInputActivityMonitor.Dispose();", source);
        Assert.Contains("WriteGamepadActivityDiagnosticIfNeeded(", source);
        Assert.Contains("\"hid_button_input\"", source);
        Assert.Contains("\"hid_state_telemetry\"", source);
        Assert.Contains("\"steam_raw_input_telemetry\"", source);
        Assert.Contains("\"xinput_button_input\"", source);
        Assert.Contains("\"xinput_telemetry\"", source);
        Assert.DoesNotContain("\"hid_input_activity\"", source);
        Assert.Contains("\"xinput\"", File.ReadAllText(Path.Combine(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "BluetoothBatteryWidget.App")),
            "Services",
            "XInputActivityMonitorService.cs")));
        Assert.Contains("ShowBatteryGuideAfterGamepadActivityRefreshAsync(e)", guidePressedMethod);
        Assert.DoesNotContain("RequestRefreshAfterGamepadActivity();", guidePressedMethod);
        Assert.Contains("MarkIntentionalGamepadInputAndExitQuietMode(\"guide_button_press\")", guidePressedMethod);
        Assert.Contains("ShouldSuppressGuideButtonPressAfterInputReportFallback(e)", guidePressedMethod);
        Assert.Contains("ShouldTreatInputReportAsIntentionalGamepadActivity(e)", inputReportMethod);
        Assert.Contains("TryHandleDefaultGuideButtonToastFromInputReport(e)", inputReportMethod);
        Assert.True(
            inputReportMethod.IndexOf("ShouldTreatInputReportAsIntentionalGamepadActivity(e)", StringComparison.Ordinal) <
            inputReportMethod.IndexOf("MarkIntentionalGamepadInputAndExitQuietMode(\"hid_button_input\")", StringComparison.Ordinal));
        Assert.True(
            inputReportMethod.IndexOf("MarkIntentionalGamepadInputAndExitQuietMode(\"hid_button_input\")", StringComparison.Ordinal) <
            inputReportMethod.IndexOf("TryWakeDisplayAfterVerifiedInput(\"hid_button_input\")", StringComparison.Ordinal));
        Assert.True(
            inputReportMethod.IndexOf("TryWakeDisplayAfterVerifiedInput(\"hid_button_input\")", StringComparison.Ordinal) <
            inputReportMethod.IndexOf("RequestRefreshAfterGamepadActivity();", StringComparison.Ordinal));
        Assert.Contains("if (!isDisplayOffWake)", inputReportMethod);
        Assert.DoesNotContain("_viewModel.MarkGamepadTelemetryActivity();", inputReportMethod);
        Assert.DoesNotContain("TryWakeDisplayAfterVerifiedInput(\"hid_button_input\");\r\n                }\r\n                else", inputReportMethod);
        Assert.True(
            inputReportMethod.IndexOf("RequestRefreshAfterGamepadActivity();", StringComparison.Ordinal) <
            inputReportMethod.IndexOf("HandleBatteryGuideInputReport(e);", StringComparison.Ordinal));
        Assert.Contains("_viewModel.MarkGamepadTelemetryActivity();", hidInputActivityMethod);
        Assert.Contains("RequestTelemetryRefreshIfAllowed();", hidInputActivityMethod);
        Assert.Contains("TryWakeDisplayAfterVerifiedInput(\"steam_raw_input_activity\")", inputActivityMethod);
        Assert.Contains("MarkIntentionalGamepadInputAndExitQuietMode(eventName);", xInputActivityMethod);
        Assert.Contains("_viewModel.MarkGamepadTelemetryActivity();", xInputActivityMethod);
        Assert.Contains("var countsAsUserActivity = e.CountsAsUserActivity || e.HasStick;", xInputActivityMethod);
        Assert.DoesNotContain("e.HasStick && !isDisplayOffWake", xInputActivityMethod);
        Assert.True(
            xInputActivityMethod.IndexOf("MarkIntentionalGamepadInputAndExitQuietMode(eventName);", StringComparison.Ordinal) <
            xInputActivityMethod.IndexOf("TryWakeDisplayAfterVerifiedInput(eventName)", StringComparison.Ordinal));
        Assert.Contains("if (e.IsWakeEligible)", xInputActivityMethod);
        Assert.Contains("TryWakeDisplayAfterVerifiedInput(eventName)", xInputActivityMethod);
        Assert.Contains("RequestRefreshAfterGamepadActivity();", xInputActivityMethod);
        Assert.Contains("RequestTelemetryRefreshIfAllowed();", xInputActivityMethod);
        Assert.Contains("GamepadActivityRefreshCooldown", source);
        Assert.Contains("TelemetryPowerIdleUpdateCooldown", source);
        Assert.Contains("_lastTelemetryPowerIdleUpdateAtUtc", source);
        Assert.Contains("UpdatePowerIdleGuideMonitoring();", source);
        Assert.Contains("private void MarkIntentionalGamepadInputAndExitQuietMode", source);
        Assert.Contains("private void NotifyDisplayIdleTimerAfterVerifiedGamepadInput", source);
        Assert.Contains("GamepadDisplayIdlePulseCooldown", source);
        Assert.Contains("SystemDisplayPower.TryNotifyDisplayUserActivity()", source);
        Assert.Contains("display_idle_timer_pulsed", source);
        Assert.Contains("private void ArmNormalGamepadMonitoring(string reason, bool requireActiveMode = true)", source);
        Assert.Contains("_naturalSleepRetryBlockedUntilUtc = DateTimeOffset.MinValue;", source);
        Assert.DoesNotContain("_naturalSleepRetryBlockedUntilUtc = DateTimeOffset.UtcNow + QuietModeIntentionalInputCooldown", source);
        Assert.Contains("intentional_gamepad_input", source);
        Assert.Contains("private async Task ShowBatteryGuideAfterGamepadActivityRefreshAsync", source);
        Assert.Contains("FindGuideButtonDevice(e) is null", source);
        Assert.Contains("RefreshAfterGamepadActivityAsync(force: true)", source);
        Assert.Contains("_lastPowerIdleInputReportByDevice", source);
        Assert.Contains("_lastDefaultGuideButtonPressedByInputReport", source);
        Assert.Contains("_inputReportGuideFallbackSuppressRegularUntilByDevice", source);
        Assert.DoesNotContain("GuideButtonReportParser.TryParseGuideButton(e.DeviceKind, e.Report", shouldTreatInputReportMethod);
        Assert.Contains("BatteryGuideTriggerParser.HasButtonDownEdgeForPowerIdleActivity(", shouldTreatInputReportMethod);
        Assert.DoesNotContain("BatteryGuideTriggerParser.CreateNeutralReportForCapture(e.DeviceKind, e.Report)", shouldTreatInputReportMethod);
        Assert.DoesNotContain("BatteryGuideTriggerParser.TryCapture(e.DeviceKind, previousReport, e.Report, out _)", shouldTreatInputReportMethod);
        Assert.DoesNotContain("BatteryGuideTriggerParser.TryCaptureButtonsOnly(e.DeviceKind, previousReport, e.Report, out _)", shouldTreatInputReportMethod);
        Assert.Contains("ShouldTreatInputReportAsDefaultGuideButtonPress(e)", defaultGuideInputReportMethod);
        Assert.Contains("GuideButtonReportParser.TryParseGuideButton(e.DeviceKind, e.Report, out var isPressed)", defaultGuideInputReportMethod);
        Assert.Contains("GuideButtonGesture.ShortPress", defaultGuideInputReportMethod);
        Assert.Contains("guide_button_input_report", defaultGuideInputReportMethod);
        Assert.Contains("ShowBatteryGuideAfterGamepadActivityRefreshAsync(args)", defaultGuideInputReportMethod);
        Assert.Contains("MarkGuideButtonInputReportFallbackHandled(args)", defaultGuideInputReportMethod);
        Assert.Contains("input_report_fallback_press_suppressed", source);
        Assert.DoesNotContain("if (e.DeviceKind == GuideButtonDeviceKind.DualSense)\r\n        {\r\n            return true;\r\n        }", source);

        var markIntentionalMethod = source[
            source.IndexOf("private void MarkIntentionalGamepadInputAndExitQuietMode", StringComparison.Ordinal)..
            source.IndexOf("private void SteamRawInputMonitor_GlobalHumanInputReceived", StringComparison.Ordinal)];
        Assert.Contains("NotifyDisplayIdleTimerAfterVerifiedGamepadInput(reason);", markIntentionalMethod);
        Assert.True(
            markIntentionalMethod.IndexOf("_viewModel.MarkIntentionalGamepadActivity();", StringComparison.Ordinal) <
            markIntentionalMethod.IndexOf("NotifyDisplayIdleTimerAfterVerifiedGamepadInput(reason);", StringComparison.Ordinal));
        Assert.True(
            markIntentionalMethod.IndexOf("NotifyDisplayIdleTimerAfterVerifiedGamepadInput(reason);", StringComparison.Ordinal) <
            markIntentionalMethod.IndexOf("ArmNormalGamepadMonitoring(reason, requireActiveMode: false);", StringComparison.Ordinal));
        Assert.Contains("ArmNormalGamepadMonitoring(reason, requireActiveMode: false);", markIntentionalMethod);
        Assert.True(
            markIntentionalMethod.IndexOf("ArmNormalGamepadMonitoring(reason, requireActiveMode: false);", StringComparison.Ordinal) <
            markIntentionalMethod.IndexOf("ApplyPowerIdleMonitorState(PowerIdleRuntimeMode.Active, shouldPause: false)", StringComparison.Ordinal));
    }

    [Fact]
    public void GamepadInputReports_AreForwardedFromHidAndSteamRawInputMonitors()
    {
        var appRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App"));
        var guideMonitorSource = File.ReadAllText(Path.Combine(appRoot, "Services", "GuideButtonMonitorService.cs"));
        var steamRawInputSource = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));
        var keyboardInputMethod = steamRawInputSource[
            steamRawInputSource.IndexOf("private void ProcessKeyboardInput", StringComparison.Ordinal)..
            steamRawInputSource.IndexOf("private void ProcessMouseInput", StringComparison.Ordinal)];
        var mouseInputMethod = steamRawInputSource[
            steamRawInputSource.IndexOf("private void ProcessMouseInput", StringComparison.Ordinal)..
            steamRawInputSource.IndexOf("private void ProcessHidInput", StringComparison.Ordinal)];
        var suppressedHidBlock = steamRawInputSource[
            steamRawInputSource.IndexOf("if (ShouldSuppressGuideInputReport(device, report))", StringComparison.Ordinal)..
            steamRawInputSource.IndexOf("InputReportReceived?.Invoke(", StringComparison.Ordinal)];

        Assert.Contains("InputReportReceived?.Invoke(", guideMonitorSource);
        Assert.Contains("new GuideButtonInputReportEventArgs(", guideMonitorSource);
        Assert.Contains("InputActivityReceived?.Invoke(", guideMonitorSource);
        Assert.Contains("RaiseInputActivityReceived(endpoint);", guideMonitorSource);
        Assert.Contains("SetPowerIdlePollingPaused(bool isPaused)", guideMonitorSource);
        Assert.Contains("!IsPowerIdlePollingPaused()", guideMonitorSource);
        Assert.Contains("out var timedOut", guideMonitorSource);
        Assert.Contains("if (!timedOut)", guideMonitorSource);
        Assert.Contains("\"monitor_read_failed\"", guideMonitorSource);
        Assert.Contains("\"monitor_idle_quiet\"", guideMonitorSource);
        Assert.Contains("staying open and waiting", guideMonitorSource);
        Assert.DoesNotContain("\"monitor_idle_timeout\"", guideMonitorSource);
        Assert.Contains("InputReportReceived?.Invoke(", steamRawInputSource);
        Assert.Contains("GuideButtonDeviceKind.SteamController", steamRawInputSource);
        Assert.Contains("new GuideButtonInputReportEventArgs(", steamRawInputSource);
        Assert.Contains("InputActivityReceived?.Invoke(", steamRawInputSource);
        Assert.Contains("GlobalHumanInputReceived?.Invoke(", steamRawInputSource);
        Assert.Contains("TryCreateGlobalHumanInputEvent(buffer, headerSize, header.Type, out var globalInput)", steamRawInputSource);
        Assert.Contains("!isSteamCandidate", steamRawInputSource);
        Assert.Contains("!isWakeOnly", steamRawInputSource);
        Assert.Contains("StartWakeOnly(IntPtr windowHandle)", steamRawInputSource);
        Assert.Contains("BuildWakeOnlyRawInputDevices", steamRawInputSource);
        Assert.True(
            steamRawInputSource.IndexOf("TryCreateGlobalHumanInputEvent(buffer, headerSize, header.Type, out var globalInput)", StringComparison.Ordinal) <
            steamRawInputSource.IndexOf("if (!TryResolveSteamDevice(deviceName", StringComparison.Ordinal));
        Assert.True(
            steamRawInputSource.IndexOf("var isSteamCandidate = IsSteamCandidateDeviceName(deviceName, knownSteamDevices)", StringComparison.Ordinal) <
            steamRawInputSource.IndexOf("TryCreateGlobalHumanInputEvent(buffer, headerSize, header.Type, out var globalInput)", StringComparison.Ordinal));
        Assert.Contains("TryGetHidInputActivity(device, report, out var countsAsUserActivity, out var isWakeEligible)", steamRawInputSource);
        Assert.Contains("BatteryGuideTriggerParser.TryCaptureButtonsOnly(", steamRawInputSource);
        Assert.Contains("raw_keyboard_seen", steamRawInputSource);
        Assert.Contains("raw_mouse_button_seen", steamRawInputSource);
        Assert.Contains("raw_mouse_move_seen", steamRawInputSource);
        Assert.DoesNotContain("ShouldRaiseMouseMovementActivity(device, mouse.LastX, mouse.LastY)", mouseInputMethod);
        Assert.DoesNotContain("ComputeRawMouseMovementDistance(int deltaX, int deltaY)", steamRawInputSource);
        Assert.DoesNotContain("RaiseInputActivityReceived(device);", keyboardInputMethod);
        Assert.DoesNotContain("RaiseInputActivityReceived(device);", mouseInputMethod);
        Assert.DoesNotContain("RaiseInputActivityReceived(device);", suppressedHidBlock);
        Assert.Contains("new GuideButtonKnownDevice(", steamRawInputSource);
        Assert.DoesNotContain("IsSteamRawInputVidPid(vendorId, productId) &&\r\n            knownDevices.Count == 1", steamRawInputSource);
    }

    [Fact]
    public void SteamControllerGuideToast_SuppressesConnectionAndRefreshCarryoverSignals()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("SteamGuideToastConnectionSuppressDuration", source);
        Assert.Contains("SteamGuideToastRefreshSuppressDuration", source);
        Assert.Contains("RefreshFromUserCommandAsync()", source);
        Assert.Contains("SuppressSteamGuideToasts(SteamGuideToastRefreshSuppressDuration, \"manual_refresh_started\")", source);
        Assert.Contains("_viewModel.RefreshAsync(forceFullRefresh: true)", source);
        Assert.Contains("SuppressSteamGuideToasts(SteamGuideToastRefreshSuppressDuration, \"manual_refresh_completed\")", source);
        Assert.Contains("SuppressGuideInputForKnownDevices(duration, reason)", source);
        Assert.Contains("steam_battery_toast_refresh_suppressed", source);
        Assert.Contains("() => Dispatcher.Invoke(() => _ = RefreshFromUserCommandAsync())", source);
        Assert.Contains("UpdateSteamGuideConnectionSuppressState(item, \"steam_device_added\")", source);
        Assert.Contains("ShouldSuppressSteamGuideToast(e.DeviceKind, e.Address, e.DisplayName, \"guide_button_press\")", source);
        Assert.Contains("ShouldSuppressSteamGuideToast(e.DeviceKind, e.Address, e.DisplayName, \"custom_trigger_input\")", source);
        Assert.Contains("ShouldSuppressSteamGuideToast(e.DeviceKind, e.Address, e.DisplayName, \"secondary_fallback\")", source);
        Assert.Contains("steam_guide_toast_refresh_suppressed", source);
        Assert.Contains("steam_guide_toast_startup_suppressed", source);
    }

    [Fact]
    public void AutomaticBatteryToast_WritesDiagnosticEventForManualIdleVerification()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        var method = source[
            source.IndexOf("private void CheckLowBatteryToast", StringComparison.Ordinal)..
            source.IndexOf("private bool ShouldSuppressSteamBatteryToastDuringSettling", StringComparison.Ordinal)];

        var toastIndex = method.IndexOf("ShowBatteryToast(item.Snapshot, automatic: true);", StringComparison.Ordinal);
        var logIndex = method.IndexOf("\"automatic_battery_toast_shown\"", StringComparison.Ordinal);

        Assert.True(toastIndex >= 0);
        Assert.True(logIndex > toastIndex);
        Assert.Contains("item.Category.ToString()", method);
        Assert.Contains("item.Address", method);
        Assert.Contains("item.DisplayName", method);
        Assert.Contains("threshold={targetThreshold}", method);
    }

    [Fact]
    public void BatteryGuideTrigger_CustomBindingLogsCaptureAndToastWithoutAddress()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        var captureLogIndex = source.IndexOf("\"custom_trigger_capture_candidate\"", StringComparison.Ordinal);
        var toastLogIndex = source.IndexOf("\"custom_trigger_toast_shown\"", StringComparison.Ordinal);

        Assert.True(captureLogIndex >= 0);
        Assert.True(toastLogIndex >= 0);

        var captureLogBlock = source[captureLogIndex..Math.Min(source.Length, captureLogIndex + 520)];
        var toastLogBlock = source[toastLogIndex..Math.Min(source.Length, toastLogIndex + 420)];

        Assert.Contains("string.Empty", captureLogBlock);
        Assert.Contains("captured.DisplayName", captureLogBlock);
        Assert.Contains("FormatBatteryGuideTriggerBits(captured)", captureLogBlock);
        Assert.DoesNotContain("e.Address", captureLogBlock);
        Assert.Contains("string.Empty", toastLogBlock);
        Assert.Contains("trigger.DisplayName", toastLogBlock);
        Assert.DoesNotContain("e.Address", toastLogBlock);
    }

    [Fact]
    public void BatteryGuideTriggerCapture_KeepsLargerComboWhenReportsArriveOutOfOrder()
    {
        var combo = new BatteryGuideTrigger(
            GuideButtonDeviceKind.SteamController,
            0x45,
            [new BatteryGuideTriggerBit(3, 0x02), new BatteryGuideTriggerBit(4, 0x01)],
            "RB + Guide");
        var single = new BatteryGuideTrigger(
            GuideButtonDeviceKind.SteamController,
            0x45,
            [new BatteryGuideTriggerBit(4, 0x01)],
            "Guide");

        Assert.True(MainWindow.ShouldReplacePendingBatteryGuideTriggerCapture(null, single));
        Assert.True(MainWindow.ShouldReplacePendingBatteryGuideTriggerCapture(single, combo));
        Assert.False(MainWindow.ShouldReplacePendingBatteryGuideTriggerCapture(combo, single));
    }

    [Fact]
    public void BatteryAlertThresholdSettings_OpenSelectionWindowAndKeepForcedFifteenPercentAlert()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var coreRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.Core");
        var mainWindowXaml = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml"));
        var mainWindowSource = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml.cs"));
        var thresholdWindowXaml = File.ReadAllText(Path.Combine(appRoot, "BatteryAlertThresholdsWindow.xaml"));
        var thresholdWindowSource = File.ReadAllText(Path.Combine(appRoot, "BatteryAlertThresholdsWindow.xaml.cs"));
        var popInAnimatorSource = File.ReadAllText(Path.Combine(appRoot, "WindowPopInAnimator.cs"));
        var viewModelSource = File.ReadAllText(Path.Combine(appRoot, "ViewModels", "MainViewModel.cs"));
        var settingsSource = File.ReadAllText(Path.Combine(coreRoot, "Models", "WidgetSettings.cs"));

        Assert.Contains("BatteryAlertThresholdsRowGrid", mainWindowXaml);
        Assert.Contains("BatteryAlertThresholdsButton", mainWindowXaml);
        Assert.Contains("BatteryAlertThresholdsButton_Click", mainWindowXaml);
        Assert.DoesNotContain("BatteryAlertThresholdsTextBox", mainWindowXaml);
        Assert.DoesNotContain("BatteryAlertThresholdsTextBox_LostFocus", mainWindowSource);
        Assert.Contains("new BatteryAlertThresholdsWindow(_viewModel.BatteryAlertThresholds, _viewModel.Language)", mainWindowSource);
        Assert.Contains("ForcedThresholdCheckBox", thresholdWindowXaml);
        Assert.Contains("HeadingTextBlock", thresholdWindowXaml);
        Assert.Contains("DescriptionLine1Run", thresholdWindowXaml);
        Assert.Contains("DescriptionLine2Run", thresholdWindowXaml);
        Assert.Contains("Title=\"자동 알림 설정\"", thresholdWindowXaml);
        Assert.Contains("WindowStartupLocation=\"Manual\"", thresholdWindowXaml);
        Assert.Contains("WindowStyle=\"None\"", thresholdWindowXaml);
        Assert.Contains("AllowsTransparency=\"True\"", thresholdWindowXaml);
        Assert.Contains("Loaded=\"Window_Loaded\"", thresholdWindowXaml);
        Assert.Contains("x:Name=\"WindowSurface\"", thresholdWindowXaml);
        Assert.Contains("x:Name=\"WindowSurfaceScale\"", thresholdWindowXaml);
        Assert.Contains("x:Name=\"WindowSurfaceSkew\"", thresholdWindowXaml);
        Assert.Contains("x:Name=\"WindowSurfaceTranslate\"", thresholdWindowXaml);
        Assert.Contains("RenderTransformOrigin=\"0.5,0.5\"", thresholdWindowXaml);
        Assert.Contains("AlertChipToggleButtonStyle", thresholdWindowXaml);
        Assert.Contains("AlertPrimaryButtonStyle", thresholdWindowXaml);
        Assert.Contains("15%는 항상 울립니다.", thresholdWindowXaml);
        Assert.Contains("<LineBreak />", thresholdWindowXaml);
        Assert.Contains("추가 알림은 30~80% 사이에서 여러 개 선택할 수 있습니다.", thresholdWindowXaml);
        Assert.Contains("ApplyLocalizedText", thresholdWindowSource);
        Assert.Contains("BatteryAlertThresholdsWindowTitle", thresholdWindowSource);
        Assert.Contains("BatteryAlertThresholdsDescriptionLine1", thresholdWindowSource);
        Assert.Contains("BatteryAlertThresholdsDescriptionLine2", thresholdWindowSource);
        Assert.Contains("ChipRootScale", thresholdWindowXaml);
        Assert.Contains("ChipClickPulse", thresholdWindowXaml);
        Assert.Contains("ChipClickPulseScale", thresholdWindowXaml);
        Assert.DoesNotContain("ChipCirclePulse", thresholdWindowXaml);
        Assert.DoesNotContain("ChipClickSpark", thresholdWindowXaml);
        Assert.DoesNotContain("ChipCirclePulseLoop", thresholdWindowXaml);
        Assert.DoesNotContain("RepeatBehavior=\"Forever\"", thresholdWindowXaml);
        Assert.DoesNotContain("AutoReverse=\"True\"", thresholdWindowXaml);
        Assert.Contains("Background=\"#4F95FF\"", thresholdWindowXaml);
        Assert.Contains("Color=\"#4F95FF\"", thresholdWindowXaml);
        Assert.Contains("BorderBrush=\"#1018222C\"", thresholdWindowXaml);
        Assert.Contains("Property=\"Background\" Value=\"#BFE7FF\"", thresholdWindowXaml);
        Assert.Contains("Property=\"BorderBrush\" Value=\"#BFE7FF\"", thresholdWindowXaml);
        Assert.Contains("Property=\"Foreground\" Value=\"#0B243B\"", thresholdWindowXaml);
        Assert.DoesNotContain("Value=\"#2941606F\"", thresholdWindowXaml);
        Assert.DoesNotContain("BorderBrush=\"#CDE7FFFF\"", thresholdWindowXaml);
        Assert.DoesNotContain("Value=\"#CDE7FFFF\"", thresholdWindowXaml);
        Assert.DoesNotContain("Value=\"#7FDDF3FF\"", thresholdWindowXaml);
        Assert.DoesNotContain("ChipWaterFill", thresholdWindowXaml);
        Assert.DoesNotContain("ThresholdWaterFillHeight", thresholdWindowSource);
        Assert.Contains("ThresholdToggle_Click", thresholdWindowSource);
        Assert.Contains("checkBox.Click += ThresholdToggle_Click", thresholdWindowSource);
        Assert.DoesNotContain("ThresholdToggle_CheckedChanged", thresholdWindowSource);
        Assert.DoesNotContain("checkBox.Checked += ThresholdToggle_CheckedChanged", thresholdWindowSource);
        Assert.Contains("PopInOriginScreenPoint", thresholdWindowSource);
        Assert.Contains("WindowSurfaceSkew", thresholdWindowSource);
        Assert.Contains("WindowPopInAnimator.Begin(", thresholdWindowSource);
        Assert.Contains("WindowSurfaceScale,", thresholdWindowSource);
        Assert.Contains("WindowSurfaceSkew,", thresholdWindowSource);
        Assert.Contains("WindowSurfaceTranslate,", thresholdWindowSource);
        Assert.Contains("ChipClickPulse", thresholdWindowSource);
        Assert.Contains("CubicEase", thresholdWindowSource);
        Assert.Contains("isChecked ? 0.96d : 0.72d", thresholdWindowSource);
        Assert.Contains("isChecked ? 1.24d : 1.12d", thresholdWindowSource);
        Assert.Contains("pulse.BeginAnimation(OpacityProperty", thresholdWindowSource);
        Assert.Contains("ScaleTransform.ScaleXProperty", thresholdWindowSource);
        Assert.Contains("HeaderDragArea_MouseLeftButtonDown", thresholdWindowXaml);
        Assert.Contains("IsEnabled=\"False\"", thresholdWindowXaml);
        Assert.Contains("ThresholdPanel", thresholdWindowXaml);
        Assert.DoesNotContain("ThresholdGrid", thresholdWindowXaml);
        Assert.Contains("WrapPanel", thresholdWindowXaml);
        Assert.Contains("SelectableThresholds", thresholdWindowSource);
        Assert.DoesNotContain("PreviewTextBlock", thresholdWindowXaml);
        Assert.DoesNotContain("배터리가 내려갈 때 알림 순서", thresholdWindowSource);
        Assert.Contains("DragMove();", thresholdWindowSource);
        Assert.Contains("TextBatteryAlertThresholds", viewModelSource);
        Assert.Contains("BatteryAlertThresholdsButtonText", viewModelSource);
        Assert.Contains("BatteryGuideTriggerLabel", viewModelSource);
        Assert.Contains("BatteryGuideTriggerSelect", viewModelSource);
        Assert.Contains("BatteryAlertThresholdsLabel", viewModelSource);
        Assert.Contains("BatteryAlertThresholdsButtonText", viewModelSource);
        Assert.Contains("BatteryAlertThresholdsTooltip", viewModelSource);
        var localizedTextBlock = viewModelSource[
            viewModelSource.IndexOf("private void RaiseLocalizedTextPropertyChanges()", StringComparison.Ordinal)..
            viewModelSource.IndexOf("internal static string BuildProbeFailureStatus", StringComparison.Ordinal)];
        Assert.Contains("OnPropertyChanged(nameof(BatteryAlertThresholdsButtonText))", localizedTextBlock);
        Assert.DoesNotContain("public string TextBatteryGuideTrigger => \"알림 버튼\"", viewModelSource);
        Assert.DoesNotContain("public string TextBatteryGuideTriggerSelect => \"사용자 키 선택\"", viewModelSource);
        Assert.DoesNotContain("public string TextBatteryAlertThresholds => \"자동 알림 설정\"", viewModelSource);
        Assert.Contains("ForcedBatteryAlertThresholdPercent = 15", settingsSource);
        Assert.Contains("MinimumCustomBatteryAlertThresholdPercent = 30", settingsSource);
        Assert.Contains("MaximumCustomBatteryAlertThresholdPercent = 80", settingsSource);
        Assert.Contains("PositionBatteryAlertThresholdsWindow(dialog);", mainWindowSource);
        Assert.Contains("private void PositionBatteryAlertThresholdsWindow(Window dialog)", mainWindowSource);
        Assert.Contains("PopInOriginScreenPoint = TryGetElementCenterScreenPoint(BatteryAlertThresholdsButton)", mainWindowSource);
        Assert.Contains("private static System.Windows.Point? TryGetElementCenterScreenPoint(FrameworkElement element)", mainWindowSource);
        Assert.Contains("TransformFromDevice", mainWindowSource);
        Assert.Contains("GetWorkingAreaForOwnerWindow();", mainWindowSource);
        Assert.Contains("TranslateTransform", popInAnimatorSource);
        Assert.Contains("SkewTransform", popInAnimatorSource);
        Assert.Contains("originScreenPoint", popInAnimatorSource);
        Assert.Contains("CalculateStartOffset", popInAnimatorSource);
        Assert.Contains("GenieStartScaleX = 0.42d", popInAnimatorSource);
        Assert.Contains("GenieStartScaleY = 0.24d", popInAnimatorSource);
        Assert.Contains("SettleDuration = TimeSpan.FromMilliseconds(700)", popInAnimatorSource);
        Assert.Contains("QuinticEase", popInAnimatorSource);
        Assert.Contains("BuildGenieScaleAnimation", popInAnimatorSource);
        Assert.Contains("BuildGenieDoubleAnimation", popInAnimatorSource);
        Assert.Contains("CalculateTransformOrigin", popInAnimatorSource);
        Assert.Contains("CalculateStartSkewX", popInAnimatorSource);
        Assert.Contains("CalculateStartSkewY", popInAnimatorSource);
        Assert.Contains("HandoffBehavior.SnapshotAndReplace", popInAnimatorSource);
        Assert.Contains("scale.ScaleX = 1d;", popInAnimatorSource);
        Assert.Contains("skew.AngleX = 0d;", popInAnimatorSource);
        Assert.Contains("translate.X = 0d;", popInAnimatorSource);
        Assert.DoesNotContain("1.025d", popInAnimatorSource);
        Assert.DoesNotContain("StartScale = 0.84d", popInAnimatorSource);
        Assert.Contains("BuildBatteryAlertThresholds(_viewModel.BatteryAlertThresholds)", mainWindowSource);
        Assert.Contains("PrimeBatteryAlertToastKeysForCurrentLevels();", mainWindowSource);
        Assert.Contains("private void PrimeBatteryAlertToastKeysForCurrentLevels()", mainWindowSource);
        Assert.Contains("ShouldSuppressAutomaticBatteryToastOnStartup", mainWindowSource);
        Assert.DoesNotContain(
            "CheckAllLowBatteryToasts();",
            mainWindowSource[
                mainWindowSource.IndexOf("private void BatteryAlertThresholdsButton_Click", StringComparison.Ordinal)..
                mainWindowSource.IndexOf("private void BatteryGuideTriggerSelectButton_Click", StringComparison.Ordinal)]);
        Assert.DoesNotContain(
            "CheckAllLowBatteryToasts();",
            mainWindowSource[
                mainWindowSource.IndexOf("if (e.PropertyName is nameof(MainViewModel.BatteryAlertThresholds))", StringComparison.Ordinal)..
                mainWindowSource.IndexOf("if (e.PropertyName is nameof(MainViewModel.ColorPresetId))", StringComparison.Ordinal)]);
        Assert.Contains("RemoveBatteryAlertToastKeysForDevice", mainWindowSource);
        Assert.Equal([15, 30, 40, 50, 60], MainWindow.BuildBatteryAlertThresholds("30, 40, 50, 60"));
        Assert.Equal(60, MainWindow.ResolveBatteryAlertThresholdToShow(60, [15, 30, 40, 50, 60]));
        Assert.Equal(60, MainWindow.ResolveBatteryAlertThresholdToShow(59, [15, 30, 40, 50, 60]));
        Assert.Equal(50, MainWindow.ResolveBatteryAlertThresholdToShow(50, [15, 30, 40, 50, 60]));
        Assert.Equal(15, MainWindow.ResolveBatteryAlertThresholdToShow(15, [15, 30, 40, 50, 60]));
        Assert.Equal([30, 40, 50, 60, 70, 80], BatteryAlertThresholdsWindow.SelectableThresholds);
    }

    [Fact]
    public void BatteryAlertThresholds_ResolveDescendingBatteryLevelsInConfiguredOrder()
    {
        var thresholds = MainWindow.BuildBatteryAlertThresholds("30, 40, 50, 60, 70, 80");

        Assert.Equal([15, 30, 40, 50, 60, 70, 80], thresholds);
        Assert.Equal(0, MainWindow.ResolveBatteryAlertThresholdToShow(81, thresholds));
        Assert.Equal(80, MainWindow.ResolveBatteryAlertThresholdToShow(80, thresholds));
        Assert.Equal(80, MainWindow.ResolveBatteryAlertThresholdToShow(79, thresholds));
        Assert.Equal(70, MainWindow.ResolveBatteryAlertThresholdToShow(70, thresholds));
        Assert.Equal(70, MainWindow.ResolveBatteryAlertThresholdToShow(69, thresholds));
        Assert.Equal(60, MainWindow.ResolveBatteryAlertThresholdToShow(60, thresholds));
        Assert.Equal(50, MainWindow.ResolveBatteryAlertThresholdToShow(50, thresholds));
        Assert.Equal(40, MainWindow.ResolveBatteryAlertThresholdToShow(40, thresholds));
        Assert.Equal(30, MainWindow.ResolveBatteryAlertThresholdToShow(30, thresholds));
        Assert.Equal(15, MainWindow.ResolveBatteryAlertThresholdToShow(15, thresholds));
        Assert.Equal(15, MainWindow.ResolveBatteryAlertThresholdToShow(1, thresholds));
    }

    [Fact]
    public void BatteryAlertThresholds_UsesOnlySelectedCustomThresholdsPlusForcedFifteen()
    {
        var thresholds = MainWindow.BuildBatteryAlertThresholds("40, 70");

        Assert.Equal([15, 40, 70], thresholds);
        Assert.Equal(0, MainWindow.ResolveBatteryAlertThresholdToShow(71, thresholds));
        Assert.Equal(70, MainWindow.ResolveBatteryAlertThresholdToShow(70, thresholds));
        Assert.Equal(70, MainWindow.ResolveBatteryAlertThresholdToShow(69, thresholds));
        Assert.Equal(40, MainWindow.ResolveBatteryAlertThresholdToShow(40, thresholds));
        Assert.Equal(40, MainWindow.ResolveBatteryAlertThresholdToShow(30, thresholds));
        Assert.Equal(15, MainWindow.ResolveBatteryAlertThresholdToShow(15, thresholds));
        Assert.Equal(15, MainWindow.ResolveBatteryAlertThresholdToShow(10, thresholds));
        Assert.DoesNotContain(30, thresholds);
        Assert.DoesNotContain(50, thresholds);
        Assert.DoesNotContain(60, thresholds);
        Assert.DoesNotContain(80, thresholds);
    }

    [Fact]
    public void NewlyAddedBatterySettingsText_FollowsSelectedLanguage()
    {
        Assert.Equal(
            "조합할 2개의 버튼을 눌러주세요",
            UiLanguageCatalog.GetExtraText("ko-KR", "BatteryGuideCapturePrompt"));
        Assert.Equal(
            "Press the 2 buttons for the combo",
            UiLanguageCatalog.GetExtraText("en-US", "BatteryGuideCapturePrompt"));
        Assert.Equal(
            "組み合わせる2つのボタンを押してください",
            UiLanguageCatalog.GetExtraText("ja-JP", "BatteryGuideCapturePrompt"));
        Assert.Equal(
            "Battery auto alerts",
            UiLanguageCatalog.GetExtraText("en-US", "BatteryAlertThresholdsHeading"));
        Assert.Equal(
            "バッテリー自動通知",
            UiLanguageCatalog.GetExtraText("ja-JP", "BatteryAlertThresholdsHeading"));
    }

    [Fact]
    public void AutomaticBatteryToast_SuppressesStartupCarryoverAlerts()
    {
        var suppressUntil = DateTimeOffset.Parse("2026-06-01T12:00:08Z");

        Assert.True(MainWindow.ShouldSuppressAutomaticBatteryToastOnStartup(
            DateTimeOffset.Parse("2026-06-01T12:00:00Z"),
            suppressUntil));
        Assert.False(MainWindow.ShouldSuppressAutomaticBatteryToastOnStartup(
            DateTimeOffset.Parse("2026-06-01T12:00:08Z"),
            suppressUntil));
    }

    [Fact]
    public void UpdateInstallerScript_RunsElevatedWaitsLogsAndChecksInstalledVersion()
    {
        var mainWindowSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));
        var serviceSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "UpdateService.cs"));

        Assert.Contains("_updateService.StartInstallerUpdateAndRestart(", mainWindowSource);
        Assert.Contains("DownloadAndVerifyInstallerAsync", mainWindowSource);
        Assert.Contains("Start-Process -FilePath $setupPath -ArgumentList $installArgs -Verb RunAs -Wait -PassThru", serviceSource);
        Assert.Contains("('/LOG=\\\"' + $logPath + '\\\"')", serviceSource);
        Assert.Contains("'/CLOSEAPPLICATIONS'", serviceSource);
        Assert.Contains("'/NORESTARTAPPLICATIONS'", serviceSource);
        Assert.Contains("installer_exit_code=", serviceSource);
        Assert.Contains("installer_launch_error=", serviceSource);
        Assert.Contains("Test-BlossInstalledVersion", serviceSource);
        Assert.Contains("ProductVersion -like ($versionPrefix + '*')", serviceSource);
        Assert.Contains("Bloss.dll", serviceSource);
        Assert.Contains("installed_target_version=", serviceSource);
    }

    [Fact]
    public void DualSenseGuideButtonToastCooldown_StaysResponsive()
    {
        Assert.True(MainWindow.GetGuideButtonToastCooldown(GuideButtonDeviceKind.DualSense) < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DualSenseGuideButtonToastCooldown_DoesNotUseSteamPowerOffWindow()
    {
        var firstToast = new DateTimeOffset(2026, 5, 26, 1, 20, 0, TimeSpan.Zero);

        Assert.False(MainWindow.ShouldSuppressGuideButtonToast(
            firstToast,
            firstToast + TimeSpan.FromSeconds(1),
            GuideButtonDeviceKind.DualSense));
    }
}
