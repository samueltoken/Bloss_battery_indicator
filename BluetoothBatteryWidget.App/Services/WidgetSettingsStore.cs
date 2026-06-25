using System.IO;
using System.Text.Json;
using System.Diagnostics;
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
        : this(GetDefaultSettingsPath(), GetDefaultLegacySettingsPath())
    {
    }

    internal WidgetSettingsStore(string settingsPath, string legacySettingsPath)
    {
        _settingsPath = settingsPath;
        _legacySettingsPath = legacySettingsPath;
    }

    public string SettingsPath => _settingsPath;

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Bloss", "settings.json");
    }

    private static string GetDefaultLegacySettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "BluetoothBatteryWidget", "settings.json");
    }

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
            return NormalizeLoaded(loaded ?? new WidgetSettings());
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"settings_load_failed path={_settingsPath} error={ex.GetType().Name}: {ex.Message}");
            BackupUnreadableSettingsFile();
            return new WidgetSettings();
        }
    }

    public void Save(WidgetSettings settings)
    {
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        AtomicFileWriter.WriteAllText(_settingsPath, json);
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

    private void BackupUnreadableSettingsFile()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var backupPath = BuildUnreadableBackupPath(_settingsPath, DateTimeOffset.Now);
            File.Copy(_settingsPath, backupPath, overwrite: false);
        }
        catch
        {
            // Loading must still recover even when the backup cannot be written.
        }
    }

    internal static string BuildUnreadableBackupPath(string settingsPath, DateTimeOffset now)
    {
        var directory = Path.GetDirectoryName(settingsPath) ?? string.Empty;
        var fileName = Path.GetFileName(settingsPath);
        var timestamp = now.ToString("yyyyMMdd-HHmmss-fff");
        return Path.Combine(directory, $"{fileName}.corrupt-{timestamp}.bak");
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
        settings.CustomTextColor = WidgetSettings.NormalizeOptionalHexColor(settings.CustomTextColor);
        settings.CustomBackgroundColor = WidgetSettings.NormalizeOptionalHexColor(settings.CustomBackgroundColor);
        settings.CustomElementColors ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.CustomElementColors = settings.CustomElementColors
            .Select(pair => new
            {
                Key = pair.Key?.Trim() ?? string.Empty,
                Value = WidgetSettings.NormalizeOptionalHexColor(pair.Value)
            })
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(settings.CustomTextColor))
        {
            settings.CustomElementColors.TryAdd("PrimaryText", settings.CustomTextColor);
        }

        if (!string.IsNullOrWhiteSpace(settings.CustomBackgroundColor))
        {
            settings.CustomElementColors.TryAdd("CardTint", settings.CustomBackgroundColor);
        }

        settings.UseCustomColors = settings.UseCustomColors && settings.CustomElementColors.Count > 0;
        settings.CustomFontPath = settings.CustomFontPath?.Trim() ?? string.Empty;
        settings.UseCustomFont = settings.UseCustomFont && !string.IsNullOrWhiteSpace(settings.CustomFontPath);
        settings.Language = WidgetSettings.NormalizeLanguage(settings.Language);
        settings.CustomGuideSoundPath = WidgetSettings.NormalizeOptionalAudioPath(settings.CustomGuideSoundPath);
        settings.GuideSoundId = WidgetSettings.NormalizeGuideSoundId(settings.GuideSoundId);
        settings.BatteryGuideTrigger = WidgetSettings.NormalizeBatteryGuideTrigger(settings.BatteryGuideTrigger);
        settings.BatteryGuideTriggerProfiles =
            WidgetSettings.NormalizeBatteryGuideTriggerProfiles(settings.BatteryGuideTriggerProfiles);
        if (!string.IsNullOrWhiteSpace(settings.BatteryGuideTrigger) &&
            BatteryGuideTriggerParser.TryParse(settings.BatteryGuideTrigger, out var legacyTrigger))
        {
            var legacyProfileKey =
                WidgetSettings.NormalizeBatteryGuideTriggerProfileKey(legacyTrigger.DeviceKind.ToString());
            if (!string.IsNullOrWhiteSpace(legacyProfileKey))
            {
                settings.BatteryGuideTriggerProfiles.TryAdd(legacyProfileKey, settings.BatteryGuideTrigger);
            }
        }

        settings.BatteryAlertThresholds = WidgetSettings.NormalizeBatteryAlertThresholds(settings.BatteryAlertThresholds);
        settings.BatteryAlertDeviceEnabled =
            WidgetSettings.NormalizeBatteryAlertDeviceEnabled(settings.BatteryAlertDeviceEnabled);
        settings.LastDs5DongleFirmwareVersion =
            WidgetSettings.NormalizeFirmwareVersionText(settings.LastDs5DongleFirmwareVersion);
        settings.LastSeenReleaseNotesVersion =
            WidgetSettings.NormalizeReleaseNotesVersion(settings.LastSeenReleaseNotesVersion);
        if (settings.GuideSoundId == WidgetSettings.GuideSoundCustomFile &&
            string.IsNullOrWhiteSpace(settings.CustomGuideSoundPath))
        {
            settings.GuideSoundId = WidgetSettings.DefaultGuideSoundId;
        }

        settings.UiScaleStep = WidgetSettings.NormalizeUiScaleStep(settings.UiScaleStep);
        settings.SettingsTextFontSize = WidgetSettings.NormalizeSettingsTextFontSize(settings.SettingsTextFontSize);
        settings.UseCustomSettingsTextStyle =
            settings.UseCustomSettingsTextStyle ||
            settings.SettingsTextBold ||
            Math.Abs(settings.SettingsTextFontSize - WidgetSettings.DefaultSettingsTextFontSize) > 0.01d ||
            settings.CustomElementColors.ContainsKey("SettingsText");

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
        settings.PowerIdlePauseMinutes = WidgetSettings.NormalizePowerIdlePauseMinutes(settings.PowerIdlePauseMinutes);
        settings.SettingsSchemaVersion = WidgetSettings.CurrentSettingsSchemaVersion;
        settings.IconOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.IconImageOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.NameOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.IconImageOverrides = IconImageOverrideParser.Parse(settings.IconImageOverrides)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        settings.NameOverrides = NameOverrideParser.Parse(settings.NameOverrides)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return settings;
    }

    private static WidgetSettings NormalizeLoaded(WidgetSettings settings)
    {
        var loadedSchemaVersion = settings.SettingsSchemaVersion;
        var normalized = Normalize(settings);
        if (loadedSchemaVersion < WidgetSettings.WindowsPowerIdleAutoSettingsSchemaVersion &&
            normalized.PowerIdlePauseMinutes == WidgetSettings.LegacyDefaultPowerIdlePauseMinutes)
        {
            normalized.PowerIdlePauseMinutes = WidgetSettings.AutoPowerIdlePauseMinutes;
        }

        return normalized;
    }
}


