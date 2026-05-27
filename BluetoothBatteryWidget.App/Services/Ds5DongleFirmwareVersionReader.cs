using System.Text;

namespace BluetoothBatteryWidget.App.Services;

internal sealed record Ds5DongleFirmwareVersionResult(
    string Version,
    string DevicePath,
    string ProductId);

internal enum Ds5DongleFirmwareVersionReadStatus
{
    Found,
    NoUsbDs5DongleEndpoint,
    OnlyBluetoothDualSenseEndpoints,
    UsbDs5DongleOpenFailed,
    FirmwareVersionReportUnavailable
}

internal sealed record Ds5DongleFirmwareVersionScanResult(
    Ds5DongleFirmwareVersionReadStatus Status,
    Ds5DongleFirmwareVersionResult? Firmware,
    int ScannedEndpointCount,
    int CandidateEndpointCount,
    int OpenedCandidateEndpointCount,
    int BluetoothSonyEndpointCount);

internal static class Ds5DongleFirmwareVersionReader
{
    internal const byte FirmwareVersionReportId = 0xF8;
    private static readonly int[] FirmwareVersionReportSizes = [64, 63, 65, 128];

    public static Ds5DongleFirmwareVersionResult? TryReadCurrentVersion(CancellationToken cancellationToken)
    {
        return ReadCurrentVersion(cancellationToken).Firmware;
    }

    internal static Ds5DongleFirmwareVersionScanResult ReadCurrentVersion(CancellationToken cancellationToken)
    {
        var scannedEndpointCount = 0;
        var candidateEndpointCount = 0;
        var openedCandidateEndpointCount = 0;
        var bluetoothSonyEndpointCount = 0;

        foreach (var endpoint in EnumerateCandidateEndpoints(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            scannedEndpointCount++;

            if (IsSupportedBluetoothEndpoint(endpoint))
            {
                bluetoothSonyEndpointCount++;
            }

            if (!IsCandidateEndpoint(endpoint))
            {
                continue;
            }

            candidateEndpointCount++;
            using var handle = HidGamepadAccess.OpenHandle(endpoint.DevicePath);
            if (handle.IsInvalid)
            {
                continue;
            }

            openedCandidateEndpointCount++;
            if (TryReadFirmwareVersion(handle, out var version))
            {
                var firmware = new Ds5DongleFirmwareVersionResult(
                    version,
                    endpoint.DevicePath,
                    endpoint.ProductId);

                return new Ds5DongleFirmwareVersionScanResult(
                    Ds5DongleFirmwareVersionReadStatus.Found,
                    firmware,
                    scannedEndpointCount,
                    candidateEndpointCount,
                    openedCandidateEndpointCount,
                    bluetoothSonyEndpointCount);
            }
        }

        return new Ds5DongleFirmwareVersionScanResult(
            ClassifyFailureStatus(
                candidateEndpointCount,
                openedCandidateEndpointCount,
                bluetoothSonyEndpointCount),
            null,
            scannedEndpointCount,
            candidateEndpointCount,
            openedCandidateEndpointCount,
            bluetoothSonyEndpointCount);
    }

    internal static IEnumerable<HidGamepadEndpoint> EnumerateCandidateEndpoints(CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in HidGamepadAccess.EnumeratePresentHidEndpoints(cancellationToken))
        {
            if (seen.Add(endpoint.DevicePath))
            {
                yield return endpoint;
            }
        }

        foreach (var endpoint in HidGamepadAccess.EnumerateProbeEndpoints(
                     addressFilter: null,
                     HidEndpointDiscoveryStage.GlobalAggressive,
                     cancellationToken))
        {
            if (seen.Add(endpoint.DevicePath))
            {
                yield return endpoint;
            }
        }
    }

    internal static bool IsCandidateEndpoint(HidGamepadEndpoint endpoint)
    {
        if (ContainsBluetoothMarker(endpoint.InstanceId) ||
            ContainsBluetoothMarker(endpoint.DevicePath))
        {
            return false;
        }

        return PlayStationUsbBridgeSupport.IsSupportedVidPid(endpoint.VendorId, endpoint.ProductId) ||
               HasSupportedVidPid(endpoint.InstanceId) ||
               HasSupportedVidPid(endpoint.DevicePath);
    }

