using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class IconImageOverrideParserTests
{
    [Fact]
    public void Parse_NormalizesAddress_AndSkipsInvalidEntries()
    {
        var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AA:11:22:33:44:55"] = " C:\\icons\\pad.png ",
            ["invalid"] = "C:\\icons\\skip.png",
            ["BB:11:22:33:44:66"] = "   "
        };

        var parsed = IconImageOverrideParser.Parse(raw);

        Assert.Single(parsed);
        Assert.True(parsed.ContainsKey("AA1122334455"));
        Assert.Equal("C:\\icons\\pad.png", parsed["AA1122334455"]);
    }

    [Fact]
    public void Set_StoresByNormalizedAddress()
    {
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        IconImageOverrideParser.Set(target, "AA:11:22:33:44:55", " C:\\icons\\me.png ");

        Assert.Single(target);
        Assert.Equal("C:\\icons\\me.png", target["AA1122334455"]);
    }

    [Fact]
    public void Remove_DeletesNormalizedAddress()
    {
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AA1122334455"] = "C:\\icons\\me.png"
        };

        IconImageOverrideParser.Remove(target, "AA:11:22:33:44:55");

        Assert.Empty(target);
    }
}

