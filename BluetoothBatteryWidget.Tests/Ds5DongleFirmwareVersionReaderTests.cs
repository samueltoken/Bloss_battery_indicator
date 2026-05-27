using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class Ds5DongleFirmwareVersionReaderTests
{
    [Fact]
    public void TryDecodeFirmwareVersionReport_ReportIdPrefixedAsciiVersion_ReturnsVersion()
    {
        var report = new byte[64];
        report[0] = Ds5DongleFirmwareVersionReader.FirmwareVersionReportId;
        "v0.6.0-hotfix"u8.CopyTo(report.AsSpan(1));

        var decoded = Ds5DongleFirmwareVersionReader.TryDecodeFirmwareVersionReport(report, out var version);

        Assert.True(decoded);
        Assert.Equal("v0.6.0-hotfix", version);
    }

    [Fact]
    public void TryDecodeFirmwareVersionReport_UnprefixedAsciiVersion_ReturnsVersion()
    {
        var report = new byte[64];
        "0.6.1"u8.CopyTo(report);

        var decoded = Ds5DongleFirmwareVersionReader.TryDecodeFirmwareVersionReport(report, out var version);

        Assert.True(decoded);
        Assert.Equal("0.6.1", version);
    }

    [Fact]
    public void TryDecodeFirmwareVersionReport_WebHidStylePayloadWithPadding_ReturnsVersion()
    {
        var report = new byte[63];
        "v0.6.0-hotfix"u8.CopyTo(report);
        report[20] = 0;

        var decoded = Ds5DongleFirmwareVersionReader.TryDecodeFirmwareVersionReport(report, out var version);

        Assert.True(decoded);
        Assert.Equal("v0.6.0-hotfix", version);
    }

    [Fact]
    public void TryDecodeFirmwareVersionReport_NonAsciiPayload_ReturnsFalse()
    {
        var report = new byte[] { Ds5DongleFirmwareVersionReader.FirmwareVersionReportId, 0xFF, 0x01, 0x02 };

        var decoded = Ds5DongleFirmwareVersionReader.TryDecodeFirmwareVersionReport(report, out var version);

        Assert.False(decoded);
        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public void TryDecodeFirmwareVersionReport_AsciiWithoutVersionDot_ReturnsFalse()
    {
        var report = new byte[64];
        "123456"u8.CopyTo(report);

        var decoded = Ds5DongleFirmwareVersionReader.TryDecodeFirmwareVersionReport(report, out var version);

        Assert.False(decoded);
        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public void IsCandidateEndpoint_UsbDualSenseBridge_ReturnsTrue()
    {
        var endpoint = new HidGamepadEndpoint(
            DevicePath: @"\\?\hid#vid_054c&pid_0ce6&mi_03#7&sample&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
            InstanceId: @"HID\VID_054C&PID_0CE6&MI_03\7&SAMPLE&0&0000",
            Address: string.Empty,
            DisplayName: "DualSense Wireless Controller",
            VendorId: "054C",
            ProductId: "0CE6",
            DiscoveryStage: HidEndpointDiscoveryStage.GlobalAggressive);

        Assert.True(Ds5DongleFirmwareVersionReader.IsCandidateEndpoint(endpoint));
    }

    [Fact]
    public void IsCandidateEndpoint_VendorDefinedSonyUsbBridge_ReturnsTrue()
    {
        var endpoint = new HidGamepadEndpoint(
            DevicePath: @"\\?\hid#vid_054c&pid_0df2&mi_03#7&sample&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
            InstanceId: @"HID\VID_054C&PID_0DF2&MI_03&COL01\7&SAMPLE&0&0000",
            Address: string.Empty,
            DisplayName: "HID-compliant vendor-defined device",
            VendorId: "054C",
            ProductId: "0DF2",
            DiscoveryStage: HidEndpointDiscoveryStage.GlobalAggressive);

        Assert.True(Ds5DongleFirmwareVersionReader.IsCandidateEndpoint(endpoint));
    }

    [Fact]
    public void IsCandidateEndpoint_BluetoothDualSense_ReturnsFalse()
    {
        var endpoint = new HidGamepadEndpoint(
            DevicePath: @"\\?\hid#bthenum#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0ce6#sample",
            InstanceId: @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\SAMPLE",
            Address: "001122334455",
            DisplayName: "Bluetooth HID Device",
            VendorId: "054C",
            ProductId: "0CE6",
            DiscoveryStage: HidEndpointDiscoveryStage.Strict);

        Assert.False(Ds5DongleFirmwareVersionReader.IsCandidateEndpoint(endpoint));
    }

    [Fact]
    public void IsSupportedBluetoothEndpoint_BluetoothDualSense_ReturnsTrue()
    {
        var endpoint = new HidGamepadEndpoint(
            DevicePath: @"\\?\hid#bthenum#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0ce6#sample",
            InstanceId: @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\SAMPLE",
            Address: "001122334455",
            DisplayName: "Bluetooth HID Device",
            VendorId: "054C",
            ProductId: "0CE6",
            DiscoveryStage: HidEndpointDiscoveryStage.Strict);

        Assert.True(Ds5DongleFirmwareVersionReader.IsSupportedBluetoothEndpoint(endpoint));
    }

    [Theory]
    [InlineData(0, 0, 0, (int)Ds5DongleFirmwareVersionReadStatus.NoUsbDs5DongleEndpoint)]
    [InlineData(0, 0, 1, (int)Ds5DongleFirmwareVersionReadStatus.OnlyBluetoothDualSenseEndpoints)]
    [InlineData(2, 0, 0, (int)Ds5DongleFirmwareVersionReadStatus.UsbDs5DongleOpenFailed)]
    [InlineData(2, 1, 0, (int)Ds5DongleFirmwareVersionReadStatus.FirmwareVersionReportUnavailable)]
    public void ClassifyFailureStatus_ReturnsActionableStatus(
        int candidateEndpointCount,
        int openedCandidateEndpointCount,
        int bluetoothSonyEndpointCount,
        int expected)
    {
        var status = Ds5DongleFirmwareVersionReader.ClassifyFailureStatus(
            candidateEndpointCount,
            openedCandidateEndpointCount,
            bluetoothSonyEndpointCount);

        Assert.Equal((Ds5DongleFirmwareVersionReadStatus)expected, status);
    }
}
