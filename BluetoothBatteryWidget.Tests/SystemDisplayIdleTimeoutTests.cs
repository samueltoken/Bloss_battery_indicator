using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class SystemDisplayIdleTimeoutTests
{
    [Fact]
    public void SelectCurrentTimeout_UsesAcValueWhenPowerIsOnline()
    {
        var result = SystemDisplayIdleTimeout.SelectCurrentTimeout(
            acSeconds: 3600,
            dcSeconds: 600,
            PowerLineKind.Online);

        Assert.Equal(TimeSpan.FromMinutes(60), result);
    }

    [Fact]
    public void SelectCurrentTimeout_UsesDcValueWhenPowerIsOffline()
    {
        var result = SystemDisplayIdleTimeout.SelectCurrentTimeout(
            acSeconds: 3600,
            dcSeconds: 600,
            PowerLineKind.Offline);

        Assert.Equal(TimeSpan.FromMinutes(10), result);
    }

    [Theory]
    [InlineData(3600, 0, 2)]
    [InlineData(0, 600, 1)]
    public void SelectCurrentTimeout_TreatsCurrentNeverAsNoTimeout(uint acSeconds, uint dcSeconds, int powerLineValue)
    {
        var powerLine = (PowerLineKind)powerLineValue;

        var result = SystemDisplayIdleTimeout.SelectCurrentTimeout(acSeconds, dcSeconds, powerLine);

        Assert.Null(result);
    }

    [Fact]
    public void SelectCurrentTimeout_UsesShortestPositiveOnlyWhenPowerLineIsUnknown()
    {
        var result = SystemDisplayIdleTimeout.SelectCurrentTimeout(
            acSeconds: 3600,
            dcSeconds: 600,
            PowerLineKind.Unknown);

        Assert.Equal(TimeSpan.FromMinutes(10), result);
    }

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(2, false, true)]
    [InlineData(0, true, true)]
    public void WritePolicy_OnlyChangesTheCurrentPowerLineWhenKnown(
        int powerLineValue,
        bool expectedAc,
        bool expectedDc)
    {
        var powerLine = (PowerLineKind)powerLineValue;

        Assert.Equal(expectedAc, SystemDisplayIdleTimeout.ShouldWriteAc(powerLine));
        Assert.Equal(expectedDc, SystemDisplayIdleTimeout.ShouldWriteDc(powerLine));
    }

    [Fact]
    public void SelectShortestPositiveTimeout_UsesEarlierDisplayOrSleepTimeout()
    {
        var result = SystemDisplayIdleTimeout.SelectShortestPositiveTimeout(
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(5), result);
    }

    [Fact]
    public void SelectShortestPositiveTimeout_IgnoresNeverOrUnavailableTimeouts()
    {
        var result = SystemDisplayIdleTimeout.SelectShortestPositiveTimeout(
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), result);
    }
}
