namespace BluetoothBatteryWidget.Core.Models;

public sealed record GamepadBatteryCandidate(
    byte ReportId,
    int ReportLength,
    int Offset,
    string Decoder,
    int BatteryPercent,
    int Score
);
