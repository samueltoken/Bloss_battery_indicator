using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class WindowBoundsNormalizer
{
    public static WindowBounds Normalize(
        WindowBounds savedBounds,
        IReadOnlyList<WindowBounds> workingAreas,
        out bool wasAdjusted)
    {
        ArgumentNullException.ThrowIfNull(savedBounds);

        if (workingAreas is null || workingAreas.Count == 0)
        {
            wasAdjusted = false;
            return Clone(savedBounds);
        }

        var validAreas = workingAreas
            .Where(IsValidBounds)
            .Select(Clone)
            .ToList();

        if (validAreas.Count == 0)
        {
            wasAdjusted = false;
            return Clone(savedBounds);
        }

        var primaryArea = validAreas[0];
        var source = Sanitize(savedBounds, primaryArea);
        var intersectingArea = FindIntersectingArea(source, validAreas);
        var targetArea = intersectingArea ?? primaryArea;

        var normalized = new WindowBounds
        {
            Left = source.Left,
            Top = source.Top,
            Width = Math.Min(source.Width, targetArea.Width),
            Height = Math.Min(source.Height, targetArea.Height)
        };

        if (intersectingArea is null)
        {
            normalized.Left = Clamp(
                source.Left,
                targetArea.Left,
                targetArea.Left + targetArea.Width - normalized.Width);
            normalized.Top = Clamp(
                source.Top,
                targetArea.Top,
                targetArea.Top + targetArea.Height - normalized.Height);
        }

        wasAdjusted =
            !AreClose(savedBounds.Left, normalized.Left) ||
            !AreClose(savedBounds.Top, normalized.Top) ||
            !AreClose(savedBounds.Width, normalized.Width) ||
            !AreClose(savedBounds.Height, normalized.Height);

        return normalized;
    }

    private static WindowBounds Sanitize(WindowBounds source, WindowBounds fallbackArea)
    {
        var left = IsFinite(source.Left) ? source.Left : fallbackArea.Left;
        var top = IsFinite(source.Top) ? source.Top : fallbackArea.Top;
        var width = IsFinite(source.Width) && source.Width > 0 ? source.Width : fallbackArea.Width;
        var height = IsFinite(source.Height) && source.Height > 0 ? source.Height : fallbackArea.Height;

        return new WindowBounds
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height
        };
    }

    private static WindowBounds? FindIntersectingArea(WindowBounds source, IReadOnlyList<WindowBounds> areas)
    {
        WindowBounds? bestArea = null;
        var bestOverlap = 0d;

        foreach (var area in areas)
        {
            var overlap = CalculateOverlapArea(source, area);
            if (overlap <= 0d)
            {
                continue;
            }

            if (bestArea is null || overlap > bestOverlap)
            {
                bestArea = area;
                bestOverlap = overlap;
            }
        }

        return bestArea;
    }

    private static double CalculateOverlapArea(WindowBounds first, WindowBounds second)
    {
        var left = Math.Max(first.Left, second.Left);
        var top = Math.Max(first.Top, second.Top);
        var right = Math.Min(first.Left + first.Width, second.Left + second.Width);
        var bottom = Math.Min(first.Top + first.Height, second.Top + second.Height);

        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            return 0d;
        }

        return width * height;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool IsValidBounds(WindowBounds bounds)
    {
        return IsFinite(bounds.Left) &&
               IsFinite(bounds.Top) &&
               IsFinite(bounds.Width) &&
               IsFinite(bounds.Height) &&
               bounds.Width > 0 &&
               bounds.Height > 0;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool AreClose(double left, double right)
    {
        if (!IsFinite(left) || !IsFinite(right))
        {
            return left.Equals(right);
        }

        return Math.Abs(left - right) < 0.01d;
    }

    private static WindowBounds Clone(WindowBounds source)
    {
        return new WindowBounds
        {
            Left = source.Left,
            Top = source.Top,
            Width = source.Width,
            Height = source.Height
        };
    }
}
