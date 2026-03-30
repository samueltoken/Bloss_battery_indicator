using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class WidgetSettingsColorPresetTests
{
    [Fact]
    public void NormalizeColorPresetId_InvalidValue_FallsBackToWhiteBlue()
    {
        var normalized = WidgetSettings.NormalizeColorPresetId("invalid-theme");

        Assert.Equal(WidgetSettings.WhiteBluePreset, normalized);
    }

    [Fact]
    public void NormalizeColorPresetId_AquaClassicAlias_MapsToWhiteBlue()
    {
        var normalized = WidgetSettings.NormalizeColorPresetId(WidgetSettings.AquaClassicPreset);

        Assert.Equal(WidgetSettings.WhiteBluePreset, normalized);
    }

    [Fact]
    public void NormalizeColorPresetId_ValidValue_IsPreserved()
    {
        var normalized = WidgetSettings.NormalizeColorPresetId(WidgetSettings.CrimsonRedPreset);

        Assert.Equal(WidgetSettings.CrimsonRedPreset, normalized);
    }

    [Fact]
    public void NormalizeColorPresetId_NewPresetValues_ArePreserved()
    {
        var values = new[]
        {
            WidgetSettings.BurgundyPreset,
            WidgetSettings.BlackTonePreset,
            WidgetSettings.DeepGreenPreset,
            WidgetSettings.CobaltBluePreset,
            WidgetSettings.DeepBlueSeaPreset,
            WidgetSettings.AblRedPreset,
            WidgetSettings.GrassGreenPreset,
            WidgetSettings.BurgundyRedPreset,
            WidgetSettings.DawnDarkPreset,
            WidgetSettings.CyberDarkPreset
        };

        foreach (var value in values)
        {
            Assert.Equal(value, WidgetSettings.NormalizeColorPresetId(value));
        }
    }

    [Fact]
    public void GuidedProbeEnabled_DefaultsToFalse()
    {
        var settings = new WidgetSettings();

        Assert.False(settings.GuidedProbeEnabled);
    }

    [Fact]
    public void StatusPanelCollapsed_DefaultsToFalse()
    {
        var settings = new WidgetSettings();

        Assert.False(settings.StatusPanelCollapsed);
    }

    [Fact]
    public void UiScaleStep_DefaultsToZero()
    {
        var settings = new WidgetSettings();

        Assert.Equal(0, settings.UiScaleStep);
    }

    [Fact]
    public void Language_DefaultsToKorean()
    {
        var settings = new WidgetSettings();

        Assert.Equal(WidgetSettings.KoreanLanguage, settings.Language);
    }

    [Theory]
    [InlineData(WidgetSettings.KoreanLanguage)]
    [InlineData(WidgetSettings.EnglishLanguage)]
    [InlineData(WidgetSettings.JapaneseLanguage)]
    [InlineData(WidgetSettings.ChineseSimplifiedLanguage)]
    [InlineData(WidgetSettings.ChineseTraditionalLanguage)]
    [InlineData(WidgetSettings.LatinLanguage)]
    [InlineData(WidgetSettings.FrenchLanguage)]
    public void NormalizeLanguage_ValidValues_ArePreserved(string language)
    {
        Assert.Equal(language, WidgetSettings.NormalizeLanguage(language));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-language")]
    public void NormalizeLanguage_InvalidValues_FallBackToKorean(string? language)
    {
        Assert.Equal(WidgetSettings.KoreanLanguage, WidgetSettings.NormalizeLanguage(language));
    }

    [Theory]
    [InlineData(-99, -5)]
    [InlineData(-5, -5)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(3, 0)]
    [InlineData(10, 0)]
    public void NormalizeUiScaleStep_ClampsToRange(int input, int expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeUiScaleStep(input));
    }

    [Fact]
    public void ThirdPartyBatteryPolicy_DefaultsToAggressive()
    {
        var settings = new WidgetSettings();

        Assert.Equal(ThirdPartyBatteryPolicy.Aggressive, settings.ThirdPartyBatteryPolicy);
    }

    [Fact]
    public void GamepadDisconnectGraceSeconds_DefaultsToExpectedValue()
    {
        var settings = new WidgetSettings();

        Assert.Equal(
            WidgetSettings.DefaultGamepadDisconnectGraceSeconds,
            settings.GamepadDisconnectGraceSeconds);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(10, 10)]
    [InlineData(70, 70)]
    [InlineData(500, 180)]
    public void NormalizeGamepadDisconnectGraceSeconds_ClampsRange(int input, int expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeGamepadDisconnectGraceSeconds(input));
    }

    [Fact]
    public void BatteryHoldSeconds_DefaultsToTenMinutes()
    {
        var settings = new WidgetSettings();

        Assert.Equal(WidgetSettings.DefaultBatteryHoldSeconds, settings.BatteryHoldSeconds);
        Assert.Equal(600, settings.BatteryHoldSeconds);
    }

    [Fact]
    public void NameOverrides_DefaultsToEmptyDictionary()
    {
        var settings = new WidgetSettings();

        Assert.NotNull(settings.NameOverrides);
        Assert.Empty(settings.NameOverrides);
    }

    [Fact]
    public void IconImageOverrides_DefaultsToEmptyDictionary()
    {
        var settings = new WidgetSettings();

        Assert.NotNull(settings.IconImageOverrides);
        Assert.Empty(settings.IconImageOverrides);
    }

    [Theory]
    [InlineData(-1, 600)]
    [InlineData(0, 600)]
    [InlineData(59, 60)]
    [InlineData(600, 600)]
    [InlineData(5000, 1800)]
    public void NormalizeBatteryHoldSeconds_ClampsRange(int input, int expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeBatteryHoldSeconds(input));
    }
}
