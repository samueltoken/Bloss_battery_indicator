using System.IO;
using System.Text.Json;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class WidgetSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly string _legacySettingsPath;

    public WidgetSettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = Path.Combine(appData, "Bloss");

        _settingsPath = Path.Combine(root, "settings.json");
        _legacySettingsPath = Path.Combine(appData, "BluetoothBatteryWidget", "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public WidgetSettings Load()
    {
        try
        {
            MigrateLegacySettingsIfNeeded();

            if (!File.Exists(_settingsPath))
            {
                return new WidgetSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<WidgetSettings>(json, JsonOptions);
            return Normalize(loaded ?? new WidgetSettings());
        }
        catch
        {
            return new WidgetSettings();
        }
    }

    public void Save(WidgetSettings settings)
    {
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private void MigrateLegacySettingsIfNeeded()
    {
        if (File.Exists(_settingsPath) || !File.Exists(_legacySettingsPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        File.Copy(_legacySettingsPath, _settingsPath, overwrite: false);
    }

    private static WidgetSettings Normalize(WidgetSettings settings)
    {
        settings.RefreshSeconds = settings.RefreshSeconds <= 0 ? 30 : settings.RefreshSeconds;
        if (!string.Equals(settings.VisualMode, WidgetSettings.NormalGlassMode, StringComparison.Ordinal) &&
            !string.Equals(settings.VisualMode, WidgetSettings.LiteGlassMode, StringComparison.Ordinal))
        {
            settings.VisualMode = WidgetSettings.NormalGlassMode;
        }

        if (!Enum.IsDefined(settings.ThirdPartyBatteryPolicy))
        {
            settings.ThirdPartyBatteryPolicy = ThirdPartyBatteryPolicy.Aggressive;
        }

        settings.ColorPresetId = WidgetSettings.NormalizeColorPresetId(settings.ColorPresetId);
        settings.Language = WidgetSettings.NormalizeLanguage(settings.Language);
        settings.UiScaleStep = WidgetSettings.NormalizeUiScaleStep(settings.UiScaleStep);
        var normalizedHold = WidgetSettings.NormalizeBatteryHoldSeconds(settings.BatteryHoldSeconds);
        if (normalizedHold == WidgetSettings.LegacyDefaultBatteryHoldSeconds)
        {
            normalizedHold = WidgetSettings.DefaultBatteryHoldSeconds;
        }

        settings.BatteryHoldSeconds = normalizedHold;
        var normalizedDisconnectGrace = WidgetSettings.NormalizeGamepadDisconnectGraceSeconds(settings.GamepadDisconnectGraceSeconds);
        if (normalizedDisconnectGrace == WidgetSettings.LegacyDefaultGamepadDisconnectGraceSeconds)
        {
            normalizedDisconnectGrace = WidgetSettings.DefaultGamepadDisconnectGraceSeconds;
        }

        settings.GamepadDisconnectGraceSeconds = normalizedDisconnectGrace;
        settings.IconOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.IconImageOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.NameOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.IconImageOverrides = IconImageOverrideParser.Parse(settings.IconImageOverrides)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        settings.NameOverrides = NameOverrideParser.Parse(settings.NameOverrides)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return settings;
    }
}


