using System.Text.Json;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class WidgetSettingsStoreTests
{
    [Fact]
    public void Load_MigratesLegacyPowerIdleOneMinuteDefaultToWindowsAuto()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(
                settingsPath,
                """
                {
                  "settingsSchemaVersion": 0,
                  "powerIdlePauseMinutes": 1
                }
                """);

            var store = new WidgetSettingsStore(settingsPath, Path.Combine(directory, "legacy.json"));

            var settings = store.Load();

            Assert.Equal(WidgetSettings.AutoPowerIdlePauseMinutes, settings.PowerIdlePauseMinutes);
            Assert.Equal(WidgetSettings.CurrentSettingsSchemaVersion, settings.SettingsSchemaVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_PreservesCurrentSchemaPowerIdleOneMinuteUserChoice()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(
                settingsPath,
                $$"""
                {
                  "settingsSchemaVersion": {{WidgetSettings.CurrentSettingsSchemaVersion}},
                  "powerIdlePauseMinutes": 1
                }
                """);

            var store = new WidgetSettingsStore(settingsPath, Path.Combine(directory, "legacy.json"));

            var settings = store.Load();

            Assert.Equal(WidgetSettings.LegacyDefaultPowerIdlePauseMinutes, settings.PowerIdlePauseMinutes);
            Assert.Equal(WidgetSettings.CurrentSettingsSchemaVersion, settings.SettingsSchemaVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_WritesCurrentSchemaAndPreservesManualOneMinuteChoice()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "Bloss", "settings.json");
            var store = new WidgetSettingsStore(settingsPath, Path.Combine(directory, "legacy.json"));

            store.Save(new WidgetSettings
            {
                PowerIdlePauseMinutes = WidgetSettings.LegacyDefaultPowerIdlePauseMinutes
            });

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            Assert.Equal(
                WidgetSettings.CurrentSettingsSchemaVersion,
                root.GetProperty("settingsSchemaVersion").GetInt32());
            Assert.Equal(
                WidgetSettings.LegacyDefaultPowerIdlePauseMinutes,
                root.GetProperty("powerIdlePauseMinutes").GetInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_BacksUpUnreadableSettingsAndReturnsDefaults()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(settingsPath, "{ not valid json");
            var store = new WidgetSettingsStore(settingsPath, Path.Combine(directory, "legacy.json"));

            var settings = store.Load();

            Assert.Equal(30, settings.RefreshSeconds);
            var backups = Directory.GetFiles(directory, "settings.json.corrupt-*.bak");
            var backup = Assert.Single(backups);
            Assert.Equal("{ not valid json", File.ReadAllText(backup));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_MigratesLegacyBatteryGuideTriggerToDeviceProfile()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(
                settingsPath,
                $$"""
                {
                  "settingsSchemaVersion": {{WidgetSettings.CurrentSettingsSchemaVersion}},
                  "batteryGuideTrigger": "DualSense|01|0A:04|Mic"
                }
                """);

            var store = new WidgetSettingsStore(settingsPath, Path.Combine(directory, "legacy.json"));

            var settings = store.Load();

            Assert.Equal("DualSense|01|0A:04|Mic", settings.BatteryGuideTrigger);
            Assert.Equal(
                "DualSense|01|0A:04|Mic",
                settings.BatteryGuideTriggerProfiles[WidgetSettings.DualSenseBatteryGuideProfileKey]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bloss-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
