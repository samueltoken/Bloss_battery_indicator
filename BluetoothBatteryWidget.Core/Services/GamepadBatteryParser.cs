using BluetoothBatteryWidget.Core.Models;

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
    private const byte SteamTritonBatteryReportId = 0x43;
    private const byte SteamTritonWirelessStatusXReportId = 0x46;
    private const byte SteamTritonWirelessStatusReportId = 0x79;
    private const int SteamTritonBatteryReportSize = 17;

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

    public static bool TryParseSteamTritonBatteryStatus(
        ReadOnlySpan<byte> report,
        out SteamControllerBatteryStatus status)
    {
        status = null!;
        if (report.Length < SteamTritonBatteryReportSize || report[0] != SteamTritonBatteryReportId)
        {
            return false;
        }

        var percent = report[2];
        if (percent > 100)
        {
            return false;
        }

        status = new SteamControllerBatteryStatus(
            BatteryPercent: percent,
            ChargeState: ParseSteamTritonChargeState(report[1]),
            BatteryVoltage: ReadUInt16LittleEndian(report, 3),
            SystemVoltage: ReadUInt16LittleEndian(report, 5),
            InputVoltage: ReadUInt16LittleEndian(report, 7),
            Current: ReadUInt16LittleEndian(report, 9),
            InputCurrent: ReadUInt16LittleEndian(report, 11),
            Temperature: ReadUInt16LittleEndian(report, 15));
        return true;
    }

    public static bool TryParseSteamTritonWirelessConnected(ReadOnlySpan<byte> report, out bool connected)
    {
        connected = false;
        if (report.Length < 2 ||
            (report[0] != SteamTritonWirelessStatusXReportId && report[0] != SteamTritonWirelessStatusReportId))
        {
            return false;
        }

        switch (report[1])
        {
            case 1:
                connected = false;
                return true;
            case 2:
                connected = true;
                return true;
            default:
                return false;
        }
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

    private static SteamControllerChargeState ParseSteamTritonChargeState(byte value)
    {
        return value switch
        {
            0 => SteamControllerChargeState.Reset,
            1 => SteamControllerChargeState.Discharging,
            2 => SteamControllerChargeState.Charging,
            3 => SteamControllerChargeState.SourceValidate,
            4 => SteamControllerChargeState.ChargingDone,
            _ => SteamControllerChargeState.Unknown
        };
    }

    private static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> source, int offset)
    {
        return (ushort)(source[offset] | (source[offset + 1] << 8));
    }
}
