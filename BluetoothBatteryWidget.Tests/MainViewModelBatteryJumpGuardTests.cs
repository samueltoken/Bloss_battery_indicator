using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class MainViewModelBatteryJumpGuardTests
{
    [Fact]
    public void TryHoldSteamControllerSetupApiFullSpike_HoldsRecentNonFullPercent()
    {
        var now = new DateTimeOffset(2026, 5, 27, 14, 30, 0, TimeSpan.Zero);
        var previous = CreateSteamSetupApiReading(89, now.AddSeconds(-20));
        var current = CreateSteamSetupApiReading(100, now);

        var held = MainViewModel.TryHoldSteamControllerSetupApiFullSpike(
            previous,
            current,
            now,
            out var heldReading);

        Assert.True(held);
        Assert.Equal(89, heldReading.BatteryPercent);
        Assert.Equal(100, heldReading.RawMetric);
        Assert.Equal(BatteryConfidence.Estimated, heldReading.BatteryConfidence);
        Assert.True(heldReading.IsBatterySuspect);
        Assert.Equal("steam_controller_setupapi_recent_nonfull_hold", heldReading.ReasonCode);
    }

    [Fact]
    public void TryHoldSteamControllerSetupApiFullSpike_DoesNotHoldNearFullProgression()
    {
        var now = new DateTimeOffset(2026, 5, 27, 14, 30, 0, TimeSpan.Zero);
        var previous = CreateSteamSetupApiReading(97, now.AddSeconds(-20));
        var current = CreateSteamSetupApiReading(100, now);

        var held = MainViewModel.TryHoldSteamControllerSetupApiFullSpike(
            previous,
            current,
            now,
            out var heldReading);

        Assert.False(held);
        Assert.Equal(current, heldReading);
    }

    [Fact]
    public void TryHoldSteamControllerSetupApiFullSpike_DoesNotHoldSteamHidHundred()
    {
        var now = new DateTimeOffset(2026, 5, 27, 14, 30, 0, TimeSpan.Zero);
        var previous = CreateSteamSetupApiReading(89, now.AddSeconds(-20));
        var current = CreateSteamSetupApiReading(100, now) with
        {
            SourceKind = BatterySourceKind.SteamHid,
            ReasonCode = "steam_triton_battery"
        };

        var held = MainViewModel.TryHoldSteamControllerSetupApiFullSpike(
            previous,
            current,
            now,
            out var heldReading);

        Assert.False(held);
        Assert.Equal(current, heldReading);
    }

    [Fact]
    public void TryHoldSteamControllerSetupApiFullSpike_DoesNotHoldNonSteamController()
    {
        var now = new DateTimeOffset(2026, 5, 27, 14, 30, 0, TimeSpan.Zero);
        var previous = CreateNonSteamSetupApiReading(89, now.AddSeconds(-20));
        var current = CreateNonSteamSetupApiReading(100, now);

        var held = MainViewModel.TryHoldSteamControllerSetupApiFullSpike(
            previous,
            current,
            now,
            out var heldReading);

        Assert.False(held);
        Assert.Equal(current, heldReading);
    }

    private static PnpBatteryReading CreateSteamSetupApiReading(int percent, DateTimeOffset observedAt)
    {
        return new PnpBatteryReading(
            InstanceId: @"BTHENUM\DEV_AABBCCDDE0F0\7&1A2B3C4D&0&BLUETOOTHDEVICE_AABBCCDDE0F0",
            Address: "AABBCCDDE0F0",
            DisplayName: "Steam Ctrl (BT) FXA0000000000",
            BatteryPercent: percent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            SourceKind: BatterySourceKind.SetupApi,
            RawMetric: percent,
            ModelKey: "",
            ObservedAt: observedAt,
            ReasonCode: "setupapi_direct");
    }

    private static PnpBatteryReading CreateNonSteamSetupApiReading(int percent, DateTimeOffset observedAt)
    {
        return new PnpBatteryReading(
            InstanceId: @"BTHENUM\DEV_112233445566\7&1A2B3C4D&0&BLUETOOTHDEVICE_112233445566",
            Address: "112233445566",
            DisplayName: "Wireless Controller",
            BatteryPercent: percent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            SourceKind: BatterySourceKind.SetupApi,
            RawMetric: percent,
            ModelKey: "VID_054C|PID_0CE6",
            ObservedAt: observedAt,
            ReasonCode: "setupapi_direct");
    }
}
