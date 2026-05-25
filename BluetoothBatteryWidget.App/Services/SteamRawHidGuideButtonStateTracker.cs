namespace BluetoothBatteryWidget.App.Services;

internal enum SteamRawHidGuideButtonDecisionKind
{
    None = 0,
    PendingRelease = 1,
    StableShortPress = 2,
    StableLongPress = 3
}

internal readonly record struct SteamRawHidGuideButtonDecision(
    SteamRawHidGuideButtonDecisionKind Kind,
    Guid PendingId,
    DateTimeOffset ReleaseDueAt,
    TimeSpan Duration)
{
    public static SteamRawHidGuideButtonDecision None { get; } = new(
        SteamRawHidGuideButtonDecisionKind.None,
        Guid.Empty,
        default,
        TimeSpan.Zero);
}

internal sealed class SteamRawHidGuideButtonStateTracker
{
    private readonly TimeSpan _shortPressMinimumDuration;
    private readonly TimeSpan _shortPressMaximumDuration;
    private readonly TimeSpan _fastShortPressMaximumDuration;
    private readonly TimeSpan _fastReleaseStabilityDelay;
    private readonly TimeSpan _cautiousReleaseStabilityDelay;
    private readonly Dictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);

    public SteamRawHidGuideButtonStateTracker(
        TimeSpan shortPressMinimumDuration,
        TimeSpan shortPressMaximumDuration,
        TimeSpan fastShortPressMaximumDuration,
        TimeSpan fastReleaseStabilityDelay,
        TimeSpan cautiousReleaseStabilityDelay)
    {
        _shortPressMinimumDuration = shortPressMinimumDuration;
        _shortPressMaximumDuration = shortPressMaximumDuration;
        _fastShortPressMaximumDuration = fastShortPressMaximumDuration;
        _fastReleaseStabilityDelay = fastReleaseStabilityDelay;
        _cautiousReleaseStabilityDelay = cautiousReleaseStabilityDelay;
    }

    public SteamRawHidGuideButtonDecision RegisterState(
        string address,
        bool pressed,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return SteamRawHidGuideButtonDecision.None;
        }

        if (!_states.TryGetValue(address, out var state))
        {
            state = new State();
            _states[address] = state;
        }

        if (pressed)
        {
            state.IsPressed = true;
            state.PendingRelease = null;
            state.SessionStartedAt ??= now;
            return SteamRawHidGuideButtonDecision.None;
        }

        if (!state.IsPressed || state.SessionStartedAt is not { } sessionStartedAt)
        {
            state.IsPressed = false;
            return SteamRawHidGuideButtonDecision.None;
        }

        state.IsPressed = false;
        var duration = now - sessionStartedAt;
        var stabilityDelay = duration <= _fastShortPressMaximumDuration
            ? _fastReleaseStabilityDelay
            : _cautiousReleaseStabilityDelay;
        var pending = new PendingRelease(
            Guid.NewGuid(),
            sessionStartedAt,
            now,
            now + stabilityDelay);
        state.PendingRelease = pending;
        return new SteamRawHidGuideButtonDecision(
            SteamRawHidGuideButtonDecisionKind.PendingRelease,
            pending.Id,
            pending.DueAt,
            duration);
    }

    public SteamRawHidGuideButtonDecision CompletePendingRelease(
        string address,
        Guid pendingId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            pendingId == Guid.Empty ||
            !_states.TryGetValue(address, out var state) ||
            state.PendingRelease is not { } pending ||
            pending.Id != pendingId ||
            state.IsPressed)
        {
            return SteamRawHidGuideButtonDecision.None;
        }

        if (now < pending.DueAt)
        {
            return SteamRawHidGuideButtonDecision.None;
        }

        state.PendingRelease = null;
        state.SessionStartedAt = null;
        var duration = pending.ReleasedAt - pending.SessionStartedAt;
        var kind = duration >= _shortPressMinimumDuration && duration <= _shortPressMaximumDuration
            ? SteamRawHidGuideButtonDecisionKind.StableShortPress
            : SteamRawHidGuideButtonDecisionKind.StableLongPress;
        return new SteamRawHidGuideButtonDecision(kind, pending.Id, pending.DueAt, duration);
    }

    public void Clear()
    {
        _states.Clear();
    }

    private sealed class State
    {
        public bool IsPressed { get; set; }

        public DateTimeOffset? SessionStartedAt { get; set; }

        public PendingRelease? PendingRelease { get; set; }
    }

    private sealed record PendingRelease(
        Guid Id,
        DateTimeOffset SessionStartedAt,
        DateTimeOffset ReleasedAt,
        DateTimeOffset DueAt);
}
