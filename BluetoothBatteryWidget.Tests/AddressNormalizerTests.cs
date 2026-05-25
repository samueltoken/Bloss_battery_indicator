using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class AddressNormalizerTests
{
    [Fact]
    public void NormalizeAddress_ReturnsExpectedHex()
    {
        var normalized = AddressNormalizer.NormalizeAddress("aa:bb:cc:dd:e0:01");
        Assert.Equal("AABBCCDDE001", normalized);
    }

    [Fact]
    public void ExtractAddressFromInstanceId_ParsesDevPrefix()
    {
        var instanceId = @"BTHLE\DEV_AABBCCDDE001\8&353FD73C&1&AABBCCDDE001";
        var normalized = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
        Assert.Equal("AABBCCDDE001", normalized);
    }

    [Fact]
    public void ExtractAddressFromInstanceId_ParsesHidBluetoothInstance()
    {
        var instanceId = @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\8&11C23AE8&0&AABBCCDDE002_C00000000";
        var normalized = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
        Assert.Equal("AABBCCDDE002", normalized);
    }
}
