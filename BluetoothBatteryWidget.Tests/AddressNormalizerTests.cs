using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class AddressNormalizerTests
{
    [Fact]
    public void NormalizeAddress_ReturnsExpectedHex()
    {
        var normalized = AddressNormalizer.NormalizeAddress("d6:2a:84:f9:a8:a4");
        Assert.Equal("D62A84F9A8A4", normalized);
    }

    [Fact]
    public void ExtractAddressFromInstanceId_ParsesDevPrefix()
    {
        var instanceId = @"BTHLE\DEV_D62A84F9A8A4\8&353FD73C&1&D62A84F9A8A4";
        var normalized = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
        Assert.Equal("D62A84F9A8A4", normalized);
    }

    [Fact]
    public void ExtractAddressFromInstanceId_ParsesHidBluetoothInstance()
    {
        var instanceId = @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\8&11C23AE8&0&90B685C680D8_C00000000";
        var normalized = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
        Assert.Equal("90B685C680D8", normalized);
    }
}
