using System.Globalization;
using BluetoothBatteryWidget.App.Converters;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryPercentTextConverterTests
{
    [Fact]
    public void Convert_WhenBatteryPercentExists_ReturnsPercentText()
    {
        var converter = new BatteryPercentTextConverter();

        var text = converter.Convert(
            [65, false, true],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("65%", text);
    }

    [Fact]
    public void Convert_WhenConnectedWithoutResolvedBattery_ReturnsNA()
    {
        var converter = new BatteryPercentTextConverter();

        var text = converter.Convert(
            [null!, false, true],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("N/A", text);
    }

    [Fact]
    public void Convert_WhenConnectingFlagIsTrue_ReturnsKoreanConnectingTextByDefault()
    {
        var converter = new BatteryPercentTextConverter();

        var text = converter.Convert(
            [null!, false, true, true],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("연결중", text);
    }

    [Fact]
    public void Convert_WhenDisconnectedAndNoBattery_ReturnsKoreanUnsupportedByDefault()
    {
        var converter = new BatteryPercentTextConverter();

        var text = converter.Convert(
            [null!, false, false],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("미지원", text);
    }

    [Fact]
    public void Convert_WhenConnecting_WithEnglishLanguage_ReturnsEnglishConnecting()
    {
        var converter = new BatteryPercentTextConverter();

        var text = converter.Convert(
            [null!, false, true, true, WidgetSettings.EnglishLanguage],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("Connecting", text);
    }

    [Fact]
    public void Convert_WhenDisconnected_WithEnglishLanguage_ReturnsEnglishUnsupported()
    {
        var converter = new BatteryPercentTextConverter();

        var text = converter.Convert(
            [null!, false, false, false, WidgetSettings.EnglishLanguage],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("Unsupported", text);
    }
}
