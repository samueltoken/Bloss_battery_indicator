namespace BluetoothBatteryWidget.Tests;

public sealed class SettingsColorCustomizationTests
{
    [Fact]
    public void CustomColorApplication_OnlyDedicatedSettingsTextTargetRecolorsSettingsHelpText()
    {
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("SetResourceColor(\"SettingsTitleBrush\", preset.PrimaryText)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTextBrush\", preset.SecondaryText)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTitleBrush\", text)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTextBrush\", secondaryText)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTitleBrush\", opaqueColor)", source);
        Assert.DoesNotContain("SetResourceColor(\"SettingsTextBrush\", opaqueColor)", source);
        Assert.Contains("TryGetValue(\"SettingsText\"", source);
        Assert.Contains("ApplyFixedSettingsTextResources", source);
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

    [Fact]
    public void ColorCustomization_ExposesWidgetBackgroundElement()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Key=\"WidgetBackgroundBrush\"", xaml);
        Assert.Contains("x:Name=\"WidgetBackgroundColorButton\"", xaml);
        Assert.Contains("Tag=\"WidgetBackground\"", xaml);
        Assert.Contains("x:Name=\"WidgetBackgroundSwatch\"", xaml);
        Assert.Contains("\"WidgetBackground\" => \"WidgetBackgroundBrush\"", source);
        Assert.Contains("ApplyWidgetBackgroundColor", source);
        Assert.Contains("SetSwatch(WidgetBackgroundSwatch, \"WidgetBackgroundBrush\")", source);
        Assert.Contains("SetColorElementButtonState(WidgetBackgroundColorButton, \"WidgetBackground\")", source);
    }

    [Fact]
    public void ColorCustomization_ExposesGlassSurfaceElement()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Key=\"GlassSurfaceBrush\"", xaml);
        Assert.Contains("x:Name=\"GlassSurfaceColorButton\"", xaml);
        Assert.Contains("Tag=\"GlassSurface\"", xaml);
        Assert.Contains("x:Name=\"GlassSurfaceSwatch\"", xaml);
        Assert.Contains("Background=\"{DynamicResource GlassSurfaceBrush}\"", xaml);
        Assert.Contains("ApplyGlassSurfaceColor", source);
        Assert.Contains("SetSwatch(GlassSurfaceSwatch, \"GlassSurfaceBrush\")", source);
        Assert.Contains("SetColorElementButtonState(GlassSurfaceColorButton, \"GlassSurface\")", source);
        Assert.Contains("\"GlassSurface\" => \"GlassSurfaceBrush\"", source);
    }

    [Fact]
    public void ColorCustomization_UsesLocalizedLabels()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));
        var viewModel = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "ViewModels", "MainViewModel.cs"));
        var languageCatalog = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "Services", "UiLanguageCatalog.cs"));

        Assert.Contains("Text=\"{Binding TextColorTargetPrimaryText}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetSecondaryText}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetBatteryText}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetWidgetBackground}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetGlassSurface}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetCardTint}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetCardBorder}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetTrack}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetPanel}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorTargetSettingsText}\"", xaml);
        Assert.Contains("Content=\"{Binding TextColorReset}\"", xaml);
        Assert.Contains("Text=\"{Binding TextColorDragHint}\"", xaml);

        Assert.Contains("GetColorElementLabel", source);
        Assert.DoesNotContain("ColorElementLabels", source);
        Assert.DoesNotContain(": \"전체 글자\"", source);

        Assert.Contains("TextColorTargetGlassSurface", viewModel);
        Assert.Contains("OnPropertyChanged(nameof(TextColorTargetGlassSurface))", viewModel);
        Assert.Contains("TextColorTargetSettingsText", viewModel);
        Assert.Contains("OnPropertyChanged(nameof(TextColorTargetSettingsText))", viewModel);
        Assert.Contains("\"ColorGlassSurface\" => \"Glass surface\"", languageCatalog);
        Assert.Contains("\"ColorSettingsText\" => \"Settings text\"", languageCatalog);
    }

    [Fact]
    public void ColorCustomization_ExposesSettingsTextStyleControls()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));
        var viewModel = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "ViewModels", "MainViewModel.cs"));

        Assert.Contains("x:Name=\"SettingsTextColorButton\"", xaml);
        Assert.Contains("Tag=\"SettingsText\"", xaml);
        Assert.Contains("x:Name=\"SettingsTextSwatch\"", xaml);
        Assert.Contains("x:Name=\"SettingsTextFontSizeSlider\"", xaml);
        Assert.Contains("ValueChanged=\"SettingsTextFontSizeSlider_ValueChanged\"", xaml);
        Assert.Contains("x:Name=\"SettingsTextBoldToggle\"", xaml);
        Assert.Contains("Checked=\"SettingsTextBoldToggle_Checked\"", xaml);
        Assert.Contains("x:Name=\"ResetSettingsTextStyleButton\"", xaml);
        Assert.Contains("MinWidth=\"128\"", xaml);
        Assert.Contains("Height=\"36\"", xaml);
        Assert.Contains("Click=\"ResetSettingsTextStyleButton_Click\"", xaml);

        Assert.Contains("SetSettingsTextFontSize", source);
        Assert.Contains("SetSettingsTextBold", source);
        Assert.Contains("ClearSettingsTextStyle", source);
        Assert.Contains("ApplySettingsTextStyle", source);
        Assert.Contains("IsSettingsTextStyleExcludedControl", source);
        Assert.Contains("control is WpfControls.ComboBox or WpfControls.ComboBoxItem", source);
        Assert.Contains("IsInsideSettingsTextStyleExcludedControl(textBlock)", source);

        Assert.Contains("UseCustomSettingsTextStyle", viewModel);
        Assert.Contains("SettingsTextFontSize", viewModel);
        Assert.Contains("SettingsTextBold", viewModel);
    }

    [Fact]
    public void ColorPresetNameOnly_ScrollsOnHover()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"ColorPresetMarqueeViewport\"", xaml);
        Assert.Contains("x:Name=\"ColorPresetMarqueeText\"", xaml);
        Assert.Contains("<Grid Width=\"124\">", xaml);
        Assert.Contains("<Grid Width=\"30\"", xaml);
        Assert.Contains("Margin=\"0,0,6,0\"", xaml);
        Assert.Contains("MouseEnter=\"ColorPresetMarqueeViewport_MouseEnter\"", xaml);
        Assert.Contains("MouseLeave=\"ColorPresetMarqueeViewport_MouseLeave\"", xaml);
        Assert.DoesNotContain("MouseEnter=\"ColorPresetComboBox_MouseEnter\"", xaml);
        Assert.DoesNotContain("MouseLeave=\"ColorPresetComboBox_MouseLeave\"", xaml);
    }

    [Fact]
    public void ColorPalette_ExposesBlackQuickSwatchAndMoreRoom()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));
        var source = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml.cs"));

        Assert.Contains("Width=\"430\"", xaml);
        Assert.Contains("x:Name=\"PaletteSurface\"", xaml);
        Assert.Contains("Height=\"196\"", xaml);
        Assert.Contains("x:Name=\"ColorQuickSwatchPanel\"", xaml);
        Assert.Contains("Tag=\"#000000\"", xaml);
        Assert.Contains("Background=\"#FF000000\"", xaml);
        Assert.Contains("Click=\"ColorQuickSwatchButton_Click\"", xaml);
        Assert.Contains("private void ColorQuickSwatchButton_Click", source);
        Assert.Contains("ApplySelectedQuickColor", source);
        Assert.Contains("var darkFade", source);
    }

    [Fact]
    public void SettingsSizeValues_UseStableAlignedBadges()
    {
        var xaml = File.ReadAllText(FindSourceFile("BluetoothBatteryWidget.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"UiScaleValueBadge\"", xaml);
        Assert.Contains("x:Name=\"SettingsTextFontSizeValueBadge\"", xaml);
        Assert.Contains("Grid.Column=\"1\"", xaml);
        Assert.Contains("Width=\"42\"", xaml);
        Assert.Contains("TextAlignment=\"Center\"", xaml);
        Assert.Contains("Margin=\"0,0,10,0\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Right\"", xaml);
        Assert.Contains("Grid.ColumnSpan=\"2\"", xaml);
        Assert.Contains("Grid.ColumnSpan=\"3\"", xaml);
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
