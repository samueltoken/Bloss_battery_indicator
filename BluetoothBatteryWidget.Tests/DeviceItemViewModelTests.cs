using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class DeviceItemViewModelTests
{
    [Fact]
    public void IsProbeEligible_True_WhenBatteryIsSuspectEvenWithPercent()
    {
        var snapshot = new DeviceBatterySnapshot(
            DeviceId: "dev-1",
            Address: "AABBCCDDE004",
            DisplayName: "Xbox Wireless Controller",
            BatteryPercent: 9,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            SourceKind: BatterySourceKind.LearnedHid,
            ModelKey: "VID_045E|PID_02E0",
            SuggestCalibration: false,
            IsBatterySuspect: true);

        var vm = new DeviceItemViewModel(snapshot);

        Assert.True(vm.IsProbeEligible);
    }

    [Fact]
    public void IsChargeComplete_ReflectsSnapshot()
    {
        var snapshot = new DeviceBatterySnapshot(
            DeviceId: "steam-triton:AABBCCDDE010",
            Address: "AABBCCDDE010",
            DisplayName: "Steam Controller",
            BatteryPercent: 100,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            IsCharging: true,
            IsChargeComplete: true);

        var vm = new DeviceItemViewModel(snapshot);

        Assert.True(vm.IsCharging);
        Assert.True(vm.IsChargeComplete);
    }
}
