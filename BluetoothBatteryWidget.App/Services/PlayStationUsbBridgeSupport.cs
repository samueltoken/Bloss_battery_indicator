using System.Security.Cryptography;
using System.Text;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal static class PlayStationUsbBridgeSupport
{
    internal const string SonyVendorId = "054C";
    internal const string DualSenseProductId = "0CE6";
    internal const string DualSenseEdgeProductId = "0DF2";
    internal const string StableDualSensePico2WAddress = "054C0CE60001";
    internal const string StableDualSenseEdgePico2WAddress = "054C0DF20001";

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

    public static string BuildSyntheticAddress(string? instanceId, string? devicePath, string? productId = null)
    {
        if (TryBuildStableUsbBridgeAddress(productId, instanceId, devicePath, out var stableAddress))
        {
            return stableAddress;
        }

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

    public static bool IsStablePico2WAddress(string? address)
    {
        var normalized = AddressNormalizer.NormalizeAddress(address);
        return string.Equals(normalized, StableDualSensePico2WAddress, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, StableDualSenseEdgePico2WAddress, StringComparison.OrdinalIgnoreCase);
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

    private static bool TryBuildStableUsbBridgeAddress(
        string? productId,
        string? instanceId,
        string? devicePath,
        out string address)
    {
        address = string.Empty;
        var normalizedProductId = NormalizeProductId(productId);
        if (string.IsNullOrWhiteSpace(normalizedProductId) &&
            HidProbeTextParser.TryParseVidPid(instanceId, out var instanceVendorId, out var instanceProductId) &&
            string.Equals(instanceVendorId, SonyVendorId, StringComparison.OrdinalIgnoreCase))
        {
            normalizedProductId = instanceProductId;
        }

        if (string.IsNullOrWhiteSpace(normalizedProductId) &&
            HidProbeTextParser.TryParseVidPid(devicePath, out var pathVendorId, out var pathProductId) &&
            string.Equals(pathVendorId, SonyVendorId, StringComparison.OrdinalIgnoreCase))
        {
            normalizedProductId = pathProductId;
        }

        address = normalizedProductId switch
        {
            DualSenseProductId => StableDualSensePico2WAddress,
            DualSenseEdgeProductId => StableDualSenseEdgePico2WAddress,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(address);
    }

    private static string NormalizeProductId(string? productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return string.Empty;
        }

        var hexOnly = new StringBuilder(4);
        foreach (var ch in productId)
        {
            if (Uri.IsHexDigit(ch))
            {
                hexOnly.Append(char.ToUpperInvariant(ch));
            }
        }

        if (hexOnly.Length < 4)
        {
            return string.Empty;
        }

        return hexOnly.ToString()[^4..];
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
