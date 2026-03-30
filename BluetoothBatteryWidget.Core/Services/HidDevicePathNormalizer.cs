namespace BluetoothBatteryWidget.Core.Services;

public static class HidDevicePathNormalizer
{
    public static string Normalize(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var path = rawPath.Trim().Replace('/', '\\');

        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return path;
        }

        if (path.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            return @"\\?\" + path[4..];
        }

        if (path.StartsWith(@"\?\", StringComparison.Ordinal))
        {
            return @"\\?\" + path[3..];
        }

        if (path.StartsWith(@"?\", StringComparison.Ordinal))
        {
            return @"\\?\" + path[2..];
        }

        return path;
    }
}
