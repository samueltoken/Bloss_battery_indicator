using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class ColorPresetCatalogTests
{
    [Fact]
    public void Presets_ContainExpectedEightUniqueValues()
    {
        var expected = new[]
        {
            WidgetSettings.WhiteBluePreset,
            WidgetSettings.CloudDancerPreset,
            WidgetSettings.MoonLavenderPreset,
            WidgetSettings.MistSagePreset,
            WidgetSettings.AuroraTealPreset,
            WidgetSettings.RoseDuskPreset,
            WidgetSettings.DeepNavyPreset,
            WidgetSettings.GraphiteBloomPreset
        };

        Assert.Equal(expected, ColorPresetCatalog.Presets.Select(preset => preset.Id));
        Assert.Equal(8, ColorPresetCatalog.Presets.Select(preset => preset.Id).Distinct().Count());
    }

    [Fact]
    public void WhiteBluePreset_KeepsLegacyBrightValues()
    {
        var preset = ColorPresetCatalog.GetById(WidgetSettings.WhiteBluePreset);

        Assert.Equal(ColorFrom("#10243C"), preset.PrimaryText);
        Assert.Equal(ColorFrom("#314E6A"), preset.SecondaryText);
        Assert.Equal(ColorFrom("#132F4D"), preset.BatteryText);
        Assert.Equal(ColorFrom("#C2FFFFFF"), preset.CardTint);
        Assert.Equal(ColorFrom("#93FFFFFF"), preset.CardBorder);
        Assert.Equal(ColorFrom("#7AB8C8D7"), preset.Track);
        Assert.Equal(ColorFrom("#E7F2F8FC"), preset.IconBack);
        Assert.Equal(ColorFrom("#A3CBDDEE"), preset.IconBorder);
        Assert.Equal(ColorFrom("#C8FFFFFF"), preset.FooterTop);
        Assert.Equal(ColorFrom("#ADECF7FF"), preset.FooterBottom);
        Assert.Equal(ColorFrom("#A7FFFFFF"), preset.ListTop);
        Assert.Equal(ColorFrom("#88ECF5FF"), preset.ListBottom);
        Assert.Equal(ColorFrom("#B5F5FAFF"), preset.ActionButtonBack);
        Assert.Equal(ColorFrom("#9ECAE0EF"), preset.ActionButtonBorder);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Presets_PrimaryText_RemainsReadableOnCard(ColorPreset preset)
    {
        var contrast = ColorPresetCatalog.GetContrastRatio(preset.PrimaryText, preset.CardTint);

        Assert.True(
            contrast >= 3.0d,
            $"{preset.Id} primary text contrast is too low: {contrast:0.00}");
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Presets_SecondaryText_RemainsReadableOnCard(ColorPreset preset)
    {
        var contrast = ColorPresetCatalog.GetContrastRatio(preset.SecondaryText, preset.CardTint);

        Assert.True(
            contrast >= 3.0d,
            $"{preset.Id} secondary text contrast is too low: {contrast:0.00}");
    }

    [Theory]
    [InlineData(WidgetSettings.DeepNavyPreset)]
    [InlineData(WidgetSettings.GraphiteBloomPreset)]
    public void DarkPresets_TextColors_AvoidPlainWhite(string presetId)
    {
        var preset = ColorPresetCatalog.GetById(presetId);

        Assert.False(IsNearWhite(preset.PrimaryText), $"{preset.Label} primary text should not be plain white.");
        Assert.False(IsNearWhite(preset.SecondaryText), $"{preset.Label} secondary text should not be plain white.");
    }

    public static IEnumerable<object[]> Presets()
    {
        return ColorPresetCatalog.Presets.Select(preset => new object[] { preset });
    }

    private static bool IsNearWhite(System.Windows.Media.Color color)
    {
        return color.R >= 240 && color.G >= 240 && color.B >= 240;
    }

    private static System.Windows.Media.Color ColorFrom(string value)
    {
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
    }
}
