using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class XInputActivityMonitorServiceTests
{
    [Fact]
    public void IsMeaningfulActivity_IgnoresNeutralState()
    {
        Assert.False(XInputActivityMonitorService.IsMeaningfulActivity(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbX: 0,
            leftThumbY: 0,
            rightThumbX: 0,
            rightThumbY: 0));
    }

    [Fact]
    public void IsMeaningfulActivity_UsesButtonsAndTriggers()
    {
        Assert.True(XInputActivityMonitorService.IsMeaningfulActivity(
            buttons: 0x1000,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbX: 0,
            leftThumbY: 0,
            rightThumbX: 0,
            rightThumbY: 0));

        Assert.True(XInputActivityMonitorService.IsMeaningfulActivity(
            buttons: 0,
            leftTrigger: 31,
            rightTrigger: 0,
            leftThumbX: 0,
            leftThumbY: 0,
            rightThumbX: 0,
            rightThumbY: 0));
    }

    [Fact]
    public void IsMeaningfulActivity_UsesStickDeadZones()
    {
        Assert.False(XInputActivityMonitorService.IsMeaningfulActivity(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbX: 500,
            leftThumbY: 500,
            rightThumbX: 500,
            rightThumbY: 500));

        Assert.True(XInputActivityMonitorService.IsMeaningfulActivity(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbX: 9000,
            leftThumbY: 0,
            rightThumbX: 0,
            rightThumbY: 0));

        Assert.True(XInputActivityMonitorService.IsMeaningfulActivity(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbX: 0,
            leftThumbY: 0,
            rightThumbX: 9000,
            rightThumbY: 0));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void ShouldRaiseActivity_RequiresMeaningfulInputAndStateChange(bool activeInput, bool expected)
    {
        Assert.Equal(expected, XInputActivityMonitorService.ShouldRaiseActivity(activeInput));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldRaiseActivity_UsesMeaningfulChangedInput(
        bool activeInput,
        bool packetChanged,
        bool expected)
    {
        Assert.Equal(expected, XInputActivityMonitorService.ShouldRaiseActivity(activeInput, packetChanged));
    }

    [Fact]
    public void ShouldTreatPacketChangeAsActivity_IgnoresFirstControllerSnapshot()
    {
        Assert.False(XInputActivityMonitorService.ShouldTreatPacketChangeAsActivity(
            previousPacketNumber: null,
            currentPacketNumber: 10));
    }

    [Fact]
    public void ShouldTreatPacketChangeAsActivity_UsesPacketChangeAfterBaseline()
    {
        Assert.False(XInputActivityMonitorService.ShouldTreatPacketChangeAsActivity(
            previousPacketNumber: 10,
            currentPacketNumber: 10));
        Assert.True(XInputActivityMonitorService.ShouldTreatPacketChangeAsActivity(
            previousPacketNumber: 10,
            currentPacketNumber: 11));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, true)]
    public void ShouldRaiseWakeOnlyActivity_UsesButtonOrTriggerOnly(
        bool hasButton,
        bool hasTrigger,
        bool packetChanged,
        bool expected)
    {
        Assert.Equal(expected, XInputActivityMonitorService.ShouldRaiseWakeOnlyActivity(
            hasButton,
            hasTrigger,
            packetChanged));
    }

    [Theory]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, true, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(false, false, true, false, false)]
    public void ShouldTreatInitialWakeOnlyStateAsActivity_AllowsFirstPressedWakeOnlySnapshot(
        bool wakeOnly,
        bool hasPreviousGamepad,
        bool hasButtonHeld,
        bool hasTriggerHeld,
        bool expected)
    {
        Assert.Equal(expected, XInputActivityMonitorService.ShouldTreatInitialWakeOnlyStateAsActivity(
            wakeOnly,
            hasPreviousGamepad,
            hasButtonHeld,
            hasTriggerHeld));
    }

    [Fact]
    public void ShouldRaiseHeldActivity_OnlyRepeatsForActiveHeldInput()
    {
        var now = DateTimeOffset.Parse("2026-06-21T00:00:20Z");
        var repeat = TimeSpan.FromSeconds(10);

        Assert.False(XInputActivityMonitorService.ShouldRaiseHeldActivity(
            activeInput: false,
            lastRaisedAtUtc: null,
            now,
            repeat));
        Assert.True(XInputActivityMonitorService.ShouldRaiseHeldActivity(
            activeInput: true,
            lastRaisedAtUtc: null,
            now,
            repeat));
        Assert.False(XInputActivityMonitorService.ShouldRaiseHeldActivity(
            activeInput: true,
            lastRaisedAtUtc: now - TimeSpan.FromSeconds(9),
            now,
            repeat));
        Assert.True(XInputActivityMonitorService.ShouldRaiseHeldActivity(
            activeInput: true,
            lastRaisedAtUtc: now - TimeSpan.FromSeconds(10),
            now,
            repeat));
    }

    [Fact]
    public void GamepadWakeInputEventArgs_TreatsDefaultStickAsTelemetryOnly()
    {
        var eventArgs = new GamepadWakeInputEventArgs(
            "xinput",
            hasButton: false,
            hasTrigger: false,
            hasStick: true,
            isGuideButton: false);

        Assert.False(eventArgs.CountsAsUserActivity);
        Assert.False(eventArgs.IsWakeEligible);
    }

    [Fact]
    public void GamepadWakeInputEventArgs_TreatsExplicitStickMovementAsUserActivityOnly()
    {
        var eventArgs = new GamepadWakeInputEventArgs(
            "xinput",
            hasButton: false,
            hasTrigger: false,
            hasStick: true,
            isGuideButton: false,
            countsAsUserActivity: true);

        Assert.True(eventArgs.CountsAsUserActivity);
        Assert.False(eventArgs.IsWakeEligible);
    }

    [Fact]
    public void GamepadWakeInputEventArgs_TreatsButtonAsWakeEligible()
    {
        var eventArgs = new GamepadWakeInputEventArgs(
            "xinput",
            hasButton: true,
            hasTrigger: false,
            hasStick: false,
            isGuideButton: false);

        Assert.True(eventArgs.CountsAsUserActivity);
        Assert.True(eventArgs.IsWakeEligible);
    }

    [Theory]
    [InlineData(0x0000, 0x1000, true)]
    [InlineData(0x1000, 0x1000, false)]
    [InlineData(0x1000, 0x0000, false)]
    public void HasButtonDownEdge_OnlyUsesNewButtonPresses(
        ushort previousButtons,
        ushort currentButtons,
        bool expected)
    {
        Assert.Equal(expected, XInputActivityMonitorService.HasButtonDownEdge(previousButtons, currentButtons));
    }

    [Theory]
    [InlineData(0, 31, true)]
    [InlineData(31, 31, false)]
    [InlineData(31, 0, false)]
    [InlineData(29, 30, false)]
    public void HasTriggerPressEdge_OnlyUsesThresholdCrossing(
        byte previousTrigger,
        byte currentTrigger,
        bool expected)
    {
        Assert.Equal(expected, XInputActivityMonitorService.HasTriggerPressEdge(previousTrigger, currentTrigger));
    }

    [Theory]
    [InlineData(0, 0, 500, 500, 7849, false)]
    [InlineData(0, 0, 9000, 0, 7849, true)]
    [InlineData(9000, 0, 9300, 0, 7849, false)]
    [InlineData(9000, 0, 15000, 0, 7849, true)]
    [InlineData(12000, 0, 0, 12000, 7849, true)]
    [InlineData(12000, 0, 0, 0, 7849, false)]
    public void HasThumbActivity_UsesDeadZoneAndLargeStickMovement(
        short previousX,
        short previousY,
        short currentX,
        short currentY,
        short deadZone,
        bool expected)
    {
        Assert.Equal(expected, XInputActivityMonitorService.HasThumbActivity(
            previousX,
            previousY,
            currentX,
            currentY,
            deadZone));
    }
}
