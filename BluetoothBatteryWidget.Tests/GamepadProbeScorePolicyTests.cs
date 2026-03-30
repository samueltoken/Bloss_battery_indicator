using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GamepadProbeScorePolicyTests
{
    [Fact]
    public void AggressiveMinimumScore_IsHigherThanStrict()
    {
        Assert.True(GamepadProbeScorePolicy.AggressiveMinimumScore > GamepadProbeScorePolicy.StrictMinimumScore);
    }

    [Fact]
    public void IsAccepted_UsesDifferentThresholdByMode()
    {
        Assert.True(GamepadProbeScorePolicy.IsAccepted(70, aggressiveFallback: false));
        Assert.False(GamepadProbeScorePolicy.IsAccepted(70, aggressiveFallback: true));
        Assert.True(GamepadProbeScorePolicy.IsAccepted(85, aggressiveFallback: true));
    }
}
