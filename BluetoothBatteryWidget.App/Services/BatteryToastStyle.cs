using System.Windows.Media;
using BluetoothBatteryWidget.Core.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;

namespace BluetoothBatteryWidget.App.Services;

internal enum BatteryToastSeverity
{
    Full,
    Draining,
    NeedsCharge,
    ChargeNow
}

internal static class BatteryToastStyle
{
    public static BatteryToastSeverity ResolveSeverity(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent >= 80)
        {
            return BatteryToastSeverity.Full;
        }

        if (percent >= 60)
        {
            return BatteryToastSeverity.Draining;
        }

        if (percent >= 30)
        {
            return BatteryToastSeverity.NeedsCharge;
        }

        return BatteryToastSeverity.ChargeNow;
    }

    public static WpfBrush ResolveAccentBrush(BatteryToastSeverity severity)
    {
        return severity switch
        {
            BatteryToastSeverity.ChargeNow => new SolidColorBrush(WpfColor.FromRgb(232, 38, 38)),
            BatteryToastSeverity.NeedsCharge => new SolidColorBrush(WpfColor.FromRgb(242, 181, 55)),
            BatteryToastSeverity.Draining => new SolidColorBrush(WpfColor.FromRgb(45, 190, 114)),
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

        return ResolveSeverity(snapshot.BatteryPercent ?? 0) switch
        {
            BatteryToastSeverity.Full => "Battery enough",
            BatteryToastSeverity.Draining => "Battery draining",
            BatteryToastSeverity.NeedsCharge => "Charging needed",
            _ => "Charge now"
        };
    }
}
