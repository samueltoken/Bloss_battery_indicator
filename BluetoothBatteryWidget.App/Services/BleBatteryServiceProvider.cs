using System.Globalization;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BluetoothBatteryWidget.App.Services;

public sealed class BleBatteryServiceProvider
{
    private static readonly TimeSpan BleEnumerationTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BleOperationTimeout = TimeSpan.FromSeconds(2);

    private static readonly string[] BleRequestedProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.DeviceAddress"
    ];

    public async Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        var byAddress = new Dictionary<string, PnpBatteryReading>(StringComparer.OrdinalIgnoreCase);
        var modelHints = BuildModelKeyHints(cancellationToken);
        var connectedGamepads = BuildConnectedGamepads(connectedDevices);

        foreach (var device in connectedGamepads.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var direct = await TryReadDirectAsync(device, modelHints, cancellationToken).ConfigureAwait(false);
            if (direct is null)
            {
                continue;
            }

            byAddress[direct.Address] = direct;
        }

        if (byAddress.Count < connectedGamepads.Count)
        {
            var unresolved = connectedGamepads.Values
                .Where(device => !byAddress.ContainsKey(device.Address))
                .ToList();
            var bridged = await ResolveViaConnectedBleAliasAsync(unresolved, modelHints, cancellationToken).ConfigureAwait(false);
            foreach (var reading in bridged)
            {
                byAddress[reading.Address] = reading;
            }
        }

        return byAddress.Values.ToList();
    }

    private static Dictionary<string, ConnectedBluetoothDevice> BuildConnectedGamepads(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices)
    {
        var byAddress = new Dictionary<string, ConnectedBluetoothDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in connectedDevices)
        {
            if (!device.IsConnected)
            {
                continue;
            }

            if (DeviceCategoryClassifier.Classify(device.DisplayName, device.CategoryHint) != DeviceCategory.Gamepad)
            {
                continue;
            }

            var normalizedAddress = AddressNormalizer.NormalizeAddress(device.Address);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                continue;
            }

            if (!byAddress.ContainsKey(normalizedAddress))
            {
                byAddress[normalizedAddress] = device with { Address = normalizedAddress };
            }
        }

        return byAddress;
    }

    private static async Task<PnpBatteryReading?> TryReadDirectAsync(
        ConnectedBluetoothDevice device,
        IReadOnlyDictionary<string, string> modelHints,
        CancellationToken cancellationToken)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(normalizedAddress) || !TryParseBluetoothAddress(normalizedAddress, out var bluetoothAddress))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await TryReadFromBluetoothAddressAsync(
                bluetoothAddress,
                normalizedAddress,
                device.DisplayName,
                modelHints,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<PnpBatteryReading>> ResolveViaConnectedBleAliasAsync(
        IReadOnlyList<ConnectedBluetoothDevice> unresolvedDevices,
        IReadOnlyDictionary<string, string> modelHints,
        CancellationToken cancellationToken)
    {
        if (unresolvedDevices.Count == 0)
        {
            return [];
        }

        var unresolvedByAddress = unresolvedDevices
            .GroupBy(device => device.Address, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var unresolvedByName = unresolvedDevices
            .GroupBy(device => NormalizeDisplayName(device.DisplayName), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.Select(device => device.Address).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);
        var mapped = new Dictionary<string, PnpBatteryReading>(StringComparer.OrdinalIgnoreCase);

        DeviceInformationCollection collection;
        try
        {
            collection = await DeviceInformation
                .FindAllAsync(BluetoothLEDevice.GetDeviceSelector(), BleRequestedProperties)
                .AsTask(cancellationToken)
                .WaitAsync(BleEnumerationTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return [];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
        catch
        {
            return [];
        }

        foreach (var info in collection)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryReadIsConnected(info))
            {
                continue;
            }

            var bleName = NormalizeDisplayName(info.Name);
            if (string.IsNullOrWhiteSpace(bleName))
            {
                continue;
            }

            var targetAddress = ResolveAliasAddress(bleName, unresolvedByName);
            if (string.IsNullOrWhiteSpace(targetAddress) || mapped.ContainsKey(targetAddress))
            {
                continue;
            }

            var bleAddressText = TryReadAddress(info);
            if (string.IsNullOrWhiteSpace(bleAddressText) || !TryParseBluetoothAddress(bleAddressText, out var bleAddress))
            {
                continue;
            }

            var targetDevice = unresolvedByAddress[targetAddress];
            var reading = await TryReadFromBluetoothAddressAsync(
                    bleAddress,
                    targetAddress,
                    targetDevice.DisplayName,
                    modelHints,
                    cancellationToken)
                .ConfigureAwait(false);
            if (reading is null)
            {
                continue;
            }

            mapped[targetAddress] = reading;
        }

        return mapped.Values.ToList();
    }

    private static string? ResolveAliasAddress(
        string bleName,
        IReadOnlyDictionary<string, List<string>> unresolvedByName)
    {
        if (unresolvedByName.TryGetValue(bleName, out var exact) && exact.Count == 1)
        {
            return exact[0];
        }

        var fuzzy = unresolvedByName
            .Where(pair => pair.Key.Contains(bleName, StringComparison.OrdinalIgnoreCase) ||
                           bleName.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return fuzzy.Count == 1 ? fuzzy[0] : null;
    }

    private static async Task<PnpBatteryReading?> TryReadFromBluetoothAddressAsync(
        ulong bluetoothAddress,
        string targetAddress,
        string targetDisplayName,
        IReadOnlyDictionary<string, string> modelHints,
        CancellationToken cancellationToken)
    {
        BluetoothLEDevice? bleDevice = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bleDevice = await BluetoothLEDevice
                .FromBluetoothAddressAsync(bluetoothAddress)
                .AsTask(cancellationToken)
                .WaitAsync(BleOperationTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (bleDevice is null)
            {
                return null;
            }

            var batteryLevel = await TryReadBatteryLevelFromDeviceAsync(bleDevice, cancellationToken).ConfigureAwait(false);
            if (batteryLevel is null)
            {
                return null;
            }

            var modelKey = BatteryModelKeyResolver.ResolveFromInstanceId(bleDevice.DeviceId);
            if (string.IsNullOrWhiteSpace(modelKey) &&
                modelHints.TryGetValue(targetAddress, out var hinted))
            {
                modelKey = hinted;
            }

            return new PnpBatteryReading(
                InstanceId: bleDevice.DeviceId ?? $"BLE_BATTERY_{targetAddress}",
                Address: targetAddress,
                DisplayName: targetDisplayName,
                BatteryPercent: batteryLevel,
                BatteryConfidence: BatteryConfidence.Confirmed,
                SourceKind: BatterySourceKind.BleGatt,
                RawMetric: batteryLevel,
                ModelKey: modelKey,
                SuggestCalibration: false,
                ObservedAt: DateTimeOffset.Now);
        }
        catch
        {
            return null;
        }
        finally
        {
            bleDevice?.Dispose();
        }
    }

    private static async Task<int?> TryReadBatteryLevelFromDeviceAsync(
        BluetoothLEDevice bleDevice,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = await bleDevice
            .GetGattServicesForUuidAsync(
                GattServiceUuids.Battery,
                BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken)
            .WaitAsync(BleOperationTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (serviceResult.Status != GattCommunicationStatus.Success)
        {
            return null;
        }

        foreach (var service in serviceResult.Services)
        {
            using (service)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var characteristicResult = await service
                    .GetCharacteristicsForUuidAsync(
                        GattCharacteristicUuids.BatteryLevel,
                        BluetoothCacheMode.Uncached)
                    .AsTask(cancellationToken)
                    .WaitAsync(BleOperationTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (characteristicResult.Status != GattCommunicationStatus.Success ||
                    characteristicResult.Characteristics.Count == 0)
                {
                    continue;
                }

                var batteryLevel = await TryReadBatteryLevelAsync(characteristicResult.Characteristics[0], cancellationToken).ConfigureAwait(false);
                if (batteryLevel is not null)
                {
                    return batteryLevel;
                }
            }
        }

        return null;
    }

    private static async Task<int?> TryReadBatteryLevelAsync(
        GattCharacteristic characteristic,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readResult = await characteristic
                .ReadValueAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken)
                .WaitAsync(BleOperationTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (readResult.Status != GattCommunicationStatus.Success || readResult.Value is null)
            {
                return null;
            }

            if (readResult.Value.Length < 1)
            {
                return null;
            }

            using var reader = DataReader.FromBuffer(readResult.Value);
            var level = reader.ReadByte();
            return level <= 100 ? level : null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> BuildModelKeyHints(CancellationToken cancellationToken)
    {
        var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var endpoints = HidGamepadAccess.EnumerateBluetoothEndpoints(addressFilter: null, cancellationToken);
        foreach (var endpoint in endpoints)
        {
            var address = AddressNormalizer.NormalizeAddress(endpoint.Address);
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            var modelKey = BatteryModelKeyResolver.ResolveNormalizedModelKey(
                endpoint.VendorId,
                endpoint.ProductId,
                endpoint.VendorId,
                endpoint.ProductId,
                address,
                endpoint.DisplayName);
            if (string.IsNullOrWhiteSpace(modelKey))
            {
                continue;
            }

            hints[address] = modelKey;
        }

        return hints;
    }

    private static bool TryParseBluetoothAddress(string normalizedAddress, out ulong value)
    {
        return ulong.TryParse(normalizedAddress, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadIsConnected(DeviceInformation info)
    {
        if (!info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value))
        {
            return false;
        }

        return value switch
        {
            bool boolValue => boolValue,
            byte byteValue => byteValue != 0,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            _ => false
        };
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

    private static string NormalizeDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            name
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }
}
