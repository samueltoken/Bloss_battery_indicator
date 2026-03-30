using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class XboxBluetoothBatteryDecoder
{
    public const string DecoderId = "xbox_bt_flags";
    private const byte XboxBluetoothBatteryReportId = 0x04;

    public static bool TryDecode(
        byte reportId,
        ReadOnlySpan<byte> report,
        out int percent,
        out bool onUsb)
    {
        percent = 0;
        onUsb = false;

        if (reportId != XboxBluetoothBatteryReportId || report.Length < 2)
        {
            return false;
        }

        var flags = report[1];
        onUsb = ((flags & 0x0C) >> 2) == 0;
        percent = (flags & 0x03) switch
        {
            0 => 10,
            1 => 40,
            2 => 70,
            3 => 100,
            _ => 0
        };

        return percent is >= 0 and <= 100;
    }

    public static bool TryDecodeFromReports(
        IReadOnlyDictionary<byte, byte[]> reports,
        out GamepadBatteryCandidate candidate)
    {
        foreach (var pair in reports)
        {
            if (!TryDecode(pair.Key, pair.Value, out var percent, out _))
            {
                continue;
            }

            candidate = new GamepadBatteryCandidate(
                ReportId: pair.Key,
                ReportLength: pair.Value.Length,
                Offset: 1,
                Decoder: DecoderId,
                BatteryPercent: percent,
                Score: 88);
            return true;
        }

        candidate = null!;
        return false;
    }
}
