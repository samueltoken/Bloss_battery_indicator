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

    private static ConnectedBluetoothDevice CreatePico2WDevice(string address)
    {
        return new ConnectedBluetoothDevice(
            "HID\\VID_054C&PID_0CE6&MI_03\\9&NEWPATH&0&0000",
            address,
            "DualSense Wireless Controller (USB/Pico2W)",
            true,
            "gamepad controller dualsense pico2w usb");
    }
}
