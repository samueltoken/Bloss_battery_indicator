using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class SteamControllerRawInputMonitorServiceTests
{
    [Fact]
    public void ShouldTreatHidReportChangeAsActivity_IgnoresSameReport()
    {
        var neutral = BuildSteamReport();

        Assert.False(SteamControllerRawInputMonitorService.ShouldTreatHidReportChangeAsActivity(neutral, neutral));
    }

    [Fact]
    public void ShouldTreatHidReportChangeAsActivity_IgnoresDifferentReportIdBaseline()
    {
        var previous = BuildSteamReport(reportId: 0x42);
        var current = BuildSteamReport(reportId: 0x45, aPressed: true);

        Assert.False(SteamControllerRawInputMonitorService.ShouldTreatHidReportChangeAsActivity(previous, current));
    }

    [Fact]
    public void ShouldTreatHidReportChangeAsActivity_UsesButtonTransition()
    {
        var previous = BuildSteamReport();
        var current = BuildSteamReport(aPressed: true);

        Assert.True(SteamControllerRawInputMonitorService.ShouldTreatHidReportChangeAsActivity(previous, current));
    }

    [Fact]
    public void ShouldTreatHidReportChangeAsActivity_UsesPadAxisTransition()
    {
        var previous = BuildSteamReport();
        var current = BuildSteamReport(leftPadX: 12000);

        Assert.False(SteamControllerRawInputMonitorService.ShouldTreatHidReportChangeAsActivity(previous, current));
    }

    [Fact]
    public void ShouldTreatHidReportChangeAsActivity_DualSenseUsesDeviceKind()
    {
        var previous = BuildDualSenseReport();
        var current = BuildDualSenseReport(psPressed: true);

        Assert.True(SteamControllerRawInputMonitorService.ShouldTreatHidReportChangeAsActivity(
            GuideButtonDeviceKind.DualSense,
            previous,
            current));
    }

    [Fact]
    public void RawInputHidPath_UsesMatchedDeviceKindInsteadOfSteamOnly()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var source = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));
        var processHidStart = source.IndexOf("private void ProcessHidInput", StringComparison.Ordinal);
        var processHidEnd = source.IndexOf("private void RaiseInputActivityReceived", processHidStart, StringComparison.Ordinal);
        var processHidMethod = source[processHidStart..processHidEnd];
        var readKnownStart = source.IndexOf("private IReadOnlyList<GuideButtonKnownDevice> ReadKnownRawInputGuideDevices", StringComparison.Ordinal);
        var readKnownEnd = source.IndexOf("private void RaiseGuideButtonPressed", readKnownStart, StringComparison.Ordinal);
        var readKnownMethod = source[readKnownStart..readKnownEnd];

        Assert.Contains("device.DeviceKind", processHidMethod);
        Assert.Contains("GuideButtonReportParser.TryParseGuideButton(", processHidMethod);
        Assert.Contains("device.DeviceKind", processHidMethod);
        Assert.Contains("new GuideButtonInputReportEventArgs(", processHidMethod);
        Assert.DoesNotContain("GuideButtonDeviceKind.SteamController,\r\n                    report));", processHidMethod);
        Assert.Contains("GuideButtonDeviceKind.DualSense", readKnownMethod);
        Assert.Contains("IsDualSenseRawInputVidPid", source);
        Assert.Contains("\"DualSense Wireless Controller\"", source);
    }

    [Fact]
    public void RawInputHidPath_PromotesFirstWakeOnlyGuidePress()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var source = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));
        var processHidStart = source.IndexOf("private void ProcessHidInput", StringComparison.Ordinal);
        var processHidEnd = source.IndexOf("private void RaiseInputActivityReceived", processHidStart, StringComparison.Ordinal);
        var processHidMethod = source[processHidStart..processHidEnd];

        Assert.Contains("TryRegisterRawHidGuidePressEdge(device, pressed)", processHidMethod);
        Assert.Contains("RaiseInputActivityReceived(device, countsAsUserActivity: true, isWakeEligible: true);", processHidMethod);
        Assert.Contains("device.DeviceKind != GuideButtonDeviceKind.SteamController", processHidMethod);
        Assert.Contains("RaiseGuideButtonPressed(device, \"raw_hid_guide_press_edge\", GuideButtonGesture.ShortPress);", processHidMethod);
        Assert.Contains("_lastRawHidGuidePressedByAddress", source);
    }

    [Fact]
    public void RawInputHidPath_DoesNotPromoteInitialPressedStateAsGuidePress()
    {
        Assert.False(SteamControllerRawInputMonitorService.ShouldRaiseRawHidGuidePressEdge(
            hasPrevious: false,
            previousPressed: false,
            currentPressed: true));
        Assert.False(SteamControllerRawInputMonitorService.ShouldRaiseRawHidGuidePressEdge(
            hasPrevious: true,
            previousPressed: false,
            currentPressed: false));
        Assert.True(SteamControllerRawInputMonitorService.ShouldRaiseRawHidGuidePressEdge(
            hasPrevious: true,
            previousPressed: false,
            currentPressed: true));
        Assert.False(SteamControllerRawInputMonitorService.ShouldRaiseRawHidGuidePressEdge(
            hasPrevious: true,
            previousPressed: true,
            currentPressed: true));
    }

    [Fact]
    public void RawInputHidPath_DoesNotApplySteamFirstInputGuardInWakeOnlyMode()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var source = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));
        var suppressStart = source.IndexOf("private bool ShouldSuppressGuideInputReport", StringComparison.Ordinal);
        var suppressEnd = source.IndexOf("private bool TryRegisterRawHidGuidePressEdge", suppressStart, StringComparison.Ordinal);
        var suppressMethod = source[suppressStart..suppressEnd];

        Assert.Contains("if (IsWakeOnlyMode)", suppressMethod);
        Assert.Contains("return false;", suppressMethod);
        Assert.True(
            suppressMethod.IndexOf("if (IsWakeOnlyMode)", StringComparison.Ordinal) <
            suppressMethod.IndexOf("device.DeviceKind != GuideButtonDeviceKind.SteamController", StringComparison.Ordinal));
    }

    [Fact]
    public void GlobalMouseMove_IsWakeEligibleForDisplayRecovery()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var source = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));

        Assert.Contains("new GlobalHumanInputEventArgs(\"raw_mouse_move\", countsAsUserActivity: true, isWakeEligible: true)", source);
    }

    [Fact]
    public void WakeOnlyRegistration_ExcludesKeyboardAndMouse()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var source = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));
        var methodStart = source.IndexOf("private static RawInputDevice[] BuildWakeOnlyRawInputDevices", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private static IReadOnlyList<RawInputDeviceInfo> EnumerateRawInputDevices", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        Assert.Contains("UsageJoystick", method);
        Assert.Contains("UsageGamepad", method);
        Assert.Contains("UsagePageVendorSteam", method);
        Assert.DoesNotContain("UsageMouse", method);
        Assert.DoesNotContain("UsageKeyboard", method);
    }

    [Fact]
    public void NormalRegistration_UsesOnlySteamVendorPage()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var source = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));
        var methodStart = source.IndexOf("private static RawInputDevice[] BuildNormalRawInputDevices", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private static RawInputDevice[] BuildHumanInputOnlyRawInputDevices", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        Assert.Contains("UsagePageVendorSteam", method);
        Assert.DoesNotContain("UsageJoystick", method);
        Assert.DoesNotContain("UsageGamepad", method);
        Assert.DoesNotContain("UsageMouse", method);
        Assert.DoesNotContain("UsageKeyboard", method);
    }

    [Fact]
    public void HumanInputOnlyRegistration_UsesOnlyKeyboardAndMouse()
    {
        var appRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BluetoothBatteryWidget.App");
        var source = File.ReadAllText(Path.Combine(appRoot, "Services", "SteamControllerRawInputMonitorService.cs"));
        var methodStart = source.IndexOf("private static RawInputDevice[] BuildHumanInputOnlyRawInputDevices", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private static RawInputDevice[] BuildWakeOnlyRawInputDevices", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        Assert.Contains("UsageMouse", method);
        Assert.Contains("UsageKeyboard", method);
        Assert.DoesNotContain("UsageJoystick", method);
        Assert.DoesNotContain("UsageGamepad", method);
        Assert.DoesNotContain("UsagePageVendorSteam", method);
    }

    private static byte[] BuildSteamReport(
        byte reportId = 0x42,
        bool aPressed = false,
        short leftPadX = 0)
    {
        var report = new byte[64];
        report[0] = reportId;
        if (aPressed)
        {
            report[2] |= 0x01;
        }

        WriteInt16(report, 18, leftPadX);
        return report;
    }

    private static byte[] BuildDualSenseReport(bool psPressed = false)
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[8] = 0x08;
        if (psPressed)
        {
            report[10] = 0x01;
        }

        return report;
    }

    private static void WriteInt16(byte[] report, int offset, short value)
    {
        report[offset] = (byte)(value & 0xFF);
        report[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}
