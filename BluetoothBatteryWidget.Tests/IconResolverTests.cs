using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class IconResolverTests
{
    [Fact]
    public void Resolve_UsesOverride_WhenConfigured()
    {
        var resolver = new IconResolver();
        var overrides = new Dictionary<string, IconKey>(StringComparer.OrdinalIgnoreCase)
        {
            ["AABBCCDDEEFF"] = IconKey.Headset
        };

        var icon = resolver.Resolve("AA:BB:CC:DD:EE:FF", DeviceCategory.Mouse, "MX Master 4", overrides);

        Assert.Equal(IconKey.Headset, icon);
    }

    [Fact]
    public void Resolve_UsesCategory_WhenNoOverride()
    {
        var resolver = new IconResolver();
        var icon = resolver.Resolve("AABBCCDDEEFF", DeviceCategory.Mouse, "MX Master 4", new Dictionary<string, IconKey>());

        Assert.Equal(IconKey.Mouse, icon);
    }
}
