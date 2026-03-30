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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "BlossTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
