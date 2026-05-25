using System.IO;

namespace BluetoothBatteryWidget.App.Services;

internal static class GuideButtonEventLog
{
    private static readonly object Sync = new();
    private static readonly string EventsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "guide-button-events.log");

    public static void Write(
        string eventName,
        string deviceKind,
        string address,
        string displayName,
        string message)
    {
        try
        {
            var line = string.Join(
                '\t',
                DateTimeOffset.Now.ToString("O"),
                GuideButtonLogFormatter.SanitizeField(eventName),
                GuideButtonLogFormatter.SanitizeField(deviceKind),
                GuideButtonLogFormatter.MaskAddress(address),
                GuideButtonLogFormatter.SanitizeField(displayName),
                GuideButtonLogFormatter.SanitizeField(message));

            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EventsPath)!);
                File.AppendAllText(EventsPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never affect battery display or button monitoring.
        }
    }
}
