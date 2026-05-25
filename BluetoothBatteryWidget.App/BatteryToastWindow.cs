using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BluetoothBatteryWidget.App.Services;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace BluetoothBatteryWidget.App;

internal sealed class BatteryToastWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _closeTimer;

    public BatteryToastWindow(
        string title,
        int percent,
        string subtitle,
        BatteryToastSeverity severity)
    {
        Width = 320;
        Height = 370;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = WpfBrushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Content = BuildContent(title, percent, subtitle, severity);

        _closeTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3.8)
        };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Close();
        };
    }

    public void ShowAtBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 28;
        Top = area.Bottom - Height - 28;
        Show();
        _closeTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closeTimer.Stop();
        base.OnClosed(e);
    }

    private static FrameworkElement BuildContent(
        string title,
        int percent,
        string subtitle,
        BatteryToastSeverity severity)
    {
        var accent = BatteryToastStyle.ResolveAccentBrush(severity);
        if (accent.CanFreeze)
        {
            accent.Freeze();
        }

        var root = new Border
        {
            Width = 320,
            Height = 370,
            CornerRadius = new CornerRadius(30),
            Background = WpfBrushes.White,
            ClipToBounds = true,
            Clip = new RectangleGeometry(new Rect(0, 0, 320, 370), 30, 30),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 28,
                ShadowDepth = 5,
                Opacity = 0.22
            }
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(188) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(182) });
        root.Child = grid;

        var top = new Grid { Background = accent };
        Grid.SetRow(top, 0);
        grid.Children.Add(top);

        top.Children.Add(new TextBlock
        {
            Text = NormalizeTitle(title),
            Foreground = WpfBrushes.White,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = WpfVerticalAlignment.Top,
            Margin = new Thickness(28, 24, 28, 0)
        });

        var icon = BuildBatteryIcon(percent);
        icon.HorizontalAlignment = WpfHorizontalAlignment.Center;
        icon.VerticalAlignment = WpfVerticalAlignment.Center;
        icon.Margin = new Thickness(0, 24, 0, 0);
        top.Children.Add(icon);

        var bottom = new Grid { Background = WpfBrushes.White };
        bottom.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        bottom.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
        bottom.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        Grid.SetRow(bottom, 1);
        grid.Children.Add(bottom);

        var badge = new Border
        {
            Width = 62,
            Height = 62,
            CornerRadius = new CornerRadius(31),
            Background = accent,
            BorderBrush = WpfBrushes.White,
            BorderThickness = new Thickness(3),
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = WpfVerticalAlignment.Top,
            Margin = new Thickness(0, -31, 0, 0),
            Child = new TextBlock
            {
                Text = "!",
                Foreground = WpfBrushes.White,
                FontSize = 44,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center,
                Margin = new Thickness(0, -4, 0, 0)
            }
        };
        Grid.SetRow(badge, 0);
        bottom.Children.Add(badge);

        var percentBlock = new TextBlock
        {
            Text = $"{Math.Clamp(percent, 0, 100)}%",
            Foreground = WpfBrushes.Black,
            FontSize = 48,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = WpfVerticalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetRow(percentBlock, 1);
        bottom.Children.Add(percentBlock);

        var subtitleBlock = new TextBlock
        {
            Text = subtitle,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(92, 92, 92)),
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = WpfVerticalAlignment.Top,
            Margin = new Thickness(18, 0, 18, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(subtitleBlock, 2);
        bottom.Children.Add(subtitleBlock);

        return root;
    }

    private static FrameworkElement BuildBatteryIcon(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var canvas = new Canvas
        {
            Width = 88,
            Height = 112
        };

        var cap = new WpfRectangle
        {
            Width = 34,
            Height = 16,
            Stroke = WpfBrushes.White,
            StrokeThickness = 4,
            Fill = WpfBrushes.Transparent
        };
        Canvas.SetLeft(cap, 27);
        Canvas.SetTop(cap, 2);
        canvas.Children.Add(cap);

        var body = new WpfRectangle
        {
            Width = 64,
            Height = 86,
            Stroke = WpfBrushes.White,
            StrokeThickness = 4,
            Fill = WpfBrushes.Transparent
        };
        Canvas.SetLeft(body, 12);
        Canvas.SetTop(body, 18);
        canvas.Children.Add(body);

        var fillHeight = Math.Max(8, 70 * clamped / 100.0);
        var fill = new WpfRectangle
        {
            Width = 42,
            Height = fillHeight,
            Fill = WpfBrushes.White
        };
        Canvas.SetLeft(fill, 23);
        Canvas.SetTop(fill, 94 - fillHeight);
        canvas.Children.Add(fill);

        return canvas;
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = string.IsNullOrWhiteSpace(title)
            ? "Controller"
            : title.Trim();
        return normalized.Length <= 32 ? normalized : normalized[..32];
    }
}
