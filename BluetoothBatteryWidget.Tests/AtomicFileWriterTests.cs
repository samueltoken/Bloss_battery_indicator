using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public void WriteAllText_CreatesTargetWhenMissing()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "settings.json");

        try
        {
            AtomicFileWriter.WriteAllText(path, "{\"theme\":\"dark\"}");

            Assert.Equal("{\"theme\":\"dark\"}", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            Assert.Empty(Directory.GetFiles(directory, "*.bak"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WriteAllText_ReplacesExistingTargetAndCleansSwapFiles()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "gamepad-profiles.json");

        try
        {
            File.WriteAllText(path, "{\"old\":true}");

            AtomicFileWriter.WriteAllText(path, "{\"new\":true}");

            Assert.Equal("{\"new\":true}", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            Assert.Empty(Directory.GetFiles(directory, "*.bak"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void PersistentStores_UseAtomicWriterInsteadOfDirectOverwrite()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "BluetoothBatteryWidget.App", "Services", "WidgetSettingsStore.cs"),
            Path.Combine(root, "BluetoothBatteryWidget.Core", "Services", "CalibrationStore.cs"),
            Path.Combine(root, "BluetoothBatteryWidget.Core", "Services", "GamepadProfileStore.cs"),
            Path.Combine(root, "BluetoothBatteryWidget.Core", "Services", "PendingGamepadCandidateStore.cs")
        };

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("File.WriteAllText", source);
            Assert.Contains("AtomicFileWriter.WriteAllText", source);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bloss-atomic-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BluetoothBatteryWidget.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find BluetoothBatteryWidget.sln.");
    }
}
