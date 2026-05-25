using System.IO;
using System.Reflection;

namespace BluetoothBatteryWidget.App.Services;

internal static class BatteryGuideChimeAudio
{
    private const string ResourceName = "BluetoothBatteryWidget.App.Assets.battery-guide-chime.wav";

    public static byte[] LoadWave()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is not null)
            {
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                var data = memory.ToArray();
                if (data.Length > 44)
                {
                    return data;
                }
            }
        }
        catch
        {
            // Fall back to generated audio if the embedded asset cannot be read.
        }

        return BatteryGuideChimeBuilder.CreateDreamChimeWave();
    }
}
