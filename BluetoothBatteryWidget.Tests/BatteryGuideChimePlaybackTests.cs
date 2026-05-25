namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryGuideChimePlaybackTests
{
    [Fact]
    public void MainWindow_BindsChimePlaybackToToastLifecycle()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("RestartBatteryGuideChime();", source);
        Assert.Contains("StopBatteryGuideChime();", source);
        Assert.DoesNotContain("PlaySync", source);
        Assert.DoesNotContain("Task.Run(() =>", source);
    }

    [Fact]
    public void ChimePlayer_RestartsSoundInsteadOfQueueingIt()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "Services",
            "BatteryGuideChimePlayer.cs"));

        Assert.Contains("StopLocked();", source);
        Assert.Contains("_player.Play();", source);
        Assert.DoesNotContain("PlaySync", source);
    }
}
