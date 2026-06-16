using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Services;

public sealed class PlayStationUsbConnectedDeviceProvider : IConnectedDeviceProvider
{
    public Task<IReadOnlyList<ConnectedBluetoothDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<ConnectedBluetoothDevice>>(() =>
        {
            var endpoints = HidGamepadAccess.EnumerateProbeEndpoints(
                addressFilter: null,
                HidEndpointDiscoveryStage.GlobalAggressive,
                cancellationToken);
            var byAddress = new Dictionary<string, ConnectedBluetoothDevice>(StringComparer.OrdinalIgnoreCase);

            foreach (var endpoint in endpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!PlayStationUsbBridgeSupport.IsSupportedUsbDualSenseEndpoint(
                        endpoint.InstanceId,
                        parentInstanceId: null,
                        endpoint.DevicePath,
                        endpoint.VendorId,
                        endpoint.ProductId))
                {
                    continue;
                }

                var address = PlayStationUsbBridgeSupport.BuildSyntheticAddress(
                    endpoint.InstanceId,
                    endpoint.DevicePath,
                    endpoint.ProductId);
                if (string.IsNullOrWhiteSpace(address))
                {
                    continue;
                }

                byAddress[address] = new ConnectedBluetoothDevice(
                    DeviceId: endpoint.InstanceId,
                    Address: address,
                    DisplayName: PlayStationUsbBridgeSupport.GetDisplayName(endpoint.ProductId),
                    IsConnected: true,
                    CategoryHint: "gamepad controller dualsense pico2w usb");
            }

            return byAddress.Values.ToList();
        }, cancellationToken);
    }
}
