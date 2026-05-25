namespace BluetoothBatteryWidget.Tests;

public sealed class GuideButtonEventScriptTests
{
    [Fact]
    public void ShowGuideButtonEventsScript_HasSteamPowerOffCheckView()
    {
        var script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "build",
            "scripts",
            "show-guide-button-events.ps1"));

        Assert.Contains("[switch]$SteamPowerOffCheck", script);
        Assert.Contains("\"duplicate_press_suppressed\"", script);
        Assert.Contains("\"raw_long_press_secondary_blocked\"", script);
        Assert.Contains("\"raw_hid_long_press_suppressed\"", script);
        Assert.Contains("\"secondary_press_suppressed\"", script);
    }
}
