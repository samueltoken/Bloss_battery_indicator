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
            ["D62A84F9A8A4"] = IconKey.Headset
        };

        var icon = resolver.Resolve("D6:2A:84:F9:A8:A4", DeviceCategory.Mouse, "MX Master 4", overrides);

        Assert.Equal(IconKey.Headset, icon);
    }

    [Fact]
    public void Resolve_UsesCategory_WhenNoOverride()
    {
        var resolver = new IconResolver();
        var icon = resolver.Resolve("D62A84F9A8A4", DeviceCategory.Mouse, "MX Master 4", new Dictionary<string, IconKey>());

        Assert.Equal(IconKey.Mouse, icon);
    }
}
