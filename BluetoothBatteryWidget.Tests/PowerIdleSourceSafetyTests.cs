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
    public void AppSources_DoNotUseWindowsCallsThatContinuouslyForceDisplayAwake()
    {
        var sourceRoots = new[]
        {
            Path.Combine(ProjectRoot, "BluetoothBatteryWidget.App"),
            Path.Combine(ProjectRoot, "BluetoothBatteryWidget.Core")
        };
        var forbiddenTokens = new[]
        {
            "ES_CONTINUOUS",
            "ES_SYSTEM_REQUIRED",
            "PowerSetRequest",
            "TryTurnDisplayOff",
            "MonitorPowerOff",
            "DisplayOffFallback",
            "display_off_fallback",
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

        var systemDisplayPowerPath = Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "Services",
            "SystemDisplayPower.cs");
        var systemDisplayPowerSource = File.ReadAllText(systemDisplayPowerPath);
        Assert.DoesNotContain("TryNotifyDisplayUserActivity", systemDisplayPowerSource);
        Assert.DoesNotContain("SetThreadExecutionState", systemDisplayPowerSource);
        Assert.DoesNotContain("EsDisplayRequired", systemDisplayPowerSource);
        Assert.DoesNotContain("ES_CONTINUOUS", systemDisplayPowerSource);
        Assert.DoesNotContain("EsContinuous", systemDisplayPowerSource);
    }

    [Fact]
    public void GamepadIdleActivityScript_DoesNotForceDisplayAwakeOrCreateSyntheticInput()
    {
        var scriptPath = Path.Combine(ProjectRoot, "scripts", "show-gamepad-idle-activity.ps1");
        var forbiddenTokens = new[]
        {
            "SetThreadExecutionState",
            "ES_DISPLAY_REQUIRED",
            "ES_SYSTEM_REQUIRED",
            "PowerSetRequest",
            "SendInput",
            "mouse_event",
            "keybd_event",
            "SetCursorPos",
            "PowerWrite",
            "PowerSetActiveScheme",
            "powercfg"
        };

        var source = File.ReadAllText(scriptPath);
        var violations = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.OrdinalIgnoreCase))
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
