using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BluetoothBatteryWidget.App;

internal static class WindowPopInAnimator
{
    private const double GenieStartScaleX = 0.42d;
    private const double GenieStartScaleY = 0.24d;
    private const double CenterPopStartScaleX = 0.94d;
    private const double CenterPopStartScaleY = 0.86d;
    private const double OriginOffsetFactor = 0.56d;
    private static readonly TimeSpan OpacityDuration = TimeSpan.FromMilliseconds(360);
    private static readonly TimeSpan MotionDuration = TimeSpan.FromMilliseconds(640);
    private static readonly TimeSpan SettleDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan CloseMotionDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan CloseOpacityDuration = CloseMotionDuration;

    internal static void AttachCentered(Window window)
    {
        window.Opacity = 0d;
        window.Loaded += (_, _) => BeginCentered(window);
    }

    private static void BeginCentered(Window window)
    {
        window.BeginAnimation(UIElement.OpacityProperty, null);
        window.BeginAnimation(Window.TopProperty, null);

        var targetTop = window.Top;
        var canSlideWindow = !double.IsNaN(targetTop) && !double.IsInfinity(targetTop);
        if (canSlideWindow)
        {
            window.Top = targetTop + 10d;
            var topAnimation = new DoubleAnimation(targetTop + 10d, targetTop, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            topAnimation.Completed += (_, _) => window.Top = targetTop;
            window.BeginAnimation(Window.TopProperty, topAnimation, HandoffBehavior.SnapshotAndReplace);
        }

        if (window.Content is FrameworkElement surface)
        {
            BeginSurfaceCenterSettle(surface);
        }

        var opacityAnimation = new DoubleAnimation(0d, 1d, OpacityDuration)
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        opacityAnimation.Completed += (_, _) => window.Opacity = 1d;
        window.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void BeginSurfaceCenterSettle(FrameworkElement surface)
    {
        if (surface.RenderTransform is not TransformGroup group)
        {
            group = new TransformGroup();
            surface.RenderTransform = group;
        }

        var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
        if (scale is null)
        {
            scale = new ScaleTransform(1d, 1d);
            group.Children.Insert(0, scale);
        }

        var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
        if (translate is null)
        {
            translate = new TranslateTransform();
            group.Children.Add(translate);
        }

        surface.RenderTransformOrigin = new System.Windows.Point(0.5d, 0.5d);
        scale.ScaleX = CenterPopStartScaleX;
        scale.ScaleY = CenterPopStartScaleY;
        translate.Y = 8d;

        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(500);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(CenterPopStartScaleX, 1d, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(CenterPopStartScaleY, 1d, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8d, 0d, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);

        scale.ScaleX = 1d;
        scale.ScaleY = 1d;
        translate.Y = 0d;
    }

    internal static void BeginCloseCentered(Window window, Action completed)
    {
        window.BeginAnimation(UIElement.OpacityProperty, null);
        window.BeginAnimation(Window.TopProperty, null);

        if (window.Content is FrameworkElement surface)
        {
            if (surface.RenderTransform is not TransformGroup group)
            {
                group = new TransformGroup();
                surface.RenderTransform = group;
            }

            var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
            if (scale is null)
            {
                scale = new ScaleTransform(1d, 1d);
                group.Children.Insert(0, scale);
            }

            var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (translate is null)
            {
                translate = new TranslateTransform();
                group.Children.Add(translate);
            }

            surface.RenderTransformOrigin = new System.Windows.Point(0.5d, 0.5d);
            var ease = new QuarticEase { EasingMode = EasingMode.EaseIn };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1d, CenterPopStartScaleX, CloseMotionDuration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            }, HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1d, CenterPopStartScaleY, CloseMotionDuration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            }, HandoffBehavior.SnapshotAndReplace);
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0d, 8d, CloseMotionDuration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            }, HandoffBehavior.SnapshotAndReplace);
        }

        var opacityAnimation = new DoubleAnimation(1d, 0d, CloseOpacityDuration)
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd
        };
        opacityAnimation.Completed += (_, _) => completed();
        window.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    internal static void Begin(
        Window window,
        FrameworkElement surface,
        ScaleTransform scale,
        SkewTransform skew,
        TranslateTransform translate,
        System.Windows.Point? originScreenPoint = null)
    {
        window.BeginAnimation(UIElement.OpacityProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        skew.BeginAnimation(SkewTransform.AngleXProperty, null);
        skew.BeginAnimation(SkewTransform.AngleYProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        var useGenieOrigin = CanUseOrigin(window, originScreenPoint);
        var startOffset = useGenieOrigin
            ? CalculateStartOffset(window, originScreenPoint!.Value)
            : new System.Windows.Point(0d, 0d);
        var startScaleX = useGenieOrigin ? GenieStartScaleX : CenterPopStartScaleX;
        var startScaleY = useGenieOrigin ? GenieStartScaleY : CenterPopStartScaleY;
        var startSkewX = useGenieOrigin ? CalculateStartSkewX(startOffset) : 0d;
        var startSkewY = useGenieOrigin ? CalculateStartSkewY(startOffset) : 0d;
        surface.RenderTransformOrigin = CalculateTransformOrigin(window, originScreenPoint, useGenieOrigin);

        window.Opacity = 0d;
        scale.ScaleX = startScaleX;
        scale.ScaleY = startScaleY;
        skew.AngleX = startSkewX;
        skew.AngleY = startSkewY;
        translate.X = startOffset.X;
        translate.Y = startOffset.Y;

        window.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0d, 1d, OpacityDuration)
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
        window.Opacity = 1d;

        var motionEase = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var scaleXAnimation = BuildGenieScaleAnimation(startScaleX, motionEase);
        var scaleYAnimation = BuildGenieScaleAnimation(startScaleY, motionEase);
        var translateXAnimation = BuildGenieDoubleAnimation(startOffset.X, 0d, motionEase);
        var translateYAnimation = BuildGenieDoubleAnimation(startOffset.Y, 0d, motionEase);
        var skewXAnimation = BuildGenieDoubleAnimation(startSkewX, 0d, motionEase);
        var skewYAnimation = BuildGenieDoubleAnimation(startSkewY, 0d, motionEase);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation, HandoffBehavior.SnapshotAndReplace);
        skew.BeginAnimation(SkewTransform.AngleXProperty, skewXAnimation, HandoffBehavior.SnapshotAndReplace);
        skew.BeginAnimation(SkewTransform.AngleYProperty, skewYAnimation, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.XProperty, translateXAnimation, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.YProperty, translateYAnimation, HandoffBehavior.SnapshotAndReplace);
        scale.ScaleX = 1d;
        scale.ScaleY = 1d;
        skew.AngleX = 0d;
        skew.AngleY = 0d;
        translate.X = 0d;
        translate.Y = 0d;
        surface.InvalidateVisual();
    }

    internal static void BeginClose(
        Window window,
        FrameworkElement surface,
        ScaleTransform scale,
        SkewTransform skew,
        TranslateTransform translate,
        System.Windows.Point? originScreenPoint,
        Action completed)
    {
        window.BeginAnimation(UIElement.OpacityProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        skew.BeginAnimation(SkewTransform.AngleXProperty, null);
        skew.BeginAnimation(SkewTransform.AngleYProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        var useGenieOrigin = CanUseOrigin(window, originScreenPoint);
        var endOffset = useGenieOrigin
            ? CalculateStartOffset(window, originScreenPoint!.Value)
            : new System.Windows.Point(0d, 0d);
        var endScaleX = useGenieOrigin ? GenieStartScaleX : CenterPopStartScaleX;
        var endScaleY = useGenieOrigin ? GenieStartScaleY : CenterPopStartScaleY;
        var endSkewX = useGenieOrigin ? CalculateStartSkewX(endOffset) : 0d;
        var endSkewY = useGenieOrigin ? CalculateStartSkewY(endOffset) : 0d;
        surface.RenderTransformOrigin = CalculateTransformOrigin(window, originScreenPoint, useGenieOrigin);

        var ease = new QuarticEase { EasingMode = EasingMode.EaseIn };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1d, endScaleX, CloseMotionDuration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        }, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1d, endScaleY, CloseMotionDuration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        }, HandoffBehavior.SnapshotAndReplace);
        skew.BeginAnimation(SkewTransform.AngleXProperty, new DoubleAnimation(0d, endSkewX, CloseMotionDuration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        }, HandoffBehavior.SnapshotAndReplace);
        skew.BeginAnimation(SkewTransform.AngleYProperty, new DoubleAnimation(0d, endSkewY, CloseMotionDuration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        }, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0d, endOffset.X, CloseMotionDuration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        }, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0d, endOffset.Y, CloseMotionDuration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        }, HandoffBehavior.SnapshotAndReplace);

        var opacityAnimation = new DoubleAnimation(1d, 0d, CloseOpacityDuration)
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd
        };
        opacityAnimation.Completed += (_, _) => completed();
        window.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private static DoubleAnimation BuildGenieScaleAnimation(
        double startValue,
        IEasingFunction easingFunction)
    {
        return new DoubleAnimation(startValue, 1d, SettleDuration)
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = easingFunction
        };
    }

    private static DoubleAnimation BuildGenieDoubleAnimation(
        double startValue,
        double endValue,
        IEasingFunction easingFunction)
    {
        return new DoubleAnimation(startValue, endValue, MotionDuration)
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = easingFunction
        };
    }

    private static bool CanUseOrigin(Window window, System.Windows.Point? originScreenPoint)
    {
        return originScreenPoint is not null &&
            !double.IsNaN(window.Left) &&
            !double.IsNaN(window.Top) &&
            !double.IsNaN(window.Width) &&
            !double.IsNaN(window.Height) &&
            !double.IsInfinity(window.Left) &&
            !double.IsInfinity(window.Top) &&
            window.Width > 0d &&
            window.Height > 0d;
    }

    private static System.Windows.Point CalculateStartOffset(Window window, System.Windows.Point origin)
    {
        var centerX = window.Left + (window.Width / 2d);
        var centerY = window.Top + (window.Height / 2d);
        var maxStartOffsetX = Math.Max(132d, window.Width * 0.34d);
        var maxStartOffsetY = Math.Max(96d, window.Height * 0.30d);
        return new System.Windows.Point(
            Math.Clamp((origin.X - centerX) * OriginOffsetFactor, -maxStartOffsetX, maxStartOffsetX),
            Math.Clamp((origin.Y - centerY) * OriginOffsetFactor, -maxStartOffsetY, maxStartOffsetY));
    }

    private static System.Windows.Point CalculateTransformOrigin(
        Window window,
        System.Windows.Point? originScreenPoint,
        bool useGenieOrigin)
    {
        if (!useGenieOrigin || originScreenPoint is not { } origin)
        {
            return new System.Windows.Point(0.5d, 0.5d);
        }

        return new System.Windows.Point(
            Math.Clamp((origin.X - window.Left) / window.Width, 0.08d, 0.92d),
            Math.Clamp((origin.Y - window.Top) / window.Height, 0.08d, 0.92d));
    }

    private static double CalculateStartSkewX(System.Windows.Point startOffset)
    {
        return Math.Clamp(startOffset.X / 56d, -6.5d, 6.5d);
    }

    private static double CalculateStartSkewY(System.Windows.Point startOffset)
    {
        return Math.Clamp(-startOffset.Y / 82d, -4.5d, 4.5d);
    }
}
