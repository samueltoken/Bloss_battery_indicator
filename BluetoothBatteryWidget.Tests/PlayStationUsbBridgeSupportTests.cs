using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class PlayStationUsbBridgeSupportTests
{
    [Fact]
    public void IsSupportedUsbDualSenseEndpoint_UsbHidDualSense_ReturnsTrue()
    {
        var instanceId = @"HID\VID_054C&PID_0CE6&MI_03\8&1A2B3C4D&0&0000";
        var parentId = @"USB\VID_054C&PID_0CE6&MI_03\7&5E6F7A8B&0&0003";
        var path = @"\\?\hid#vid_054c&pid_0ce6&mi_03#8&13c03f06&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

        var supported = PlayStationUsbBridgeSupport.IsSupportedUsbDualSenseEndpoint(
            instanceId,
            parentId,
            path,
            "054C",
            "0CE6");

        Assert.True(supported);
    }

    [Fact]
    public void IsSupportedUsbDualSenseEndpoint_BluetoothDualSense_ReturnsFalse()
    {
        var instanceId = @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\8&11C23AE8&0&AABBCCDDE006_C00000000";

        var supported = PlayStationUsbBridgeSupport.IsSupportedUsbDualSenseEndpoint(
            instanceId,
            parentInstanceId: null,
            devicePath: null,
            "054C",
            "0CE6");

        Assert.False(supported);
    }

    [Fact]
    public void BuildSyntheticAddress_SameEndpoint_ReturnsStableTwelveHexAddress()
    {
        var instanceId = @"HID\VID_054C&PID_0CE6&MI_03\8&1A2B3C4D&0&0000";
        var path = @"\\?\hid#vid_054c&pid_0ce6&mi_03#8&13c03f06&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

        var first = PlayStationUsbBridgeSupport.BuildSyntheticAddress(instanceId, path);
        var second = PlayStationUsbBridgeSupport.BuildSyntheticAddress(instanceId, path);

        Assert.Equal(first, second);
        Assert.Equal(12, first.Length);
        Assert.Equal(first, AddressNormalizer.NormalizeAddress(first));
    }

    [Fact]
    public void Compose_UsbPicoDualSenseConnectedWithSonyHidReading_ShowsBattery()
    {
        var address = PlayStationUsbBridgeSupport.BuildSyntheticAddress(
            @"HID\VID_054C&PID_0CE6&MI_03\8&1A2B3C4D&0&0000",
            @"\\?\hid#vid_054c&pid_0ce6&mi_03#8&13c03f06&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}");
        var connected = new List<ConnectedBluetoothDevice>
        {
            new(
                "usb-dualsense",
                address,
                "DualSense Wireless Controller (USB/Pico2W)",
                true,
                "gamepad controller dualsense pico2w usb")
        };
        var readings = new List<PnpBatteryReading>
        {
            new(
                "usb-dualsense",
                address,
                "DualSense Wireless Controller (USB/Pico2W)",
                85,
                SourceKind: BatterySourceKind.SonyHid)
        };
        var composer = new DeviceSnapshotComposer(new IconResolver());

        var snapshots = composer.Compose(
            connected,
            readings,
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow);

        Assert.Single(snapshots);
        Assert.Equal(85, snapshots[0].BatteryPercent);
        Assert.Equal(DeviceCategory.Gamepad, snapshots[0].Category);
        Assert.Equal(BatterySourceKind.SonyHid, snapshots[0].SourceKind);
    }
}
