using System.Runtime.InteropServices;

namespace BluetoothBatteryWidget.App.Services;

internal enum DisplayPowerState
{
    Unknown,
    Off,
    On,
    Dimmed
}

internal sealed class DisplayPowerStateChangedEventArgs(DisplayPowerState previousState, DisplayPowerState currentState)
    : EventArgs
{
    public DisplayPowerState PreviousState { get; } = previousState;

    public DisplayPowerState CurrentState { get; } = currentState;
}

internal sealed class DisplayPowerCoordinator : IDisposable
{
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtPowerSettingChange = 0x8013;
    private const int DeviceNotifyWindowHandle = 0;
    private static readonly Guid GuidSessionDisplayStatus = new("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");

    private readonly object _sync = new();
    private IntPtr _notificationHandle;
    private DisplayPowerState _currentState = DisplayPowerState.Unknown;
    private bool _disposed;

    public event EventHandler<DisplayPowerStateChangedEventArgs>? StateChanged;

    public DisplayPowerState CurrentState
    {
        get
        {
            lock (_sync)
            {
                return _currentState;
            }
        }
    }

    public bool Register(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return false;
            }

            if (_notificationHandle != IntPtr.Zero)
            {
                return true;
            }

            var displayStatusGuid = GuidSessionDisplayStatus;
            _notificationHandle = RegisterPowerSettingNotification(
                windowHandle,
                ref displayStatusGuid,
                DeviceNotifyWindowHandle);

            return _notificationHandle != IntPtr.Zero;
        }
    }

    public IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmPowerBroadcast ||
            wParam.ToInt32() != PbtPowerSettingChange ||
            lParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var header = Marshal.PtrToStructure<PowerBroadcastSettingHeader>(lParam);
        if (header.PowerSetting != GuidSessionDisplayStatus || header.DataLength < sizeof(int))
        {
            return IntPtr.Zero;
        }

        var valueOffset = Marshal.OffsetOf<PowerBroadcastSettingWithInt>(nameof(PowerBroadcastSettingWithInt.Data)).ToInt32();
        var value = Marshal.ReadInt32(lParam, valueOffset);
        UpdateState(ParseDisplayPowerState(value));
        handled = true;
        return new IntPtr(1);
    }

    public void Dispose()
    {
        IntPtr notificationHandle;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            notificationHandle = _notificationHandle;
            _notificationHandle = IntPtr.Zero;
        }

        if (notificationHandle != IntPtr.Zero)
        {
            _ = UnregisterPowerSettingNotification(notificationHandle);
        }
    }

    private void UpdateState(DisplayPowerState state)
    {
        DisplayPowerState previous;
        lock (_sync)
        {
            if (_currentState == state)
            {
                return;
            }

            previous = _currentState;
            _currentState = state;
        }

        StateChanged?.Invoke(this, new DisplayPowerStateChangedEventArgs(previous, state));
    }

    private static DisplayPowerState ParseDisplayPowerState(int value)
    {
        return value switch
        {
            0 => DisplayPowerState.Off,
            1 => DisplayPowerState.On,
            2 => DisplayPowerState.Dimmed,
            _ => DisplayPowerState.Unknown
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient,
        ref Guid powerSettingGuid,
        int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerBroadcastSettingHeader
    {
        public Guid PowerSetting;
        public uint DataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerBroadcastSettingWithInt
    {
        public Guid PowerSetting;
        public uint DataLength;
        public int Data;
    }
}
