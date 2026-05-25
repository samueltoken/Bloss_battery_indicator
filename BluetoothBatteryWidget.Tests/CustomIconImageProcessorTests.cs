using System.Windows.Media;
using System.Windows.Media.Imaging;
using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class CustomIconImageProcessorTests
{
    [Fact]
    public void TryCreateSoftRoundIcon_FeathersCornerAlphaAndKeepsCenterOpaque()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "BlossTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, "source.png");
        var targetPath = Path.Combine(tempDirectory, "target.png");

        try
        {
            SaveSolidPng(sourcePath, 64, 64);

            Assert.True(CustomIconImageProcessor.TryCreateSoftRoundIcon(sourcePath, targetPath));

            var processed = LoadBitmap(targetPath);
            var stride = processed.PixelWidth * 4;
            var pixels = new byte[stride * processed.PixelHeight];
            processed.CopyPixels(pixels, stride, 0);

            Assert.Equal(0, pixels[3]);
            var centerIndex = (32 * stride) + (32 * 4);
            Assert.True(pixels[centerIndex + 3] > 240);
            var featherIndex = (32 * stride) + (56 * 4);
            Assert.InRange(pixels[featherIndex + 3], 80, 220);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp test files.
            }
        }
    }

    private static void SaveSolidPng(string path, int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 0;
            pixels[index + 1] = 64;
            pixels[index + 2] = 220;
            pixels[index + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96d, 96d, PixelFormats.Pbgra32, null, pixels, stride);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0d);
    }
}
