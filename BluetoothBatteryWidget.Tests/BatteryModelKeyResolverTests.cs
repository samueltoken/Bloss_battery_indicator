using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryModelKeyResolverTests
{
    [Fact]
    public void ResolveNormalizedModelKey_PrefersIdentityVidPid()
    {
        var key = BatteryModelKeyResolver.ResolveNormalizedModelKey(
            identityVendorId: "054C",
            identityProductId: "09CC",
            transportVendorId: "045E",
            transportProductId: "02E0",
            address: "AA-BB-CC-DD-EE-FF",
            displayName: "Wireless Controller");

        Assert.Equal(GamepadProfileStore.BuildModelKey("054C", "09CC"), key);
    }

    [Fact]
    public void ResolveNormalizedModelKey_UsesTransportVidPid_WhenIdentityMissing()
    {
        var key = BatteryModelKeyResolver.ResolveNormalizedModelKey(
            identityVendorId: "",
            identityProductId: "",
            transportVendorId: "045E",
            transportProductId: "0B13",
            address: "A05A5F89E531",
            displayName: "Xbox Wireless Controller");

        Assert.Equal(GamepadProfileStore.BuildModelKey("045E", "0B13"), key);
    }

    [Fact]
    public void ResolveNormalizedModelKey_FallsBackToAddressNameFingerprint()
    {
        var keyA = BatteryModelKeyResolver.ResolveNormalizedModelKey(
            identityVendorId: "",
            identityProductId: "",
            transportVendorId: "",
            transportProductId: "",
            address: "A05A5F89E531",
            displayName: "Xbox Wireless Controller");
        var keyB = BatteryModelKeyResolver.ResolveNormalizedModelKey(
            identityVendorId: "",
            identityProductId: "",
            transportVendorId: "",
            transportProductId: "",
            address: "A05A5F89E531",
            displayName: "Xbox Wireless Controller");

        Assert.StartsWith("FP_", keyA);
        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void ResolveIdentityKey_SeparatesDevicesWithDifferentEndpointSignature()
    {
        var keyA = BatteryModelKeyResolver.ResolveIdentityKey(
            identityVendorId: "045E",
            identityProductId: "02E0",
            transportVendorId: "045E",
            transportProductId: "02E0",
            address: "A05A5F89E531",
            displayName: "Controller",
            endpointSignature: "BTHENUM|HID");
        var keyB = BatteryModelKeyResolver.ResolveIdentityKey(
            identityVendorId: "045E",
            identityProductId: "02E0",
            transportVendorId: "045E",
            transportProductId: "02E0",
            address: "A05A5F89E531",
            displayName: "Controller",
            endpointSignature: "BTHLE|HID");

        Assert.NotEqual(keyA, keyB);
        Assert.Contains("EP=", keyA, StringComparison.Ordinal);
        Assert.Contains("EP=", keyB, StringComparison.Ordinal);
    }
}
