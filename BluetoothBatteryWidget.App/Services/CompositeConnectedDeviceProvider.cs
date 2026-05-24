using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class CompositeConnectedDeviceProvider : IConnectedDeviceProvider
{
    private readonly IReadOnlyList<IConnectedDeviceProvider> _providers;

    public CompositeConnectedDeviceProvider(params IConnectedDeviceProvider[] providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlyList<ConnectedBluetoothDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken)
    {
        var byAddress = new Dictionary<string, ConnectedBluetoothDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ConnectedBluetoothDevice> devices;
            try
            {
                devices = await provider.GetConnectedDevicesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                continue;
            }

            foreach (var device in devices)
            {
                var normalizedAddress = AddressNormalizer.NormalizeAddress(device.Address);
                if (string.IsNullOrWhiteSpace(normalizedAddress))
                {
                    continue;
                }

                var normalizedDevice = device with { Address = normalizedAddress };
                if (!byAddress.TryGetValue(normalizedAddress, out var existing) ||
                    ShouldPrefer(normalizedDevice, existing))
                {
                    byAddress[normalizedAddress] = normalizedDevice;
                }
            }
        }

        return byAddress.Values.ToList();
    }

    private static bool ShouldPrefer(ConnectedBluetoothDevice candidate, ConnectedBluetoothDevice existing)
    {
        if (candidate.IsConnected && !existing.IsConnected)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(existing.DisplayName) &&
               !string.IsNullOrWhiteSpace(candidate.DisplayName);
    }
}
