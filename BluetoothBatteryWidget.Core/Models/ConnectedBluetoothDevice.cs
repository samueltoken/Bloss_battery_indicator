namespace BluetoothBatteryWidget.Core.Models;

public sealed record ConnectedBluetoothDevice(
    string DeviceId,
    string Address,
    string DisplayName,
    bool IsConnected,
    string? CategoryHint
);
