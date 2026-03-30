using System.Globalization;
using System.Windows.Data;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Converters;

public sealed class IconGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IconKey icon)
        {
            return "\uE702";
        }

        return icon switch
        {
            IconKey.Mouse => "\uE962",
            IconKey.Keyboard => "\uE765",
            IconKey.Headset => "\uE95B",
            IconKey.Earbuds => "\uEC06",
            IconKey.Speaker => "\uE7F5",
            IconKey.Gamepad => "\uE7FC",
            IconKey.Phone => "\uE8EA",
            IconKey.Tablet => "\uE70A",
            IconKey.Laptop => "\uE770",
            _ => "\uE702"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
