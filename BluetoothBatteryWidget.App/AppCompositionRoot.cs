using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App;

internal static class AppCompositionRoot
{
    public static MainViewModel CreateMainViewModel()
    {
        var settingsStore = new WidgetSettingsStore();
        var autostartService = new AutostartService();
        var steamTritonReader = new SteamControllerTritonHidReader();
        var winRtConnectedDeviceProvider = new WinRtConnectedDeviceProvider();
        var connectedDeviceProvider = new CompositeConnectedDeviceProvider(
            winRtConnectedDeviceProvider,
            new PlayStationUsbConnectedDeviceProvider(),
            new SteamControllerTritonConnectedDeviceProvider(steamTritonReader));
        var setupApiBatteryLevelProvider = new SetupApiBatteryLevelProvider();
        var profileStore = new GamepadProfileStore();
        var pendingCandidateStore = new PendingGamepadCandidateStore();
        var observationStore = new BatteryObservationStore();
        var calibrationStore = new CalibrationStore();
        var evidenceResolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var batteryLevelProvider = new CompositeBatteryLevelProvider(
            setupApiBatteryLevelProvider,
            new GameInputBatteryProvider(),
            new LearnedHidBatteryLevelProvider(profileStore),
            new SonyHidBatteryLevelProvider(),
            new XInputBatteryLevelProvider(),
            new HidFeatureBatteryProvider(),
            new BleBatteryServiceProvider(),
            new SteamControllerTritonBatteryProvider(steamTritonReader),
            evidenceResolver);
        var gamepadProbeService = new GamepadProbeService(profileStore, pendingCandidateStore);
        var snapshotComposer = new DeviceSnapshotComposer(new IconResolver());

        return new MainViewModel(
            connectedDeviceProvider,
            batteryLevelProvider,
            snapshotComposer,
            settingsStore,
            autostartService,
            gamepadProbeService,
            calibrationStore);
    }
}
