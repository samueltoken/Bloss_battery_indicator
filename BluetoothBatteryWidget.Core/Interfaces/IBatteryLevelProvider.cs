using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Interfaces;

public interface IBatteryLevelProvider
{
    Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken);
}
