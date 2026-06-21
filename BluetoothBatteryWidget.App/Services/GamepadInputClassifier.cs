namespace BluetoothBatteryWidget.App.Services;

internal sealed record GamepadInputClassification(
    bool CountsAsUserActivity,
    bool IsWakeEligible,
    string EventName,
    string DiagnosticMessage);

internal static class GamepadInputClassifier
{
    public static GamepadInputClassification ClassifyHidActivity(GuideButtonActivityEventArgs activity)
    {
        return new GamepadInputClassification(
            activity.CountsAsUserActivity,
            activity.IsWakeEligible,
            "hid_state_telemetry",
            activity.CountsAsUserActivity
                ? "HID controller state changed as user activity."
                : "HID controller state changed, but it was treated as telemetry for display-sleep safety.");
    }

    public static GamepadInputClassification ClassifySteamRawInputActivity(GuideButtonActivityEventArgs activity)
    {
        return new GamepadInputClassification(
            activity.CountsAsUserActivity,
            activity.IsWakeEligible,
            "steam_raw_input_activity",
            activity.CountsAsUserActivity
                ? "Steam RawInput controller activity treated as intentional input."
                : "Steam RawInput activity changed, but it was treated as telemetry for display-sleep safety.");
    }

    public static GamepadInputClassification ClassifyXInputActivity(GamepadWakeInputEventArgs activity)
    {
        var countsAsUserActivity = activity.CountsAsUserActivity;
        var eventName = countsAsUserActivity
            ? activity.HasStick && !activity.HasButton && !activity.HasTrigger
                ? "xinput_stick_input"
                : "xinput_button_input"
            : activity.HasStick
                ? "xinput_stick_telemetry"
                : "xinput_telemetry";

        return new GamepadInputClassification(
            countsAsUserActivity,
            activity.IsWakeEligible,
            eventName,
            countsAsUserActivity
                ? "Intentional XInput controller input refreshed Bloss local idle tracking."
                : "XInput controller stick/noise activity was treated as telemetry for display-sleep safety.");
    }
}
