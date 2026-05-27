using System.IO;

namespace BluetoothBatteryWidget.Core.Models;

public sealed class WidgetSettings
{
    public const string NormalGlassMode = "NormalGlass";
    public const string LiteGlassMode = "LiteGlass";
    public const string WhiteBluePreset = "WhiteBlue";
    public const string CloudDancerPreset = "CloudDancer";
    public const string MoonLavenderPreset = "MoonLavender";
    public const string MistSagePreset = "MistSage";
    public const string AuroraTealPreset = "AuroraTeal";
    public const string RoseDuskPreset = "RoseDusk";
    public const string DeepNavyPreset = "DeepNavy";
    public const string GraphiteBloomPreset = "GraphiteBloom";
    public const string AquaClassicPreset = "AquaClassic";
    public const string OceanMistPreset = "OceanMist";
    public const string SkyGlassPreset = "SkyGlass";
    public const string MintBluePreset = "MintBlue";
    public const string SteelAquaPreset = "SteelAqua";
    public const string BurgundyPreset = "Burgundy";
    public const string CrimsonRedPreset = "CrimsonRed";
    public const string BlackTonePreset = "BlackTone";
    public const string DeepGreenPreset = "DeepGreen";
    public const string CobaltBluePreset = "CobaltBlue";
    public const string DeepBlueSeaPreset = "DeepBlueSea";
    public const string AblRedPreset = "AblRed";
    public const string GrassGreenPreset = "GrassGreen";
    public const string BurgundyRedPreset = "BurgundyRed";
    public const string DawnDarkPreset = "DawnDark";
    public const string CyberDarkPreset = "CyberDark";
    public const string KoreanLanguage = "ko-KR";
    public const string EnglishLanguage = "en-US";
    public const string JapaneseLanguage = "ja-JP";
    public const string ChineseSimplifiedLanguage = "zh-CN";
    public const string ChineseTraditionalLanguage = "zh-TW";
    public const string LatinLanguage = "la-LA";
    public const string FrenchLanguage = "fr-FR";
    public const string GuideSoundInfographic2Seconds = "infographic-2s";
    public const string GuideSoundInfographic1Second = "infographic-1s";
    public const string GuideSoundLongAgo = "long-ago";
    public const string GuideSoundRick = "rick";
    public const string GuideSoundWarning = "warning";
    public const string GuideSoundSmile = "smile";
    public const string GuideSoundCustomFile = "custom-file";
    public const string DefaultGuideSoundId = GuideSoundInfographic2Seconds;
    public const int DefaultBatteryHoldSeconds = 600;
    public const int LegacyDefaultBatteryHoldSeconds = 90;
    public const int MinimumBatteryHoldSeconds = 60;
    public const int MaximumBatteryHoldSeconds = 1800;
    public const int DefaultGamepadDisconnectGraceSeconds = 0;
    public const int LegacyDefaultGamepadDisconnectGraceSeconds = 70;
    public const int MinimumGamepadDisconnectGraceSeconds = 0;
    public const int MaximumGamepadDisconnectGraceSeconds = 180;
    public const double DefaultSettingsTextFontSize = 13d;
    public const double MinimumSettingsTextFontSize = 11d;
    public const double MaximumSettingsTextFontSize = 18d;
    public const int MaximumFirmwareVersionTextLength = 64;

    public bool Autostart { get; set; } = true;

    public bool StartMinimizedToTray { get; set; } = false;

    public bool CloseToTray { get; set; } = true;

    public int RefreshSeconds { get; set; } = 30;

    public string VisualMode { get; set; } = NormalGlassMode;

    public string ColorPresetId { get; set; } = WhiteBluePreset;

    public bool UseCustomColors { get; set; } = false;

    public string CustomTextColor { get; set; } = string.Empty;

    public string CustomBackgroundColor { get; set; } = string.Empty;

    public Dictionary<string, string> CustomElementColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool UseCustomFont { get; set; } = false;

    public string CustomFontPath { get; set; } = string.Empty;

    public string Language { get; set; } = KoreanLanguage;

    public bool GuidedProbeEnabled { get; set; } = false;

    public bool GuideSoundEnabled { get; set; } = true;

    public string GuideSoundId { get; set; } = DefaultGuideSoundId;

    public string CustomGuideSoundPath { get; set; } = string.Empty;

    public string LastDs5DongleFirmwareVersion { get; set; } = string.Empty;

    public bool StatusPanelCollapsed { get; set; } = false;

    public int UiScaleStep { get; set; } = 0;

    public bool UseCustomSettingsTextStyle { get; set; } = false;

    public double SettingsTextFontSize { get; set; } = DefaultSettingsTextFontSize;

    public bool SettingsTextBold { get; set; } = false;

    public ThirdPartyBatteryPolicy ThirdPartyBatteryPolicy { get; set; } = ThirdPartyBatteryPolicy.Aggressive;

    public int BatteryHoldSeconds { get; set; } = DefaultBatteryHoldSeconds;

