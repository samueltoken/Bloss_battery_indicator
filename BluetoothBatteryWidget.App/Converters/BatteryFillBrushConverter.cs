using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BluetoothBatteryWidget.App.Converters;

public sealed class BatteryFillBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush NotAvailableBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(120, 120, 120));

    private static readonly System.Windows.Media.Color MinColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5A5A");
    private static readonly System.Windows.Media.Color MaxColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#34C759");

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int percentage)
        {
            return NotAvailableBrush;
        }

        percentage = Math.Clamp(percentage, 0, 100);
        var ratio = percentage / 100.0;

        var r = (byte)(MinColor.R + ((MaxColor.R - MinColor.R) * ratio));
        var g = (byte)(MinColor.G + ((MaxColor.G - MinColor.G) * ratio));
        var b = (byte)(MinColor.B + ((MaxColor.B - MinColor.B) * ratio));

        return CreateFrozenBrush(System.Windows.Media.Color.FromRgb(r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush CreateFrozenBrush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
