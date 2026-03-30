namespace BluetoothBatteryWidget.Core.Services;

public static class IconImageOverrideParser
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
            var path = pair.Value?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedAddress) || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            result[normalizedAddress] = path;
        }

        return result;
    }

    public static void Set(IDictionary<string, string> target, string address, string imagePath)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        var normalizedPath = imagePath?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAddress) || string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        target[normalizedAddress] = normalizedPath;
    }

    public static void Remove(IDictionary<string, string> target, string address)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return;
        }

        target.Remove(normalizedAddress);
    }

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
}

