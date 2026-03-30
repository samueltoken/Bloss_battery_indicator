namespace BluetoothBatteryWidget.Core.Models;

public sealed record XInputBatteryReading(
    int UserIndex,
    int BatteryPercent,
    double? RawMetric = null
);
