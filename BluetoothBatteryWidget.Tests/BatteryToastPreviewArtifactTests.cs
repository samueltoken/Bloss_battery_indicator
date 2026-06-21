using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BluetoothBatteryWidget.App;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using WpfPath = System.Windows.Shapes.Path;

namespace BluetoothBatteryWidget.Tests;

public sealed class BatteryToastPreviewArtifactTests
{
    [Fact]
    public void ReleaseNotesWindow_RendersStyledPreviewImage()
    {
        Exception? threadException = null;
        string? outputPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                outputPath = RenderReleaseNotesWindowPreview();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.NotNull(outputPath);
        AssertRenderedPng(outputPath, 760, 520, 48 * 1024);
        AssertTransparentCornerPixels(outputPath);
    }

    private static void AssertRenderedPng(string outputPath, int expectedWidth, int expectedHeight, long minLength)
    {
        var info = new FileInfo(outputPath);
        Assert.True(info.Exists);
        Assert.True(info.Length > minLength, $"Preview image was too small: {info.Length} bytes.");

        using var stream = File.OpenRead(outputPath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        Assert.Equal(expectedWidth, frame.PixelWidth);
        Assert.Equal(expectedHeight, frame.PixelHeight);
    }

    private static void AssertTransparentCornerPixels(string outputPath)
    {
        using var stream = File.OpenRead(outputPath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var stride = frame.PixelWidth * 4;
        var pixels = new byte[stride * frame.PixelHeight];
        frame.CopyPixels(pixels, stride, 0);

        foreach (var (x, y) in new[]
        {
            (0, 0),
            (1, 1),
            (5, 5),
            (frame.PixelWidth - 1, 0),
            (0, frame.PixelHeight - 1),
            (frame.PixelWidth - 1, frame.PixelHeight - 1)
        })
        {
            var alpha = pixels[(y * stride) + (x * 4) + 3];
            Assert.Equal(0, alpha);
        }
    }

    private static void AssertCaptureTabLabelsRender(string outputPath)
    {
        using var stream = File.OpenRead(outputPath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var stride = frame.PixelWidth * 4;
        var pixels = new byte[stride * frame.PixelHeight];
        frame.CopyPixels(pixels, stride, 0);

        AssertLightPixelCount("Custom tab label", pixels, stride, 70, 66, 120, 34, 140);
        AssertLightPixelCount("PS tab label", pixels, stride, 270, 66, 48, 34, 20);
        AssertLightPixelCount("STEAMCON tab label", pixels, stride, 405, 66, 112, 34, 90);
    }

    private static void AssertCaptureBlueprintSurfaceBlendsWithWindow(string outputPath)
    {
        var image = ReadRenderedPixels(outputPath);
        var outerSurface = AverageRegion(image, 116, 346, 10, 10);
        foreach (var (label, x, y) in new[]
        {
            ("blueprint center background", 520, 352),
            ("blueprint right background", 760, 440),
            ("blueprint upper body background", 520, 192),
            ("blueprint lower body background", 720, 548)
        })
        {
            var innerSurface = AverageRegion(image, x, y, 10, 10);
            var distance = ColorDistance(outerSurface, innerSurface);
            Assert.True(
                distance < 22,
                $"{label} still looks like a separate rectangle. Color distance from window surface was {distance:0.0}.");
        }
    }

    private static void AssertCaptureBlueprintDetailLinesVisible(string outputPath)
    {
        var image = ReadRenderedPixels(outputPath);
        var surface = AverageRegion(image, 482, 352, 18, 18);
        var visibleDetailPixels = CountPixelsDifferentFrom(
            image,
            surface,
            left: 120,
            top: 155,
            width: 740,
            height: 465,
            minimumDistance: 55);

        Assert.True(
            visibleDetailPixels > 12000,
            $"Blueprint detail lines are too faint or missing: {visibleDetailPixels} visible pixels.");
    }

    private static void AssertCaptureWindowRoundedCornersTransparent(string outputPath)
    {
        var image = ReadRenderedPixels(outputPath);
        foreach (var (x, y) in new[]
        {
            (12, 12),
            (14, 12),
            (12, 14),
            (image.Width - 13, 12),
            (image.Width - 15, 12),
            (12, image.Height - 13),
            (image.Width - 13, image.Height - 13)
        })
        {
            var alpha = image.Pixels[(y * image.Stride) + (x * 4) + 3];
            Assert.True(alpha <= 0x40, $"Capture window corner at {x},{y} was not transparent enough: {alpha}.");
        }
    }

    private static void AssertCaptureSteamThemeUsesGreenSurface(string outputPath)
    {
        var image = ReadRenderedPixels(outputPath);
        var surface = AverageRegion(image, 116, 346, 10, 10);

        Assert.True(
            surface.G > surface.R + 24 && surface.G > surface.B + 10,
            $"Steam profile surface should be green, but sampled RGB was {surface.R},{surface.G},{surface.B}.");
    }

    private static void AssertCaptureCustomThemeUsesNeutralSurface(string outputPath)
    {
        var image = ReadRenderedPixels(outputPath);
        var surface = AverageRegion(image, 116, 346, 10, 10);

        Assert.True(
            Math.Abs(surface.R - surface.G) < 16 &&
            Math.Abs(surface.G - surface.B) < 22 &&
            surface.R < 40 &&
            surface.G < 44 &&
            surface.B < 52,
            $"Custom tab surface should be neutral dark, but sampled RGB was {surface.R},{surface.G},{surface.B}.");
    }

    private static RenderedPixels ReadRenderedPixels(string outputPath)
    {
        using var stream = File.OpenRead(outputPath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var stride = frame.PixelWidth * 4;
        var pixels = new byte[stride * frame.PixelHeight];
        frame.CopyPixels(pixels, stride, 0);
        return new RenderedPixels(frame.PixelWidth, frame.PixelHeight, stride, pixels);
    }

    private static Color AverageRegion(RenderedPixels image, int left, int top, int width, int height)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        long count = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
                {
                    continue;
                }

                var offset = (y * image.Stride) + (x * 4);
                var alpha = image.Pixels[offset + 3];
                if (alpha <= 0x80)
                {
                    continue;
                }

                blue += image.Pixels[offset];
                green += image.Pixels[offset + 1];
                red += image.Pixels[offset + 2];
                count++;
            }
        }

        Assert.True(count > 0, "Sample region did not contain visible pixels.");
        return Color.FromRgb((byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }

    private static int CountPixelsDifferentFrom(
        RenderedPixels image,
        Color baseColor,
        int left,
        int top,
        int width,
        int height,
        double minimumDistance)
    {
        var count = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var offset = (y * image.Stride) + (x * 4);
                var alpha = image.Pixels[offset + 3];
                if (alpha <= 0x80)
                {
                    continue;
                }

                var color = Color.FromRgb(
                    image.Pixels[offset + 2],
                    image.Pixels[offset + 1],
                    image.Pixels[offset]);
                if (ColorDistance(baseColor, color) >= minimumDistance)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static double ColorDistance(Color first, Color second)
    {
        var red = first.R - second.R;
        var green = first.G - second.G;
        var blue = first.B - second.B;
        return Math.Sqrt((red * red) + (green * green) + (blue * blue));
    }

    private sealed record RenderedPixels(int Width, int Height, int Stride, byte[] Pixels);

    private enum CapturePreviewSelectedTab
    {
        Custom,
        PlayStation,
        SteamController
    }

    private static void AssertLightPixelCount(
        string label,
        byte[] pixels,
        int stride,
        int left,
        int top,
        int width,
        int height,
        int minimum)
    {
        var count = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var offset = (y * stride) + (x * 4);
                var blue = pixels[offset];
                var green = pixels[offset + 1];
                var red = pixels[offset + 2];
                var alpha = pixels[offset + 3];
                if (alpha > 0x80 && red > 0xC8 && green > 0xC8 && blue > 0xC8)
                {
                    count++;
                }
            }
        }

        Assert.True(count >= minimum, $"{label} did not render enough readable light pixels: {count}.");
    }

    [Fact]
    public void BatteryGuideTriggerCaptureWindow_RendersOriginalBlueprintPreviewImage()
    {
        Exception? threadException = null;
        string? outputPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                outputPath = RenderBatteryGuideTriggerCaptureWindowPreview(
                    new BatteryGuideTrigger(
                        GuideButtonDeviceKind.SteamController,
                        0x45,
                        [new BatteryGuideTriggerBit(3, 0x02), new BatteryGuideTriggerBit(4, 0x01)],
                        "RB + Guide"),
                    "battery-guide-trigger-capture-window.png");
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.NotNull(outputPath);
        AssertRenderedPng(outputPath, 980, 735, 64 * 1024);
        AssertCaptureTabLabelsRender(outputPath);
    }

    [Fact]
    public void BatteryGuideTriggerCaptureWindow_ProfileTabsHighlightSavedTriggerButtons()
    {
        Exception? threadException = null;

        var thread = new Thread(() =>
        {
            try
            {
                AssertProfileTabsMarkSavedTriggersAndHighlightButtons();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }
    }

    [Fact]
    public void BatteryGuideTriggerCaptureWindow_RendersConfiguredProfileTabPreviewImage()
    {
        Exception? threadException = null;
        string? outputPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                outputPath = RenderBatteryGuideTriggerCaptureWindowProfilePreview(
                    "battery-guide-trigger-capture-profile-tabs.png",
                    CapturePreviewSelectedTab.SteamController);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.NotNull(outputPath);
        AssertRenderedPng(outputPath, 980, 735, 64 * 1024);
        AssertCaptureBlueprintSurfaceBlendsWithWindow(outputPath);
        AssertCaptureBlueprintDetailLinesVisible(outputPath);
        AssertCaptureWindowRoundedCornersTransparent(outputPath);
        AssertCaptureSteamThemeUsesGreenSurface(outputPath);
    }

    [Fact]
    public void BatteryGuideTriggerCaptureWindow_RendersCustomTabNeutralPreviewImage()
    {
        Exception? threadException = null;
        string? outputPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                outputPath = RenderBatteryGuideTriggerCaptureWindowProfilePreview(
                    "battery-guide-trigger-capture-custom-tab.png",
                    CapturePreviewSelectedTab.Custom);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.NotNull(outputPath);
        AssertRenderedPng(outputPath, 980, 735, 64 * 1024);
        AssertCaptureBlueprintSurfaceBlendsWithWindow(outputPath);
        AssertCaptureBlueprintDetailLinesVisible(outputPath);
        AssertCaptureWindowRoundedCornersTransparent(outputPath);
        AssertCaptureCustomThemeUsesNeutralSurface(outputPath);
    }

    [Fact]
    public void BatteryGuideTriggerCaptureWindow_RendersDpadLeftPreviewImage()
    {
        Exception? threadException = null;
        string? outputPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                outputPath = RenderBatteryGuideTriggerCaptureWindowPreview(
                    new BatteryGuideTrigger(
                        GuideButtonDeviceKind.SteamController,
                        0x45,
                        [new BatteryGuideTriggerBit(3, 0x10)],
                        "Left"),
                    "battery-guide-trigger-capture-dpad-left.png");
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.NotNull(outputPath);
        Assert.True(new FileInfo(outputPath).Length > 64 * 1024);
    }

    [Fact]
    public void BatteryAlertThresholdsWindow_RendersStyledPreviewImage()
    {
        Exception? threadException = null;
        string? outputPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                outputPath = RenderBatteryAlertThresholdsWindowPreview();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.NotNull(outputPath);
        Assert.True(new FileInfo(outputPath).Length > 4096);
    }

    [Fact]
    public void BatteryAlertThresholdsWindow_TogglesThresholdButtonsWithoutClosing()
    {
        Exception? threadException = null;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new BatteryAlertThresholdsWindow(string.Empty);
                try
                {
                    var rootElement = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                    rootElement.Measure(new Size(window.Width, window.Height));
                    rootElement.Arrange(new Rect(0, 0, window.Width, window.Height));
                    rootElement.UpdateLayout();

                    var thresholdButtons = FindVisualChildren<System.Windows.Controls.Primitives.ToggleButton>(rootElement)
                        .Where(button => button.Tag is int)
                        .OrderBy(button => (int)button.Tag)
                        .ToArray();

                    Assert.Equal(BatteryAlertThresholdsWindow.SelectableThresholds, thresholdButtons.Select(button => (int)button.Tag));
                    foreach (var button in thresholdButtons)
                    {
                        button.ApplyTemplate();
                        button.IsChecked = true;
                        button.IsChecked = false;
                        button.IsChecked = true;
                    }
                }
                finally
                {
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }
    }

    [Fact]
    public void SecondaryWindows_PopInAnimationSettlesToStableFinalValues()
    {
        Exception? threadException = null;

        var thread = new Thread(() =>
        {
            try
            {
                AssertSecondaryWindowsPopInSettles();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }
    }

    [Fact]
    public void BatteryGuideTriggerResetIcon_RendersUnclippedPreviewImage()
    {
        Exception? threadException = null;
        string? outputPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                outputPath = RenderBatteryGuideTriggerResetIconPreview();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.NotNull(outputPath);
        Assert.True(new FileInfo(outputPath).Length > 1024);
    }

    private static void AssertProfileTabsMarkSavedTriggersAndHighlightButtons()
    {
        var dualSenseTrigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.DualSense,
            0x01,
            [new BatteryGuideTriggerBit(9, 0x02), new BatteryGuideTriggerBit(9, 0x20)],
            "R1 + Options");
        var steamControllerTrigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.SteamController,
            0x45,
            [new BatteryGuideTriggerBit(2, 0x10), new BatteryGuideTriggerBit(4, 0x01)],
            "Quick Access + Guide");
        var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WidgetSettings.DualSenseBatteryGuideProfileKey] = dualSenseTrigger.ToPersistedString(),
            [WidgetSettings.SteamControllerBatteryGuideProfileKey] = steamControllerTrigger.ToPersistedString()
        };

        var window = new BatteryGuideTriggerCaptureWindow
        {
            Width = 980,
            Height = 735
        };
        try
        {
            window.SetProfiles(profiles, string.Empty);
            LayoutWindowContent(window);

            Assert.Equal("Configured", window.PlayStationTabItem.Tag);
            Assert.Equal("SteamConfigured", window.SteamControllerTabItem.Tag);

            window.CaptureTabControl.SelectedItem = window.CustomTabItem;
            LayoutWindowContent(window);
            Assert.Equal(Color.FromRgb(0x0B, 0x0E, 0x12), window.WindowSurfaceBackgroundBrush.Color);

            window.CaptureTabControl.SelectedItem = window.PlayStationTabItem;
            LayoutWindowContent(window);
            AssertHotspotColor(window.RBKey, Color.FromArgb(0xE6, 0x1E, 0x78, 0xFF));
            AssertHotspotColor(window.MenuKey, Color.FromArgb(0xE6, 0x1E, 0x78, 0xFF));
            AssertHotspotColor(window.QuickAccessKey, Color.FromArgb(0x00, 0x00, 0x00, 0x00));

            window.CaptureTabControl.SelectedItem = window.SteamControllerTabItem;
            LayoutWindowContent(window);
            AssertHotspotColor(window.QuickAccessKey, Color.FromArgb(0xE6, 0x1E, 0x78, 0xFF));
            AssertHotspotColor(window.GuideKey, Color.FromArgb(0xE6, 0x1E, 0x78, 0xFF));
            AssertHotspotColor(window.RBKey, Color.FromArgb(0x00, 0x00, 0x00, 0x00));
        }
        finally
        {
            window.Close();
        }
    }

    private static void LayoutWindowContent(Window window)
    {
        var rootElement = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
        rootElement.Measure(new Size(window.Width, window.Height));
        rootElement.Arrange(new Rect(0, 0, window.Width, window.Height));
        rootElement.UpdateLayout();
    }

    private static void AssertHotspotColor(Border border, Color expected)
    {
        var brush = Assert.IsType<SolidColorBrush>(border.Background);
        Assert.Equal(expected, brush.Color);
    }

    private static string RenderBatteryGuideTriggerCaptureWindowProfilePreview(
        string fileName,
        CapturePreviewSelectedTab selectedTab)
    {
        var root = GetProjectRoot();
        var outputDirectory = Path.Combine(root, "artifacts", "alert-toast-previews");
        Directory.CreateDirectory(outputDirectory);

        var dualSenseTrigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.DualSense,
            0x01,
            [new BatteryGuideTriggerBit(9, 0x02), new BatteryGuideTriggerBit(9, 0x20)],
            "R1 + Options");
        var steamControllerTrigger = new BatteryGuideTrigger(
            GuideButtonDeviceKind.SteamController,
            0x45,
            [new BatteryGuideTriggerBit(2, 0x10), new BatteryGuideTriggerBit(4, 0x01)],
            "Quick Access + Guide");
        var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WidgetSettings.DualSenseBatteryGuideProfileKey] = dualSenseTrigger.ToPersistedString(),
            [WidgetSettings.SteamControllerBatteryGuideProfileKey] = steamControllerTrigger.ToPersistedString()
        };

