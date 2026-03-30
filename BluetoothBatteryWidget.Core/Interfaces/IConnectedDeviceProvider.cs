using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Interfaces;

public interface IConnectedDeviceProvider
{
    Task<IReadOnlyList<ConnectedBluetoothDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken);
}
