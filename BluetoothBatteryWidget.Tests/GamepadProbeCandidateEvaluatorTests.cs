using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GamepadProbeCandidateEvaluatorTests
{
    [Fact]
    public void SelectBest_UniqueHighScoreCandidate_ReturnsWinner()
    {
        var reportA = new byte[] { 0x31, 0x00, 0x00, 0x00, 0x00, 73, 0x00, 0x00 };
        var reportB = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 73, 0x00, 0x00 };
        var reports = new Dictionary<byte, byte[]>
        {
            [0x31] = reportA,
            [0x01] = reportB
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);

        Assert.False(selection.IsTie);
        Assert.NotNull(selection.Winner);
        Assert.Equal(5, selection.Winner!.Offset);
        Assert.Equal(73, selection.Winner.BatteryPercent);
        Assert.Equal(GamepadProbeCandidateEvaluator.DecoderPercent100, selection.Winner.Decoder);
        Assert.True(selection.Winner.Score >= 70);
    }

    [Fact]
    public void SelectBest_MultipleTopCandidates_UsesDeterministicTieBreak()
    {
        var reportA = new byte[] { 0x31, 0x00, 60, 0x00, 70, 0x00 };
        var reportB = new byte[] { 0x01, 0x00, 60, 0x00, 70, 0x00 };
        var reports = new Dictionary<byte, byte[]>
        {
            [0x31] = reportA,
            [0x01] = reportB
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);

        Assert.False(selection.IsTie);
        Assert.NotNull(selection.Winner);
        Assert.True(selection.Winner!.Offset >= 2);
        Assert.True(selection.Winner.Score >= 70);
    }

    [Fact]
    public void SelectBest_PenalizesOffsetZeroCandidate()
    {
        var reportA = new byte[] { 80, 0x00, 0x00, 0x00, 0x00, 82 };
        var reportB = new byte[] { 80, 0x00, 0x00, 0x00, 0x00, 82 };
        var reports = new Dictionary<byte, byte[]>
        {
            [0x31] = reportA,
            [0x01] = reportB
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);

        Assert.False(selection.IsTie);
        Assert.NotNull(selection.Winner);
        Assert.Equal(5, selection.Winner!.Offset);
    }

    [Fact]
    public void SelectBest_PrefersExactCandidateOverCoarseXboxFlags()
    {
        var reports = new Dictionary<byte, byte[]>
        {
            [0x04] = new byte[] { 0x04, 0x03, 80, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);

        Assert.NotNull(selection.Winner);
        Assert.Equal(GamepadProbeCandidateEvaluator.DecoderPercent100, selection.Winner!.Decoder);
        Assert.Equal(80, selection.Winner.BatteryPercent);
        Assert.True(selection.Winner.Score > 0);
    }

    [Fact]
    public void EnumerateCandidates_PreservesReportIdForSameOffsetPercent()
    {
        var reports = new Dictionary<byte, byte[]>
        {
            [0x04] = new byte[] { 0x04, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 9 },
            [0x11] = new byte[] { 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 9 }
        };

        var reportIds = GamepadProbeCandidateEvaluator.EnumerateCandidates(reports)
            .Where(candidate =>
                candidate.Decoder == GamepadProbeCandidateEvaluator.DecoderPercent100 &&
                candidate.Offset == 13 &&
                candidate.BatteryPercent == 9)
            .Select(candidate => candidate.ReportId)
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(new byte[] { 0x04, 0x11 }, reportIds);
    }

    [Fact]
    public void SelectBest_PrefersDedicatedReportOverGenericXboxStatusByte()
    {
        var reports = new Dictionary<byte, byte[]>
        {
            [0x04] = new byte[] { 0x04, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 9 },
            [0x11] = new byte[] { 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 9 }
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);

        Assert.NotNull(selection.Winner);
        Assert.Equal(0x11, selection.Winner!.ReportId);
        Assert.Equal(13, selection.Winner.Offset);
        Assert.Equal(9, selection.Winner.BatteryPercent);
        Assert.Equal(GamepadProbeCandidateEvaluator.DecoderPercent100, selection.Winner.Decoder);
    }

    [Fact]
    public void SelectBest_X05ProTraceShape_DemotesGenericStatusReport()
    {
        var reports = new Dictionary<byte, byte[]>
        {
            [0x04] = new byte[] { 0x04, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 9 },
            [0x01] = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 9 },
            [0x11] = new byte[] { 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 9 }
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);
        var genericStatus = GamepadProbeCandidateEvaluator.EnumerateCandidates(reports)
            .Single(candidate =>
                candidate.ReportId == 0x04 &&
                candidate.Offset == 13 &&
                candidate.Decoder == GamepadProbeCandidateEvaluator.DecoderPercent100);

        Assert.NotNull(selection.Winner);
        Assert.NotEqual(0x04, selection.Winner!.ReportId);
        Assert.Equal(13, selection.Winner.Offset);
        Assert.Equal(9, selection.Winner.BatteryPercent);
        Assert.Equal(GamepadProbeCandidateEvaluator.DecoderPercent100, selection.Winner.Decoder);
        Assert.True(selection.Winner.Score > genericStatus.Score);
    }

    [Fact]
    public void SelectBest_KeepsXboxFlagsAsCoarseCandidate_WhenOnlyBatterySignal()
    {
        var reports = new Dictionary<byte, byte[]>
        {
            [0x04] = new byte[] { 0x04, 0x03, 0x00, 0x00 }
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);

        Assert.NotNull(selection.Winner);
        Assert.Equal(GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags, selection.Winner!.Decoder);
        Assert.Equal(100, selection.Winner.BatteryPercent);
        Assert.True(selection.Winner.Score > 0);
    }

    [Fact]
    public void ToProfile_PreservesRepeatedExactValidationMarker()
    {
        var profile = GamepadProbeCandidateEvaluator.ToProfile(
            "045e",
            "02e0",
            new GamepadBatteryCandidate(
                ReportId: 0x11,
                ReportLength: 16,
                Offset: 13,
                Decoder: GamepadProbeCandidateEvaluator.DecoderPercent100,
                BatteryPercent: 9,
                Score: 89),
            identityKey: "ID_TEST",
            confidence: BatteryConfidence.Estimated,
            validationKind: GamepadProfileStore.RepeatedExactHidValidationKind,
            validationCount: GamepadProfileStore.RepeatedExactHidValidationMinCount);

        Assert.Equal(GamepadProfileStore.RepeatedExactHidValidationKind, profile.ValidationKind);
        Assert.Equal(GamepadProfileStore.RepeatedExactHidValidationMinCount, profile.ValidationCount);
    }
}
