using BluetoothBatteryWidget.Core.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class GamepadBatteryParserTests
{
    [Fact]
    public void TryParseDualSenseBatteryPercent_BluetoothDischarging_ReturnsMidpointPercent()
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[54] = 0x04; // discharging + data 4

        var parsed = GamepadBatteryParser.TryParseDualSenseBatteryPercent(report, out var percent);

        Assert.True(parsed);
        Assert.Equal(45, percent);
    }

    [Fact]
    public void TryParseDualSenseBatteryPercent_UsbFull_ReturnsHundred()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[53] = 0x20; // full

        var parsed = GamepadBatteryParser.TryParseDualSenseBatteryPercent(report, out var percent);

        Assert.True(parsed);
        Assert.Equal(100, percent);
    }

    [Fact]
    public void TryParseDualSenseBatteryPercent_ChargingError_ReturnsNull()
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[54] = 0xF0; // charging error

        var parsed = GamepadBatteryParser.TryParseDualSenseBatteryPercent(report, out var percent);

        Assert.True(parsed);
        Assert.Null(percent);
    }

    [Fact]
    public void TryParseDualShock4BatteryPercent_BluetoothCharging_ReturnsMidpointPercent()
    {
        var report = new byte[78];
        report[0] = 0x11;
        report[32] = 0x14; // cable connected + data 4

        var parsed = GamepadBatteryParser.TryParseDualShock4BatteryPercent(report, out var percent);

        Assert.True(parsed);
        Assert.Equal(45, percent);
    }

    [Fact]
    public void TryParseDualShock4BatteryPercent_UsbDischarging_ReturnsMidpointPercent()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[30] = 0x03; // discharging + data 3

        var parsed = GamepadBatteryParser.TryParseDualShock4BatteryPercent(report, out var percent);

        Assert.True(parsed);
        Assert.Equal(35, percent);
    }

    [Fact]
    public void TryParseDualShock4BatteryPercent_ChargingUnknown_ReturnsNull()
    {
        var report = new byte[78];
        report[0] = 0x11;
        report[32] = 0x1E; // cable connected + data 14(error)

        var parsed = GamepadBatteryParser.TryParseDualShock4BatteryPercent(report, out var percent);

        Assert.True(parsed);
        Assert.Null(percent);
    }

    [Fact]
    public void TryParseSteamTritonBatteryStatus_Discharging_ReturnsPercent()
    {
        var report = new byte[17];
        report[0] = 0x43;
        report[1] = 0x01;
        report[2] = 73;
        report[3] = 0x34;
        report[4] = 0x12;

        var parsed = GamepadBatteryParser.TryParseSteamTritonBatteryStatus(report, out var status);

        Assert.True(parsed);
        Assert.Equal(73, status.BatteryPercent);
        Assert.Equal(SteamControllerChargeState.Discharging, status.ChargeState);
        Assert.False(status.IsCharging);
        Assert.Equal(0x1234, status.BatteryVoltage);
    }

    [Fact]
    public void TryParseSteamTritonBatteryStatus_ChargingDone_IsCharging()
    {
        var report = new byte[17];
        report[0] = 0x43;
        report[1] = 0x04;
        report[2] = 100;

        var parsed = GamepadBatteryParser.TryParseSteamTritonBatteryStatus(report, out var status);

        Assert.True(parsed);
        Assert.Equal(100, status.BatteryPercent);
        Assert.Equal(SteamControllerChargeState.ChargingDone, status.ChargeState);
        Assert.True(status.IsCharging);
        Assert.True(status.IsChargeComplete);
    }

    [Fact]
    public void TryParseSteamTritonWirelessConnected_ConnectReport_ReturnsConnected()
    {
        var report = new byte[] { 0x79, 0x02 };

        var parsed = GamepadBatteryParser.TryParseSteamTritonWirelessConnected(report, out var connected);

        Assert.True(parsed);
        Assert.True(connected);
    }
}
