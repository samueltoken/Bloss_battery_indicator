using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class FirmwareUpdateOverrideMigrationTests
{
    [Fact]
    public void TryCopyPico2WOverridesToStableAddress_CopiesSingleOldAddressOverrides()
    {
        const string oldAddress = "AABBCCDDE020";
        var settings = new WidgetSettings();
        settings.NameOverrides[oldAddress] = "내 듀얼센스";
        settings.IconOverrides[oldAddress] = IconKey.Gamepad.ToString();
        settings.IconImageOverrides[oldAddress] = @"C:\Bloss\Icons\dualsense.png";
        var connectedDevices = new List<ConnectedBluetoothDevice>
        {
            CreatePico2WDevice(PlayStationUsbBridgeSupport.StableDualSensePico2WAddress)
        };

        var changed = FirmwareUpdateOverrideMigration.TryCopyPico2WOverridesToStableAddress(
            settings,
            connectedDevices);

        Assert.True(changed);
        Assert.Equal("내 듀얼센스", settings.NameOverrides[PlayStationUsbBridgeSupport.StableDualSensePico2WAddress]);
        Assert.Equal(IconKey.Gamepad.ToString(), settings.IconOverrides[PlayStationUsbBridgeSupport.StableDualSensePico2WAddress]);
        Assert.Equal(
            @"C:\Bloss\Icons\dualsense.png",
            settings.IconImageOverrides[PlayStationUsbBridgeSupport.StableDualSensePico2WAddress]);
        Assert.Equal("내 듀얼센스", settings.NameOverrides[oldAddress]);
    }

    [Fact]
    public void TryCopyPico2WOverridesToStableAddress_DoesNotOverwriteExistingStableOverride()
    {
        const string oldAddress = "AABBCCDDE020";
        var settings = new WidgetSettings();
        settings.NameOverrides[oldAddress] = "예전 이름";
        settings.NameOverrides[PlayStationUsbBridgeSupport.StableDualSensePico2WAddress] = "새 이름";
        var connectedDevices = new List<ConnectedBluetoothDevice>
        {
            CreatePico2WDevice(PlayStationUsbBridgeSupport.StableDualSensePico2WAddress)
        };

        var changed = FirmwareUpdateOverrideMigration.TryCopyPico2WOverridesToStableAddress(
            settings,
            connectedDevices);

        Assert.False(changed);
        Assert.Equal("새 이름", settings.NameOverrides[PlayStationUsbBridgeSupport.StableDualSensePico2WAddress]);
    }

    [Fact]
    public void TryCopyPico2WOverridesToStableAddress_DoesNotGuessWhenMultipleOldAddressesExist()
    {
        var settings = new WidgetSettings();
        settings.NameOverrides["AABBCCDDE020"] = "첫 번째";
        settings.NameOverrides["AABBCCDDE021"] = "두 번째";
        var connectedDevices = new List<ConnectedBluetoothDevice>
        {
            CreatePico2WDevice(PlayStationUsbBridgeSupport.StableDualSensePico2WAddress)
        };

        var changed = FirmwareUpdateOverrideMigration.TryCopyPico2WOverridesToStableAddress(
            settings,
            connectedDevices);

        Assert.False(changed);
        Assert.False(settings.NameOverrides.ContainsKey(PlayStationUsbBridgeSupport.StableDualSensePico2WAddress));
    }

    [Fact]
    public void TryMigratePico2WOverridesToControllerAddress_CopiesStableProfileOnce()
    {
        const string controllerAddress = "581031BA792B";
        var bridgeAddress = PlayStationUsbBridgeSupport.StableDualSensePico2WAddress;
        var settings = new WidgetSettings();
        settings.NameOverrides[bridgeAddress] = "blue";
        settings.IconOverrides[bridgeAddress] = IconKey.Gamepad.ToString();
        settings.IconImageOverrides[bridgeAddress] = @"C:\Bloss\Icons\blue.png";
        var connectedDevices = new List<ConnectedBluetoothDevice>
        {
            CreatePico2WDevice(controllerAddress, bridgeAddress)
        };

        var changed = FirmwareUpdateOverrideMigration.TryMigratePico2WOverridesToControllerAddress(
            settings,
            connectedDevices);

        Assert.True(changed);
        Assert.Equal("blue", settings.NameOverrides[controllerAddress]);
        Assert.Equal(IconKey.Gamepad.ToString(), settings.IconOverrides[controllerAddress]);
        Assert.Equal(@"C:\Bloss\Icons\blue.png", settings.IconImageOverrides[controllerAddress]);
        Assert.Contains(bridgeAddress, settings.Pico2WProfileMigrationCompletedBridgeIds);
        Assert.Equal("blue", settings.NameOverrides[bridgeAddress]);
    }

    [Fact]
    public void TryMigratePico2WOverridesToControllerAddress_DoesNotCopyStableProfileToNextController()
    {
        const string controllerA = "581031BA792B";
        const string controllerB = "A1B2C3D4E5F6";
        var bridgeAddress = PlayStationUsbBridgeSupport.StableDualSensePico2WAddress;
        var settings = new WidgetSettings();
        settings.NameOverrides[bridgeAddress] = "blue";

        Assert.True(FirmwareUpdateOverrideMigration.TryMigratePico2WOverridesToControllerAddress(
            settings,
            [CreatePico2WDevice(controllerA, bridgeAddress)]));
        var changedForControllerB = FirmwareUpdateOverrideMigration.TryMigratePico2WOverridesToControllerAddress(
            settings,
            [CreatePico2WDevice(controllerB, bridgeAddress)]);

        Assert.False(changedForControllerB);
        Assert.Equal("blue", settings.NameOverrides[controllerA]);
        Assert.False(settings.NameOverrides.ContainsKey(controllerB));
    }

    [Fact]
    public void TryMigratePico2WOverridesToControllerAddress_DoesNotOverwriteActualMacProfile()
    {
        const string controllerAddress = "581031BA792B";
        var bridgeAddress = PlayStationUsbBridgeSupport.StableDualSensePico2WAddress;
        var settings = new WidgetSettings();
        settings.NameOverrides[bridgeAddress] = "old bridge name";
        settings.NameOverrides[controllerAddress] = "blue";

        var changed = FirmwareUpdateOverrideMigration.TryMigratePico2WOverridesToControllerAddress(
            settings,
            [CreatePico2WDevice(controllerAddress, bridgeAddress)]);

        Assert.True(changed);
        Assert.Equal("blue", settings.NameOverrides[controllerAddress]);
        Assert.Contains(bridgeAddress, settings.Pico2WProfileMigrationCompletedBridgeIds);
    }

    [Fact]
    public void TryMigratePico2WOverridesToControllerAddress_MultiplePicoMacsRemainAmbiguous()
    {
        var bridgeAddress = PlayStationUsbBridgeSupport.StableDualSensePico2WAddress;
        var settings = new WidgetSettings();
        settings.NameOverrides[bridgeAddress] = "old bridge name";

        var changed = FirmwareUpdateOverrideMigration.TryMigratePico2WOverridesToControllerAddress(
            settings,
            [
                CreatePico2WDevice("581031BA792B", bridgeAddress),
                CreatePico2WDevice("A1B2C3D4E5F6", bridgeAddress)
            ]);

        Assert.False(changed);
        Assert.False(settings.NameOverrides.ContainsKey("581031BA792B"));
        Assert.False(settings.NameOverrides.ContainsKey("A1B2C3D4E5F6"));
        Assert.Empty(settings.Pico2WProfileMigrationCompletedBridgeIds);
    }

    private static ConnectedBluetoothDevice CreatePico2WDevice(string address, string? bridgeAddress = null)
    {
        return new ConnectedBluetoothDevice(
            "HID\\VID_054C&PID_0CE6&MI_03\\9&NEWPATH&0&0000",
            address,
            "DualSense Wireless Controller (USB/Pico2W)",
            true,
            "gamepad controller dualsense pico2w usb",
            bridgeAddress);
    }
}
