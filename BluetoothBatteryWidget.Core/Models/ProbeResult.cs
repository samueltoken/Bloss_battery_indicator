namespace BluetoothBatteryWidget.Core.Models;

public sealed record ProbeResult(
    bool Success,
    int? BatteryPercent,
    string Message,
    GamepadBatteryProfile? Profile,
    ProbeErrorDetail? ErrorDetail = null,
    bool IsPending = false
);
