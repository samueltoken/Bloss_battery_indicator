using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using System.Windows.Media;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryToastStyleTests
{
    [Theory]
    [InlineData(80, 0)]
    [InlineData(79, 1)]
    [InlineData(60, 1)]
    [InlineData(59, 2)]
    [InlineData(30, 2)]
    [InlineData(29, 3)]
    [InlineData(1, 3)]
    public void ResolveSeverity_UsesRequestedBatteryBands(int percent, int expected)
    {
        Assert.Equal(expected, (int)BatteryToastStyle.ResolveSeverity(percent));
    }

    [Theory]
    [InlineData(100, "Battery enough")]
    [InlineData(80, "Battery enough")]
    [InlineData(79, "Battery draining")]
    [InlineData(60, "Battery draining")]
    [InlineData(59, "Charging needed")]
    [InlineData(30, "Charging needed")]
    [InlineData(29, "Charge now")]
    [InlineData(1, "Charge now")]
    public void BuildSubtitle_UsesRequestedBatteryBandText(int percent, string expected)
    {
        var snapshot = new DeviceBatterySnapshot(
            DeviceId: "device",
            Address: "AABBCCDDEEFF",
            DisplayName: "COBALT BLUE",
            BatteryPercent: percent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            SourceKind: BatterySourceKind.SonyHid);

        Assert.Equal(expected, BatteryToastStyle.BuildSubtitle(snapshot, automatic: true));
    }

    [Theory]
    [InlineData(80, 47, 123, 234)]
    [InlineData(60, 45, 190, 114)]
    [InlineData(30, 242, 181, 55)]
    [InlineData(29, 232, 38, 38)]
    public void ResolveAccentBrush_UsesRequestedBatteryBandColors(int percent, byte red, byte green, byte blue)
    {
        var brush = Assert.IsType<SolidColorBrush>(BatteryToastStyle.ResolveAccentBrush(
            BatteryToastStyle.ResolveSeverity(percent)));

        Assert.Equal(Color.FromRgb(red, green, blue), brush.Color);
    }
}
