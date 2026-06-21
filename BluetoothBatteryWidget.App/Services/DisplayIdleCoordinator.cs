namespace BluetoothBatteryWidget.App.Services;

internal enum PowerIdleRuntimeMode
{
    Active,
    DisplayIdleQuiet,
    DisplayOffWakeOnly,
    WakeRecovery
}

internal sealed class DisplayIdleCoordinator
{
    private static readonly TimeSpan DefaultQuietLead = TimeSpan.FromSeconds(15);

    public bool IsActive { get; private set; }

    public string LastReason { get; private set; } = string.Empty;

    public PowerIdleRuntimeMode ResolveMode(
        DateTimeOffset now,
        DisplayPowerState displayState,
        DateTimeOffset wakeRecoveryUntilUtc,
        TimeSpan? displayTimeout,
        TimeSpan systemIdle,
        TimeSpan localIdle,
        bool isGuideCaptureActive,
        bool isRefreshRunning,
        bool isProbeRunning,
        bool hasConnectedGamepad)
    {
        if (displayState is DisplayPowerState.Off or DisplayPowerState.Dimmed)
        {
            return PowerIdleRuntimeMode.DisplayOffWakeOnly;
        }

        if (now < wakeRecoveryUntilUtc)
        {
            return PowerIdleRuntimeMode.WakeRecovery;
        }

        if (ShouldEnterQuiet(
                displayTimeout,
                systemIdle,
                localIdle,
                isGuideCaptureActive,
                isRefreshRunning,
                isProbeRunning,
                hasConnectedGamepad))
        {
            return PowerIdleRuntimeMode.DisplayIdleQuiet;
        }

        return PowerIdleRuntimeMode.Active;
    }

    public bool ShouldEnterQuiet(
        TimeSpan? displayTimeout,
        TimeSpan systemIdle,
        TimeSpan localIdle,
        bool isGuideCaptureActive,
        bool isRefreshRunning,
        bool isProbeRunning,
        bool hasConnectedGamepad)
    {
        if (displayTimeout is null || displayTimeout.Value <= TimeSpan.Zero)
        {
            return false;
        }

        if (isGuideCaptureActive ||
            isRefreshRunning ||
            isProbeRunning ||
            hasConnectedGamepad)
        {
            return false;
        }

        var quietPoint = displayTimeout.Value - DefaultQuietLead;
        if (quietPoint < TimeSpan.FromSeconds(5))
        {
            quietPoint = TimeSpan.FromSeconds(5);
        }

        return systemIdle >= quietPoint && localIdle >= quietPoint;
    }

    public void Acquire(string reason)
    {
        IsActive = true;
        LastReason = reason;
    }

    public void Release(string reason)
    {
        IsActive = false;
        LastReason = reason;
    }
}
