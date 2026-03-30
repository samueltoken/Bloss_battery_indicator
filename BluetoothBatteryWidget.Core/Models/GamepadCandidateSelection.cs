namespace BluetoothBatteryWidget.Core.Models;

public sealed record GamepadCandidateSelection(
    GamepadBatteryCandidate? Winner,
    bool IsTie,
    int CandidateCount
);
