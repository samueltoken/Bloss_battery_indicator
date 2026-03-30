namespace BluetoothBatteryWidget.Core.Models;

public sealed record PnpBatteryReading(
    string InstanceId,
    string Address,
    string DisplayName,
    int? BatteryPercent,
    BatteryConfidence BatteryConfidence = BatteryConfidence.Confirmed,
    BatterySourceKind SourceKind = BatterySourceKind.Unknown,
    double? RawMetric = null,
    string? ModelKey = null,
    bool SuggestCalibration = false,
    DateTimeOffset? ObservedAt = null,
    bool IsBatterySuspect = false
);
