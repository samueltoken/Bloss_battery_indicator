using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class DeviceCategoryClassifier
{
    public static DeviceCategory Classify(string displayName, string? categoryHint)
    {
        var source = $"{displayName} {categoryHint}".ToLowerInvariant();

        if (ContainsAny(source, "mouse"))
        {
            return DeviceCategory.Mouse;
        }

        if (ContainsAny(source, "keyboard", "키보드"))
        {
            return DeviceCategory.Keyboard;
        }

        if (ContainsAny(source, "headset", "headphone", "헤드셋", "헤드폰"))
        {
            return DeviceCategory.Headset;
        }

        if (ContainsAny(source, "earbud", "buds", "airpods", "이어", "버즈"))
        {
            return DeviceCategory.Earbuds;
        }

        if (ContainsAny(source, "speaker", "스피커"))
        {
            return DeviceCategory.Speaker;
        }

        if (ContainsAny(
                source,
                "controller",
                "gamepad",
                "xbox",
                "dualshock",
                "gamesir",
                "game sir",
                "gulikit",
                "flydigi",
                "easysmx",
                "easy smx",
                "게임패드"))
        {
            return DeviceCategory.Gamepad;
        }

        if (ContainsAny(source, "phone", "iphone", "galaxy"))
        {
            return DeviceCategory.Phone;
        }

        if (ContainsAny(source, "tablet", "ipad"))
        {
            return DeviceCategory.Tablet;
        }

        if (ContainsAny(source, "laptop", "notebook"))
        {
            return DeviceCategory.Laptop;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return DeviceCategory.Unknown;
        }

        return DeviceCategory.Other;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        foreach (var value in values)
        {
            if (source.Contains(value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
