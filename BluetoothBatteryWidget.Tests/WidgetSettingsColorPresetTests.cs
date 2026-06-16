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
    public void GuideSound_DefaultsToEnabledTwoSecondInfographic()
    {
        var settings = new WidgetSettings();

        Assert.True(settings.GuideSoundEnabled);
        Assert.Equal(WidgetSettings.GuideSoundInfographic2Seconds, settings.GuideSoundId);
    }

    [Fact]
    public void LastDs5DongleFirmwareVersion_DefaultsToEmpty()
    {
        var settings = new WidgetSettings();

        Assert.Empty(settings.LastDs5DongleFirmwareVersion);
    }

    [Theory]
    [InlineData(" v0.6.0-hotfix ", "v0.6.0-hotfix")]
    [InlineData("0.6.1\r\n", "0.6.1")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeFirmwareVersionText_TrimsControlCharacters(string? input, string expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeFirmwareVersionText(input));
    }

    [Theory]
    [InlineData(WidgetSettings.GuideSoundInfographic2Seconds)]
    [InlineData(WidgetSettings.GuideSoundInfographic1Second)]
    [InlineData(WidgetSettings.GuideSoundLongAgo)]
    [InlineData(WidgetSettings.GuideSoundRick)]
    [InlineData(WidgetSettings.GuideSoundWarning)]
    [InlineData(WidgetSettings.GuideSoundSmile)]
    [InlineData(WidgetSettings.GuideSoundCustomFile)]
    public void NormalizeGuideSoundId_ValidValues_ArePreserved(string soundId)
    {
        Assert.Equal(soundId, WidgetSettings.NormalizeGuideSoundId(soundId));
    }

    [Theory]
    [InlineData(@"C:\sound.wav", @"C:\sound.wav")]
    [InlineData(@"C:\sound.mp3", @"C:\sound.mp3")]
    [InlineData(@"C:\sound.wma", @"C:\sound.wma")]
    [InlineData(@"C:\sound.m4a", @"C:\sound.m4a")]
    [InlineData(@"C:\sound.txt", "")]
    [InlineData("", "")]
    public void NormalizeOptionalAudioPath_OnlyKeepsSupportedAudioExtensions(string input, string expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeOptionalAudioPath(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("outer-space")]
    [InlineData("missing")]
    public void NormalizeGuideSoundId_InvalidValues_FallBackToDefault(string? soundId)
    {
        Assert.Equal(WidgetSettings.DefaultGuideSoundId, WidgetSettings.NormalizeGuideSoundId(soundId));
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
    public void SettingsTextStyle_DefaultsToDisabled()
    {
        var settings = new WidgetSettings();

        Assert.False(settings.UseCustomSettingsTextStyle);
        Assert.Equal(WidgetSettings.DefaultSettingsTextFontSize, settings.SettingsTextFontSize);
        Assert.False(settings.SettingsTextBold);
    }

    [Theory]
    [InlineData(-1, 13)]
    [InlineData(0, 13)]
    [InlineData(10, 11)]
    [InlineData(11, 11)]
    [InlineData(14.4, 14)]
    [InlineData(18, 18)]
    [InlineData(20, 18)]
    public void NormalizeSettingsTextFontSize_ClampsRange(double input, double expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeSettingsTextFontSize(input));
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
    public void PowerIdlePauseMinutes_DefaultsToWindowsAuto()
    {
        var settings = new WidgetSettings();

        Assert.Equal(WidgetSettings.DefaultPowerIdlePauseMinutes, settings.PowerIdlePauseMinutes);
        Assert.Equal(WidgetSettings.AutoPowerIdlePauseMinutes, settings.PowerIdlePauseMinutes);
    }

    [Fact]
    public void BatteryGuideTrigger_DefaultsToGuideButton()
    {
        var settings = new WidgetSettings();

        Assert.Empty(settings.BatteryGuideTrigger);
        Assert.NotNull(settings.BatteryGuideTriggerProfiles);
        Assert.Empty(settings.BatteryGuideTriggerProfiles);
        Assert.Equal("DualSense", WidgetSettings.DualSenseBatteryGuideProfileKey);
        Assert.Equal("SteamController", WidgetSettings.SteamControllerBatteryGuideProfileKey);
    }

    [Fact]
    public void NormalizeBatteryGuideTriggerProfiles_NormalizesKnownDeviceKeys()
    {
        var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sony DualSense"] = " DualSense|01|0A:04|Mic \r\n",
            ["Steam Controller"] = "SteamController|45|03:40,04:80|View + RT",
            ["Unknown"] = "DualSense|01|0A:01|PS",
            ["Steam"] = ""
        };

        var normalized = WidgetSettings.NormalizeBatteryGuideTriggerProfiles(profiles);

        Assert.Equal(2, normalized.Count);
        Assert.Equal("DualSense|01|0A:04|Mic", normalized[WidgetSettings.DualSenseBatteryGuideProfileKey]);
        Assert.Equal(
            "SteamController|45|03:40,04:80|View + RT",
            normalized[WidgetSettings.SteamControllerBatteryGuideProfileKey]);
        Assert.False(normalized.ContainsKey("Unknown"));
    }

    [Fact]
    public void LastSeenReleaseNotesVersion_DefaultsToEmpty()
    {
        var settings = new WidgetSettings();

        Assert.Empty(settings.LastSeenReleaseNotesVersion);
    }

    [Fact]
    public void BatteryAlertThresholds_DefaultsToThirty()
    {
        var settings = new WidgetSettings();

        Assert.Equal("30", settings.BatteryAlertThresholds);
        Assert.Equal(15, WidgetSettings.ForcedBatteryAlertThresholdPercent);
        Assert.Equal(30, WidgetSettings.MinimumCustomBatteryAlertThresholdPercent);
        Assert.Equal(80, WidgetSettings.MaximumCustomBatteryAlertThresholdPercent);
    }

    [Theory]
    [InlineData("80, 30, 15, 29, 30, 90", "30, 80")]
    [InlineData("45% / 60 | 75; bad", "45, 60, 75")]
    [InlineData("10, 20, 90", "")]
    public void NormalizeBatteryAlertThresholds_KeepsOnlyThirtyToEightyAndSorts(string input, string expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizeBatteryAlertThresholds(input));
    }

    [Fact]
    public void GetBatteryAlertThresholdPercents_ParsesMultipleSeparators()
    {
        var thresholds = WidgetSettings.GetBatteryAlertThresholdPercents("30,40; 50 / 60 | 70 80");

        Assert.Equal([30, 40, 50, 60, 70, 80], thresholds);
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

    [Theory]
    [InlineData(-2, 0)]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(300, 300)]
    [InlineData(999, 300)]
    public void NormalizePowerIdlePauseMinutes_ClampsRange(int input, int expected)
    {
        Assert.Equal(expected, WidgetSettings.NormalizePowerIdlePauseMinutes(input));
    }

    [Fact]
    public void NormalizeBatteryGuideTrigger_RemovesControlsAndLimitsLength()
    {
        var input = new string('A', WidgetSettings.MaximumBatteryGuideTriggerLength + 20) + "\r\n";

        var normalized = WidgetSettings.NormalizeBatteryGuideTrigger(input);

        Assert.Equal(WidgetSettings.MaximumBatteryGuideTriggerLength, normalized.Length);
        Assert.DoesNotContain('\r', normalized);
        Assert.DoesNotContain('\n', normalized);
    }

    [Fact]
    public void NormalizeReleaseNotesVersion_RemovesControlsAndLimitsLength()
    {
        var input = new string('1', WidgetSettings.MaximumReleaseNotesVersionLength + 20) + "\r\n";

        var normalized = WidgetSettings.NormalizeReleaseNotesVersion(input);

        Assert.Equal(WidgetSettings.MaximumReleaseNotesVersionLength, normalized.Length);
        Assert.DoesNotContain('\r', normalized);
        Assert.DoesNotContain('\n', normalized);
    }
}
