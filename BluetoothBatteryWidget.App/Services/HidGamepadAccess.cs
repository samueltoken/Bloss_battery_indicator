using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BluetoothBatteryWidget.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace BluetoothBatteryWidget.App.Services;

internal enum HidEndpointDiscoveryStage
{
    Strict = 0,
    Relaxed = 1,
    GlobalAggressive = 2
}

internal sealed record HidGamepadEndpoint(
    string DevicePath,
    string InstanceId,
    string Address,
    string DisplayName,
    string VendorId,
    string ProductId,
    HidEndpointDiscoveryStage DiscoveryStage
);

internal readonly record struct HidReportReadStatistics(
    int GetInputSuccessCount,
    int GetInputFailureCount,
    int StreamSuccessCount,
    int StreamFailureCount,
    int StreamTimeoutCount,
    int AttemptCount,
    int HardFailCount
);

internal static class HidGamepadAccess
{
    private static readonly IntPtr InvalidHandleValue = new(-1);
    private static readonly int[] ProbeFallbackSizes = [32, 64, 78, 128, 256, 512];
    private static readonly int[] ProbeFeatureFallbackSizes = [16, 32, 64, 78, 128, 256, 512];
    private const int StreamDisableFailureThreshold = 8;
    private const int StreamDisableTimeoutThreshold = 3;

    internal static IReadOnlyList<int> DefaultProbeFallbackSizes => ProbeFallbackSizes;
    internal static IReadOnlyList<int> DefaultProbeFeatureFallbackSizes => ProbeFeatureFallbackSizes;

    internal sealed class StreamReadContext : IDisposable
    {
        private readonly SafeFileHandle _sourceHandle;
        private readonly SafeFileHandle _borrowedHandle;
        private readonly bool _addRefAcquired;
        private FileStream? _stream;
        private bool _disposed;
        private int _consecutiveStreamFailureCount;
        private int _consecutiveStreamTimeoutCount;

        internal StreamReadContext(SafeFileHandle sourceHandle)
        {
            _sourceHandle = sourceHandle;
            if (TryBorrowNonOwningHandle(sourceHandle, out var borrowedHandle, out var addRefAcquired))
            {
                _borrowedHandle = borrowedHandle;
                _addRefAcquired = addRefAcquired;
                IsFallbackEnabled = true;
            }
            else
            {
                _borrowedHandle = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
                _addRefAcquired = false;
                IsFallbackEnabled = false;
            }
        }

        public bool IsFallbackEnabled { get; private set; }

        public bool HasSuccessfulRead { get; private set; }

        public bool HasStreamSuccess { get; private set; }

        public int LastGetInputErrorCode { get; private set; }

        public bool TryRead(byte[] buffer, int bufferLength, int timeoutMs, out int bytesRead, out bool streamTimedOut)
        {
            bytesRead = 0;
            streamTimedOut = false;

            if (!IsFallbackEnabled || _disposed || bufferLength <= 0 || buffer.Length < bufferLength)
            {
                return false;
            }

            try
            {
                _stream ??= new FileStream(_borrowedHandle, FileAccess.Read, Math.Max(64, bufferLength), isAsync: true);
                using var cts = new CancellationTokenSource(Math.Max(40, timeoutMs));
                var readTask = _stream.ReadAsync(buffer.AsMemory(0, bufferLength), cts.Token).AsTask();

                try
                {
                    readTask.Wait(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    streamTimedOut = true;
                    RegisterStreamFailure(timedOut: true);
                    return false;
                }

                bytesRead = readTask.Result;
                if (bytesRead <= 0)
                {
                    RegisterStreamFailure(timedOut: false);
                    return false;
                }

                RegisterStreamSuccess();
                return true;
            }
            catch (ObjectDisposedException)
            {
                DisableFallback();
                return false;
            }
            catch
            {
                RegisterStreamFailure(timedOut: false);
                return false;
            }
        }

        public void RegisterGetInputSuccess()
        {
            HasSuccessfulRead = true;
        }

        public bool ShouldSkipFallback(int lastWin32Error, bool isHardFail)
        {
            LastGetInputErrorCode = lastWin32Error;
            if (!IsFallbackEnabled)
            {
                return true;
            }

            if (isHardFail)
            {
                DisableFallback();
                return true;
            }

            return false;
        }

        public void RegisterStreamSuccess()
        {
            HasSuccessfulRead = true;
            HasStreamSuccess = true;
            _consecutiveStreamFailureCount = 0;
            _consecutiveStreamTimeoutCount = 0;
        }

        public void RegisterStreamFailure(bool timedOut)
        {
            _consecutiveStreamFailureCount++;
            if (timedOut)
            {
                _consecutiveStreamTimeoutCount++;
            }

            if (_consecutiveStreamFailureCount >= StreamDisableFailureThreshold ||
                _consecutiveStreamTimeoutCount >= StreamDisableTimeoutThreshold)
            {
                DisableFallback();
            }
        }

        private void DisableFallback()
        {
            IsFallbackEnabled = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _stream?.Dispose();
            }
            catch
            {
                // Ignore stream dispose failures.
            }

            try
            {
                _borrowedHandle.Dispose();
            }
            catch
            {
                // Ignore borrowed handle dispose failures.
            }

            ReleaseBorrowedHandle(_sourceHandle, _addRefAcquired);
            _disposed = true;
        }
    }

