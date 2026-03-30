using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class NameOverrideParserTests
{
    [Fact]
    public void Parse_NormalizesAddress_AndSkipsBlankName()
    {
        var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AA:11:22:33:44:55"] = "  내꺼  ",
            ["BB1122334455"] = "   "
        };

        var parsed = NameOverrideParser.Parse(raw);

        Assert.True(parsed.ContainsKey("AA1122334455"));
        Assert.Equal("내꺼", parsed["AA1122334455"]);
        Assert.False(parsed.ContainsKey("BB1122334455"));
    }

    [Fact]
    public void Set_StoresTrimmedName_WithNormalizedAddress()
    {
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        NameOverrideParser.Set(target, "AA:11:22:33:44:55", "  내꺼 ");

        Assert.Equal("내꺼", target["AA1122334455"]);
    }

    [Fact]
    public void Remove_DeletesByNormalizedAddress()
    {
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AA1122334455"] = "내꺼"
        };

        NameOverrideParser.Remove(target, "AA:11:22:33:44:55");

        Assert.Empty(target);
    }
}
