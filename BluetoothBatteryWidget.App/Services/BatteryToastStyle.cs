using System.Windows.Media;
using BluetoothBatteryWidget.Core.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;

namespace BluetoothBatteryWidget.App.Services;

internal enum BatteryToastSeverity
{
    Healthy,
    Warning,
    Low
}

internal static class BatteryToastStyle
{
    public static BatteryToastSeverity ResolveSeverity(int percent)
    {
        if (percent < 30)
        {
            return BatteryToastSeverity.Low;
        }

        if (percent < 60)
        {
            return BatteryToastSeverity.Warning;
        }

        return BatteryToastSeverity.Healthy;
    }

    public static WpfBrush ResolveAccentBrush(BatteryToastSeverity severity)
    {
        return severity switch
        {
            BatteryToastSeverity.Low => new SolidColorBrush(WpfColor.FromRgb(232, 38, 38)),
            BatteryToastSeverity.Warning => new SolidColorBrush(WpfColor.FromRgb(242, 181, 55)),
            _ => new SolidColorBrush(WpfColor.FromRgb(47, 123, 234))
        };
    }

    public static string BuildSubtitle(DeviceBatterySnapshot snapshot, bool automatic)
    {
        if (snapshot.IsChargeComplete)
        {
            return "Charged";
        }

        if (snapshot.IsCharging)
        {
            return "Charging";
        }

        var percent = snapshot.BatteryPercent ?? 0;
        if (automatic || percent < 30)
        {
            return "Low Battery";
        }

        if (percent < 60)
        {
            return "Battery Notice";
        }

        return "Battery OK";
    }
}
