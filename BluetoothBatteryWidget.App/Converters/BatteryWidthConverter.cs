using System.Globalization;
using System.Windows.Data;

namespace BluetoothBatteryWidget.App.Converters;

public sealed class BatteryWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not int percentage || values[1] is not double totalWidth || totalWidth <= 0)
        {
            return 0d;
        }

        percentage = Math.Clamp(percentage, 0, 100);
        return totalWidth * percentage / 100.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
