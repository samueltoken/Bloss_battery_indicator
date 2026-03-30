namespace BluetoothBatteryWidget.Core.Services;

public static class NameOverrideParser
{
    public static IReadOnlyDictionary<string, string> Parse(IReadOnlyDictionary<string, string>? rawOverrides)
    {
        if (rawOverrides is null || rawOverrides.Count == 0)
        {
            return Empty;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in rawOverrides)
        {
            var normalizedAddress = AddressNormalizer.NormalizeAddress(pair.Key);
            var name = pair.Value?.Trim();
            if (string.IsNullOrEmpty(normalizedAddress) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            result[normalizedAddress] = name;
        }

        return result;
    }

    public static void Set(IDictionary<string, string> target, string address, string displayName)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        var name = displayName?.Trim();
        if (string.IsNullOrEmpty(normalizedAddress) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        target[normalizedAddress] = name;
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

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
}
