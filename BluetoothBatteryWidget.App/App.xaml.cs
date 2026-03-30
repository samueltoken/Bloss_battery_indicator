using System.Windows;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Services;
using System.Threading;
using System.Threading.Tasks;

namespace BluetoothBatteryWidget.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\Bloss.Test.SingleInstance";
    private const string ActivateSignalName = @"Local\Bloss.Test.Activate";
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateSignal;
    private CancellationTokenSource? _activateSignalCts;
    private Task? _activateSignalTask;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        _activateSignal = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: ActivateSignalName);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            try
            {
                _activateSignal.Set();
            }
            catch
            {
                // Ignore activation signal failures.
            }

            Shutdown();
            return;
        }

        var settingsStore = new WidgetSettingsStore();
        var autostartService = new AutostartService();
        var connectedDeviceProvider = new WinRtConnectedDeviceProvider();
        var setupApiBatteryLevelProvider = new SetupApiBatteryLevelProvider();
        var profileStore = new GamepadProfileStore();
        var pendingCandidateStore = new PendingGamepadCandidateStore();
        var observationStore = new BatteryObservationStore();
        var calibrationStore = new CalibrationStore();
        var evidenceResolver = new BatteryEvidenceResolver(observationStore, calibrationStore);
        var gameInputBatteryProvider = new GameInputBatteryProvider();
        var learnedHidBatteryLevelProvider = new LearnedHidBatteryLevelProvider(profileStore);
        var sonyHidBatteryLevelProvider = new SonyHidBatteryLevelProvider();
        var xInputBatteryLevelProvider = new XInputBatteryLevelProvider();
        var hidFeatureBatteryProvider = new HidFeatureBatteryProvider();
        var bleBatteryServiceProvider = new BleBatteryServiceProvider();
        var batteryLevelProvider = new CompositeBatteryLevelProvider(
            setupApiBatteryLevelProvider,
            gameInputBatteryProvider,
            learnedHidBatteryLevelProvider,
            sonyHidBatteryLevelProvider,
            xInputBatteryLevelProvider,
            hidFeatureBatteryProvider,
            bleBatteryServiceProvider,
            evidenceResolver);
        var gamepadProbeService = new GamepadProbeService(profileStore, pendingCandidateStore);
        var iconResolver = new IconResolver();
        var snapshotComposer = new DeviceSnapshotComposer(iconResolver);

        var viewModel = new MainViewModel(
            connectedDeviceProvider,
            batteryLevelProvider,
            snapshotComposer,
            settingsStore,
            autostartService,
            gamepadProbeService,
            calibrationStore);

        viewModel.LoadSettingsOnce();

        var mainWindow = new MainWindow(viewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
        StartActivateSignalListener();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopActivateSignalListener();

        if (_singleInstanceMutex is not null)
        {
            if (_ownsSingleInstanceMutex)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore release failures on shutdown.
                }
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        _activateSignal?.Dispose();
        _activateSignal = null;

        base.OnExit(e);
    }

    private void StartActivateSignalListener()
    {
        if (_activateSignal is null || MainWindow is null)
        {
            return;
        }

        _activateSignalCts = new CancellationTokenSource();
        var token = _activateSignalCts.Token;
        _activateSignalTask = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _activateSignal.WaitOne();
                }
                catch
                {
                    break;
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                Dispatcher.Invoke(() =>
                {
                    if (MainWindow is null)
                    {
                        return;
                    }

                    if (!MainWindow.IsVisible)
                    {
                        MainWindow.Show();
                    }

                    if (MainWindow.WindowState == WindowState.Minimized)
                    {
                        MainWindow.WindowState = WindowState.Normal;
                    }

                    MainWindow.Activate();
                    MainWindow.Topmost = true;
                    MainWindow.Topmost = false;
                    MainWindow.Focus();
                });
            }
        }, token);
    }

    private void StopActivateSignalListener()
    {
        if (_activateSignalCts is null)
        {
            return;
        }

        try
        {
            _activateSignalCts.Cancel();
            _activateSignal?.Set();
            _activateSignalTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore shutdown listener failures.
        }
        finally
        {
            _activateSignalTask = null;
            _activateSignalCts.Dispose();
            _activateSignalCts = null;
        }
    }
}
