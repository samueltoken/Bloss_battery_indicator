using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Interfaces;

public interface IIconResolver
{
    IconKey Resolve(
        string address,
        DeviceCategory category,
        string displayName,
        IReadOnlyDictionary<string, IconKey> overrides);
}
