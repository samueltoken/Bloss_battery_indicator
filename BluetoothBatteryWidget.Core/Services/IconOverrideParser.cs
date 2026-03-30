using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class IconOverrideParser
{
    public static IReadOnlyDictionary<string, IconKey> Parse(IReadOnlyDictionary<string, string>? rawOverrides)
    {
        if (rawOverrides is null || rawOverrides.Count == 0)
        {
            return Empty;
        }

        var result = new Dictionary<string, IconKey>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in rawOverrides)
        {
            var normalizedAddress = AddressNormalizer.NormalizeAddress(pair.Key);
            if (string.IsNullOrEmpty(normalizedAddress))
            {
                continue;
            }

            if (Enum.TryParse<IconKey>(pair.Value, ignoreCase: true, out var parsed))
            {
                result[normalizedAddress] = parsed;
            }
        }

        return result;
    }

    public static void Set(IDictionary<string, string> target, string address, IconKey icon)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrEmpty(normalizedAddress))
        {
            return;
        }

        target[normalizedAddress] = icon.ToString();
    }

    public static void Remove(IDictionary<string, string> target, string address)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrEmpty(normalizedAddress))
        {
            return;
        }

        target.Remove(normalizedAddress);
    }

    private static readonly IReadOnlyDictionary<string, IconKey> Empty = new Dictionary<string, IconKey>();
}
