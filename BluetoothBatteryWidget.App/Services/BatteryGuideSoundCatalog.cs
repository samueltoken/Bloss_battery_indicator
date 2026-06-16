using System.IO;
using System.Reflection;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Services;

internal sealed record BatteryGuideSoundOption(
    string Id,
    string DisplayName,
    string ResourceName,
    string FileName,
    bool IsWave,
    string? ExternalPath = null);

internal static class BatteryGuideSoundCatalog
{
    private const string ResourcePrefix = "BluetoothBatteryWidget.App.Assets.";

    public static IReadOnlyList<BatteryGuideSoundOption> GuideOptions { get; } =
    [
        new(
            WidgetSettings.GuideSoundInfographic2Seconds,
            "Infographic 2 sec",
            ResourcePrefix + "guide-sound-infographic-2s.wav",
            "guide-sound-infographic-2s.wav",
            true),
        new(
            WidgetSettings.GuideSoundInfographic1Second,
            "Infographic 1 sec",
            ResourcePrefix + "guide-sound-infographic-1s.wav",
            "guide-sound-infographic-1s.wav",
            true),
        new(
            WidgetSettings.GuideSoundLongAgo,
            "long ago",
            ResourcePrefix + "guide-sound-long-ago.mp3",
            "guide-sound-long-ago.mp3",
            false),
        new(
            WidgetSettings.GuideSoundRick,
            "Rick",
            ResourcePrefix + "guide-sound-rick.mp3",
            "guide-sound-rick.mp3",
            false),
        new(
            WidgetSettings.GuideSoundWarning,
            "Warning",
            ResourcePrefix + "guide-sound-warning.mp3",
            "guide-sound-warning.mp3",
            false),
        new(
            WidgetSettings.GuideSoundSmile,
            "Smile",
            ResourcePrefix + "guide-sound-smile.mp3",
            "guide-sound-smile.mp3",
            false)
    ];

    public static BatteryGuideSoundOption OuterSpaceSound { get; } = new(
        "outer-space",
        "Outer Space",
        ResourcePrefix + "labs-outer-space.mp3",
        "labs-outer-space.mp3",
        false);

    public static BatteryGuideSoundOption Version107DigitalCitySound { get; } = new(
        "version-107-digital-city",
        "Version 1.0.7 Digital City",
        ResourcePrefix + "version-107-digital-city.mp3",
        "version-107-digital-city.mp3",
        false);

    public static IReadOnlyList<BatteryGuideSoundOption> GetGuideOptions(string? customPath, string? language = null)
    {
        var normalizedCustomPath = WidgetSettings.NormalizeOptionalAudioPath(customPath);
        if (string.IsNullOrWhiteSpace(normalizedCustomPath) || !File.Exists(normalizedCustomPath))
        {
            return GuideOptions;
        }

        return GuideOptions
            .Append(CreateCustomOption(normalizedCustomPath, language))
            .ToArray();
    }

    public static BatteryGuideSoundOption ResolveGuideSound(string? soundId, string? customPath = null, string? language = null)
    {
        var normalized = WidgetSettings.NormalizeGuideSoundId(soundId);
        if (string.Equals(normalized, WidgetSettings.GuideSoundCustomFile, StringComparison.Ordinal))
        {
            return TryCreateCustomOption(customPath, language, out var customOption)
                ? customOption
                : GuideOptions.First(option => string.Equals(option.Id, WidgetSettings.DefaultGuideSoundId, StringComparison.Ordinal));
        }

        return GuideOptions.First(option => string.Equals(option.Id, normalized, StringComparison.Ordinal));
    }

    public static byte[] LoadBytes(BatteryGuideSoundOption option)
    {
        if (!string.IsNullOrWhiteSpace(option.ExternalPath) && File.Exists(option.ExternalPath))
        {
            try
            {
                return File.ReadAllBytes(option.ExternalPath);
            }
            catch
            {
                return [];
            }
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(option.ResourceName);
        if (stream is null)
        {
            return [];
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public static string EnsureTempFile(BatteryGuideSoundOption option)
    {
        if (!string.IsNullOrWhiteSpace(option.ExternalPath) && File.Exists(option.ExternalPath))
        {
            return option.ExternalPath;
        }

        var data = LoadBytes(option);
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var directory = Path.Combine(Path.GetTempPath(), "Bloss", "audio");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, option.FileName);
        if (!File.Exists(path) || new FileInfo(path).Length != data.Length)
        {
            File.WriteAllBytes(path, data);
        }

        return path;
    }

    private static bool TryCreateCustomOption(string? path, string? language, out BatteryGuideSoundOption option)
    {
        var normalizedPath = WidgetSettings.NormalizeOptionalAudioPath(path);
        if (!string.IsNullOrWhiteSpace(normalizedPath) && File.Exists(normalizedPath))
        {
            option = CreateCustomOption(normalizedPath, language);
            return true;
        }

        option = GuideOptions.First(option => string.Equals(option.Id, WidgetSettings.DefaultGuideSoundId, StringComparison.Ordinal));
        return false;
    }

    private static BatteryGuideSoundOption CreateCustomOption(string path, string? language)
    {
        var extension = Path.GetExtension(path);
        var isWave = string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase);
        return new BatteryGuideSoundOption(
            WidgetSettings.GuideSoundCustomFile,
            UiLanguageCatalog.GetExtraText(language ?? WidgetSettings.EnglishLanguage, "GuideSoundCustomOption"),
            string.Empty,
            Path.GetFileName(path),
            isWave,
            path);
    }
}
