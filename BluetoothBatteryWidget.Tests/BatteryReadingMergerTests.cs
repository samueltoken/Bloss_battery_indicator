using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryReadingMergerTests
{
    [Fact]
    public void MergeByAddress_HigherPriorityNonNull_OverridesLowerPriority()
    {
        var setup = new List<PnpBatteryReading>
        {
            new("setup", "90B685C680D8", "DualSense", 25)
        };
        var xinput = new List<PnpBatteryReading>
        {
            new("xinput", "90B685C680D8", "DualSense", null)
        };
        var sony = new List<PnpBatteryReading>
        {
            new("sony", "90B685C680D8", "DualSense", 65)
        };

        var merged = BatteryReadingMerger.MergeByAddress(setup, xinput, sony);

        Assert.Single(merged);
        Assert.Equal(65, merged[0].BatteryPercent);
        Assert.Equal("90B685C680D8", merged[0].Address);
        Assert.Equal(BatteryConfidence.Confirmed, merged[0].BatteryConfidence);
    }

    [Fact]
    public void MergeByAddress_HigherPriorityNull_DoesNotOverrideExistingValue()
    {
        var setup = new List<PnpBatteryReading>
        {
            new("setup", "90B685C680D8", "DualSense", 40)
        };
        var sony = new List<PnpBatteryReading>
        {
            new("sony", "90B685C680D8", "DualSense", null)
        };

        var merged = BatteryReadingMerger.MergeByAddress(setup, sony);

        Assert.Single(merged);
        Assert.Equal(40, merged[0].BatteryPercent);
    }

    [Fact]
    public void MergeByAddress_InvalidAddress_IsIgnored()
    {
        var setup = new List<PnpBatteryReading>
        {
            new("setup", "invalid-address", "Unknown", 10)
        };

        var merged = BatteryReadingMerger.MergeByAddress(setup);

        Assert.Empty(merged);
    }

    [Fact]
    public void MergeByAddress_LearnedCanFillSetupNull_WithoutOverridingXInput()
    {
        var setup = new List<PnpBatteryReading>
        {
            new("setup", "90B685C680D8", "Pad", null)
        };
        var learned = new List<PnpBatteryReading>
        {
            new("learned", "90B685C680D8", "Pad", 55)
        };
        var xinput = new List<PnpBatteryReading>
        {
            new("xinput", "90B685C680D8", "Pad", 85)
        };

        var merged = BatteryReadingMerger.MergeByAddress(setup, learned, xinput);

        Assert.Single(merged);
        Assert.Equal(85, merged[0].BatteryPercent);
    }

    [Fact]
    public void MergeByAddress_LearnedCanFillSetupNull_WhenUpperSourcesNull()
    {
        var setup = new List<PnpBatteryReading>
        {
            new("setup", "90B685C680D8", "Pad", null)
        };
        var learned = new List<PnpBatteryReading>
        {
            new("learned", "90B685C680D8", "Pad", 60)
        };
        var xinput = new List<PnpBatteryReading>
        {
            new("xinput", "90B685C680D8", "Pad", null)
        };

        var merged = BatteryReadingMerger.MergeByAddress(setup, learned, xinput);

        Assert.Single(merged);
        Assert.Equal(60, merged[0].BatteryPercent);
    }

    [Fact]
    public void MergeByAddress_HigherPriorityConfirmed_OverridesEstimated()
    {
        var learned = new List<PnpBatteryReading>
        {
            new("learned", "90B685C680D8", "Pad", 58, BatteryConfidence.Estimated)
        };
        var xinput = new List<PnpBatteryReading>
        {
            new("xinput", "90B685C680D8", "Pad", 85, BatteryConfidence.Confirmed)
        };

        var merged = BatteryReadingMerger.MergeByAddress(learned, xinput);

        Assert.Single(merged);
        Assert.Equal(85, merged[0].BatteryPercent);
        Assert.Equal(BatteryConfidence.Confirmed, merged[0].BatteryConfidence);
    }
}
