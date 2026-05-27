using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryGuideSoundCatalogTests
{
    [Fact]
    public void GuideOptions_ExposeRequestedSoundNames()
    {
        var names = BatteryGuideSoundCatalog.GuideOptions.Select(option => option.DisplayName).ToArray();

        Assert.Contains("Infographic 2 sec", names);
        Assert.Contains("Infographic 1 sec", names);
        Assert.Contains("long ago", names);
        Assert.Contains("Rick", names);
        Assert.Contains("Warning", names);
        Assert.Contains("Smile", names);
    }

    [Fact]
    public void ResolveGuideSound_InvalidValue_FallsBackToTwoSecondInfographic()
    {
        var option = BatteryGuideSoundCatalog.ResolveGuideSound("missing");

        Assert.Equal(WidgetSettings.GuideSoundInfographic2Seconds, option.Id);
        Assert.Equal("Infographic 2 sec", option.DisplayName);
    }

    [Fact]
    public void CustomGuideSound_FilePath_IsIncludedAndReadable()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"bloss-custom-guide-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(tempPath, new byte[] { 82, 73, 70, 70, 44, 0, 0, 0, 87, 65, 86, 69 });

        try
        {
            var options = BatteryGuideSoundCatalog.GetGuideOptions(tempPath);
            var custom = Assert.Single(options, option => option.Id == WidgetSettings.GuideSoundCustomFile);

            Assert.Equal(tempPath, custom.ExternalPath);
            Assert.Equal("Custom sound", custom.DisplayName);
            Assert.Equal(custom, BatteryGuideSoundCatalog.ResolveGuideSound(WidgetSettings.GuideSoundCustomFile, tempPath));
            Assert.Equal(12, BatteryGuideSoundCatalog.LoadBytes(custom).Length);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void CustomGuideSound_MissingFile_FallsBackToDefault()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.wav");

        var option = BatteryGuideSoundCatalog.ResolveGuideSound(WidgetSettings.GuideSoundCustomFile, missingPath);

        Assert.Equal(WidgetSettings.DefaultGuideSoundId, option.Id);
        Assert.DoesNotContain(BatteryGuideSoundCatalog.GetGuideOptions(missingPath), item => item.Id == WidgetSettings.GuideSoundCustomFile);
    }

    [Theory]
    [InlineData(WidgetSettings.GuideSoundInfographic2Seconds)]
    [InlineData(WidgetSettings.GuideSoundInfographic1Second)]
    [InlineData(WidgetSettings.GuideSoundLongAgo)]
    [InlineData(WidgetSettings.GuideSoundRick)]
    [InlineData(WidgetSettings.GuideSoundWarning)]
    [InlineData(WidgetSettings.GuideSoundSmile)]
    public void GuideSoundResources_AreEmbedded(string soundId)
    {
        var option = BatteryGuideSoundCatalog.ResolveGuideSound(soundId);
        var data = BatteryGuideSoundCatalog.LoadBytes(option);

        Assert.NotEmpty(data);
        Assert.True(data.Length > 44);
    }

    [Fact]
    public void InfographicOneSecond_IsShorterThanTwoSecondVersion()
    {
        var twoSecond = BatteryGuideSoundCatalog.LoadBytes(
            BatteryGuideSoundCatalog.ResolveGuideSound(WidgetSettings.GuideSoundInfographic2Seconds));
        var oneSecond = BatteryGuideSoundCatalog.LoadBytes(
            BatteryGuideSoundCatalog.ResolveGuideSound(WidgetSettings.GuideSoundInfographic1Second));

        Assert.InRange(ReadWaveDurationSeconds(twoSecond), 1.95, 2.05);
        Assert.InRange(ReadWaveDurationSeconds(oneSecond), 0.95, 1.05);
        Assert.True(oneSecond.Length < twoSecond.Length);
    }

    [Fact]
    public void OuterSpaceSound_IsLabsOnlyResource()
    {
        Assert.Equal("Outer Space", BatteryGuideSoundCatalog.OuterSpaceSound.DisplayName);
        Assert.DoesNotContain(BatteryGuideSoundCatalog.GuideOptions, option => option.Id == BatteryGuideSoundCatalog.OuterSpaceSound.Id);
        Assert.True(BatteryGuideSoundCatalog.LoadBytes(BatteryGuideSoundCatalog.OuterSpaceSound).Length > 44);
    }

    private static double ReadWaveDurationSeconds(byte[] data)
    {
        var channels = BitConverter.ToInt16(data, 22);
        var sampleRate = BitConverter.ToInt32(data, 24);
        var bitsPerSample = BitConverter.ToInt16(data, 34);
        var dataLength = 0;
        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            var chunkLength = BitConverter.ToInt32(data, offset + 4);
            if (string.Equals(chunkId, "data", StringComparison.Ordinal))
            {
                dataLength = chunkLength;
                break;
            }

            offset += 8 + chunkLength + (chunkLength % 2);
        }

        var bytesPerSecond = sampleRate * channels * (bitsPerSample / 8.0);
        return dataLength / bytesPerSecond;
    }
}
