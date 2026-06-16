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

    [Fact]
    public void ShowGuideButtonEventsScript_HasCustomTriggerCheckView()
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

        Assert.Contains("[switch]$CustomTriggerCheck", script);
        Assert.Contains("\"custom_trigger_capture_candidate\"", script);
        Assert.Contains("\"custom_trigger_toast_shown\"", script);
        Assert.Contains("\"custom_trigger_duplicate_suppressed\"", script);
        Assert.Contains("\"secondary_fallback_custom_trigger_suppressed\"", script);
        Assert.Contains("custom notification-button", script);
        Assert.Contains("Select-GuideButtonEventPublicFields", script);
        Assert.Contains("Detail = if ($_.Event -like \"custom_trigger_*\")", script);
        Assert.Contains("Format-Table -AutoSize -Wrap", script);
        Assert.DoesNotContain("Format-Table -AutoSize\r\n", script);
    }
}
