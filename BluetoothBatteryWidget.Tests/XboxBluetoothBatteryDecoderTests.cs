using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class XboxBluetoothBatteryDecoderTests
{
    [Theory]
    [InlineData(0x00, 10)]
    [InlineData(0x01, 40)]
    [InlineData(0x02, 70)]
    [InlineData(0x03, 100)]
    public void TryDecode_MapsFlagsToPercent(byte flags, int expectedPercent)
    {
        var report = new byte[] { 0x04, flags };

        var ok = XboxBluetoothBatteryDecoder.TryDecode(0x04, report, out var percent, out var onUsb);

        Assert.True(ok);
        Assert.Equal(expectedPercent, percent);
        Assert.True(onUsb);
    }

    [Fact]
    public void TryDecode_ReturnsFalse_WhenReportIdDiffers()
    {
        var ok = XboxBluetoothBatteryDecoder.TryDecode(0x01, new byte[] { 0x01, 0x03 }, out _, out _);
        Assert.False(ok);
    }
}
