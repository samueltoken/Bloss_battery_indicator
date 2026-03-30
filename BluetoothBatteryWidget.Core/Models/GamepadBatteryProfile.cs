namespace BluetoothBatteryWidget.Core.Models;

public sealed record GamepadBatteryProfile(
    string VendorId,
    string ProductId,
    byte ReportId,
    int ReportLength,
    int Offset,
    string Decoder,
    int Score,
    BatteryConfidence Confidence = BatteryConfidence.Confirmed,
    GamepadProfileState State = GamepadProfileState.Active,
    string IdentityKey = ""
);
