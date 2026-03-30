using System.Text.RegularExpressions;

namespace BluetoothBatteryWidget.Core.Services;

public static partial class AddressNormalizer
{
    [GeneratedRegex("DEV_([0-9A-Fa-f]{12})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex InstanceAddressRegex();
    [GeneratedRegex("&([0-9A-Fa-f]{12})_", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex HidInstanceAddressRegex();

    public static string NormalizeAddress(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[12];
        var index = 0;
        foreach (var ch in raw)
        {
            if (Uri.IsHexDigit(ch))
            {
                if (index >= buffer.Length)
                {
                    return string.Empty;
                }

                buffer[index] = char.ToUpperInvariant(ch);
                index++;
            }
        }

        if (index != 12)
        {
            return string.Empty;
        }

        return new string(buffer);
    }

    public static string NormalizeAddress(ulong rawAddress) => rawAddress.ToString("X12");

    public static string ExtractAddressFromInstanceId(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return string.Empty;
        }

        var match = InstanceAddressRegex().Match(instanceId);
        if (match.Success)
        {
            return NormalizeAddress(match.Groups[1].Value);
        }

        var hidMatch = HidInstanceAddressRegex().Match(instanceId);
        return hidMatch.Success ? NormalizeAddress(hidMatch.Groups[1].Value) : string.Empty;
    }
}
