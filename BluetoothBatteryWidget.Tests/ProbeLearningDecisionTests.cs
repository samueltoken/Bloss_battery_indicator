using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class ProbeLearningDecisionTests
{
    [Fact]
    public void ResolveLearningDecision_ConfirmedThreshold_PersistsImmediately()
    {
        var decision = GamepadProbeService.ResolveLearningDecision(bestScore: 72, votes: 1);

        Assert.True(decision.Accepted);
        Assert.True(decision.PersistProfile);
        Assert.False(decision.IsPending);
        Assert.Equal(BatteryConfidence.Confirmed, decision.Confidence);
    }

    [Fact]
    public void ResolveLearningDecision_MidScore_FirstVote_Pending()
    {
        var decision = GamepadProbeService.ResolveLearningDecision(bestScore: 63, votes: 1);

        Assert.True(decision.Accepted);
        Assert.False(decision.PersistProfile);
        Assert.True(decision.IsPending);
        Assert.Equal(BatteryConfidence.Estimated, decision.Confidence);
    }

    [Fact]
    public void ResolveLearningDecision_MidScore_SecondVote_PersistsEstimated()
    {
        var decision = GamepadProbeService.ResolveLearningDecision(bestScore: 63, votes: 2);

        Assert.True(decision.Accepted);
        Assert.True(decision.PersistProfile);
        Assert.False(decision.IsPending);
        Assert.Equal(BatteryConfidence.Estimated, decision.Confidence);
    }

    [Fact]
    public void ResolveLearningDecision_LowScore_Rejected()
    {
        var decision = GamepadProbeService.ResolveLearningDecision(bestScore: 54, votes: 2);

        Assert.False(decision.Accepted);
        Assert.False(decision.PersistProfile);
        Assert.False(decision.IsPending);
    }

    [Fact]
    public void ResolveLearningDecision_WithHigherRequiredVotes_StaysPendingUntilThreshold()
    {
        var pending = GamepadProbeService.ResolveLearningDecision(bestScore: 63, votes: 2, requiredVotes: 3);
        var persisted = GamepadProbeService.ResolveLearningDecision(bestScore: 63, votes: 3, requiredVotes: 3);

        Assert.True(pending.Accepted);
        Assert.False(pending.PersistProfile);
        Assert.True(pending.IsPending);

        Assert.True(persisted.Accepted);
        Assert.True(persisted.PersistProfile);
        Assert.False(persisted.IsPending);
    }

    [Fact]
    public void ResolveLearningDecision_ConfirmedCandidateCanRequireRepeatedProof()
    {
        var pending = GamepadProbeService.ResolveLearningDecision(
            bestScore: 82,
            votes: 2,
            requiredVotes: 3,
            requireVotesForConfirmed: true);
        var persisted = GamepadProbeService.ResolveLearningDecision(
            bestScore: 82,
            votes: 3,
            requiredVotes: 3,
            requireVotesForConfirmed: true);

        Assert.True(pending.Accepted);
        Assert.False(pending.PersistProfile);
        Assert.True(pending.IsPending);

        Assert.True(persisted.Accepted);
        Assert.True(persisted.PersistProfile);
        Assert.False(persisted.IsPending);
        Assert.Equal(BatteryConfidence.Confirmed, persisted.Confidence);
    }

    [Fact]
    public void HasRepeatedExactCandidateMovement_RequiresPercentRangeChange()
    {
        var flat = new PendingGamepadCandidate(
            "VID_045E|PID_0B13",
            "IDK_ID=VID_045E|RID_04|OFF_13|DEC_PERCENT100",
            89,
            3,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            MinPercent: 9,
            MaxPercent: 9);
        var moved = flat with { MaxPercent = 12 };
        var tooFewVotes = moved with { VoteCount = 2 };

        Assert.False(GamepadProbeService.HasRepeatedExactCandidateMovement(flat));
        Assert.False(GamepadProbeService.HasRepeatedExactCandidateMovement(tooFewVotes));
        Assert.True(GamepadProbeService.HasRepeatedExactCandidateMovement(moved));
    }

    [Fact]
    public void ShouldAllowImmediateEstimatedAcceptance_AllowsStrongSinglePassCandidate()
    {
        var allowed = GamepadProbeService.ShouldAllowImmediateEstimatedAcceptance(
            score: 62,
            reportRepeatCount: 11,
            decoderConfidence: 0.58d,
            hasStrongCompetingCandidate: false,
            isSuspiciousXboxLowCandidate: false,
            suppressionReason: "delayed_acceptance_low_confidence");

        Assert.True(allowed);
    }

    [Fact]
    public void ShouldAllowImmediateEstimatedAcceptance_BlocksWhenRepeatEvidenceIsInsufficient()
    {
        var allowed = GamepadProbeService.ShouldAllowImmediateEstimatedAcceptance(
            score: 70,
            reportRepeatCount: 1,
            decoderConfidence: 0.80d,
            hasStrongCompetingCandidate: false,
            isSuspiciousXboxLowCandidate: false,
            suppressionReason: null);

        Assert.False(allowed);
    }
}
