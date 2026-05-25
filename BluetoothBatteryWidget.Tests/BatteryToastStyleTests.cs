using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryToastStyleTests
{
    [Theory]
    [InlineData(80, 0)]
    [InlineData(60, 0)]
    [InlineData(59, 1)]
    [InlineData(30, 1)]
    [InlineData(29, 2)]
    public void ResolveSeverity_UsesRequestedBatteryBands(int percent, int expected)
    {
        Assert.Equal(expected, (int)BatteryToastStyle.ResolveSeverity(percent));
    }

    [Fact]
    public void BuildSubtitle_LowAutomaticToast_UsesLowBatteryText()
    {
        var snapshot = new DeviceBatterySnapshot(
            DeviceId: "device",
            Address: "AABBCCDDEEFF",
            DisplayName: "COBALT BLUE",
            BatteryPercent: 20,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            SourceKind: BatterySourceKind.SonyHid);

        Assert.Equal("Low Battery", BatteryToastStyle.BuildSubtitle(snapshot, automatic: true));
    }
}
