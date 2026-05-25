using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryGuideMessageBuilderTests
{
    [Fact]
    public void BuildToastSubtitle_DualSenseManualToast_ShowsEstimatedRuntime()
    {
        var snapshot = CreateSnapshot(
            displayName: "DualSense Wireless Controller (USB/Pico2W)",
            percent: 75,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_0CE6");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "ko", automatic: false);

        Assert.Equal("예상 6시간 남음", subtitle);
    }

    [Fact]
    public void BuildToastSubtitle_SteamManualToast_ShowsEstimatedRuntime()
    {
        var snapshot = CreateSnapshot(
            displayName: "Steam Controller",
            percent: 75,
            sourceKind: BatterySourceKind.SteamHid,
            modelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK");

        var subtitle = BatteryGuideMessageBuilder.BuildToastSubtitle(snapshot, "en", automatic: false);

        Assert.Equal("About 15 hours left", subtitle);
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

        Assert.Equal("Low Battery", subtitle);
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
