namespace BluetoothBatteryWidget.Core.Models;

public sealed class WidgetSettings
{
    public const string NormalGlassMode = "NormalGlass";
    public const string LiteGlassMode = "LiteGlass";
    public const string WhiteBluePreset = "WhiteBlue";
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
    public const int DefaultBatteryHoldSeconds = 600;
    public const int LegacyDefaultBatteryHoldSeconds = 90;
    public const int MinimumBatteryHoldSeconds = 60;
    public const int MaximumBatteryHoldSeconds = 1800;
    public const int DefaultGamepadDisconnectGraceSeconds = 0;
    public const int LegacyDefaultGamepadDisconnectGraceSeconds = 70;
    public const int MinimumGamepadDisconnectGraceSeconds = 0;
    public const int MaximumGamepadDisconnectGraceSeconds = 180;

    public bool Autostart { get; set; } = true;

    public bool CloseToTray { get; set; } = true;

    public int RefreshSeconds { get; set; } = 30;

    public string VisualMode { get; set; } = NormalGlassMode;

    public string ColorPresetId { get; set; } = WhiteBluePreset;

    public string Language { get; set; } = KoreanLanguage;

    public bool GuidedProbeEnabled { get; set; } = false;

    public bool StatusPanelCollapsed { get; set; } = false;

    public int UiScaleStep { get; set; } = 0;

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
            AquaClassicPreset => WhiteBluePreset,
            OceanMistPreset => OceanMistPreset,
            SkyGlassPreset => SkyGlassPreset,
            MintBluePreset => MintBluePreset,
            SteelAquaPreset => SteelAquaPreset,
            BurgundyPreset => BurgundyPreset,
            CrimsonRedPreset => CrimsonRedPreset,
            BlackTonePreset => BlackTonePreset,
            DeepGreenPreset => DeepGreenPreset,
            CobaltBluePreset => CobaltBluePreset,
            DeepBlueSeaPreset => DeepBlueSeaPreset,
            AblRedPreset => AblRedPreset,
            GrassGreenPreset => GrassGreenPreset,
            BurgundyRedPreset => BurgundyRedPreset,
            DawnDarkPreset => DawnDarkPreset,
            CyberDarkPreset => CyberDarkPreset,
            _ => WhiteBluePreset
        };
    }

    public static int NormalizeUiScaleStep(int uiScaleStep)
    {
        return Math.Clamp(uiScaleStep, -5, 0);
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
