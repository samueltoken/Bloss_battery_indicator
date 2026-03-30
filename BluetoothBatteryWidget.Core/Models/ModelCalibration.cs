namespace BluetoothBatteryWidget.Core.Models;

public sealed record ModelCalibration(
    string ModelKey,
    double FullAnchorRawMetric,
    DateTimeOffset UpdatedAt
);
