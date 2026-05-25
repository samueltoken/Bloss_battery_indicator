using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Services;

internal static class BatteryGuideMessageBuilder
{
    public static string Build(DeviceBatterySnapshot snapshot, string language)
    {
        var isKorean = IsKorean(language);
        var name = string.IsNullOrWhiteSpace(snapshot.DisplayName)
            ? (isKorean ? "연결된 기기" : "Connected device")
            : snapshot.DisplayName.Trim();

        if (snapshot.BatteryPercent is null)
        {
            return isKorean
                ? $"{name}: 배터리 정보를 읽는 중입니다."
                : $"{name}: reading battery.";
        }

        var percent = Math.Clamp(snapshot.BatteryPercent.Value, 0, 100);
        if (snapshot.IsChargeComplete)
        {
            return isKorean
                ? $"{name}: 충전 완료 · {percent}%"
                : $"{name}: charged · {percent}%";
        }

        if (snapshot.IsCharging)
        {
            return isKorean
                ? $"{name}: 충전 중 · {percent}%"
                : $"{name}: charging · {percent}%";
        }

        var estimatedHours = EstimateRuntimeHours(snapshot, percent);
        if (estimatedHours is null)
        {
            return isKorean
                ? $"{name}: 현재 배터리 {percent}%"
                : $"{name}: {percent}% battery.";
        }

        return isKorean
            ? $"{name}: {percent}% · 예상 {FormatDurationKorean(estimatedHours.Value)} 남음"
            : $"{name}: {percent}% · about {FormatDurationEnglish(estimatedHours.Value)} left";
    }

    public static string BuildToastSubtitle(DeviceBatterySnapshot snapshot, string language, bool automatic)
    {
        var isKorean = IsKorean(language);
        if (snapshot.IsChargeComplete)
        {
            return isKorean ? "충전 완료" : "Charged";
        }

        if (snapshot.IsCharging)
        {
            return isKorean ? "충전 중" : "Charging";
        }

        var percent = Math.Clamp(snapshot.BatteryPercent ?? 0, 0, 100);
        if (automatic || percent < 30)
        {
            return isKorean ? "배터리 부족" : "Low Battery";
        }

        var estimatedHours = EstimateRuntimeHours(snapshot, percent);
        if (estimatedHours is not null)
        {
            return isKorean
                ? $"예상 {FormatDurationKorean(estimatedHours.Value)} 남음"
                : $"About {FormatDurationEnglish(estimatedHours.Value)} left";
        }

        if (percent < 60)
        {
            return isKorean ? "배터리 확인 필요" : "Battery Notice";
        }

        return isKorean ? "배터리 양호" : "Battery OK";
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

    private static bool IsKorean(string language)
    {
        return string.Equals(language, "ko", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(language, "ko-KR", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDurationKorean(double hours)
    {
        if (hours < 0.75d)
        {
            return "30분";
        }

        var rounded = Math.Max(1, (int)Math.Round(hours, MidpointRounding.AwayFromZero));
        return $"{rounded}시간";
    }

    private static string FormatDurationEnglish(double hours)
    {
        if (hours < 0.75d)
        {
            return "30 min";
        }

        var rounded = Math.Max(1, (int)Math.Round(hours, MidpointRounding.AwayFromZero));
        return rounded == 1 ? "1 hour" : $"{rounded} hours";
    }
}
