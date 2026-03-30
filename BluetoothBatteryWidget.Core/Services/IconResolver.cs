using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public sealed class IconResolver : IIconResolver
{
    public IconKey Resolve(
        string address,
        DeviceCategory category,
        string displayName,
        IReadOnlyDictionary<string, IconKey> overrides)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        if (!string.IsNullOrEmpty(normalizedAddress) && overrides.TryGetValue(normalizedAddress, out var overrideIcon))
        {
            return overrideIcon;
        }

        return category switch
        {
            DeviceCategory.Mouse => IconKey.Mouse,
            DeviceCategory.Keyboard => IconKey.Keyboard,
            DeviceCategory.Headset => IconKey.Headset,
            DeviceCategory.Earbuds => IconKey.Earbuds,
            DeviceCategory.Speaker => IconKey.Speaker,
            DeviceCategory.Gamepad => IconKey.Gamepad,
            DeviceCategory.Phone => IconKey.Phone,
            DeviceCategory.Tablet => IconKey.Tablet,
            DeviceCategory.Laptop => IconKey.Laptop,
            DeviceCategory.Other when displayName.Contains("mouse", StringComparison.OrdinalIgnoreCase) => IconKey.Mouse,
            DeviceCategory.Other when displayName.Contains("keyboard", StringComparison.OrdinalIgnoreCase) => IconKey.Keyboard,
            DeviceCategory.Other when displayName.Contains("controller", StringComparison.OrdinalIgnoreCase) => IconKey.Gamepad,
            _ => IconKey.Unknown
        };
    }
}
