using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BluetoothBatteryWidget.App.Services;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace BluetoothBatteryWidget.App;

internal sealed class LabsWindow : Window
{
    private static readonly TimeSpan CodeCityDuration = TimeSpan.FromSeconds(55);

    private readonly BatteryGuideChimePlayer _audioPlayer = new(BatteryGuideChimeAudio.LoadWave());
    private readonly DispatcherTimer _closeTimer = new() { Interval = CodeCityDuration };
    private readonly CodeCityView _codeCityView = new(CodeCityDuration);
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

        Loaded += (_, _) => StartExperience();
        Closed += (_, _) =>
        {
            _closeTimer.Stop();
            _codeCityView.Stop();
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
                _codeCityView
            }
        };
    }

    private void StartExperience()
    {
        ApplyFullScreenBounds();
        Opacity = 0;
        Activate();
        Focus();
        _codeCityView.Start();
        _audioPlayer.PlayFromStart(BatteryGuideSoundCatalog.Version107DigitalCitySound);
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
        _codeCityView.Stop();
        _audioPlayer.Stop();
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(560))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private sealed class CodeCityView : FrameworkElement
    {
        private const int StarCount = 120;
        private const int CodeMarkCount = 132;
        private const int SpeedLineCount = 92;
        private const double TargetFrameMilliseconds = 16.0;

        private static readonly WpfColor TunnelGreen = WpfColor.FromRgb(47, 255, 118);
        private static readonly WpfColor TunnelCyan = WpfColor.FromRgb(95, 241, 255);
        private static readonly WpfColor SoftWhite = WpfColor.FromRgb(235, 255, 246);
        private static readonly WpfColor CityTeal = WpfColor.FromRgb(70, 232, 214);
        private static readonly WpfColor CityAmber = WpfColor.FromRgb(255, 180, 86);
        private static readonly WpfColor RoadBlue = WpfColor.FromRgb(42, 122, 154);

        private readonly TimeSpan _duration;
        private readonly Stopwatch _clock = new();
        private readonly StarSeed[] _stars;
        private readonly CodeMarkSeed[] _codeMarks;
        private readonly SpeedLineSeed[] _speedLines;
        private TimeSpan _lastFrame = TimeSpan.MinValue;
        private bool _running;

        public CodeCityView(TimeSpan duration)
        {
            _duration = duration;
            _stars = BuildStars();
            _codeMarks = BuildCodeMarks();
            _speedLines = BuildSpeedLines();
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

            var seconds = Math.Min(_clock.Elapsed.TotalSeconds, _duration.TotalSeconds);
            var progress = Math.Clamp(seconds / _duration.TotalSeconds, 0, 1);
            var w = bounds.Width;
            var h = bounds.Height;
            var center = new WpfPoint(w * 0.5, h * 0.5);

            DrawBaseBackground(drawingContext, bounds, seconds, progress);
            DrawStarfield(drawingContext, w, h, seconds, progress);
            DrawCityDrive(drawingContext, bounds, seconds, progress);
            DrawTunnel(drawingContext, w, h, seconds, progress);
            DrawSeedAndScan(drawingContext, center, w, h, seconds);
            DrawSpeedLines(drawingContext, center, w, h, seconds, progress);
            DrawDepthOverlay(drawingContext, bounds, progress);
            DrawProgressRail(drawingContext, bounds, progress);
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

        private void DrawBaseBackground(DrawingContext dc, Rect bounds, double seconds, double progress)
        {
            var city = SmoothSeconds(31.8, 38.0, seconds);
            var scan = SmoothSeconds(1.2, 6.2, seconds) * (1 - SmoothSeconds(5.1, 7.0, seconds));
            dc.DrawRectangle(CreateSolidBrush(WpfColor.FromRgb(0, 3, 5)), null, bounds);

            var skyBrush = new LinearGradientBrush
            {
                StartPoint = new WpfPoint(0.5, 0),
                EndPoint = new WpfPoint(0.5, 1)
            };
            skyBrush.GradientStops.Add(new GradientStop(Mix(WpfColor.FromRgb(2, 7, 10), WpfColor.FromRgb(7, 31, 36), city), 0));
            skyBrush.GradientStops.Add(new GradientStop(Mix(WpfColor.FromRgb(1, 7, 8), WpfColor.FromRgb(8, 50, 56), city), 0.46));
            skyBrush.GradientStops.Add(new GradientStop(Mix(WpfColor.FromRgb(0, 2, 3), WpfColor.FromRgb(2, 9, 12), city), 1));
            dc.DrawRectangle(skyBrush, null, bounds);

            var glow = new RadialGradientBrush
            {
                Center = new WpfPoint(0.5, 0.48),
                GradientOrigin = new WpfPoint(0.5, 0.48),
                RadiusX = 0.66,
                RadiusY = 0.72
            };
            glow.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(34 + scan * 92 + city * 32), 37, 255, 143), 0));
            glow.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(10 + scan * 50 + city * 42), 0, 185, 190), 0.42));
            glow.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 0, 0, 0), 1));
            dc.DrawRectangle(glow, null, bounds);

            if (city > 0)
            {
                var fog = new LinearGradientBrush
                {
                    StartPoint = new WpfPoint(0.5, 0.33),
                    EndPoint = new WpfPoint(0.5, 1)
                };
                fog.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 0, 0, 0), 0));
                fog.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(city * 55), 53, 118, 123), 0.62));
                fog.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(city * 18), 255, 170, 82), 1));
                dc.DrawRectangle(fog, null, bounds);
            }
        }

        private void DrawStarfield(DrawingContext dc, double w, double h, double seconds, double progress)
        {
            var alpha = SmoothSeconds(8.0, 18.0, seconds) * (1 - SmoothSeconds(52.0, 55.0, seconds));
            if (alpha <= 0)
            {
                return;
            }

            var vpY = h * (0.49 - SmoothSeconds(30.0, 39.0, seconds) * 0.08);
            var speedRamp = SmoothSeconds(10.0, 30.8, seconds);
            var travel = Math.Max(0, seconds - 7.4) * (0.55 + speedRamp * 2.3);

            foreach (var star in _stars)
            {
                var z = 0.75 + PositiveModulo(star.Z - travel * (0.18 + star.Hue * 0.08), 11.5);
                var point = Project(star.X, star.Y, z, w, h, vpY);
                if (!IsLooseInside(point, w, h))
                {
                    continue;
                }

                var size = Math.Clamp(star.Size * (4.0 / z), 0.55, 3.8);
                var opacity = alpha * star.Opacity * Math.Clamp((11.8 - z) / 8.8, 0.12, 1);
                dc.DrawEllipse(CreateSolidBrush(WpfColor.FromArgb(Alpha(opacity * 185), 155, 252, 220)), null, point, size, size);
            }
        }

        private void DrawTunnel(DrawingContext dc, double w, double h, double seconds, double progress)
        {
            var alpha = SmoothSeconds(3.8, 8.0, seconds) * (1 - SmoothSeconds(31.0, 33.4, seconds));
            if (alpha <= 0.01)
            {
                return;
            }

            var vpY = h * (0.5 - SmoothSeconds(23.0, 31.0, seconds) * 0.035);
            var speedRamp = SmoothSeconds(9.0, 30.8, seconds);
            var finalPull = SmoothSeconds(24.0, 31.2, seconds);
            var travel = Math.Max(0, seconds - 5.2) * (0.95 + speedRamp * 2.7 + finalPull * 1.55);

            DrawTunnelLongLines(dc, w, h, vpY, travel, alpha, speedRamp);
            DrawTunnelRibs(dc, w, h, vpY, travel, alpha, speedRamp);
            DrawCodeMarks(dc, w, h, vpY, travel, alpha, speedRamp);
        }

        private void DrawTunnelLongLines(
            DrawingContext dc,
            double w,
            double h,
            double vpY,
            double travel,
            double alpha,
            double speedRamp)
        {
            var penDim = CreatePen(WpfColor.FromRgb(38, 160, 104), alpha * 0.26, 0.75 + speedRamp * 0.65);
            var penBright = CreatePen(TunnelCyan, alpha * 0.18, 1.1 + speedRamp * 0.9);

            for (var side = -1; side <= 1; side += 2)
            {
                for (var index = 0; index < 9; index++)
                {
                    var x = side * (0.42 + index * 0.145);
                    var y = -0.58 + index * 0.11;
                    var near = Project(x * 2.1, y, 0.72, w, h, vpY);
                    var far = Project(x * 0.28, y * 0.28, 12.6, w, h, vpY);
                    dc.DrawLine(index % 3 == 0 ? penBright : penDim, far, near);
                }
            }

            for (var index = 0; index < 7; index++)
            {
                var y = -0.72 + index * 0.24;
                var leftNear = Project(-1.75, y, 0.72, w, h, vpY);
                var leftFar = Project(-0.24, y * 0.2, 12.6, w, h, vpY);
                var rightNear = Project(1.75, y, 0.72, w, h, vpY);
                var rightFar = Project(0.24, y * 0.2, 12.6, w, h, vpY);
                dc.DrawLine(penDim, leftFar, leftNear);
                dc.DrawLine(penDim, rightFar, rightNear);
            }
        }

        private void DrawTunnelRibs(
            DrawingContext dc,
            double w,
            double h,
            double vpY,
            double travel,
            double alpha,
            double speedRamp)
        {
            for (var index = 0; index < 22; index++)
            {
                var z = 0.7 + PositiveModulo(index * 0.72 - travel * 0.92, 14.2);
                var depth = Math.Clamp((14.6 - z) / 14.0, 0, 1);
                var opacity = alpha * depth * depth * (0.12 + speedRamp * 0.18);
                if (opacity <= 0.012)
                {
                    continue;
                }

                var leftTop = Project(-1.72, -0.74, z, w, h, vpY);
                var rightTop = Project(1.72, -0.74, z, w, h, vpY);
                var leftBottom = Project(-1.72, 0.74, z, w, h, vpY);
                var rightBottom = Project(1.72, 0.74, z, w, h, vpY);
                var pen = CreatePen(index % 4 == 0 ? TunnelCyan : TunnelGreen, opacity, 0.8 + depth * 2.0);

                dc.DrawLine(pen, leftTop, rightTop);
                dc.DrawLine(pen, rightTop, rightBottom);
                dc.DrawLine(pen, rightBottom, leftBottom);
                dc.DrawLine(pen, leftBottom, leftTop);
            }
        }

        private void DrawCodeMarks(
            DrawingContext dc,
            double w,
            double h,
            double vpY,
            double travel,
            double alpha,
            double speedRamp)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            foreach (var mark in _codeMarks)
            {
                var z = 0.82 + PositiveModulo(mark.Z - travel * mark.Speed, 13.4);
                var x = mark.Side * (0.92 + mark.A * 0.62);
                var y = -0.64 + mark.B * 1.24;
                var point = Project(x, y, z, w, h, vpY);
                if (!IsLooseInside(point, w, h))
                {
                    continue;
                }

                var depth = Math.Clamp((13.8 - z) / 13.0, 0, 1);
                var fontSize = Math.Clamp(24.0 / z + 6.5, 7.0, 20.0);
                var opacity = alpha * mark.Brightness * depth * (0.32 + speedRamp * 0.34);
                if (opacity <= 0.02)
                {
                    continue;
                }

                var text = new FormattedText(
                    mark.Glyph,
                    CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Consolas"),
                    fontSize,
                    CreateSolidBrush(WpfColor.FromArgb(Alpha(opacity * 255), 78, 255, 134)),
                    dpi.PixelsPerDip);
                dc.DrawText(text, point);
            }
        }

        private void DrawSeedAndScan(DrawingContext dc, WpfPoint center, double w, double h, double seconds)
        {
            var seedIn = SmoothSeconds(0.15, 1.0, seconds);
            var scanOut = 1 - SmoothSeconds(5.05, 6.2, seconds);
            var visible = seedIn * scanOut;
            if (visible <= 0.01)
            {
                return;
            }

            var maxRadius = Math.Sqrt(w * w + h * h) * 0.62;
            var scan = SmoothSeconds(1.0, 5.0, seconds);
            var ringRadius = Math.Max(6, maxRadius * EaseOutCubic(scan));
            var ringOpacity = visible * (1 - scan * 0.72);

            dc.DrawEllipse(
                null,
                CreatePen(SoftWhite, ringOpacity * 0.42, 1.2 + scan * 3.4),
                center,
                ringRadius,
                ringRadius * 0.62);

            for (var index = 0; index < 42; index++)
            {
                var angle = index * Math.PI * 2 / 42.0;
                var inner = Math.Max(8, ringRadius * (0.05 + Hash01(index, 3) * 0.15));
                var outer = ringRadius * (0.72 + Hash01(index, 7) * 0.32);
                var start = new WpfPoint(center.X + Math.Cos(angle) * inner, center.Y + Math.Sin(angle) * inner * 0.72);
                var end = new WpfPoint(center.X + Math.Cos(angle) * outer, center.Y + Math.Sin(angle) * outer * 0.72);
                var color = index % 4 == 0 ? TunnelCyan : TunnelGreen;
                dc.DrawLine(CreatePen(color, ringOpacity * (0.12 + Hash01(index, 11) * 0.22), 0.8 + scan * 1.8), start, end);
            }

            var dotRadius = 3.0 + seedIn * 12.0 + scan * 6.0;
            var dotOpacity = visible * (1 - scan * 0.62);
            dc.DrawEllipse(CreateSolidBrush(WpfColor.FromArgb(Alpha(dotOpacity * 225), 232, 255, 245)), null, center, dotRadius, dotRadius);
            dc.DrawEllipse(
                null,
                CreatePen(TunnelCyan, dotOpacity * 0.58, 1.2 + scan * 1.2),
                center,
                dotRadius * 2.1,
                dotRadius * 2.1);
        }

        private void DrawSpeedLines(DrawingContext dc, WpfPoint center, double w, double h, double seconds, double progress)
        {
            var alpha = SmoothSeconds(7.0, 12.0, seconds) * (1 - SmoothSeconds(32.0, 35.0, seconds));
            if (alpha <= 0.01)
            {
                return;
            }

            var speedRamp = SmoothSeconds(11.0, 31.0, seconds);
            var travel = seconds * (0.74 + speedRamp * 2.6);
            foreach (var line in _speedLines)
            {
                var phase = PositiveModulo(line.Offset - travel * line.Speed, 1);
                var radius = Lerp(Math.Min(w, h) * 0.06, Math.Sqrt(w * w + h * h) * 0.66, EaseOutCubic(phase));
                var tailRadius = Math.Max(4, radius - (42 + speedRamp * 220) * line.Length);
                var angle = line.Angle + Math.Sin(seconds * 0.18 + line.Offset * 9.0) * 0.018;
                var head = new WpfPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius * 0.58);
                var tail = new WpfPoint(center.X + Math.Cos(angle) * tailRadius, center.Y + Math.Sin(angle) * tailRadius * 0.58);
                var opacity = alpha * line.Opacity * FadeInOut(phase, 0.06, 0.82) * (0.5 + speedRamp * 0.78);
                if (opacity <= 0.02)
                {
                    continue;
                }

                dc.DrawLine(CreatePen(line.CyanBias > 0.55 ? TunnelCyan : TunnelGreen, opacity * 0.65, 0.75 + speedRamp * 1.6), tail, head);
            }
        }

        private void DrawCityDrive(DrawingContext dc, Rect bounds, double seconds, double progress)
        {
            var city = SmoothSeconds(31.8, 37.2, seconds) * (1 - SmoothSeconds(53.0, 55.0, seconds));
            if (city <= 0.01)
            {
                return;
            }

            dc.PushOpacity(city);
            var w = bounds.Width;
            var h = bounds.Height;
            var horizon = h * (0.52 - SmoothSeconds(35.0, 48.0, seconds) * 0.05);
            var roadTop = horizon + h * 0.08;

            DrawFarCity(dc, w, h, horizon, seconds, city);
            DrawRoad(dc, w, h, horizon, roadTop, seconds, city);
            DrawBuildingCanyon(dc, w, h, horizon, seconds, city);
            DrawRoadReflections(dc, w, h, horizon, seconds, city);
            DrawCityAtmosphere(dc, bounds, horizon, city);
            dc.Pop();
        }

        private void DrawFarCity(DrawingContext dc, double w, double h, double horizon, double seconds, double city)
        {
            for (var index = 0; index < 34; index++)
            {
                var t = index / 33.0;
                var x = Lerp(w * -0.04, w * 1.04, t);
                var width = w * (0.018 + Hash01(index, 17) * 0.034);
                var height = h * (0.12 + Hash01(index, 19) * 0.34);
                var y = horizon - height + Math.Sin(seconds * 0.13 + index) * 2.0;
                var alpha = 0.18 + Hash01(index, 23) * 0.24;
                var brush = new LinearGradientBrush(
                    WpfColor.FromArgb(Alpha(alpha * 120), 6, 27, 31),
                    WpfColor.FromArgb(Alpha(alpha * 175), 4, 13, 17),
                    90);
                var rect = new Rect(x, y, width, height);
                dc.DrawRectangle(brush, null, rect);

                if (index % 5 == 0)
                {
                    dc.DrawLine(CreatePen(CityAmber, alpha * 0.5, 1.1), new WpfPoint(x + width * 0.5, y - h * 0.035), new WpfPoint(x + width * 0.5, y));
                }

                DrawPerspectiveWindows(dc, rect, index, alpha * city, 3, 8);
            }
        }

        private void DrawRoad(DrawingContext dc, double w, double h, double horizon, double roadTop, double seconds, double city)
        {
            var road = new StreamGeometry();
            using (var context = road.Open())
            {
                context.BeginFigure(new WpfPoint(w * 0.44, roadTop), true, true);
                context.LineTo(new WpfPoint(w * 0.56, roadTop), true, false);
                context.LineTo(new WpfPoint(w * 0.78, h), true, false);
                context.LineTo(new WpfPoint(w * 0.22, h), true, false);
            }

            road.Freeze();
            var roadBrush = new LinearGradientBrush
            {
                StartPoint = new WpfPoint(0.5, 0),
                EndPoint = new WpfPoint(0.5, 1)
            };
            roadBrush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(118 * city), 12, 31, 36), 0));
            roadBrush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(190 * city), 6, 13, 16), 1));
            dc.DrawGeometry(roadBrush, null, road);

            for (var index = 0; index < 5; index++)
            {
                var xTop = w * (0.47 + index * 0.015);
                var xBottom = w * (0.36 + index * 0.07);
                dc.DrawLine(
                    CreatePen(RoadBlue, 0.22 * city, 1.1),
                    new WpfPoint(xTop, roadTop + h * 0.02),
                    new WpfPoint(xBottom, h));
            }

            var travel = seconds * 0.42;
            for (var dash = 0; dash < 18; dash++)
            {
                var p = PositiveModulo(dash / 18.0 + travel, 1);
                var y = Lerp(roadTop + 16, h + 40, p * p);
                var width = Lerp(w * 0.012, w * 0.085, p);
                var opacity = city * FadeInOut(p, 0.06, 0.9) * 0.42;
                dc.DrawLine(
                    CreatePen(WpfColor.FromRgb(190, 255, 236), opacity, 1.1 + p * 2.0),
                    new WpfPoint(w * 0.5 - width, y),
                    new WpfPoint(w * 0.5 + width, y));
            }
        }

        private void DrawBuildingCanyon(DrawingContext dc, double w, double h, double horizon, double seconds, double city)
        {
            for (var depthIndex = 13; depthIndex >= 0; depthIndex--)
            {
                var t = depthIndex / 13.0;
                var z = 1 - t;
                var yBase = Lerp(h * 1.08, horizon + h * 0.04, t);
                var top = Lerp(h * 0.12, horizon - h * (0.12 + Hash01(depthIndex, 97) * 0.22), t);
                var sideOffset = Lerp(w * 0.06, w * 0.34, t);
                var width = Lerp(w * 0.28, w * 0.08, t) * (0.78 + Hash01(depthIndex, 101) * 0.58);
                var lean = Lerp(w * 0.06, w * 0.015, t);
                var alpha = city * (0.18 + (1 - t) * 0.48);

                DrawSideBuilding(dc, true, sideOffset, top, yBase, width, lean, depthIndex, alpha, seconds, w, h);
                DrawSideBuilding(dc, false, w - sideOffset, top, yBase, width, lean, depthIndex + 41, alpha, seconds, w, h);
            }

            DrawStreetLights(dc, w, h, horizon, seconds, city);
        }

        private static void DrawSideBuilding(
            DrawingContext dc,
            bool left,
            double anchorX,
            double top,
            double bottom,
            double width,
            double lean,
            int seed,
            double alpha,
            double seconds,
            double w,
            double h)
        {
            var sign = left ? -1 : 1;
            var nearX = anchorX;
            var farX = anchorX + sign * width;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new WpfPoint(nearX, bottom), true, true);
                context.LineTo(new WpfPoint(farX, bottom - h * 0.04), true, false);
                context.LineTo(new WpfPoint(farX + sign * lean, top), true, false);
                context.LineTo(new WpfPoint(nearX + sign * lean * 0.22, top + h * 0.04), true, false);
            }

            geometry.Freeze();
            var body = new LinearGradientBrush
            {
                StartPoint = left ? new WpfPoint(0, 0.5) : new WpfPoint(1, 0.5),
                EndPoint = left ? new WpfPoint(1, 0.5) : new WpfPoint(0, 0.5)
            };
            body.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(alpha * 210), 4, 14, 17), 0));
            body.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(alpha * 155), 10, 52, 55), 0.62));
            body.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(alpha * 72), 10, 18, 22), 1));
            dc.DrawGeometry(body, CreatePen(CityTeal, alpha * 0.28, 1.0), geometry);

            var rows = 7 + seed % 5;
            var cols = 2 + seed % 4;
            for (var row = 0; row < rows; row++)
            {
                var yy = Lerp(top + h * 0.05, bottom - h * 0.13, row / Math.Max(1.0, rows - 1.0));
                for (var col = 0; col < cols; col++)
                {
                    if (Hash01(seed + row * 13 + col * 7, 113) < 0.42)
                    {
                        continue;
                    }

                    var lane = col / Math.Max(1.0, cols - 1.0);
                    var xx = Lerp(nearX + sign * lean * 0.4, farX + sign * lean * 0.6, lane);
                    var winW = Math.Abs(width) * 0.055;
                    var winH = h * 0.008;
                    var color = Hash01(seed + row + col, 127) > 0.76 ? CityAmber : CityTeal;
                    dc.DrawRoundedRectangle(
                        CreateSolidBrush(WpfColor.FromArgb(Alpha(alpha * 145), color.R, color.G, color.B)),
                        null,
                        new Rect(xx - winW * 0.5, yy, winW, winH),
                        winH * 0.4,
                        winH * 0.4);
                }
            }

            if (seed % 4 == 0)
            {
                var panelHeight = h * (0.045 + Hash01(seed, 131) * 0.035);
                var panelWidth = Math.Abs(width) * 0.22;
                var panelX = Lerp(nearX, farX, 0.56);
                var panelY = Lerp(top, bottom, 0.38);
                dc.DrawRoundedRectangle(
                    CreateSolidBrush(WpfColor.FromArgb(Alpha(alpha * 70), CityAmber.R, CityAmber.G, CityAmber.B)),
                    CreatePen(CityAmber, alpha * 0.54, 1.0),
                    new Rect(panelX - panelWidth * 0.5, panelY, panelWidth, panelHeight),
                    4,
                    4);
            }
        }

        private void DrawStreetLights(DrawingContext dc, double w, double h, double horizon, double seconds, double city)
        {
            for (var index = 0; index < 7; index++)
            {
                var t = index / 6.0;
                var y = Lerp(h * 0.96, horizon + h * 0.1, t);
                var side = index % 2 == 0 ? -1 : 1;
                var x = w * 0.5 + side * Lerp(w * 0.16, w * 0.38, t);
                var height = Lerp(h * 0.18, h * 0.08, t);
                var arm = Lerp(w * 0.08, w * 0.035, t) * -side;
                var alpha = city * (0.22 + (1 - t) * 0.36);

                dc.DrawLine(CreatePen(WpfColor.FromRgb(117, 154, 146), alpha, 1.4), new WpfPoint(x, y), new WpfPoint(x, y - height));
                dc.DrawLine(CreatePen(WpfColor.FromRgb(117, 154, 146), alpha, 1.2), new WpfPoint(x, y - height), new WpfPoint(x + arm, y - height - h * 0.015));
                dc.DrawLine(CreatePen(CityAmber, alpha * 1.6, 2.1), new WpfPoint(x + arm * 0.55, y - height - h * 0.013), new WpfPoint(x + arm, y - height - h * 0.015));
                dc.DrawEllipse(
                    CreateSolidBrush(WpfColor.FromArgb(Alpha(alpha * 34), CityAmber.R, CityAmber.G, CityAmber.B)),
                    null,
                    new WpfPoint(x + arm, y - height),
                    h * 0.052,
                    h * 0.034);
            }
        }

        private void DrawRoadReflections(DrawingContext dc, double w, double h, double horizon, double seconds, double city)
        {
            for (var index = 0; index < 26; index++)
            {
                var t = index / 25.0;
                var y = Lerp(horizon + h * 0.17, h * 0.98, t);
                var width = Lerp(w * 0.12, w * 0.56, t);
                var alpha = city * (0.12 + Hash01(index, 151) * 0.18) * (1 - t * 0.42);
                var color = index % 5 == 0 ? CityAmber : CityTeal;
                dc.DrawLine(
                    CreatePen(color, alpha, 0.8 + t * 2.5),
                    new WpfPoint(w * 0.5 - width * (0.25 + Hash01(index, 157) * 0.3), y),
                    new WpfPoint(w * 0.5 + width * (0.25 + Hash01(index, 163) * 0.3), y + h * 0.006));
            }
        }

        private void DrawCityAtmosphere(DrawingContext dc, Rect bounds, double horizon, double city)
        {
            var haze = new LinearGradientBrush
            {
                StartPoint = new WpfPoint(0.5, 0),
                EndPoint = new WpfPoint(0.5, 1)
            };
            haze.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 0, 0, 0), 0));
            haze.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(city * 42), 120, 230, 218), 0.54));
            haze.GradientStops.Add(new GradientStop(WpfColor.FromArgb(Alpha(city * 26), 255, 181, 92), 1));
            dc.DrawRectangle(haze, null, bounds);

            dc.DrawRectangle(
                CreateSolidBrush(WpfColor.FromArgb(Alpha(city * 28), 5, 13, 15)),
                null,
                new Rect(0, 0, bounds.Width, horizon * 0.88));
        }

        private static void DrawPerspectiveWindows(DrawingContext dc, Rect building, int seed, double alpha, int columns, int rows)
        {
            if (building.Width <= 4 || building.Height <= 8)
            {
                return;
            }

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (Hash01(seed + row * 17 + column * 31, 71) < 0.48)
                    {
                        continue;
                    }

                    var x = building.X + building.Width * (0.18 + column * 0.26);
                    var y = building.Y + building.Height * (0.18 + row * 0.095);
                    var width = building.Width * 0.075;
                    var height = building.Height * 0.018;
                    var color = Hash01(seed + row + column, 83) > 0.82 ? CityAmber : CityTeal;
                    dc.DrawRectangle(
                        CreateSolidBrush(WpfColor.FromArgb(Alpha(alpha * 120), color.R, color.G, color.B)),
                        null,
                        new Rect(x, y, width, height));
                }
            }
        }

        private static void DrawDepthOverlay(DrawingContext dc, Rect bounds, double progress)
        {
            var vignette = new RadialGradientBrush
            {
                Center = new WpfPoint(0.5, 0.52),
                GradientOrigin = new WpfPoint(0.5, 0.5),
                RadiusX = 0.84,
                RadiusY = 0.95
            };
            vignette.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 0, 0, 0), 0));
            vignette.GradientStops.Add(new GradientStop(WpfColor.FromArgb(76, 0, 0, 0), 0.58));
            vignette.GradientStops.Add(new GradientStop(WpfColor.FromArgb(226, 0, 0, 0), 1));
            dc.DrawRectangle(vignette, null, bounds);
        }

        private static void DrawProgressRail(DrawingContext dc, Rect bounds, double progress)
        {
            var railWidth = Math.Min(420, bounds.Width * 0.26);
            var railHeight = 4.0;
            var rail = new Rect(bounds.Width - railWidth - 34, bounds.Height - 38, railWidth, railHeight);
            dc.DrawRoundedRectangle(CreateSolidBrush(WpfColor.FromArgb(42, 220, 255, 240)), null, rail, railHeight / 2, railHeight / 2);
            dc.DrawRoundedRectangle(
                CreateSolidBrush(WpfColor.FromArgb(185, 75, 241, 220)),
                null,
                new Rect(rail.X, rail.Y, rail.Width * progress, rail.Height),
                railHeight / 2,
                railHeight / 2);
        }

        private static void DrawFinalBlackout(DrawingContext dc, Rect bounds, double progress)
        {
            var fade = Smooth(0.97, 1.0, progress);
            if (fade <= 0)
            {
                return;
            }

            dc.DrawRectangle(CreateSolidBrush(WpfColor.FromArgb(Alpha(fade * 255), 0, 0, 0)), null, bounds);
        }

        private static WpfPoint Project(double x, double y, double z, double w, double h, double vpY)
        {
            var focal = Math.Min(w, h) * 0.72;
            var scale = focal / Math.Max(0.45, z);
            return new WpfPoint(w * 0.5 + x * scale, vpY + y * scale * 0.62);
        }

        private static bool IsLooseInside(WpfPoint point, double width, double height)
        {
            return point.X >= -120 && point.X <= width + 120 && point.Y >= -120 && point.Y <= height + 120;
        }

        private static StarSeed[] BuildStars()
        {
            var stars = new StarSeed[StarCount];
            for (var index = 0; index < stars.Length; index++)
            {
                stars[index] = new StarSeed(
                    (Hash01(index, 3) - 0.5) * 2.8,
                    (Hash01(index, 5) - 0.5) * 1.7,
                    0.7 + Hash01(index, 7) * 10.8,
                    0.7 + Hash01(index, 11) * 2.8,
                    Hash01(index, 13),
                    0.34 + Hash01(index, 17) * 0.56);
            }

            return stars;
        }

        private static CodeMarkSeed[] BuildCodeMarks()
        {
            const string glyphs = "010101<>[]{}ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var marks = new CodeMarkSeed[CodeMarkCount];
            for (var index = 0; index < marks.Length; index++)
            {
                var glyphIndex = (int)Math.Floor(Hash01(index, 23) * glyphs.Length);
                marks[index] = new CodeMarkSeed(
                    Hash01(index, 29) > 0.5 ? 1 : -1,
                    Hash01(index, 31),
                    Hash01(index, 37),
                    0.7 + Hash01(index, 41) * 12.8,
                    0.42 + Hash01(index, 43) * 0.86,
                    glyphs[Math.Clamp(glyphIndex, 0, glyphs.Length - 1)].ToString(CultureInfo.InvariantCulture),
                    0.35 + Hash01(index, 47) * 0.62);
            }

            return marks;
        }

        private static SpeedLineSeed[] BuildSpeedLines()
        {
            var lines = new SpeedLineSeed[SpeedLineCount];
            for (var index = 0; index < lines.Length; index++)
            {
                lines[index] = new SpeedLineSeed(
                    index * 2.399963229728653 + Hash01(index, 53) * 0.18,
                    Hash01(index, 59),
                    0.38 + Hash01(index, 61) * 0.88,
                    0.48 + Hash01(index, 67) * 0.82,
                    0.26 + Hash01(index, 71) * 0.48,
                    Hash01(index, 73));
            }

            return lines;
        }

        private static SolidColorBrush CreateSolidBrush(WpfColor color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static WpfPen CreatePen(WpfColor color, double opacity, double thickness)
        {
            var brush = CreateSolidBrush(WpfColor.FromArgb(Alpha(opacity * 255), color.R, color.G, color.B));
            var pen = new WpfPen(brush, Math.Max(0.45, thickness))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }

        private static byte Alpha(double value)
        {
            return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        }

        private static WpfColor Mix(WpfColor start, WpfColor end, double amount)
        {
            var t = Math.Clamp(amount, 0, 1);
            return WpfColor.FromRgb(
                (byte)Math.Clamp((int)Math.Round(Lerp(start.R, end.R, t)), 0, 255),
                (byte)Math.Clamp((int)Math.Round(Lerp(start.G, end.G, t)), 0, 255),
                (byte)Math.Clamp((int)Math.Round(Lerp(start.B, end.B, t)), 0, 255));
        }

        private static double Lerp(double start, double end, double amount)
        {
            return start + (end - start) * amount;
        }

        private static double SmoothSeconds(double start, double end, double seconds)
        {
            return Smooth(start, end, seconds);
        }

        private static double Smooth(double start, double end, double value)
        {
            if (end <= start)
            {
                return value >= end ? 1 : 0;
            }

            var t = Math.Clamp((value - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }

        private static double EaseOutCubic(double value)
        {
            var t = Math.Clamp(value, 0, 1);
            var inverse = 1 - t;
            return 1 - inverse * inverse * inverse;
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

        private readonly record struct StarSeed(
            double X,
            double Y,
            double Z,
            double Size,
            double Hue,
            double Opacity);

        private readonly record struct CodeMarkSeed(
            int Side,
            double A,
            double B,
            double Z,
            double Speed,
            string Glyph,
            double Brightness);

        private readonly record struct SpeedLineSeed(
            double Angle,
            double Offset,
            double Speed,
            double Length,
            double Opacity,
            double CyanBias);
    }
}
