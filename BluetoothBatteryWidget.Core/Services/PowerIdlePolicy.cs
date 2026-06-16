using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class PowerIdlePolicy
{
    private static readonly TimeSpan AutoPauseLeadTime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MinimumAutoPauseDelay = TimeSpan.FromSeconds(15);

    public static bool ShouldPauseBackgroundWork(
        int configuredIdleMinutes,
        TimeSpan systemIdleDuration,
        bool isProbeRunning,
        bool isRefreshRunning)
    {
        return ShouldPauseBackgroundWork(
            ResolveIdleDelay(configuredIdleMinutes, displayIdleTimeout: null),
            systemIdleDuration,
            isProbeRunning,
            isRefreshRunning);
    }

    public static bool ShouldPauseBackgroundWork(
        int configuredIdleMinutes,
        TimeSpan systemIdleDuration,
        TimeSpan localIdleDuration,
        bool isProbeRunning,
        bool isRefreshRunning)
    {
        return ShouldPauseBackgroundWork(
            ResolveIdleDelay(configuredIdleMinutes, displayIdleTimeout: null),
            systemIdleDuration,
            localIdleDuration,
            isProbeRunning,
            isRefreshRunning);
    }

    public static bool ShouldPauseBackgroundWork(
        TimeSpan? idleDelay,
        TimeSpan systemIdleDuration,
        bool isProbeRunning,
        bool isRefreshRunning)
    {
        return ShouldPauseBackgroundWork(
            idleDelay,
            systemIdleDuration,
            localIdleDuration: TimeSpan.Zero,
            isProbeRunning,
            isRefreshRunning);
    }

    public static bool ShouldPauseBackgroundWork(
        TimeSpan? idleDelay,
        TimeSpan systemIdleDuration,
        TimeSpan localIdleDuration,
        bool isProbeRunning,
        bool isRefreshRunning)
    {
        if (idleDelay is null || idleDelay.Value <= TimeSpan.Zero || isProbeRunning || isRefreshRunning)
        {
            return false;
        }

        return systemIdleDuration >= idleDelay.Value ||
               localIdleDuration >= idleDelay.Value;
    }

    public static TimeSpan? ResolveIdleDelay(int configuredIdleMinutes, TimeSpan? displayIdleTimeout)
    {
        if (configuredIdleMinutes == WidgetSettings.AutoPowerIdlePauseMinutes)
        {
            return ResolveAutoIdleDelay(displayIdleTimeout);
        }

        if (configuredIdleMinutes <= 0)
        {
            return null;
        }

        return TimeSpan.FromMinutes(configuredIdleMinutes);
    }

    private static TimeSpan? ResolveAutoIdleDelay(TimeSpan? displayIdleTimeout)
    {
        if (displayIdleTimeout is null || displayIdleTimeout.Value <= TimeSpan.Zero)
        {
            return null;
        }

        if (displayIdleTimeout.Value <= MinimumAutoPauseDelay)
        {
            return displayIdleTimeout.Value;
        }

        var resolvedDelay = displayIdleTimeout.Value - AutoPauseLeadTime;
        return resolvedDelay <= MinimumAutoPauseDelay
            ? MinimumAutoPauseDelay
            : resolvedDelay;
    }
}
