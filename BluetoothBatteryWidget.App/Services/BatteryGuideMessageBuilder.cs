using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Services;

internal static class BatteryGuideMessageBuilder
{
    public static string Build(DeviceBatterySnapshot snapshot, string language)
    {
        var name = string.IsNullOrWhiteSpace(snapshot.DisplayName)
            ? ExtraText(language, "BatteryGuideConnectedDevice")
            : snapshot.DisplayName.Trim();

        if (snapshot.BatteryPercent is null)
        {
            return ExtraFormat(language, "BatteryGuideReadingFormat", name);
        }

        var percent = Math.Clamp(snapshot.BatteryPercent.Value, 0, 100);
        if (snapshot.IsChargeComplete)
        {
            return ExtraFormat(language, "BatteryGuideChargedFormat", name, percent);
        }

        if (snapshot.IsCharging)
        {
            return ExtraFormat(language, "BatteryGuideChargingFormat", name, percent);
        }

        var estimatedHours = EstimateRuntimeHours(snapshot, percent);
        if (estimatedHours is null)
        {
            return ExtraFormat(language, "BatteryGuidePercentFormat", name, percent);
        }

        return ExtraFormat(language, "BatteryGuideEstimatedFormat", name, percent, FormatDuration(language, estimatedHours.Value));
    }

    public static string BuildToastSubtitle(DeviceBatterySnapshot snapshot, string language, bool automatic)
    {
        if (snapshot.IsChargeComplete)
        {
            return ExtraText(language, "BatteryGuideSubtitleCharged");
        }

        if (snapshot.IsCharging)
        {
            return ExtraText(language, "BatteryGuideSubtitleCharging");
        }

        var percent = Math.Clamp(snapshot.BatteryPercent ?? 0, 0, 100);
        return percent switch
        {
            >= 80 => ExtraText(language, "BatteryGuideSubtitleOk"),
            >= 60 => ExtraText(language, "BatteryGuideSubtitleNotice"),
            >= 30 => ExtraText(language, "BatteryGuideSubtitleCheck"),
            _ => ExtraText(language, "BatteryGuideSubtitleLow")
        };
    }

    private static double? EstimateRuntimeHours(DeviceBatterySnapshot snapshot, int percent)
    {
        var fullRuntimeHours = ResolveFullRuntimeHours(snapshot);
        if (fullRuntimeHours is null)
        {
            return null;
        }

        return Math.Max(0.25d, fullRuntimeHours.Value * percent / 100d);
    }

    private static double? ResolveFullRuntimeHours(DeviceBatterySnapshot snapshot)
    {
        var text = $"{snapshot.DisplayName} {snapshot.ModelKey}".ToUpperInvariant();
        if (text.Contains("DUALSENSE", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("VID_054C", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("VID_054C|PID_0CE6", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("VID_054C|PID_0DF2", StringComparison.OrdinalIgnoreCase))
        {
            return 8d;
        }

        if (text.Contains("STEAM CONTROLLER", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("STEAM CTRL", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("VID_28DE", StringComparison.OrdinalIgnoreCase))
        {
            return 20d;
        }

        return null;
    }

    private static string ExtraText(string language, string key)
    {
        return UiLanguageCatalog.GetExtraText(language, key);
    }

    private static string ExtraFormat(string language, string key, params object[] args)
    {
        return string.Format(ExtraText(language, key), args);
    }

    private static string FormatDuration(string language, double hours)
    {
        if (hours < 0.75d)
        {
            return ExtraText(language, "BatteryGuideDurationHalfHour");
        }

        var rounded = Math.Max(1, (int)Math.Round(hours, MidpointRounding.AwayFromZero));
        return rounded == 1
            ? ExtraText(language, "BatteryGuideDurationOneHour")
            : ExtraFormat(language, "BatteryGuideDurationHoursFormat", rounded);
    }
}
