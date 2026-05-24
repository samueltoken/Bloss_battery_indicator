namespace BluetoothBatteryWidget.Core.Models;

public sealed record SteamControllerBatteryStatus(
    int BatteryPercent,
    SteamControllerChargeState ChargeState,
    ushort BatteryVoltage,
    ushort SystemVoltage,
    ushort InputVoltage,
    ushort Current,
    ushort InputCurrent,
    ushort Temperature)
{
    public bool IsCharging => ChargeState is
        SteamControllerChargeState.Charging or
        SteamControllerChargeState.SourceValidate or
        SteamControllerChargeState.ChargingDone;

    public bool IsChargeComplete => ChargeState == SteamControllerChargeState.ChargingDone;
}

public enum SteamControllerChargeState
{
    Reset = 0,
    Discharging = 1,
    Charging = 2,
    SourceValidate = 3,
    ChargingDone = 4,
    Unknown = 255
}
