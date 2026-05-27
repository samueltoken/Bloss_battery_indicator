namespace BluetoothBatteryWidget.App.Services;

internal static class BatteryGuideChimeAudio
{
    public static byte[] LoadWave()
    {
        try
        {
            var option = BatteryGuideSoundCatalog.ResolveGuideSound(null);
            var data = BatteryGuideSoundCatalog.LoadBytes(option);
            if (data.Length > 44)
            {
                return data;
            }
        }
        catch
        {
            // Fall back to generated audio if the embedded asset cannot be read.
        }

        return BatteryGuideChimeBuilder.CreateDreamChimeWave();
    }
}
