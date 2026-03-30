using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class HidProbeTextParserTests
{
    [Fact]
    public void ExtractAddress_LowerCaseDevToken_ParsesSuccessfully()
    {
        var text = @"BTHENUM\DEV_90b685c680d8\8&2A5F5B95&0&BLUETOOTHDEVICE_90B685C680D8";

        var address = HidProbeTextParser.ExtractAddress(text);

        Assert.Equal("90B685C680D8", address);
    }

    [Fact]
    public void TryParseVidPid_InstanceFormat_ParsesSuccessfully()
    {
        var instanceId = @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\8&1234567&0&001122334455_C00000000";

        var parsed = HidProbeTextParser.TryParseVidPid(instanceId, out var vid, out var pid);

        Assert.True(parsed);
        Assert.Equal("054C", vid);
        Assert.Equal("0CE6", pid);
    }

    [Fact]
    public void TryParseVidPid_DevicePathFormat_ParsesSuccessfully()
    {
        var path = @"\\?\\hid#vid_045e&pid_0b13&mi_03#8&1a2b3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

        var parsed = HidProbeTextParser.TryParseVidPid(path, out var vid, out var pid);

        Assert.True(parsed);
        Assert.Equal("045E", vid);
        Assert.Equal("0B13", pid);
    }
}
