using System.Security.Cryptography;
using System.Text;

namespace BluetoothBatteryWidget.Core.Services;

public static class BatteryModelKeyResolver
{
    public static string ResolveFromInstanceId(string? instanceId)
    {
        if (!HidProbeTextParser.TryParseVidPid(instanceId, out var vendorId, out var productId))
        {
            return string.Empty;
        }

        return ResolveFromVidPid(vendorId, productId);
    }

    public static string ResolveFromSignal(string? signalText)
    {
        if (!HidProbeTextParser.TryParseVidPid(signalText, out var vendorId, out var productId))
        {
            return string.Empty;
        }

        return ResolveFromVidPid(vendorId, productId);
    }

    public static string ResolveFromVidPid(string? vendorId, string? productId)
    {
        if (string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(productId))
        {
            return string.Empty;
        }

        return GamepadProfileStore.BuildModelKey(vendorId, productId);
    }

    public static string ResolveNormalizedModelKey(
        string? identityVendorId,
        string? identityProductId,
        string? transportVendorId,
        string? transportProductId,
        string? address,
        string? displayName)
    {
        var identityModel = ResolveFromVidPid(identityVendorId, identityProductId);
        if (!string.IsNullOrWhiteSpace(identityModel))
        {
            return identityModel;
        }

        var transportModel = ResolveFromVidPid(transportVendorId, transportProductId);
        if (!string.IsNullOrWhiteSpace(transportModel))
        {
            return transportModel;
        }

        return BuildAddressNameFingerprint(address, displayName);
    }

    public static string ResolveIdentityKey(
        string? identityVendorId,
        string? identityProductId,
        string? transportVendorId,
        string? transportProductId,
        string? address,
        string? displayName,
        string? endpointSignature)
    {
        var segments = new List<string>(4);
        var identityModel = ResolveFromVidPid(identityVendorId, identityProductId);
        if (!string.IsNullOrWhiteSpace(identityModel))
        {
            segments.Add($"ID={identityModel}");
        }

        var transportModel = ResolveFromVidPid(transportVendorId, transportProductId);
        if (!string.IsNullOrWhiteSpace(transportModel))
        {
            segments.Add($"TR={transportModel}");
        }

        var fingerprint = BuildAddressNameFingerprint(address, displayName);
        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            segments.Add($"FP={fingerprint}");
        }

        var endpointToken = BuildEndpointToken(endpointSignature);
        if (!string.IsNullOrWhiteSpace(endpointToken))
        {
            segments.Add($"EP={endpointToken}");
        }

        if (segments.Count == 0)
        {
            return "IDENTITY_UNKNOWN";
        }

        return string.Join("|", segments);
    }

    public static string BuildEndpointSignature(params string?[] parts)
    {
        if (parts is null || parts.Length == 0)
        {
            return string.Empty;
        }

        var normalized = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim().ToUpperInvariant())
            .ToArray();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        return string.Join("|", normalized);
    }

    private static string BuildAddressNameFingerprint(string? address, string? displayName)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        var normalizedName = string.IsNullOrWhiteSpace(displayName)
            ? string.Empty
            : displayName.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedAddress) && string.IsNullOrWhiteSpace(normalizedName))
        {
            return string.Empty;
        }

        var source = $"{normalizedAddress}|{normalizedName}";
        var bytes = Encoding.UTF8.GetBytes(source);
        var hash = SHA1.HashData(bytes);
        var token = Convert.ToHexString(hash[..6]);
        return $"FP_{token}";
    }

    private static string BuildEndpointToken(string? endpointSignature)
    {
        if (string.IsNullOrWhiteSpace(endpointSignature))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(endpointSignature.Trim().ToUpperInvariant());
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash[..4]);
    }
}