        var window = new BatteryGuideTriggerCaptureWindow
        {
            Width = 980,
            Height = 735
        };
        try
        {
            window.SetProfiles(profiles, string.Empty);
            window.CaptureTabControl.SelectedItem = selectedTab switch
            {
                CapturePreviewSelectedTab.Custom => window.CustomTabItem,
                CapturePreviewSelectedTab.PlayStation => window.PlayStationTabItem,
                CapturePreviewSelectedTab.SteamController => window.SteamControllerTabItem,
                _ => window.CustomTabItem
            };

            var rootElement = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
            rootElement.Measure(new Size(window.Width, window.Height));
            rootElement.Arrange(new Rect(0, 0, window.Width, window.Height));
            rootElement.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)window.Width,
                (int)window.Height,
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(rootElement);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var outputPath = Path.Combine(outputDirectory, fileName);
            using var stream = File.Create(outputPath);
            encoder.Save(stream);
            return outputPath;
        }
        finally
        {
            window.Close();
        }
    }

    private static string RenderBatteryGuideTriggerCaptureWindowPreview(BatteryGuideTrigger trigger, string fileName)
    {
        var root = GetProjectRoot();
        var outputDirectory = Path.Combine(root, "artifacts", "alert-toast-previews");
        Directory.CreateDirectory(outputDirectory);

        var window = new BatteryGuideTriggerCaptureWindow
        {
            Width = 980,
            Height = 735
        };
        try
        {
            window.SetCandidate(trigger);

            var rootElement = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
            rootElement.Measure(new Size(window.Width, window.Height));
            rootElement.Arrange(new Rect(0, 0, window.Width, window.Height));
            rootElement.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)window.Width,
                (int)window.Height,
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(rootElement);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var outputPath = Path.Combine(outputDirectory, fileName);
            using var stream = File.Create(outputPath);
            encoder.Save(stream);
            return outputPath;
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertSecondaryWindowsPopInSettles()
    {
        var windows = new List<Window>();
        string? tempImagePath = null;
        try
        {
            var alertWindow = PrepareWindow(new BatteryAlertThresholdsWindow("30, 50"));
            windows.Add(alertWindow);
            AssertButtonOriginPopInSettles(alertWindow);

            var guideWindow = PrepareWindow(new BatteryGuideTriggerCaptureWindow());
            windows.Add(guideWindow);
            AssertButtonOriginPopInSettles(guideWindow);

            var iconOverrideWindow = PrepareWindow(new IconOverrideWindow(
                [CreateSnapshot()],
                new Dictionary<string, IconKey>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
            windows.Add(iconOverrideWindow);
            AssertCenteredPopInSettles(iconOverrideWindow);

            tempImagePath = CreateTempPreviewImage();
            var imageAdjustWindow = PrepareWindow(new IconImageAdjustWindow(tempImagePath));
            windows.Add(imageAdjustWindow);
            AssertCenteredPopInSettles(imageAdjustWindow);
        }
        finally
        {
            foreach (var window in windows)
            {
                window.Close();
            }

            if (!string.IsNullOrWhiteSpace(tempImagePath) && File.Exists(tempImagePath))
            {
                File.Delete(tempImagePath);
            }
        }
    }

    private static T PrepareWindow<T>(T window)
        where T : Window
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000d;
        window.Top = -20000d;
        window.ShowInTaskbar = false;
        if (window is BatteryAlertThresholdsWindow alertWindow)
        {
            alertWindow.PopInOriginScreenPoint = new Point(window.Left + 32d, window.Top + 32d);
        }
        else if (window is BatteryGuideTriggerCaptureWindow guideWindow)
        {
            guideWindow.PopInOriginScreenPoint = new Point(window.Left + 32d, window.Top + 32d);
        }

        return window;
    }

    private static void AssertButtonOriginPopInSettles(Window window)
    {
        var surface = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("WindowSurface"));
        var scale = Assert.IsAssignableFrom<ScaleTransform>(window.FindName("WindowSurfaceScale"));
        var skew = Assert.IsAssignableFrom<SkewTransform>(window.FindName("WindowSurfaceSkew"));
        var translate = Assert.IsAssignableFrom<TranslateTransform>(window.FindName("WindowSurfaceTranslate"));

        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(1000));

        AssertCloseTo(1d, window.Opacity);
        AssertCloseTo(1d, scale.ScaleX);
        AssertCloseTo(1d, scale.ScaleY);
        AssertCloseTo(0d, skew.AngleX);
        AssertCloseTo(0d, skew.AngleY);
        AssertCloseTo(0d, translate.X);
        AssertCloseTo(0d, translate.Y);
    }

    private static void AssertCenteredPopInSettles(Window window)
    {
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(760));

        AssertCloseTo(1d, window.Opacity);
        var surface = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
        var transformGroup = Assert.IsType<TransformGroup>(surface.RenderTransform);
        var scale = Assert.Single(transformGroup.Children.OfType<ScaleTransform>());
        var translate = Assert.Single(transformGroup.Children.OfType<TranslateTransform>());

        Assert.Equal(new Point(0.5d, 0.5d), surface.RenderTransformOrigin);
        AssertCloseTo(1d, scale.ScaleX);
        AssertCloseTo(1d, scale.ScaleY);
        AssertCloseTo(0d, translate.Y);
    }

    private static void WaitForDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void AssertCloseTo(double expected, double actual)
    {
        Assert.InRange(actual, expected - 0.001d, expected + 0.001d);
    }

    private static DeviceBatterySnapshot CreateSnapshot()
    {
        return new DeviceBatterySnapshot(
            DeviceId: "test-device",
            Address: "AABBCCDDEEFF",
            DisplayName: "Test Controller",
            BatteryPercent: 88,
            BatteryConfidence: BatteryConfidence.Confirmed,
            IsConnected: true,
            Category: DeviceCategory.Gamepad,
            IconKey: IconKey.Gamepad,
            LastUpdated: DateTimeOffset.Now);
    }

    private static string CreateTempPreviewImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bloss-icon-adjust-test-{Guid.NewGuid():N}.png");
        var bitmap = new WriteableBitmap(32, 32, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[32 * 32 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 0x55;
            pixels[index + 1] = 0x99;
            pixels[index + 2] = 0xDD;
            pixels[index + 3] = 0xFF;
        }

        bitmap.WritePixels(new Int32Rect(0, 0, 32, 32), pixels, 32 * 4, 0);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static string RenderReleaseNotesWindowPreview()
    {
        var root = GetProjectRoot();
        var outputDirectory = Path.Combine(root, "artifacts", "release-notes-previews");
        Directory.CreateDirectory(outputDirectory);

        var window = new ReleaseNotesWindow("1.0.8");
        try
        {
            var rootElement = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
            rootElement.Measure(new Size(window.Width, window.Height));
            rootElement.Arrange(new Rect(0, 0, window.Width, window.Height));
            rootElement.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)window.Width,
                (int)window.Height,
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(rootElement);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var outputPath = Path.Combine(outputDirectory, "release-notes-window.png");
            using var stream = File.Create(outputPath);
            encoder.Save(stream);
            return outputPath;
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void BatteryToastWindow_RendersAutomaticAlertPreviewImages()
    {
        Exception? threadException = null;
        var outputPaths = Array.Empty<string>();

        var thread = new Thread(() =>
        {
            try
            {
                outputPaths = RenderAutomaticAlertPreviewImages();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.Equal(7, outputPaths.Length);
        Assert.All(outputPaths, path => Assert.True(new FileInfo(path).Length > 1024));
    }

    private static string RenderBatteryAlertThresholdsWindowPreview()
    {
        var root = GetProjectRoot();
        var outputDirectory = Path.Combine(root, "artifacts", "alert-toast-previews");
        Directory.CreateDirectory(outputDirectory);

        var window = new BatteryAlertThresholdsWindow("30, 50, 80");
        try
        {
            var rootElement = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
            rootElement.Measure(new Size(window.Width, window.Height));
            rootElement.Arrange(new Rect(0, 0, window.Width, window.Height));
            rootElement.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)window.Width,
                (int)window.Height,
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(rootElement);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var outputPath = Path.Combine(outputDirectory, "battery-alert-settings-window.png");
            using var stream = File.Create(outputPath);
            encoder.Save(stream);
            return outputPath;
        }
        finally
        {
            window.Close();
        }
    }

    private static string RenderBatteryGuideTriggerResetIconPreview()
    {
        var root = GetProjectRoot();
        var outputDirectory = Path.Combine(root, "artifacts", "alert-toast-previews");
        Directory.CreateDirectory(outputDirectory);

        var mainWindowXaml = File.ReadAllText(Path.Combine(root, "BluetoothBatteryWidget.App", "MainWindow.xaml"));
        var buttonAssetPath = Path.Combine(root, "BluetoothBatteryWidget.App", "Assets", "reset-button-blue.png");
        Assert.True(File.Exists(buttonAssetPath));
        var strokeMatch = Regex.Match(
            mainWindowXaml,
            "BatteryGuideTriggerResetPath\"[\\s\\S]*?Data=\"(?<data>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(strokeMatch.Success);

        var canvas = new Canvas
        {
            Width = 24,
            Height = 24,
            ClipToBounds = false
        };
        canvas.Children.Add(new WpfPath
        {
            Data = Geometry.Parse(strokeMatch.Groups["data"].Value),
            Fill = Brushes.White
        });

        var preview = new Grid
        {
            Width = 112,
            Height = 112,
            Background = Brushes.Transparent
        };
        preview.Children.Add(new Image
        {
            Width = 70,
            Height = 70,
            Source = new BitmapImage(new Uri(buttonAssetPath)),
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true
        });
        preview.Children.Add(new Viewbox
        {
            Width = 39,
            Height = 39,
            Stretch = Stretch.Uniform,
            Child = canvas
        });
        foreach (var child in preview.Children.OfType<FrameworkElement>())
        {
            child.HorizontalAlignment = HorizontalAlignment.Center;
            child.VerticalAlignment = VerticalAlignment.Center;
        }

        preview.Measure(new Size(preview.Width, preview.Height));
        preview.Arrange(new Rect(0, 0, preview.Width, preview.Height));
        preview.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            (int)preview.Width,
            (int)preview.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(preview);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        var outputPath = Path.Combine(outputDirectory, "battery-guide-reset-icon.png");
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
        return outputPath;
    }

    private static string[] RenderAutomaticAlertPreviewImages()
    {
        var root = GetProjectRoot();
        var outputDirectory = Path.Combine(root, "artifacts", "alert-toast-previews");
        Directory.CreateDirectory(outputDirectory);

        var paths = new List<string>();
        foreach (var percent in new[] { 15, 30, 40, 50, 60, 70, 80 })
        {
            var subtitle = percent switch
            {
                >= 80 => "배터리 충분",
                >= 60 => "배터리 닳는중",
                >= 30 => "충전이 필요함",
                _ => "바로 충전하세요"
            };
            var toast = new BatteryToastWindow(
                "Bloss 알림 예시",
                percent,
                subtitle,
                BatteryToastStyle.ResolveSeverity(percent));
            try
            {
                var rootElement = Assert.IsAssignableFrom<FrameworkElement>(toast.Content);
                rootElement.Measure(new Size(toast.Width, toast.Height));
                rootElement.Arrange(new Rect(0, 0, toast.Width, toast.Height));
                rootElement.UpdateLayout();

                var bitmap = new RenderTargetBitmap(
                    (int)toast.Width,
                    (int)toast.Height,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(rootElement);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                var outputPath = Path.Combine(outputDirectory, $"battery-alert-{percent}.png");
                using var stream = File.Create(outputPath);
                encoder.Save(stream);
                paths.Add(outputPath);
            }
            finally
            {
                toast.Close();
            }
        }

        return paths.ToArray();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
    }
}
