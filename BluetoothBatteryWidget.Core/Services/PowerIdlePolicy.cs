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
            localIdleDuration: null,
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
        return ShouldPauseBackgroundWork(
            idleDelay,
            systemIdleDuration,
            (TimeSpan?)localIdleDuration,
            isProbeRunning,
            isRefreshRunning);
    }

    private static bool ShouldPauseBackgroundWork(
        TimeSpan? idleDelay,
        TimeSpan systemIdleDuration,
        TimeSpan? localIdleDuration,
        bool isProbeRunning,
        bool isRefreshRunning)
    {
        if (idleDelay is null || idleDelay.Value <= TimeSpan.Zero || isProbeRunning || isRefreshRunning)
        {
            return false;
        }

        // If the app saw real local/gamepad activity recently, keep the light guide
        // path alive. Otherwise, allow display-sleep protection even when controller
        // drivers keep resetting Windows' global idle clock.
        if (localIdleDuration.HasValue && localIdleDuration.Value < idleDelay.Value)
        {
            return false;
        }

        return systemIdleDuration >= idleDelay.Value ||
               (localIdleDuration.HasValue && localIdleDuration.Value >= idleDelay.Value);
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
