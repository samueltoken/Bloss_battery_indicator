using System.Globalization;
using System.Text.RegularExpressions;

namespace BluetoothBatteryWidget.Core.Services;

public static partial class HidProbeTextParser
{
    [GeneratedRegex(@"VID&([0-9A-Fa-f]{4,8}).*?PID&([0-9A-Fa-f]{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex AmpVidPidRegex();

    [GeneratedRegex(@"VID_([0-9A-Fa-f]{4,8}).*?PID_([0-9A-Fa-f]{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UnderscoreVidPidRegex();

    public static string ExtractAddress(string? text)
    {
        return AddressNormalizer.ExtractAddressFromInstanceId(text);
    }

    public static bool TryParseVidPid(string? text, out string vendorId, out string productId)
    {
        vendorId = string.Empty;
        productId = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (TryMatchVidPid(AmpVidPidRegex().Match(text), out vendorId, out productId))
        {
            return true;
        }

        return TryMatchVidPid(UnderscoreVidPidRegex().Match(text), out vendorId, out productId);
    }

    private static bool TryMatchVidPid(Match match, out string vendorId, out string productId)
    {
        vendorId = string.Empty;
        productId = string.Empty;
        if (!match.Success)
        {
            return false;
        }

        var rawVid = match.Groups[1].Value;
        var rawPid = match.Groups[2].Value;
        if (string.IsNullOrWhiteSpace(rawVid) || string.IsNullOrWhiteSpace(rawPid))
        {
            return false;
        }

        var normalizedVid = rawVid.Length > 4 && rawVid.StartsWith("0002", StringComparison.OrdinalIgnoreCase)
            ? rawVid[^4..]
            : rawVid[..Math.Min(4, rawVid.Length)];

        if (!ushort.TryParse(normalizedVid, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vid) ||
            !ushort.TryParse(rawPid[..Math.Min(4, rawPid.Length)], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid))
        {
            return false;
        }

        vendorId = vid.ToString("X4");
        productId = pid.ToString("X4");
        return true;
    }
}
