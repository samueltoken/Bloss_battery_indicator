using System.Runtime.InteropServices;
using System.Text;
using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class SetupApiBatteryLevelProvider : IBatteryLevelProvider
{
    private static readonly DEVPROPKEY BatteryPercentProperty = new()
    {
        fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
        pid = 2
    };

    public Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<PnpBatteryReading>>(() =>
        {
            var byAddress = new Dictionary<string, PnpBatteryReading>(StringComparer.OrdinalIgnoreCase);
            var addressCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var enumerator in Enumerators)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CollectFromEnumerator(enumerator, byAddress, addressCache, cancellationToken);
            }

            return byAddress.Values.ToList();
        }, cancellationToken);
    }

    private static void CollectFromEnumerator(
        string enumerator,
        IDictionary<string, PnpBatteryReading> target,
        IDictionary<string, string> addressCache,
        CancellationToken cancellationToken)
    {
        var infoSet = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
        if (infoSet == IntPtr.Zero || infoSet == InvalidHandleValue)
        {
            return;
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var deviceInfoData = CreateDeviceInfoData();
                if (!SetupDiEnumDeviceInfo(infoSet, index, ref deviceInfoData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == ERROR_NO_MORE_ITEMS)
                    {
                        break;
                    }

                    continue;
                }

                var instanceId = TryGetInstanceId(infoSet, ref deviceInfoData);
                if (!IsBluetoothEndpoint(instanceId))
                {
                    continue;
                }

                if (!addressCache.TryGetValue(instanceId, out var address))
                {
                    address = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
                    addressCache[instanceId] = address;
                }

                if (string.IsNullOrEmpty(address))
                {
                    continue;
                }

                var displayName = TryGetFriendlyName(infoSet, ref deviceInfoData);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = $"Bluetooth {address[^4..]}";
                }

                var batteryPercent = TryGetBatteryPercent(infoSet, ref deviceInfoData);
                var modelKey = BatteryModelKeyResolver.ResolveFromInstanceId(instanceId);
                var candidate = new PnpBatteryReading(
                    InstanceId: instanceId,
                    Address: address,
                    DisplayName: displayName,
                    BatteryPercent: batteryPercent,
                    BatteryConfidence: BatteryConfidence.Confirmed,
                    SourceKind: BatterySourceKind.SetupApi,
                    RawMetric: batteryPercent,
                    ModelKey: modelKey);

                if (!target.TryGetValue(address, out var existing))
                {
                    target[address] = candidate;
                    continue;
                }

                if (existing.BatteryPercent is null && candidate.BatteryPercent is not null)
                {
                    target[address] = candidate;
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(infoSet);
        }
    }

    private static bool IsBluetoothEndpoint(string instanceId)
    {
        return instanceId.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase) ||
               instanceId.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase);
    }

    private static SP_DEVINFO_DATA CreateDeviceInfoData()
    {
        return new SP_DEVINFO_DATA
        {
            cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
        };
    }

    private static string TryGetInstanceId(IntPtr infoSet, ref SP_DEVINFO_DATA data)
    {
        var buffer = new StringBuilder(260);
        if (SetupDiGetDeviceInstanceIdW(infoSet, ref data, buffer, (uint)buffer.Capacity, out var requiredSize))
        {
            return buffer.ToString();
        }

        if (requiredSize == 0 || requiredSize > 4096)
        {
            return string.Empty;
        }

        buffer = new StringBuilder((int)requiredSize);
        return SetupDiGetDeviceInstanceIdW(infoSet, ref data, buffer, (uint)buffer.Capacity, out _)
            ? buffer.ToString()
            : string.Empty;
    }

    private static string? TryGetFriendlyName(IntPtr infoSet, ref SP_DEVINFO_DATA data)
    {
        return TryGetRegistryString(infoSet, ref data, SPDRP_FRIENDLYNAME) ??
               TryGetRegistryString(infoSet, ref data, SPDRP_DEVICEDESC);
    }

    private static string? TryGetRegistryString(IntPtr infoSet, ref SP_DEVINFO_DATA data, uint property)
    {
        if (!TryReadRegistryProperty(infoSet, ref data, property, out var buffer))
        {
            return null;
        }

        var value = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? TryGetBatteryPercent(IntPtr infoSet, ref SP_DEVINFO_DATA data)
    {
        var key = BatteryPercentProperty;
        if (!TryReadDeviceProperty(infoSet, ref data, ref key, out _, out var buffer))
        {
            return null;
        }

        if (buffer.Length == 0)
        {
            return null;
        }

        uint value = buffer.Length switch
        {
            1 => buffer[0],
            2 => BitConverter.ToUInt16(buffer, 0),
            _ => BitConverter.ToUInt32(buffer, 0)
        };

        if (value > 100)
        {
            return null;
        }

        return (int)value;
    }

    private static bool TryReadRegistryProperty(
        IntPtr infoSet,
        ref SP_DEVINFO_DATA data,
        uint property,
        out byte[] buffer)
    {
        buffer = [];

        if (SetupDiGetDeviceRegistryPropertyW(
                infoSet,
                ref data,
                property,
                out _,
                IntPtr.Zero,
                0,
                out var requiredSize))
        {
            return true;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != ERROR_INSUFFICIENT_BUFFER || requiredSize == 0)
        {
            return false;
        }

        return TryReadRegistryPropertyWithBuffer(infoSet, ref data, property, requiredSize, out buffer);
    }

    private static bool TryReadRegistryPropertyWithBuffer(
        IntPtr infoSet,
        ref SP_DEVINFO_DATA data,
        uint property,
        uint bufferSize,
        out byte[] buffer)
    {
        buffer = [];
        var memory = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            if (!SetupDiGetDeviceRegistryPropertyW(
                    infoSet,
                    ref data,
                    property,
                    out _,
                    memory,
                    bufferSize,
                    out var requiredSize))
            {
                return false;
            }

            if (requiredSize == 0)
            {
                return true;
            }

            buffer = new byte[requiredSize];
            Marshal.Copy(memory, buffer, 0, (int)requiredSize);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static bool TryReadDeviceProperty(
        IntPtr infoSet,
        ref SP_DEVINFO_DATA data,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        out byte[] buffer)
    {
        buffer = [];
        propertyType = 0;

        var initialSize = (uint)sizeof(uint);
        var memory = Marshal.AllocHGlobal((int)initialSize);
        try
        {
            if (SetupDiGetDevicePropertyW(
                    infoSet,
                    ref data,
                    ref propertyKey,
                    out propertyType,
                    memory,
                    initialSize,
                    out var requiredSize,
                    0))
            {
                buffer = new byte[requiredSize];
                Marshal.Copy(memory, buffer, 0, (int)requiredSize);
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_INSUFFICIENT_BUFFER || requiredSize == 0)
            {
                return false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }

        return TryReadDevicePropertyWithBuffer(infoSet, ref data, ref propertyKey, out propertyType, out buffer);
    }

    private static bool TryReadDevicePropertyWithBuffer(
        IntPtr infoSet,
        ref SP_DEVINFO_DATA data,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        out byte[] buffer)
    {
        buffer = [];
        propertyType = 0;

        if (!SetupDiGetDevicePropertyW(
                infoSet,
                ref data,
                ref propertyKey,
                out propertyType,
                IntPtr.Zero,
                0,
                out var requiredSize,
                0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_INSUFFICIENT_BUFFER || requiredSize == 0)
            {
                return false;
            }
        }

        var memory = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            if (!SetupDiGetDevicePropertyW(
                    infoSet,
                    ref data,
                    ref propertyKey,
                    out propertyType,
                    memory,
                    requiredSize,
                    out var actualSize,
                    0))
            {
                return false;
            }

            buffer = new byte[actualSize];
            Marshal.Copy(memory, buffer, 0, (int)actualSize);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static readonly IntPtr InvalidHandleValue = new(-1);
    private static readonly string[] Enumerators = ["BTHLE", "BTHENUM"];
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_ALLCLASSES = 0x00000004;
    private const int ERROR_NO_MORE_ITEMS = 259;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(
        IntPtr classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceIdW(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        StringBuilder deviceInstanceId,
        uint deviceInstanceIdSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        IntPtr propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDevicePropertyW(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        IntPtr propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
}
