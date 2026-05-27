using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class SteamRawHidGuideButtonStateTrackerTests
{
    [Fact]
    public void GuideButtonGesture_HasLongPressSignalForRawInputSuppression()
    {
        Assert.True(Enum.IsDefined(typeof(GuideButtonGesture), GuideButtonGesture.LongPress));
    }

    [Fact]
    public void QuickTap_CompletesAsSoonAsReleaseArrives()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var completed = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(120));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.Equal(start.AddMilliseconds(120), completed.ReleaseDueAt);
        Assert.Equal(TimeSpan.FromMilliseconds(120), completed.Duration);
    }

    [Fact]
    public void SlowerTap_CompletesAsSoonAsReleaseArrives()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var completed = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(520));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.Equal(start.AddMilliseconds(520), completed.ReleaseDueAt);
        Assert.Equal(TimeSpan.FromMilliseconds(520), completed.Duration);
    }

    [Fact]
    public void CasualSlowTap_StillCountsAsShortPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var completed = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(1800));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(1800), completed.Duration);
    }

    [Fact]
    public void RepeatedShortTaps_EachCompleteOnRelease()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var first = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(120));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(1000));
        var second = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(1120));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, first.Kind);
        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, second.Kind);
    }

    [Fact]
    public void NewHoldAfterShortTap_BecomesStableLongPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var firstRelease = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(520));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(1000));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(2200));
        var finalRelease = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(3600));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, firstRelease.Kind);
        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableLongPress, finalRelease.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(2600), finalRelease.Duration);
    }

    [Fact]
    public void MissingReleaseThenLaterTap_ResetsStalePressedSession()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        tracker.RegisterState(address, pressed: true, start.AddSeconds(5));
        var completed = tracker.RegisterState(address, pressed: false, start.AddSeconds(5).AddMilliseconds(120));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(120), completed.Duration);
    }

    [Fact]
    public void StatusReportCanClearStalePressedSessionWithoutCreatingPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var cleared = tracker.ClearStalePressedSession(address, start.AddSeconds(4));
        var release = tracker.RegisterState(address, pressed: false, start.AddSeconds(4).AddMilliseconds(100));

        Assert.True(cleared);
        Assert.Equal(SteamRawHidGuideButtonDecisionKind.None, release.Kind);
    }

    [Fact]
    public void ContinuousHoldStillBecomesStableLongPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(600));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(1200));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(2200));
        var completed = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(2600));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableLongPress, completed.Kind);
    }

    [Fact]
    public void DelayedReleaseAfterSilentRawHidGap_SuppressesAsLongPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var completed = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(3200));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableLongPress, completed.Kind);
    }

    [Fact]
    public void FreshPressAfterMissedRelease_StartsNewRawHidSession()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(3000));
        var completed = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(3120));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(120), completed.Duration);
    }

    [Fact]
    public void StatusReleaseAfterLongHeldRawReports_SuppressesToast()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(500));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(1000));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(1500));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(2000));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(2500));

        var completed = tracker.RegisterStatusReleaseHint(address, start.AddMilliseconds(2506));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableLongPress, completed.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(2506), completed.Duration);
    }

    [Fact]
    public void StatusReleaseForNormalShortTap_CompletesImmediately()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var completed = tracker.RegisterStatusReleaseHint(address, start.AddMilliseconds(180));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(180), completed.Duration);
    }

    [Fact]
    public void FreshPressAfterImmediateStatusRelease_DoesNotReuseOldStartTime()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var first = tracker.RegisterStatusReleaseHint(address, start.AddMilliseconds(180));
        tracker.RegisterState(address, pressed: true, start.AddSeconds(8));
        var second = tracker.RegisterState(address, pressed: false, start.AddSeconds(8).AddMilliseconds(120));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, first.Kind);
        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, second.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(120), second.Duration);
    }

    [Fact]
    public void GetActivity_ReturnsPressedDurationWhileSteamButtonIsHeld()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var activity = tracker.GetActivity(address, start.AddMilliseconds(700));

        Assert.True(activity.IsPressed);
        Assert.Equal(TimeSpan.FromMilliseconds(700), activity.PressedDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(700), activity.LastStateAge);
        Assert.False(activity.HasPendingRelease);
    }

    [Fact]
    public void GetActivity_ReturnsNoneAfterShortTapCompletes()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var completed = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(120));
        var activity = tracker.GetActivity(address, start.AddMilliseconds(220));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.False(activity.IsPressed);
        Assert.False(activity.HasPendingRelease);
    }

    [Fact]
    public void ClearActivity_DropsOldPressedStateSoFallbackCanUseFreshTap()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var cleared = tracker.ClearActivity(address, start.AddSeconds(4));
        var activity = tracker.GetActivity(address, start.AddSeconds(4).AddMilliseconds(1));

        Assert.True(cleared);
        Assert.False(activity.IsPressed);
        Assert.False(activity.HasPendingRelease);
    }

    private static SteamRawHidGuideButtonStateTracker CreateTracker()
    {
        return new SteamRawHidGuideButtonStateTracker(
            TimeSpan.FromMilliseconds(35),
            TimeSpan.FromMilliseconds(2200),
            TimeSpan.FromMilliseconds(320),
            TimeSpan.FromMilliseconds(180),
            TimeSpan.FromMilliseconds(450),
            TimeSpan.FromMilliseconds(1800),
            TimeSpan.FromMilliseconds(700));
    }
}
