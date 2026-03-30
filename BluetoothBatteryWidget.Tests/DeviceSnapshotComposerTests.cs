using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class DeviceSnapshotComposerTests
{
    [Fact]
    public void Compose_SortsByLowestBattery_AndKeepsNaLast()
    {
        var composer = new DeviceSnapshotComposer(new IconResolver());
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("a", "AA1122334455", "Keyboard", true, "Input.Keyboard"),
            new("b", "BB1122334455", "Mouse", true, "Input.Mouse"),
            new("c", "CC1122334455", "Gamepad", true, "Input.GameController")
        };

        var battery = new List<PnpBatteryReading>
        {
            new("b", "BB1122334455", "Mouse", 55),
            new("a", "AA1122334455", "Keyboard", 22)
        };

        var snapshots = composer.Compose(
            connected,
            battery,
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow);

        Assert.Equal(3, snapshots.Count);
        Assert.Equal("Keyboard", snapshots[0].DisplayName);
        Assert.Equal("Mouse", snapshots[1].DisplayName);
        Assert.Equal("Gamepad", snapshots[2].DisplayName);
        Assert.Null(snapshots[2].BatteryPercent);
    }

    [Fact]
    public void Compose_PreservesBatteryConfidence()
    {
        var composer = new DeviceSnapshotComposer(new IconResolver());
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("a", "AA1122334455", "Gamepad", true, "Input.GameController")
        };
        var battery = new List<PnpBatteryReading>
        {
            new("learned", "AA1122334455", "Gamepad", 61, BatteryConfidence.Estimated)
        };

        var snapshots = composer.Compose(
            connected,
            battery,
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow);

        Assert.Single(snapshots);
        Assert.Equal(BatteryConfidence.Estimated, snapshots[0].BatteryConfidence);
    }

    [Fact]
    public void Compose_AppliesNameOverride_WithoutChangingCategory()
    {
        var composer = new DeviceSnapshotComposer(new IconResolver());
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("g", "AA1122334455", "Xbox Wireless Controller", true, "Input.GameController")
        };
        var battery = new List<PnpBatteryReading>();
        var nameOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AA1122334455"] = "내꺼"
        };

        var snapshots = composer.Compose(
            connected,
            battery,
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            nameOverrides,
            DateTimeOffset.UtcNow);

        Assert.Single(snapshots);
        Assert.Equal("내꺼", snapshots[0].DisplayName);
        Assert.Equal("Xbox Wireless Controller", snapshots[0].BaseDisplayName);
        Assert.Equal(DeviceCategory.Gamepad, snapshots[0].Category);
    }

    [Fact]
    public void Compose_AppliesCustomIconImagePath_WhenFileExists()
    {
        var composer = new DeviceSnapshotComposer(new IconResolver());
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("g", "AA1122334455", "Xbox Wireless Controller", true, "Input.GameController")
        };
        var battery = new List<PnpBatteryReading>();
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tempFilePath, [0x89, 0x50, 0x4E, 0x47]);

        try
        {
            var imageOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AA1122334455"] = tempFilePath
            };

            var snapshots = composer.Compose(
                connected,
                battery,
                new Dictionary<string, IconKey>(),
                imageOverrides,
                new Dictionary<string, string>(),
                DateTimeOffset.UtcNow);

            Assert.Single(snapshots);
            Assert.Equal(tempFilePath, snapshots[0].CustomIconImagePath);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }
}
