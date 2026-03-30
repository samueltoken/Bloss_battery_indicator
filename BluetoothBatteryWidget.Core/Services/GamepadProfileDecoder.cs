using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class GamepadProfileDecoder
{
    public static bool TryDecode(GamepadBatteryProfile profile, ReadOnlySpan<byte> report, out int batteryPercent)
    {
        batteryPercent = 0;

        if (profile.Offset < 0 || profile.Offset >= report.Length)
        {
            return false;
        }

        var raw = report[profile.Offset];
        return profile.Decoder switch
        {
            GamepadProbeCandidateEvaluator.DecoderPercent100 => TryDecodePercent100(raw, out batteryPercent),
            GamepadProbeCandidateEvaluator.DecoderPercent255 => TryDecodePercent255(raw, out batteryPercent),
            GamepadProbeCandidateEvaluator.DecoderNibble10 => TryDecodeNibble10(raw, out batteryPercent),
            GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags => XboxBluetoothBatteryDecoder.TryDecode(profile.ReportId, report, out batteryPercent, out _),
            _ => false
        };
    }

    private static bool TryDecodePercent100(byte raw, out int percent)
    {
        if (raw > 100)
        {
            percent = 0;
            return false;
        }

        percent = raw;
        return true;
    }

    private static bool TryDecodePercent255(byte raw, out int percent)
    {
        percent = (int)Math.Round(raw / 255d * 100d, MidpointRounding.AwayFromZero);
        return percent is >= 0 and <= 100;
    }

    private static bool TryDecodeNibble10(byte raw, out int percent)
    {
        var nibble = raw & 0x0F;
        if (nibble > 10)
        {
            percent = 0;
            return false;
        }

        percent = nibble == 10 ? 100 : nibble * 10 + 5;
        return true;
    }
}
