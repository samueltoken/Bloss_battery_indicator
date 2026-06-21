using System.IO;

namespace BluetoothBatteryWidget.App.Services;

internal static class DiagnosticLogFileTrimmer
{
    public static void TrimIfNeeded(string path, long maxBytes, long keepBytes)
    {
        if (string.IsNullOrWhiteSpace(path) || maxBytes <= 0 || keepBytes <= 0)
        {
            return;
        }

        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= maxBytes)
        {
            return;
        }

        var bytesToKeep = Math.Min(file.Length, keepBytes);
        var buffer = new byte[bytesToKeep];
        using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        stream.Seek(-bytesToKeep, SeekOrigin.End);

        var read = 0;
        while (read < buffer.Length)
        {
            var chunk = stream.Read(buffer, read, buffer.Length - read);
            if (chunk <= 0)
            {
                break;
            }

            read += chunk;
        }

        var start = FindFirstCompleteLineStart(buffer, read);
        stream.SetLength(0);
        stream.Position = 0;
        stream.Write(buffer, start, read - start);
    }

    private static int FindFirstCompleteLineStart(byte[] buffer, int length)
    {
        for (var index = 0; index < length; index++)
        {
            if (buffer[index] == (byte)'\n')
            {
                return Math.Min(index + 1, length);
            }
        }

        return 0;
    }
}