    public static IReadOnlyList<HidGamepadEndpoint> EnumerateBluetoothEndpoints(
        string? addressFilter,
        CancellationToken cancellationToken)
    {
        var endpoints = EnumerateProbeEndpoints(addressFilter, HidEndpointDiscoveryStage.Strict, cancellationToken);
        return endpoints
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.VendorId) && !string.IsNullOrWhiteSpace(endpoint.ProductId))
            .ToList();
    }

    public static IReadOnlyList<HidGamepadEndpoint> EnumerateProbeEndpoints(
        string? addressFilter,
        HidEndpointDiscoveryStage discoveryStage,
        CancellationToken cancellationToken)
    {
        var normalizedFilter = AddressNormalizer.NormalizeAddress(addressFilter);
        var results = new List<HidGamepadEndpoint>();
        var byPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    continue;
                }

                var ancestorIds = discoveryStage == HidEndpointDiscoveryStage.Strict
                    ? [TryGetParentInstanceId(devInfoData.DevInst)]
                    : TryGetAncestorInstanceIds(devInfoData.DevInst, 4);

                var isBluetoothEndpoint = IsBluetoothHidEndpoint(instanceId) ||
                                          ancestorIds.Any(ancestor => IsBluetoothHidEndpoint(ancestor));
                var address = ResolveAddress(instanceId, ancestorIds, devicePath);
                var hasAddress = !string.IsNullOrWhiteSpace(address);

                if (discoveryStage == HidEndpointDiscoveryStage.Strict &&
                    (!isBluetoothEndpoint ||
                     !hasAddress ||
                     (!string.IsNullOrWhiteSpace(normalizedFilter) &&
                      !string.Equals(address, normalizedFilter, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                if (discoveryStage == HidEndpointDiscoveryStage.Relaxed &&
                    (!hasAddress ||
                     (!string.IsNullOrWhiteSpace(normalizedFilter) &&
                      !string.Equals(address, normalizedFilter, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                if (!TryResolveVidPid(infoSet, ref devInfoData, instanceId, ancestorIds, devicePath, out var vendorId, out var productId))
                {
                    vendorId = string.Empty;
                    productId = string.Empty;
                }

                if (discoveryStage == HidEndpointDiscoveryStage.GlobalAggressive)
                {
                    using var capabilityHandle = OpenHandle(devicePath);
                    if (capabilityHandle.IsInvalid)
                    {
                        continue;
                    }

                    if (!TryGetCapabilities(capabilityHandle, out var usagePage, out var usage, out _))
                    {
                        continue;
                    }

                    if (usagePage != 0x01 || (usage != 0x04 && usage != 0x05))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(productId))
                    {
                        _ = TryGetDeviceAttributes(capabilityHandle, out vendorId, out productId);
                    }
                }

                var suffix = string.IsNullOrWhiteSpace(address)
                    ? "HID"
                    : address[^Math.Min(4, address.Length)..];
                var displayName = TryGetFriendlyName(infoSet, ref devInfoData) ?? $"Bluetooth {suffix}";
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = normalizedFilter;
                }

                var endpoint = new HidGamepadEndpoint(
                    DevicePath: devicePath,
                    InstanceId: instanceId,
                    Address: address,
                    DisplayName: displayName,
                    VendorId: vendorId,
                    ProductId: productId,
                    DiscoveryStage: discoveryStage);

                if (!byPath.Add(endpoint.DevicePath))
                {
                    continue;
                }

                results.Add(endpoint);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(infoSet);
        }

        return results;
    }

    public static SafeFileHandle OpenHandle(string devicePath)
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

        readWrite.Dispose();
        return CreateFileW(
            normalizedPath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);
    }

    public static StreamReadContext CreateStreamReadContext(SafeFileHandle handle)
    {
        return new StreamReadContext(handle);
    }

    public static bool TryReadInputReport(SafeFileHandle handle, byte reportId, int minimumReportSize, out byte[] report)
    {
        return TryReadInputReport(
            handle,
            reportId,
            minimumReportSize,
            out report,
            StreamReadTimeoutMs,
            retryCount: 0);
    }

    public static bool TryReadInputReport(
        SafeFileHandle handle,
        byte reportId,
        int minimumReportSize,
        out byte[] report,
        int streamReadTimeoutMs,
        int retryCount)
    {
        return TryReadInputReport(
            handle,
            reportId,
            minimumReportSize,
            out report,
            streamReadTimeoutMs,
            retryCount,
            out _,
            streamContext: null,
            maxAttempts: int.MaxValue);
    }

    public static bool TryReadInputReport(
        SafeFileHandle handle,
        byte reportId,
        int minimumReportSize,
        out byte[] report,
        int streamReadTimeoutMs,
        int retryCount,
        out HidReportReadStatistics readStatistics,
        StreamReadContext? streamContext = null,
        int maxAttempts = int.MaxValue)
    {
        var getInputSuccessCount = 0;
        var getInputFailureCount = 0;
        var streamSuccessCount = 0;
        var streamFailureCount = 0;
        var streamTimeoutCount = 0;
        var attemptCount = 0;
        var hardFailCount = 0;

        if (handle.IsInvalid || handle.IsClosed)
        {
            report = [];
            readStatistics = new HidReportReadStatistics(0, 0, 0, 0, 0, 0, 0);
            return false;
        }

        var effectiveMaxAttempts = Math.Max(1, maxAttempts);
        var retries = Math.Max(0, retryCount);
        for (var attempt = 0; attempt <= retries && attemptCount < effectiveMaxAttempts; attempt++)
        {
            if (handle.IsInvalid || handle.IsClosed)
            {
                break;
            }

            var bufferLength = ResolveInputReportSize(handle, minimumReportSize);
            var rented = ArrayPool<byte>.Shared.Rent(bufferLength);
            try
            {
                Array.Clear(rented, 0, bufferLength);
                rented[0] = reportId;
                attemptCount++;

                if (HidD_GetInputReport(handle, rented, bufferLength))
                {
                    getInputSuccessCount++;
                    streamContext?.RegisterGetInputSuccess();
                    report = CopySegment(rented, bufferLength);
                    readStatistics = new HidReportReadStatistics(
                        getInputSuccessCount,
                        getInputFailureCount,
                        streamSuccessCount,
                        streamFailureCount,
                        streamTimeoutCount,
                        attemptCount,
                        hardFailCount);
                    return true;
                }

                getInputFailureCount++;
                var lastError = Marshal.GetLastWin32Error();
                var isHardFail = IsHardFailGetInputError(lastError);
                if (isHardFail)
                {
                    hardFailCount++;
                }

                if (streamContext is not null && streamContext.ShouldSkipFallback(lastError, isHardFail))
                {
                    continue;
                }

                if (TryReadInputReportByStream(
                        streamContext,
                        bufferLength,
                        streamReadTimeoutMs,
                        rented,
                        out var bytesRead,
                        out var streamTimedOut))
                {
                    streamSuccessCount++;
                    // Preserve the existing fixed-length behavior so decoders remain stable.
                    report = CopySegment(rented, Math.Max(bytesRead, bufferLength));
                    readStatistics = new HidReportReadStatistics(
                        getInputSuccessCount,
                        getInputFailureCount,
                        streamSuccessCount,
                        streamFailureCount,
                        streamTimeoutCount,
                        attemptCount,
                        hardFailCount);
                    return true;
                }

                streamFailureCount++;
                if (streamTimedOut)
                {
                    streamTimeoutCount++;
                }
            }
            catch (ObjectDisposedException)
            {
                streamFailureCount++;
                streamContext?.RegisterStreamFailure(timedOut: false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }

            if (attempt < retries)
            {
                Thread.Sleep(12);
            }
        }

        report = [];
        readStatistics = new HidReportReadStatistics(
            getInputSuccessCount,
            getInputFailureCount,
            streamSuccessCount,
            streamFailureCount,
            streamTimeoutCount,
            attemptCount,
            hardFailCount);
        return false;
    }

    public static bool TryReadInputReportSnapshot(
        SafeFileHandle handle,
        byte reportId,
        int minimumReportSize,
        out byte[] report,
        out int errorCode)
    {
        errorCode = 0;
        if (handle.IsInvalid || handle.IsClosed)
        {
            report = [];
            errorCode = ERROR_INVALID_HANDLE;
            return false;
        }

        var bufferLength = ResolveInputReportSize(handle, minimumReportSize);
        var rented = ArrayPool<byte>.Shared.Rent(bufferLength);
        try
        {
            Array.Clear(rented, 0, bufferLength);
            rented[0] = reportId;

            if (HidD_GetInputReport(handle, rented, bufferLength))
            {
                report = CopySegment(rented, bufferLength);
                return true;
            }

            errorCode = Marshal.GetLastWin32Error();
            report = [];
            return false;
        }
        catch
        {
            errorCode = Marshal.GetLastWin32Error();
            report = [];
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    public static bool TryReadFeatureReport(
        SafeFileHandle handle,
        byte reportId,
        int minimumReportSize,
        out byte[] report,
        int retryCount = 1)
    {
        if (handle.IsInvalid || handle.IsClosed)
        {
            report = [];
            return false;
        }

        var retries = Math.Max(0, retryCount);
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            var bufferLength = ResolveFeatureReportSize(handle, minimumReportSize);
            var rented = ArrayPool<byte>.Shared.Rent(bufferLength);
            try
            {
                Array.Clear(rented, 0, bufferLength);
                rented[0] = reportId;

                if (HidD_GetFeature(handle, rented, bufferLength))
                {
                    report = CopySegment(rented, bufferLength);
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }

            if (attempt < retries)
            {
                Thread.Sleep(8);
            }
        }

        report = [];
        return false;
    }

    public static bool TrySendOutputReport(
        SafeFileHandle handle,
        byte reportId,
        int minimumReportSize = 16)
    {
        if (handle.IsInvalid || handle.IsClosed)
        {
            return false;
        }

        var bufferLength = Math.Max(2, minimumReportSize);
        var rented = ArrayPool<byte>.Shared.Rent(bufferLength);
        try
        {
            Array.Clear(rented, 0, bufferLength);
            rented[0] = reportId;
            return HidD_SetOutputReport(handle, rented, bufferLength);
        }
        catch
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    public static bool TrySendOutputPacket(
        SafeFileHandle handle,
        IReadOnlyList<byte> payload)
    {
        if (handle.IsInvalid || handle.IsClosed || payload.Count == 0)
        {
            return false;
        }

        var rented = ArrayPool<byte>.Shared.Rent(payload.Count);
        try
        {
            Array.Clear(rented, 0, payload.Count);
            for (var index = 0; index < payload.Count; index++)
            {
                rented[index] = payload[index];
            }

            return HidD_SetOutputReport(handle, rented, payload.Count);
        }
        catch
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    public static bool TryGetCapabilities(
        SafeFileHandle handle,
        out ushort usagePage,
        out ushort usage,
        out int inputReportByteLength)
    {
        usagePage = 0;
        usage = 0;
        inputReportByteLength = 0;

        if (!TryGetCaps(handle, out var caps))
        {
            return false;
        }

        usagePage = caps.UsagePage;
        usage = caps.Usage;
        inputReportByteLength = caps.InputReportByteLength;
        return true;
    }

    public static bool TryGetDeviceAttributes(
        SafeFileHandle handle,
        out string vendorId,
        out string productId)
    {
        vendorId = string.Empty;
        productId = string.Empty;

        var attributes = new HIDD_ATTRIBUTES
        {
            Size = Marshal.SizeOf<HIDD_ATTRIBUTES>()
        };

        if (!HidD_GetAttributes(handle, ref attributes))
        {
            return false;
        }

        if (attributes.VendorID == 0 || attributes.ProductID == 0)
        {
            return false;
        }

        vendorId = attributes.VendorID.ToString("X4");
        productId = attributes.ProductID.ToString("X4");
        return true;
    }

    public static IReadOnlyList<int> BuildProbeReportSizes(SafeFileHandle handle)
    {
        var results = new List<int>(6);
        if (TryGetCapabilities(handle, out _, out _, out var capsSize) && capsSize > 0)
        {
            results.Add(capsSize);
        }

        foreach (var size in ProbeFallbackSizes)
        {
            if (!results.Contains(size))
            {
                results.Add(size);
            }
        }

        return results;
    }

    public static IReadOnlyList<int> BuildProbeFeatureSizes(SafeFileHandle handle)
    {
        var results = new List<int>(6);
        var capsSize = TryGetFeatureReportSize(handle);
        if (capsSize is > 0)
        {
            results.Add(capsSize.Value);
        }

        foreach (var size in ProbeFeatureFallbackSizes)
        {
            if (!results.Contains(size))
            {
                results.Add(size);
            }
        }

        return results;
    }

    private static int ResolveInputReportSize(SafeFileHandle handle, int minimumSize)
    {
        var deviceSize = TryGetInputReportSize(handle);
        if (deviceSize is null || deviceSize <= 0)
        {
            return minimumSize;
        }

        return Math.Max(minimumSize, deviceSize.Value);
    }

    private static int ResolveFeatureReportSize(SafeFileHandle handle, int minimumSize)
    {
        var deviceSize = TryGetFeatureReportSize(handle);
        if (deviceSize is null || deviceSize <= 0)
        {
            return minimumSize;
        }

        return Math.Max(minimumSize, deviceSize.Value);
    }

    private static int? TryGetInputReportSize(SafeFileHandle handle)
    {
        if (!TryGetCaps(handle, out var caps))
        {
            return null;
        }

        return caps.InputReportByteLength;
    }

    private static int? TryGetFeatureReportSize(SafeFileHandle handle)
    {
        if (!TryGetCaps(handle, out var caps))
        {
            return null;
        }

        return caps.FeatureReportByteLength;
    }

    private static bool TryGetCaps(SafeFileHandle handle, out HIDP_CAPS caps)
    {
        caps = default;
        if (!HidD_GetPreparsedData(handle, out var preparsedData) || preparsedData == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var status = HidP_GetCaps(preparsedData, out caps);
            return status >= 0;
        }
        finally
        {
            HidD_FreePreparsedData(preparsedData);
        }
    }

    private static bool TryResolveVidPid(
        IntPtr infoSet,
        ref SP_DEVINFO_DATA devInfoData,
        string instanceId,
        IReadOnlyList<string> ancestorIds,
        string devicePath,
        out string vendorId,
        out string productId)
    {
        if (HidProbeTextParser.TryParseVidPid(instanceId, out vendorId, out productId))
        {
            return true;
        }

        foreach (var ancestor in ancestorIds)
        {
            if (HidProbeTextParser.TryParseVidPid(ancestor, out vendorId, out productId))
            {
                return true;
            }
        }

        if (HidProbeTextParser.TryParseVidPid(devicePath, out vendorId, out productId))
        {
            return true;
        }

        var hardwareIds = TryGetRegistryString(infoSet, ref devInfoData, SPDRP_HARDWAREID);
        if (HidProbeTextParser.TryParseVidPid(hardwareIds, out vendorId, out productId))
        {
            return true;
        }

        vendorId = string.Empty;
        productId = string.Empty;
        return false;
    }

    private static string ResolveAddress(string instanceId, IReadOnlyList<string> ancestorIds, string devicePath)
    {
        var fromInstance = HidProbeTextParser.ExtractAddress(instanceId);
        if (!string.IsNullOrWhiteSpace(fromInstance))
        {
            return fromInstance;
        }

        foreach (var ancestor in ancestorIds)
        {
            var address = HidProbeTextParser.ExtractAddress(ancestor);
            if (!string.IsNullOrWhiteSpace(address))
            {
                return address;
            }
        }

        return HidProbeTextParser.ExtractAddress(devicePath);
    }

    private static IReadOnlyList<string> TryGetAncestorInstanceIds(uint devInst, int maxDepth)
    {
        var results = new List<string>(Math.Max(1, maxDepth));
        var current = devInst;
        for (var depth = 0; depth < Math.Max(1, maxDepth); depth++)
        {
            if (CM_Get_Parent(out var parentDevInst, current, 0) != CR_SUCCESS)
            {
                break;
            }

            const int maxDeviceIdLen = 512;
            var buffer = new StringBuilder(maxDeviceIdLen);
            if (CM_Get_Device_ID(parentDevInst, buffer, buffer.Capacity, 0) != CR_SUCCESS)
            {
                break;
            }

            var instanceId = buffer.ToString();
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                results.Add(instanceId);
            }

            current = parentDevInst;
        }

        return results;
    }

    private static bool IsBluetoothHidEndpoint(string instanceId)
    {
        return instanceId.StartsWith(@"BTHENUM\{00001124-", StringComparison.OrdinalIgnoreCase) ||
               instanceId.StartsWith(@"HID\{00001124-", StringComparison.OrdinalIgnoreCase);
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

        foreach (var candidate in candidates)
        {
            var normalized = HidDevicePathNormalizer.Normalize(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return string.Empty;
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

    private static bool TryReadInputReportByStream(
        StreamReadContext? streamContext,
        int bufferLength,
        int streamReadTimeoutMs,
        byte[] buffer,
        out int bytesRead,
        out bool streamTimedOut)
    {
        bytesRead = 0;
        streamTimedOut = false;

        if (streamContext is null || !streamContext.IsFallbackEnabled || bufferLength <= 0 || buffer.Length < bufferLength)
        {
            return false;
        }

        try
        {
            return streamContext.TryRead(buffer, bufferLength, streamReadTimeoutMs, out bytesRead, out streamTimedOut);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryBorrowNonOwningHandle(
        SafeFileHandle sourceHandle,
        out SafeFileHandle borrowedHandle,
        out bool addRefAcquired)
    {
        borrowedHandle = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
        addRefAcquired = false;

        if (sourceHandle.IsInvalid || sourceHandle.IsClosed)
        {
            return false;
        }

        try
        {
            sourceHandle.DangerousAddRef(ref addRefAcquired);
            var rawHandle = sourceHandle.DangerousGetHandle();
            if (rawHandle == IntPtr.Zero || rawHandle == InvalidHandleValue)
            {
                ReleaseBorrowedHandle(sourceHandle, addRefAcquired);
                addRefAcquired = false;
                return false;
            }

            borrowedHandle = new SafeFileHandle(rawHandle, ownsHandle: false);
            return true;
        }
        catch
        {
            ReleaseBorrowedHandle(sourceHandle, addRefAcquired);
            addRefAcquired = false;
            return false;
        }
    }

    internal static void ReleaseBorrowedHandle(SafeFileHandle sourceHandle, bool addRefAcquired)
    {
        if (!addRefAcquired)
        {
            return;
        }

        try
        {
            sourceHandle.DangerousRelease();
        }
        catch
        {
            // ignore
        }
    }

    private static bool IsHardFailGetInputError(int errorCode)
    {
        return errorCode is ERROR_INVALID_FUNCTION or
            ERROR_ACCESS_DENIED or
            ERROR_INVALID_HANDLE or
            ERROR_NOT_SUPPORTED or
            ERROR_INVALID_PARAMETER or
            ERROR_DEVICE_NOT_CONNECTED;
    }

    private static byte[] CopySegment(byte[] source, int length)
    {
        var resolvedLength = Math.Clamp(length, 0, source.Length);
        if (resolvedLength == 0)
        {
            return [];
        }

        var result = new byte[resolvedLength];
        Buffer.BlockCopy(source, 0, result, 0, resolvedLength);
        return result;
    }

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
    private const int ERROR_NO_MORE_ITEMS = 259;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int ERROR_INVALID_FUNCTION = 1;
    private const int ERROR_ACCESS_DENIED = 5;
    private const int ERROR_INVALID_HANDLE = 6;
    private const int ERROR_NOT_SUPPORTED = 50;
    private const int ERROR_INVALID_PARAMETER = 87;
    private const int ERROR_DEVICE_NOT_CONNECTED = 1167;
    private const int CR_SUCCESS = 0;

    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_HARDWAREID = 0x00000001;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetInputReport(
        SafeFileHandle hidDeviceObject,
        byte[] reportBuffer,
        int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetFeature(
        SafeFileHandle hidDeviceObject,
        byte[] reportBuffer,
        int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetOutputReport(
        SafeFileHandle hidDeviceObject,
        byte[] reportBuffer,
        int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(
        SafeFileHandle hidDeviceObject,
        out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(
        SafeFileHandle hidDeviceObject,
        ref HIDD_ATTRIBUTES attributes);

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
}
