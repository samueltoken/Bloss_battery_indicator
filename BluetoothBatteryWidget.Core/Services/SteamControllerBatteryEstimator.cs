using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class SteamControllerBatteryEstimator
{
    private static readonly (int Millivolts, int Percent)[] VoltageCurve =
    [
        (3300, 5),
        (3400, 10),
        (3500, 20),
        (3600, 35),
        (3700, 50),
        (3800, 65),
        (3900, 78),
        (4000, 88),
        (4100, 94),
        (4200, 99)
    ];

    public static bool TryEstimatePercentFromVoltage(
        SteamControllerBatteryStatus status,
        out int batteryPercent)
    {
        return TryEstimatePercentFromVoltage(status.BatteryVoltage, out batteryPercent);
    }

    public static bool TryEstimatePercentFromVoltage(
        int millivolts,
        out int batteryPercent)
    {
        batteryPercent = 0;
        if (millivolts < VoltageCurve[0].Millivolts || millivolts > VoltageCurve[^1].Millivolts + 80)
        {
            return false;
        }

        if (millivolts <= VoltageCurve[0].Millivolts)
        {
            batteryPercent = VoltageCurve[0].Percent;
            return true;
        }

        for (var index = 1; index < VoltageCurve.Length; index++)
        {
            var lower = VoltageCurve[index - 1];
            var upper = VoltageCurve[index];
            if (millivolts > upper.Millivolts)
            {
                continue;
            }

            var ratio = (millivolts - lower.Millivolts) / (double)(upper.Millivolts - lower.Millivolts);
            batteryPercent = (int)Math.Round(
                lower.Percent + ((upper.Percent - lower.Percent) * ratio),
                MidpointRounding.AwayFromZero);
            batteryPercent = Math.Clamp(batteryPercent, 1, 99);
            return true;
        }

        batteryPercent = 99;
        return true;
    }
}
