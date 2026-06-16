using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryGuideTriggerParserTests
{
    [Fact]
    public void TryCapture_DualSenseReport_CapturesNewButtonBit()
    {
        var previous = new byte[64];
        previous[0] = 0x01;
        previous[8] = 0x08;
        var current = new byte[64];
        current[0] = 0x01;
        current[8] = 0x08;
        current[10] = 0x02;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal(GuideButtonDeviceKind.DualSense, trigger.DeviceKind);
        Assert.Equal(0x01, trigger.ReportId);
        var bit = Assert.Single(trigger.Bits);
        Assert.Equal(10, bit.Offset);
        Assert.Equal(0x02, bit.Mask);
    }

    [Fact]
    public void TryCapture_DualSensePaddedShortBluetoothReport_CapturesOnlyPsButton()
    {
        var previous = new byte[64];
        previous[0] = 0x01;
        previous[8] = 0x08;
        var current = new byte[64];
        current[0] = 0x01;
        current[7] = 0x01;
        current[8] = 0x08;
        current[9] = 0x03;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("PS", trigger.DisplayName);
        var bit = Assert.Single(trigger.Bits);
        Assert.Equal(7, bit.Offset);
        Assert.Equal(0x01, bit.Mask);
        Assert.Equal(new[] { "Guide" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, current));
        current[7] = 0x00;
        Assert.False(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, current));
    }

    [Fact]
    public void TryCapture_DualSenseBluetoothFullReport_CapturesPsR1Combo()
    {
        var previous = new byte[78];
        previous[0] = 0x31;
        previous[9] = 0x08;
        var current = new byte[78];
        current[0] = 0x31;
        current[9] = 0x08;
        current[10] = 0x02;
        current[11] = 0x01;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("PS + R1", trigger.DisplayName);
        Assert.DoesNotContain("Button ", trigger.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(new BatteryGuideTriggerBit(10, 0x02), trigger.Bits);
        Assert.Contains(new BatteryGuideTriggerBit(11, 0x01), trigger.Bits);
        Assert.Contains("Guide", BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.Contains("RB", BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, current));
    }

    [Fact]
    public void TryCapture_DualSenseBluetoothFullReport_CapturesPsR2Combo()
    {
        var previous = new byte[78];
        previous[0] = 0x31;
        previous[9] = 0x08;
        var current = new byte[78];
        current[0] = 0x31;
        current[9] = 0x08;
        current[10] = 0x08;
        current[11] = 0x01;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("PS + R2", trigger.DisplayName);
        Assert.DoesNotContain("Button ", trigger.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(new BatteryGuideTriggerBit(10, 0x08), trigger.Bits);
        Assert.Contains(new BatteryGuideTriggerBit(11, 0x01), trigger.Bits);
        Assert.Contains("Guide", BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.Contains("RT", BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, current));
    }

    [Fact]
    public void TryCapture_DualSenseUsbReport_CapturesDpadDirection()
    {
        var previous = new byte[64];
        previous[0] = 0x01;
        previous[8] = 0x08;
        var current = new byte[64];
        current[0] = 0x01;
        current[8] = 0x00;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("D-Pad Up", trigger.DisplayName);
        Assert.Equal(new[] { "Up" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, current));

        var released = current.ToArray();
        released[8] = 0x08;
        Assert.False(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, released));
    }

    [Fact]
    public void CreateNeutralReportForCapture_DualSenseSeedsDpadNeutral()
    {
        var current = new byte[64];
        current[0] = 0x01;
        current[8] = 0x00;

        var neutral = BatteryGuideTriggerParser.CreateNeutralReportForCapture(GuideButtonDeviceKind.DualSense, current);

        Assert.Equal(0x01, neutral[0]);
        Assert.Equal(0x08, neutral[8]);
        Assert.True(BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            neutral,
            current,
            out var trigger));
        Assert.Equal("D-Pad Up", trigger.DisplayName);
    }

    [Fact]
    public void CreateNeutralReportForCapture_SteamUsesCurrentReportAsBaseline()
    {
        var current = new byte[54];
        current[0] = 0x45;
        current[4] = 0x01;

        var neutral = BatteryGuideTriggerParser.CreateNeutralReportForCapture(GuideButtonDeviceKind.SteamController, current);

        Assert.Equal(current, neutral);
        Assert.False(BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            neutral,
            current,
            out _));
    }

    [Fact]
    public void TryCapture_DualSenseUsbReport_CapturesMicVisualKey()
    {
        var previous = new byte[64];
        previous[0] = 0x01;
        previous[8] = 0x08;
        var current = previous.ToArray();
        current[10] = 0x04;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Mic", trigger.DisplayName);
        Assert.Equal(new[] { "Mic" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, current));
    }

    [Fact]
    public void TryCapture_DualSenseBluetoothFullReport_CapturesDpadAndPsCombo()
    {
        var previous = new byte[78];
        previous[0] = 0x31;
        previous[9] = 0x08;
        var current = new byte[78];
        current[0] = 0x31;
        current[9] = 0x06;
        current[11] = 0x01;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("PS + D-Pad Left", trigger.DisplayName);
        Assert.Equal(new[] { "Guide", "Left" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, current));
    }

    [Fact]
    public void TryCapture_DualSenseBluetoothFullReport_CapturesDiagonalDpadAsTwoVisualKeys()
    {
        var previous = new byte[78];
        previous[0] = 0x31;
        previous[9] = 0x08;
        var current = new byte[78];
        current[0] = 0x31;
        current[9] = 0x01;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.DualSense,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("D-Pad Up Right", trigger.DisplayName);
        Assert.Equal(new[] { "Up", "Right" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesFaceButtonCombo()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[2] = 0x03;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal(GuideButtonDeviceKind.SteamController, trigger.DeviceKind);
        Assert.Equal(0x45, trigger.ReportId);
        Assert.Equal("A + B", trigger.DisplayName);
        Assert.Contains(new BatteryGuideTriggerBit(2, 0x01), trigger.Bits);
        Assert.Contains(new BatteryGuideTriggerBit(2, 0x02), trigger.Bits);
        Assert.Equal(new[] { "A", "B" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesBumperGuideCombo()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[3] = 0x02;
        current[4] = 0x01;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("RB + Guide", trigger.DisplayName);
        Assert.Contains(new BatteryGuideTriggerBit(3, 0x02), trigger.Bits);
        Assert.Contains(new BatteryGuideTriggerBit(4, 0x01), trigger.Bits);
        Assert.Equal(new[] { "RB", "Guide" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, current));

        current[4] = 0x00;
        Assert.False(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, current));
        Assert.True(BatteryGuideTriggerParser.HasAnyTriggerBitPressed(
            trigger,
            GuideButtonDeviceKind.SteamController,
            current));

        current[3] = 0x00;
        Assert.False(BatteryGuideTriggerParser.HasAnyTriggerBitPressed(
            trigger,
            GuideButtonDeviceKind.SteamController,
            current));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesQuickAccessButton()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[2] = 0x10;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Quick Access", trigger.DisplayName);
        Assert.Contains(new BatteryGuideTriggerBit(2, 0x10), trigger.Bits);
        Assert.Equal(new[] { "QuickAccess" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, current));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesRightTriggerAsRt()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[4] = 0x80;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("RT", trigger.DisplayName);
        Assert.Contains(new BatteryGuideTriggerBit(4, 0x80), trigger.Bits);
        Assert.DoesNotContain(new BatteryGuideTriggerBit(2, 0x20), trigger.Bits);
        Assert.Equal(new[] { "RT" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_MapsViewAndMenuToCorrectVisualKeys()
    {
        var previous = new byte[54];
        previous[0] = 0x45;

        var viewReport = previous.ToArray();
        viewReport[3] = 0x40;
        Assert.True(BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            viewReport,
            out var viewTrigger));
        Assert.Equal("View", viewTrigger.DisplayName);
        Assert.Equal(new[] { "View" }, BatteryGuideTriggerParser.GetVisualButtonKeys(viewTrigger));

        var menuReport = previous.ToArray();
        menuReport[2] = 0x40;
        Assert.True(BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            menuReport,
            out var menuTrigger));
        Assert.Equal("Menu", menuTrigger.DisplayName);
        Assert.Equal(new[] { "Menu" }, BatteryGuideTriggerParser.GetVisualButtonKeys(menuTrigger));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesLeftPadDirectionFromAxis()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        WriteInt16LittleEndian(current, 20, -12000);

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Left Pad Up", trigger.DisplayName);
        Assert.Equal(new[] { "Up" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, current));

        var released = current.ToArray();
        WriteInt16LittleEndian(released, 20, -3000);
        Assert.False(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, released));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_UsesDominantLeftPadAxisWhenDigitalDpadIsDiagonal()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[3] = 0x30; // Up + Left can happen on the Steam Controller touch pad.
        WriteInt16LittleEndian(current, 18, -15000);
        WriteInt16LittleEndian(current, 20, -7000);

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Left Pad Left", trigger.DisplayName);
        Assert.Equal(new[] { "Left" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.Single(trigger.Bits);
    }

    [Fact]
    public void TryCapture_SteamTritonReport_DownLeftDiagonalDpadChoosesDownWhenYAxisDominates()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[3] = 0x14; // Down + Left can happen on the Steam Controller touch pad.
        WriteInt16LittleEndian(current, 18, -5000);
        WriteInt16LittleEndian(current, 20, 16000);

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Left Pad Down", trigger.DisplayName);
        Assert.Equal(new[] { "Down" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.Single(trigger.Bits);
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesCleanDigitalDpadWhenAxisIsUnavailable()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[3] = 0x20;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Up", trigger.DisplayName);
        Assert.Equal(new[] { "Up" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.Single(trigger.Bits);
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesDominantStickAxisOnly()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        WriteInt16LittleEndian(current, 10, 14000);
        WriteInt16LittleEndian(current, 12, 7600);

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Left Stick Right", trigger.DisplayName);
        Assert.Equal(new[] { "LeftPad" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.Single(trigger.Bits);
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, current));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesRightStickDirectionFromAxis()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        WriteInt16LittleEndian(current, 16, 12500);

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Right Stick Down", trigger.DisplayName);
        Assert.Equal(new[] { "RightPad" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.SteamController, current));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesPadClicksAndTriggersOnSeparateBits()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        current[4] = 0x40;
        current[5] = 0x04;
        current[5] |= 0x08;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("Right Pad + Left Pad + LT", trigger.DisplayName);
        Assert.Contains(new BatteryGuideTriggerBit(4, 0x40), trigger.Bits);
        Assert.Contains(new BatteryGuideTriggerBit(5, 0x04), trigger.Bits);
        Assert.Contains(new BatteryGuideTriggerBit(5, 0x08), trigger.Bits);
        Assert.Equal(new[] { "RightPad", "LeftPad", "LT" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_IgnoresUnmappedTouchNoise()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        previous[5] = 0x31;
        var current = new byte[54];
        current[0] = 0x45;
        current[5] = 0x31;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out _);

        Assert.False(captured);
    }

    [Fact]
    public void TryCapture_SteamTritonReport_DoesNotPromoteHeldButtonWhenOnlyUnmappedNoiseChanges()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        previous[3] = 0x02;
        var current = new byte[54];
        current[0] = 0x45;
        current[3] = 0x02;
        current[5] = 0x31;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out _);

        Assert.False(captured);
    }

    [Fact]
    public void TryCapture_SteamTritonReport_CapturesWholeComboWhenSecondButtonArrives()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        previous[3] = 0x02;
        var current = new byte[54];
        current[0] = 0x45;
        current[3] = 0x02;
        current[4] = 0x01;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("RB + Guide", trigger.DisplayName);
        Assert.Contains(new BatteryGuideTriggerBit(3, 0x02), trigger.Bits);
        Assert.Contains(new BatteryGuideTriggerBit(4, 0x01), trigger.Bits);
        Assert.Equal(new[] { "RB", "Guide" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
    }

    [Fact]
    public void TryCapture_SteamTritonReport_KeepsKnownComboWhenTouchNoiseIsPresent()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        previous[3] = 0x02;
        previous[5] = 0x31;
        var current = new byte[54];
        current[0] = 0x45;
        current[3] = 0x02;
        current[4] = 0x01;
        current[5] = 0x31;

        var captured = BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger);

        Assert.True(captured);
        Assert.Equal("RB + Guide", trigger.DisplayName);
        Assert.DoesNotContain(trigger.Bits, bit => bit.Offset == 5 && bit.Mask == 0x01);
        Assert.DoesNotContain(trigger.Bits, bit => bit.Offset == 5 && bit.Mask == 0x10);
        Assert.DoesNotContain(trigger.Bits, bit => bit.Offset == 5 && bit.Mask == 0x20);
        Assert.Equal(new[] { "RB", "Guide" }, BatteryGuideTriggerParser.GetVisualButtonKeys(trigger));
    }

    [Fact]
    public void IsMatch_RequiresCapturedBitToBePressed()
    {
        var trigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.DualSense,
            0x01,
            [new BatteryGuideTriggerBit(10, 0x02)],
            "Custom button");
        var matching = new byte[64];
        matching[0] = 0x01;
        matching[10] = 0x02;
        var released = new byte[64];
        released[0] = 0x01;

        Assert.True(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, matching));
        Assert.False(BatteryGuideTriggerParser.IsMatch(trigger, GuideButtonDeviceKind.DualSense, released));
    }

    [Fact]
    public void PersistedString_RoundTrips()
    {
        var trigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.SteamController,
            0x42,
            [new BatteryGuideTriggerBit(4, 0x01), new BatteryGuideTriggerBit(5, 0x04)],
            "Custom combo");

        var parsed = BatteryGuideTriggerParser.TryParse(trigger.ToPersistedString(), out var roundTrip);

        Assert.True(parsed);
        Assert.Equal(trigger.DeviceKind, roundTrip.DeviceKind);
        Assert.Equal(trigger.ReportId, roundTrip.ReportId);
        Assert.Equal(trigger.Bits, roundTrip.Bits);
        Assert.Equal(trigger.DisplayName, roundTrip.DisplayName);
    }

    [Fact]
    public void PersistedString_RoundTripsSteamAxisTrigger()
    {
        var previous = new byte[54];
        previous[0] = 0x45;
        var current = new byte[54];
        current[0] = 0x45;
        WriteInt16LittleEndian(current, 18, -15000);

        Assert.True(BatteryGuideTriggerParser.TryCapture(
            GuideButtonDeviceKind.SteamController,
            previous,
            current,
            out var trigger));
        Assert.True(BatteryGuideTriggerParser.TryParse(trigger.ToPersistedString(), out var roundTrip));

        Assert.Equal(trigger.Bits, roundTrip.Bits);
        Assert.Equal("Left Pad Left", roundTrip.DisplayName);
        Assert.Equal(new[] { "Left" }, BatteryGuideTriggerParser.GetVisualButtonKeys(roundTrip));
        Assert.True(BatteryGuideTriggerParser.IsMatch(roundTrip, GuideButtonDeviceKind.SteamController, current));
    }

    private static void WriteInt16LittleEndian(byte[] report, int offset, short value)
    {
        report[offset] = (byte)(value & 0xFF);
        report[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}
