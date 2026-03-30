using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class LearnedHidBatteryLevelProviderTests
{
    [Fact]
    public void NormalizeIdentityForEndpointDrift_RemovesEndpointTokenOnly()
    {
        var normalized = LearnedHidBatteryLevelProvider.NormalizeIdentityForEndpointDrift(
            "ID=VID_054C|PID_09CC|TR=VID_054C|PID_09CC|FP=FP_1F2D073D14AB|EP=762121DE");

        Assert.Equal(
            "ID=VID_054C|PID_09CC|TR=VID_054C|PID_09CC|FP=FP_1F2D073D14AB",
            normalized);
    }

    [Fact]
    public void ShouldEmitNaAfterRevalidationFailure_RequiresTwoStrikes()
    {
        var oneStrike = LearnedHidBatteryLevelProvider.ShouldEmitNaAfterRevalidationFailure(
            RevalidationFailureKind.NoSignal,
            new GamepadProfileHealthState(NoSignalStrike: 1));
        var twoStrikes = LearnedHidBatteryLevelProvider.ShouldEmitNaAfterRevalidationFailure(
            RevalidationFailureKind.NoSignal,
            new GamepadProfileHealthState(NoSignalStrike: 2));

        Assert.False(oneStrike);
        Assert.True(twoStrikes);
    }
}
