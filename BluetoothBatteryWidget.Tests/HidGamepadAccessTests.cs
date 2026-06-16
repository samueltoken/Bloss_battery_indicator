using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class HidGamepadAccessTests
{
    [Fact]
    public void SelectRuntimeBluetoothEndpoints_PrefersStrictEndpointWhenPathDuplicates()
    {
        var strict = Endpoint(
            path: "\\\\?\\hid#strict",
            address: "AA:BB:CC:DD:00:11",
            stage: HidEndpointDiscoveryStage.Strict,
            vendorId: "1234",
            productId: "5678");
        var relaxedDuplicate = strict with
        {
            DiscoveryStage = HidEndpointDiscoveryStage.Relaxed,
            DisplayName = "Relaxed duplicate"
        };
        var relaxedUnique = Endpoint(
            path: "\\\\?\\hid#relaxed",
            address: "AA-BB-CC-DD-00-22",
            stage: HidEndpointDiscoveryStage.Relaxed,
            vendorId: "2345",
            productId: "6789");

        var selected = HidGamepadAccess.SelectRuntimeBluetoothEndpoints(
            [relaxedDuplicate, relaxedUnique, strict],
            requireVidPid: true);

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, endpoint =>
            endpoint.DevicePath == strict.DevicePath &&
            endpoint.DiscoveryStage == HidEndpointDiscoveryStage.Strict);
        Assert.Contains(selected, endpoint =>
            endpoint.DevicePath == relaxedUnique.DevicePath &&
            endpoint.Address == "AABBCCDD0022");
    }

    [Fact]
    public void SelectRuntimeBluetoothEndpoints_WhenVidPidRequired_RemovesUnidentifiedEndpoint()
    {
        var unidentified = Endpoint(
            path: "\\\\?\\hid#no-vid",
            address: "AA:BB:CC:DD:00:33",
            stage: HidEndpointDiscoveryStage.Relaxed,
            vendorId: "",
            productId: "");
        var identified = Endpoint(
            path: "\\\\?\\hid#with-vid",
            address: "AA:BB:CC:DD:00:44",
            stage: HidEndpointDiscoveryStage.Relaxed,
            vendorId: "20BC",
            productId: "5501");

        var selected = HidGamepadAccess.SelectRuntimeBluetoothEndpoints(
            [unidentified, identified],
            requireVidPid: true);

        Assert.Single(selected);
        Assert.Equal(identified.DevicePath, selected[0].DevicePath);
    }

    [Fact]
    public void SelectRuntimeBluetoothEndpoints_WhenVidPidNotRequired_KeepsEndpointForAttributeFallback()
    {
        var unidentified = Endpoint(
            path: "\\\\?\\hid#no-vid",
            address: "AA:BB:CC:DD:00:55",
            stage: HidEndpointDiscoveryStage.Relaxed,
            vendorId: "",
            productId: "");

        var selected = HidGamepadAccess.SelectRuntimeBluetoothEndpoints(
            [unidentified],
            requireVidPid: false);

        Assert.Single(selected);
        Assert.Equal("AABBCCDD0055", selected[0].Address);
    }

    private static HidGamepadEndpoint Endpoint(
        string path,
        string address,
        HidEndpointDiscoveryStage stage,
        string vendorId,
        string productId)
    {
        return new HidGamepadEndpoint(
            DevicePath: path,
            InstanceId: $"BTHLEDEVICE\\DEV_{address}",
            Address: address,
            DisplayName: "Third Party Gamepad",
            VendorId: vendorId,
            ProductId: productId,
            DiscoveryStage: stage);
    }
}
