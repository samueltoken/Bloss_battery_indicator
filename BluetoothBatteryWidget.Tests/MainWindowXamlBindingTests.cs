using BluetoothBatteryWidget.App;
using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class MainWindowXamlBindingTests
{
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
    public void SteamControllerGuideButtonToastCooldown_MatchesVisibleToastLifetime()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), MainWindow.GetGuideButtonToastCooldown(GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void SteamControllerGuideButtonToastCooldown_SuppressesPowerOffReopenWhileToastIsVisible()
    {
        var firstToast = new DateTimeOffset(2026, 5, 26, 1, 20, 0, TimeSpan.Zero);

        Assert.True(MainWindow.ShouldSuppressGuideButtonToast(
            firstToast,
            firstToast + TimeSpan.FromSeconds(3.8),
            GuideButtonDeviceKind.SteamController));
        Assert.False(MainWindow.ShouldSuppressGuideButtonToast(
            firstToast,
            firstToast + TimeSpan.FromSeconds(4.1),
            GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void SteamSecondaryFallbackBurstWindow_BlocksPowerOffRepeatLongerThanToastCooldown()
    {
        Assert.True(MainWindow.GetSteamSecondaryFallbackBurstWindow() >= TimeSpan.FromSeconds(6));
        Assert.True(MainWindow.GetSteamSecondaryFallbackBurstWindow() > MainWindow.GetGuideButtonToastCooldown(GuideButtonDeviceKind.SteamController));
    }

    [Fact]
    public void SteamSecondaryFallbackDelay_IsShortButStillCatchesRepeatedHoldSignals()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(380), MainWindow.GetSteamSecondaryFallbackDelay());
        Assert.True(MainWindow.GetSteamSecondaryFallbackDelay() > TimeSpan.FromMilliseconds(350));
        Assert.True(MainWindow.GetSteamSecondaryFallbackDelay() < TimeSpan.FromMilliseconds(450));
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
