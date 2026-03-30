namespace BluetoothBatteryWidget.Core.Models;

public sealed record BleBatteryReading(
    string Address,
    int BatteryPercent,
    string? ModelKey
);
