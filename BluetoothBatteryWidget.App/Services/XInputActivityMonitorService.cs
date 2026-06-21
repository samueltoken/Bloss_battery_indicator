using System.Runtime.InteropServices;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class GamepadWakeInputEventArgs(
    string source,
    bool hasButton,
    bool hasTrigger,
    bool hasStick,
    bool isGuideButton,
    bool countsAsUserActivity = false)
    : EventArgs
{
    public string Source { get; } = source;

    public bool HasButton { get; } = hasButton;

    public bool HasTrigger { get; } = hasTrigger;

    public bool HasStick { get; } = hasStick;

    public bool IsGuideButton { get; } = isGuideButton;

    public bool IsWakeEligible => IsGuideButton || HasButton || HasTrigger;

    public bool CountsAsUserActivity { get; } =
        countsAsUserActivity || isGuideButton || hasButton || hasTrigger;
}

internal sealed class XInputActivityMonitorService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan WakeOnlyPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HeldActivityRepeatInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PowerIdleStopWait = TimeSpan.FromMilliseconds(1000);
    private const uint ErrorSuccess = 0;
    private const int ControllerCount = 4;
    private const byte TriggerThreshold = 30;
    private const short LeftThumbDeadZone = 7849;
    private const short RightThumbDeadZone = 8689;
    private const short ThumbMovementThreshold = 3500;

    private readonly object _sync = new();
    private readonly uint?[] _lastPacketNumbers = new uint?[ControllerCount];
    private readonly XInputGamepad?[] _lastGamepads = new XInputGamepad?[ControllerCount];
    private readonly DateTimeOffset?[] _lastHeldActivityAtUtc = new DateTimeOffset?[ControllerCount];
    private System.Threading.Timer? _timer;
    private bool _disposed;
    private bool _isWakeOnlyMode;
    private int _isPolling;

    public event EventHandler<GamepadWakeInputEventArgs>? InputActivityReceived;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return !_disposed && _timer is not null;
            }
        }
    }

    public bool IsWakeOnlyMode
    {
        get
        {
            lock (_sync)
            {
                return !_disposed && _timer is not null && _isWakeOnlyMode;
            }
        }
    }

    public void Start()
    {
        StartCore(wakeOnly: false);
    }

    public void StartWakeOnly()
    {
        StartCore(wakeOnly: true);
    }

    private void StartCore(bool wakeOnly)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_timer is not null && _isWakeOnlyMode == wakeOnly)
            {
                return;
            }
        }

        Stop();

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _isWakeOnlyMode = wakeOnly;
            _timer = new System.Threading.Timer(
                _ => Poll(),
                null,
                TimeSpan.Zero,
                wakeOnly ? WakeOnlyPollInterval : PollInterval);
        }
    }

    public void Stop()
    {
        StopCore(waitForCallbacks: false);
    }

    public void StopForPowerIdle()
    {
        StopCore(waitForCallbacks: true);
    }

    private void StopCore(bool waitForCallbacks)
    {
        System.Threading.Timer? timer;
        lock (_sync)
        {
            timer = _timer;
            _timer = null;
            _isWakeOnlyMode = false;
            Array.Clear(_lastPacketNumbers, 0, _lastPacketNumbers.Length);
            Array.Clear(_lastGamepads, 0, _lastGamepads.Length);
            Array.Clear(_lastHeldActivityAtUtc, 0, _lastHeldActivityAtUtc.Length);
        }

        DisposeTimer(timer, waitForCallbacks);
    }

    public void Dispose()
    {
        System.Threading.Timer? timer;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            timer = _timer;
            _timer = null;
            _isWakeOnlyMode = false;
            Array.Clear(_lastPacketNumbers, 0, _lastPacketNumbers.Length);
            Array.Clear(_lastGamepads, 0, _lastGamepads.Length);
            Array.Clear(_lastHeldActivityAtUtc, 0, _lastHeldActivityAtUtc.Length);
        }

        DisposeTimer(timer, waitForCallbacks: false);
    }

    private static void DisposeTimer(System.Threading.Timer? timer, bool waitForCallbacks)
    {
        if (timer is null)
        {
            return;
        }

        if (!waitForCallbacks)
        {
            timer.Dispose();
            return;
        }

        using var done = new ManualResetEvent(false);
        try
        {
            if (timer.Dispose(done))
            {
                _ = done.WaitOne(PowerIdleStopWait);
            }
        }
        catch
        {
            timer.Dispose();
        }
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) == 1)
        {
            return;
        }

        try
        {
            bool wakeOnly;
            lock (_sync)
            {
                wakeOnly = _isWakeOnlyMode;
            }

            for (uint userIndex = 0; userIndex < ControllerCount; userIndex++)
            {
                if (XInputGetState(userIndex, out var state) != ErrorSuccess)
                {
                    _lastPacketNumbers[userIndex] = null;
                    _lastGamepads[userIndex] = null;
                    continue;
                }

                var previousPacketNumber = _lastPacketNumbers[userIndex];
                var previousGamepad = _lastGamepads[userIndex];
                var now = DateTimeOffset.UtcNow;
                var hasButtonHeld = state.Gamepad.Buttons != 0;
                var hasTriggerHeld = state.Gamepad.LeftTrigger > TriggerThreshold ||
                                     state.Gamepad.RightTrigger > TriggerThreshold;
                var initialWakeOnlyInput = ShouldTreatInitialWakeOnlyStateAsActivity(
                    wakeOnly,
                    previousGamepad.HasValue,
                    hasButtonHeld,
                    hasTriggerHeld);
                var hasButtonEdge = (initialWakeOnlyInput && hasButtonHeld) ||
                    (previousGamepad.HasValue && HasButtonDownEdge(previousGamepad.Value.Buttons, state.Gamepad.Buttons));
                var hasTriggerEdge = (initialWakeOnlyInput && hasTriggerHeld) ||
                    (previousGamepad.HasValue &&
                     (HasTriggerPressEdge(previousGamepad.Value.LeftTrigger, state.Gamepad.LeftTrigger) ||
                      HasTriggerPressEdge(previousGamepad.Value.RightTrigger, state.Gamepad.RightTrigger)));
                var hasStickMovement = previousGamepad.HasValue &&
                               (HasThumbActivity(
                                    previousGamepad.Value.ThumbLX,
                                    previousGamepad.Value.ThumbLY,
                                    state.Gamepad.ThumbLX,
                                    state.Gamepad.ThumbLY,
                                    LeftThumbDeadZone) ||
                                HasThumbActivity(
                                    previousGamepad.Value.ThumbRX,
                                    previousGamepad.Value.ThumbRY,
                                    state.Gamepad.ThumbRX,
                                    state.Gamepad.ThumbRY,
                                    RightThumbDeadZone));
                var hasStickHeld = IsThumbOutsideDeadZone(
                                       state.Gamepad.ThumbLX,
                                       state.Gamepad.ThumbLY,
                                       LeftThumbDeadZone) ||
                                   IsThumbOutsideDeadZone(
                                       state.Gamepad.ThumbRX,
                                       state.Gamepad.ThumbRY,
                                       RightThumbDeadZone);
                var edgeInput = hasButtonEdge || hasTriggerEdge || hasStickMovement;
                var heldInput = hasButtonHeld || hasTriggerHeld || hasStickHeld;
                var packetChanged = ShouldTreatPacketChangeAsActivity(previousPacketNumber, state.PacketNumber);
                var packetChangedOrInitialWakeInput = packetChanged || initialWakeOnlyInput;
                _lastPacketNumbers[userIndex] = state.PacketNumber;
                _lastGamepads[userIndex] = state.Gamepad;

                var shouldRaiseEdge = wakeOnly
                    ? ShouldRaiseWakeOnlyActivity(hasButtonEdge, hasTriggerEdge, packetChangedOrInitialWakeInput)
                    : ShouldRaiseActivity(edgeInput, packetChanged);
                var shouldRaiseHeld = !wakeOnly &&
                    !shouldRaiseEdge &&
                    ShouldRaiseHeldActivity(
                        heldInput,
                        _lastHeldActivityAtUtc[userIndex],
                        now,
                        HeldActivityRepeatInterval);
                if (!heldInput)
                {
                    _lastHeldActivityAtUtc[userIndex] = null;
                }
                else if (shouldRaiseEdge || shouldRaiseHeld)
                {
                    _lastHeldActivityAtUtc[userIndex] = now;
                }

                if (shouldRaiseEdge || shouldRaiseHeld)
                {
                    var eventHasButton = hasButtonEdge || (shouldRaiseHeld && hasButtonHeld);
                    var eventHasTrigger = hasTriggerEdge || (shouldRaiseHeld && hasTriggerHeld);
                    var eventHasStick = hasStickMovement || (shouldRaiseHeld && hasStickHeld);
                    var eventCountsAsUserActivity = eventHasButton || eventHasTrigger;
                    InputActivityReceived?.Invoke(
                        this,
                        new GamepadWakeInputEventArgs(
                            "xinput",
                            eventHasButton,
                            eventHasTrigger,
                            eventHasStick,
                            isGuideButton: false,
                            eventCountsAsUserActivity));
                }
            }
        }
        catch (DllNotFoundException)
        {
            Dispose();
        }
        catch (EntryPointNotFoundException)
        {
            Dispose();
        }
        catch
        {
            // XInput activity is best-effort; battery display must keep running.
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    internal static bool IsMeaningfulActivity(
        ushort buttons,
        byte leftTrigger,
        byte rightTrigger,
        short leftThumbX,
        short leftThumbY,
        short rightThumbX,
        short rightThumbY)
    {
        return buttons != 0 ||
               leftTrigger > TriggerThreshold ||
               rightTrigger > TriggerThreshold ||
               IsThumbOutsideDeadZone(leftThumbX, leftThumbY, LeftThumbDeadZone) ||
               IsThumbOutsideDeadZone(rightThumbX, rightThumbY, RightThumbDeadZone);
    }

    internal static bool ShouldRaiseActivity(bool activeInput)
    {
        return ShouldRaiseActivity(activeInput, packetChanged: false);
    }

    internal static bool ShouldRaiseActivity(bool activeInput, bool packetChanged)
    {
        return activeInput && packetChanged;
    }

    internal static bool ShouldTreatPacketChangeAsActivity(uint? previousPacketNumber, uint currentPacketNumber)
    {
        return previousPacketNumber.HasValue && previousPacketNumber.Value != currentPacketNumber;
    }

    internal static bool ShouldRaiseWakeOnlyActivity(
        bool hasButton,
        bool hasTrigger,
        bool packetChanged)
    {
        return packetChanged && (hasButton || hasTrigger);
    }

    internal static bool ShouldTreatInitialWakeOnlyStateAsActivity(
        bool wakeOnly,
        bool hasPreviousGamepad,
        bool hasButtonHeld,
        bool hasTriggerHeld)
    {
        return wakeOnly &&
               !hasPreviousGamepad &&
               (hasButtonHeld || hasTriggerHeld);
    }

    internal static bool ShouldRaiseHeldActivity(
        bool activeInput,
        DateTimeOffset? lastRaisedAtUtc,
        DateTimeOffset nowUtc,
        TimeSpan repeatInterval)
    {
        if (!activeInput)
        {
            return false;
        }

        return lastRaisedAtUtc is null ||
               nowUtc - lastRaisedAtUtc.Value >= repeatInterval;
    }

    internal static bool HasButtonDownEdge(ushort previousButtons, ushort currentButtons)
    {
        return (currentButtons & ~previousButtons) != 0;
    }

    internal static bool HasTriggerPressEdge(byte previousTrigger, byte currentTrigger)
    {
        return previousTrigger <= TriggerThreshold && currentTrigger > TriggerThreshold;
    }

    internal static bool HasThumbActivity(
        short previousX,
        short previousY,
        short currentX,
        short currentY,
        short deadZone)
    {
        var currentOutside = IsThumbOutsideDeadZone(currentX, currentY, deadZone);
        if (!currentOutside)
        {
            return false;
        }

        var previousOutside = IsThumbOutsideDeadZone(previousX, previousY, deadZone);
        if (!previousOutside)
        {
            return true;
        }

        return IsThumbMovementLargeEnough(previousX, previousY, currentX, currentY);
    }

    private static bool IsThumbOutsideDeadZone(short x, short y, short deadZone)
    {
        var xValue = (long)x;
        var yValue = (long)y;
        var deadZoneValue = (long)deadZone;
        return (xValue * xValue) + (yValue * yValue) > deadZoneValue * deadZoneValue;
    }

    private static bool IsThumbMovementLargeEnough(short previousX, short previousY, short currentX, short currentY)
    {
        var deltaX = (long)currentX - previousX;
        var deltaY = (long)currentY - previousY;
        var threshold = (long)ThumbMovementThreshold;
        return (deltaX * deltaX) + (deltaY * deltaY) >= threshold * threshold;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);
}
