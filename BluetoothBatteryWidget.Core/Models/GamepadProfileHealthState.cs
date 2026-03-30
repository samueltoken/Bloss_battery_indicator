namespace BluetoothBatteryWidget.Core.Models;

public sealed record GamepadProfileHealthState(
    int NoSignalStrike = 0,
    int WeakSignalStrike = 0,
    int MismatchStrike = 0,
    DateTimeOffset? LastHealthyAt = null,
    int ConsecutiveSuccessCount = 0
);

public sealed record ProfileStateTransition(
    GamepadProfileState Before,
    GamepadProfileState After,
    RevalidationFailureKind FailureKind,
    GamepadProfileHealthState Health
);
