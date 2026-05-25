using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BluetoothBatteryWidget.App.Services;

public static class CustomIconImageProcessor
{
    private const double FeatherRatio = 0.18d;

    public static bool TryCreateSoftRoundIcon(string sourcePath, string targetPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetPath) || !File.Exists(sourcePath))
            {
                return false;
            }

            var bitmap = LoadBitmap(sourcePath);
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0d);
            converted.Freeze();

            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var stride = checked(width * 4);
            var pixels = new byte[checked(stride * height)];
            converted.CopyPixels(pixels, stride, 0);
            ApplySoftRoundMask(pixels, width, height, stride);

            var output = BitmapSource.Create(
                width,
                height,
                bitmap.DpiX,
                bitmap.DpiY,
                PixelFormats.Pbgra32,
                null,
                pixels,
                stride);
            output.Freeze();

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var stream = File.Create(targetPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            encoder.Save(stream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static void ApplySoftRoundMask(byte[] pixels, int width, int height, int stride)
    {
        var centerX = (width - 1d) * 0.5d;
        var centerY = (height - 1d) * 0.5d;
        var radius = (Math.Min(width, height) * 0.5d) - 1d;
        var feather = Math.Max(10d, Math.Min(width, height) * FeatherRatio);
        var solidRadius = Math.Max(0d, radius - feather);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var mask = distance <= solidRadius
                    ? 1d
                    : Math.Clamp((radius - distance) / Math.Max(0.0001d, feather), 0d, 1d);
                mask = mask * mask * (3d - (2d * mask));

                var index = (y * stride) + (x * 4);
                pixels[index] = ScaleByte(pixels[index], mask);
                pixels[index + 1] = ScaleByte(pixels[index + 1], mask);
                pixels[index + 2] = ScaleByte(pixels[index + 2], mask);
                pixels[index + 3] = ScaleByte(pixels[index + 3], mask);
            }
        }
    }

    private static BitmapSource LoadBitmap(string sourcePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static byte ScaleByte(byte value, double scale)
    {
        return (byte)Math.Clamp(Math.Round(value * scale), 0d, 255d);
    }
}
