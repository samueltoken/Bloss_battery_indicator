using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal static class GuideButtonLogFormatter
{
    public static string SanitizeField(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    public static string MaskAddress(string? value)
    {
        var normalized = AddressNormalizer.NormalizeAddress(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : "****";
        }

        if (normalized.Length <= 4)
        {
            return "****";
        }

        return new string('*', normalized.Length - 4) + normalized[^4..];
    }
}
