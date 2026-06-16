using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace BluetoothBatteryWidget.App.Services;

internal static class SystemDisplayIdleTimeout
{
    private static readonly Guid VideoSettingsSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid VideoIdleSetting = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    private static readonly Guid SleepSettingsSubgroup = new("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    private static readonly Guid StandbyIdleSetting = new("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private static readonly object SyncRoot = new();
    private static DateTimeOffset _cachedDisplayAtUtc = DateTimeOffset.MinValue;
    private static DateTimeOffset _cachedSleepAtUtc = DateTimeOffset.MinValue;
    private static PowerLineKind _cachedDisplayPowerLine = PowerLineKind.Unknown;
    private static PowerLineKind _cachedSleepPowerLine = PowerLineKind.Unknown;
    private static TimeSpan? _cachedDisplayTimeout;
    private static TimeSpan? _cachedSleepTimeout;

    public static TimeSpan? GetCurrentTimeout()
    {
        lock (SyncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var powerLine = GetCurrentPowerLineKind();
            if (now - _cachedDisplayAtUtc <= CacheDuration && _cachedDisplayPowerLine == powerLine)
            {
                return _cachedDisplayTimeout;
            }

            _cachedDisplayTimeout = ReadCurrentTimeout(VideoSettingsSubgroup, VideoIdleSetting, powerLine);
            _cachedDisplayPowerLine = powerLine;
            _cachedDisplayAtUtc = now;
            return _cachedDisplayTimeout;
        }
    }

    public static TimeSpan? GetCurrentSleepTimeout()
    {
        lock (SyncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var powerLine = GetCurrentPowerLineKind();
            if (now - _cachedSleepAtUtc <= CacheDuration && _cachedSleepPowerLine == powerLine)
            {
                return _cachedSleepTimeout;
            }

            _cachedSleepTimeout = ReadCurrentTimeout(SleepSettingsSubgroup, StandbyIdleSetting, powerLine);
            _cachedSleepPowerLine = powerLine;
            _cachedSleepAtUtc = now;
            return _cachedSleepTimeout;
        }
    }

    public static TimeSpan? GetCurrentDisplayOrSleepTimeout()
    {
        return SelectShortestPositiveTimeout(GetCurrentTimeout(), GetCurrentSleepTimeout());
    }

    internal static TimeSpan? SelectShortestPositiveTimeout(params TimeSpan?[] timeouts)
    {
        var candidates = timeouts
            .Where(timeout => timeout is not null && timeout.Value > TimeSpan.Zero)
            .Select(timeout => timeout!.Value)
            .ToArray();
        return candidates.Length == 0
            ? null
            : candidates.Min();
    }

    public static bool TrySetCurrentTimeout(TimeSpan? timeout)
    {
        lock (SyncRoot)
        {
            var seconds = timeout is null || timeout.Value <= TimeSpan.Zero
                ? 0u
                : (uint)Math.Clamp(
                    Math.Round(timeout.Value.TotalSeconds, MidpointRounding.AwayFromZero),
                    1d,
                    uint.MaxValue);

            if (!TryGetActiveScheme(out var scheme))
            {
                return false;
            }

            var powerLine = GetCurrentPowerLineKind();
            var writeResult = WriteDisplayIdleTimeoutForPowerLine(scheme, powerLine, seconds);
            var activeResult = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
            if (writeResult != 0 || activeResult != 0)
            {
                return false;
            }

            _cachedDisplayTimeout = timeout is null || timeout.Value <= TimeSpan.Zero
                ? null
                : TimeSpan.FromSeconds(seconds);
            _cachedDisplayPowerLine = powerLine;
            _cachedDisplayAtUtc = DateTimeOffset.UtcNow;
            return true;
        }
    }

    internal static bool ShouldWriteAc(PowerLineKind powerLine)
    {
        return powerLine is PowerLineKind.Online or PowerLineKind.Unknown;
    }

    internal static bool ShouldWriteDc(PowerLineKind powerLine)
    {
        return powerLine is PowerLineKind.Offline or PowerLineKind.Unknown;
    }

    internal static TimeSpan? SelectCurrentTimeout(uint acSeconds, uint dcSeconds, PowerLineKind powerLine)
    {
        return powerLine switch
        {
            PowerLineKind.Online => SecondsToTimeout(acSeconds),
            PowerLineKind.Offline => SecondsToTimeout(dcSeconds),
            _ => SelectFallbackTimeout(acSeconds, dcSeconds)
        };
    }

    private static TimeSpan? ReadCurrentTimeout(Guid subgroupGuid, Guid settingGuid, PowerLineKind powerLine)
    {
        try
        {
            if (!TryGetActiveScheme(out var scheme))
            {
                return null;
            }

            var acTimeout = ReadTimeoutSeconds(scheme, subgroupGuid, settingGuid, useAc: true);
            var dcTimeout = ReadTimeoutSeconds(scheme, subgroupGuid, settingGuid, useAc: false);
            return SelectCurrentTimeout(acTimeout, dcTimeout, powerLine);
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? SelectFallbackTimeout(uint acSeconds, uint dcSeconds)
    {
        var candidates = new[] { acSeconds, dcSeconds }
            .Where(seconds => seconds > 0)
            .Select(seconds => TimeSpan.FromSeconds(seconds))
            .ToArray();

        return candidates.Length == 0
            ? null
            : candidates.Min();
    }

    private static TimeSpan? SecondsToTimeout(uint seconds)
    {
        return seconds == 0
            ? null
            : TimeSpan.FromSeconds(seconds);
    }

    private static PowerLineKind GetCurrentPowerLineKind()
    {
        try
        {
            return Forms.SystemInformation.PowerStatus.PowerLineStatus switch
            {
                Forms.PowerLineStatus.Online => PowerLineKind.Online,
                Forms.PowerLineStatus.Offline => PowerLineKind.Offline,
                _ => PowerLineKind.Unknown
            };
        }
        catch
        {
            return PowerLineKind.Unknown;
        }
    }

    private static uint WriteDisplayIdleTimeoutForPowerLine(Guid scheme, PowerLineKind powerLine, uint seconds)
    {
        var result = 0u;
        if (ShouldWriteAc(powerLine))
        {
            var subgroup = VideoSettingsSubgroup;
            var setting = VideoIdleSetting;
            result |= PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, seconds);
        }

        if (ShouldWriteDc(powerLine))
        {
            var subgroup = VideoSettingsSubgroup;
            var setting = VideoIdleSetting;
            result |= PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, seconds);
        }

        return result;
    }

    private static bool TryGetActiveScheme(out Guid scheme)
    {
        scheme = Guid.Empty;
        var schemePointer = IntPtr.Zero;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out schemePointer) != 0 || schemePointer == IntPtr.Zero)
            {
                return false;
            }

            scheme = Marshal.PtrToStructure<Guid>(schemePointer);
            return true;
        }
        finally
        {
            if (schemePointer != IntPtr.Zero)
            {
                LocalFree(schemePointer);
            }
        }
    }

    private static uint ReadTimeoutSeconds(Guid scheme, Guid subgroupGuid, Guid settingGuid, bool useAc)
    {
        var subgroup = subgroupGuid;
        var setting = settingGuid;
        var result = useAc
            ? PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out var value)
            : PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out value);

        return result == 0 ? value : 0;
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid,
        uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);
}

internal enum PowerLineKind
{
    Unknown,
    Online,
    Offline
}
