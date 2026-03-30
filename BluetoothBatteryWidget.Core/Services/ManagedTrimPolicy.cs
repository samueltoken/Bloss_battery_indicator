namespace BluetoothBatteryWidget.Core.Services;

public static class ManagedTrimPolicy
{
    public static bool ShouldRunManagedTrim(
        double privateMb,
        double thresholdMb,
        DateTime nowUtc,
        DateTime lastRunUtc,
        TimeSpan minInterval)
    {
        if (privateMb <= thresholdMb)
        {
            return false;
        }

        if (lastRunUtc == DateTime.MinValue)
        {
            return true;
        }

        return nowUtc - lastRunUtc >= minInterval;
    }
}
