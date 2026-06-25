using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GuideButtonMonitorServiceTests
{
    [Fact]
    public void BuildInputReportActionSignature_DualSenseIgnoresStickNoise()
    {
        var neutral = BuildDualSenseReport();
        var stickNoise = BuildDualSenseReport();
        stickNoise[1] = 0x80;

        Assert.Equal(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, neutral),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, stickNoise));
    }

    [Fact]
    public void BuildInputReportActionSignature_DualSenseKeepsGuideButtonChange()
    {
        var neutral = BuildDualSenseReport();
        var guidePressed = BuildDualSenseReport();
        guidePressed[10] = 0x01;

        Assert.NotEqual(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, neutral),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, guidePressed));
    }

    [Fact]
    public void BuildInputReportActionSignature_SteamControllerQuantizesAxisNoise()
    {
        var trigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.SteamController,
            0x42,
            [new BatteryGuideTriggerBit(0x80 + 4, 0x02)],
            "LeftPad");
        var neutral = BuildSteamReport();
        var axisNoise = BuildSteamReport();
        WriteInt16(axisNoise, 18, 1200);
        var activeAxis = BuildSteamReport();
        WriteInt16(activeAxis, 18, 7000);
        var strongerActiveAxis = BuildSteamReport();
        WriteInt16(strongerActiveAxis, 18, 11000);

        Assert.Equal(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, neutral),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, axisNoise));
        Assert.Equal(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, neutral),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, activeAxis));
        Assert.NotEqual(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, neutral, trigger),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, activeAxis, trigger));
        Assert.Equal(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, activeAxis, trigger),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.SteamController, strongerActiveAxis, trigger));
    }

    [Fact]
    public void BuildInputReportActionSignature_DualSenseTracksOnlyConfiguredCustomTrigger()
    {
        var trigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.DualSense,
            0x01,
            [new BatteryGuideTriggerBit(10, 0x04)],
            "Mic");
        var neutral = BuildDualSenseReport();
        var squarePressed = BuildDualSenseReport();
        squarePressed[8] = 0x18;
        var micPressed = BuildDualSenseReport();
        micPressed[10] = 0x04;

        Assert.Equal(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, neutral),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, squarePressed));
        Assert.NotEqual(
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, neutral, trigger),
            GuideButtonMonitorService.BuildInputReportActionSignature(GuideButtonDeviceKind.DualSense, micPressed, trigger));
    }

    [Fact]
    public void ResolveRepeatedInputReportThrottle_UsesSettlingWindowUnlessDetailed()
    {
        Assert.Equal(TimeSpan.Zero, GuideButtonMonitorService.ResolveRepeatedInputReportThrottle(
            detailedInputMode: true,
            TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromMilliseconds(24), GuideButtonMonitorService.ResolveRepeatedInputReportThrottle(
            detailedInputMode: false,
            TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromMilliseconds(16), GuideButtonMonitorService.ResolveRepeatedInputReportThrottle(
            detailedInputMode: false,
            TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void RuntimeDiagnostics_DisablesFileLoggingForPortableTestExe()
    {
        Assert.True(RuntimeDiagnostics.IsPortableTestExecutablePath(@"C:\temp\test.exe"));
        Assert.True(RuntimeDiagnostics.IsPortableTestExecutablePath(@"C:\temp\TEST.EXE"));
        Assert.False(RuntimeDiagnostics.IsPortableTestExecutablePath(@"C:\temp\Bloss.exe"));
    }

    [Fact]
    public void ShouldSuppressNoisySteamStatusInputReport_SuppressesStatusTelemetry()
    {
        var statusReport = new byte[64];
        statusReport[0] = 0x43;
        statusReport[1] = 0x01;
        statusReport[2] = 75;

        Assert.True(GuideButtonMonitorService.ShouldSuppressNoisySteamStatusInputReport(
            GuideButtonDeviceKind.SteamController,
            statusReport));
        Assert.False(GuideButtonMonitorService.ShouldSuppressNoisySteamStatusInputReport(
            GuideButtonDeviceKind.DualSense,
            statusReport));
    }

    [Fact]
    public void GetEndpointOpenFailureRetryDelay_BacksOffAndClamps()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), GuideButtonMonitorService.GetEndpointOpenFailureRetryDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(30), GuideButtonMonitorService.GetEndpointOpenFailureRetryDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(2), GuideButtonMonitorService.GetEndpointOpenFailureRetryDelay(99));
    }

    private static byte[] BuildDualSenseReport()
    {
        var report = new byte[78];
        report[0] = 0x01;
        report[8] = 0x08;
        return report;
    }

    private static byte[] BuildSteamReport()
    {
        var report = new byte[64];
        report[0] = 0x42;
        return report;
    }

    private static void WriteInt16(byte[] report, int offset, short value)
    {
        report[offset] = (byte)(value & 0xFF);
        report[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}
