namespace BluetoothBatteryWidget.Tests;

public sealed class SettingsColorCustomizationTests
{
    [Fact]
    public void CustomColorApplication_DoesNotRecolorSettingsHelpText()
    {
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("SetResourceColor(\"SettingsTitleBrush\", preset.PrimaryText)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTextBrush\", preset.SecondaryText)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTitleBrush\", text)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTextBrush\", secondaryText)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTitleBrush\", opaqueColor)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTextBrush\", opaqueColor)", source);
    }

    [Fact]
    public void PopupChrome_ClaimsPreviewMouseDownBeforeMainWindowDrag()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"SettingsPopupChrome\"", xaml);
        Assert.Contains("x:Name=\"ColorPopupChrome\"", xaml);
        Assert.Contains("PreviewMouseLeftButtonDown=\"PopupChrome_MouseLeftButtonDown\"", xaml);
    }

    [Fact]
    public void CustomColors_DisableChargingCardColorOverride()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("DataContext.UseCustomColors", xaml);
        Assert.Contains("Value=\"False\"", xaml);
    }

    [Fact]
    public void PresetComboBox_UsesTransparentToggleOverlay()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("ComboTransparentToggleButtonStyle", xaml);
        Assert.Contains("Style=\"{StaticResource ComboTransparentToggleButtonStyle}\"", xaml);
    }

    [Fact]
    public void ColorElementButtons_RespectStretchAlignment()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("HorizontalAlignment=\"{TemplateBinding HorizontalContentAlignment}\"", xaml);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\" />", xaml);
    }

    [Fact]
    public void DeviceCardCriticalColors_UseDynamicResources()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("Value=\"{DynamicResource CardTintBrush}\"", xaml);
        Assert.Contains("Value=\"{DynamicResource CardBorderBrush}\"", xaml);
        Assert.Contains("Value=\"{DynamicResource BatteryTextBrush}\"", xaml);
        Assert.DoesNotContain("Value=\"{StaticResource CardTintBrush}\"", xaml);
        Assert.DoesNotContain("Value=\"{StaticResource CardBorderBrush}\"", xaml);
        Assert.DoesNotContain("Value=\"{StaticResource BatteryTextBrush}\"", xaml);
    }

    [Fact]
    public void PresetComboBox_ClickCanResetCustomColorsBeforeSelectionChanges()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));

        Assert.Contains("PreviewMouseLeftButtonDown=\"ColorPresetComboBox_PreviewMouseLeftButtonDown\"", xaml);
        Assert.Contains("ColorPresetComboBoxItem_PreviewMouseLeftButtonDown", source);
        Assert.Contains("_viewModel.UseCustomColors", source);
    }

    [Fact]
    public void GlassBackground_HasLayeredTextureStops()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"GlassTextureLayer\"", xaml);
        Assert.Contains("x:Name=\"GlassSheenTop\"", xaml);
        Assert.Contains("x:Name=\"GlassDepthBottom\"", xaml);
    }

    private static string FindSourceFile(params string[] pathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not find source file: {Path.Combine(pathParts)}");
    }
}
