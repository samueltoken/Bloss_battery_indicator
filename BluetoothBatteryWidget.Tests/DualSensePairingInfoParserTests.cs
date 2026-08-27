using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class DualSensePairingInfoParserTests
{
    [Fact]
    public void TryParseControllerAddress_PairingReport_ReturnsDisplayOrderAddress()
    {
        var report = new byte[DualSensePairingInfoParser.ReportSize];
        report[0] = 0x09;
        report[1] = 0x2B;
        report[2] = 0x79;
        report[3] = 0xBA;
        report[4] = 0x31;
        report[5] = 0x10;
        report[6] = 0x58;

        var parsed = DualSensePairingInfoParser.TryParseControllerAddress(report, out var address);

        Assert.True(parsed);
        Assert.Equal("581031BA792B", address);
    }

    [Fact]
    public void TryParseControllerAddress_ShortReport_ReturnsFalse()
    {
        var report = new byte[DualSensePairingInfoParser.ReportSize - 1];
        report[0] = 0x09;

        Assert.False(DualSensePairingInfoParser.TryParseControllerAddress(report, out _));
    }

    [Fact]
    public void TryParseControllerAddress_WrongReportId_ReturnsFalse()
    {
        var report = CreateReport("581031BA792B");
        report[0] = 0x08;

        Assert.False(DualSensePairingInfoParser.TryParseControllerAddress(report, out _));
    }

    [Theory]
    [InlineData((byte)0x00)]
    [InlineData((byte)0xFF)]
    public void TryParseControllerAddress_UniformInvalidAddress_ReturnsFalse(byte value)
    {
        var report = new byte[DualSensePairingInfoParser.ReportSize];
        report[0] = 0x09;
        Array.Fill(report, value, 1, 6);

        Assert.False(DualSensePairingInfoParser.TryParseControllerAddress(report, out _));
    }

    internal static byte[] CreateReport(string displayAddress)
    {
        var displayBytes = Convert.FromHexString(displayAddress);
        var report = new byte[DualSensePairingInfoParser.ReportSize];
        report[0] = DualSensePairingInfoParser.ReportId;
        for (var index = 0; index < displayBytes.Length; index++)
        {
            report[1 + index] = displayBytes[displayBytes.Length - 1 - index];
        }

        return report;
    }
}
