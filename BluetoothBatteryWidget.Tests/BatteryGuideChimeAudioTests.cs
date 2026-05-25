using BluetoothBatteryWidget.App.Services;
using System.Text;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryGuideChimeAudioTests
{
    [Fact]
    public void LoadWave_LoadsEmbeddedTwoSecondLowSizeAudio()
    {
        var data = BatteryGuideChimeAudio.LoadWave();
        var info = ReadWaveInfo(data);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(data, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(data, 8, 4));
        Assert.Equal(1, info.Channels);
        Assert.Equal(22050, info.SampleRate);
        Assert.Equal(16, info.BitsPerSample);
        Assert.InRange(info.DurationSeconds, 1.95, 2.05);
        Assert.True(data.Length < 100_000);
    }

    private static WaveInfo ReadWaveInfo(byte[] data)
    {
        var channels = BitConverter.ToInt16(data, 22);
        var sampleRate = BitConverter.ToInt32(data, 24);
        var bitsPerSample = BitConverter.ToInt16(data, 34);
        var dataLength = 0;

        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var chunkId = Encoding.ASCII.GetString(data, offset, 4);
            var chunkLength = BitConverter.ToInt32(data, offset + 4);
            if (string.Equals(chunkId, "data", StringComparison.Ordinal))
            {
                dataLength = chunkLength;
                break;
            }

            offset += 8 + chunkLength + (chunkLength % 2);
        }

        var bytesPerSecond = sampleRate * channels * (bitsPerSample / 8.0);
        return new WaveInfo(channels, sampleRate, bitsPerSample, dataLength / bytesPerSecond);
    }

    private sealed record WaveInfo(
        short Channels,
        int SampleRate,
        short BitsPerSample,
        double DurationSeconds);
}
