using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class MainViewModelDualShock4ConnectingTests
{
    [Fact]
    public void ShouldTreatDualShock4InitialLowAsConnecting_WhenLowAndWithinWindow_ReturnsTrue()
    {
        var snapshot = CreateSnapshot(
            batteryPercent: 5,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_09CC");
        var connectedSince = DateTimeOffset.Now.AddSeconds(-20);
        var now = DateTimeOffset.Now;

        var result = MainViewModel.ShouldTreatDualShock4InitialLowAsConnecting(snapshot, connectedSince, now);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTreatDualShock4InitialLowAsConnecting_WhenPastWindow_ReturnsFalse()
    {
        var snapshot = CreateSnapshot(
            batteryPercent: 5,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_09CC");
        var connectedSince = DateTimeOffset.Now.AddSeconds(-75);
        var now = DateTimeOffset.Now;

        var result = MainViewModel.ShouldTreatDualShock4InitialLowAsConnecting(snapshot, connectedSince, now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldTreatDualShock4InitialLowAsConnecting_WhenNotLowPercent_ReturnsFalse()
    {
        var snapshot = CreateSnapshot(
            batteryPercent: 85,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_054C|PID_09CC");
        var connectedSince = DateTimeOffset.Now.AddSeconds(-10);
        var now = DateTimeOffset.Now;

        var result = MainViewModel.ShouldTreatDualShock4InitialLowAsConnecting(snapshot, connectedSince, now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldTreatDualShock4InitialLowAsConnecting_WhenOtherModel_ReturnsFalse()
    {
        var snapshot = CreateSnapshot(
            batteryPercent: 5,
            sourceKind: BatterySourceKind.SonyHid,
            modelKey: "VID_045E|PID_02E0");
        var connectedSince = DateTimeOffset.Now.AddSeconds(-10);
        var now = DateTimeOffset.Now;

        var result = MainViewModel.ShouldTreatDualShock4InitialLowAsConnecting(snapshot, connectedSince, now);

        Assert.False(result);
    }

    private static DeviceBatterySnapshot CreateSnapshot(
        int? batteryPercent,
        BatterySourceKind sourceKind,
        string modelKey)
    {
        return new DeviceBatterySnapshot(
            DeviceId: "id",
            Address: "A45385EDE1A5",
            DisplayName: "Wireless Controller",
            BatteryPercent: batteryPercent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            SourceKind: sourceKind,
            ModelKey: modelKey);
    }
}
