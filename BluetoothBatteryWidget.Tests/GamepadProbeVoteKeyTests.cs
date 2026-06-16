using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GamepadProbeVoteKeyTests
{
    [Fact]
    public void BuildVoteCandidateKey_NormalizesCoreIdentityFields()
    {
        var key = GamepadProbeService.BuildVoteCandidateKey(
            identityKey: "id=vid_045e|pid_02e0|fp_test",
            decoder: "xbox_bt_flags",
            reportId: 0x04,
            offset: 1);

        Assert.Contains("IDK_ID=VID_045E|PID_02E0|FP_TEST", key, StringComparison.Ordinal);
        Assert.Contains("RID_04", key, StringComparison.Ordinal);
        Assert.Contains("OFF_1", key, StringComparison.Ordinal);
        Assert.Contains("DEC_XBOX_BT_FLAGS", key, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildVoteCandidateKey_DoesNotDependOnReportLength()
    {
        var keyA = GamepadProbeService.BuildVoteCandidateKey(
            identityKey: "ID_TEST",
            decoder: "xbox_bt_flags",
            reportId: 0x04,
            offset: 1);
        var keyB = GamepadProbeService.BuildVoteCandidateKey(
            identityKey: "ID_TEST",
            decoder: "xbox_bt_flags",
            reportId: 0x04,
            offset: 1);

        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void SanitizeProbeTraceText_MasksDeviceIdentifiers()
    {
        var compactAddress = "AABBCC" + "DDEEFF";
        var raw = "path=" + "BTHENUM" + "\\" + "DEV_" + compactAddress +
                  " identity=ID=VID_045E|PID_02E0|TR=VID_045E|PID_02E0|FP=FP_" + compactAddress +
                  "|EP=" + "123456" +
                  " addr=" + compactAddress;

        var sanitized = GamepadProbeService.SanitizeProbeTraceText(raw);

        Assert.Contains("DEVICE_PATH_MASKED", sanitized, StringComparison.Ordinal);
        Assert.Contains("VID_MASKED", sanitized, StringComparison.Ordinal);
        Assert.Contains("PID_MASKED", sanitized, StringComparison.Ordinal);
        Assert.Contains("FP=FP_MASKED", sanitized, StringComparison.Ordinal);
        Assert.Contains("EP=EP_MASKED", sanitized, StringComparison.Ordinal);
        Assert.Contains("ADDR_MASKED", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(compactAddress, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("045E", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("02E0", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCandidateDecisionHint_ClassifiesX05ProBatteryCandidates()
    {
        var coarse = GamepadProbeService.BuildCandidateDecisionHint(
            new GamepadBatteryCandidate(0x04, 16, 1, GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags, 100, 80),
            "brand.easysmx",
            reportSeen: 2);
        var statusReport = GamepadProbeService.BuildCandidateDecisionHint(
            new GamepadBatteryCandidate(0x04, 16, 13, GamepadProbeCandidateEvaluator.DecoderPercent100, 9, 89),
            "brand.easysmx",
            reportSeen: 2);
        var dedicatedReport = GamepadProbeService.BuildCandidateDecisionHint(
            new GamepadBatteryCandidate(0x11, 16, 13, GamepadProbeCandidateEvaluator.DecoderPercent100, 9, 89),
            "brand.easysmx",
            reportSeen: 1);

        Assert.StartsWith("reject-exact:xbox_bt_flags@0x04", coarse, StringComparison.Ordinal);
        Assert.Contains("seen=2", coarse, StringComparison.Ordinal);
        Assert.Contains("need=none", coarse, StringComparison.Ordinal);
        Assert.Contains("reason=xbox-bucket-open-source", coarse, StringComparison.Ordinal);
        Assert.StartsWith("demote:percent100@0x04", statusReport, StringComparison.Ordinal);
        Assert.Contains("seen=2", statusReport, StringComparison.Ordinal);
        Assert.Contains("need=repeat3+move2", statusReport, StringComparison.Ordinal);
        Assert.Contains("reason=status-report-needs-stronger-proof", statusReport, StringComparison.Ordinal);
        Assert.StartsWith("watch:percent100@0x11", dedicatedReport, StringComparison.Ordinal);
        Assert.Contains("seen=1", dedicatedReport, StringComparison.Ordinal);
        Assert.Contains("need=repeat3+move2", dedicatedReport, StringComparison.Ordinal);
        Assert.Contains("reason=needs-same-report-repeat-and-movement", dedicatedReport, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSelectedCandidateProofText_ShowsRepeatAndMovementGate()
    {
        var candidate = new GamepadBatteryCandidate(
            0x11,
            16,
            13,
            GamepadProbeCandidateEvaluator.DecoderPercent100,
            9,
            89);
        var flatPending = new PendingGamepadCandidate(
            "SAFE",
            "RID_11|OFF_13|DEC_PERCENT100",
            89,
            3,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            MinPercent: 9,
            MaxPercent: 9);
        var readyPending = flatPending with { MaxPercent = 12 };
        var lowRepeatMovedPending = readyPending with { VoteCount = 2 };
        var pendingDecision = new GamepadProbeService.LearningDecision(
            Accepted: true,
            PersistProfile: false,
            IsPending: true,
            Confidence: BatteryConfidence.Estimated);
        var readyDecision = pendingDecision with
        {
            PersistProfile = true,
            IsPending = false,
            Confidence = BatteryConfidence.Confirmed
        };

        var flat = GamepadProbeService.BuildSelectedCandidateProofText(
            candidate,
            votes: 3,
            requiredVotes: 3,
            requiresRepeatedExactProof: true,
            hasRequiredExactMovement: false,
            flatPending,
            pendingDecision);
        var lowRepeat = GamepadProbeService.BuildSelectedCandidateProofText(
            candidate,
            votes: 2,
            requiredVotes: 3,
            requiresRepeatedExactProof: true,
            hasRequiredExactMovement: true,
            lowRepeatMovedPending,
            pendingDecision);
        var lowRepeatFlat = GamepadProbeService.BuildSelectedCandidateProofText(
            candidate,
            votes: 2,
            requiredVotes: 3,
            requiresRepeatedExactProof: true,
            hasRequiredExactMovement: false,
            flatPending,
            pendingDecision);
        var ready = GamepadProbeService.BuildSelectedCandidateProofText(
            candidate,
            votes: 3,
            requiredVotes: 3,
            requiresRepeatedExactProof: true,
            hasRequiredExactMovement: true,
            readyPending,
            readyDecision);

        Assert.Contains("votes=3/3", flat, StringComparison.Ordinal);
        Assert.Contains("repeat=yes", flat, StringComparison.Ordinal);
        Assert.Contains("movement=no:9-9", flat, StringComparison.Ordinal);
        Assert.Contains("gate=needs-movement", flat, StringComparison.Ordinal);
        Assert.Contains("votes=2/3", lowRepeat, StringComparison.Ordinal);
        Assert.Contains("movement=yes:9-12", lowRepeat, StringComparison.Ordinal);
        Assert.Contains("gate=needs-repeat", lowRepeat, StringComparison.Ordinal);
        Assert.Contains("gate=needs-repeat-and-movement", lowRepeatFlat, StringComparison.Ordinal);
        Assert.Contains("movement=yes:9-12", ready, StringComparison.Ordinal);
        Assert.Contains("gate=ready-after-repeat-and-movement", ready, StringComparison.Ordinal);
    }
}
