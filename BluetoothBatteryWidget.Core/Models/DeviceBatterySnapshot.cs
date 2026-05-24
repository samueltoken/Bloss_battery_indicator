namespace BluetoothBatteryWidget.Core.Models;

public sealed record DeviceBatterySnapshot(
    string DeviceId,
    string Address,
    string DisplayName,
    int? BatteryPercent,
    BatteryConfidence BatteryConfidence,
    bool IsConnected,
    DeviceCategory Category,
    IconKey IconKey,
    DateTimeOffset LastUpdated,
    BatterySourceKind SourceKind = BatterySourceKind.Unknown,
    string? ModelKey = null,
    bool SuggestCalibration = false,
    bool IsBatterySuspect = false,
    bool IsStale = false,
    bool IsBatteryConnecting = false,
    BatteryDisplayState DisplayState = BatteryDisplayState.Unknown,
    string? BaseDisplayName = null,
    string? CustomIconImagePath = null,
    bool IsCharging = false,
    bool IsChargeComplete = false
);
