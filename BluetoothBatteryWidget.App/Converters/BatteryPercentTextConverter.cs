using System.Globalization;
using System.Windows.Data;
using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.App.Converters;

public sealed class BatteryPercentTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var language = values.Length > 4 && values[4] is string selectedLanguage
            ? selectedLanguage
            : null;
        var localized = UiLanguageCatalog.Get(language);

        var percentage = values.Length > 0 && values[0] is int value
            ? value
            : (int?)null;
        if (percentage is not null)
        {
            return $"{Math.Clamp(percentage.Value, 0, 100)}%";
        }

        var isBatteryConnecting = values.Length > 3 && values[3] is bool connecting && connecting;
        if (isBatteryConnecting)
        {
            return localized.ConnectingText;
        }

        var isConnected = values.Length > 2 && values[2] is bool connected && connected;
        if (isConnected)
        {
            return "N/A";
        }

        return localized.UnsupportedText;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
