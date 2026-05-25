using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GuideButtonLogFormatterTests
{
    [Fact]
    public void MaskAddress_NormalBluetoothAddress_KeepsOnlyLastFourCharacters()
    {
        var masked = GuideButtonLogFormatter.MaskAddress("AA:BB:CC:DD:EE:FF");

        Assert.Equal("********EEFF", masked);
        Assert.DoesNotContain("AABB", masked);
        Assert.DoesNotContain("CCDD", masked);
    }

    [Fact]
    public void MaskAddress_ShortAddress_IsFullyMasked()
    {
        var masked = GuideButtonLogFormatter.MaskAddress("E020");

        Assert.Equal("****", masked);
    }

    [Fact]
    public void SanitizeField_ReplacesTabsAndLineBreaks()
    {
        var sanitized = GuideButtonLogFormatter.SanitizeField("  one\ttwo\r\nthree  ");

        Assert.Equal("one two  three", sanitized);
    }
}
