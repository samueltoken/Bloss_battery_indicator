namespace BluetoothBatteryWidget.Core.Services;

public static class XInputBatteryLevelMapper
{
    public static int? ToPercent(byte batteryLevel)
    {
        return batteryLevel switch
        {
            0x00 => 5,
            0x01 => 25,
            0x02 => 55,
            0x03 => 85,
            _ => null
        };
    }
}
