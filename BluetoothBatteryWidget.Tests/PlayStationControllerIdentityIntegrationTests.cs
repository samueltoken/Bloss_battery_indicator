using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class PlayStationControllerIdentityIntegrationTests
{
    private const string ControllerA = "581031BA792B";
    private const string ControllerB = "A1B2C3D4E5F6";

    [Fact]
    public void Resolve_SameRefreshUsesOneRead_ThenShortCacheExpires()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var reports = new Queue<byte[]>(
        [
            DualSensePairingInfoParserTests.CreateReport(ControllerA),
            DualSensePairingInfoParserTests.CreateReport(ControllerB)
        ]);
        var readCount = 0;
        var resolver = new PlayStationControllerIdentityResolver(
            _ =>
            {
                readCount++;
                return reports.Dequeue();
            },
            () => now,
            TimeSpan.FromSeconds(5));

        var first = resolver.Resolve("instance-a", "path-a", PlayStationUsbBridgeSupport.DualSenseProductId);
        var sameRefresh = resolver.Resolve("instance-a", "path-a", PlayStationUsbBridgeSupport.DualSenseProductId);
        now = now.AddSeconds(6);
        var nextRefresh = resolver.Resolve("instance-a", "path-a", PlayStationUsbBridgeSupport.DualSenseProductId);

        Assert.Equal(ControllerA, first.ControllerAddress);
        Assert.Equal(ControllerA, sameRefresh.ControllerAddress);
        Assert.Equal(ControllerB, nextRefresh.ControllerAddress);
        Assert.Equal(2, readCount);
    }

    [Fact]
    public void Resolve_EndpointDisappears_DoesNotReusePreviousController()
    {
        var reports = new Queue<byte[]>(
        [
            DualSensePairingInfoParserTests.CreateReport(ControllerA),
            DualSensePairingInfoParserTests.CreateReport(ControllerB)
        ]);
        var resolver = new PlayStationControllerIdentityResolver(_ => reports.Dequeue());

        var first = resolver.Resolve("instance-a", "path-a", PlayStationUsbBridgeSupport.DualSenseProductId);
        resolver.RetainPresentEndpoints([]);
        var second = resolver.Resolve("instance-a", "path-a", PlayStationUsbBridgeSupport.DualSenseProductId);

        Assert.Equal(ControllerA, first.ControllerAddress);
        Assert.Equal(ControllerB, second.ControllerAddress);
    }

    [Fact]
    public void Resolve_NewEndpointGeneration_DoesNotReusePreviousController()
    {
        var reports = new Queue<byte[]>(
        [
            DualSensePairingInfoParserTests.CreateReport(ControllerA),
            DualSensePairingInfoParserTests.CreateReport(ControllerB)
        ]);
        var resolver = new PlayStationControllerIdentityResolver(_ => reports.Dequeue());

        var first = resolver.Resolve("instance-generation-a", "path-generation-a", PlayStationUsbBridgeSupport.DualSenseProductId);
        var second = resolver.Resolve("instance-generation-b", "path-generation-b", PlayStationUsbBridgeSupport.DualSenseProductId);

        Assert.Equal(ControllerA, first.ControllerAddress);
        Assert.Equal(ControllerB, second.ControllerAddress);
    }

    [Fact]
    public void Resolve_DualSenseEdge_UsesActualMacAndEdgeBridgeAddress()
    {
        var resolver = new PlayStationControllerIdentityResolver(
            _ => DualSensePairingInfoParserTests.CreateReport(ControllerA));

        var identity = resolver.Resolve("edge-instance", "edge-path", PlayStationUsbBridgeSupport.DualSenseEdgeProductId);

        Assert.True(identity.HasVerifiedControllerAddress);
        Assert.Equal(ControllerA, identity.ControllerAddress);
        Assert.Equal(PlayStationUsbBridgeSupport.StableDualSenseEdgePico2WAddress, identity.BridgeAddress);
    }

    [Fact]
    public async Task PicoAndWinRtSameController_ComposeOneActualMacRowWithBattery()
    {
        var endpoint = CreateEndpoint("path-a", "instance-a");
        var readCount = 0;
        var resolver = new PlayStationControllerIdentityResolver(_ =>
        {
            readCount++;
            return DualSensePairingInfoParserTests.CreateReport(ControllerA);
        });
        var usbProvider = new PlayStationUsbConnectedDeviceProvider(resolver, _ => [endpoint]);
        var winRtProvider = new StaticConnectedDeviceProvider(
        [
            new ConnectedBluetoothDevice(
                "winrt-aep-a",
                ControllerA,
                "DualSense Wireless Controller",
                true,
                "gamepad controller")
        ]);
        var composite = new CompositeConnectedDeviceProvider(winRtProvider, usbProvider);

        var connected = await composite.GetConnectedDevicesAsync(CancellationToken.None);
        var identityForBattery = resolver.Resolve(
            endpoint.InstanceId,
            endpoint.DevicePath,
            endpoint.ProductId);
        var readings = new[]
        {
            CreateReading(identityForBattery.ControllerAddress, 95)
        };
        var snapshots = CreateComposer().Compose(
            connected,
            readings,
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow);

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(ControllerA, snapshot.Address);
        Assert.Equal(95, snapshot.BatteryPercent);
        Assert.DoesNotContain(snapshots, item => item.Address == PlayStationUsbBridgeSupport.StableDualSensePico2WAddress);
        Assert.Equal(PlayStationUsbBridgeSupport.StableDualSensePico2WAddress, Assert.Single(connected).BridgeAddress);
        Assert.Equal(1, readCount);
    }

    [Fact]
    public async Task PairingInfoUnavailable_FallsBackToStableBridgeAddressAndBattery()
    {
        var endpoint = CreateEndpoint("path-a", "instance-a");
        var resolver = new PlayStationControllerIdentityResolver(_ => null);
        var provider = new PlayStationUsbConnectedDeviceProvider(resolver, _ => [endpoint]);

        var connected = await provider.GetConnectedDevicesAsync(CancellationToken.None);
        var device = Assert.Single(connected);
        var readings = new[] { CreateReading(device.Address, 95) };
        var snapshots = CreateComposer().Compose(
            connected,
            readings,
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow);

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(PlayStationUsbBridgeSupport.StableDualSensePico2WAddress, snapshot.Address);
        Assert.Equal(95, snapshot.BatteryPercent);
    }

    [Fact]
    public async Task DifferentBluetoothControllerWithoutBattery_IsPreserved()
    {
        var endpoint = CreateEndpoint("path-a", "instance-a");
        var resolver = new PlayStationControllerIdentityResolver(
            _ => DualSensePairingInfoParserTests.CreateReport(ControllerA));
        var usbProvider = new PlayStationUsbConnectedDeviceProvider(resolver, _ => [endpoint]);
        var secondController = new ConnectedBluetoothDevice(
            "winrt-aep-b",
            ControllerB,
            "DualSense Wireless Controller",
            true,
            "gamepad controller");
        var composite = new CompositeConnectedDeviceProvider(
            new StaticConnectedDeviceProvider([secondController]),
            usbProvider);

        var connected = await composite.GetConnectedDevicesAsync(CancellationToken.None);
        var snapshots = CreateComposer().Compose(
            connected,
            [CreateReading(ControllerA, 95)],
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow);

        Assert.Equal(2, snapshots.Count);
        Assert.Contains(snapshots, item => item.Address == ControllerA && item.BatteryPercent == 95);
        Assert.Contains(snapshots, item => item.Address == ControllerB && item.BatteryPercent is null);
    }

    [Fact]
    public void ControllerProfiles_FollowActualMacWhenControllersAlternate()
    {
        var composer = CreateComposer();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ControllerA] = "blue"
        };
        var icons = new Dictionary<string, IconKey>(StringComparer.OrdinalIgnoreCase)
        {
            [ControllerA] = IconKey.Mouse
        };

        var controllerA = ComposeSingle(composer, ControllerA, names, icons);
        var controllerBBeforeRename = ComposeSingle(composer, ControllerB, names, icons);
        names[ControllerB] = "red";
        icons[ControllerB] = IconKey.Keyboard;
        var controllerBAfterRename = ComposeSingle(composer, ControllerB, names, icons);
        var controllerAReconnected = ComposeSingle(composer, ControllerA, names, icons);

        Assert.Equal("blue", controllerA.DisplayName);
        Assert.Equal(IconKey.Mouse, controllerA.IconKey);
        Assert.Equal("DualSense Wireless Controller", controllerBBeforeRename.DisplayName);
        Assert.Equal(IconKey.Gamepad, controllerBBeforeRename.IconKey);
        Assert.Equal("red", controllerBAfterRename.DisplayName);
        Assert.Equal(IconKey.Keyboard, controllerBAfterRename.IconKey);
        Assert.Equal("blue", controllerAReconnected.DisplayName);
        Assert.Equal(IconKey.Mouse, controllerAReconnected.IconKey);
    }

    [Fact]
    public async Task TwoPicoEndpoints_KeepTwoActualMacRowsAndBatteryReadings()
    {
        var endpointA = CreateEndpoint("path-a", "instance-a");
        var endpointB = CreateEndpoint("path-b", "instance-b");
        var resolver = new PlayStationControllerIdentityResolver(path =>
            DualSensePairingInfoParserTests.CreateReport(
                path.Equals("path-a", StringComparison.OrdinalIgnoreCase) ? ControllerA : ControllerB));
        var provider = new PlayStationUsbConnectedDeviceProvider(resolver, _ => [endpointA, endpointB]);

        var connected = await provider.GetConnectedDevicesAsync(CancellationToken.None);
        var snapshots = CreateComposer().Compose(
            connected,
            [CreateReading(ControllerA, 95), CreateReading(ControllerB, 70)],
            new Dictionary<string, IconKey>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow);

        Assert.Equal(2, snapshots.Count);
        Assert.Contains(snapshots, item => item.Address == ControllerA && item.BatteryPercent == 95);
        Assert.Contains(snapshots, item => item.Address == ControllerB && item.BatteryPercent == 70);
        Assert.DoesNotContain(snapshots, item => item.Address == PlayStationUsbBridgeSupport.StableDualSensePico2WAddress);
    }

    private static DeviceBatterySnapshot ComposeSingle(
        DeviceSnapshotComposer composer,
        string address,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<string, IconKey> icons)
    {
        var connected = new[]
        {
            new ConnectedBluetoothDevice(
                $"usb-{address}",
                address,
                "DualSense Wireless Controller",
                true,
                "gamepad controller dualsense pico2w usb",
                PlayStationUsbBridgeSupport.StableDualSensePico2WAddress)
        };
        var snapshots = composer.Compose(
            connected,
            [CreateReading(address, 90)],
            icons,
            new Dictionary<string, string>(),
            names,
            DateTimeOffset.UtcNow);
        return Assert.Single(snapshots);
    }

    private static HidGamepadEndpoint CreateEndpoint(string path, string instanceId)
    {
        return new HidGamepadEndpoint(
            path,
            instanceId,
            string.Empty,
            "DualSense Wireless Controller",
            PlayStationUsbBridgeSupport.SonyVendorId,
            PlayStationUsbBridgeSupport.DualSenseProductId,
            HidEndpointDiscoveryStage.GlobalAggressive);
    }

    private static PnpBatteryReading CreateReading(string address, int batteryPercent)
    {
        return new PnpBatteryReading(
            $"hid-{address}",
            address,
            "DualSense Wireless Controller",
            batteryPercent,
            SourceKind: BatterySourceKind.SonyHid);
    }

    private static DeviceSnapshotComposer CreateComposer()
    {
        return new DeviceSnapshotComposer(new IconResolver());
    }

    private sealed class StaticConnectedDeviceProvider : IConnectedDeviceProvider
    {
        private readonly IReadOnlyList<ConnectedBluetoothDevice> _devices;

        public StaticConnectedDeviceProvider(IReadOnlyList<ConnectedBluetoothDevice> devices)
        {
            _devices = devices;
        }

        public Task<IReadOnlyList<ConnectedBluetoothDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_devices);
        }
    }
}
