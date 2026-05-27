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

internal readonly record struct SteamRawHidGuideButtonActivity(
    bool IsPressed,
    TimeSpan PressedDuration,
    TimeSpan LastStateAge,
    bool HasPendingRelease,
    TimeSpan PendingPressDuration,
    TimeSpan PendingLastPressedAge,
    TimeSpan PendingReleaseAge)
{
    public static SteamRawHidGuideButtonActivity None { get; } = new(
        IsPressed: false,
        PressedDuration: TimeSpan.Zero,
        LastStateAge: TimeSpan.Zero,
        HasPendingRelease: false,
        PendingPressDuration: TimeSpan.Zero,
        PendingLastPressedAge: TimeSpan.Zero,
        PendingReleaseAge: TimeSpan.Zero);
}

internal sealed class SteamRawHidGuideButtonStateTracker
{
    private readonly TimeSpan _shortPressMinimumDuration;
    private readonly TimeSpan _shortPressMaximumDuration;
    private readonly TimeSpan _fastShortPressMaximumDuration;
    private readonly TimeSpan _fastReleaseStabilityDelay;
    private readonly TimeSpan _cautiousReleaseStabilityDelay;
    private readonly TimeSpan _stalePressedResetGap;
    private readonly TimeSpan _missedReleaseRecoveryGap;
    private readonly Dictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);

    public SteamRawHidGuideButtonStateTracker(
        TimeSpan shortPressMinimumDuration,
        TimeSpan shortPressMaximumDuration,
        TimeSpan fastShortPressMaximumDuration,
        TimeSpan fastReleaseStabilityDelay,
        TimeSpan cautiousReleaseStabilityDelay,
        TimeSpan stalePressedResetGap,
        TimeSpan missedReleaseRecoveryGap)
    {
        _shortPressMinimumDuration = shortPressMinimumDuration;
        _shortPressMaximumDuration = shortPressMaximumDuration;
        _fastShortPressMaximumDuration = fastShortPressMaximumDuration;
        _fastReleaseStabilityDelay = fastReleaseStabilityDelay;
        _cautiousReleaseStabilityDelay = cautiousReleaseStabilityDelay;
        _stalePressedResetGap = stalePressedResetGap;
        _missedReleaseRecoveryGap = missedReleaseRecoveryGap;
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
            if (!state.IsPressed)
            {
                state.IsPressed = true;
                state.SessionStartedAt = state.PendingRelease?.SessionStartedAt ?? now;
                state.PendingRelease = null;
                state.LastStateAt = now;
                return SteamRawHidGuideButtonDecision.None;
            }

            if (state.SessionStartedAt.HasValue &&
                IsStalePressedSession(state, now))
            {
                state.SessionStartedAt = now;
                state.LastStateAt = now;
                state.PendingRelease = null;
                return SteamRawHidGuideButtonDecision.None;
            }

            if (state.SessionStartedAt is { } currentSessionStartedAt &&
                state.LastStateAt is { } lastStateAt &&
                LooksLikeMissedReleaseThenFreshPress(currentSessionStartedAt, lastStateAt, now))
            {
                state.SessionStartedAt = now;
                state.LastStateAt = now;
                state.PendingRelease = null;
                return SteamRawHidGuideButtonDecision.None;
            }

            state.IsPressed = true;
            state.PendingRelease = null;
            state.LastStateAt = now;
            return SteamRawHidGuideButtonDecision.None;
        }

        if (!state.IsPressed || state.SessionStartedAt is not { } sessionStartedAt)
        {
            state.IsPressed = false;
            state.LastStateAt = now;
            if (state.PendingRelease is null)
            {
                state.SessionStartedAt = null;
            }

            return SteamRawHidGuideButtonDecision.None;
        }

        return CompleteRelease(state, sessionStartedAt, now);
    }

    public SteamRawHidGuideButtonDecision RegisterStatusReleaseHint(
        string address,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !_states.TryGetValue(address, out var state) ||
            !state.IsPressed ||
            state.SessionStartedAt is not { } sessionStartedAt)
        {
            return SteamRawHidGuideButtonDecision.None;
        }

        var duration = now - sessionStartedAt;
        var isNormalShortTap = duration >= _shortPressMinimumDuration && duration <= _shortPressMaximumDuration;

        state.IsPressed = false;
        state.SessionStartedAt = null;
        state.PendingRelease = null;
        state.LastStateAt = now;

        if (!isNormalShortTap)
        {
            return new SteamRawHidGuideButtonDecision(
                SteamRawHidGuideButtonDecisionKind.StableLongPress,
                Guid.Empty,
                now,
                duration);
        }

        return new SteamRawHidGuideButtonDecision(
            SteamRawHidGuideButtonDecisionKind.StableShortPress,
            Guid.Empty,
            now,
            duration);
    }

    private SteamRawHidGuideButtonDecision BeginPendingRelease(
        State state,
        DateTimeOffset sessionStartedAt,
        DateTimeOffset now,
        bool forceShortPress)
    {
        state.IsPressed = false;
        var duration = now - sessionStartedAt;
        var lastPressedAt = state.LastStateAt ?? sessionStartedAt;
        var stabilityDelay = duration <= _fastShortPressMaximumDuration
            ? _fastReleaseStabilityDelay
            : _cautiousReleaseStabilityDelay;
        var pending = new PendingRelease(
            Guid.NewGuid(),
            sessionStartedAt,
            lastPressedAt,
            now,
            now + stabilityDelay,
            forceShortPress);
        state.PendingRelease = pending;
        state.LastStateAt = now;
        return new SteamRawHidGuideButtonDecision(
            SteamRawHidGuideButtonDecisionKind.PendingRelease,
            pending.Id,
            pending.DueAt,
            duration);
    }

    private SteamRawHidGuideButtonDecision CompleteRelease(
        State state,
        DateTimeOffset sessionStartedAt,
        DateTimeOffset now)
    {
        var duration = now - sessionStartedAt;
        var kind =
            duration >= _shortPressMinimumDuration && duration <= _shortPressMaximumDuration
                ? SteamRawHidGuideButtonDecisionKind.StableShortPress
                : SteamRawHidGuideButtonDecisionKind.StableLongPress;

        state.IsPressed = false;
        state.SessionStartedAt = null;
        state.PendingRelease = null;
        state.LastStateAt = now;

        return new SteamRawHidGuideButtonDecision(
            kind,
            Guid.Empty,
            now,
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
        state.LastStateAt = now;
        var duration = pending.ReleasedAt - pending.SessionStartedAt;
        var kind = pending.ForceShortPress ||
                   (duration >= _shortPressMinimumDuration && duration <= _shortPressMaximumDuration)
            ? SteamRawHidGuideButtonDecisionKind.StableShortPress
            : SteamRawHidGuideButtonDecisionKind.StableLongPress;
        return new SteamRawHidGuideButtonDecision(kind, pending.Id, pending.DueAt, duration);
    }

    public bool ClearStalePressedSession(string address, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !_states.TryGetValue(address, out var state) ||
            !state.IsPressed ||
            !IsStalePressedSession(state, now))
        {
            return false;
        }

        state.IsPressed = false;
        state.SessionStartedAt = null;
        state.PendingRelease = null;
        state.LastStateAt = now;
        return true;
    }

    public SteamRawHidGuideButtonActivity GetActivity(string address, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !_states.TryGetValue(address, out var state))
        {
            return SteamRawHidGuideButtonActivity.None;
        }

        if (state.IsPressed && state.SessionStartedAt is { } sessionStartedAt)
        {
            return new SteamRawHidGuideButtonActivity(
                IsPressed: true,
                PressedDuration: now - sessionStartedAt,
                LastStateAge: state.LastStateAt.HasValue ? now - state.LastStateAt.Value : TimeSpan.MaxValue,
                HasPendingRelease: false,
                PendingPressDuration: TimeSpan.Zero,
                PendingLastPressedAge: TimeSpan.Zero,
                PendingReleaseAge: TimeSpan.Zero);
        }

        if (state.PendingRelease is { } pending)
        {
            return new SteamRawHidGuideButtonActivity(
                IsPressed: false,
                PressedDuration: TimeSpan.Zero,
                LastStateAge: TimeSpan.Zero,
                HasPendingRelease: true,
                PendingPressDuration: pending.ReleasedAt - pending.SessionStartedAt,
                PendingLastPressedAge: now - pending.LastPressedAt,
                PendingReleaseAge: now - pending.ReleasedAt);
        }

        return SteamRawHidGuideButtonActivity.None;
    }

    public bool ClearActivity(string address, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !_states.TryGetValue(address, out var state) ||
            (!state.IsPressed && state.PendingRelease is null))
        {
            return false;
        }

        state.IsPressed = false;
        state.SessionStartedAt = null;
        state.PendingRelease = null;
        state.LastStateAt = now;
        return true;
    }

    public void Clear()
    {
        _states.Clear();
    }

    private bool IsStalePressedSession(State state, DateTimeOffset now)
    {
        if (state.SessionStartedAt is not { } sessionStartedAt ||
            state.LastStateAt is not { } lastStateAt)
        {
            return false;
        }

        return now - sessionStartedAt > _shortPressMaximumDuration &&
               now - lastStateAt > _stalePressedResetGap;
    }

    private bool LooksLikeMissedReleaseThenFreshPress(
        DateTimeOffset sessionStartedAt,
        DateTimeOffset lastStateAt,
        DateTimeOffset now)
    {
        return now - sessionStartedAt > _shortPressMaximumDuration &&
               now - lastStateAt >= _missedReleaseRecoveryGap;
    }

    private sealed class State
    {
        public bool IsPressed { get; set; }

        public DateTimeOffset? SessionStartedAt { get; set; }

        public DateTimeOffset? LastStateAt { get; set; }

        public PendingRelease? PendingRelease { get; set; }
    }

    private sealed record PendingRelease(
        Guid Id,
        DateTimeOffset SessionStartedAt,
        DateTimeOffset LastPressedAt,
        DateTimeOffset ReleasedAt,
        DateTimeOffset DueAt,
        bool ForceShortPress);
}
