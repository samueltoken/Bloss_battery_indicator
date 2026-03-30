using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class AddressNormalizerTests
{
    [Fact]
    public void NormalizeAddress_ReturnsExpectedHex()
    {
        var normalized = AddressNormalizer.NormalizeAddress("aa:bb:cc:dd:ee:ff");
        Assert.Equal("AABBCCDDEEFF", normalized);
    }

    [Fact]
    public void ExtractAddressFromInstanceId_ParsesDevPrefix()
    {
        var instanceId = @"BTHLE\DEV_AABBCCDDEEFF\8&353FD73C&1&AABBCCDDEEFF";
        var normalized = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
        Assert.Equal("AABBCCDDEEFF", normalized);
    }

    [Fact]
    public void ExtractAddressFromInstanceId_ParsesHidBluetoothInstance()
    {
        var instanceId = @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\8&11C23AE8&0&112233445566_C00000000";
        var normalized = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
        Assert.Equal("112233445566", normalized);
    }
}
