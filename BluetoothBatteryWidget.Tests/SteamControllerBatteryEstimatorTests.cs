using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class SteamControllerBatteryEstimatorTests
{
    [Fact]
    public void TryEstimatePercentFromVoltage_PlausibleVoltage_ReturnsEstimate()
    {
        var status = CreateStatus(4100);

        var estimated = SteamControllerBatteryEstimator.TryEstimatePercentFromVoltage(status, out var percent);

        Assert.True(estimated);
        Assert.Equal(94, percent);
    }

    [Fact]
    public void TryEstimatePercentFromVoltage_HighVoltage_CapsBelowHundred()
    {
        var status = CreateStatus(4200);

        var estimated = SteamControllerBatteryEstimator.TryEstimatePercentFromVoltage(status, out var percent);

        Assert.True(estimated);
        Assert.Equal(99, percent);
    }

    [Fact]
    public void TryEstimatePercentFromVoltage_ImplausibleVoltage_ReturnsFalse()
    {
        var status = CreateStatus(4660);

        var estimated = SteamControllerBatteryEstimator.TryEstimatePercentFromVoltage(status, out _);

        Assert.False(estimated);
    }

    private static SteamControllerBatteryStatus CreateStatus(ushort batteryVoltage)
    {
        return new SteamControllerBatteryStatus(
            BatteryPercent: 100,
            ChargeState: SteamControllerChargeState.Charging,
            BatteryVoltage: batteryVoltage,
            SystemVoltage: 0,
            InputVoltage: 0,
            Current: 0,
            InputCurrent: 0,
            Temperature: 0);
    }
}
