using System.Text;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal static class XboxEndpointSignalBuilder
{
    public static IReadOnlyDictionary<string, string> Build(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        var connectedAddresses = connectedDevices
            .Select(device => AddressNormalizer.NormalizeAddress(device.Address))
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (connectedAddresses.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var byAddress = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
        var endpoints = HidGamepadAccess.EnumerateBluetoothEndpoints(addressFilter: null, cancellationToken);
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var address = AddressNormalizer.NormalizeAddress(endpoint.Address);
            if (string.IsNullOrWhiteSpace(address) || !connectedAddresses.Contains(address))
            {
                continue;
            }

            if (!byAddress.TryGetValue(address, out var sb))
            {
                sb = new StringBuilder();
                byAddress[address] = sb;
            }

            AppendToken(sb, endpoint.VendorId, "VID_");
            AppendToken(sb, endpoint.ProductId, "PID_");
            AppendText(sb, endpoint.InstanceId);
            AppendText(sb, endpoint.DevicePath);
        }

        return byAddress.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AppendToken(StringBuilder sb, string value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (sb.Length > 0)
        {
            sb.Append(' ');
        }

        sb.Append(prefix);
        sb.Append(value.Trim());
    }

    private static void AppendText(StringBuilder sb, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (sb.Length > 0)
        {
            sb.Append(' ');
        }

        sb.Append(value.Trim());
    }
}
