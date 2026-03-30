using BluetoothBatteryWidget.App.ViewModels;

namespace BluetoothBatteryWidget.Tests;

public sealed class MainViewModelProbeStateTests
{
    [Fact]
    public void ShouldExpireProbeState_ReturnsFalse_WhenRunning()
    {
        var now = new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc);
        var expired = MainViewModel.ShouldExpireProbeState(
            isRunning: true,
            updatedAtUtc: now.AddMinutes(-30),
            nowUtc: now,
            ttl: TimeSpan.FromMinutes(5));

        Assert.False(expired);
    }

    [Fact]
    public void ShouldExpireProbeState_ReturnsTrue_WhenCompletedAndOlderThanTtl()
    {
        var now = new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc);
        var expired = MainViewModel.ShouldExpireProbeState(
            isRunning: false,
            updatedAtUtc: now.AddMinutes(-6),
            nowUtc: now,
            ttl: TimeSpan.FromMinutes(5));

        Assert.True(expired);
    }

    [Fact]
    public void ComputeAutoProbeBackoff_FirstFailure_UsesBaseCooldown()
    {
        var backoff = MainViewModel.ComputeAutoProbeBackoff(
            baseCooldown: TimeSpan.FromSeconds(90),
            failureCount: 1,
            maxExponent: 4);

        Assert.Equal(TimeSpan.FromSeconds(90), backoff);
    }

    [Fact]
    public void ComputeAutoProbeBackoff_MultipleFailures_UsesExponentialBackoffWithCap()
    {
        var backoff = MainViewModel.ComputeAutoProbeBackoff(
            baseCooldown: TimeSpan.FromSeconds(90),
            failureCount: 5,
            maxExponent: 4);

        Assert.Equal(TimeSpan.FromSeconds(1440), backoff);
    }

    [Fact]
    public void ApplyNoSignalFailureBackoff_NoSignal_BoostsAndAppliesMinimum()
    {
        var boosted = MainViewModel.ApplyNoSignalFailureBackoff(
            backoff: TimeSpan.FromSeconds(90),
            isNoSignal: true);

        Assert.Equal(TimeSpan.FromMinutes(6), boosted);
    }

    [Fact]
    public void ApplyNoSignalFailureBackoff_NonNoSignal_ReturnsOriginalBackoff()
    {
        var backoff = TimeSpan.FromSeconds(300);
        var boosted = MainViewModel.ApplyNoSignalFailureBackoff(
            backoff: backoff,
            isNoSignal: false);

        Assert.Equal(backoff, boosted);
    }

    [Fact]
    public void ApplyWeakSignalFailureBackoff_WeakSignal_BoostsBackoff()
    {
        var boosted = MainViewModel.ApplyWeakSignalFailureBackoff(
            backoff: TimeSpan.FromSeconds(120),
            isWeakSignal: true);

        Assert.True(boosted > TimeSpan.FromSeconds(120));
        Assert.True(boosted >= TimeSpan.FromMinutes(4));
    }

    [Fact]
    public void ApplyBackoffJitter_IsDeterministicForSameKeyAndFailureCount()
    {
        var first = MainViewModel.ApplyBackoffJitter(
            backoff: TimeSpan.FromSeconds(240),
            key: "AABBCCDDEEFF",
            failureCount: 3);
        var second = MainViewModel.ApplyBackoffJitter(
            backoff: TimeSpan.FromSeconds(240),
            key: "AABBCCDDEEFF",
            failureCount: 3);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ShouldKeepMissingDevice_ZeroGrace_RemovesImmediately()
    {
        var now = new DateTime(2026, 3, 28, 1, 0, 0, DateTimeKind.Utc);
        var keep = MainViewModel.ShouldKeepMissingDevice(
            nowUtc: now,
            missingSinceUtc: now,
            disconnectGrace: TimeSpan.Zero);

        Assert.False(keep);
    }

    [Fact]
    public void ShouldKeepMissingDevice_PositiveGrace_RespectsWindow()
    {
        var now = new DateTime(2026, 3, 28, 1, 0, 0, DateTimeKind.Utc);
        var keep = MainViewModel.ShouldKeepMissingDevice(
            nowUtc: now,
            missingSinceUtc: now.AddSeconds(-5),
            disconnectGrace: TimeSpan.FromSeconds(10));

        Assert.True(keep);
    }

    [Fact]
    public void ShouldKeepMissingDevice_BeforeMinimumMissingCount_KeepsDevice()
    {
        var now = new DateTime(2026, 3, 28, 1, 0, 0, DateTimeKind.Utc);
        var keep = MainViewModel.ShouldKeepMissingDevice(
            nowUtc: now,
            missingSinceUtc: now,
            disconnectGrace: TimeSpan.Zero,
            missingCount: 1,
            minimumMissingCount: 2);

        Assert.True(keep);
    }

    [Fact]
    public void ShouldKeepMissingDevice_AfterMinimumMissingCount_UsesGraceWindow()
    {
        var now = new DateTime(2026, 3, 28, 1, 0, 30, DateTimeKind.Utc);
        var keep = MainViewModel.ShouldKeepMissingDevice(
            nowUtc: now,
            missingSinceUtc: now.AddSeconds(-20),
            disconnectGrace: TimeSpan.FromSeconds(30),
            missingCount: 2,
            minimumMissingCount: 2);
        var remove = MainViewModel.ShouldKeepMissingDevice(
            nowUtc: now,
            missingSinceUtc: now.AddSeconds(-40),
            disconnectGrace: TimeSpan.FromSeconds(30),
            missingCount: 2,
            minimumMissingCount: 2);

        Assert.True(keep);
        Assert.False(remove);
    }
}