    public int GamepadDisconnectGraceSeconds { get; set; } = DefaultGamepadDisconnectGraceSeconds;

    public WindowBounds? WindowBounds { get; set; }

    public Dictionary<string, string> IconOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> IconImageOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> NameOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string NormalizeColorPresetId(string? presetId)
    {
        return presetId switch
        {
            WhiteBluePreset => WhiteBluePreset,
            CloudDancerPreset => CloudDancerPreset,
            MoonLavenderPreset => MoonLavenderPreset,
            MistSagePreset => MistSagePreset,
            AuroraTealPreset => AuroraTealPreset,
            RoseDuskPreset => RoseDuskPreset,
            DeepNavyPreset => DeepNavyPreset,
            GraphiteBloomPreset => GraphiteBloomPreset,
            AquaClassicPreset => WhiteBluePreset,
            OceanMistPreset => WhiteBluePreset,
            SkyGlassPreset => WhiteBluePreset,
            MintBluePreset => WhiteBluePreset,
            SteelAquaPreset => WhiteBluePreset,
            BurgundyPreset => RoseDuskPreset,
            CrimsonRedPreset => RoseDuskPreset,
            AblRedPreset => RoseDuskPreset,
            BurgundyRedPreset => RoseDuskPreset,
            BlackTonePreset => GraphiteBloomPreset,
            DawnDarkPreset => GraphiteBloomPreset,
            CyberDarkPreset => GraphiteBloomPreset,
            DeepGreenPreset => MistSagePreset,
            GrassGreenPreset => MistSagePreset,
            CobaltBluePreset => DeepNavyPreset,
            DeepBlueSeaPreset => DeepNavyPreset,
            _ => WhiteBluePreset
        };
    }

    public static string NormalizeOptionalHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
        {
            trimmed = $"#{trimmed}";
        }

        if (trimmed.Length is not (7 or 9))
        {
            return string.Empty;
        }

        for (var index = 1; index < trimmed.Length; index++)
        {
            if (!Uri.IsHexDigit(trimmed[index]))
            {
                return string.Empty;
            }
        }

        return trimmed.ToUpperInvariant();
    }

    public static int NormalizeUiScaleStep(int uiScaleStep)
    {
        return Math.Clamp(uiScaleStep, -5, 0);
    }

    public static double NormalizeSettingsTextFontSize(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return DefaultSettingsTextFontSize;
        }

        return Math.Clamp(
            Math.Round(value, MidpointRounding.AwayFromZero),
            MinimumSettingsTextFontSize,
            MaximumSettingsTextFontSize);
    }

    public static string NormalizeLanguage(string? language)
    {
        return language switch
        {
            KoreanLanguage => KoreanLanguage,
            EnglishLanguage => EnglishLanguage,
            JapaneseLanguage => JapaneseLanguage,
            ChineseSimplifiedLanguage => ChineseSimplifiedLanguage,
            ChineseTraditionalLanguage => ChineseTraditionalLanguage,
            LatinLanguage => LatinLanguage,
            FrenchLanguage => FrenchLanguage,
            _ => KoreanLanguage
        };
    }

    public static string NormalizeGuideSoundId(string? soundId)
    {
        return soundId switch
        {
            GuideSoundInfographic2Seconds => GuideSoundInfographic2Seconds,
            GuideSoundInfographic1Second => GuideSoundInfographic1Second,
            GuideSoundLongAgo => GuideSoundLongAgo,
            GuideSoundRick => GuideSoundRick,
            GuideSoundWarning => GuideSoundWarning,
            GuideSoundSmile => GuideSoundSmile,
            GuideSoundCustomFile => GuideSoundCustomFile,
            _ => DefaultGuideSoundId
        };
    }

    public static string NormalizeFirmwareVersionText(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = new string(version
            .Trim()
            .Where(character => !char.IsControl(character))
            .Take(MaximumFirmwareVersionTextLength)
            .ToArray());
        return normalized.Trim();
    }

    public static string NormalizeOptionalAudioPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var extension = Path.GetExtension(trimmed);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.ToLowerInvariant() switch
        {
            ".wav" or ".mp3" or ".wma" or ".m4a" => trimmed,
            _ => string.Empty
        };
    }

    public static int NormalizeGamepadDisconnectGraceSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            return DefaultGamepadDisconnectGraceSeconds;
        }

        return Math.Clamp(
            seconds,
            MinimumGamepadDisconnectGraceSeconds,
            MaximumGamepadDisconnectGraceSeconds);
    }

    public static int NormalizeBatteryHoldSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            return DefaultBatteryHoldSeconds;
        }

        return Math.Clamp(
            seconds,
            MinimumBatteryHoldSeconds,
            MaximumBatteryHoldSeconds);
    }
}

public enum ThirdPartyBatteryPolicy
{
    Conservative = 0,
    Aggressive = 1,
    Hybrid = 2
}

public sealed class WindowBounds
{
    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }
}
