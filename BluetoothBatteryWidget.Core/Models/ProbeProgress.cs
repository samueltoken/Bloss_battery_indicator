namespace BluetoothBatteryWidget.Core.Models;

public sealed record ProbeProgress(
    ProbeStage Stage,
    int Percent,
    string Status
);
