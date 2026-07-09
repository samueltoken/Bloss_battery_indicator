using System.Windows.Controls.Primitives;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App;

internal sealed record SettingsPopupLayoutPlan(
    PlacementMode Placement,
    double Width,
    double MaxHeight,
    double HorizontalOffset,
    double VerticalOffset);

internal static class SettingsPopupLayoutPlanner
{
    public const double LegacyWidth = 348d;
    public const double LegacyVerticalOffset = 8d;

    private const double CenteredMinWidth = 320d;
    private const double CenteredMaxWidth = 420d;
    private const double CenteredEdgeMargin = 24d;

    public static SettingsPopupLayoutPlan CreateCentered(WindowBounds workArea, double desiredHeight)
    {
        var width = CalculateCenteredWidth(workArea.Width);
        var maxHeight = CalculateCenteredMaxHeight(workArea.Height);
        var height = desiredHeight > 0d
            ? Math.Min(desiredHeight, maxHeight)
            : maxHeight;

        return new SettingsPopupLayoutPlan(
            PlacementMode.Absolute,
            width,
            maxHeight,
            workArea.Left + Math.Max(0d, (workArea.Width - width) / 2d),
            workArea.Top + Math.Max(0d, (workArea.Height - height) / 2d));
    }

    public static SettingsPopupLayoutPlan CreateLegacy(WindowBounds workArea)
    {
        return new SettingsPopupLayoutPlan(
            PlacementMode.Bottom,
            LegacyWidth,
            CalculateLegacyMaxHeight(workArea.Height),
            0d,
            LegacyVerticalOffset);
    }

    private static double CalculateCenteredWidth(double workAreaWidth)
    {
        var usableWidth = Math.Max(1d, workAreaWidth - (CenteredEdgeMargin * 2d));
        if (usableWidth < CenteredMinWidth)
        {
            return usableWidth;
        }

        return Math.Min(CenteredMaxWidth, usableWidth);
    }

    private static double CalculateCenteredMaxHeight(double workAreaHeight)
    {
        return Math.Max(1d, workAreaHeight - (CenteredEdgeMargin * 2d));
    }

    private static double CalculateLegacyMaxHeight(double workAreaHeight)
    {
        return Math.Max(1d, workAreaHeight - (CenteredEdgeMargin * 2d));
    }
}
