using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Services;

public sealed class SteamControllerTritonConnectedDeviceProvider : IConnectedDeviceProvider
{
    private readonly SteamControllerTritonHidReader _reader;

    public SteamControllerTritonConnectedDeviceProvider(SteamControllerTritonHidReader reader)
    {
        _reader = reader;
    }

    public async Task<IReadOnlyList<ConnectedBluetoothDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken)
    {
        var snapshots = await Task.Run(
            () => _reader.ReadSnapshots(cancellationToken, waitForBattery: false),
            cancellationToken).ConfigureAwait(false);

        return snapshots
            .Where(snapshot => snapshot.IsConnected)
            .Select(snapshot => new ConnectedBluetoothDevice(
                DeviceId: snapshot.DeviceId,
                Address: snapshot.Address,
                DisplayName: snapshot.DisplayName,
                IsConnected: true,
                CategoryHint: "gamepad controller steam puck"))
            .ToList();
    }
}
