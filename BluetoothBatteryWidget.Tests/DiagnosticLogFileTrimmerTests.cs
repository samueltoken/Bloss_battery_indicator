using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class DiagnosticLogFileTrimmerTests
{
    [Fact]
    public void TrimIfNeeded_KeepsRecentCompleteLines()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bloss-log-trim-{Guid.NewGuid():N}.log");
        try
        {
            File.WriteAllLines(path, Enumerable.Range(0, 80).Select(index => $"line-{index:D2}"));

            DiagnosticLogFileTrimmer.TrimIfNeeded(path, maxBytes: 120, keepBytes: 80);

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("line-00", text);
            Assert.Contains("line-79", text);
            Assert.False(text.StartsWith("ne-", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void DiagnosticLogs_DefineBoundedFileSizes()
    {
        Assert.True(GuideButtonEventLog.MaxLogBytes > GuideButtonEventLog.KeepLogBytes);
        Assert.True(PowerIdleDebugLog.MaxLogBytes > PowerIdleDebugLog.KeepLogBytes);
    }
}
