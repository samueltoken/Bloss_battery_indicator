using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using Forms = System.Windows.Forms;

namespace BluetoothBatteryWidget.App;

public partial class MainWindow
{
    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(new Action(QueueWidgetBoundsRepairAfterDisplayChange));
            return;
        }

        QueueWidgetBoundsRepairAfterDisplayChange();
    }

    private async void QueueWidgetBoundsRepairAfterDisplayChange()
    {
        try
        {
            await Task.Delay(500).ConfigureAwait(true);
            if (_isExiting)
            {
                return;
            }

            EnsureWidgetAccessibleOnConnectedMonitor();
        }
        catch
        {
            // Display topology recovery is best-effort only.
        }
    }

    private void ResetWidgetPositionToCurrentMonitor()
    {
        WindowState = WindowState.Normal;
        Opacity = 1d;

        if (!IsVisible)
        {
            Show();
        }

        var area = GetWorkingAreaFromCurrentCursor();
        CenterWindowInArea(area);
        SaveWindowBounds();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void EnsureWidgetAccessibleOnConnectedMonitor()
    {
        var workingAreas = GetWorkingAreas();
        if (workingAreas.Count == 0)
        {
            return;
        }

        var currentBounds = new WindowBounds
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };

        if (HasMeaningfulVisibleArea(currentBounds, workingAreas))
        {
            var clampedBounds = ClampToAccessibleBounds(currentBounds, workingAreas);
            if (!AreClose(currentBounds, clampedBounds))
            {
                Left = clampedBounds.Left;
                Top = clampedBounds.Top;
                Width = clampedBounds.Width;
                Height = clampedBounds.Height;
                SaveWindowBounds();
                UpdateSettingsPopupLayout();
            }

            return;
        }

        CenterWindowInArea(GetWorkingAreaFromCurrentCursor());
        SaveWindowBounds();
        UpdateSettingsPopupLayout();
    }

    private void CenterWindowInArea(WindowBounds area)
    {
        var width = Math.Clamp(ActualWidth > 0d ? ActualWidth : Width, MinWidth, Math.Max(MinWidth, area.Width - StartupSafetyMargin * 2d));
        var height = Math.Clamp(ActualHeight > 0d ? ActualHeight : Height, MinHeight, Math.Max(MinHeight, area.Height - StartupSafetyMargin * 2d));

        Width = width;
        Height = height;
        Left = area.Left + Math.Max(StartupSafetyMargin, (area.Width - width) / 2d);
        Top = area.Top + Math.Max(StartupSafetyMargin, (area.Height - height) / 2d);
    }

    private WindowBounds GetWorkingAreaForOwnerWindow()
    {
        var width = ActualWidth > 0d ? ActualWidth : Width;
        var height = ActualHeight > 0d ? ActualHeight : Height;
        if (double.IsNaN(Left) ||
            double.IsNaN(Top) ||
            double.IsNaN(width) ||
            double.IsNaN(height) ||
            double.IsInfinity(Left) ||
            double.IsInfinity(Top) ||
            double.IsInfinity(width) ||
            double.IsInfinity(height) ||
            width <= 0d ||
            height <= 0d)
        {
            return GetWorkingAreaFromCurrentCursor();
        }

        var center = new System.Drawing.Point(
            (int)Math.Round(Left + (width / 2d)),
            (int)Math.Round(Top + (height / 2d)));
        return GetWorkingAreaFromScreen(Forms.Screen.FromPoint(center), this);
    }

    private static WindowBounds GetWorkingAreaFromCurrentCursor()
    {
        return GetWorkingAreaFromScreen(Forms.Screen.FromPoint(Forms.Cursor.Position), null);
    }

    private static WindowBounds GetWorkingAreaFromScreen(Forms.Screen screen, Visual? dpiSource)
    {
        var workingArea = screen.WorkingArea;
        if (dpiSource is not null)
        {
            var presentationSource = PresentationSource.FromVisual(dpiSource);
            var transform = presentationSource?.CompositionTarget?.TransformFromDevice;
            if (transform is not null)
            {
                var topLeft = transform.Value.Transform(new System.Windows.Point(workingArea.Left, workingArea.Top));
                var bottomRight = transform.Value.Transform(new System.Windows.Point(workingArea.Right, workingArea.Bottom));
                return new WindowBounds
                {
                    Left = topLeft.X,
                    Top = topLeft.Y,
                    Width = Math.Max(1d, bottomRight.X - topLeft.X),
                    Height = Math.Max(1d, bottomRight.Y - topLeft.Y)
                };
            }
        }

        return new WindowBounds
        {
            Left = workingArea.Left,
            Top = workingArea.Top,
            Width = workingArea.Width,
            Height = workingArea.Height
        };
    }

    private void ApplyWindowBounds()
    {
        var workingAreas = GetWorkingAreas();
        var primaryArea = workingAreas.Count > 0
            ? workingAreas[0]
            : new WindowBounds
            {
                Left = SystemParameters.WorkArea.Left,
                Top = SystemParameters.WorkArea.Top,
                Width = SystemParameters.WorkArea.Width,
                Height = SystemParameters.WorkArea.Height
            };

        var bounds = _viewModel.Settings.WindowBounds;
        if (bounds is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            ApplySafeStartupBounds(primaryArea);
            return;
        }

        var normalizedBounds = WindowBoundsNormalizer.Normalize(bounds, workingAreas, out var wasAdjusted);
        var clampedBounds = ClampToAccessibleBounds(normalizedBounds, workingAreas);

        Left = clampedBounds.Left;
        Top = clampedBounds.Top;
        Width = clampedBounds.Width;
        Height = clampedBounds.Height;

        if (wasAdjusted || !AreClose(normalizedBounds, clampedBounds))
        {
            _viewModel.SetWindowBounds(new Rect(
                clampedBounds.Left,
                clampedBounds.Top,
                clampedBounds.Width,
                clampedBounds.Height));
        }
    }

    private void ApplySafeStartupBounds(WindowBounds area)
    {
        var maxWidth = Math.Min(area.Width * StartupMaxWorkAreaRatio, StartupMaxAbsoluteWidth);
        var maxHeight = Math.Min(area.Height * StartupMaxWorkAreaRatio, StartupMaxAbsoluteHeight);
        var width = Math.Clamp(StartupDefaultWidth, MinWidth, Math.Max(MinWidth, maxWidth));
        var height = Math.Clamp(StartupDefaultHeight, MinHeight, Math.Max(MinHeight, maxHeight));

        Width = width;
        Height = height;

        var left = area.Left + Math.Max(StartupSafetyMargin, (area.Width - width) / 2d);
        var top = area.Top + Math.Max(StartupSafetyMargin, (area.Height - height) / 2d);
        Left = left;
        Top = top;
    }

    private static WindowBounds ClampToAccessibleBounds(WindowBounds source, IReadOnlyList<WindowBounds> workingAreas)
    {
        if (workingAreas.Count == 0)
        {
            return source;
        }

        var area = SelectBestArea(source, workingAreas);
        var maxWidth = Math.Max(360d, Math.Min(area.Width * StartupMaxWorkAreaRatio, StartupMaxAbsoluteWidth));
        var maxHeight = Math.Max(250d, Math.Min(area.Height * StartupMaxWorkAreaRatio, StartupMaxAbsoluteHeight));

        var width = Math.Clamp(source.Width, 360d, maxWidth);
        var height = Math.Clamp(source.Height, 250d, maxHeight);

        var minLeft = area.Left + StartupSafetyMargin;
        var minTop = area.Top + StartupSafetyMargin;
        var maxLeft = area.Left + area.Width - width - StartupSafetyMargin;
        var maxTop = area.Top + area.Height - height - StartupSafetyMargin;

        var left = maxLeft < minLeft ? area.Left : Math.Clamp(source.Left, minLeft, maxLeft);
        var top = maxTop < minTop ? area.Top : Math.Clamp(source.Top, minTop, maxTop);

        return new WindowBounds
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height
        };
    }

    private static WindowBounds SelectBestArea(WindowBounds source, IReadOnlyList<WindowBounds> workingAreas)
    {
        var bestArea = workingAreas[0];
        var bestOverlap = -1d;

        foreach (var area in workingAreas)
        {
            var overlap = CalculateOverlapArea(source, area);
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestArea = area;
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

    private static bool HasMeaningfulVisibleArea(WindowBounds source, IReadOnlyList<WindowBounds> workingAreas)
    {
        if (workingAreas.Count == 0)
        {
            return false;
        }

        var overlapArea = CalculateOverlapArea(source, SelectBestArea(source, workingAreas));
        if (overlapArea <= 0d)
        {
            return false;
        }

        var windowArea = Math.Max(1d, source.Width * source.Height);
        var requiredVisibleArea = Math.Min(windowArea * 0.25d, 120d * 120d);
        return overlapArea >= requiredVisibleArea;
    }

    private static bool AreClose(WindowBounds left, WindowBounds right)
    {
        const double epsilon = 0.01d;
        return Math.Abs(left.Left - right.Left) < epsilon &&
               Math.Abs(left.Top - right.Top) < epsilon &&
               Math.Abs(left.Width - right.Width) < epsilon &&
               Math.Abs(left.Height - right.Height) < epsilon;
    }

    private void SaveBoundsThrottled()
    {
        if (!_initialBoundsApplied)
        {
            return;
        }

        if (DateTime.UtcNow - _lastBoundsSaveAt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        SaveWindowBounds();
        _lastBoundsSaveAt = DateTime.UtcNow;
    }

    private void SaveWindowBounds()
    {
        var bounds = new Rect(Left, Top, Width, Height);
        _viewModel.SetWindowBounds(bounds);
    }

    private static IReadOnlyList<WindowBounds> GetWorkingAreas()
    {
        return Forms.Screen.AllScreens
            .OrderByDescending(screen => screen.Primary)
            .Select(screen => new WindowBounds
            {
                Left = screen.WorkingArea.Left,
                Top = screen.WorkingArea.Top,
                Width = screen.WorkingArea.Width,
                Height = screen.WorkingArea.Height
            })
            .ToList();
    }
}
