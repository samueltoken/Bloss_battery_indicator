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
        var normalized = WidgetSettings.NormalizeColorPresetId(WidgetSettings.CloudDancerPreset);

        Assert.Equal(WidgetSettings.CloudDancerPreset, normalized);
    }

    [Fact]
    public void NormalizeColorPresetId_NewPresetValues_ArePreserved()
    {
        var values = new[]
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

        foreach (var value in values)
        {
            Assert.Equal(value, WidgetSettings.NormalizeColorPresetId(value));
        }
    }

    [Theory]
    [InlineData(WidgetSettings.BurgundyPreset, WidgetSettings.RoseDuskPreset)]
    [InlineData(WidgetSettings.CrimsonRedPreset, WidgetSettings.RoseDuskPreset)]
    [InlineData(WidgetSettings.BlackTonePreset, WidgetSettings.GraphiteBloomPreset)]
    [InlineData(WidgetSettings.DeepGreenPreset, WidgetSettings.MistSagePreset)]
    [InlineData(WidgetSettings.CobaltBluePreset, WidgetSettings.DeepNavyPreset)]
    [InlineData(WidgetSettings.DeepBlueSeaPreset, WidgetSettings.DeepNavyPreset)]
    [InlineData(WidgetSettings.AblRedPreset, WidgetSettings.RoseDuskPreset)]
    [InlineData(WidgetSettings.GrassGreenPreset, WidgetSettings.MistSagePreset)]
    [InlineData(WidgetSettings.BurgundyRedPreset, WidgetSettings.RoseDuskPreset)]
    [InlineData(WidgetSettings.DawnDarkPreset, WidgetSettings.GraphiteBloomPreset)]
    [InlineData(WidgetSettings.CyberDarkPreset, WidgetSettings.GraphiteBloomPreset)]
    public void NormalizeColorPresetId_LegacyAliases_MapToSupportedPresets(string legacyPreset, string expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeColorPresetId(legacyPreset));
    }

    [Fact]
    public void CustomAppearance_DefaultsToDisabled()
    {
        var settings = new WidgetSettings();

        Assert.False(settings.UseCustomColors);
        Assert.Empty(settings.CustomTextColor);
        Assert.Empty(settings.CustomBackgroundColor);
        Assert.NotNull(settings.CustomElementColors);
        Assert.Empty(settings.CustomElementColors);
        Assert.False(settings.UseCustomFont);
        Assert.Empty(settings.CustomFontPath);
    }

    [Theory]
    [InlineData("#123abc", "#123ABC")]
    [InlineData("123abc", "#123ABC")]
    [InlineData("#80123abc", "#80123ABC")]
    [InlineData("80123abc", "#80123ABC")]
    public void NormalizeOptionalHexColor_ValidHex_IsNormalized(string input, string expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeOptionalHexColor(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#12XX56")]
    public void NormalizeOptionalHexColor_InvalidHex_ReturnsEmpty(string? input)
    {
        Assert.Empty(WidgetSettings.NormalizeOptionalHexColor(input));
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
