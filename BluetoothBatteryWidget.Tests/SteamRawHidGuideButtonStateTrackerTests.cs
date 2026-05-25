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
    public void QuickTap_UsesFastStableReleaseWindow()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var pending = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(120));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.PendingRelease, pending.Kind);
        Assert.Equal(start.AddMilliseconds(300), pending.ReleaseDueAt);
        Assert.Equal(TimeSpan.FromMilliseconds(120), pending.Duration);

        var early = tracker.CompletePendingRelease(address, pending.PendingId, pending.ReleaseDueAt.AddMilliseconds(-1));
        Assert.Equal(SteamRawHidGuideButtonDecisionKind.None, early.Kind);

        var completed = tracker.CompletePendingRelease(address, pending.PendingId, pending.ReleaseDueAt);
        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
    }

    [Fact]
    public void SlowerTap_UsesCautiousStableReleaseWindow()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var pending = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(520));

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.PendingRelease, pending.Kind);
        Assert.Equal(start.AddMilliseconds(970), pending.ReleaseDueAt);
        Assert.Equal(TimeSpan.FromMilliseconds(520), pending.Duration);
    }

    [Fact]
    public void CasualSlowTap_StillCountsAsShortPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var pending = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(1800));
        var completed = tracker.CompletePendingRelease(address, pending.PendingId, pending.ReleaseDueAt);

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableShortPress, completed.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(1800), completed.Duration);
    }

    [Fact]
    public void RepressDuringStableReleaseWindow_CancelsPendingShortPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var pending = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(520));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(1000));

        var stale = tracker.CompletePendingRelease(address, pending.PendingId, pending.ReleaseDueAt);

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.None, stale.Kind);
    }

    [Fact]
    public void FlickeringPowerOffHold_BecomesStableLongPress()
    {
        var tracker = CreateTracker();
        var address = "AABBCCDDEEFF";
        var start = DateTimeOffset.Parse("2026-05-26T00:00:00+09:00");

        tracker.RegisterState(address, pressed: true, start);
        var firstRelease = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(520));
        tracker.RegisterState(address, pressed: true, start.AddMilliseconds(1000));
        var firstStale = tracker.CompletePendingRelease(address, firstRelease.PendingId, firstRelease.ReleaseDueAt);
        Assert.Equal(SteamRawHidGuideButtonDecisionKind.None, firstStale.Kind);

        var finalRelease = tracker.RegisterState(address, pressed: false, start.AddMilliseconds(2600));
        var completed = tracker.CompletePendingRelease(address, finalRelease.PendingId, finalRelease.ReleaseDueAt);

        Assert.Equal(SteamRawHidGuideButtonDecisionKind.StableLongPress, completed.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(2600), completed.Duration);
    }

    private static SteamRawHidGuideButtonStateTracker CreateTracker()
    {
        return new SteamRawHidGuideButtonStateTracker(
            TimeSpan.FromMilliseconds(35),
            TimeSpan.FromMilliseconds(2200),
            TimeSpan.FromMilliseconds(320),
            TimeSpan.FromMilliseconds(180),
            TimeSpan.FromMilliseconds(450));
    }
}
