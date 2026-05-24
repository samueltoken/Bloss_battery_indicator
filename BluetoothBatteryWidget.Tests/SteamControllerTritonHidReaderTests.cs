using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class SteamControllerTritonHidReaderTests
{
    [Fact]
    public void IsDisplayableControllerBattery_ZeroPercentNotCharging_ReturnsFalse()
    {
        var status = CreateStatus(0, SteamControllerChargeState.Discharging);

        Assert.False(SteamControllerTritonHidReader.IsDisplayableControllerBattery(status));
    }

    [Fact]
    public void IsDisplayableControllerBattery_ZeroPercentCharging_ReturnsTrue()
    {
        var status = CreateStatus(0, SteamControllerChargeState.Charging);

        Assert.True(SteamControllerTritonHidReader.IsDisplayableControllerBattery(status));
    }

    [Fact]
    public void IsDisplayableControllerBattery_WirelessPercent_ReturnsTrue()
    {
        var status = CreateStatus(94, SteamControllerChargeState.Discharging);

        Assert.True(SteamControllerTritonHidReader.IsDisplayableControllerBattery(status));
    }

    [Fact]
    public void IsSuspiciousDockedFullBattery_ChargingHundred_ReturnsFalse()
    {
        var status = CreateStatus(100, SteamControllerChargeState.Charging);

        Assert.False(SteamControllerTritonHidReader.IsSuspiciousDockedFullBattery(status));
    }

    [Fact]
    public void IsSuspiciousDockedFullBattery_ChargingDoneHundred_ReturnsFalse()
    {
        var status = CreateStatus(100, SteamControllerChargeState.ChargingDone);

        Assert.False(SteamControllerTritonHidReader.IsSuspiciousDockedFullBattery(status));
    }

    [Fact]
    public void ChoosePreferredBatteryStatus_CurrentChargingHundredCandidateNonFull_KeepsCurrent()
    {
        var current = CreateStatus(100, SteamControllerChargeState.Charging);
        var candidate = CreateStatus(96, SteamControllerChargeState.Discharging);

        var selected = SteamControllerTritonHidReader.ChoosePreferredBatteryStatus(current, candidate);

        Assert.Equal(100, selected.BatteryPercent);
        Assert.Equal(SteamControllerChargeState.Charging, selected.ChargeState);
    }

    [Fact]
    public void ChoosePreferredBatteryStatus_CurrentNonFullCandidateDockedFull_KeepsCurrent()
    {
        var current = CreateStatus(96, SteamControllerChargeState.Discharging);
        var candidate = CreateStatus(100, SteamControllerChargeState.Charging);

        var selected = SteamControllerTritonHidReader.ChoosePreferredBatteryStatus(current, candidate);

        Assert.Equal(96, selected.BatteryPercent);
        Assert.Equal(SteamControllerChargeState.Discharging, selected.ChargeState);
    }

    [Fact]
    public void ShouldExposeController_PuckOnlyZeroPercentNotCharging_ReturnsFalse()
    {
        Assert.False(SteamControllerTritonHidReader.ShouldExposeController(
            displayableBatteryStatus: null,
            sawAnyBatteryStatus: true,
            sawConnectedSignal: true,
            sawDisconnectedSignal: false));
    }

    [Fact]
    public void ShouldExposeController_ChargingBattery_ReturnsTrue()
    {
        var status = CreateStatus(0, SteamControllerChargeState.Charging);

        Assert.True(SteamControllerTritonHidReader.ShouldExposeController(
            status,
            sawAnyBatteryStatus: true,
            sawConnectedSignal: false,
            sawDisconnectedSignal: false));
    }

    [Fact]
    public void ShouldExposeController_InputSignalBeforeBatteryArrives_ReturnsTrue()
    {
        Assert.True(SteamControllerTritonHidReader.ShouldExposeController(
            displayableBatteryStatus: null,
            sawAnyBatteryStatus: false,
            sawConnectedSignal: true,
            sawDisconnectedSignal: false));
    }

    [Fact]
    public void ShouldExposeController_DisconnectedSignalWithoutBattery_ReturnsFalse()
    {
        Assert.False(SteamControllerTritonHidReader.ShouldExposeController(
            displayableBatteryStatus: null,
            sawAnyBatteryStatus: false,
            sawConnectedSignal: false,
            sawDisconnectedSignal: true));
    }

    private static SteamControllerBatteryStatus CreateStatus(int percent, SteamControllerChargeState state)
    {
        return new SteamControllerBatteryStatus(
            BatteryPercent: percent,
            ChargeState: state,
            BatteryVoltage: 0,
            SystemVoltage: 0,
            InputVoltage: 0,
            Current: 0,
            InputCurrent: 0,
            Temperature: 0);
    }
}
