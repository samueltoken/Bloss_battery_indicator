using System.Security.Cryptography;
using System.Text;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal static class PlayStationUsbBridgeSupport
{
    internal const string SonyVendorId = "054C";
    internal const string DualSenseProductId = "0CE6";
    internal const string DualSenseEdgeProductId = "0DF2";

    public static bool IsSupportedUsbDualSenseEndpoint(
        string? instanceId,
        string? parentInstanceId,
        string? devicePath,
        string? vendorId,
        string? productId)
    {
        if (IsBluetoothEndpoint(instanceId) ||
            IsBluetoothEndpoint(parentInstanceId) ||
            IsBluetoothEndpoint(devicePath))
        {
            return false;
        }

        if (IsSupportedVidPid(vendorId, productId))
        {
            return true;
        }

        return HasSupportedVidPid(instanceId) ||
               HasSupportedVidPid(parentInstanceId) ||
               HasSupportedVidPid(devicePath);
    }

    public static string BuildSyntheticAddress(string? instanceId, string? devicePath)
    {
        var source = $"{instanceId ?? string.Empty}|{devicePath ?? string.Empty}";
        if (string.IsNullOrWhiteSpace(source.Trim('|')))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(source.ToUpperInvariant());
        var hash = SHA256.HashData(bytes);
        var builder = new StringBuilder(12);
        for (var index = 0; index < 6; index++)
        {
            builder.Append(hash[index].ToString("X2"));
        }

        return AddressNormalizer.NormalizeAddress(builder.ToString());
    }

    public static string GetDisplayName(string? productId)
    {
        return string.Equals(productId, DualSenseEdgeProductId, StringComparison.OrdinalIgnoreCase)
            ? "DualSense Edge Wireless Controller (USB/Pico2W)"
            : "DualSense Wireless Controller (USB/Pico2W)";
    }

    public static bool IsSupportedVidPid(string? vendorId, string? productId)
    {
        if (!string.Equals(vendorId, SonyVendorId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(productId, DualSenseProductId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, DualSenseEdgeProductId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSupportedVidPid(string? text)
    {
        return HidProbeTextParser.TryParseVidPid(text, out var vendorId, out var productId) &&
               IsSupportedVidPid(vendorId, productId);
    }

    private static bool IsBluetoothEndpoint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("{00001124-0000-1000-8000-00805F9B34FB}", StringComparison.OrdinalIgnoreCase);
    }
}
