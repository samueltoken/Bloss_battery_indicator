namespace BluetoothBatteryWidget.Tests;

public sealed class PowerIdleSourceSafetyTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void AppSources_DoNotUseWindowsCallsThatForceDisplayAwake()
    {
        var sourceRoots = new[]
        {
            Path.Combine(ProjectRoot, "BluetoothBatteryWidget.App"),
            Path.Combine(ProjectRoot, "BluetoothBatteryWidget.Core")
        };
        var forbiddenTokens = new[]
        {
            "SetThreadExecutionState",
            "ES_DISPLAY_REQUIRED",
            "ES_SYSTEM_REQUIRED",
            "PowerSetRequest",
            "SendInput(",
            "mouse_event(",
            "keybd_event("
        };

        var violations = sourceRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsGeneratedPath(path))
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(ProjectRoot, path)} contains {token}");
            })
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }
}
