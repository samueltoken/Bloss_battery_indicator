using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BluetoothBatteryWidget.App.Services;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace BluetoothBatteryWidget.App;

internal sealed class LabsWindow : Window
{
    private const string ExperienceName = "Red Shift";
    private const double DesignWidth = 1600;
    private const double DesignHeight = 900;
    private static readonly TimeSpan OuterSpaceDuration = TimeSpan.FromSeconds(11);

    private readonly BatteryGuideChimePlayer _audioPlayer = new(BatteryGuideChimeAudio.LoadWave());
    private readonly DispatcherTimer _closeTimer = new() { Interval = OuterSpaceDuration };
    private readonly HyperspacePortalView _portalView = new(OuterSpaceDuration);
    private bool _closing;

    public LabsWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        Background = WpfBrushes.Black;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = true;
        UseLayoutRounding = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = BuildContent();

        Loaded += (_, _) => StartOuterSpace();
        Closed += (_, _) =>
        {
            _closeTimer.Stop();
            _portalView.Stop();
            _audioPlayer.Dispose();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                BeginOutro();
            }
        };
        _closeTimer.Tick += (_, _) => BeginOutro();
    }

    private FrameworkElement BuildContent()
    {
        return new Grid
        {
            Background = WpfBrushes.Black,
            ClipToBounds = true,
            Children =
            {
                _portalView
            }
        };
    }

    private void StartOuterSpace()
    {
        ApplyFullScreenBounds();
        Opacity = 0;
        Activate();
        Focus();
        _portalView.Start();
        _audioPlayer.PlayFromStart(BatteryGuideSoundCatalog.OuterSpaceSound);
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        _closeTimer.Start();
    }

    private void ApplyFullScreenBounds()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void BeginOutro()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _closeTimer.Stop();
        _portalView.Stop();
        _audioPlayer.Stop();
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(560))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private sealed class HyperspacePortalView : FrameworkElement
    {
        private const int HyperspaceStreakCount = 132;
        private const int PortalRingCount = 22;
        private const int SpiralRibbonCount = 7;
        private const int ShockwaveCount = 4;
        private const double CenterPullRadius = 44;
        private const double TargetFrameMilliseconds = 16.0;

        private static readonly WpfColor[] RedHotPalette =
        [
            WpfColor.FromRgb(255, 42, 32),
            WpfColor.FromRgb(196, 0, 18),
            WpfColor.FromRgb(255, 100, 36),
            WpfColor.FromRgb(255, 190, 76),
            WpfColor.FromRgb(120, 0, 18)
        ];

        private readonly TimeSpan _duration;
        private readonly Stopwatch _clock = new();
        private readonly StreakSeed[] _streaks;
        private readonly WpfBrush _spaceBrush = CreateSpaceBrush();
        private readonly WpfBrush _nebulaBrush = CreateNebulaBrush();
        private readonly WpfBrush _vignetteBrush = CreateVignetteBrush();
        private readonly WpfBrush _coreBrush = CreateCoreBrush();
        private TimeSpan _lastFrame = TimeSpan.MinValue;
        private bool _running;

        public HyperspacePortalView(TimeSpan duration)
        {
            _duration = duration;
            _streaks = BuildStreakSeeds();
            Focusable = false;
            IsHitTestVisible = false;
            ClipToBounds = true;
            SnapsToDevicePixels = false;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _lastFrame = TimeSpan.MinValue;
            _clock.Restart();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            InvalidateVisual();
        }

        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _clock.Stop();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 1 || bounds.Height <= 1)
            {
                return;
            }

            var seconds = _clock.Elapsed.TotalSeconds;
            var progress = Math.Clamp(seconds / _duration.TotalSeconds, 0, 1);
            var heat = GlobalHeat(progress);
            var travel = seconds * (0.42 + heat * 1.55) + heat * heat * 9.2;
            var scale = Math.Min(bounds.Width / DesignWidth, bounds.Height / DesignHeight);
            var center = new WpfPoint(bounds.Width * 0.5, bounds.Height * 0.5);
            var maxRadius = Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) * (0.57 + heat * 0.09);
            var coreRadius = CenterPullRadius * Math.Max(0.85, scale) * (0.88 + heat * 0.92);

            DrawDeepSpace(drawingContext, bounds, heat);
            DrawThermalShockwaves(drawingContext, center, maxRadius, coreRadius, scale, travel, heat);
            DrawPortalRings(drawingContext, center, maxRadius, coreRadius, scale, seconds, travel, heat);
            DrawSpiralRibbons(drawingContext, center, maxRadius, coreRadius, scale, travel, heat);
            DrawHyperspaceStreaks(drawingContext, center, maxRadius, coreRadius, scale, travel, heat);
            DrawPortalCore(drawingContext, center, coreRadius, maxRadius, scale, progress, heat);
            DrawDepthAndHud(drawingContext, bounds, progress, heat);
            DrawFinalBlackout(drawingContext, bounds, progress);
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (!_running)
            {
                return;
            }

            var now = _clock.Elapsed;
            if (_lastFrame != TimeSpan.MinValue &&
                (now - _lastFrame).TotalMilliseconds < TargetFrameMilliseconds)
            {
                return;
            }

            _lastFrame = now;
            InvalidateVisual();
        }

        private void DrawDeepSpace(DrawingContext drawingContext, Rect bounds, double heat)
        {
            drawingContext.DrawRectangle(_spaceBrush, null, bounds);
            drawingContext.DrawRectangle(_nebulaBrush, null, bounds);
            drawingContext.DrawRectangle(
                CreateSolidBrush(WpfColor.FromArgb((byte)(18 + heat * 72), 86, 0, 5)),
                null,
                bounds);
        }

        private void DrawPortalRings(
            DrawingContext drawingContext,
            WpfPoint center,
            double maxRadius,
            double coreRadius,
            double scale,
            double seconds,
            double travel,
            double heat)
        {
            for (var index = 0; index < PortalRingCount; index++)
            {
                var phase = PositiveModulo(travel * 0.52 + index / (double)PortalRingCount, 1.0);
                var eased = EaseInCubic(phase);
                var radius = Lerp(maxRadius * (0.96 + heat * 0.08), coreRadius * (1.42 - heat * 0.2), eased);
                var flatten = 0.43 + 0.11 * Math.Sin(travel * 0.55 + index * 0.73);
                var opacity = FadeInOut(phase, 0.08, 0.7);
                if (opacity <= 0.02)
                {
                    continue;
                }

                var color = HeatColor(index, heat);
                var thickness = (1.05 + (1 - eased) * (2.0 + heat * 3.4)) * Math.Max(0.9, scale);
                var pen = CreatePen(color, opacity * (0.4 + heat * 0.5), thickness);
                drawingContext.PushTransform(new RotateTransform(travel * (16 + heat * 22) + index * 19, center.X, center.Y));
                drawingContext.DrawEllipse(null, pen, center, radius, radius * flatten);
                drawingContext.Pop();
            }
        }

        private void DrawSpiralRibbons(
            DrawingContext drawingContext,
            WpfPoint center,
            double maxRadius,
            double coreRadius,
            double scale,
            double travel,
            double heat)
        {
            for (var band = 0; band < SpiralRibbonCount; band++)
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    for (var step = 0; step <= 48; step++)
                    {
                        var p = step / 48.0;
                        var radius = Lerp(maxRadius * (0.78 + band * 0.026 + heat * 0.04), coreRadius * (1.02 + heat * 0.28), EaseInCubic(p));
                        var angle = travel * (0.56 + heat * 0.28 + band * 0.028) + band * 1.18 + p * (6.2 + heat * 2.6 + band * 0.24);
                        var point = PointFromPolar(center, angle, radius, 0.5 + heat * 0.03);
                        if (step == 0)
                        {
                            context.BeginFigure(point, false, false);
                        }
                        else
                        {
                            context.LineTo(point, true, false);
                        }
                    }
                }

                geometry.Freeze();
                var color = HeatColor(band + 1, heat);
                drawingContext.DrawGeometry(null, CreatePen(color, 0.1 + heat * 0.32, (1.05 + band * 0.22 + heat * 1.15) * scale), geometry);
            }
        }

        private void DrawHyperspaceStreaks(
            DrawingContext drawingContext,
            WpfPoint center,
            double maxRadius,
            double coreRadius,
            double scale,
            double travel,
            double heat)
        {
            foreach (var streak in _streaks)
            {
                var phase = PositiveModulo(travel * streak.Speed + streak.Offset + heat * streak.HeatOffset, 1.0);
                var headProgress = EaseInCubic(phase);
                var tailProgress = EaseInCubic(Math.Max(phase - streak.Length * (0.72 + heat * 0.48), 0));
                var outerRadius = maxRadius * (streak.OuterScale + heat * 0.16);
                var headRadius = Lerp(outerRadius, coreRadius * (0.9 - heat * 0.1), headProgress);
                var tailRadius = Lerp(outerRadius * (1.02 + heat * 0.1), coreRadius * (3.4 + heat * 1.6), tailProgress);
                var angleDrift = travel * streak.Drift + phase * streak.Curl * (0.85 + heat * 1.35);
                var flatten = streak.Flatten - heat * 0.06;
                var head = PointFromPolar(center, streak.Angle + angleDrift, headRadius, flatten);
                var tail = PointFromPolar(center, streak.Angle + angleDrift - streak.Curl * 0.22, tailRadius, flatten);
                var opacity = FadeInOut(phase, 0.025, 0.86) * streak.Opacity * (0.72 + heat * 0.78);
                if (opacity <= 0.025)
                {
                    continue;
                }

                var thickness = (0.55 + (1 - headProgress) * streak.Thickness * (0.85 + heat * 1.45)) * Math.Max(0.9, scale);
                drawingContext.DrawLine(CreatePen(HeatColor(streak.ColorIndex, heat), opacity, thickness), tail, head);
            }
        }

        private void DrawThermalShockwaves(
            DrawingContext drawingContext,
            WpfPoint center,
            double maxRadius,
            double coreRadius,
            double scale,
            double travel,
            double heat)
        {
            for (var index = 0; index < ShockwaveCount; index++)
            {
                var phase = PositiveModulo(travel * (0.18 + index * 0.025) + index * 0.23, 1);
                var radius = Lerp(coreRadius * (1.7 + heat), maxRadius * (0.55 + heat * 0.2), phase);
                var opacity = (1 - phase) * (0.05 + heat * 0.18);
                drawingContext.PushTransform(new RotateTransform(travel * (9 + index * 3), center.X, center.Y));
                drawingContext.DrawEllipse(
                    null,
                    CreatePen(HeatColor(index + 3, heat), opacity, (0.9 + heat * 1.5) * scale),
                    center,
                    radius,
                    radius * (0.38 + index * 0.025));
                drawingContext.Pop();
            }
        }

        private void DrawPortalCore(
            DrawingContext drawingContext,
            WpfPoint center,
            double coreRadius,
            double maxRadius,
            double scale,
            double progress,
            double heat)
        {
            var engulf = EndEngulf(progress);
            var blackRadius = Lerp(
                coreRadius * (1.05 + heat * 1.42),
                maxRadius * 1.34,
                engulf);
            var coronaRadius = blackRadius + (18 + heat * 64) * Math.Max(0.8, scale) * (1 - engulf * 0.72);

            drawingContext.DrawEllipse(
                null,
                CreatePen(WpfColor.FromRgb(255, 70, 24), (0.28 + heat * 0.44) * (1 - engulf * 0.82), (2.0 + heat * 3.4) * scale),
                center,
                coronaRadius,
                coronaRadius);
            drawingContext.DrawEllipse(
                null,
                CreatePen(WpfColor.FromRgb(255, 198, 80), (0.14 + heat * 0.28) * (1 - engulf * 0.92), (1.0 + heat * 2.1) * scale),
                center,
                coronaRadius * 0.74,
                coronaRadius * 0.74);
            drawingContext.DrawEllipse(_coreBrush, null, center, blackRadius, blackRadius);
        }

        private void DrawDepthAndHud(DrawingContext drawingContext, Rect bounds, double progress, double heat)
        {
            drawingContext.DrawRectangle(_vignetteBrush, null, bounds);

            var railWidth = Math.Min(360, bounds.Width * 0.22);
            var railHeight = 4.0;
            var rail = new Rect(bounds.Width - railWidth - 34, bounds.Height - 38, railWidth, railHeight);
            drawingContext.DrawRoundedRectangle(CreateSolidBrush(WpfColor.FromArgb(42, 255, 255, 255)), null, rail, railHeight / 2, railHeight / 2);
            drawingContext.DrawRoundedRectangle(
                CreateSolidBrush(WpfColor.FromArgb((byte)(145 + heat * 90), 255, 64, 32)),
                null,
                new Rect(rail.X, rail.Y, rail.Width * progress, rail.Height),
                railHeight / 2,
                railHeight / 2);

            var dpi = VisualTreeHelper.GetDpi(this);
            var text = new FormattedText(
                ExperienceName.ToUpperInvariant(),
                CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                13,
                CreateSolidBrush(WpfColor.FromArgb(130, 255, 176, 142)),
                dpi.PixelsPerDip);
            drawingContext.DrawText(text, new WpfPoint(34, 28));
        }

        private static void DrawFinalBlackout(DrawingContext drawingContext, Rect bounds, double progress)
        {
            var opacity = EndEngulf(progress);
            if (opacity <= 0)
            {
                return;
            }

            drawingContext.DrawRectangle(
                CreateSolidBrush(WpfColor.FromArgb((byte)Math.Clamp(opacity * 255, 0, 255), 0, 0, 0)),
                null,
                bounds);
        }

        private static StreakSeed[] BuildStreakSeeds()
        {
            var streaks = new StreakSeed[HyperspaceStreakCount];
            for (var index = 0; index < streaks.Length; index++)
            {
                var angle = index * 2.399963229728653 + (index % 7) * 0.043;
                var outerScale = 0.62 + Hash01(index, 3) * 0.48;
                var speed = 0.58 + Hash01(index, 5) * 0.9;
                var offset = Hash01(index, 7);
                var length = 0.15 + Hash01(index, 11) * 0.22;
                var curl = (Hash01(index, 13) - 0.5) * 0.68;
                var drift = (Hash01(index, 17) - 0.5) * 0.05;
                var flatten = 0.52 + Hash01(index, 19) * 0.18;
                var opacity = 0.38 + Hash01(index, 23) * 0.44;
                var thickness = 1.1 + Hash01(index, 29) * 2.1;
                var heatOffset = Hash01(index, 31) * 0.37;
                streaks[index] = new StreakSeed(
                    angle,
                    outerScale,
                    speed,
                    offset,
                    length,
                    curl,
                    drift,
                    flatten,
                    opacity,
                    thickness,
                    heatOffset,
                    index % RedHotPalette.Length);
            }

            return streaks;
        }

        private static WpfBrush CreateSpaceBrush()
        {
            var brush = new RadialGradientBrush
            {
                RadiusX = 0.72,
                RadiusY = 0.86,
                Center = new WpfPoint(0.5, 0.5),
                GradientOrigin = new WpfPoint(0.5, 0.48)
            };
            brush.GradientStops.Add(new GradientStop(WpfColor.FromRgb(14, 2, 4), 0));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromRgb(4, 0, 2), 0.58));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0, 0, 0), 1));
            brush.Freeze();
            return brush;
        }

        private static WpfBrush CreateNebulaBrush()
        {
            var brush = new RadialGradientBrush
            {
                RadiusX = 0.42,
                RadiusY = 0.58,
                Center = new WpfPoint(0.5, 0.52),
                GradientOrigin = new WpfPoint(0.5, 0.5)
            };
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(98, 74, 4, 4), 0));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(46, 38, 0, 0), 0.54));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 0, 0, 0), 1));
            brush.Freeze();
            return brush;
        }

        private static WpfBrush CreateVignetteBrush()
        {
            var brush = new RadialGradientBrush
            {
                RadiusX = 0.78,
                RadiusY = 0.92,
                Center = new WpfPoint(0.5, 0.5),
                GradientOrigin = new WpfPoint(0.5, 0.5)
            };
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 0, 0, 0), 0));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(54, 0, 0, 0), 0.48));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(238, 0, 0, 0), 1));
            brush.Freeze();
            return brush;
        }

        private static WpfBrush CreateCoreBrush()
        {
            var brush = new RadialGradientBrush
            {
                RadiusX = 0.52,
                RadiusY = 0.52,
                Center = new WpfPoint(0.5, 0.5),
                GradientOrigin = new WpfPoint(0.5, 0.5)
            };
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(255, 0, 0, 0), 0));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(238, 14, 0, 0), 0.48));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 255, 56, 18), 1));
            brush.Freeze();
            return brush;
        }

        private static WpfColor HeatColor(int index, double heat)
        {
            var baseColor = RedHotPalette[Math.Abs(index) % RedHotPalette.Length];
            var target = heat > 0.72
                ? WpfColor.FromRgb(255, 226, 138)
                : WpfColor.FromRgb(255, 78, 30);
            var amount = Math.Clamp(heat * 0.62, 0, 0.74);
            return WpfColor.FromRgb(
                (byte)Math.Clamp(Lerp(baseColor.R, target.R, amount), 0, 255),
                (byte)Math.Clamp(Lerp(baseColor.G, target.G, amount), 0, 255),
                (byte)Math.Clamp(Lerp(baseColor.B, target.B, amount), 0, 255));
        }

        private static SolidColorBrush CreateSolidBrush(WpfColor color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static WpfPen CreatePen(WpfColor color, double opacity, double thickness)
        {
            var alpha = (byte)Math.Clamp(opacity * 255, 0, 255);
            var brush = new SolidColorBrush(WpfColor.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            var pen = new WpfPen(brush, Math.Max(0.45, thickness))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }

        private static WpfPoint PointFromPolar(WpfPoint center, double angle, double radius, double flatten)
        {
            return new WpfPoint(
                center.X + Math.Cos(angle) * radius,
                center.Y + Math.Sin(angle) * radius * flatten);
        }

        private static double Lerp(double start, double end, double amount)
        {
            return start + (end - start) * amount;
        }

        private static double EaseInCubic(double value)
        {
            return value * value * value;
        }

        private static double GlobalHeat(double progress)
        {
            var smooth = progress * progress * (3 - 2 * progress);
            return Math.Clamp(smooth * 0.9 + progress * progress * 0.28, 0, 1);
        }

        private static double EndEngulf(double progress)
        {
            var amount = Math.Clamp((progress - 0.72) / 0.28, 0, 1);
            return amount * amount * amount;
        }

        private static double FadeInOut(double value, double fadeIn, double fadeOutStart)
        {
            if (value < fadeIn)
            {
                return Math.Clamp(value / fadeIn, 0, 1);
            }

            if (value > fadeOutStart)
            {
                return Math.Clamp((1 - value) / (1 - fadeOutStart), 0, 1);
            }

            return 1;
        }

        private static double PositiveModulo(double value, double modulo)
        {
            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static double Hash01(int index, int salt)
        {
            var value = Math.Sin((index + 1) * (salt * 12.9898 + 78.233)) * 43758.5453;
            return value - Math.Floor(value);
        }

        private readonly record struct StreakSeed(
            double Angle,
            double OuterScale,
            double Speed,
            double Offset,
            double Length,
            double Curl,
            double Drift,
            double Flatten,
            double Opacity,
            double Thickness,
            double HeatOffset,
            int ColorIndex);
    }
}
