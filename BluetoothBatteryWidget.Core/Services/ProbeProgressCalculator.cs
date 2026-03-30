namespace BluetoothBatteryWidget.Core.Services;

public static class ProbeProgressCalculator
{
    public static int DeviceCheck(double ratio) => MapRange(0, 10, ratio);

    public static int EnumerateInterfaces(double ratio) => MapRange(10, 30, ratio);

    public static int CollectReports(double ratio) => MapRange(30, 75, ratio);

    public static int EvaluateCandidates(double ratio) => MapRange(75, 95, ratio);

    public static int PersistProfile(double ratio) => MapRange(95, 100, ratio);

    private static int MapRange(int start, int end, double ratio)
    {
        var normalized = Math.Clamp(ratio, 0d, 1d);
        return start + (int)Math.Round((end - start) * normalized, MidpointRounding.AwayFromZero);
    }
}
