using System.Reflection;

namespace BluetoothBatteryWidget.App.Services;

internal static class AppVersionInfo
{
    internal const string FallbackVersion = "1.0.9";

    internal static string DisplayVersion => ResolveDisplayVersion();

    private static string ResolveDisplayVersion()
    {
        var assembly = typeof(AppVersionInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+');
            return plusIndex > 0 ? informational[..plusIndex] : informational;
        }

        var version = assembly.GetName().Version;
        if (version is not null)
        {
            return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }

        return FallbackVersion;
    }
}
