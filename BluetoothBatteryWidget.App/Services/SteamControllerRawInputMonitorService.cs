using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class GlobalHumanInputEventArgs(string source, bool countsAsUserActivity, bool isWakeEligible)
    : EventArgs
{
    public string Source { get; } = source;

    public bool CountsAsUserActivity { get; } = countsAsUserActivity;

    public bool IsWakeEligible { get; } = isWakeEligible;
}

internal sealed class SteamControllerRawInputMonitorService : IDisposable
{
    private enum RawInputMonitorMode
    {
        None,
        Normal,
        HumanInputOnly,
        WakeOnly
    }

    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidiDeviceInfo = 0x2000000B;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevRemove = 0x00000001;
    private const uint RidevPageOnly = 0x00000020;
    private const ushort UsagePageGenericDesktop = 0x01;
    private const ushort UsagePageVendorSteam = 0xFF00;
    private const ushort UsageMouse = 0x02;
    private const ushort UsageJoystick = 0x04;
    private const ushort UsageGamepad = 0x05;
    private const ushort UsageKeyboard = 0x06;
    private const uint RimTypeMouse = 0;
    private const uint RimTypeKeyboard = 1;
    private const uint RimTypeHid = 2;
    private const ushort RiKeyBreak = 0x0001;
    private static readonly TimeSpan PressDebounce = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan DiagnosticRepeatInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ShortPressMinimumDuration = TimeSpan.FromMilliseconds(35);
    private static readonly TimeSpan ShortPressMaximumDuration = TimeSpan.FromMilliseconds(2200);
    private static readonly TimeSpan RawHidFastShortPressMaximumDuration = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan RawHidFastReleaseStabilityDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan RawHidCautiousReleaseStabilityDelay = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan RawHidStalePressedResetGap = TimeSpan.FromMilliseconds(1800);
    private static readonly TimeSpan RawHidMissedReleaseRecoveryGap = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan KnownRawInputDeviceCacheDuration = TimeSpan.FromMilliseconds(600);

    private readonly object _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _lastPressByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenRawHidInputByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _lastRawHidReportByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ulong> _lastRawInputReportActionSignatureByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _lastRawHidGuidePressedByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _rawHidGuideNeutralReportCountByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GuideButtonDeviceKind, BatteryGuideTrigger> _activeBatteryGuideTriggers = new();
    private readonly SteamRawHidGuideButtonStateTracker _rawHidGuideButtonStateTracker = new(
        ShortPressMinimumDuration,
        ShortPressMaximumDuration,
        RawHidFastShortPressMaximumDuration,
        RawHidFastReleaseStabilityDelay,
        RawHidCautiousReleaseStabilityDelay,
        RawHidStalePressedResetGap,
        RawHidMissedReleaseRecoveryGap);
    private readonly Dictionary<string, DateTimeOffset> _lastDiagnosticByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<IntPtr, string> _rawInputDeviceNameByHandle = new();
    private Func<IReadOnlyList<GuideButtonKnownDevice>> _knownDeviceProvider = static () => [];
    private IReadOnlyList<GuideButtonKnownDevice> _cachedKnownRawInputGuideDevices = [];
    private DateTimeOffset _cachedKnownRawInputGuideDevicesUntilUtc = DateTimeOffset.MinValue;
    private bool _isRegistered;
    private RawInputMonitorMode _monitorMode = RawInputMonitorMode.None;
    private bool _isDetailedInputReportMode;
    private bool _steamRawHidBaselineReadyNotified;
    private bool _disposed;

    public event EventHandler<GuideButtonPressedEventArgs>? GuideButtonPressed;
    public event EventHandler<GuideButtonInputReportEventArgs>? InputReportReceived;
    public event EventHandler<GuideButtonActivityEventArgs>? InputActivityReceived;
    public event EventHandler<GlobalHumanInputEventArgs>? GlobalHumanInputReceived;
    public event EventHandler? SteamRawHidBaselineReady;

    public bool IsRegistered
    {
        get
        {
            lock (_sync)
            {
                return _isRegistered && !_disposed;
            }
        }
    }

    public bool IsNormalMode
    {
        get
        {
            lock (_sync)
            {
                return _isRegistered && !_disposed && _monitorMode == RawInputMonitorMode.Normal;
            }
        }
    }

    public bool IsWakeOnlyMode
    {
        get
        {
            lock (_sync)
            {
                return _isRegistered && !_disposed && _monitorMode == RawInputMonitorMode.WakeOnly;
            }
        }
    }

    public bool IsHumanInputOnlyMode
    {
        get
        {
            lock (_sync)
            {
                return _isRegistered && !_disposed && _monitorMode == RawInputMonitorMode.HumanInputOnly;
            }
        }
    }

    public bool HasSteamRawHidBaseline
    {
        get
        {
            lock (_sync)
            {
                return _seenRawHidInputByAddress.Count > 0;
            }
        }
    }

    public void SetKnownDeviceProvider(Func<IReadOnlyList<GuideButtonKnownDevice>> provider)
    {
        lock (_sync)
        {
            _knownDeviceProvider = provider ?? (static () => []);
        }
    }

    public SteamRawHidGuideButtonActivity GetGuideButtonActivity(string address)
    {
        address = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return SteamRawHidGuideButtonActivity.None;
        }

