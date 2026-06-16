using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal static class FirmwareUpdateOverrideMigration
{
    public static bool TryCopyPico2WOverridesToStableAddress(
        WidgetSettings settings,
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices)
    {
        if (connectedDevices.Count == 0)
        {
            return false;
        }

        settings.IconOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.IconImageOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.NameOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var currentAddresses = connectedDevices
            .Select(device => AddressNormalizer.NormalizeAddress(device.Address))
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (currentAddresses.Count == 0)
        {
            return false;
        }

        var targetAddresses = connectedDevices
            .Where(IsPico2WPlayStationDevice)
            .Select(device => AddressNormalizer.NormalizeAddress(device.Address))
            .Where(PlayStationUsbBridgeSupport.IsStablePico2WAddress)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (targetAddresses.Count == 0)
        {
            return false;
        }

        var orphanOverrideAddresses = CollectOverrideAddresses(settings)
            .Where(address => !currentAddresses.Contains(address))
            .Where(address => !PlayStationUsbBridgeSupport.IsStablePico2WAddress(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (orphanOverrideAddresses.Count != 1)
        {
            return false;
        }

        var sourceAddress = orphanOverrideAddresses[0];
        var changed = false;
        foreach (var targetAddress in targetAddresses)
        {
            if (HasAnyOverride(settings, targetAddress))
            {
                continue;
            }

            changed |= CopyOverrides(settings, sourceAddress, targetAddress);
        }

        return changed;
    }

    private static bool IsPico2WPlayStationDevice(ConnectedBluetoothDevice device)
    {
        var text = $"{device.DisplayName} {device.CategoryHint} {device.DeviceId}";
        return text.Contains("pico2w", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("dualsense", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("054C", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> CollectOverrideAddresses(WidgetSettings settings)
    {
        foreach (var address in CollectAddresses(settings.IconOverrides))
        {
            yield return address;
        }

        foreach (var address in CollectAddresses(settings.IconImageOverrides))
        {
            yield return address;
        }

        foreach (var address in CollectAddresses(settings.NameOverrides))
        {
            yield return address;
        }
    }

    private static IEnumerable<string> CollectAddresses(IDictionary<string, string> overrides)
    {
        foreach (var key in overrides.Keys)
        {
            var normalized = AddressNormalizer.NormalizeAddress(key);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static bool HasAnyOverride(WidgetSettings settings, string address)
    {
        return settings.IconOverrides.ContainsKey(address) ||
               settings.IconImageOverrides.ContainsKey(address) ||
               settings.NameOverrides.ContainsKey(address);
    }

    private static bool CopyOverrides(WidgetSettings settings, string sourceAddress, string targetAddress)
    {
        var changed = false;
        changed |= CopyOverride(settings.IconOverrides, sourceAddress, targetAddress);
        changed |= CopyOverride(settings.IconImageOverrides, sourceAddress, targetAddress);
        changed |= CopyOverride(settings.NameOverrides, sourceAddress, targetAddress);
        return changed;
    }

    private static bool CopyOverride(IDictionary<string, string> overrides, string sourceAddress, string targetAddress)
    {
        if (!overrides.TryGetValue(sourceAddress, out var value) ||
            string.IsNullOrWhiteSpace(value) ||
            overrides.ContainsKey(targetAddress))
        {
            return false;
        }

        overrides[targetAddress] = value;
        return true;
    }
}
