using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class PowerIdlePolicyTests
{
    [Theory]
    [InlineData(-1, 999, false)]
    [InlineData(0, 30, false)]
    [InlineData(1, 30, false)]
    [InlineData(1, 60, true)]
    [InlineData(5, 299, false)]
    [InlineData(5, 300, true)]
    public void ShouldPauseBackgroundWork_UsesConfiguredIdleMinutes(int minutes, int idleSeconds, bool expected)
    {
        var result = PowerIdlePolicy.ShouldPauseBackgroundWork(
            minutes,
            TimeSpan.FromSeconds(idleSeconds),
            isProbeRunning: false,
            isRefreshRunning: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ShouldPauseBackgroundWork_KeepsActiveWorkRunning(bool isProbeRunning, bool isRefreshRunning)
    {
        var result = PowerIdlePolicy.ShouldPauseBackgroundWork(
            configuredIdleMinutes: 1,
            systemIdleDuration: TimeSpan.FromMinutes(30),
            isProbeRunning,
            isRefreshRunning);

        Assert.False(result);
    }

    [Fact]
    public void ResolveIdleDelay_AutoUsesWindowsDisplayTimeoutBeforeScreenTurnsOff()
    {
        var result = PowerIdlePolicy.ResolveIdleDelay(
            WidgetSettings.AutoPowerIdlePauseMinutes,
            TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromSeconds(15), result);
    }

    [Fact]
    public void ResolveIdleDelay_AutoLeavesEnoughRoomBeforeNextRefresh()
    {
        var result = PowerIdlePolicy.ResolveIdleDelay(
            WidgetSettings.AutoPowerIdlePauseMinutes,
            TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(15), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void ResolveIdleDelay_AutoStaysOffWhenWindowsDisplayTimeoutIsUnavailable(int? displaySeconds)
    {
        var displayTimeout = displaySeconds is null
            ? (TimeSpan?)null
            : TimeSpan.FromSeconds(displaySeconds.Value);

        var result = PowerIdlePolicy.ResolveIdleDelay(
            WidgetSettings.AutoPowerIdlePauseMinutes,
            displayTimeout);

        Assert.Null(result);
    }

    [Fact]
    public void ShouldPauseBackgroundWork_UsesResolvedAutoDelay()
    {
        Assert.False(PowerIdlePolicy.ShouldPauseBackgroundWork(
            TimeSpan.FromSeconds(45),
            TimeSpan.FromSeconds(44),
            isProbeRunning: false,
            isRefreshRunning: false));
        Assert.True(PowerIdlePolicy.ShouldPauseBackgroundWork(
            TimeSpan.FromSeconds(45),
            TimeSpan.FromSeconds(45),
            isProbeRunning: false,
            isRefreshRunning: false));
    }

    [Fact]
    public void ShouldPauseBackgroundWork_UsesLocalIdleWhenControllerNoiseResetsWindowsIdle()
    {
        var result = PowerIdlePolicy.ShouldPauseBackgroundWork(
            TimeSpan.FromSeconds(45),
            systemIdleDuration: TimeSpan.FromSeconds(2),
            localIdleDuration: TimeSpan.FromSeconds(46),
            isProbeRunning: false,
            isRefreshRunning: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ShouldPauseBackgroundWork_LocalIdleStillKeepsActiveWorkRunning(bool isProbeRunning, bool isRefreshRunning)
    {
        var result = PowerIdlePolicy.ShouldPauseBackgroundWork(
            TimeSpan.FromSeconds(45),
            systemIdleDuration: TimeSpan.FromSeconds(2),
            localIdleDuration: TimeSpan.FromMinutes(5),
            isProbeRunning,
            isRefreshRunning);

        Assert.False(result);
    }
}
