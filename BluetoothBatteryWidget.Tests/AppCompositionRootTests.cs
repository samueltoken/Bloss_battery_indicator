namespace BluetoothBatteryWidget.Tests;

public sealed class AppCompositionRootTests
{
    [Fact]
    public void AppStartup_DelegatesServiceAssemblyToCompositionRoot()
    {
        var appRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App"));
        var appSource = File.ReadAllText(Path.Combine(appRoot, "App.xaml.cs"));
        var compositionSource = File.ReadAllText(Path.Combine(appRoot, "AppCompositionRoot.cs"));

        Assert.Contains("AppCompositionRoot.CreateMainViewModel()", appSource);
        Assert.DoesNotContain("new CompositeConnectedDeviceProvider", appSource);
        Assert.DoesNotContain("new CompositeBatteryLevelProvider", appSource);
        Assert.DoesNotContain("new MainViewModel", appSource);
        Assert.Contains("new CompositeConnectedDeviceProvider", compositionSource);
        Assert.Contains("new MainViewModel", compositionSource);
    }
}
