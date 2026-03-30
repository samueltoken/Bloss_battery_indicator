using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class XboxBatteryMatchingTests
{
    [Theory]
    [InlineData(0x00, 5)]
    [InlineData(0x01, 25)]
    [InlineData(0x02, 55)]
    [InlineData(0x03, 85)]
    public void XInputBatteryLevelMapper_MapsExpectedPercent(byte level, int expectedPercent)
    {
        var mapped = XInputBatteryLevelMapper.ToPercent(level);
        Assert.Equal(expectedPercent, mapped);
    }

    [Fact]
    public void MatchStrict_SingleXboxCandidateAndSingleReading_ReturnsMappedReading()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "A1B2C3D4E5F6", "Xbox Wireless Controller", true, "Input.Gaming")
        };

        var readings = new List<XInputBatteryReading>
        {
            new(0, 55)
        };

        var matched = XboxBatteryMatcher.MatchStrict(connected, readings);

        Assert.Single(matched);
        Assert.Equal("A1B2C3D4E5F6", matched[0].Address);
        Assert.Equal(55, matched[0].BatteryPercent);
    }

    [Fact]
    public void MatchStrict_MultipleCandidates_DoesNotMatch()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "A1B2C3D4E5F6", "Xbox Wireless Controller", true, "Input.Gaming"),
            new("dev2", "112233445566", "Xbox Elite Controller", true, "Input.Gaming")
        };

        var readings = new List<XInputBatteryReading>
        {
            new(0, 85)
        };

        var matched = XboxBatteryMatcher.MatchStrict(connected, readings);

        Assert.Empty(matched);
    }

    [Fact]
    public void MatchStrict_NonXboxDevice_DoesNotMatch()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "90B685C680D8", "DualSense Wireless Controller", true, "Input.Gaming")
        };
        var readings = new List<XInputBatteryReading>
        {
            new(0, 85)
        };

        var matched = XboxBatteryMatcher.MatchStrict(connected, readings);

        Assert.Empty(matched);
    }

    [Fact]
    public void MatchBestEffort_WithEndpointSignal_SelectsUniqueWinner()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "A1B2C3D4E5F6", "Wireless Controller", true, "Input.Gaming"),
            new("dev2", "112233445566", "Controller", true, "Input.Gaming")
        };
        var readings = new List<XInputBatteryReading>
        {
            new(0, 85)
        };
        var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A1B2C3D4E5F6"] = "VID_045E PID_0B22 XINPUT XUSB",
            ["112233445566"] = "VID_1234 PID_5678"
        };

        var matched = XboxBatteryMatcher.MatchBestEffort(connected, readings, signals);

        Assert.Single(matched);
        Assert.Equal("A1B2C3D4E5F6", matched[0].Address);
        Assert.Equal(BatteryConfidence.Confirmed, matched[0].BatteryConfidence);
    }

    [Fact]
    public void MatchGameInputBestEffort_SingleReading_MapsWinner()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "A1B2C3D4E5F6", "Xbox Wireless Controller", true, "Input.Gaming")
        };
        var readings = new List<GameInputBatteryReading>
        {
            new(0, 63)
        };
        var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A1B2C3D4E5F6"] = "VID_045E PID_0B22"
        };

        var matched = XboxBatteryMatcher.MatchGameInputBestEffort(connected, readings, signals);

        Assert.Single(matched);
        Assert.Equal(63, matched[0].BatteryPercent);
        Assert.Equal("GAMEINPUT_SLOT_0", matched[0].InstanceId);
    }

    [Fact]
    public void MatchGameInputBestEffort_AmbiguousCandidates_UsesPreferredAddress()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "A1B2C3D4E5F6", "Wireless Controller", true, "Input.Gaming"),
            new("dev2", "112233445566", "Wireless Controller", true, "Input.Gaming")
        };
        var readings = new List<GameInputBatteryReading>
        {
            new(0, 71)
        };
        var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A1B2C3D4E5F6"] = "VID_045E PID_0B22",
            ["112233445566"] = "VID_045E PID_0B22"
        };

        var matched = XboxBatteryMatcher.MatchGameInputBestEffort(
            connected,
            readings,
            signals,
            preferredAddress: "112233445566");

        Assert.Single(matched);
        Assert.Equal("112233445566", matched[0].Address);
    }

    [Fact]
    public void MatchGameInputBestEffort_AmbiguousCandidatesWithoutPreferred_DoesNotMatch()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "A1B2C3D4E5F6", "Wireless Controller", true, "Input.Gaming"),
            new("dev2", "112233445566", "Wireless Controller", true, "Input.Gaming")
        };
        var readings = new List<GameInputBatteryReading>
        {
            new(0, 71)
        };
        var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A1B2C3D4E5F6"] = "VID_045E PID_0B22",
            ["112233445566"] = "VID_045E PID_0B22"
        };

        var matched = XboxBatteryMatcher.MatchGameInputBestEffort(
            connected,
            readings,
            signals);

        Assert.Empty(matched);
    }

    [Fact]
    public void MatchBestEffort_MultipleCandidatesWithoutSignalEvidence_DoesNotMatch()
    {
        var connected = new List<ConnectedBluetoothDevice>
        {
            new("dev1", "A1B2C3D4E5F6", "Xbox Wireless Controller", true, "Input.Gaming"),
            new("dev2", "112233445566", "Controller", true, "Input.Gaming")
        };
        var readings = new List<XInputBatteryReading>
        {
            new(0, 55)
        };
        var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A1B2C3D4E5F6"] = "BTENUM DEVICE",
            ["112233445566"] = "BTENUM DEVICE"
        };

        var matched = XboxBatteryMatcher.MatchBestEffort(connected, readings, signals);

        Assert.Empty(matched);
    }
}
