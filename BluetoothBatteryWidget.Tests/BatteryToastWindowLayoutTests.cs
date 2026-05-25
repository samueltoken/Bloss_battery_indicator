namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryToastWindowLayoutTests
{
    [Fact]
    public void ToastWindow_HasEnoughHeightForSubtitleAndRoundedCorners()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App",
            "BatteryToastWindow.cs"));

        Assert.Contains("Height = 370", source);
        Assert.Contains("CornerRadius = new CornerRadius(30)", source);
        Assert.Contains("Clip = new RectangleGeometry(new Rect(0, 0, 320, 370), 30, 30)", source);
        Assert.Contains("new GridLength(182)", source);
        Assert.Contains("new GridLength(44)", source);
        Assert.Contains("Interval = TimeSpan.FromSeconds(3.8)", source);
        Assert.Contains("_closeTimer.Start();", source);
        Assert.Contains("Close();", source);
    }
}
