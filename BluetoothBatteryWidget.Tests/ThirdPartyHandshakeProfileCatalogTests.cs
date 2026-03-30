using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class ThirdPartyHandshakeProfileCatalogTests
{
    [Fact]
    public void Resolve_GuliKitDisplayName_UsesGuliKitProfile()
    {
        var profile = ThirdPartyHandshakeProfileCatalog.Resolve(
            vendorId: "045E",
            productId: "0B13",
            displayName: "GuliKit Controller XW",
            endpointSignal: "BTHENUM");

        Assert.Equal("brand.gulikit", profile.ProfileId);
        Assert.NotEmpty(profile.RecoveryInputReportIds);
    }

    [Fact]
    public void Resolve_XboxVidPid_UsesXboxLayerProfile()
    {
        var profile = ThirdPartyHandshakeProfileCatalog.Resolve(
            vendorId: "045E",
            productId: "0B13",
            displayName: "Xbox Wireless Controller",
            endpointSignal: string.Empty);

        Assert.Equal("xbox.layer", profile.ProfileId);
    }

    [Fact]
    public void Resolve_UnknownDevice_FallsBackToDefaultProfile()
    {
        var profile = ThirdPartyHandshakeProfileCatalog.Resolve(
            vendorId: "FFFF",
            productId: "EEEE",
            displayName: "Unknown Controller",
            endpointSignal: "UNKNOWN");

        Assert.Equal("generic.default", profile.ProfileId);
        Assert.NotEmpty(profile.RecoveryInputReportIds);
    }

    [Fact]
    public void ResolveSelection_GameSirVendor_UsesGameSirProfile()
    {
        var selection = ThirdPartyHandshakeProfileCatalog.ResolveSelection(
            vendorId: "20BC",
            productId: "5500",
            displayName: "Xbox Wireless Controller",
            endpointSignal: "BTHENUM",
            deviceAddress: "11:22:33:44:55:66");

        Assert.Equal("brand.gamesir", selection.Profile.ProfileId);
        Assert.Equal("gamesir", selection.BrandHint);
        Assert.Contains("brand:", selection.ProfileSelectionReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSelection_KnownEasySmxOui_UsesEasySmxProfile()
    {
        var selection = ThirdPartyHandshakeProfileCatalog.ResolveSelection(
            vendorId: "FFFF",
            productId: "EEEE",
            displayName: "Wireless Controller",
            endpointSignal: string.Empty,
            deviceAddress: "A0:5A:5F:89:E5:31");

        Assert.Equal("brand.easysmx", selection.Profile.ProfileId);
        Assert.Equal("easysmx", selection.BrandHint);
    }
}
