using System.Globalization;
using System.IO;

namespace BluetoothBatteryWidget.App.Services;

internal static class PowerIdleDebugLog
{
    internal const long MaxLogBytes = 2L * 1024L * 1024L;
    internal const long KeepLogBytes = 1L * 1024L * 1024L;
    private static readonly object Sync = new();
    private static readonly string EventsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "power-idle-debug.log");

    public static void Write(
        string mode,
        TimeSpan? delay,
        TimeSpan? displayTimeout,
        TimeSpan systemIdle,
        TimeSpan localIdle,
        TimeSpan gamepadIdle,
        bool shouldPause,
        bool isRefreshRunning,
        bool isProbeRunning,
        bool isGuideCaptureActive,
        bool guideRunning,
        bool guidePollingPaused,
        bool guideInitialPressedAllowed,
        bool rawInputRegistered,
        bool xInputRunning,
        string rawInputMode,
        string xInputMode,
        TimeSpan normalMonitorRemaining)
    {
        try
        {
            var line = string.Join(
                '\t',
                DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
                $"mode={mode}",
                $"delay={Format(delay)}",
                $"displayTimeout={Format(displayTimeout)}",
                $"systemIdle={Format(systemIdle)}",
                $"localIdle={Format(localIdle)}",
                $"gamepadIdle={Format(gamepadIdle)}",
                $"shouldPause={shouldPause}",
                $"refresh={isRefreshRunning}",
                $"probe={isProbeRunning}",
                $"guideCapture={isGuideCaptureActive}",
                $"guideRunning={guideRunning}",
                $"guidePollingPaused={guidePollingPaused}",
                $"guideInitialPressedAllowed={guideInitialPressedAllowed}",
                $"rawInputRegistered={rawInputRegistered}",
                $"xInputRunning={xInputRunning}",
                $"rawInputMode={rawInputMode}",
                $"xInputMode={xInputMode}",
                $"normalMonitorRemaining={Format(normalMonitorRemaining)}");

            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EventsPath)!);
                File.AppendAllText(EventsPath, line + Environment.NewLine);
                DiagnosticLogFileTrimmer.TrimIfNeeded(EventsPath, MaxLogBytes, KeepLogBytes);
            }
        }
        catch
        {
            // Diagnostics must never affect power or guide-button behavior.
        }
    }

    private static string Format(TimeSpan? value)
    {
        return value is null
            ? "null"
            : Format(value.Value);
    }

    private static string Format(TimeSpan value)
    {
        return value == TimeSpan.MaxValue
            ? "max"
            : Math.Round(value.TotalSeconds, 1).ToString(CultureInfo.InvariantCulture) + "s";
    }
}
