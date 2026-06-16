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
    public const int AutoPowerIdlePauseMinutes = -1;
    public const int LegacyDefaultPowerIdlePauseMinutes = 1;
    public const int DefaultPowerIdlePauseMinutes = AutoPowerIdlePauseMinutes;
    public const int MinimumPowerIdlePauseMinutes = 0;
    public const int MaximumPowerIdlePauseMinutes = 300;
    public const int CurrentSettingsSchemaVersion = 2;
    public const int WindowsPowerIdleAutoSettingsSchemaVersion = 2;
    public const int MaximumBatteryGuideTriggerLength = 512;
    public const int MaximumBatteryGuideTriggerProfiles = 8;
    public const string DualSenseBatteryGuideProfileKey = "DualSense";
    public const string SteamControllerBatteryGuideProfileKey = "SteamController";
    public const int ForcedBatteryAlertThresholdPercent = 15;
    public const int MinimumCustomBatteryAlertThresholdPercent = 30;
    public const int MaximumCustomBatteryAlertThresholdPercent = 80;
    public const int MaximumBatteryAlertThresholdsLength = 64;
    public const string DefaultBatteryAlertThresholds = "30";
    public const double DefaultSettingsTextFontSize = 13d;
    public const double MinimumSettingsTextFontSize = 11d;
    public const double MaximumSettingsTextFontSize = 18d;
    public const int MaximumFirmwareVersionTextLength = 64;
    public const int MaximumReleaseNotesVersionLength = 32;

    public bool Autostart { get; set; } = true;

    public int SettingsSchemaVersion { get; set; } = 0;

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

    public string BatteryGuideTrigger { get; set; } = string.Empty;

    public Dictionary<string, string> BatteryGuideTriggerProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string BatteryAlertThresholds { get; set; } = DefaultBatteryAlertThresholds;

    public string LastDs5DongleFirmwareVersion { get; set; } = string.Empty;

    public string LastSeenReleaseNotesVersion { get; set; } = string.Empty;

    public bool StatusPanelCollapsed { get; set; } = false;

    public int UiScaleStep { get; set; } = 0;

    public bool UseCustomSettingsTextStyle { get; set; } = false;

    public double SettingsTextFontSize { get; set; } = DefaultSettingsTextFontSize;

    public bool SettingsTextBold { get; set; } = false;

    public ThirdPartyBatteryPolicy ThirdPartyBatteryPolicy { get; set; } = ThirdPartyBatteryPolicy.Aggressive;

    public int BatteryHoldSeconds { get; set; } = DefaultBatteryHoldSeconds;

    public int GamepadDisconnectGraceSeconds { get; set; } = DefaultGamepadDisconnectGraceSeconds;

    public int PowerIdlePauseMinutes { get; set; } = DefaultPowerIdlePauseMinutes;

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
        var normalized = language?.Trim();
        if (string.Equals(normalized, KoreanLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "ko", StringComparison.OrdinalIgnoreCase))
        {
            return KoreanLanguage;
        }

        if (string.Equals(normalized, EnglishLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "en", StringComparison.OrdinalIgnoreCase))
        {
            return EnglishLanguage;
        }

        if (string.Equals(normalized, JapaneseLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "ja", StringComparison.OrdinalIgnoreCase))
        {
            return JapaneseLanguage;
        }

        if (string.Equals(normalized, ChineseSimplifiedLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "zh", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            return ChineseSimplifiedLanguage;
        }

        if (string.Equals(normalized, ChineseTraditionalLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            return ChineseTraditionalLanguage;
        }

        if (string.Equals(normalized, LatinLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "la", StringComparison.OrdinalIgnoreCase))
        {
            return LatinLanguage;
        }

        if (string.Equals(normalized, FrenchLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "fr", StringComparison.OrdinalIgnoreCase))
        {
            return FrenchLanguage;
        }

        return KoreanLanguage;
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

    public static string NormalizeReleaseNotesVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = new string(version
            .Trim()
            .Where(character => !char.IsControl(character))
            .Take(MaximumReleaseNotesVersionLength)
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

    public static int NormalizePowerIdlePauseMinutes(int minutes)
    {
        if (minutes == AutoPowerIdlePauseMinutes)
        {
            return AutoPowerIdlePauseMinutes;
        }

        return Math.Clamp(
            minutes,
            MinimumPowerIdlePauseMinutes,
            MaximumPowerIdlePauseMinutes);
    }

    public static string NormalizeBatteryGuideTrigger(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = new string(value
            .Trim()
            .Where(character => !char.IsControl(character))
            .Take(MaximumBatteryGuideTriggerLength)
            .ToArray());
        return normalized.Trim();
    }

    public static string NormalizeBatteryGuideTriggerProfileKey(string? value)
    {
        var normalized = value?.Trim();
        if (string.Equals(normalized, DualSenseBatteryGuideProfileKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "DualSense Wireless Controller", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Sony DualSense", StringComparison.OrdinalIgnoreCase))
        {
            return DualSenseBatteryGuideProfileKey;
        }

        if (string.Equals(normalized, SteamControllerBatteryGuideProfileKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Steam Controller", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Steam", StringComparison.OrdinalIgnoreCase))
        {
            return SteamControllerBatteryGuideProfileKey;
        }

        return string.Empty;
    }

    public static Dictionary<string, string> NormalizeBatteryGuideTriggerProfiles(
        IDictionary<string, string>? profiles)
    {
        var normalizedProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (profiles is null)
        {
            return normalizedProfiles;
        }

        foreach (var pair in profiles)
        {
            var key = NormalizeBatteryGuideTriggerProfileKey(pair.Key);
            var value = NormalizeBatteryGuideTrigger(pair.Value);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalizedProfiles[key] = value;
            if (normalizedProfiles.Count >= MaximumBatteryGuideTriggerProfiles)
            {
                break;
            }
        }

        return normalizedProfiles;
    }

    public static string NormalizeBatteryAlertThresholds(string? value)
    {
        var thresholds = GetBatteryAlertThresholdPercents(value);
        return thresholds.Count == 0
            ? string.Empty
            : string.Join(", ", thresholds);
    }

    public static IReadOnlyList<int> GetBatteryAlertThresholdPercents(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '/', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part.TrimEnd('%'), out var percent) ? percent : (int?)null)
            .Where(percent => percent is >= MinimumCustomBatteryAlertThresholdPercent and <= MaximumCustomBatteryAlertThresholdPercent)
            .Select(percent => percent!.Value)
            .Distinct()
            .OrderBy(percent => percent)
            .Take(8)
            .ToArray();
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
