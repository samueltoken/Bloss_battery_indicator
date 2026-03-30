namespace BluetoothBatteryWidget.Core.Models;

public sealed record GameInputBatteryReading(
    int SourceIndex,
    int BatteryPercent,
    double? RawMetric = null,
    double? FullMetric = null
);
