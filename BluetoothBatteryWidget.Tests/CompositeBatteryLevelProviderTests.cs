using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class CompositeBatteryLevelProviderTests
{
    [Fact]
    public async Task RunProviderSafelyAsync_CompletesWithoutTimeout()
    {
        var result = await CompositeBatteryLevelProvider.RunProviderSafelyAsync(
            providerName: "test",
            timeout: TimeSpan.FromMilliseconds(300),
            provider: _ => Task.FromResult<IReadOnlyList<PnpBatteryReading>>(
            [
                new PnpBatteryReading(
                    InstanceId: "id",
                    Address: "AABBCCDD0011",
                    DisplayName: "Pad",
                    BatteryPercent: 77)
            ]),
            cancellationToken: CancellationToken.None);

        Assert.False(result.TimedOut);
        Assert.Single(result.Readings);
        Assert.Equal(77, result.Readings[0].BatteryPercent);
    }

    [Fact]
    public async Task RunProviderSafelyAsync_ReturnsEmptyWhenTimedOut()
    {
        var result = await CompositeBatteryLevelProvider.RunProviderSafelyAsync(
            providerName: "test",
            timeout: TimeSpan.FromMilliseconds(80),
            provider: async token =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return Array.Empty<PnpBatteryReading>();
            },
            cancellationToken: CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Empty(result.Readings);
    }

    [Fact]
    public void ShouldTraceSteamTritonReadings_WhenRawSteamChargingHundred_ReturnsTrue()
    {
        var raw = new[]
        {
            new PnpBatteryReading(
                InstanceId: "steam-triton:AABBCCDDE010",
                Address: "AABBCCDDE010",
                DisplayName: "Steam Controller",
                BatteryPercent: 100,
                SourceKind: BatterySourceKind.SteamHid,
                IsCharging: true)
        };

        var result = CompositeBatteryLevelProvider.ShouldTraceSteamTritonReadings(raw, []);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTraceSteamTritonReadings_WhenResolvedDockedFullPending_ReturnsTrue()
    {
        var resolved = new[]
        {
            new PnpBatteryReading(
                InstanceId: "steam-triton:AABBCCDDE010",
                Address: "AABBCCDDE010",
                DisplayName: "Steam Controller",
                BatteryPercent: null,
                SourceKind: BatterySourceKind.SteamHid,
                ReasonCode: "steam_triton_docked_full_pending",
                IsCharging: true)
        };

        var result = CompositeBatteryLevelProvider.ShouldTraceSteamTritonReadings([], resolved);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTraceSteamTritonReadings_WhenResolvedChargeCompleteLatched_ReturnsTrue()
    {
        var resolved = new[]
        {
            new PnpBatteryReading(
                InstanceId: "steam-triton:AABBCCDDE010",
                Address: "AABBCCDDE010",
                DisplayName: "Steam Controller",
                BatteryPercent: 100,
                SourceKind: BatterySourceKind.SteamHid,
                RawMetric: 97,
                ReasonCode: "steam_triton_charge_complete_latched")
        };

        var result = CompositeBatteryLevelProvider.ShouldTraceSteamTritonReadings([], resolved);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTraceSteamTritonReadings_WhenResolvedBluetoothChargeCompleteLatched_ReturnsTrue()
    {
        var resolved = new[]
        {
            new PnpBatteryReading(
                InstanceId: "BTHLEDEVICE\\{0000180F-0000-1000-8000-00805F9B34FB}_DEV_VID&0228DE_PID&1303_REV&0100_AABBCCDDE011",
                Address: "AABBCCDDE011",
                DisplayName: "Steam Ctrl (BT) FXA9961304141",
                BatteryPercent: 100,
                SourceKind: BatterySourceKind.BleGatt,
                RawMetric: 96,
                ModelKey: "VID_28DE|PID_1303",
                ReasonCode: "steam_controller_bluetooth_charge_complete_latched")
        };

        var result = CompositeBatteryLevelProvider.ShouldTraceSteamTritonReadings([], resolved);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTraceSteamTritonReadings_WhenNormalSteamBattery_ReturnsFalse()
    {
        var raw = new[]
        {
            new PnpBatteryReading(
                InstanceId: "steam-triton:AABBCCDDE010",
                Address: "AABBCCDDE010",
                DisplayName: "Steam Controller",
                BatteryPercent: 96,
                SourceKind: BatterySourceKind.SteamHid,
                IsCharging: false)
        };

        var result = CompositeBatteryLevelProvider.ShouldTraceSteamTritonReadings(raw, raw);

        Assert.False(result);
    }

    [Fact]
    public void BuildSteamTritonTraceFingerprint_WhenSteamPercentChanges_DetectsStateChange()
    {
        var wireless = CreateSteamReading(97, rawMetric: 97, isCharging: false, reasonCode: "steam_triton_battery");
        var docked = CreateSteamReading(96, rawMetric: 96, isCharging: true, reasonCode: "steam_triton_charging");

        var first = CompositeBatteryLevelProvider.BuildSteamTritonTraceFingerprint([wireless], [wireless]);
        var second = CompositeBatteryLevelProvider.BuildSteamTritonTraceFingerprint([docked], [docked]);

        Assert.True(CompositeBatteryLevelProvider.HasSteamTritonTraceStateChanged(null, first));
        Assert.True(CompositeBatteryLevelProvider.HasSteamTritonTraceStateChanged(first, second));
        Assert.False(CompositeBatteryLevelProvider.HasSteamTritonTraceStateChanged(second, second));
    }

    [Fact]
    public void BuildSteamTritonTraceFingerprint_WhenSameAddressRawSourceChanges_DetectsStateChange()
    {
        var steam = CreateSteamReading(96, rawMetric: 96, isCharging: true, reasonCode: "steam_triton_charging");
        var setupHundred = CreateSetupReading(100);
        var setupNinetySix = setupHundred with
        {
            BatteryPercent = 96,
            RawMetric = 96,
            ReasonCode = "setupapi_estimated"
        };

        var first = CompositeBatteryLevelProvider.BuildSteamTritonTraceFingerprint([steam, setupHundred], [steam]);
        var second = CompositeBatteryLevelProvider.BuildSteamTritonTraceFingerprint([steam, setupNinetySix], [steam]);

        Assert.True(CompositeBatteryLevelProvider.HasSteamTritonTraceStateChanged(first, second));
    }

    private static PnpBatteryReading CreateSteamReading(
        int batteryPercent,
        double rawMetric,
        bool isCharging,
        string reasonCode)
    {
        return new PnpBatteryReading(
            InstanceId: "steam-triton:AABBCCDDE010",
            Address: "AABBCCDDE010",
            DisplayName: "Steam Controller",
            BatteryPercent: batteryPercent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            SourceKind: BatterySourceKind.SteamHid,
            RawMetric: rawMetric,
            ModelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK",
            ReasonCode: reasonCode,
            ActiveSource: "steamhid",
            PathType: "receiver",
            DisplayState: isCharging ? BatteryDisplayState.Charging : BatteryDisplayState.Verified,
            IsCharging: isCharging);
    }

    private static PnpBatteryReading CreateSetupReading(int batteryPercent)
    {
        return new PnpBatteryReading(
            InstanceId: "SETUPAPI",
            Address: "AABBCCDDE010",
            DisplayName: "Steam Controller",
            BatteryPercent: batteryPercent,
            BatteryConfidence: BatteryConfidence.Estimated,
            SourceKind: BatterySourceKind.SetupApi,
            RawMetric: batteryPercent,
            ModelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK",
            ReasonCode: "setupapi_estimated",
            ActiveSource: "setupapi",
            PathType: "receiver",
            DisplayState: BatteryDisplayState.Estimated);
    }
}
