using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class ProbeProgressCalculatorTests
{
    [Fact]
    public void ProgressStages_AreMonotonicAndReachHundred()
    {
        var values = new[]
        {
            ProbeProgressCalculator.DeviceCheck(0),
            ProbeProgressCalculator.DeviceCheck(1),
            ProbeProgressCalculator.EnumerateInterfaces(0),
            ProbeProgressCalculator.EnumerateInterfaces(1),
            ProbeProgressCalculator.CollectReports(0),
            ProbeProgressCalculator.CollectReports(1),
            ProbeProgressCalculator.EvaluateCandidates(0),
            ProbeProgressCalculator.EvaluateCandidates(1),
            ProbeProgressCalculator.PersistProfile(0),
            ProbeProgressCalculator.PersistProfile(1)
        };

        for (var i = 1; i < values.Length; i++)
        {
            Assert.True(values[i] >= values[i - 1], $"progress must be monotonic: {values[i - 1]} -> {values[i]}");
        }

        Assert.Equal(100, values[^1]);
    }
}
