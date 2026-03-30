namespace BluetoothBatteryWidget.Core.Models;

public sealed record HidFeatureBatteryReading(
    string Address,
    string InstanceId,
    string DisplayName,
    string VendorId,
    string ProductId,
    int BatteryPercent,
    int Score
);
