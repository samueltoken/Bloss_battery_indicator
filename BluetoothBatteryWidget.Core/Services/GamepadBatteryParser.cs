namespace BluetoothBatteryWidget.Core.Services;

public static class GamepadBatteryParser
{
    private const byte DualSenseUsbReportId = 0x01;
    private const int DualSenseUsbReportSize = 64;
    private const byte DualSenseBtReportId = 0x31;
    private const int DualSenseBtReportSize = 78;
    private const int DualSenseStatusOffsetInPayload = 52;

    private const byte DualShock4UsbReportId = 0x01;
    private const int DualShock4UsbReportSize = 64;
    private const byte DualShock4BtReportId = 0x11;
    private const int DualShock4BtReportSize = 78;
    private const int DualShock4Status0OffsetInCommon = 29;
    private const int DualShock4Status1OffsetInCommon = 30;

    public static bool TryParseDualSenseBatteryPercent(ReadOnlySpan<byte> report, out int? batteryPercent)
    {
        batteryPercent = null;
        if (!TryGetDualSenseStatus(report, out var status))
        {
            return false;
        }

        batteryPercent = ParseDualSenseStatus(status);
        return true;
    }

    public static bool TryParseDualShock4BatteryPercent(ReadOnlySpan<byte> report, out int? batteryPercent)
    {
        batteryPercent = null;
        if (!TryGetDualShock4Status(report, out var status0, out _))
        {
            return false;
        }

        batteryPercent = ParseDualShock4Status(status0);
        return true;
    }

    private static bool TryGetDualSenseStatus(ReadOnlySpan<byte> report, out byte status)
    {
        status = 0;
        if (report.Length == 0)
        {
            return false;
        }

        switch (report[0])
        {
            case DualSenseUsbReportId when report.Length >= DualSenseUsbReportSize:
            {
                var absoluteOffset = 1 + DualSenseStatusOffsetInPayload;
                status = report[absoluteOffset];
                return true;
            }
            case DualSenseBtReportId when report.Length >= DualSenseBtReportSize:
            {
                var absoluteOffset = 2 + DualSenseStatusOffsetInPayload;
                status = report[absoluteOffset];
                return true;
            }
            default:
                return false;
        }
    }

    private static bool TryGetDualShock4Status(ReadOnlySpan<byte> report, out byte status0, out byte status1)
    {
        status0 = 0;
        status1 = 0;
        if (report.Length == 0)
        {
            return false;
        }

        switch (report[0])
        {
            case DualShock4UsbReportId when report.Length >= DualShock4UsbReportSize:
            {
                var commonStart = 1;
                status0 = report[commonStart + DualShock4Status0OffsetInCommon];
                status1 = report[commonStart + DualShock4Status1OffsetInCommon];
                return true;
            }
            case DualShock4BtReportId when report.Length >= DualShock4BtReportSize:
            {
                var commonStart = 3;
                status0 = report[commonStart + DualShock4Status0OffsetInCommon];
                status1 = report[commonStart + DualShock4Status1OffsetInCommon];
                return true;
            }
            default:
                return false;
        }
    }

    private static int? ParseDualSenseStatus(byte status)
    {
        var batteryData = status & 0x0F;
        var chargingStatus = (status & 0xF0) >> 4;
        return chargingStatus switch
        {
            0x0 => Math.Min(batteryData * 10 + 5, 100),
            0x1 => Math.Min(batteryData * 10 + 5, 100),
            0x2 => 100,
            0xA => null,
            0xB => null,
            0xF => null,
            _ => null
        };
    }

    private static int? ParseDualShock4Status(byte status0)
    {
        var batteryData = status0 & 0x0F;
        var cableConnected = (status0 & 0x10) != 0;

        if (cableConnected)
        {
            return batteryData switch
            {
                < 10 => batteryData * 10 + 5,
                10 => 100,
                11 => 100,
                _ => null
            };
        }

        return batteryData < 10
            ? batteryData * 10 + 5
            : 100;
    }
}
