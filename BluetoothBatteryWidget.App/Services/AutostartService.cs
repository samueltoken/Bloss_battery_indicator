using Microsoft.Win32;
using System.IO;

namespace BluetoothBatteryWidget.App.Services;

public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Bloss";
    private const string LegacyRunValueName = "BluetoothBatteryWidget";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var currentValue = key?.GetValue(RunValueName) as string;
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                return true;
            }

            var legacyValue = key?.GetValue(LegacyRunValueName) as string;
            return !string.IsNullOrWhiteSpace(legacyValue);
        }
        catch
        {
            return false;
        }
    }

    public void Apply(bool enabled, bool startMinimizedToTray = false)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var launchCommand = ResolveLaunchCommand(startMinimizedToTray);
                if (string.IsNullOrWhiteSpace(launchCommand))
                {
                    return;
                }

                key.SetValue(RunValueName, launchCommand);
                key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
                key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // intentionally ignored; autostart preference is best-effort.
        }
    }

    private static string? ResolveLaunchCommand(bool startMinimizedToTray)
    {
        if (IsPortableTestExecutablePath(Environment.ProcessPath))
        {
            return null;
        }

        var startupArgument = startMinimizedToTray ? " --start-in-tray" : string.Empty;
        var blossExePath = Path.Combine(AppContext.BaseDirectory, "Bloss.exe");
        if (File.Exists(blossExePath))
        {
            return $"\"{blossExePath}\"{startupArgument}";
        }

        var legacyExePath = Path.Combine(AppContext.BaseDirectory, "BluetoothBatteryWidget.App.exe");
        if (File.Exists(legacyExePath))
        {
            return $"\"{legacyExePath}\"{startupArgument}";
        }

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        if (processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var blossDllPath = Path.Combine(AppContext.BaseDirectory, "Bloss.dll");
            if (File.Exists(blossDllPath))
            {
                return $"\"{processPath}\" \"{blossDllPath}\"{startupArgument}";
            }

            var legacyDllPath = Path.Combine(AppContext.BaseDirectory, "BluetoothBatteryWidget.App.dll");
            if (File.Exists(legacyDllPath))
            {
                return $"\"{processPath}\" \"{legacyDllPath}\"{startupArgument}";
            }
        }

        return $"\"{processPath}\"{startupArgument}";
    }

    internal static bool IsPortableTestExecutablePath(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        return string.Equals(
            Path.GetFileName(processPath),
            "test.exe",
            StringComparison.OrdinalIgnoreCase);
    }
}