    internal static bool IsSupportedBluetoothEndpoint(HidGamepadEndpoint endpoint)
    {
        if (!ContainsBluetoothMarker(endpoint.InstanceId) &&
            !ContainsBluetoothMarker(endpoint.DevicePath))
        {
            return false;
        }

        return PlayStationUsbBridgeSupport.IsSupportedVidPid(endpoint.VendorId, endpoint.ProductId) ||
               HasSupportedVidPid(endpoint.InstanceId) ||
               HasSupportedVidPid(endpoint.DevicePath);
    }

    internal static Ds5DongleFirmwareVersionReadStatus ClassifyFailureStatus(
        int candidateEndpointCount,
        int openedCandidateEndpointCount,
        int bluetoothSonyEndpointCount)
    {
        if (candidateEndpointCount <= 0)
        {
            return bluetoothSonyEndpointCount > 0
                ? Ds5DongleFirmwareVersionReadStatus.OnlyBluetoothDualSenseEndpoints
                : Ds5DongleFirmwareVersionReadStatus.NoUsbDs5DongleEndpoint;
        }

        return openedCandidateEndpointCount <= 0
            ? Ds5DongleFirmwareVersionReadStatus.UsbDs5DongleOpenFailed
            : Ds5DongleFirmwareVersionReadStatus.FirmwareVersionReportUnavailable;
    }

    internal static bool TryDecodeFirmwareVersionReport(byte[] report, out string version)
    {
        version = string.Empty;
        if (report.Length == 0)
        {
            return false;
        }

        var offsets = report[0] == FirmwareVersionReportId
            ? new[] { 1, 0 }
            : [0, 1];

        foreach (var offset in offsets)
        {
            if (TryDecodeAsciiVersion(report, offset, out version))
            {
                return true;
            }
        }

        version = string.Empty;
        return false;
    }

    private static bool TryReadFirmwareVersion(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out string version)
    {
        version = string.Empty;
        foreach (var reportSize in BuildFirmwareVersionReportSizes(handle))
        {
            if (!HidGamepadAccess.TryReadFeatureReportExact(
                    handle,
                    FirmwareVersionReportId,
                    reportSize,
                    out var report,
                    retryCount: 2))
            {
                continue;
            }

            if (TryDecodeFirmwareVersionReport(report, out version))
            {
                return true;
            }
        }

        return false;
    }

    internal static IReadOnlyList<int> BuildFirmwareVersionReportSizes(Microsoft.Win32.SafeHandles.SafeFileHandle handle)
    {
        var results = new List<int>();
        foreach (var size in FirmwareVersionReportSizes)
        {
            AddReportSize(results, size);
        }

        foreach (var size in HidGamepadAccess.BuildProbeFeatureSizes(handle))
        {
            AddReportSize(results, size);
        }

        return results;
    }

    private static void AddReportSize(List<int> sizes, int size)
    {
        if (size <= 1 || sizes.Contains(size))
        {
            return;
        }

        sizes.Add(size);
    }

    private static bool TryDecodeAsciiVersion(byte[] report, int offset, out string version)
    {
        version = string.Empty;
        if (offset >= report.Length)
        {
            return false;
        }

        var count = 0;
        for (var index = offset; index < report.Length; index++)
        {
            var value = report[index];
            if (value == 0)
            {
                break;
            }

            if (value < 0x20 || value > 0x7E)
            {
                return false;
            }

            count++;
        }

        if (count == 0)
        {
            return false;
        }

        var text = Encoding.ASCII.GetString(report, offset, count).Trim();
        if (!IsLikelyVersionString(text))
        {
            return false;
        }

        version = text;
        return true;
    }

    private static bool IsLikelyVersionString(string text)
    {
        if (text.Length is < 2 or > 40)
        {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(text[0]))
        {
            return false;
        }

        var hasDigit = false;
        var hasDot = false;
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (ch == '.')
            {
                hasDot = true;
                continue;
            }

            if (char.IsAsciiLetter(ch) ||
                ch is '-' or '_' or '+')
            {
                continue;
            }

            return false;
        }

        return hasDigit && hasDot;
    }

    private static bool HasSupportedVidPid(string? text)
    {
        return BluetoothBatteryWidget.Core.Services.HidProbeTextParser.TryParseVidPid(text, out var vendorId, out var productId) &&
               PlayStationUsbBridgeSupport.IsSupportedVidPid(vendorId, productId);
    }

    private static bool ContainsBluetoothMarker(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("BLUETOOTH", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("{00001124-0000-1000-8000-00805F9B34FB}", StringComparison.OrdinalIgnoreCase);
    }
}
