namespace BluetoothBatteryWidget.Core.Models;

public enum RevalidationFailureKind
{
    None = 0,
    NoSignal = 1,
    WeakSignal = 2,
    DecodeMismatch = 3,
    SpreadOutlier = 4
}
