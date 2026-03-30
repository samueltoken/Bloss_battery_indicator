using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GamepadProfileDecoderTests
{
    [Fact]
    public void TryDecode_XboxBluetoothFlags_DecodesExpectedPercent()
    {
        var profile = new GamepadBatteryProfile(
            VendorId: "045E",
            ProductId: "02E0",
            ReportId: 0x04,
            ReportLength: 16,
            Offset: 1,
            Decoder: GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags,
            Score: 88);

        var decoded = GamepadProfileDecoder.TryDecode(profile, new byte[] { 0x04, 0x03 }, out var percent);

        Assert.True(decoded);
        Assert.Equal(100, percent);
    }

    [Fact]
    public void TryDecode_XboxBluetoothFlags_WrongReportId_ReturnsFalse()
    {
        var profile = new GamepadBatteryProfile(
            VendorId: "045E",
            ProductId: "02E0",
            ReportId: 0x01,
            ReportLength: 16,
            Offset: 1,
            Decoder: GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags,
            Score: 88);

        var decoded = GamepadProfileDecoder.TryDecode(profile, new byte[] { 0x01, 0x03 }, out _);

        Assert.False(decoded);
    }
}
