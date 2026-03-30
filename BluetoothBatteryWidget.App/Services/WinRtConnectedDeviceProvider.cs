using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BluetoothBatteryWidget.App.Services;

public sealed class WinRtConnectedDeviceProvider : IConnectedDeviceProvider
{
    private static readonly TimeSpan EnumerationTimeout = TimeSpan.FromSeconds(4);

    private static readonly string[] RequestedProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.DeviceAddress",
        "System.Devices.Aep.Category"
    ];

    public async Task<IReadOnlyList<ConnectedBluetoothDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken)
    {
        var devicesByAddress = new Dictionary<string, ConnectedBluetoothDevice>(StringComparer.OrdinalIgnoreCase);

        await CollectAsync(BluetoothLEDevice.GetDeviceSelector(), devicesByAddress, cancellationToken).ConfigureAwait(false);
        await CollectAsync(BluetoothDevice.GetDeviceSelector(), devicesByAddress, cancellationToken).ConfigureAwait(false);

        return devicesByAddress.Values.ToList();
    }

    private static async Task CollectAsync(
        string selector,
        Dictionary<string, ConnectedBluetoothDevice> target,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(EnumerationTimeout);
            var collection = await DeviceInformation
                .FindAllAsync(selector, RequestedProperties)
                .AsTask(timeoutCts.Token)
                .ConfigureAwait(false);
            foreach (var info in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryReadIsConnected(info, out var isConnected) || !isConnected)
                {
                    continue;
                }

                var normalizedAddress = TryReadAddress(info);
                if (string.IsNullOrEmpty(normalizedAddress))
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(info.Name)
                    ? $"Bluetooth {normalizedAddress[^4..]}"
                    : info.Name.Trim();

                var categoryHint = TryReadStringProperty(info, "System.Devices.Aep.Category");
                var candidate = new ConnectedBluetoothDevice(
                    DeviceId: info.Id,
                    Address: normalizedAddress,
                    DisplayName: displayName,
                    IsConnected: true,
                    CategoryHint: categoryHint);

                if (!target.TryGetValue(normalizedAddress, out var existing))
                {
                    target[normalizedAddress] = candidate;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existing.DisplayName) && !string.IsNullOrWhiteSpace(candidate.DisplayName))
                {
                    target[normalizedAddress] = candidate;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout/cancel: keep best-effort partial result.
        }
        catch
        {
            // WinRT enumeration may fail on specific stacks; return best effort.
        }
    }

    private static bool TryReadIsConnected(DeviceInformation info, out bool isConnected)
    {
        isConnected = false;
        if (!info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value))
        {
            return false;
        }

        isConnected = value switch
        {
            bool boolValue => boolValue,
            byte byteValue => byteValue != 0,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            _ => false
        };

        return true;
    }

    private static string TryReadAddress(DeviceInformation info)
    {
        if (!info.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var value))
        {
            return string.Empty;
        }

        return value switch
        {
            ulong rawUlong => AddressNormalizer.NormalizeAddress(rawUlong),
            long rawLong => AddressNormalizer.NormalizeAddress(unchecked((ulong)rawLong)),
            string address => AddressNormalizer.NormalizeAddress(address),
            _ => AddressNormalizer.NormalizeAddress(value.ToString())
        };
    }

    private static string? TryReadStringProperty(DeviceInformation info, string keyName)
    {
        if (!info.Properties.TryGetValue(keyName, out var value))
        {
            return null;
        }

        return value?.ToString();
    }
}
