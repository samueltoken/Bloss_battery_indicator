using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class WindowBoundsNormalizerTests
{
    [Fact]
    public void Normalize_WhenBoundsIntersectWorkingArea_KeepsPositionAndSize()
    {
        var savedBounds = new WindowBounds
        {
            Left = 120,
            Top = 80,
            Width = 540,
            Height = 560
        };

        var workingAreas = new List<WindowBounds>
        {
            new()
            {
                Left = 0,
                Top = 0,
                Width = 1920,
                Height = 1080
            }
        };

        var normalized = WindowBoundsNormalizer.Normalize(savedBounds, workingAreas, out var wasAdjusted);

        Assert.False(wasAdjusted);
        Assert.Equal(savedBounds.Left, normalized.Left);
        Assert.Equal(savedBounds.Top, normalized.Top);
        Assert.Equal(savedBounds.Width, normalized.Width);
        Assert.Equal(savedBounds.Height, normalized.Height);
    }

    [Fact]
    public void Normalize_WhenBoundsAreOffscreen_ClampsToPrimaryWorkingArea()
    {
        var savedBounds = new WindowBounds
        {
            Left = 2500,
            Top = 100,
            Width = 540,
            Height = 560
        };

        var workingAreas = new List<WindowBounds>
        {
            new()
            {
                Left = 0,
                Top = 0,
                Width = 1920,
                Height = 1080
            }
        };

        var normalized = WindowBoundsNormalizer.Normalize(savedBounds, workingAreas, out var wasAdjusted);

        Assert.True(wasAdjusted);
        Assert.Equal(1380, normalized.Left);
        Assert.Equal(100, normalized.Top);
        Assert.Equal(540, normalized.Width);
        Assert.Equal(560, normalized.Height);
    }

    [Fact]
    public void Normalize_WhenBoundsExceedWorkingArea_ShrinksSizeToWorkingArea()
    {
        var savedBounds = new WindowBounds
        {
            Left = 100,
            Top = 100,
            Width = 2600,
            Height = 1400
        };

        var workingAreas = new List<WindowBounds>
        {
            new()
            {
                Left = 0,
                Top = 0,
                Width = 1920,
                Height = 1080
            }
        };

        var normalized = WindowBoundsNormalizer.Normalize(savedBounds, workingAreas, out var wasAdjusted);

        Assert.True(wasAdjusted);
        Assert.Equal(100, normalized.Left);
        Assert.Equal(100, normalized.Top);
        Assert.Equal(1920, normalized.Width);
        Assert.Equal(1080, normalized.Height);
    }
}
