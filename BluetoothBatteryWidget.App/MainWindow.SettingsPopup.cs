using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BluetoothBatteryWidget.Core.Models;
using WpfControls = System.Windows.Controls;
using WpfPopup = System.Windows.Controls.Primitives.Popup;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfThumb = System.Windows.Controls.Primitives.Thumb;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace BluetoothBatteryWidget.App;

public partial class MainWindow
{
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AnimateSettingsGearClick();

        if (IsSettingsPopupOpen())
        {
            CloseSettingsPopup();
            return;
        }

        OpenSettingsPopup();
    }

    private void SettingsPopupCloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseSettingsPopup();
    }

    private void OpenSettingsPopup()
    {
        FinishPopupDrag();
        UpdateSettingsPopupLayout();
        SettingsPopup.IsOpen = true;
        Dispatcher.BeginInvoke(new Action(UpdateSettingsPopupLayout), System.Windows.Threading.DispatcherPriority.Loaded);
        UpdateVersionMenuHeader();
    }

    private void UpdateSettingsPopupLayout()
    {
        if (SettingsPopupChrome is null || SettingsPopupScrollViewer is null)
        {
            return;
        }

        var workArea = GetWorkingAreaForOwnerWindow();
        var plan = _viewModel.UseCenteredSettingsPopup
            ? CreateCenteredSettingsPopupPlan(workArea)
            : SettingsPopupLayoutPlanner.CreateLegacy(workArea);

        SettingsPopup.PlacementTarget = _viewModel.UseCenteredSettingsPopup ? null : GlassCard;
        SettingsPopup.Placement = plan.Placement;
        SettingsPopupChrome.Width = plan.Width;
        SettingsPopupScrollViewer.MaxHeight = plan.MaxHeight;
        SettingsPopup.HorizontalOffset = plan.HorizontalOffset;
        SettingsPopup.VerticalOffset = plan.VerticalOffset;
    }

    private SettingsPopupLayoutPlan CreateCenteredSettingsPopupPlan(WindowBounds workArea)
    {
        var preliminaryPlan = SettingsPopupLayoutPlanner.CreateCentered(workArea, 0d);
        SettingsPopupChrome.Width = preliminaryPlan.Width;
        SettingsPopupScrollViewer.MaxHeight = preliminaryPlan.MaxHeight;
        SettingsPopupChrome.Measure(new System.Windows.Size(preliminaryPlan.Width, preliminaryPlan.MaxHeight));
        return SettingsPopupLayoutPlanner.CreateCentered(workArea, SettingsPopupChrome.DesiredSize.Height);
    }

    private void CloseSettingsPopup()
    {
        FinishPopupDrag();
        _settingsAutoCloseTimer.Stop();
        SettingsPopup.IsOpen = false;
        CloseSettingsAccordions(animate: false);
        ColorCustomPopup.IsOpen = false;
    }

    private bool IsSettingsPopupOpen()
    {
        return SettingsPopup.IsOpen;
    }

    private void ToggleSettingsAccordion(FrameworkElement body, WpfControls.TextBlock arrow)
    {
        var shouldOpen = body.Visibility != Visibility.Visible;
        CloseSettingsAccordions(body, animate: shouldOpen);
        if (shouldOpen)
        {
            OpenSettingsAccordion(body, arrow);
        }
        else
        {
            CloseSettingsAccordion(body, arrow, animate: true);
        }

        QueueSettingsAutoCloseCheck();
    }

    private void CloseSettingsAccordions(FrameworkElement? exceptBody = null, bool animate = false)
    {
        CloseSettingsAccordion(EnvironmentAccordionBody, EnvironmentAccordionArrow, animate, exceptBody);
        CloseSettingsAccordion(CustomizeAccordionBody, CustomizeAccordionArrow, animate, exceptBody);
        CloseSettingsAccordion(LabsAccordionBody, LabsAccordionArrow, animate, exceptBody);
    }

    private void OpenSettingsAccordion(FrameworkElement body, WpfControls.TextBlock arrow)
    {
        var animationToken = NextSettingsAccordionAnimationToken(body);
        body.BeginAnimation(HeightProperty, null);
        body.BeginAnimation(OpacityProperty, null);
        body.Visibility = Visibility.Visible;
        body.Opacity = 0d;
        body.Height = double.NaN;
        body.Measure(new System.Windows.Size(Math.Max(SettingsPopupChrome.ActualWidth - 24d, 320d), double.PositiveInfinity));

        var targetHeight = Math.Max(1d, body.DesiredSize.Height);
        body.Height = 0d;
        var heightAnimation = new DoubleAnimation(0d, targetHeight, TimeSpan.FromMilliseconds(SettingsAccordionAnimationMilliseconds))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        heightAnimation.Completed += (_, _) =>
        {
            if (!IsCurrentSettingsAccordionAnimation(body, animationToken))
            {
                return;
            }

            body.BeginAnimation(HeightProperty, null);
            body.Height = double.NaN;
            body.Opacity = 1d;
        };

        body.BeginAnimation(HeightProperty, heightAnimation);
        body.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        arrow.Text = "⌃";
    }

    private void CloseSettingsAccordion(
        FrameworkElement body,
        WpfControls.TextBlock arrow,
        bool animate,
        FrameworkElement? exceptBody = null)
    {
        if (ReferenceEquals(body, exceptBody))
        {
            return;
        }

        var animationToken = NextSettingsAccordionAnimationToken(body);
        body.BeginAnimation(HeightProperty, null);
        body.BeginAnimation(OpacityProperty, null);
        arrow.Text = "⌄";

        if (body.Visibility != Visibility.Visible || !animate)
        {
            body.Visibility = Visibility.Collapsed;
            body.Height = double.NaN;
            body.Opacity = 1d;
            return;
        }

        body.Measure(new System.Windows.Size(Math.Max(SettingsPopupChrome.ActualWidth - 24d, 320d), double.PositiveInfinity));
        var startHeight = Math.Max(body.ActualHeight, body.DesiredSize.Height);
        if (startHeight <= 1d)
        {
            body.Visibility = Visibility.Collapsed;
            body.Height = double.NaN;
            body.Opacity = 1d;
            return;
        }

        body.Height = startHeight;
        var heightAnimation = new DoubleAnimation(startHeight, 0d, TimeSpan.FromMilliseconds(SettingsAccordionAnimationMilliseconds - 35))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        };
        heightAnimation.Completed += (_, _) =>
        {
            if (!IsCurrentSettingsAccordionAnimation(body, animationToken))
            {
                return;
            }

            body.BeginAnimation(HeightProperty, null);
            body.Visibility = Visibility.Collapsed;
            body.Height = double.NaN;
            body.Opacity = 1d;
        };

        body.BeginAnimation(HeightProperty, heightAnimation);
        body.BeginAnimation(OpacityProperty, new DoubleAnimation(1d, 0d, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        });
    }

    private int NextSettingsAccordionAnimationToken(FrameworkElement body)
    {
        _settingsAccordionAnimationTokens.TryGetValue(body, out var token);
        token++;
        _settingsAccordionAnimationTokens[body] = token;
        return token;
    }

    private bool IsCurrentSettingsAccordionAnimation(FrameworkElement body, int token)
    {
        return _settingsAccordionAnimationTokens.TryGetValue(body, out var currentToken) &&
               currentToken == token;
    }

    private void SettingsPopupArea_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _settingsAutoCloseTimer.Stop();
    }

    private void SettingsPopupArea_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_viewModel.UseCenteredSettingsPopup)
        {
            return;
        }

        QueueSettingsAutoCloseCheck();
    }

    private void QueueSettingsAutoCloseCheck()
    {
        _settingsAutoCloseTimer.Stop();
        _settingsAutoCloseTimer.Start();
    }

    private void SettingsAutoCloseTimer_Tick(object? sender, EventArgs e)
    {
        _settingsAutoCloseTimer.Stop();
        if (_viewModel.UseCenteredSettingsPopup)
        {
            return;
        }

        if (IsMouseOverSettingsSurface() || IsAnySettingsDropDownOpen())
        {
            QueueSettingsAutoCloseCheck();
            return;
        }

        CloseSettingsPopup();
    }

    private bool IsMouseOverSettingsSurface()
    {
        return SettingsPopupChrome.IsMouseOver ||
               ColorPopupChrome.IsMouseOver;
    }

    private bool IsAnySettingsDropDownOpen()
    {
        return ColorPresetComboBox.IsDropDownOpen ||
               GuideSoundComboBox.IsDropDownOpen ||
               LanguageComboBox.IsDropDownOpen ||
               ColorCustomPopup.IsOpen && ColorPopupChrome.IsMouseOver;
    }

    private void AnimateSettingsGearClick()
    {
        var currentAngle = SettingsGearRotateTransform.Angle;
        var animation = new DoubleAnimation
        {
            From = currentAngle,
            To = currentAngle + 180d,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        SettingsGearRotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void PopupChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement chrome)
        {
            return;
        }

        TryBeginPopupDrag(chrome, e, e.OriginalSource as DependencyObject);
    }

    private bool TryBeginPopupDrag(FrameworkElement chrome, MouseButtonEventArgs e, DependencyObject? originalSource)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            IsPopupDragBlocked(originalSource))
        {
            return false;
        }

        var popup = GetPopupForChrome(chrome);
        if (popup is null || !popup.IsOpen)
        {
            return false;
        }

        if (ReferenceEquals(chrome, SettingsPopupChrome) && !_viewModel.UseCenteredSettingsPopup)
        {
            return false;
        }

        _draggingPopup = popup;
        _popupDragChrome = chrome;
        _popupDragStartScreenPoint = chrome.PointToScreen(e.GetPosition(chrome));
        _popupDragStartHorizontalOffset = popup.HorizontalOffset;
        _popupDragStartVerticalOffset = popup.VerticalOffset;
        Mouse.Capture(chrome, CaptureMode.SubTree);
        e.Handled = true;
        return true;
    }

    private void PopupChrome_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingPopup is null ||
            _popupDragChrome is null ||
            !ReferenceEquals(sender, _popupDragChrome))
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FinishPopupDrag();
            return;
        }

        var currentPoint = _popupDragChrome.PointToScreen(e.GetPosition(_popupDragChrome));
        _draggingPopup.HorizontalOffset = _popupDragStartHorizontalOffset + currentPoint.X - _popupDragStartScreenPoint.X;
        _draggingPopup.VerticalOffset = _popupDragStartVerticalOffset + currentPoint.Y - _popupDragStartScreenPoint.Y;
        e.Handled = true;
    }

    private void PopupChrome_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPopup is null ||
            _popupDragChrome is null ||
            !ReferenceEquals(sender, _popupDragChrome))
        {
            return;
        }

        FinishPopupDrag();
        e.Handled = true;
    }

    private void PopupChrome_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_popupDragChrome is not null && ReferenceEquals(sender, _popupDragChrome))
        {
            FinishPopupDrag();
        }
    }

    private void SettingsPopupDragThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (!_viewModel.UseCenteredSettingsPopup || !SettingsPopup.IsOpen)
        {
            return;
        }

        SettingsPopup.HorizontalOffset += e.HorizontalChange;
        SettingsPopup.VerticalOffset += e.VerticalChange;
        e.Handled = true;
    }

    private void ColorPopupDragThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        ColorCustomPopup.HorizontalOffset += e.HorizontalChange;
        ColorCustomPopup.VerticalOffset += e.VerticalChange;
        e.Handled = true;
    }

    private static bool IsPopupDragBlocked(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfControls.Button ||
                source is WpfToggleButton ||
                source is WpfControls.TextBox ||
                source is WpfControls.ComboBox ||
                source is WpfControls.Slider ||
                source is WpfScrollBar ||
                source is WpfControls.ListBoxItem ||
                source is WpfThumb ||
                source is FrameworkElement frameworkElement && IsNamedPopupInteractiveElement(frameworkElement.Name))
            {
                return true;
            }

            source = GetDependencyParent(source);
        }

        return false;
    }

    private FrameworkElement? GetPopupChromeFromSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, SettingsPopupChrome))
            {
                return SettingsPopupChrome;
            }

            if (ReferenceEquals(source, ColorPopupChrome))
            {
                return ColorPopupChrome;
            }

            source = GetDependencyParent(source);
        }

        return null;
    }

    private WpfPopup? GetPopupForChrome(FrameworkElement chrome)
    {
        if (ReferenceEquals(chrome, SettingsPopupChrome))
        {
            return SettingsPopup;
        }

        if (ReferenceEquals(chrome, ColorPopupChrome))
        {
            return ColorCustomPopup;
        }

        return null;
    }

    private void FinishPopupDrag()
    {
        var chrome = _popupDragChrome;
        _draggingPopup = null;
        _popupDragChrome = null;

        if (chrome?.IsMouseCaptured == true)
        {
            chrome.ReleaseMouseCapture();
        }
    }

    private static DependencyObject? GetDependencyParent(DependencyObject source)
    {
        try
        {
            return System.Windows.Media.VisualTreeHelper.GetParent(source) ??
                   LogicalTreeHelper.GetParent(source);
        }
        catch (InvalidOperationException)
        {
            return LogicalTreeHelper.GetParent(source);
        }
    }

    private static bool IsNamedPopupInteractiveElement(string name)
    {
        return name is "PaletteSurface"
            or "PaletteCursor"
            or "SelectedColorPreviewBorder"
            or "SelectedColorHexText"
            or "PrimaryTextColorButton"
            or "SecondaryTextColorButton"
            or "BatteryTextColorButton"
            or "GlassSurfaceColorButton"
            or "CardTintColorButton"
            or "CardBorderColorButton"
            or "TrackColorButton"
            or "PanelColorButton";
    }
}
