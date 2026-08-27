using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Services;

public sealed class PlayStationUsbConnectedDeviceProvider : IConnectedDeviceProvider
{
    private readonly PlayStationControllerIdentityResolver _identityResolver;
    private readonly Func<CancellationToken, IReadOnlyList<HidGamepadEndpoint>> _endpointEnumerator;

    public PlayStationUsbConnectedDeviceProvider()
        : this(new PlayStationControllerIdentityResolver())
    {
    }

    internal PlayStationUsbConnectedDeviceProvider(
        PlayStationControllerIdentityResolver identityResolver,
        Func<CancellationToken, IReadOnlyList<HidGamepadEndpoint>>? endpointEnumerator = null)
    {
        _identityResolver = identityResolver;
        _endpointEnumerator = endpointEnumerator ?? (cancellationToken =>
            HidGamepadAccess.EnumerateProbeEndpoints(
                addressFilter: null,
                HidEndpointDiscoveryStage.GlobalAggressive,
                cancellationToken));
    }

    public Task<IReadOnlyList<ConnectedBluetoothDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<ConnectedBluetoothDevice>>(() =>
        {
            var endpoints = _endpointEnumerator(cancellationToken);
            var byAddress = new Dictionary<string, ConnectedBluetoothDevice>(StringComparer.OrdinalIgnoreCase);
            var supportedEndpoints = endpoints
                .Where(endpoint => PlayStationUsbBridgeSupport.IsSupportedUsbDualSenseEndpoint(
                    endpoint.InstanceId,
                    parentInstanceId: null,
                    endpoint.DevicePath,
                    endpoint.VendorId,
                    endpoint.ProductId))
                .ToList();

            _identityResolver.RetainPresentEndpoints(supportedEndpoints);

            foreach (var endpoint in supportedEndpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var identity = _identityResolver.Resolve(
                    endpoint.InstanceId,
                    endpoint.DevicePath,
                    endpoint.ProductId);
                var address = identity.ControllerAddress;
                if (string.IsNullOrWhiteSpace(address))
                {
                    continue;
                }

                byAddress[address] = new ConnectedBluetoothDevice(
                    DeviceId: endpoint.InstanceId,
                    Address: address,
                    DisplayName: PlayStationUsbBridgeSupport.GetDisplayName(endpoint.ProductId),
                    IsConnected: true,
                    CategoryHint: "gamepad controller dualsense pico2w usb",
                    BridgeAddress: identity.BridgeAddress);
            }

            return byAddress.Values.ToList();
        }, cancellationToken);
    }
}
