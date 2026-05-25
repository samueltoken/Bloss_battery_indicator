using System.IO;
using System.Text;

namespace BluetoothBatteryWidget.App.Services;

internal static class BatteryGuideChimeBuilder
{
    public static byte[] CreateDreamChimeWave()
    {
        const int sampleRate = 44100;
        const int durationMs = 2000;
        const short channels = 2;
        const short bitsPerSample = 16;
        const short blockAlign = channels * bitsPerSample / 8;
        const int byteRate = sampleRate * blockAlign;
        var sampleCount = sampleRate * durationMs / 1000;
        var dataLength = sampleCount * blockAlign;

        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);

        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRate;
            var progress = (double)i / Math.Max(1, sampleCount - 1);
            var fadeIn = Math.Min(1.0, progress / 0.035);
            var fadeOut = Math.Min(1.0, (1.0 - progress) / 0.18);
            var safetyEnvelope = fadeIn * fadeOut;

            var bell =
                Bell(t, 0.00, 659.25, 0.56) +
                Bell(t, 0.16, 987.77, 0.42) +
                Bell(t, 0.42, 783.99, 0.34) +
                Bell(t, 0.82, 1318.51, 0.22);
            var pad = SoftTone(t, 261.63, 0.26, 0.08) +
                      SoftTone(t, 392.00, 0.19, 0.11) +
                      SoftTone(t, 523.25, 0.15, 0.13);
            var sparkle = 0.06 * Math.Sin(2.0 * Math.PI * (1760.0 + 22.0 * Math.Sin(2.0 * Math.PI * 1.3 * t)) * t) *
                          Math.Exp(-1.7 * Math.Max(0.0, t - 0.2));

            var left = (bell + pad + sparkle) * safetyEnvelope;
            var right = (Bell(t + 0.012, 0.00, 659.25, 0.48) +
                         Bell(t + 0.018, 0.16, 987.77, 0.36) +
                         Bell(t + 0.010, 0.42, 783.99, 0.31) +
                         Bell(t + 0.020, 0.82, 1318.51, 0.18) +
                         SoftTone(t + 0.006, 261.63, 0.23, 0.09) +
                         SoftTone(t + 0.004, 392.00, 0.17, 0.12) +
                         sparkle * 0.82) * safetyEnvelope;

            writer.Write(ToSample(left));
            writer.Write(ToSample(right));
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static double Bell(double t, double start, double frequency, double volume)
    {
        var local = t - start;
        if (local < 0)
        {
            return 0;
        }

        var shimmer = 1.0 + 0.006 * Math.Sin(2.0 * Math.PI * 6.0 * local);
        var decay = Math.Exp(-3.45 * local);
        var body = Math.Sin(2.0 * Math.PI * frequency * shimmer * local);
        var overtone = 0.34 * Math.Sin(2.0 * Math.PI * frequency * 2.01 * local);
        return (body + overtone) * decay * volume;
    }

    private static double SoftTone(double t, double frequency, double volume, double phase)
    {
        var slowPulse = 0.64 + 0.36 * Math.Sin(2.0 * Math.PI * 0.72 * t + phase);
        var swell = Math.Min(1.0, t / 0.45) * Math.Pow(Math.Max(0.0, 1.0 - t / 2.15), 0.75);
        return Math.Sin(2.0 * Math.PI * frequency * t + phase) * slowPulse * swell * volume;
    }

    private static short ToSample(double value)
    {
        return (short)Math.Clamp(value * 11800.0, short.MinValue, short.MaxValue);
    }
}
