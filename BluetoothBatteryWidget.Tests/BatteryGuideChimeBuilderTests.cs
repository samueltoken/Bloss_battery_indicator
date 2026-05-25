using BluetoothBatteryWidget.App.Services;
using System.Text;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryGuideChimeBuilderTests
{
    [Fact]
    public void CreateDreamChimeWave_BuildsPlayablePcmWave()
    {
        var data = BatteryGuideChimeBuilder.CreateDreamChimeWave();

        Assert.True(data.Length > 44);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(data, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(data, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(data, 12, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(data, 36, 4));
        Assert.Equal(44100, BitConverter.ToInt32(data, 24));
        Assert.Equal(2, BitConverter.ToInt16(data, 22));
        Assert.Equal(16, BitConverter.ToInt16(data, 34));
        Assert.Equal(44100 * 2 * 2, BitConverter.ToInt32(data, 28));
        Assert.Equal(44100 * 2 * 2 * 2, BitConverter.ToInt32(data, 40));
    }
}
