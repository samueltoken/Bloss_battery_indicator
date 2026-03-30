namespace BluetoothBatteryWidget.Core.Models;

public enum ProbeStage
{
    None = 0,
    DeviceCheck = 1,
    EnumerateInterfaces = 2,
    CollectReports = 3,
    EvaluateCandidates = 4,
    PersistProfile = 5,
    Completed = 6,
    Failed = 7
}
