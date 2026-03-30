using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class ProbeCollectionTuningTests
{
    [Fact]
    public void StreamTimeoutSequence_IsStrongTuningProfile()
    {
        Assert.Equal([180, 260, 420, 700], GamepadProbeService.StreamTimeoutsForTesting);
    }

    [Fact]
    public void DefaultProbeFallbackSizes_Contains512()
    {
        Assert.Contains(512, HidGamepadAccess.DefaultProbeFallbackSizes);
    }

    [Fact]
    public void ShouldRunNoSignalRecovery_OnlyWhenReadSuccessIsZeroAndScoreIsZero()
    {
        Assert.True(GamepadProbeService.ShouldRunNoSignalRecovery(readSuccessCount: 0, bestObservedScore: 0));
        Assert.False(GamepadProbeService.ShouldRunNoSignalRecovery(readSuccessCount: 1, bestObservedScore: 0));
        Assert.False(GamepadProbeService.ShouldRunNoSignalRecovery(readSuccessCount: 0, bestObservedScore: 15));
    }
}
