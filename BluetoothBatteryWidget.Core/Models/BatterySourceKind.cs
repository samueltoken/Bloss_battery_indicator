namespace BluetoothBatteryWidget.Core.Models;

public enum BatterySourceKind
{
    Unknown = 0,
    SetupApi = 1,
    GameInput = 2,
    XInput = 3,
    SonyHid = 4,
    LearnedHid = 5,
    BleGatt = 6,
    HidFeature = 7
}
