namespace BluetoothBatteryWidget.Core.Models;

public enum ProbeFailureKind
{
    Unknown = 0,
    NoSignal = 1,
    WeakSignal = 2,
    PolicyBlocked = 3,
    AddressInvalid = 4,
    StepOnly = 5,
    FixedBad = 6,
    MixedPath = 7,
    ReceiverBlocked = 8
}
