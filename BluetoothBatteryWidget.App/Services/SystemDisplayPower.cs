using System.Runtime.InteropServices;

namespace BluetoothBatteryWidget.App.Services;

internal static class SystemDisplayPower
{
    private const int WmSysCommand = 0x0112;
    private const int ScMonitorPower = 0xF170;
    private const int MonitorPowerOn = -1;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint TimeoutMilliseconds = 500;

    public static bool TryTurnDisplayOn(IntPtr ownerHwnd)
    {
        return SendMonitorPowerCommand(ownerHwnd, MonitorPowerOn);
    }

    private static bool SendMonitorPowerCommand(IntPtr ownerHwnd, int state)
    {
        if (ownerHwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var result = SendMessageTimeout(
                ownerHwnd,
                WmSysCommand,
                new IntPtr(ScMonitorPower),
                new IntPtr(state),
                SmtoAbortIfHung,
                TimeoutMilliseconds,
                out _);
            return result != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
