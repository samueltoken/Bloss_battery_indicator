using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class GuideButtonReportParserTests
{
    [Fact]
    public void TryParseGuideButton_DualSenseUsbReport_DetectsPsButton()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[10] = 0x01;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.DualSense,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_DualSenseBluetoothExtendedReport_DetectsPsButton()
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[11] = 0x01;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.DualSense,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_DualSenseUsbReport_DoesNotUseBluetoothPsOffset()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[11] = 0x01;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.DualSense,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.False(pressed);
    }

    [Fact]
    public void TryParseGuideButton_DualSenseBluetoothExtendedReport_DoesNotUseUsbPsOffset()
    {
        var report = new byte[78];
        report[0] = 0x31;
        report[10] = 0x01;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.DualSense,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.False(pressed);
    }

    [Fact]
    public void TryParseGuideButton_DualSensePaddedShortBluetoothReport_DetectsPsButton()
    {
        var report = new byte[78];
        report[0] = 0x01;
        report[7] = 0x01;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.DualSense,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerReport_DetectsSteamLogoButton()
    {
        var report = new byte[64];
        report[9] = 0x20;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerEnvelopeReport_DetectsSteamLogoButton()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[1] = 0x00;
        report[2] = 0x01;
        report[3] = 60;
        report[9] = 0x20;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerTritonStateReport_DetectsSteamLogoButton()
    {
        var report = new byte[54];
        report[0] = 0x42;
        report[4] = 0x01;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerTritonStateReport_DetectsSteamLogoRelease()
    {
        var report = new byte[54];
        report[0] = 0x42;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.False(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerTritonExtendedReport_DetectsSteamLogoButton()
    {
        var report = new byte[54];
        report[0] = 0x45;
        report[4] = 0x01;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerTritonExtendedReport_KeepsPressedAcrossAuxiliaryFlagChanges()
    {
        var report = new byte[54];
        report[0] = 0x45;
        report[4] = 0x01;
        report[5] = 0x30;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerFeatureEnvelopeWithLeadingReportId_DetectsSteamLogoButton()
    {
        var report = new byte[65];
        report[0] = 0x00;
        report[1] = 0x01;
        report[2] = 0x00;
        report[3] = 0x01;
        report[4] = 60;
        report[10] = 0x20;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.True(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerWirelessEnvelope_DoesNotTreatPayloadAsButton()
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[1] = 0x00;
        report[2] = 0x03;
        report[3] = 1;
        report[9] = 0x20;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.True(parsed);
        Assert.False(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerKeyboardEscapeFallback_DoesNotTriggerGuideButton()
    {
        var report = new byte[8];
        report[2] = 0x29;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.False(parsed);
        Assert.False(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerKeyboardEnterFallback_DoesNotTriggerGuideButton()
    {
        var report = new byte[8];
        report[2] = 0x28;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.False(parsed);
        Assert.False(pressed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerKeyboardFallbackWithExtraKey_DoesNotParseAsGuideButton()
    {
        var report = new byte[8];
        report[2] = 0x29;
        report[3] = 0x04;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseGuideButton_SteamControllerBatteryReport_DoesNotTreatPayloadAsButton()
    {
        var report = new byte[17];
        report[0] = 0x43;
        report[9] = 0x20;

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData(0x43, 0x57)]
    [InlineData(0x43, 0x5A)]
    [InlineData(0x43, 0x5B)]
    [InlineData(0x44, 0x57)]
    [InlineData(0x44, 0x5A)]
    [InlineData(0x44, 0x5B)]
    public void IsSteamControllerStatusReport_RecognizesFullRawStatusReport(int reportId, int marker)
    {
        var report = new byte[54];
        report[0] = (byte)reportId;
        report[1] = 0x01;
        report[2] = (byte)marker;
        report[3] = 0xD8;
        report[4] = 0x0F;
        report[5] = 0xF0;
        report[6] = 0x0F;

        Assert.True(GuideButtonReportParser.IsSteamControllerStatusReport(report));

        var parsed = GuideButtonReportParser.TryParseGuideButton(
            GuideButtonDeviceKind.SteamController,
            report,
            out var pressed);

        Assert.False(parsed);
        Assert.False(pressed);
    }

    [Fact]
    public void IsSteamControllerStatusReport_DoesNotTreatShortBatteryReportAsReleaseHint()
    {
        var report = new byte[17];
        report[0] = 0x43;
        report[1] = 0x01;
        report[2] = 0x57;
        report[9] = 0x20;

        Assert.False(GuideButtonReportParser.IsSteamControllerStatusReport(report));
    }

    [Fact]
    public void BatteryGuideMessageBuilder_DualSenseKoreanMessage_IncludesEstimatedTime()
    {
        var snapshot = new DeviceBatterySnapshot(
            DeviceId: "dualsense:AABBCCDDE020",
            Address: "AABBCCDDE020",
            DisplayName: "DualSense Wireless Controller (USB/Pico2W)",
            BatteryPercent: 75,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            SourceKind: BatterySourceKind.SonyHid,
            ModelKey: "VID_054C|PID_0CE6");

        var message = BatteryGuideMessageBuilder.Build(snapshot, "ko");

        Assert.Contains("75%", message);
        Assert.Contains("예상 6시간", message);
    }

    [Fact]
    public void BatteryGuideMessageBuilder_SteamChargingMessage_DoesNotShowRemainingRuntime()
    {
        var snapshot = new DeviceBatterySnapshot(
            DeviceId: "steam-triton:AABBCCDDE010",
            Address: "AABBCCDDE010",
            DisplayName: "Steam Controller",
            BatteryPercent: 96,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now,
            SourceKind: BatterySourceKind.SteamHid,
            ModelKey: "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK",
            IsCharging: true);

        var message = BatteryGuideMessageBuilder.Build(snapshot, "ko");

        Assert.Contains("충전 중", message);
        Assert.DoesNotContain("남음", message);
    }
}
