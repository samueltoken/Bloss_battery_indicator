namespace BluetoothBatteryWidget.Core.Models;

public sealed record BatteryEvidence(
    string Address,
    string ModelKey,
    BatterySourceKind SourceKind,
    int DerivedPercent,
    double? RawMetric,
    DateTimeOffset ObservedAt
);
