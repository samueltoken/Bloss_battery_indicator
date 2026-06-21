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

    [Fact]
    public void ShowGamepadIdleActivityScript_WatchesIdleAndGamepadActivity()
    {
        var script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "scripts",
            "show-gamepad-idle-activity.ps1"));

        Assert.Contains("GetLastInputInfo", script);
        Assert.Contains("\"hid_button_input\"", script);
        Assert.Contains("\"hid_button_activity\"", script);
        Assert.Contains("\"xinput_button_input\"", script);
        Assert.Contains("\"xinput_stick_input\"", script);
        Assert.Contains("\"xinput_stick_telemetry\"", script);
        Assert.Contains("\"display_on_fallback_sent\"", script);
        Assert.Contains("\"display_on_fallback_failed\"", script);
        Assert.Contains("\"wake_recovery_bypass_armed\"", script);
        Assert.Contains("\"guide_toast_deferred_until_display_wake\"", script);
        Assert.DoesNotContain("\"hid_input_activity\"", script);
        Assert.DoesNotContain("\"hid_state_activity\"", script);
        Assert.DoesNotContain("\"steam_raw_input_activity\"", script);
        Assert.DoesNotContain("\"raw_mouse_move_seen\"", script);
        Assert.DoesNotContain("\"raw_keyboard_seen\"", script);
        Assert.DoesNotContain("\"raw_mouse_button_seen\"", script);
        Assert.DoesNotContain("\"raw_hid_unparsed\"", script);
        Assert.DoesNotContain("\"xinput_activity\"", script);
        Assert.Contains("\"automatic_battery_toast_shown\"", script);
        Assert.Contains("[switch]$RequireActivity", script);
        Assert.Contains("[switch]$RequirePopup", script);
        Assert.Contains("[switch]$RequireAutoAlert", script);
        Assert.Contains("Do not touch the mouse or keyboard", script);
        Assert.Contains("power-idle-debug.log", script);
        Assert.Contains("Convert-PowerIdleDebugLine", script);
        Assert.Contains("Get-LatestPowerIdleState", script);
        Assert.Contains("RawInputMode", script);
        Assert.Contains("XInputMode", script);
        Assert.Contains("NormalMonitorRemaining", script);
        Assert.Contains("GuideInitialPressedAllowed", script);
        Assert.Contains("normalLeft=", script);
        Assert.Contains("guideInitial=", script);
        Assert.Contains("wake={7}", script);
        Assert.Contains("mode={2}", script);
        Assert.Contains("monitors={3}", script);
        Assert.Contains("Latest power idle state:", script);
    }
}
