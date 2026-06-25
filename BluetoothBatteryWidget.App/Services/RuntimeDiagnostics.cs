using System.IO;
using System.Threading;

namespace BluetoothBatteryWidget.App.Services;

internal static class RuntimeDiagnostics
{
    private static int _fileLoggingEnabled = 1;

    public static bool IsFileLoggingEnabled => Volatile.Read(ref _fileLoggingEnabled) == 1;

    public static void ConfigureForProcess(string? processPath)
    {
        SetFileLoggingEnabled(!IsPortableTestExecutablePath(processPath));
    }

    public static void SetFileLoggingEnabled(bool isEnabled)
    {
        Volatile.Write(ref _fileLoggingEnabled, isEnabled ? 1 : 0);
    }

    internal static bool IsPortableTestExecutablePath(string? processPath)
    {
        return string.Equals(
            Path.GetFileName(processPath),
            "test.exe",
            StringComparison.OrdinalIgnoreCase);
    }
}
