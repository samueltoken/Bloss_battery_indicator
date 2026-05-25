using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryEvidenceResolverTests
{
    [Fact]
    public void ResolveAndRecord_GameInputLowWithoutCalibration_HidesPercentAndSuggestsCalibration()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;

        var readings = new List<PnpBatteryReading>
        {
            new(
                InstanceId: "GAMEINPUT_SLOT_0",
                Address: "A1B2C3D4E5F6",
                DisplayName: "Xbox Wireless Controller",
                BatteryPercent: 10,
                BatteryConfidence: BatteryConfidence.Confirmed,
                SourceKind: BatterySourceKind.GameInput,
                RawMetric: 0.9,
                ModelKey: "VID_2DC8|PID_6100")
        };

        var resolved = resolver.ResolveAndRecord(readings, now);

        Assert.Single(resolved);
        Assert.Null(resolved[0].BatteryPercent);
        Assert.True(resolved[0].SuggestCalibration);
    }

    [Fact]
    public void ResolveAndRecord_GameInputHighWithoutCalibration_KeepsPercent()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;

        var readings = new List<PnpBatteryReading>
        {
            new(
                InstanceId: "GAMEINPUT_SLOT_0",
                Address: "A1B2C3D4E5F6",
                DisplayName: "Xbox Wireless Controller",
                BatteryPercent: 72,
                BatteryConfidence: BatteryConfidence.Confirmed,
                SourceKind: BatterySourceKind.GameInput,
                RawMetric: 6.2,
                ModelKey: "VID_2DC8|PID_6100")
        };

        var resolved = resolver.ResolveAndRecord(readings, now);

        Assert.Single(resolved);
        Assert.Equal(72, resolved[0].BatteryPercent);
    }

    [Fact]
    public void ResolveAndRecord_GameInputSuspiciousFixedHigh_HidesPercentAsNa()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;

        var readings = new List<PnpBatteryReading>
        {
            new(
                InstanceId: "GAMEINPUT_SLOT_0",
                Address: "AABBCCDDE003",
                DisplayName: "Xbox Wireless Controller",
                BatteryPercent: 100,
                BatteryConfidence: BatteryConfidence.Estimated,
                SourceKind: BatterySourceKind.GameInput,
                RawMetric: 100,
                ModelKey: "ID=VID_045E|PID_0B22|TR=VID_045E|PID_0B22|FP=FP_AABBCCDDE003",
                SuggestCalibration: true,
                IsBatterySuspect: true)
        };

        var resolved = resolver.ResolveAndRecord(readings, now);

        Assert.Single(resolved);
        Assert.Null(resolved[0].BatteryPercent);
        Assert.Equal(BatteryDisplayState.NA, resolved[0].DisplayState);
        Assert.Equal("gameinput_hold_low_confidence", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_GameInputSevereDrop_HidesPercentAsSuspect()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "VID_2DC8|PID_6100";
        var address = "A1B2C3D4E5F6";
        var displayName = "Xbox Wireless Controller";
        var firstNow = DateTimeOffset.UtcNow;
        var secondNow = firstNow.AddMinutes(1);

        var first = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "GAMEINPUT_SLOT_0",
                    Address: address,
                    DisplayName: displayName,
                    BatteryPercent: 96,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.GameInput,
                    RawMetric: 7.8,
                    ModelKey: modelKey)
            ],
            firstNow);
        Assert.Single(first);
        Assert.Equal(96, first[0].BatteryPercent);

        var second = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "GAMEINPUT_SLOT_0",
                    Address: address,
                    DisplayName: displayName,
                    BatteryPercent: 10,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.GameInput,
                    RawMetric: 0.9,
                    ModelKey: modelKey)
            ],
            secondNow);

        Assert.Single(second);
        Assert.Null(second[0].BatteryPercent);
        Assert.True(second[0].IsBatterySuspect);
    }

    [Fact]
    public void ResolveAndRecord_GameInputWithCalibration_UsesAnchorAsHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var modelKey = "VID_2DC8|PID_6100";
        calibrationStore.UpsertFullAnchor(modelKey, 1.0, DateTimeOffset.UtcNow.AddMinutes(-1));

        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        var readings = new List<PnpBatteryReading>
        {
            new(
                InstanceId: "GAMEINPUT_SLOT_0",
                Address: "A1B2C3D4E5F6",
                DisplayName: "Xbox Wireless Controller",
                BatteryPercent: 10,
                BatteryConfidence: BatteryConfidence.Confirmed,
                SourceKind: BatterySourceKind.GameInput,
                RawMetric: 1.0,
                ModelKey: modelKey)
        };

        var resolved = resolver.ResolveAndRecord(readings, now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.False(resolved[0].SuggestCalibration);
        Assert.Equal(BatteryConfidence.Confirmed, resolved[0].BatteryConfidence);
    }

    [Fact]
    public void ResolveAndRecord_SonyPico2WDualSenseSingleUpwardBucketSpike_HoldsPreviousStableValue()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        RecordSonyPico2WObservation(observationStore, 75, now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                CreateSonyPico2WReading(85)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(75, resolved[0].BatteryPercent);
        Assert.Equal(85, resolved[0].RawMetric);
        Assert.Equal(BatteryConfidence.Estimated, resolved[0].BatteryConfidence);
        Assert.True(resolved[0].IsBatterySuspect);
        Assert.Equal("sony_hid_usb_pico2w_hold_previous_stable", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SonyPico2WDualSenseRepeatedUpwardBucketSpike_AcceptsRepeatedValue()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        RecordSonyPico2WObservation(observationStore, 75, now.AddMinutes(-2));

        var first = resolver.ResolveAndRecord(
            [
                CreateSonyPico2WReading(85)
            ],
            now);
        var second = resolver.ResolveAndRecord(
            [
                CreateSonyPico2WReading(85)
            ],
            now.AddSeconds(8));

        Assert.Single(first);
        Assert.Equal(75, first[0].BatteryPercent);
        Assert.Single(second);
        Assert.Equal(85, second[0].BatteryPercent);
        Assert.Equal(85, second[0].RawMetric);
        Assert.Equal(BatteryConfidence.Confirmed, second[0].BatteryConfidence);
        Assert.False(second[0].IsBatterySuspect);
        Assert.Equal("sony_hid_usb_pico2w_confirmed_after_repeat", second[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SonyBluetoothDualSense_IsNotHeldByPico2WGuard()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        RecordSonyPico2WObservation(observationStore, 75, now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                CreateSonyPico2WReading(85) with
                {
                    DisplayName = "DualSense Wireless Controller",
                    ReasonCode = "sony_hid_bluetooth",
                    PathType = "bluetooth_hid"
                }
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(85, resolved[0].BatteryPercent);
        Assert.Equal("sonyhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonChargingFullWithRecentNonFull_KeepsReportedHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: address,
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 96,
                    RawMetric: 96,
                    ObservedAt: now.AddMinutes(-4))
            ],
            now.AddMinutes(-4));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charging",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.True(resolved[0].IsCharging);
        Assert.Equal(BatteryDisplayState.Charging, resolved[0].DisplayState);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonChargingFullWithSameDayNonFull_KeepsReportedHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: address,
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 95,
                    RawMetric: 95,
                    ObservedAt: now.AddHours(-6))
            ],
            now.AddHours(-6));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charging",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonChargingFullWithRecentInternalAndVoltage_UsesInternalPercent()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: address,
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 98,
                    RawMetric: 98,
                    ObservedAt: now.AddMinutes(-4)),
                new BatteryEvidence(
                    Address: address,
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 96,
                    RawMetric: 4133,
                    ObservedAt: now.AddMinutes(-1))
            ],
            now.AddMinutes(-1));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 4133,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charging",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(98, resolved[0].BatteryPercent);
        Assert.Equal(98, resolved[0].RawMetric);
        Assert.Equal("steam_triton_recent_nonfull_hold", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonChargingFullWithVoltageAndNoRecentInternal_UsesVoltageFallback()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 4133,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charging",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(96, resolved[0].BatteryPercent);
        Assert.Equal(4133, resolved[0].RawMetric);
        Assert.Equal(BatteryConfidence.Estimated, resolved[0].BatteryConfidence);
        Assert.Equal("steam_triton_voltage_estimated_charging", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonChargeCompleteFull_KeepsHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: address,
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 98,
                    RawMetric: 98,
                    ObservedAt: now.AddMinutes(-4))
            ],
            now.AddMinutes(-4));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charge_complete",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true,
                    IsChargeComplete: true)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.True(resolved[0].IsCharging);
        Assert.True(resolved[0].IsChargeComplete);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonWirelessNearFullAfterChargeComplete_LatchesHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var fullAt = DateTimeOffset.UtcNow;
        var undockedAt = fullAt.AddSeconds(30);

        var docked = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charge_complete",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true,
                    IsChargeComplete: true)
            ],
            fullAt);
        Assert.Single(docked);
        Assert.Equal(100, docked[0].BatteryPercent);

        var undocked = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 97,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 97,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_battery",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Verified)
            ],
            undockedAt);

        Assert.Single(undocked);
        Assert.Equal(100, undocked[0].BatteryPercent);
        Assert.Equal(97, undocked[0].RawMetric);
        Assert.False(undocked[0].IsCharging);
        Assert.False(undocked[0].IsChargeComplete);
        Assert.Equal(BatteryDisplayState.Verified, undocked[0].DisplayState);
        Assert.Equal("steam_triton_charge_complete_latched", undocked[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonWirelessNearFullWithoutChargeComplete_KeepsReportedPercent()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 97,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 97,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_battery",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Verified)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(97, resolved[0].BatteryPercent);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonWirelessNearFullWithOtherAddressChargeComplete_KeepsReportedPercent()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var now = DateTimeOffset.UtcNow;

        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: "AAAAAAAAAAAA",
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 100,
                    RawMetric: 100,
                    ObservedAt: now.AddMinutes(-2),
                    IsCharging: true,
                    IsChargeComplete: true,
                    ReasonCode: "steam_triton_charge_complete")
            ],
            now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: "AABBCCDDE010",
                    DisplayName: "Steam Controller",
                    BatteryPercent: 97,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 97,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_battery",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Verified)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(97, resolved[0].BatteryPercent);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonWirelessNinetyFourAfterChargeComplete_TreatsAsRealDrain()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: address,
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 100,
                    RawMetric: 100,
                    ObservedAt: now.AddMinutes(-2),
                    IsCharging: true,
                    IsChargeComplete: true,
                    ReasonCode: "steam_triton_charge_complete")
            ],
            now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 94,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 94,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_battery",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Verified)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(94, resolved[0].BatteryPercent);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamControllerBluetoothNearFullAfterPuckChargeComplete_LatchesHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        RecordSteamPuckChargeComplete(observationStore, now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "BTHLEDEVICE\\{0000180F-0000-1000-8000-00805F9B34FB}_DEV_VID&0228DE_PID&1303_REV&0100_AABBCCDDE011",
                    Address: "AABBCCDDE011",
                    DisplayName: "Steam Ctrl (BT) SAMPLE000001",
                    BatteryPercent: 97,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.BleGatt,
                    RawMetric: 97,
                    ModelKey: "VID_28DE|PID_1303",
                    ReasonCode: "blegatt_direct",
                    ActiveSource: "blegatt",
                    PathType: "bluetooth",
                    DisplayState: BatteryDisplayState.Verified)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.Equal(97, resolved[0].RawMetric);
        Assert.False(resolved[0].IsCharging);
        Assert.False(resolved[0].IsChargeComplete);
        Assert.Equal(BatteryConfidence.Confirmed, resolved[0].BatteryConfidence);
        Assert.Equal("steam_controller_bluetooth_charge_complete_latched", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamControllerBluetoothNearFullWithoutPuckChargeComplete_KeepsReportedPercent()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;

        var resolved = resolver.ResolveAndRecord(
            [
                CreateSteamBluetoothReading(97, rawMetric: 97, BatterySourceKind.BleGatt)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(97, resolved[0].BatteryPercent);
        Assert.Equal("blegatt_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamControllerBluetoothNinetyFourAfterPuckChargeComplete_TreatsAsRealDrain()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        RecordSteamPuckChargeComplete(observationStore, now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                CreateSteamBluetoothReading(94, rawMetric: 94, BatterySourceKind.BleGatt)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(94, resolved[0].BatteryPercent);
        Assert.Equal("blegatt_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_NonSteamBluetoothNearFullAfterPuckChargeComplete_KeepsReportedPercent()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        RecordSteamPuckChargeComplete(observationStore, now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "BTHLE\\DEV_112233445566",
                    Address: "112233445566",
                    DisplayName: "Wireless Controller",
                    BatteryPercent: 97,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.BleGatt,
                    RawMetric: 97,
                    ModelKey: "VID_054C|PID_0CE6",
                    ReasonCode: "blegatt_direct",
                    ActiveSource: "blegatt",
                    PathType: "bluetooth",
                    DisplayState: BatteryDisplayState.Verified)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(97, resolved[0].BatteryPercent);
        Assert.Equal("blegatt_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamControllerBluetoothGameInputNearFullAfterPuckChargeComplete_LatchesHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;
        RecordSteamPuckChargeComplete(observationStore, now.AddMinutes(-2));

        var resolved = resolver.ResolveAndRecord(
            [
                CreateSteamBluetoothReading(96, rawMetric: 812, BatterySourceKind.GameInput)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.Equal(812, resolved[0].RawMetric);
        Assert.False(resolved[0].SuggestCalibration);
        Assert.False(resolved[0].IsBatterySuspect);
        Assert.Equal("steam_controller_bluetooth_charge_complete_latched", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonChargingFullWithoutRecentNonFull_KeepsReportedHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charging",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.True(resolved[0].IsCharging);
        Assert.False(resolved[0].IsBatterySuspect);
        Assert.Equal(BatteryDisplayState.Charging, resolved[0].DisplayState);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonChargingFullWithOtherHundred_KeepsReportedHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_charging",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true),
                new PnpBatteryReading(
                    InstanceId: "SETUPAPI",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Estimated,
                    SourceKind: BatterySourceKind.SetupApi,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "setupapi_estimated",
                    ActiveSource: "setupapi",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Estimated)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.True(resolved[0].IsCharging);
        Assert.Equal("steamhid_direct", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonVoltageEstimateWithOtherHundred_KeepsEstimate()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 94,
                    BatteryConfidence: BatteryConfidence.Estimated,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 4100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_voltage_estimated_charging",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Charging,
                    IsCharging: true),
                new PnpBatteryReading(
                    InstanceId: "SETUPAPI",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Estimated,
                    SourceKind: BatterySourceKind.SetupApi,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "setupapi_estimated",
                    ActiveSource: "setupapi",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Estimated)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(94, resolved[0].BatteryPercent);
        Assert.True(resolved[0].IsCharging);
        Assert.Equal("steam_triton_voltage_estimated_charging", resolved[0].ReasonCode);
    }

    [Fact]
    public void ResolveAndRecord_SteamTritonWirelessFullWithoutCharging_KeepsHundred()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;

        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: address,
                    ModelKey: modelKey,
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 96,
                    RawMetric: 96,
                    ObservedAt: now.AddMinutes(-4))
            ],
            now.AddMinutes(-4));

        var resolved = resolver.ResolveAndRecord(
            [
                new PnpBatteryReading(
                    InstanceId: "steam-triton:AABBCCDDE010",
                    Address: address,
                    DisplayName: "Steam Controller",
                    BatteryPercent: 100,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SteamHid,
                    RawMetric: 100,
                    ModelKey: modelKey,
                    ReasonCode: "steam_triton_battery",
                    ActiveSource: "steamhid",
                    PathType: "receiver",
                    DisplayState: BatteryDisplayState.Verified,
                    IsCharging: false)
            ],
            now);

        Assert.Single(resolved);
        Assert.Equal(100, resolved[0].BatteryPercent);
        Assert.False(resolved[0].IsCharging);
        Assert.Equal(BatteryDisplayState.Verified, resolved[0].DisplayState);
    }

    [Fact]
    public void ResolveAndRecord_ConflictWithoutTrustedWinner_HoldsOutput()
    {
        var root = CreateTempDirectory();
        var observationStore = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var calibrationStore = new CalibrationStore(Path.Combine(root, "calibrations.json"));
        var resolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var now = DateTimeOffset.UtcNow;

        var readings = new List<PnpBatteryReading>
        {
            new(
                InstanceId: "GI",
                Address: "A1B2C3D4E5F6",
                DisplayName: "Controller",
                BatteryPercent: 30,
                BatteryConfidence: BatteryConfidence.Confirmed,
                SourceKind: BatterySourceKind.GameInput,
                RawMetric: 3.0,
                ModelKey: "VID_2DC8|PID_6100"),
            new(
                InstanceId: "SETUP",
                Address: "A1B2C3D4E5F6",
                DisplayName: "Controller",
                BatteryPercent: 80,
                BatteryConfidence: BatteryConfidence.Confirmed,
                SourceKind: BatterySourceKind.SetupApi,
                RawMetric: 80,
                ModelKey: "VID_2DC8|PID_6100")
        };

        var resolved = resolver.ResolveAndRecord(readings, now);

        Assert.Single(resolved);
        Assert.Null(resolved[0].BatteryPercent);
    }

    [Fact]
    public void ObservationStore_Record_KeepsLatest64PerModel()
    {
        var root = CreateTempDirectory();
        var store = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var modelKey = "VID_2DC8|PID_6100";
        var now = DateTimeOffset.UtcNow;

        var batch = Enumerable.Range(0, 80)
            .Select(index => new BatteryEvidence(
                Address: "A1B2C3D4E5F6",
                ModelKey: modelKey,
                SourceKind: BatterySourceKind.GameInput,
                DerivedPercent: Math.Clamp(index, 0, 100),
                RawMetric: index + 0.1,
                ObservedAt: now.AddMinutes(index)))
            .ToList();

        store.Record(batch, now.AddHours(2));
        var recent = store.GetRecentForModel(modelKey, BatterySourceKind.GameInput, now.AddHours(2));

        Assert.Equal(64, recent.Count);
        Assert.Equal(16, recent[0].DerivedPercent);
        Assert.Equal(79, recent[^1].DerivedPercent);
    }

    [Fact]
    public void ObservationStore_Record_PreservesSteamChargeCompleteAnchorBeyondLatest64()
    {
        var root = CreateTempDirectory();
        var store = new BatteryObservationStore(Path.Combine(root, "observations.jsonl"));
        var modelKey = "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK";
        var address = "AABBCCDDE010";
        var now = DateTimeOffset.UtcNow;
        var batch = new List<BatteryEvidence>
        {
            new(
                Address: address,
                ModelKey: modelKey,
                SourceKind: BatterySourceKind.SteamHid,
                DerivedPercent: 100,
                RawMetric: 100,
                ObservedAt: now,
                IsCharging: true,
                IsChargeComplete: true,
                ReasonCode: "steam_triton_charge_complete")
        };
        batch.AddRange(Enumerable.Range(1, 80)
            .Select(index => new BatteryEvidence(
                Address: address,
                ModelKey: modelKey,
                SourceKind: BatterySourceKind.SteamHid,
                DerivedPercent: 97,
                RawMetric: 97,
                ObservedAt: now.AddMinutes(index),
                ReasonCode: "steam_triton_battery")));

        store.Record(batch, now.AddMinutes(81));
        var recent = store.GetRecentForModel(modelKey, BatterySourceKind.SteamHid, now.AddMinutes(81));

        Assert.Equal(65, recent.Count);
        Assert.Contains(recent, item => item.IsChargeComplete && item.DerivedPercent == 100);
    }

    [Fact]
    public void ObservationStore_LoadsLegacyObservationWithoutChargeFields()
    {
        var root = CreateTempDirectory();
        var path = Path.Combine(root, "observations.jsonl");
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(
            path,
            $"{{\"address\":\"A1B2C3D4E5F6\",\"modelKey\":\"VID_2DC8|PID_6100\",\"sourceKind\":2,\"derivedPercent\":77,\"rawMetric\":77,\"observedAt\":\"{now:O}\"}}{Environment.NewLine}");
        var store = new BatteryObservationStore(path);

        var recent = store.GetRecentForModel("VID_2DC8|PID_6100", BatterySourceKind.GameInput, now);

        Assert.Single(recent);
        Assert.Equal(77, recent[0].DerivedPercent);
        Assert.False(recent[0].IsCharging);
        Assert.False(recent[0].IsChargeComplete);
        Assert.Equal(string.Empty, recent[0].ReasonCode);
    }

    private static void RecordSteamPuckChargeComplete(BatteryObservationStore observationStore, DateTimeOffset observedAt)
    {
        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: "AABBCCDDE010",
                    ModelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK",
                    SourceKind: BatterySourceKind.SteamHid,
                    DerivedPercent: 100,
                    RawMetric: 100,
                    ObservedAt: observedAt,
                    IsCharging: true,
                    IsChargeComplete: true,
                    ReasonCode: "steam_triton_charge_complete")
            ],
            observedAt);
    }

    private static void RecordSonyPico2WObservation(BatteryObservationStore observationStore, int batteryPercent, DateTimeOffset observedAt)
    {
        observationStore.Record(
            [
                new BatteryEvidence(
                    Address: "AABBCCDDE020",
                    ModelKey: "VID_054C|PID_0CE6",
                    SourceKind: BatterySourceKind.SonyHid,
                    DerivedPercent: batteryPercent,
                    RawMetric: batteryPercent,
                    ObservedAt: observedAt,
                    ReasonCode: "sony_hid_usb_pico2w_dualsense")
            ],
            observedAt);
    }

    private static PnpBatteryReading CreateSonyPico2WReading(int batteryPercent)
    {
        return new PnpBatteryReading(
            InstanceId: @"USB\VID_054C&PID_0CE6\DUALSENSE_PICO2W",
            Address: "AABBCCDDE020",
            DisplayName: "DualSense Wireless Controller (USB/Pico2W)",
            BatteryPercent: batteryPercent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            SourceKind: BatterySourceKind.SonyHid,
            RawMetric: batteryPercent,
            ModelKey: "VID_054C|PID_0CE6",
            ReasonCode: "sony_hid_usb_pico2w_dualsense",
            ActiveSource: "sony_hid",
            PathType: "usb_pico2w",
            DisplayState: BatteryDisplayState.Verified);
    }

    private static PnpBatteryReading CreateSteamBluetoothReading(
        int batteryPercent,
        double rawMetric,
        BatterySourceKind sourceKind)
    {
        return new PnpBatteryReading(
            InstanceId: "BTHLEDEVICE\\{0000180F-0000-1000-8000-00805F9B34FB}_DEV_VID&0228DE_PID&1303_REV&0100_AABBCCDDE011",
            Address: "AABBCCDDE011",
            DisplayName: "Steam Ctrl (BT) SAMPLE000001",
            BatteryPercent: batteryPercent,
            BatteryConfidence: BatteryConfidence.Confirmed,
            SourceKind: sourceKind,
            RawMetric: rawMetric,
            ModelKey: "VID_28DE|PID_1303",
            ReasonCode: $"{sourceKind.ToString().ToLowerInvariant()}_direct",
            ActiveSource: sourceKind.ToString().ToLowerInvariant(),
            PathType: "bluetooth",
            DisplayState: BatteryDisplayState.Verified);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "BlossTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
