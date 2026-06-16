using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryGuideMessageBuilderTests
{
    [Fact]
    public void BuildToastSubtitle_DualSenseManualToast_ShowsEnoughStatus()
    {
        var snapshot = CreateSnapshot(
            displayName: "DualSense Wireless Controller (USB/Pico2W)",
            percent: 75,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_0CE6");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "ko", automatic: false);

        Assert.Equal("배터리 닳는중", subtitle);
    }

    [Fact]
    public void BuildToastSubtitle_SteamManualToast_ShowsEnoughStatus()
    {
        var snapshot = CreateSnapshot(
            displayName: "Steam Controller",
            percent: 75,
            sourceKind: BatterySourceKind.SteamHid,
            modelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "en", automatic: false);

        Assert.Equal("Battery draining", subtitle);
    }

    [Fact]
    public void BuildToastSubtitle_AutomaticLowBattery_KeepsLowBatteryWarning()
    {
        var snapshot = CreateSnapshot(
            displayName: "DualSense Wireless Controller (USB/Pico2W)",
            percent: 20,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_0CE6");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "en", automatic: true);

        Assert.Equal("Charge now", subtitle);
    }

    [Fact]
    public void BuildToastSubtitle_AutomaticHighThreshold_UsesNoticeText()
    {
        var snapshot = CreateSnapshot(
            displayName: "Steam Controller",
            percent: 80,
            sourceKind: BatterySourceKind.SteamHid,
            modelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "ko", automatic: true);

        Assert.Equal("배터리 충분", subtitle);
    }

    [Theory]
    [InlineData(100, "배터리 충분")]
    [InlineData(80, "배터리 충분")]
    [InlineData(79, "배터리 닳는중")]
    [InlineData(60, "배터리 닳는중")]
    [InlineData(59, "충전이 필요함")]
    [InlineData(30, "충전이 필요함")]
    [InlineData(29, "바로 충전하세요")]
    [InlineData(1, "바로 충전하세요")]
    public void BuildToastSubtitle_AutomaticAlerts_UsesClearRangeText(int percent, string expected)
    {
        var snapshot = CreateSnapshot(
            displayName: "Steam Controller",
            percent: percent,
            sourceKind: BatterySourceKind.SteamHid,
            modelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "ko", automatic: true);

        Assert.Equal(expected, subtitle);
    }

    [Theory]
    [InlineData(85, "배터리 충분")]
    [InlineData(79, "배터리 닳는중")]
    [InlineData(59, "충전이 필요함")]
    [InlineData(29, "바로 충전하세요")]
    public void BuildToastSubtitle_ManualUnknownDevice_UsesBatteryHealthRanges(int percent, string expected)
    {
        var snapshot = CreateSnapshot(
            displayName: "Generic Controller",
            percent: percent,
            sourceKind: BatterySourceKind.Unknown,
            modelKey: "GENERIC");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "ko", automatic: false);

        Assert.Equal(expected, subtitle);
    }

    [Fact]
    public void Build_WithJapaneseLanguage_UsesJapaneseText()
    {
        var snapshot = CreateSnapshot(
            displayName: "DualSense Wireless Controller (USB/Pico2W)",
            percent: 75,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_0CE6");

        var message = BatteryGuideMessageBuilder.Build(snapshot, WidgetSettings.JapaneseLanguage);
        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, WidgetSettings.JapaneseLanguage, automatic: false);

        Assert.Contains("約 6時間 残り", message);
        Assert.Equal("バッテリー消耗中", subtitle);
    }

    [Fact]
    public void BuildToastSubtitle_WithFrenchLanguage_UsesFrenchText()
    {
        var snapshot = CreateSnapshot(
            displayName: "DualSense Wireless Controller (USB/Pico2W)",
            percent: 20,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_0CE6");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, WidgetSettings.FrenchLanguage, automatic: true);

        Assert.Equal("Chargez maintenant", subtitle);
    }

    private static DeviceBatterySnapshot CreateSnapshot(
        string displayName,
        int percent,
        BatterySourceKind sourceKind,
        string modelKey)
    {
        return new DeviceBatterySnapshot(
            DeviceId: "device:AABBCCDDEEFF",
            Address: "AABBCCDDEEFF",
            DisplayName: displayName,
            BatteryPercent: percent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            SourceKind: sourceKind,
            ModelKey: modelKey);
    }
}
