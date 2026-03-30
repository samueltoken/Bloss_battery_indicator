using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace BluetoothBatteryWidget.App.Services;

public sealed class SonyHidBatteryLevelProvider
{
    private const ushort SonyVendorId = 0x054C;
    private static readonly HashSet<ushort> DualSenseProductIds = [0x0CE6, 0x0DF2];
    private static readonly HashSet<ushort> DualShock4ProductIds = [0x05C4, 0x09CC];
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<PnpBatteryReading>>(() =>
        {
            var byAddress = new Dictionary<string, PnpBatteryReading>(StringComparer.OrdinalIgnoreCase);

            HidD_GetHidGuid(out var hidClassGuid);
            var infoSet = SetupDiGetClassDevsW(
                ref hidClassGuid,
                null,
                IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (infoSet == IntPtr.Zero || infoSet == InvalidHandleValue)
            {
                return [];
            }

            try
            {
                try
                {
                    for (uint index = 0; ; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var interfaceData = new SP_DEVICE_INTERFACE_DATA
                        {
                            cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
                        };

                        if (!SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref hidClassGuid, index, ref interfaceData))
                        {
                            var lastError = Marshal.GetLastWin32Error();
                            if (lastError == ERROR_NO_MORE_ITEMS)
                            {
                                break;
                            }

                            continue;
                        }

                        if (!TryReadHidInterfaceDetail(infoSet, ref interfaceData, out var devicePath, out var devInfoData))
                        {
                            continue;
                        }

                        var instanceId = TryGetInstanceId(infoSet, ref devInfoData);
                        if (string.IsNullOrWhiteSpace(instanceId) || !IsBluetoothHidEndpoint(instanceId))
                        {
                            continue;
                        }

                        if (!TryParseVidPid(instanceId, out var vid, out var pid) || vid != SonyVendorId)
                        {
                            continue;
                        }

                        var isDualSense = DualSenseProductIds.Contains(pid);
                        var isDualShock4 = DualShock4ProductIds.Contains(pid);
                        if (!isDualSense && !isDualShock4)
                        {
                            continue;
                        }

                        var address = AddressNormalizer.ExtractAddressFromInstanceId(instanceId);
                        if (string.IsNullOrEmpty(address))
                        {
                            var parentInstanceId = TryGetParentInstanceId(devInfoData.DevInst);
                            address = AddressNormalizer.ExtractAddressFromInstanceId(parentInstanceId);
                        }

                        if (string.IsNullOrEmpty(address))
                        {
                            continue;
                        }

                        var displayName = TryGetFriendlyName(infoSet, ref devInfoData);
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            displayName = isDualSense ? "DualSense Wireless Controller" : "DualShock 4 Wireless Controller";
                        }

                        int? batteryPercent = null;
                        using (var handle = OpenHidHandle(devicePath))
                        {
                            if (!handle.IsInvalid)
                            {
                                batteryPercent = isDualSense
                                    ? TryReadDualSenseBatteryPercent(handle)
                                    : TryReadDualShock4BatteryPercent(handle);
                            }
                        }

                        var reading = new PnpBatteryReading(
                            InstanceId: instanceId,
                            Address: address,
                            DisplayName: displayName,
                            BatteryPercent: batteryPercent,
                            BatteryConfidence: BatteryConfidence.Confirmed,
                            SourceKind: BatterySourceKind.SonyHid,
                            RawMetric: null,
                            ModelKey: BatteryModelKeyResolver.ResolveFromVidPid(vid.ToString("X4"), pid.ToString("X4")));

                        if (!byAddress.TryGetValue(address, out var existing))
                        {
                            byAddress[address] = reading;
                            continue;
                        }

                        if (existing.BatteryPercent is null && reading.BatteryPercent is not null)
                        {
                            byAddress[address] = reading;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Return partial results gathered before cancellation.
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(infoSet);
            }

            return byAddress.Values.ToList();
        }, cancellationToken);
    }

    private static bool IsBluetoothHidEndpoint(string instanceId)
    {
        return instanceId.StartsWith(@"BTHENUM\{00001124-", StringComparison.OrdinalIgnoreCase) ||
               instanceId.StartsWith(@"HID\{00001124-", StringComparison.OrdinalIgnoreCase);
    }

    private static SafeFileHandle OpenHidHandle(string devicePath)
    {
        var normalizedPath = HidDevicePathNormalizer.Normalize(devicePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return new SafeFileHandle(InvalidHandleValue, ownsHandle: false);
        }

        var readWrite = CreateFileW(
            normalizedPath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (!readWrite.IsInvalid)
        {
            return readWrite;
        }

        var readWriteError = Marshal.GetLastWin32Error();
        readWrite.Dispose();
        var readOnly = CreateFileW(
            normalizedPath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (!readOnly.IsInvalid)
        {
            return readOnly;
        }

        var readOnlyError = Marshal.GetLastWin32Error();
        Debug.WriteLine($"[SonyHidBattery] Open failed. RW={readWriteError}, RO={readOnlyError}, Path={normalizedPath}");
        return readOnly;
    }

    private static int? TryReadDualSenseBatteryPercent(SafeFileHandle handle)
    {
        if (TryReadInputReport(handle, 0x31, 78, GamepadBatteryParser.TryParseDualSenseBatteryPercent, out var percent) &&
            percent is not null)
        {
            return percent;
        }

        if (TryReadInputReport(handle, 0x01, 64, GamepadBatteryParser.TryParseDualSenseBatteryPercent, out percent) &&
            percent is not null)
        {
            return percent;
        }

        // Some Windows stacks expose DualSense through a DS4-compatible input layout.
        if (TryReadInputReport(handle, 0x11, 78, GamepadBatteryParser.TryParseDualShock4BatteryPercent, out percent) &&
            percent is not null)
        {
            return percent;
        }

        if (TryReadInputReport(handle, 0x01, 64, GamepadBatteryParser.TryParseDualShock4BatteryPercent, out percent) &&
            percent is not null)
        {
            return percent;
        }

        return percent;
    }

    private static int? TryReadDualShock4BatteryPercent(SafeFileHandle handle)
    {
        if (TryReadInputReport(handle, 0x11, 78, GamepadBatteryParser.TryParseDualShock4BatteryPercent, out var percent) &&
            percent is not null)
        {
            return percent;
        }

        if (TryReadInputReport(handle, 0x01, 64, GamepadBatteryParser.TryParseDualShock4BatteryPercent, out percent) &&
            percent is not null)
        {
            return percent;
        }

        if (TryReadInputReport(handle, 0x31, 78, GamepadBatteryParser.TryParseDualSenseBatteryPercent, out percent) &&
            percent is not null)
        {
            return percent;
        }

        if (TryReadInputReport(handle, 0x01, 64, GamepadBatteryParser.TryParseDualSenseBatteryPercent, out percent) &&
            percent is not null)
        {
            return percent;
        }

        return percent;
    }

    private static bool TryReadInputReport(
        SafeFileHandle handle,
        byte reportId,
        int reportSize,
        TryParseBatteryPercent parse,
        out int? batteryPercent)
    {
        batteryPercent = null;
        var bufferLength = ResolveInputReportSize(handle, reportSize);
        var buffer = new byte[bufferLength];
        buffer[0] = reportId;

        if (!HidD_GetInputReport(handle, buffer, buffer.Length))
        {
            return TryReadInputReportByStream(handle, reportSize, parse, out batteryPercent);
        }

        return parse(buffer, out batteryPercent);
    }

    private delegate bool TryParseBatteryPercent(ReadOnlySpan<byte> report, out int? batteryPercent);

    private static int ResolveInputReportSize(SafeFileHandle handle, int minimumSize)
    {
        var deviceSize = TryGetInputReportSize(handle);
        if (deviceSize is null || deviceSize <= 0)
        {
            return minimumSize;
        }

        return Math.Max(minimumSize, deviceSize.Value);
    }

    private static int? TryGetInputReportSize(SafeFileHandle handle)
    {
        if (!HidD_GetPreparsedData(handle, out var preparsedData) || preparsedData == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var status = HidP_GetCaps(preparsedData, out var caps);
            if (status < 0)
            {
                return null;
            }

            return caps.InputReportByteLength;
        }
        finally
        {
            HidD_FreePreparsedData(preparsedData);
        }
    }

    private static bool TryParseVidPid(string instanceId, out ushort vid, out ushort pid)
    {
        vid = 0;
        pid = 0;

        var vidMarker = "VID&";
        var pidMarker = "PID&";

        var vidIndex = instanceId.IndexOf(vidMarker, StringComparison.OrdinalIgnoreCase);
        var pidIndex = instanceId.IndexOf(pidMarker, StringComparison.OrdinalIgnoreCase);
        if (vidIndex < 0 || pidIndex < 0)
        {
            return false;
        }

        var vidValueStart = vidIndex + vidMarker.Length;
        var pidValueStart = pidIndex + pidMarker.Length;
        if (instanceId.Length < vidValueStart + 8 || instanceId.Length < pidValueStart + 4)
        {
            return false;
        }

        var vidRaw = instanceId.Substring(vidValueStart, 8);
        var pidRaw = instanceId.Substring(pidValueStart, 4);
        if (vidRaw.StartsWith("0002", StringComparison.OrdinalIgnoreCase))
        {
            vidRaw = vidRaw[4..];
        }
        else
        {
            vidRaw = vidRaw[..4];
        }

        return ushort.TryParse(vidRaw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vid) &&
               ushort.TryParse(pidRaw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pid);
    }

    private static bool TryReadHidInterfaceDetail(
        IntPtr infoSet,
        ref SP_DEVICE_INTERFACE_DATA interfaceData,
        out string devicePath,
        out SP_DEVINFO_DATA devInfoData)
    {
        devicePath = string.Empty;
        devInfoData = new SP_DEVINFO_DATA
        {
            cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
        };

        if (!SetupDiGetDeviceInterfaceDetailW(
                infoSet,
                ref interfaceData,
                IntPtr.Zero,
                0,
                out var requiredSize,
                IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_INSUFFICIENT_BUFFER || requiredSize == 0)
            {
                return false;
            }
        }

        var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

            if (!SetupDiGetDeviceInterfaceDetailW(
                    infoSet,
                    ref interfaceData,
                    detailBuffer,
                    requiredSize,
                    out _,
                    ref devInfoData))
            {
                return false;
            }

            devicePath = ReadDevicePathFromBuffer(detailBuffer);
            return !string.IsNullOrWhiteSpace(devicePath);
        }
        finally
        {
            Marshal.FreeHGlobal(detailBuffer);
        }
    }

    private static string ReadDevicePathFromBuffer(IntPtr detailBuffer)
    {
        var candidates = new[]
        {
            Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4)),
            Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, IntPtr.Size == 8 ? 8 : 4))
        };

        string? fallback = null;
        foreach (var candidate in candidates)
        {
            var normalized = HidDevicePathNormalizer.Normalize(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (fallback is null)
            {
                fallback = normalized;
            }

            if (normalized.StartsWith(@"\\?\hid", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
        }

        return fallback ?? string.Empty;
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

    private static string TryGetParentInstanceId(uint devInst)
    {
        if (CM_Get_Parent(out var parentDevInst, devInst, 0) != CR_SUCCESS)
        {
            return string.Empty;
        }

        const int maxDeviceIdLen = 512;
        var buffer = new StringBuilder(maxDeviceIdLen);
        return CM_Get_Device_ID(parentDevInst, buffer, buffer.Capacity, 0) == CR_SUCCESS
            ? buffer.ToString()
            : string.Empty;
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

        var memory = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            if (!SetupDiGetDeviceRegistryPropertyW(
                    infoSet,
                    ref data,
                    property,
                    out _,
                    memory,
                    requiredSize,
                    out var actualSize))
            {
                return false;
            }

            if (actualSize == 0)
            {
                return true;
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

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
    private const int ERROR_NO_MORE_ITEMS = 259;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int CR_SUCCESS = 0;

    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    private const int StreamReadTimeoutMs = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetInputReport(
        SafeFileHandle hidDeviceObject,
        byte[] reportBuffer,
        int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(
        SafeFileHandle hidDeviceObject,
        out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(
        IntPtr preparsedData,
        out HIDP_CAPS capabilities);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Parent(
        out uint pdnDevInst,
        uint dnDevInst,
        uint ulFlags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID(
        uint dnDevInst,
        StringBuilder buffer,
        int bufferLen,
        uint ulFlags);

    private static bool TryReadInputReportByStream(
        SafeFileHandle handle,
        int minimumSize,
        TryParseBatteryPercent parse,
        out int? batteryPercent)
    {
        batteryPercent = null;
        var bufferLength = ResolveInputReportSize(handle, minimumSize);
        var buffer = new byte[bufferLength];

        try
        {
            using var stream = new FileStream(handle, FileAccess.Read, bufferLength, isAsync: true);
            using var cts = new CancellationTokenSource(StreamReadTimeoutMs);
            var readTask = stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).AsTask();
            readTask.Wait(cts.Token);

            if (readTask.Result <= 0)
            {
                return false;
            }

            return parse(buffer, out batteryPercent);
        }
        catch (IOException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
            return false;
        }
    }
}