        lock (_sync)
        {
            var now = DateTimeOffset.Now;
            return _rawHidGuideButtonStateTracker.GetActivity(address, now);
        }
    }

    public bool ClearGuideButtonActivity(string address)
    {
        address = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        lock (_sync)
        {
            return _rawHidGuideButtonStateTracker.ClearActivity(address, DateTimeOffset.Now);
        }
    }

    public bool HasStableNeutralGuideBaseline(string address)
    {
        address = AddressNormalizer.NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        lock (_sync)
        {
            return _rawHidGuideNeutralReportCountByAddress.TryGetValue(address, out var count) && count >= 2;
        }
    }

    public void Start(IntPtr windowHandle)
    {
        StartCore(windowHandle, RawInputMonitorMode.Normal);
    }

    public void StartWakeOnly(IntPtr windowHandle)
    {
        StartCore(windowHandle, RawInputMonitorMode.WakeOnly);
    }

    public void StartHumanInputOnly(IntPtr windowHandle)
    {
        StartCore(windowHandle, RawInputMonitorMode.HumanInputOnly);
    }

    public void SetDetailedInputReportMode(bool isDetailed)
    {
        lock (_sync)
        {
            _isDetailedInputReportMode = isDetailed;
        }
    }

    public void SetActiveBatteryGuideTriggers(IReadOnlyDictionary<GuideButtonDeviceKind, BatteryGuideTrigger> triggers)
    {
        lock (_sync)
        {
            _activeBatteryGuideTriggers.Clear();
            foreach (var pair in triggers)
            {
                _activeBatteryGuideTriggers[pair.Key] = pair.Value;
            }

            _lastRawInputReportActionSignatureByDevice.Clear();
        }
    }

    private void StartCore(IntPtr windowHandle, RawInputMonitorMode mode)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var alreadyRegistered = false;
        var shouldStopExisting = false;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            alreadyRegistered = _isRegistered && _monitorMode == mode;
            shouldStopExisting = _isRegistered && _monitorMode != mode;
        }

        if (alreadyRegistered)
        {
            return;
        }

        if (shouldStopExisting)
        {
            Stop();
        }

        var devices = mode switch
        {
            RawInputMonitorMode.WakeOnly => BuildWakeOnlyRawInputDevices(windowHandle),
            RawInputMonitorMode.HumanInputOnly => BuildHumanInputOnlyRawInputDevices(windowHandle),
            _ => BuildNormalRawInputDevices(windowHandle)
        };

        var registered = RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RawInputDevice>());

        lock (_sync)
        {
            _isRegistered = registered;
            _monitorMode = registered ? mode : RawInputMonitorMode.None;
        }

        GuideButtonEventLog.Write(
            registered ? "raw_input_started" : "raw_input_start_failed",
            "SteamController",
            string.Empty,
            "Steam Controller",
            registered
                ? mode == RawInputMonitorMode.WakeOnly
                    ? "Steam Controller wake-only raw gamepad/HID/vendor-page monitor started."
                    : mode == RawInputMonitorMode.HumanInputOnly
                        ? "Global keyboard/mouse raw input monitor started for power-idle recovery."
                    : "Steam Controller raw vendor-page HID monitor started."
                : $"Steam Controller raw input registration failed. win32={Marshal.GetLastWin32Error()}.");

        LogRawDeviceSummary();
    }

    public void Stop()
    {
        bool shouldUnregister;
        lock (_sync)
        {
            shouldUnregister = _isRegistered && !_disposed;
            _isRegistered = false;
            _monitorMode = RawInputMonitorMode.None;
            _lastPressByAddress.Clear();
            _seenRawHidInputByAddress.Clear();
            _lastRawHidReportByAddress.Clear();
            _lastRawInputReportActionSignatureByDevice.Clear();
            _lastRawHidGuidePressedByAddress.Clear();
            _rawHidGuideNeutralReportCountByAddress.Clear();
            _rawHidGuideButtonStateTracker.Clear();
            _lastRawInputReportActionSignatureByDevice.Clear();
            _rawInputDeviceNameByHandle.Clear();
            _cachedKnownRawInputGuideDevices = [];
            _cachedKnownRawInputGuideDevicesUntilUtc = DateTimeOffset.MinValue;
            _steamRawHidBaselineReadyNotified = false;
        }

        if (!shouldUnregister)
        {
            return;
        }

        var devices = new[]
        {
            BuildRawInputRemovalDevice(UsagePageGenericDesktop, UsageMouse),
            BuildRawInputRemovalDevice(UsagePageGenericDesktop, UsageJoystick),
            BuildRawInputRemovalDevice(UsagePageGenericDesktop, UsageGamepad),
            BuildRawInputRemovalDevice(UsagePageGenericDesktop, UsageKeyboard),
            BuildRawInputPageRemovalDevice(UsagePageVendorSteam)
        };

        _ = RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RawInputDevice>());

        GuideButtonEventLog.Write(
            "raw_input_paused",
            "SteamController",
            string.Empty,
            "Steam Controller",
            "Steam Controller raw input monitor paused for power idle.");
    }

    public IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmInput || lParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        bool isRegistered;
        lock (_sync)
        {
            isRegistered = _isRegistered && !_disposed;
        }

        if (!isRegistered)
        {
            return IntPtr.Zero;
        }

        TryProcessRawInput(lParam);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _isRegistered = false;
            _monitorMode = RawInputMonitorMode.None;
            _knownDeviceProvider = static () => [];
            _lastPressByAddress.Clear();
            _seenRawHidInputByAddress.Clear();
            _lastRawHidReportByAddress.Clear();
            _lastRawInputReportActionSignatureByDevice.Clear();
            _lastRawHidGuidePressedByAddress.Clear();
            _rawHidGuideNeutralReportCountByAddress.Clear();
            _rawHidGuideButtonStateTracker.Clear();
            _lastDiagnosticByKey.Clear();
            _lastRawInputReportActionSignatureByDevice.Clear();
            _activeBatteryGuideTriggers.Clear();
            _rawInputDeviceNameByHandle.Clear();
            _cachedKnownRawInputGuideDevices = [];
            _cachedKnownRawInputGuideDevicesUntilUtc = DateTimeOffset.MinValue;
            _steamRawHidBaselineReadyNotified = false;
        }
    }

    public void LogRawDeviceSummary()
    {
        if (!RuntimeDiagnostics.IsFileLoggingEnabled)
        {
            return;
        }

        WriteRawDeviceSummary();
    }

    private void TryProcessRawInput(IntPtr rawInputHandle)
    {
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        var size = 0u;
        var queryResult = GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref size, headerSize);
        if (queryResult == uint.MaxValue || size == 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var read = GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize);
            if (read == uint.MaxValue || read != size)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            var deviceName = GetCachedRawInputDeviceName(header.Device);
            var knownGuideDevices = ReadKnownRawInputGuideDevices();
            var isSteamCandidate = IsSteamCandidateDeviceName(deviceName, knownGuideDevices);
            var isWakeOnly = IsWakeOnlyMode;
            var isHumanInputOnly = IsHumanInputOnlyMode;
            if (isHumanInputOnly)
            {
                if (TryCreateGlobalHumanInputEvent(buffer, headerSize, header.Type, out var humanOnlyInput))
                {
                    GlobalHumanInputReceived?.Invoke(this, humanOnlyInput);
                }

                return;
            }

            if (!isSteamCandidate &&
                !isWakeOnly &&
                TryCreateGlobalHumanInputEvent(buffer, headerSize, header.Type, out var globalInput))
            {
                GlobalHumanInputReceived?.Invoke(this, globalInput);
            }

            if (!TryResolveSteamDevice(deviceName, knownGuideDevices, out var matchedDevice, out _))
            {
                WriteUnmatchedSteamInputDiagnostic(header.Type, deviceName);
                return;
            }

            switch (header.Type)
            {
                case RimTypeKeyboard:
                    ProcessKeyboardInput(buffer, headerSize, matchedDevice);
                    break;
                case RimTypeMouse:
                    ProcessMouseInput(buffer, headerSize, matchedDevice);
                    break;
                case RimTypeHid:
                    ProcessHidInput(buffer, headerSize, matchedDevice);
                    break;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TryCreateGlobalHumanInputEvent(
        IntPtr buffer,
        uint headerSize,
        uint rawInputType,
        out GlobalHumanInputEventArgs eventArgs)
    {
        var createdEventArgs = rawInputType switch
        {
            RimTypeKeyboard => CreateKeyboardHumanInputEvent(buffer, headerSize),
            RimTypeMouse => CreateMouseHumanInputEvent(buffer, headerSize),
            _ => null
        };

        if (createdEventArgs is null)
        {
            eventArgs = new GlobalHumanInputEventArgs(string.Empty, countsAsUserActivity: false, isWakeEligible: false);
            return false;
        }

        eventArgs = createdEventArgs;
        return true;
    }

    private static GlobalHumanInputEventArgs? CreateKeyboardHumanInputEvent(IntPtr buffer, uint headerSize)
    {
        var keyboard = Marshal.PtrToStructure<RawKeyboard>(IntPtr.Add(buffer, (int)headerSize));
        var isKeyDown = (keyboard.Flags & RiKeyBreak) == 0;
        return isKeyDown && keyboard.VKey != 0
            ? new GlobalHumanInputEventArgs("raw_keyboard", countsAsUserActivity: true, isWakeEligible: true)
            : null;
    }

    private static GlobalHumanInputEventArgs? CreateMouseHumanInputEvent(IntPtr buffer, uint headerSize)
    {
        var mouse = Marshal.PtrToStructure<RawMouse>(IntPtr.Add(buffer, (int)headerSize));
        var buttonFlags = (ushort)(mouse.Buttons & 0xFFFF);
        var movementDistance = Math.Abs(mouse.LastX) + Math.Abs(mouse.LastY);
        if (buttonFlags != 0)
        {
            return new GlobalHumanInputEventArgs("raw_mouse_button", countsAsUserActivity: true, isWakeEligible: true);
        }

        return movementDistance >= 8
            ? new GlobalHumanInputEventArgs("raw_mouse_move", countsAsUserActivity: true, isWakeEligible: true)
            : null;
    }

    private void ProcessKeyboardInput(IntPtr buffer, uint headerSize, GuideButtonKnownDevice device)
    {
        var keyboard = Marshal.PtrToStructure<RawKeyboard>(IntPtr.Add(buffer, (int)headerSize));
        var isKeyDown = (keyboard.Flags & RiKeyBreak) == 0;
        if (!isKeyDown)
        {
            return;
        }

        WriteDiagnostic(
            $"raw_keyboard:{device.Address}:{keyboard.VKey:X4}:{keyboard.MakeCode:X4}",
            "raw_keyboard_seen",
            device,
            $"Steam raw keyboard input seen. vkey=0x{keyboard.VKey:X4}, make=0x{keyboard.MakeCode:X4}, flags=0x{keyboard.Flags:X4}.");

        // Steam Controller lizard mode maps normal game buttons to keyboard keys
        // such as Esc and Enter. Do not use keyboard input as guide or idle activity.
    }

    private void ProcessMouseInput(IntPtr buffer, uint headerSize, GuideButtonKnownDevice device)
    {
        var mouse = Marshal.PtrToStructure<RawMouse>(IntPtr.Add(buffer, (int)headerSize));
        var buttonFlags = (ushort)(mouse.Buttons & 0xFFFF);
        var hasMovement = mouse.LastX != 0 || mouse.LastY != 0;
        if (buttonFlags == 0 && !hasMovement)
        {
            return;
        }

        if (buttonFlags == 0)
        {
            WriteDiagnostic(
                $"raw_mouse_move:{device.Address}:{mouse.LastX}:{mouse.LastY}",
                "raw_mouse_move_seen",
                device,
                $"Steam raw mouse movement seen. dx={mouse.LastX}; dy={mouse.LastY}.");
            return;
        }

        WriteDiagnostic(
            $"raw_mouse:{device.Address}:{buttonFlags:X4}",
            "raw_mouse_button_seen",
            device,
            $"Steam raw mouse button input seen. flags=0x{buttonFlags:X4}; dx={mouse.LastX}; dy={mouse.LastY}.");
    }

    private void ProcessHidInput(IntPtr buffer, uint headerSize, GuideButtonKnownDevice device)
    {
        var rawHidOffset = (int)headerSize;
        var reportSize = Marshal.ReadInt32(buffer, rawHidOffset);
        var reportCount = Marshal.ReadInt32(buffer, rawHidOffset + 4);
        if (reportSize <= 0 || reportCount <= 0 || reportSize > 512 || reportCount > 32)
        {
            return;
        }

        var dataOffset = rawHidOffset + 8;
        var rawDataLength = reportSize * reportCount;
        var rawData = ArrayPool<byte>.Shared.Rent(rawDataLength);
        try
        {
            Marshal.Copy(IntPtr.Add(buffer, dataOffset), rawData, 0, rawDataLength);
            var detailedInputMode = IsDetailedInputReportMode();
            for (var index = 0; index < reportCount; index++)
            {
                var report = rawData.AsSpan(index * reportSize, reportSize);
                if (ShouldSuppressGuideInputReport(device, report))
                {
                    continue;
                }

                var hasGuideState = GuideButtonReportParser.TryParseGuideButton(
                    device.DeviceKind,
                    report,
                    out var pressed);
                if (ShouldEvaluateRawHidInputActivity(device.DeviceKind, detailedInputMode) &&
                    TryGetHidInputActivity(device, report, out var countsAsUserActivity, out var isWakeEligible))
                {
                    RaiseInputActivityReceived(device, countsAsUserActivity, isWakeEligible);
                }

                if (ShouldPublishRawInputReport(device, report, detailedInputMode))
                {
                    InputReportReceived?.Invoke(
                        this,
                        new GuideButtonInputReportEventArgs(
                            device.Address,
                            ResolveDisplayName(device),
                            device.DeviceKind,
                            report));
                }

                if (hasGuideState)
                {
                    if (TryRegisterRawHidGuidePressEdge(device, pressed))
                    {
                        RaiseInputActivityReceived(device, countsAsUserActivity: true, isWakeEligible: true);
                        if (device.DeviceKind != GuideButtonDeviceKind.SteamController)
                        {
                            RaiseGuideButtonPressed(device, "raw_hid_guide_press_edge", GuideButtonGesture.ShortPress);
                        }
                    }

                    if (device.DeviceKind == GuideButtonDeviceKind.SteamController)
                    {
                        RegisterRawHidGuideState(device, pressed, report);
                    }

                    continue;
                }

                if (device.DeviceKind == GuideButtonDeviceKind.SteamController &&
                    TryApplyRawHidStatusReleaseHint(device, report))
                {
                    continue;
                }

                if (device.DeviceKind == GuideButtonDeviceKind.SteamController)
                {
                    TryClearStaleRawHidGuideState(device, report);
                }

                WriteDiagnostic(
                    $"raw_hid:{device.Address}:{BuildReportSignature(report)}",
                    "raw_hid_unparsed",
                    device,
                    $"{device.DeviceKind} raw HID input was seen but not recognized as the guide button. {FormatReportSample(report)}");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rawData, clearArray: true);
        }
    }

    private void RaiseInputActivityReceived(
        GuideButtonKnownDevice device,
        bool countsAsUserActivity,
        bool isWakeEligible)
    {
        InputActivityReceived?.Invoke(
            this,
            new GuideButtonActivityEventArgs(
                device.Address,
                ResolveDisplayName(device),
                device.DeviceKind,
                countsAsUserActivity,
                isWakeEligible));
    }

    private bool ShouldSuppressGuideInputReport(GuideButtonKnownDevice device, ReadOnlySpan<byte> report)
    {
        if (IsWakeOnlyMode)
        {
            return false;
        }

        if (device.DeviceKind != GuideButtonDeviceKind.SteamController)
        {
            return false;
        }

        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var now = DateTimeOffset.Now;
        bool isFirstInputForDevice;
        bool shouldNotifyBaselineReady;
        var reportCopy = report.ToArray();
        var hasGuideState = GuideButtonReportParser.TryParseGuideButton(
            device.DeviceKind,
            report,
            out var pressed);
        lock (_sync)
        {
            isFirstInputForDevice = _seenRawHidInputByAddress.Add(address);
            if (!isFirstInputForDevice)
            {
                return false;
            }

            _lastPressByAddress.Remove(address);
            _lastRawHidReportByAddress[address] = reportCopy;
            if (hasGuideState)
            {
                _lastRawHidGuidePressedByAddress[address] = pressed;
                UpdateRawHidGuideNeutralReportCountLocked(address, pressed);
            }
            else
            {
                _lastRawHidGuidePressedByAddress.Remove(address);
                _rawHidGuideNeutralReportCountByAddress.Remove(address);
            }

            _rawHidGuideButtonStateTracker.ClearActivity(address, now);
            shouldNotifyBaselineReady = !_steamRawHidBaselineReadyNotified;
            _steamRawHidBaselineReadyNotified = true;
        }

        var knownDevice = new GuideButtonKnownDevice(
            address,
            string.IsNullOrWhiteSpace(device.DisplayName) ? "Steam Controller" : device.DisplayName,
            GuideButtonDeviceKind.SteamController);
        WriteDiagnostic(
            $"raw_hid_initial_baseline:{address}:{BuildReportSignature(report)}",
            "raw_hid_initial_baseline",
            knownDevice,
            $"Steam Controller first raw HID report was stored as baseline without showing a guide toast. pressed={pressed}; parsed={hasGuideState}; {FormatReportSample(report)}");
        if (shouldNotifyBaselineReady)
        {
            SteamRawHidBaselineReady?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    private bool ShouldPublishRawInputReport(
        GuideButtonKnownDevice device,
        ReadOnlySpan<byte> report,
        bool detailedInputMode)
    {
        if (GuideButtonMonitorService.ShouldSuppressNoisySteamStatusInputReport(device.DeviceKind, report))
        {
            return false;
        }

        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        if (detailedInputMode)
        {
            return true;
        }

        var reportId = report.Length > 0 ? report[0].ToString("X2") : "empty";
        var key = $"{device.DeviceKind}:{address}:{reportId}";
        var trigger = GetActiveBatteryGuideTrigger(device.DeviceKind);
        var signature = GuideButtonMonitorService.BuildInputReportActionSignature(device.DeviceKind, report, trigger);
        lock (_sync)
        {
            if (_lastRawInputReportActionSignatureByDevice.TryGetValue(key, out var previous) &&
                previous == signature)
            {
                return false;
            }

            _lastRawInputReportActionSignatureByDevice[key] = signature;
            return true;
        }
    }

    private bool ShouldEvaluateRawHidInputActivity(GuideButtonDeviceKind deviceKind, bool detailedInputMode)
    {
        if (detailedInputMode || IsWakeOnlyMode)
        {
            return true;
        }

        lock (_sync)
        {
            return _activeBatteryGuideTriggers.ContainsKey(deviceKind);
        }
    }

    private BatteryGuideTrigger? GetActiveBatteryGuideTrigger(GuideButtonDeviceKind deviceKind)
    {
        lock (_sync)
        {
            return _activeBatteryGuideTriggers.TryGetValue(deviceKind, out var trigger)
                ? trigger
                : null;
        }
    }

    private bool IsDetailedInputReportMode()
    {
        lock (_sync)
        {
            return _isDetailedInputReportMode;
        }
    }

    private bool TryRegisterRawHidGuidePressEdge(GuideButtonKnownDevice device, bool pressed)
    {
        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        lock (_sync)
        {
            if (!_lastRawHidGuidePressedByAddress.TryGetValue(address, out var previousPressed))
            {
                _lastRawHidGuidePressedByAddress[address] = pressed;
                UpdateRawHidGuideNeutralReportCountLocked(address, pressed);
                return false;
            }

            _lastRawHidGuidePressedByAddress[address] = pressed;
            var neutralReportCount = UpdateRawHidGuideNeutralReportCountLocked(address, pressed);
            if (neutralReportCount < 2)
            {
                return false;
            }

            return ShouldRaiseRawHidGuidePressEdge(
                hasPrevious: true,
                previousPressed,
                pressed);
        }
    }

    private int UpdateRawHidGuideNeutralReportCountLocked(string address, bool pressed)
    {
        if (pressed)
        {
            return _rawHidGuideNeutralReportCountByAddress.TryGetValue(address, out var count)
                ? count
                : 0;
        }

        var nextCount = _rawHidGuideNeutralReportCountByAddress.TryGetValue(address, out var previousCount)
            ? Math.Min(2, previousCount + 1)
            : 1;
        _rawHidGuideNeutralReportCountByAddress[address] = nextCount;
        return nextCount;
    }

    internal static bool ShouldRaiseRawHidGuidePressEdge(
        bool hasPrevious,
        bool previousPressed,
        bool currentPressed)
    {
        return hasPrevious && currentPressed && !previousPressed;
    }

    private bool TryGetHidInputActivity(
        GuideButtonKnownDevice device,
        ReadOnlySpan<byte> report,
        out bool countsAsUserActivity,
        out bool isWakeEligible)
    {
        countsAsUserActivity = false;
        isWakeEligible = false;

        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address) || report.Length == 0)
        {
            return false;
        }

        byte[]? previousReport;
        var currentReport = report.ToArray();
        lock (_sync)
        {
            _lastRawHidReportByAddress.TryGetValue(address, out previousReport);
            _lastRawHidReportByAddress[address] = currentReport;
        }

        if (previousReport is null ||
            previousReport.Length == 0 ||
            previousReport[0] != currentReport[0])
        {
            return false;
        }

        countsAsUserActivity = ShouldTreatHidReportChangeAsActivity(device.DeviceKind, previousReport, currentReport);
        isWakeEligible = countsAsUserActivity;
        return countsAsUserActivity;
    }

    internal static bool ShouldTreatHidReportChangeAsActivity(
        ReadOnlySpan<byte> previousReport,
        ReadOnlySpan<byte> currentReport)
    {
        return ShouldTreatHidReportChangeAsActivity(
            GuideButtonDeviceKind.SteamController,
            previousReport,
            currentReport);
    }

    internal static bool ShouldTreatHidReportChangeAsActivity(
        GuideButtonDeviceKind deviceKind,
        ReadOnlySpan<byte> previousReport,
        ReadOnlySpan<byte> currentReport)
    {
        if (previousReport.Length == 0 ||
            currentReport.Length == 0 ||
            previousReport[0] != currentReport[0])
        {
            return false;
        }

        return BatteryGuideTriggerParser.TryCaptureButtonsOnly(
            deviceKind,
            previousReport,
            currentReport,
            out _);
    }

    private bool TryApplyRawHidStatusReleaseHint(GuideButtonKnownDevice device, ReadOnlySpan<byte> report)
    {
        if (!GuideButtonReportParser.IsSteamControllerStatusReport(report))
        {
            return false;
        }

        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        SteamRawHidGuideButtonActivity activity;
        SteamRawHidGuideButtonDecision decision;
        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            activity = _rawHidGuideButtonStateTracker.GetActivity(address, now);
            if (!activity.IsPressed)
            {
                WriteDiagnostic(
                    $"raw_hid_status_release_skipped:{device.Address}:{BuildReportSignature(report)}",
                    "raw_hid_status_release_skipped",
                    device,
                    $"Steam raw HID status report was neutral, but no pressed Raw HID state was active. pending={activity.HasPendingRelease}; pendingPressMs={(int)activity.PendingPressDuration.TotalMilliseconds}; pendingLastPressedAgeMs={(int)activity.PendingLastPressedAge.TotalMilliseconds}; {FormatReportSample(report)}");
                return false;
            }

            decision = _rawHidGuideButtonStateTracker.RegisterStatusReleaseHint(address, now);
        }

        WriteDiagnostic(
            $"raw_hid_status_release:{device.Address}:{BuildReportSignature(report)}",
            "raw_hid_status_release_hint",
            device,
            $"Steam raw HID status report released a stuck pressed-state. rawHeldMs={(int)activity.PressedDuration.TotalMilliseconds}; rawLastStateAgeMs={(int)activity.LastStateAge.TotalMilliseconds}; {FormatReportSample(report)}");

        if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableShortPress)
        {
            RaiseGuideButtonPressed(device, "raw_hid_status_release_hint", GuideButtonGesture.ShortPress);
        }
        else if (decision.Kind == SteamRawHidGuideButtonDecisionKind.PendingRelease)
        {
            _ = CompleteRawHidShortPressCandidateAsync(device, address, decision.PendingId, decision.ReleaseDueAt);
        }
        else if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableLongPress)
        {
            NotifyRawHidLongPressSuppressed(device, address, decision.Duration);
        }

        return true;
    }

    private void TryClearStaleRawHidGuideState(GuideButtonKnownDevice device, ReadOnlySpan<byte> report)
    {
        if (!GuideButtonReportParser.IsSteamControllerStatusReport(report))
        {
            return;
        }

        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var cleared = false;
        lock (_sync)
        {
            cleared = _rawHidGuideButtonStateTracker.ClearStalePressedSession(address, DateTimeOffset.Now);
        }

        if (!cleared)
        {
            return;
        }

        WriteDiagnostic(
            $"raw_hid_stale_clear:{device.Address}",
            "raw_hid_stale_state_cleared",
            device,
            $"Steam raw HID pressed-state was stale and was cleared without showing a toast. {FormatReportSample(report)}");
    }

    private void RegisterRawHidGuideState(GuideButtonKnownDevice device, bool pressed, ReadOnlySpan<byte> report)
    {
        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        SteamRawHidGuideButtonDecision decision;
        lock (_sync)
        {
            decision = _rawHidGuideButtonStateTracker.RegisterState(address, pressed, DateTimeOffset.Now);
        }

        if (decision.Kind != SteamRawHidGuideButtonDecisionKind.None)
        {
            GuideButtonEventLog.Write(
                "raw_hid_guide_state",
                "SteamController",
                address,
                string.IsNullOrWhiteSpace(device.DisplayName) ? "Steam Controller" : device.DisplayName,
                $"Steam raw HID guide state parsed. pressed={pressed}; decision={decision.Kind}; {FormatReportSample(report)}");
        }

        if (decision.Kind == SteamRawHidGuideButtonDecisionKind.PendingRelease)
        {
            _ = CompleteRawHidShortPressCandidateAsync(device, address, decision.PendingId, decision.ReleaseDueAt);
        }
        else if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableShortPress)
        {
            RaiseGuideButtonPressed(device, "raw_hid_release_short_press", GuideButtonGesture.ShortPress);
        }
        else if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableLongPress)
        {
            NotifyRawHidLongPressSuppressed(device, address, decision.Duration);
        }
    }

    private async Task CompleteRawHidShortPressCandidateAsync(
        GuideButtonKnownDevice device,
        string address,
        Guid pendingId,
        DateTimeOffset releaseDueAt)
    {
        try
        {
            var delay = releaseDueAt - DateTimeOffset.Now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }
        catch
        {
            return;
        }

        SteamRawHidGuideButtonDecision decision;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            decision = _rawHidGuideButtonStateTracker.CompletePendingRelease(
                address,
                pendingId,
                DateTimeOffset.Now);
        }

        if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableShortPress)
        {
            RaiseGuideButtonPressed(device, "raw_hid_stable_short_press", GuideButtonGesture.ShortPress);
        }
        else if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableLongPress)
        {
            NotifyRawHidLongPressSuppressed(device, address, decision.Duration);
        }
    }

    private void NotifyRawHidLongPressSuppressed(
        GuideButtonKnownDevice device,
        string address,
        TimeSpan duration)
    {
        var displayName = string.IsNullOrWhiteSpace(device.DisplayName)
            ? "Steam Controller"
            : device.DisplayName;
        GuideButtonPressed?.Invoke(
            this,
            new GuideButtonPressedEventArgs(address, displayName, GuideButtonDeviceKind.SteamController, GuideButtonGesture.LongPress));
        GuideButtonEventLog.Write(
            "raw_hid_long_press_suppressed",
            "SteamController",
            address,
            displayName,
            $"Steam raw HID guide hold was ignored. durationMs={(int)duration.TotalMilliseconds}.");
    }

    private bool TryResolveSteamDevice(
        string deviceName,
        IReadOnlyList<GuideButtonKnownDevice> knownDevices,
        out GuideButtonKnownDevice matchedDevice,
        out bool isSteamDevice)
    {
        matchedDevice = new GuideButtonKnownDevice(string.Empty, "Steam Controller", GuideButtonDeviceKind.SteamController);
        isSteamDevice = false;

        var address = AddressNormalizer.ExtractAddressFromInstanceId(deviceName);
        if (!string.IsNullOrWhiteSpace(address))
        {
            var byAddress = knownDevices.FirstOrDefault(
                device => string.Equals(
                    AddressNormalizer.NormalizeAddress(device.Address),
                    address,
                    StringComparison.OrdinalIgnoreCase));
            if (byAddress is not null)
            {
                matchedDevice = byAddress;
                isSteamDevice = true;
                return true;
            }
        }

        if (!HidProbeTextParser.TryParseVidPid(deviceName, out var vendorId, out var productId))
        {
            return false;
        }

        if (IsSteamRawInputVidPid(vendorId, productId))
        {
            var knownSteamDevices = knownDevices
                .Where(device => device.DeviceKind == GuideButtonDeviceKind.SteamController)
                .ToList();
            matchedDevice = knownSteamDevices.Count == 1
                ? knownSteamDevices[0]
                : new GuideButtonKnownDevice(
                    AddressNormalizer.NormalizeAddress(address),
                    "Steam Controller",
                    GuideButtonDeviceKind.SteamController);
            isSteamDevice = true;
            return true;
        }

        if (!IsDualSenseRawInputVidPid(vendorId, productId))
        {
            return false;
        }

        var knownDualSenseDevices = knownDevices
            .Where(device => device.DeviceKind == GuideButtonDeviceKind.DualSense)
            .ToList();
        matchedDevice = knownDualSenseDevices.Count == 1
            ? knownDualSenseDevices[0]
            : new GuideButtonKnownDevice(
                AddressNormalizer.NormalizeAddress(address),
                "DualSense Wireless Controller",
                GuideButtonDeviceKind.DualSense);
        isSteamDevice = false;
        return true;
    }

    private static bool IsSteamRawInputVidPid(string? vendorId, string? productId)
    {
        if (string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(productId))
        {
            return false;
        }

        var isKnownSteamVendor =
            string.Equals(vendorId, "28DE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(vendorId, "0228", StringComparison.OrdinalIgnoreCase);
        if (!isKnownSteamVendor)
        {
            return false;
        }

        return string.Equals(productId, "1303", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1304", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1305", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1142", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDualSenseRawInputVidPid(string? vendorId, string? productId)
    {
        if (!string.Equals(vendorId, "054C", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(productId, "0CE6", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "0DF2", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "0DF3", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteRawDeviceSummary()
    {
        var knownDevices = ReadKnownRawInputGuideDevices();
        var rawDevices = EnumerateRawInputDevices();
        var steamCandidates = rawDevices
            .Where(device => IsSteamCandidateDeviceName(device.DeviceName, knownDevices))
            .Select(device => BuildRawDeviceSummaryItem(device, knownDevices))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var summary = steamCandidates.Count == 0
            ? "No guide-button raw input device was listed."
            : string.Join("; ", steamCandidates);

        GuideButtonEventLog.Write(
            "raw_input_devices",
            "GuideButton",
            knownDevices.Count == 1 ? knownDevices[0].Address : string.Empty,
            "Guide Button",
            $"RawInput devices={rawDevices.Count}, knownGuide={knownDevices.Count}, guideCandidates={steamCandidates.Count}. {summary}");
    }

    private void WriteUnmatchedSteamInputDiagnostic(uint rawInputType, string deviceName)
    {
        var knownDevices = ReadKnownRawInputGuideDevices();
        if (!IsSteamCandidateDeviceName(deviceName, knownDevices))
        {
            return;
        }

        var address = AddressNormalizer.ExtractAddressFromInstanceId(deviceName);
        var addressText = string.IsNullOrWhiteSpace(address)
            ? "none"
            : GuideButtonLogFormatter.MaskAddress(address);
        var vidPidText = HidProbeTextParser.TryParseVidPid(deviceName, out var vendorId, out var productId)
            ? $"vid={vendorId}, pid={productId}"
            : "vid=unknown, pid=unknown";
        var rawInfoText = TryGetRawHidInfoByName(deviceName, out var usagePage, out var usage)
            ? $", usagePage=0x{usagePage:X4}, usage=0x{usage:X4}"
            : string.Empty;
        WriteDiagnostic(
            $"raw_unmatched:{rawInputType}:{vidPidText}:{addressText}",
            "raw_unmatched_steam_input",
            new GuideButtonKnownDevice(string.Empty, "Steam Controller", GuideButtonDeviceKind.SteamController),
            $"Guide-button-looking raw input was seen but did not match a known battery-list address. type={FormatRawInputType(rawInputType)}, {vidPidText}{rawInfoText}, address={addressText}, knownGuide={knownDevices.Count}.");
    }

    private IReadOnlyList<GuideButtonKnownDevice> ReadKnownSteamDevices()
    {
        return ReadKnownRawInputGuideDevices()
            .Where(device => device.DeviceKind == GuideButtonDeviceKind.SteamController)
            .ToList();
    }

    private IReadOnlyList<GuideButtonKnownDevice> ReadKnownRawInputGuideDevices()
    {
        Func<IReadOnlyList<GuideButtonKnownDevice>> provider;
        var now = DateTimeOffset.UtcNow;
        lock (_sync)
        {
            if (now < _cachedKnownRawInputGuideDevicesUntilUtc)
            {
                return _cachedKnownRawInputGuideDevices;
            }

            provider = _knownDeviceProvider;
        }

        try
        {
            var devices = provider()
                .Where(device => device.DeviceKind is GuideButtonDeviceKind.SteamController or GuideButtonDeviceKind.DualSense)
                .Select(device => device with
                {
                    Address = AddressNormalizer.NormalizeAddress(device.Address),
                    DisplayName = ResolveDisplayName(device)
                })
                .Where(device => !string.IsNullOrWhiteSpace(device.Address))
                .GroupBy(device => $"{device.DeviceKind}:{device.Address}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            lock (_sync)
            {
                _cachedKnownRawInputGuideDevices = devices;
                _cachedKnownRawInputGuideDevicesUntilUtc = now + KnownRawInputDeviceCacheDuration;
            }

            return devices;
        }
        catch
        {
            return [];
        }
    }

    private void RaiseGuideButtonPressed(GuideButtonKnownDevice device, string source, GuideButtonGesture gesture)
    {
        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            if (_lastPressByAddress.TryGetValue(address, out var lastPress) &&
                now - lastPress <= PressDebounce)
            {
                return;
            }

            _lastPressByAddress[address] = now;
        }

        var displayName = string.IsNullOrWhiteSpace(device.DisplayName)
            ? ResolveDisplayName(device)
            : device.DisplayName;
        GuideButtonPressed?.Invoke(
            this,
            new GuideButtonPressedEventArgs(address, displayName, device.DeviceKind, gesture));
        GuideButtonEventLog.Write(
            "pressed",
            device.DeviceKind.ToString(),
            address,
            displayName,
            $"Guide button {gesture} detected from {source}.");
    }

    private void WriteDiagnostic(string key, string eventName, GuideButtonKnownDevice device, string message)
    {
        if (!RuntimeDiagnostics.IsFileLoggingEnabled)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            if (_lastDiagnosticByKey.TryGetValue(key, out var lastSeen) &&
                now - lastSeen <= DiagnosticRepeatInterval)
            {
                return;
            }

            _lastDiagnosticByKey[key] = now;
        }

        GuideButtonEventLog.Write(
            eventName,
            "SteamController",
            device.Address,
            string.IsNullOrWhiteSpace(device.DisplayName) ? "Steam Controller" : device.DisplayName,
            message);
    }

    private static RawInputDevice BuildRawInputDevice(ushort usage, IntPtr windowHandle)
    {
        return new RawInputDevice
        {
            UsagePage = UsagePageGenericDesktop,
            Usage = usage,
            Flags = RidevInputSink,
            Target = windowHandle
        };
    }

    private static RawInputDevice BuildRawInputPageDevice(ushort usagePage, IntPtr windowHandle)
    {
        return new RawInputDevice
        {
            UsagePage = usagePage,
            Usage = 0,
            Flags = RidevInputSink | RidevPageOnly,
            Target = windowHandle
        };
    }

    private static RawInputDevice[] BuildNormalRawInputDevices(IntPtr windowHandle)
    {
        return
        [
            BuildRawInputPageDevice(UsagePageVendorSteam, windowHandle)
        ];
    }

    private static RawInputDevice[] BuildHumanInputOnlyRawInputDevices(IntPtr windowHandle)
    {
        return
        [
            BuildRawInputDevice(UsageMouse, windowHandle),
            BuildRawInputDevice(UsageKeyboard, windowHandle)
        ];
    }

    private static RawInputDevice[] BuildWakeOnlyRawInputDevices(IntPtr windowHandle)
    {
        return
        [
            BuildRawInputDevice(UsageJoystick, windowHandle),
            BuildRawInputDevice(UsageGamepad, windowHandle),
            BuildRawInputPageDevice(UsagePageVendorSteam, windowHandle)
        ];
    }

    private static RawInputDevice BuildRawInputRemovalDevice(ushort usagePage, ushort usage)
    {
        return new RawInputDevice
        {
            UsagePage = usagePage,
            Usage = usage,
            Flags = RidevRemove,
            Target = IntPtr.Zero
        };
    }

    private static RawInputDevice BuildRawInputPageRemovalDevice(ushort usagePage)
    {
        return new RawInputDevice
        {
            UsagePage = usagePage,
            Usage = 0,
            Flags = RidevRemove,
            Target = IntPtr.Zero
        };
    }

    private static IReadOnlyList<RawInputDeviceInfo> EnumerateRawInputDevices()
    {
        var count = 0u;
        var headerSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
        var query = GetRawInputDeviceList(IntPtr.Zero, ref count, headerSize);
        if (query == uint.MaxValue || count == 0)
        {
            return [];
        }

        var devices = new RawInputDeviceList[count];
        var read = GetRawInputDeviceList(devices, ref count, headerSize);
        if (read == uint.MaxValue)
        {
            return [];
        }

        var results = new List<RawInputDeviceInfo>((int)Math.Min(count, read));
        for (var index = 0; index < read && index < devices.Length; index++)
        {
            var device = devices[index];
            results.Add(new RawInputDeviceInfo(
                device.Device,
                device.Type,
                GetRawInputDeviceName(device.Device)));
        }

        return results;
    }

    private static bool IsSteamCandidateDeviceName(
        string deviceName,
        IReadOnlyList<GuideButtonKnownDevice> knownDevices)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        if (deviceName.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("28DE", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("DualSense", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("054C", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var address = AddressNormalizer.ExtractAddressFromInstanceId(deviceName);
        return !string.IsNullOrWhiteSpace(address) &&
               knownDevices.Any(device => string.Equals(
                   AddressNormalizer.NormalizeAddress(device.Address),
                   address,
                   StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveDisplayName(GuideButtonKnownDevice device)
    {
        if (!string.IsNullOrWhiteSpace(device.DisplayName))
        {
            return device.DisplayName;
        }

        return device.DeviceKind == GuideButtonDeviceKind.DualSense
            ? "DualSense Wireless Controller"
            : "Steam Controller";
    }

    private static string BuildRawDeviceSummaryItem(
        RawInputDeviceInfo device,
        IReadOnlyList<GuideButtonKnownDevice> knownDevices)
    {
        var address = AddressNormalizer.ExtractAddressFromInstanceId(device.DeviceName);
        var matchedKnownAddress = !string.IsNullOrWhiteSpace(address) &&
                                  knownDevices.Any(knownDevice => string.Equals(
                                      AddressNormalizer.NormalizeAddress(knownDevice.Address),
                                      address,
                                      StringComparison.OrdinalIgnoreCase));
        var addressText = string.IsNullOrWhiteSpace(address)
            ? "none"
            : GuideButtonLogFormatter.MaskAddress(address);
        var vidPidText = HidProbeTextParser.TryParseVidPid(device.DeviceName, out var vendorId, out var productId)
            ? $"vid={vendorId},pid={productId}"
            : "vid=unknown,pid=unknown";
        var hidInfoText = TryGetRawHidInfo(device.Device, out _, out _, out var usagePage, out var usage)
            ? $",usagePage=0x{usagePage:X4},usage=0x{usage:X4}"
            : string.Empty;
        return $"type={FormatRawInputType(device.Type)},{vidPidText}{hidInfoText},address={addressText},addressMatched={matchedKnownAddress}";
    }

    private static string FormatRawInputType(uint rawInputType)
    {
        return rawInputType switch
        {
            RimTypeMouse => "Mouse",
            RimTypeKeyboard => "Keyboard",
            RimTypeHid => "HID",
            _ => rawInputType.ToString("X")
        };
    }

    private static string GetRawInputDeviceName(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        var size = 0u;
        _ = GetRawInputDeviceInfo(deviceHandle, RidiDeviceName, null, ref size);
        if (size == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((int)size);
        var result = GetRawInputDeviceInfo(deviceHandle, RidiDeviceName, builder, ref size);
        return result == uint.MaxValue ? string.Empty : builder.ToString();
    }

    private string GetCachedRawInputDeviceName(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        lock (_sync)
        {
            if (_rawInputDeviceNameByHandle.TryGetValue(deviceHandle, out var cachedName))
            {
                return cachedName;
            }
        }

        var deviceName = GetRawInputDeviceName(deviceHandle);
        lock (_sync)
        {
            _rawInputDeviceNameByHandle[deviceHandle] = deviceName;
        }

        return deviceName;
    }

    private static bool TryGetRawHidInfoByName(string deviceName, out ushort usagePage, out ushort usage)
    {
        usagePage = 0;
        usage = 0;
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        foreach (var device in EnumerateRawInputDevices())
        {
            if (string.Equals(device.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase) &&
                TryGetRawHidInfo(device.Device, out _, out _, out usagePage, out usage))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRawHidInfo(
        IntPtr deviceHandle,
        out string vendorId,
        out string productId,
        out ushort usagePage,
        out ushort usage)
    {
        vendorId = string.Empty;
        productId = string.Empty;
        usagePage = 0;
        usage = 0;
        if (deviceHandle == IntPtr.Zero)
        {
            return false;
        }

        var size = (uint)Marshal.SizeOf<RawInputDeviceInfoData>();
        var info = new RawInputDeviceInfoData
        {
            Size = size
        };
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.StructureToPtr(info, buffer, fDeleteOld: false);
            var result = GetRawInputDeviceInfo(deviceHandle, RidiDeviceInfo, buffer, ref size);
            if (result == uint.MaxValue)
            {
                return false;
            }

            info = Marshal.PtrToStructure<RawInputDeviceInfoData>(buffer);
            if (info.Type != RimTypeHid)
            {
                return false;
            }

            vendorId = info.VendorId.ToString("X4");
            productId = info.ProductId.ToString("X4");
            usagePage = info.UsagePage;
            usage = info.Usage;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string BuildReportSignature(ReadOnlySpan<byte> report)
    {
        var sampleLength = Math.Min(6, report.Length);
        Span<char> chars = stackalloc char[sampleLength * 2];
        for (var index = 0; index < sampleLength; index++)
        {
            _ = report[index].TryFormat(chars.Slice(index * 2, 2), out _, "X2");
        }

        return new string(chars);
    }

    private static string FormatReportSample(ReadOnlySpan<byte> report)
    {
        var sampleLength = Math.Min(12, report.Length);
        var parts = new string[sampleLength];
        for (var index = 0; index < sampleLength; index++)
        {
            parts[index] = report[index].ToString("X2");
        }

        return $"len={report.Length}, first={string.Join('-', parts)}.";
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        IntPtr pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RawInputDeviceList[]? pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        StringBuilder? pData,
        ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList
    {
        public IntPtr Device;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceInfoData
    {
        public uint Size;
        public uint Type;
        public uint VendorId;
        public uint ProductId;
        public uint VersionNumber;
        public ushort UsagePage;
        public ushort Usage;
    }

    private sealed record RawInputDeviceInfo(
        IntPtr Device,
        uint Type,
        string DeviceName);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public uint Buttons;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }
}
