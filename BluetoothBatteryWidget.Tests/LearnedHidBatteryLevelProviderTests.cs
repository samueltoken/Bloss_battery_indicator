using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class LearnedHidBatteryLevelProviderTests
{
    [Fact]
    public void NormalizeIdentityForEndpointDrift_RemovesEndpointTokenOnly()
    {
        var normalized = LearnedHidBatteryLevelProvider.NormalizeIdentityForEndpointDrift(
            "ID=VID_054C|PID_09CC|TR=VID_054C|PID_09CC|FP=FP_AABBCCDDE012|EP=762121DE");

        Assert.Equal(
            "ID=VID_054C|PID_09CC|TR=VID_054C|PID_09CC|FP=FP_AABBCCDDE012",
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

    [Fact]
    public void ShouldHoldProfileForRepeatedProof_BlocksXboxNamedExactProfileWithoutRepeatedProof()
    {
        var profile = new GamepadBatteryProfile(
            "0000",
            "0000",
            0x11,
            16,
            13,
            GamepadProbeCandidateEvaluator.DecoderPercent100,
            89,
            IdentityKey: "ID=VID_0000|PID_0000|TR=VID_0000|PID_0000|FP=FP_TEST");

        var shouldHold = LearnedHidBatteryLevelProvider.ShouldHoldProfileForRepeatedProof(
            profile,
            "Xbox Wireless Controller",
            "0000",
            "0000");

        Assert.True(shouldHold);
    }

    [Fact]
    public void ShouldHoldProfileForRepeatedProof_AllowsXboxNamedExactProfileWithRepeatedProof()
    {
        var profile = new GamepadBatteryProfile(
            "0000",
            "0000",
            0x11,
            16,
            13,
            GamepadProbeCandidateEvaluator.DecoderPercent100,
            89,
            IdentityKey: "ID=VID_0000|PID_0000|TR=VID_0000|PID_0000|FP=FP_TEST",
            ValidationKind: GamepadProfileStore.RepeatedExactHidValidationKind,
            ValidationCount: GamepadProfileStore.RepeatedExactHidValidationMinCount);

        var shouldHold = LearnedHidBatteryLevelProvider.ShouldHoldProfileForRepeatedProof(
            profile,
            "Xbox Wireless Controller",
            "0000",
            "0000");

        Assert.False(shouldHold);
    }

    [Fact]
    public void ShouldHoldProfileForRepeatedProof_AllowsNonXboxExactProfileWithoutRepeatedProof()
    {
        var profile = new GamepadBatteryProfile(
            "054C",
            "0CE6",
            0x31,
            78,
            10,
            GamepadProbeCandidateEvaluator.DecoderPercent100,
            89);

        var shouldHold = LearnedHidBatteryLevelProvider.ShouldHoldProfileForRepeatedProof(
            profile,
            "DualSense Wireless Controller",
            "054C",
            "054C");

        Assert.False(shouldHold);
    }
}
