using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GameInputBatteryProviderTests
{
    [Fact]
    public void NormalizePercent_ScaledByTenPattern_UsesRemainingAsPercent()
    {
        var percent = GameInputBatteryProvider.NormalizePercent(
            mappedPercent: 10,
            remainingCapacity: 100,
            fullCapacity: 1000);

        Assert.Equal(100, percent);
    }

    [Fact]
    public void NormalizePercent_NormalBatteryReport_UsesMappedPercent()
    {
        var percent = GameInputBatteryProvider.NormalizePercent(
            mappedPercent: 64,
            remainingCapacity: 3200,
            fullCapacity: 5000);

        Assert.Equal(64, percent);
    }

    [Fact]
    public void NormalizePercent_ScaledPatternAtLowBattery_KeepsLowValue()
    {
        var percent = GameInputBatteryProvider.NormalizePercent(
            mappedPercent: 1,
            remainingCapacity: 8,
            fullCapacity: 1000);

        Assert.Equal(8, percent);
    }
}
