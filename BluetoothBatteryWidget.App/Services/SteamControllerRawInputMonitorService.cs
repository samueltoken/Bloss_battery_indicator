using System.Runtime.InteropServices;
using System.Text;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class SteamControllerRawInputMonitorService : IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidiDeviceInfo = 0x2000000B;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevPageOnly = 0x00000020;
    private const ushort UsagePageGenericDesktop = 0x01;
    private const ushort UsagePageVendorSteam = 0xFF00;
    private const ushort UsageMouse = 0x02;
    private const ushort UsageJoystick = 0x04;
    private const ushort UsageGamepad = 0x05;
    private const ushort UsageKeyboard = 0x06;
    private const uint RimTypeMouse = 0;
    private const uint RimTypeKeyboard = 1;
    private const uint RimTypeHid = 2;
    private const ushort RiKeyBreak = 0x0001;
    private static readonly TimeSpan PressDebounce = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan DiagnosticRepeatInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ShortPressMinimumDuration = TimeSpan.FromMilliseconds(35);
    private static readonly TimeSpan ShortPressMaximumDuration = TimeSpan.FromMilliseconds(2200);
    private static readonly TimeSpan RawHidFastShortPressMaximumDuration = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan RawHidFastReleaseStabilityDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan RawHidCautiousReleaseStabilityDelay = TimeSpan.FromMilliseconds(450);

    private readonly object _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _lastPressByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly SteamRawHidGuideButtonStateTracker _rawHidGuideButtonStateTracker = new(
        ShortPressMinimumDuration,
        ShortPressMaximumDuration,
        RawHidFastShortPressMaximumDuration,
        RawHidFastReleaseStabilityDelay,
        RawHidCautiousReleaseStabilityDelay);
    private readonly Dictionary<string, DateTimeOffset> _lastDiagnosticByKey = new(StringComparer.OrdinalIgnoreCase);
    private Func<IReadOnlyList<GuideButtonKnownDevice>> _knownDeviceProvider = static () => [];
    private bool _isRegistered;
    private bool _disposed;

    public event EventHandler<GuideButtonPressedEventArgs>? GuideButtonPressed;

    public void SetKnownDeviceProvider(Func<IReadOnlyList<GuideButtonKnownDevice>> provider)
    {
        lock (_sync)
        {
            _knownDeviceProvider = provider ?? (static () => []);
        }
    }

    public void Start(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var devices = new[]
        {
            BuildRawInputDevice(UsageMouse, windowHandle),
            BuildRawInputDevice(UsageJoystick, windowHandle),
            BuildRawInputDevice(UsageGamepad, windowHandle),
            BuildRawInputDevice(UsageKeyboard, windowHandle),
            BuildRawInputPageDevice(UsagePageVendorSteam, windowHandle)
        };

        var registered = RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RawInputDevice>());

        lock (_sync)
        {
            _isRegistered = registered;
        }

        GuideButtonEventLog.Write(
            registered ? "raw_input_started" : "raw_input_start_failed",
            "SteamController",
            string.Empty,
            "Steam Controller",
            registered
                ? "Steam Controller raw keyboard/mouse/HID/vendor-page monitor started."
                : $"Steam Controller raw input registration failed. win32={Marshal.GetLastWin32Error()}.");

        LogRawDeviceSummary();
    }

    public IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmInput || lParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        bool isRegistered;
        lock (_sync)
        {
            isRegistered = _isRegistered && !_disposed;
        }

        if (!isRegistered)
        {
            return IntPtr.Zero;
        }

        TryProcessRawInput(lParam);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _isRegistered = false;
            _knownDeviceProvider = static () => [];
            _lastPressByAddress.Clear();
            _rawHidGuideButtonStateTracker.Clear();
            _lastDiagnosticByKey.Clear();
        }
    }

    public void LogRawDeviceSummary()
    {
        WriteRawDeviceSummary();
    }

    private void TryProcessRawInput(IntPtr rawInputHandle)
    {
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        var size = 0u;
        var queryResult = GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref size, headerSize);
        if (queryResult == uint.MaxValue || size == 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var read = GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize);
            if (read == uint.MaxValue || read != size)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            var deviceName = GetRawInputDeviceName(header.Device);
            if (!TryResolveSteamDevice(deviceName, out var matchedDevice, out var isSteamDevice))
            {
                WriteUnmatchedSteamInputDiagnostic(header.Type, deviceName);
                return;
            }

            switch (header.Type)
            {
                case RimTypeKeyboard:
                    ProcessKeyboardInput(buffer, headerSize, matchedDevice);
                    break;
                case RimTypeMouse:
                    ProcessMouseInput(buffer, headerSize, matchedDevice);
                    break;
                case RimTypeHid:
                    ProcessHidInput(buffer, headerSize, matchedDevice);
                    break;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void ProcessKeyboardInput(IntPtr buffer, uint headerSize, GuideButtonKnownDevice device)
    {
        var keyboard = Marshal.PtrToStructure<RawKeyboard>(IntPtr.Add(buffer, (int)headerSize));
        var isKeyDown = (keyboard.Flags & RiKeyBreak) == 0;
        if (!isKeyDown)
        {
            return;
        }

        WriteDiagnostic(
            $"raw_keyboard:{device.Address}:{keyboard.VKey:X4}:{keyboard.MakeCode:X4}",
            "raw_keyboard_seen",
            device,
            $"Steam raw keyboard input seen. vkey=0x{keyboard.VKey:X4}, make=0x{keyboard.MakeCode:X4}, flags=0x{keyboard.Flags:X4}.");

        // Steam Controller lizard mode maps normal game buttons to keyboard keys
        // such as Esc and Enter. Do not use keyboard input as the guide button.
    }

    private void ProcessMouseInput(IntPtr buffer, uint headerSize, GuideButtonKnownDevice device)
    {
        var mouse = Marshal.PtrToStructure<RawMouse>(IntPtr.Add(buffer, (int)headerSize));
        var buttonFlags = (ushort)(mouse.Buttons & 0xFFFF);
        if (buttonFlags == 0)
        {
            return;
        }

        WriteDiagnostic(
            $"raw_mouse:{device.Address}:{buttonFlags:X4}",
            "raw_mouse_button_seen",
            device,
            $"Steam raw mouse button input seen. flags=0x{buttonFlags:X4}.");
    }

    private void ProcessHidInput(IntPtr buffer, uint headerSize, GuideButtonKnownDevice device)
    {
        var rawHidOffset = (int)headerSize;
        var reportSize = Marshal.ReadInt32(buffer, rawHidOffset);
        var reportCount = Marshal.ReadInt32(buffer, rawHidOffset + 4);
        if (reportSize <= 0 || reportCount <= 0 || reportSize > 512 || reportCount > 32)
        {
            return;
        }

        var dataOffset = rawHidOffset + 8;
        var rawData = new byte[reportSize * reportCount];
        Marshal.Copy(IntPtr.Add(buffer, dataOffset), rawData, 0, rawData.Length);

        for (var index = 0; index < reportCount; index++)
        {
            var report = rawData.AsSpan(index * reportSize, reportSize);
            if (GuideButtonReportParser.TryParseGuideButton(
                    GuideButtonDeviceKind.SteamController,
                    report,
                    out var pressed))
            {
                RegisterRawHidGuideState(device, pressed);
                continue;
            }

            WriteDiagnostic(
                $"raw_hid:{device.Address}:{BuildReportSignature(report)}",
                "raw_hid_unparsed",
                device,
                $"Steam raw HID input was seen but not recognized as the guide button. {FormatReportSample(report)}");
        }
    }

    private void RegisterRawHidGuideState(GuideButtonKnownDevice device, bool pressed)
    {
        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        SteamRawHidGuideButtonDecision decision;
        lock (_sync)
        {
            decision = _rawHidGuideButtonStateTracker.RegisterState(address, pressed, DateTimeOffset.Now);
        }

        if (decision.Kind == SteamRawHidGuideButtonDecisionKind.PendingRelease)
        {
            _ = CompleteRawHidShortPressCandidateAsync(device, address, decision.PendingId, decision.ReleaseDueAt);
        }
    }

    private async Task CompleteRawHidShortPressCandidateAsync(
        GuideButtonKnownDevice device,
        string address,
        Guid pendingId,
        DateTimeOffset releaseDueAt)
    {
        try
        {
            var delay = releaseDueAt - DateTimeOffset.Now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }
        catch
        {
            return;
        }

        SteamRawHidGuideButtonDecision decision;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            decision = _rawHidGuideButtonStateTracker.CompletePendingRelease(
                address,
                pendingId,
                DateTimeOffset.Now);
        }

        if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableShortPress)
        {
            RaiseGuideButtonPressed(device, "raw_hid_stable_short_press", GuideButtonGesture.ShortPress);
        }
        else if (decision.Kind == SteamRawHidGuideButtonDecisionKind.StableLongPress)
        {
            NotifyRawHidLongPressSuppressed(device, address, decision.Duration);
        }
    }

    private void NotifyRawHidLongPressSuppressed(
        GuideButtonKnownDevice device,
        string address,
        TimeSpan duration)
    {
        var displayName = string.IsNullOrWhiteSpace(device.DisplayName)
            ? "Steam Controller"
            : device.DisplayName;
        GuideButtonPressed?.Invoke(
            this,
            new GuideButtonPressedEventArgs(address, displayName, GuideButtonDeviceKind.SteamController, GuideButtonGesture.LongPress));
        GuideButtonEventLog.Write(
            "raw_hid_long_press_suppressed",
            "SteamController",
            address,
            displayName,
            $"Steam raw HID guide hold was ignored. durationMs={(int)duration.TotalMilliseconds}.");
    }

    private bool TryResolveSteamDevice(
        string deviceName,
        out GuideButtonKnownDevice matchedDevice,
        out bool isSteamDevice)
    {
        matchedDevice = new GuideButtonKnownDevice(string.Empty, "Steam Controller", GuideButtonDeviceKind.SteamController);
        isSteamDevice = false;

        var knownDevices = ReadKnownSteamDevices();
        if (knownDevices.Count == 0)
        {
            return false;
        }

        var address = AddressNormalizer.ExtractAddressFromInstanceId(deviceName);
        if (!string.IsNullOrWhiteSpace(address))
        {
            var byAddress = knownDevices.FirstOrDefault(
                device => string.Equals(
                    AddressNormalizer.NormalizeAddress(device.Address),
                    address,
                    StringComparison.OrdinalIgnoreCase));
            if (byAddress is not null)
            {
                matchedDevice = byAddress;
                isSteamDevice = true;
                return true;
            }
        }

        if (HidProbeTextParser.TryParseVidPid(deviceName, out var vendorId, out var productId) &&
            IsSteamRawInputVidPid(vendorId, productId) &&
            knownDevices.Count == 1)
        {
            matchedDevice = knownDevices[0];
            isSteamDevice = true;
            return true;
        }

        return false;
    }

    private static bool IsSteamRawInputVidPid(string? vendorId, string? productId)
    {
        if (string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(productId))
        {
            return false;
        }

        var isKnownSteamVendor =
            string.Equals(vendorId, "28DE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(vendorId, "0228", StringComparison.OrdinalIgnoreCase);
        if (!isKnownSteamVendor)
        {
            return false;
        }

        return string.Equals(productId, "1303", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1304", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1305", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(productId, "1142", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteRawDeviceSummary()
    {
        var knownDevices = ReadKnownSteamDevices();
        var rawDevices = EnumerateRawInputDevices();
        var steamCandidates = rawDevices
            .Where(device => IsSteamCandidateDeviceName(device.DeviceName, knownDevices))
            .Select(device => BuildRawDeviceSummaryItem(device, knownDevices))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var summary = steamCandidates.Count == 0
            ? "No Steam-looking raw input device was listed."
            : string.Join("; ", steamCandidates);

        GuideButtonEventLog.Write(
            "raw_input_devices",
            "SteamController",
            knownDevices.Count == 1 ? knownDevices[0].Address : string.Empty,
            "Steam Controller",
            $"RawInput devices={rawDevices.Count}, knownSteam={knownDevices.Count}, steamCandidates={steamCandidates.Count}. {summary}");
    }

    private void WriteUnmatchedSteamInputDiagnostic(uint rawInputType, string deviceName)
    {
        var knownDevices = ReadKnownSteamDevices();
        if (!IsSteamCandidateDeviceName(deviceName, knownDevices))
        {
            return;
        }

        var address = AddressNormalizer.ExtractAddressFromInstanceId(deviceName);
        var addressText = string.IsNullOrWhiteSpace(address)
            ? "none"
            : GuideButtonLogFormatter.MaskAddress(address);
        var vidPidText = HidProbeTextParser.TryParseVidPid(deviceName, out var vendorId, out var productId)
            ? $"vid={vendorId}, pid={productId}"
            : "vid=unknown, pid=unknown";
        var rawInfoText = TryGetRawHidInfoByName(deviceName, out var usagePage, out var usage)
            ? $", usagePage=0x{usagePage:X4}, usage=0x{usage:X4}"
            : string.Empty;
        WriteDiagnostic(
            $"raw_unmatched:{rawInputType}:{vidPidText}:{addressText}",
            "raw_unmatched_steam_input",
            new GuideButtonKnownDevice(string.Empty, "Steam Controller", GuideButtonDeviceKind.SteamController),
            $"Steam-looking raw input was seen but did not match a known battery-list address. type={FormatRawInputType(rawInputType)}, {vidPidText}{rawInfoText}, address={addressText}, knownSteam={knownDevices.Count}.");
    }

    private IReadOnlyList<GuideButtonKnownDevice> ReadKnownSteamDevices()
    {
        Func<IReadOnlyList<GuideButtonKnownDevice>> provider;
        lock (_sync)
        {
            provider = _knownDeviceProvider;
        }

        try
        {
            return provider()
                .Where(device => device.DeviceKind == GuideButtonDeviceKind.SteamController)
                .Select(device => device with
                {
                    Address = AddressNormalizer.NormalizeAddress(device.Address),
                    DisplayName = string.IsNullOrWhiteSpace(device.DisplayName)
                        ? "Steam Controller"
                        : device.DisplayName
                })
                .Where(device => !string.IsNullOrWhiteSpace(device.Address))
                .GroupBy(device => device.Address, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void RaiseGuideButtonPressed(GuideButtonKnownDevice device, string source, GuideButtonGesture gesture)
    {
        var address = AddressNormalizer.NormalizeAddress(device.Address);
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            if (_lastPressByAddress.TryGetValue(address, out var lastPress) &&
                now - lastPress <= PressDebounce)
            {
                return;
            }

            _lastPressByAddress[address] = now;
        }

        var displayName = string.IsNullOrWhiteSpace(device.DisplayName)
            ? "Steam Controller"
            : device.DisplayName;
        GuideButtonPressed?.Invoke(
            this,
            new GuideButtonPressedEventArgs(address, displayName, GuideButtonDeviceKind.SteamController, gesture));
        GuideButtonEventLog.Write(
            "pressed",
            "SteamController",
            address,
            displayName,
            $"Guide button {gesture} detected from {source}.");
    }

    private void WriteDiagnostic(string key, string eventName, GuideButtonKnownDevice device, string message)
    {
        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            if (_lastDiagnosticByKey.TryGetValue(key, out var lastSeen) &&
                now - lastSeen <= DiagnosticRepeatInterval)
            {
                return;
            }

            _lastDiagnosticByKey[key] = now;
        }

        GuideButtonEventLog.Write(
            eventName,
            "SteamController",
            device.Address,
            string.IsNullOrWhiteSpace(device.DisplayName) ? "Steam Controller" : device.DisplayName,
            message);
    }

    private static RawInputDevice BuildRawInputDevice(ushort usage, IntPtr windowHandle)
    {
        return new RawInputDevice
        {
            UsagePage = UsagePageGenericDesktop,
            Usage = usage,
            Flags = RidevInputSink,
            Target = windowHandle
        };
    }

    private static RawInputDevice BuildRawInputPageDevice(ushort usagePage, IntPtr windowHandle)
    {
        return new RawInputDevice
        {
            UsagePage = usagePage,
            Usage = 0,
            Flags = RidevInputSink | RidevPageOnly,
            Target = windowHandle
        };
    }

    private static IReadOnlyList<RawInputDeviceInfo> EnumerateRawInputDevices()
    {
        var count = 0u;
        var headerSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
        var query = GetRawInputDeviceList(IntPtr.Zero, ref count, headerSize);
        if (query == uint.MaxValue || count == 0)
        {
            return [];
        }

        var devices = new RawInputDeviceList[count];
        var read = GetRawInputDeviceList(devices, ref count, headerSize);
        if (read == uint.MaxValue)
        {
            return [];
        }

        var results = new List<RawInputDeviceInfo>((int)Math.Min(count, read));
        for (var index = 0; index < read && index < devices.Length; index++)
        {
            var device = devices[index];
            results.Add(new RawInputDeviceInfo(
                device.Device,
                device.Type,
                GetRawInputDeviceName(device.Device)));
        }

        return results;
    }

    private static bool IsSteamCandidateDeviceName(
        string deviceName,
        IReadOnlyList<GuideButtonKnownDevice> knownDevices)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        if (deviceName.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("28DE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var address = AddressNormalizer.ExtractAddressFromInstanceId(deviceName);
        return !string.IsNullOrWhiteSpace(address) &&
               knownDevices.Any(device => string.Equals(
                   AddressNormalizer.NormalizeAddress(device.Address),
                   address,
                   StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildRawDeviceSummaryItem(
        RawInputDeviceInfo device,
        IReadOnlyList<GuideButtonKnownDevice> knownDevices)
    {
        var address = AddressNormalizer.ExtractAddressFromInstanceId(device.DeviceName);
        var matchedKnownAddress = !string.IsNullOrWhiteSpace(address) &&
                                  knownDevices.Any(knownDevice => string.Equals(
                                      AddressNormalizer.NormalizeAddress(knownDevice.Address),
                                      address,
                                      StringComparison.OrdinalIgnoreCase));
        var addressText = string.IsNullOrWhiteSpace(address)
            ? "none"
            : GuideButtonLogFormatter.MaskAddress(address);
        var vidPidText = HidProbeTextParser.TryParseVidPid(device.DeviceName, out var vendorId, out var productId)
            ? $"vid={vendorId},pid={productId}"
            : "vid=unknown,pid=unknown";
        var hidInfoText = TryGetRawHidInfo(device.Device, out _, out _, out var usagePage, out var usage)
            ? $",usagePage=0x{usagePage:X4},usage=0x{usage:X4}"
            : string.Empty;
        return $"type={FormatRawInputType(device.Type)},{vidPidText}{hidInfoText},address={addressText},addressMatched={matchedKnownAddress}";
    }

    private static string FormatRawInputType(uint rawInputType)
    {
        return rawInputType switch
        {
            RimTypeMouse => "Mouse",
            RimTypeKeyboard => "Keyboard",
            RimTypeHid => "HID",
            _ => rawInputType.ToString("X")
        };
    }

    private static string GetRawInputDeviceName(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        var size = 0u;
        _ = GetRawInputDeviceInfo(deviceHandle, RidiDeviceName, null, ref size);
        if (size == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((int)size);
        var result = GetRawInputDeviceInfo(deviceHandle, RidiDeviceName, builder, ref size);
        return result == uint.MaxValue ? string.Empty : builder.ToString();
    }

    private static bool TryGetRawHidInfoByName(string deviceName, out ushort usagePage, out ushort usage)
    {
        usagePage = 0;
        usage = 0;
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        foreach (var device in EnumerateRawInputDevices())
        {
            if (string.Equals(device.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase) &&
                TryGetRawHidInfo(device.Device, out _, out _, out usagePage, out usage))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRawHidInfo(
        IntPtr deviceHandle,
        out string vendorId,
        out string productId,
        out ushort usagePage,
        out ushort usage)
    {
        vendorId = string.Empty;
        productId = string.Empty;
        usagePage = 0;
        usage = 0;
        if (deviceHandle == IntPtr.Zero)
        {
            return false;
        }

        var size = (uint)Marshal.SizeOf<RawInputDeviceInfoData>();
        var info = new RawInputDeviceInfoData
        {
            Size = size
        };
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.StructureToPtr(info, buffer, fDeleteOld: false);
            var result = GetRawInputDeviceInfo(deviceHandle, RidiDeviceInfo, buffer, ref size);
            if (result == uint.MaxValue)
            {
                return false;
            }

            info = Marshal.PtrToStructure<RawInputDeviceInfoData>(buffer);
            if (info.Type != RimTypeHid)
            {
                return false;
            }

            vendorId = info.VendorId.ToString("X4");
            productId = info.ProductId.ToString("X4");
            usagePage = info.UsagePage;
            usage = info.Usage;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string BuildReportSignature(ReadOnlySpan<byte> report)
    {
        var sampleLength = Math.Min(6, report.Length);
        Span<char> chars = stackalloc char[sampleLength * 2];
        for (var index = 0; index < sampleLength; index++)
        {
            _ = report[index].TryFormat(chars.Slice(index * 2, 2), out _, "X2");
        }

        return new string(chars);
    }

    private static string FormatReportSample(ReadOnlySpan<byte> report)
    {
        var sampleLength = Math.Min(12, report.Length);
        var parts = new string[sampleLength];
        for (var index = 0; index < sampleLength; index++)
        {
            parts[index] = report[index].ToString("X2");
        }

        return $"len={report.Length}, first={string.Join('-', parts)}.";
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        IntPtr pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RawInputDeviceList[]? pRawInputDeviceList,
        ref uint puiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        StringBuilder? pData,
        ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList
    {
        public IntPtr Device;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceInfoData
    {
        public uint Size;
        public uint Type;
        public uint VendorId;
        public uint ProductId;
        public uint VersionNumber;
        public ushort UsagePage;
        public ushort Usage;
    }

    private sealed record RawInputDeviceInfo(
        IntPtr Device,
        uint Type,
        string DeviceName);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public uint Buttons;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }
}
