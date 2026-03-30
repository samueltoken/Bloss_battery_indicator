using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime;

namespace BluetoothBatteryWidget.App.Services;

public static class ProcessMemoryTrimmer
{
    public static void TryTrim(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            _ = EmptyWorkingSet(process.Handle);
        }
        catch
        {
            // ignore trim failures
        }
    }

    public static void TryManagedTrim(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            var previousMode = GCSettings.LargeObjectHeapCompactionMode;
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Optimized, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }
            finally
            {
                GCSettings.LargeObjectHeapCompactionMode = previousMode;
            }

            _ = EmptyWorkingSet(process.Handle);
        }
        catch
        {
            // ignore trim failures
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
}
