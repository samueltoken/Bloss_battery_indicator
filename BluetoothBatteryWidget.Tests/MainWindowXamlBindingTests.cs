using BluetoothBatteryWidget.App;
using BluetoothBatteryWidget.App.Services;
using System.Text.RegularExpressions;

namespace BluetoothBatteryWidget.Tests;

public sealed class MainWindowXamlBindingTests
{
    [Fact]
    public void MainWindowXaml_HexBrushTokensUseValidWpfLengths()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
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
    public void SettingsPopupAutoClose_ProtectsCursorInsidePopupSurfacesByScreenBounds()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("SettingsAutoCloseProtectedScreenMargin", source);
        Assert.Contains("Forms.Cursor.Position", source);
        Assert.Contains("PointToScreen", source);
        Assert.Contains("IsCursorWithinElementScreenBounds(SettingsPopupChrome", source);
        Assert.Contains("IsCursorWithinElementScreenBounds(ColorPopupChrome", source);
        Assert.Contains("IsCursorWithinElementScreenBounds(SettingsButton", source);
        Assert.Contains("IsCursorWithinElementScreenBounds(ColorCustomizeButton", source);
        Assert.Contains("ColorCustomPopup.IsOpen && IsCursorWithinElementScreenBounds(ColorPopupChrome", source);
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

        Assert.Contains("_trayResetPositionMenuItem", mainWindowSource);
        Assert.Contains("ResetWidgetPositionToCurrentMonitor", mainWindowSource);
        Assert.Contains("CenterWindowInArea(GetWorkingAreaFromCurrentCursor())", mainWindowSource);
        Assert.Contains("Forms.Screen.FromPoint(Forms.Cursor.Position)", mainWindowSource);
        Assert.Contains("HasMeaningfulVisibleArea", mainWindowSource);
        Assert.Contains("PrepareStartHiddenInTray", mainWindowSource);
        Assert.Contains("--start-in-tray", appSource);
        Assert.Contains("--start-in-tray", autostartSource);
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
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("_trayAutostartMenuItem", source);
        Assert.Contains("_trayStartMinimizedToTrayMenuItem", source);
        Assert.Contains("ToggleAutostartFromTray", source);
        Assert.Contains("ToggleStartMinimizedToTrayFromTray", source);
        Assert.Contains("_trayAutostartMenuItem.Checked = _viewModel.AutostartEnabled", source);
        Assert.Contains("_trayStartMinimizedToTrayMenuItem.Checked = _viewModel.StartMinimizedToTrayEnabled", source);
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
        Assert.Contains("DualSense가 Pico2W를 통해 연결된 상태", source);
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
    public void LabsWindow_UsesFullscreenPaintTunnelAndAutoClose()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "LabsWindow.cs"));

        Assert.Contains("Red Shift", source);
        Assert.Contains("BatteryGuideSoundCatalog.OuterSpaceSound", source);
        Assert.Contains("HyperspacePortalView", source);
        Assert.Contains("CompositionTarget.Rendering", source);
        Assert.Contains("DrawHyperspaceStreaks", source);
        Assert.Contains("DrawPortalRings", source);
        Assert.Contains("DrawSpiralRibbons", source);
        Assert.Contains("DrawThermalShockwaves", source);
        Assert.Contains("DrawFinalBlackout", source);
        Assert.Contains("ApplyFullScreenBounds", source);
        Assert.Contains("SystemParameters.VirtualScreenWidth", source);
        Assert.Contains("OuterSpaceDuration", source);
        Assert.Contains("BeginAnimation", source);
        Assert.Contains("HyperspaceStreakCount", source);
        Assert.Contains("PortalRingCount", source);
        Assert.Contains("CreateVignetteBrush", source);
        Assert.Contains("brush.Freeze()", source);
        Assert.Contains("CreateCoreBrush", source);
        Assert.Contains("CenterPullRadius", source);
        Assert.Contains("RedHotPalette", source);
        Assert.Contains("GlobalHeat", source);
        Assert.Contains("EndEngulf", source);
        Assert.Contains("HeatColor", source);
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
        Assert.Contains("activity.IsPressed", source);
        Assert.Contains("IsSteamControllerStatusReport(report)", source);
        Assert.DoesNotContain("raw_hid_battery_release_hint", source);
        Assert.DoesNotContain("_rawHidGuideButtonStateTracker.ClearStalePressedSession(address, now);", source);
    }

    [Fact]
    public void UpdateInstallerScript_RunsElevatedWaitsLogsAndChecksInstalledVersion()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("StartInstallerUpdateAndRestart(setupPath, releaseInfo.Version)", source);
        Assert.Contains("Start-Process -FilePath $setupPath -ArgumentList $installArgs -Verb RunAs -Wait -PassThru", source);
        Assert.Contains("('/LOG=\\\"' + $logPath + '\\\"')", source);
        Assert.DoesNotContain("'/CLOSEAPPLICATIONS'", source);
        Assert.DoesNotContain("'/NORESTARTAPPLICATIONS'", source);
        Assert.Contains("installer_exit_code=", source);
        Assert.Contains("installer_launch_error=", source);
        Assert.Contains("Test-BlossInstalledVersion", source);
        Assert.Contains("ProductVersion -like ($versionPrefix + '*')", source);
        Assert.Contains("Bloss.dll", source);
        Assert.Contains("installed_target_version=", source);
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
