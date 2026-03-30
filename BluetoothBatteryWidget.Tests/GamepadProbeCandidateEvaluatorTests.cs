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
    public void SelectBest_PrefersXboxDedicatedDecoder_WhenAvailable()
    {
        var reports = new Dictionary<byte, byte[]>
        {
            [0x04] = new byte[] { 0x04, 0x03, 80, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);

        Assert.NotNull(selection.Winner);
        Assert.Equal(GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags, selection.Winner!.Decoder);
        Assert.True(selection.Winner.Score > 0);
    }
}
