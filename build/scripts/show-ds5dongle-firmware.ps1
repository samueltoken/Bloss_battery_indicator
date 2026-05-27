[CmdletBinding()]
param(
    [switch]$ShowAllSonyHid
)

$ErrorActionPreference = 'Stop'

$source = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BlossDiagnostics
{
    public sealed class Ds5DongleProbeResult
    {
        public string ProductId { get; set; }
        public string DevicePath { get; set; }
        public string Status { get; set; }
        public string Version { get; set; }
        public int ReportSize { get; set; }
        public int ErrorCode { get; set; }

        public Ds5DongleProbeResult()
        {
            ProductId = "";
            DevicePath = "";
            Status = "";
            Version = "";
        }
    }

    public static class Ds5DongleHidProbe
    {
        private const int DIGCF_PRESENT = 0x00000002;
        private const int DIGCF_DEVICEINTERFACE = 0x00000010;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private static readonly int[] FirmwareReportSizes = new[] { 64, 63, 65, 128 };

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public UIntPtr Reserved;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            int memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetailW(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize,
            out int requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        public static List<Ds5DongleProbeResult> Probe()
        {
            var results = new List<Ds5DongleProbeResult>();
            foreach (var devicePath in EnumerateHidDevicePaths())
            {
                var lower = devicePath.ToLowerInvariant();
                if (!IsSonyDs5BridgePath(lower))
                {
                    continue;
                }

                if (lower.Contains("bthenum") || lower.Contains("bluetooth"))
                {
                    results.Add(new Ds5DongleProbeResult
                    {
                        ProductId = ResolveProductId(lower),
                        DevicePath = devicePath,
                        Status = "bluetooth_endpoint_skipped"
                    });
                    continue;
                }

                results.Add(ReadFirmwareVersion(devicePath, ResolveProductId(lower)));
            }

            return results;
        }

        private static Ds5DongleProbeResult ReadFirmwareVersion(string devicePath, string productId)
        {
            var result = new Ds5DongleProbeResult
            {
                ProductId = productId,
                DevicePath = devicePath,
                Status = "open_failed"
            };

            var handle = OpenHandle(devicePath, GENERIC_READ | GENERIC_WRITE);
            var readHandle = handle;
            SafeFileHandle fallbackHandle = null;
            if (readHandle.IsInvalid)
            {
                fallbackHandle = OpenHandle(devicePath, GENERIC_READ);
                readHandle = fallbackHandle;
            }

            try
            {
                if (readHandle.IsInvalid)
                {
                    result.ErrorCode = Marshal.GetLastWin32Error();
                    return result;
                }

                foreach (var reportSize in FirmwareReportSizes)
                {
                    var buffer = new byte[reportSize];
                    buffer[0] = 0xF8;
                    if (!HidD_GetFeature(readHandle, buffer, buffer.Length))
                    {
                        result.ErrorCode = Marshal.GetLastWin32Error();
                        continue;
                    }

                    string version;
                    if (TryDecodeVersion(buffer, out version))
                    {
                        result.Status = "found";
                        result.Version = version;
                        result.ReportSize = reportSize;
                        result.ErrorCode = 0;
                        return result;
                    }

                    result.Status = "report_decoded_false";
                    result.ReportSize = reportSize;
                }

                if (result.Status == "open_failed")
                {
                    result.Status = "feature_report_unavailable";
                }

                return result;
            }
            finally
            {
                if (fallbackHandle != null)
                {
                    fallbackHandle.Dispose();
                }

                handle.Dispose();
            }
        }

        private static SafeFileHandle OpenHandle(string devicePath, uint desiredAccess)
        {
            return CreateFileW(
                devicePath,
                desiredAccess,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);
        }

        private static IEnumerable<string> EnumerateHidDevicePaths()
        {
            var results = new List<string>();
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            var infoSet = SetupDiGetClassDevsW(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (infoSet == IntPtr.Zero || infoSet == InvalidHandleValue)
            {
                return results;
            }

            try
            {
                for (var index = 0; ; index++)
                {
                    var interfaceData = new SP_DEVICE_INTERFACE_DATA
                    {
                        cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA))
                    };

                    if (!SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                    {
                        if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }

                        continue;
                    }

                    int requiredSize;
                    SetupDiGetDeviceInterfaceDetailW(infoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
                    if (requiredSize <= 0)
                    {
                        continue;
                    }

                    var detailBuffer = Marshal.AllocHGlobal(requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                        int requiredSizeAgain;
                        if (!SetupDiGetDeviceInterfaceDetailW(infoSet, ref interfaceData, detailBuffer, requiredSize, out requiredSizeAgain, IntPtr.Zero))
                        {
                            continue;
                        }

                        var path = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            results.Add(path);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(infoSet);
            }

            return results;
        }

        private static bool IsSonyDs5BridgePath(string lowerPath)
        {
            return lowerPath.Contains("vid_054c&pid_0ce6") ||
                   lowerPath.Contains("vid_054c&pid_0df2");
        }

        private static string ResolveProductId(string lowerPath)
        {
            if (lowerPath.Contains("pid_0df2"))
            {
                return "0DF2";
            }

            if (lowerPath.Contains("pid_0ce6"))
            {
                return "0CE6";
            }

            return "";
        }

        private static bool TryDecodeVersion(byte[] report, out string version)
        {
            version = "";
            var offsets = report.Length > 0 && report[0] == 0xF8
                ? new[] { 1, 0 }
                : new[] { 0, 1 };

            foreach (var offset in offsets)
            {
                if (TryDecodeAsciiVersion(report, offset, out version))
                {
                    return true;
                }
            }

            version = "";
            return false;
        }

        private static bool TryDecodeAsciiVersion(byte[] report, int offset, out string version)
        {
            version = "";
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
            if (!IsLikelyVersion(text))
            {
                return false;
            }

            version = text;
            return true;
        }

        private static bool IsLikelyVersion(string text)
        {
            if (text.Length < 2 || text.Length > 40)
            {
                return false;
            }

            var hasDigit = false;
            var hasDot = false;
            foreach (var ch in text)
            {
                if (ch >= '0' && ch <= '9')
                {
                    hasDigit = true;
                    continue;
                }

                if (ch == '.')
                {
                    hasDot = true;
                    continue;
                }

                if ((ch >= 'A' && ch <= 'Z') ||
                    (ch >= 'a' && ch <= 'z') ||
                    ch == '-' ||
                    ch == '_' ||
                    ch == '+')
                {
                    continue;
                }

                return false;
            }

            return hasDigit && hasDot;
        }
    }
}
"@

if (-not ([System.Management.Automation.PSTypeName]'BlossDiagnostics.Ds5DongleHidProbe').Type)
{
    Add-Type -TypeDefinition $source -Language CSharp
}

Write-Host "DS5Dongle firmware probe"
Write-Host "Source method: Sony USB HID VID_054C PID_0CE6/0DF2, Feature Report 0xF8"
Write-Host ""

$bootDrives = Get-Volume -ErrorAction SilentlyContinue |
    Where-Object { $_.FileSystemLabel -match '^(RP2350|RPI-RP2)$' } |
    Select-Object DriveLetter, FileSystemLabel, DriveType

if ($bootDrives)
{
    Write-Host "BOOTSEL storage detected:"
    $bootDrives | Format-Table -AutoSize
    Write-Host "A BOOTSEL drive cannot report the installed app firmware through HID. Use the updater copy step in this state."
    Write-Host ""
}

$pnpSony = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
    Where-Object {
        $_.InstanceId -match 'VID[_&]0*054C.*PID[_&](0CE6|0DF2)' -or
        $_.FriendlyName -match 'DualSense|DS5|DS5Dongle|Pico'
    } |
    Select-Object Class, FriendlyName, InstanceId, Status

if ($pnpSony)
{
    Write-Host "Windows PnP Sony/Pico-like devices:"
    if ($ShowAllSonyHid)
    {
        $pnpSony | Format-List
    }
    else
    {
        $pnpSony | Select-Object Class, FriendlyName, Status | Format-Table -AutoSize
    }
    Write-Host ""
}
else
{
    Write-Host "Windows PnP does not currently show a Sony/Pico-like device."
    Write-Host ""
}

$results = [BlossDiagnostics.Ds5DongleHidProbe]::Probe()
if ($results.Count -eq 0)
{
    Write-Host "No USB HID endpoint matching DS5Dongle VID_054C PID_0CE6/0DF2 was found."
    Write-Host "If Pico2W is connected normally, turn on/connect DualSense through Pico2W and try again."
    Write-Host "DS5Dongle exposes the normal USB HID device only after the controller is connected."
    exit 2
}

$found = $false
foreach ($result in $results)
{
    if ($result.Status -eq 'found')
    {
        $found = $true
    }
}

Write-Host "DS5Dongle HID probe results:"
$results |
    Select-Object ProductId, Status, Version, ReportSize, ErrorCode, DevicePath |
    Format-Table -AutoSize -Wrap

if ($found)
{
    exit 0
}

Write-Host ""
Write-Host "A matching endpoint was found, but firmware version was not read."
Write-Host "Likely causes: firmware older than v0.6.0-hotfix, another app/browser holds the HID device, or Windows exposed only a non-feature-report endpoint."
exit 3
