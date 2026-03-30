using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BluetoothBatteryWidget.App;

public partial class IconImageAdjustWindow : Window
{
    private const double PreviewSize = 200d;
    private const int OutputSize = 256;
    private const double MinZoomFactor = 0.25d;
    private const double MaxZoomFactor = 4.0d;
    private const double WheelZoomStep = 0.10d;

    private static readonly string TempOutputDirectory = Path.Combine(Path.GetTempPath(), "Bloss", "icon-adjusted");

    private readonly BitmapSource _sourceImage;
    private bool _isUiReady;
    private bool _isDragging;
    private System.Windows.Point _dragStartPoint;
    private double _dragStartOffsetX;
    private double _dragStartOffsetY;
    private double _zoomFactor = 1.0d;
    private double _offsetX;
    private double _offsetY;

    public IconImageAdjustWindow(string sourceImagePath)
    {
        InitializeComponent();
        _sourceImage = LoadBitmap(sourceImagePath);
        AttachHandlers();
        _isUiReady = true;
        PreviewImage.Source = _sourceImage;
        UpdatePreviewLayout();
    }

    public string? ResultImagePath { get; private set; }

    public static bool IsGeneratedTempPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullTempRoot = Path.GetFullPath(TempOutputDirectory + Path.DirectorySeparatorChar);
            return fullPath.StartsWith(fullTempRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static BitmapSource LoadBitmap(string sourceImagePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.UriSource = new Uri(sourceImagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var rendered = RenderAdjustedBitmap();
        var outputPath = SaveBitmapToTemp(rendered);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        ResultImagePath = outputPath;
        DialogResult = true;
        Close();
    }

    private void PreviewFrame_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (!_isUiReady)
        {
            return;
        }

        var zoomDelta = e.Delta > 0 ? (1.0d + WheelZoomStep) : (1.0d - WheelZoomStep);
        var nextZoom = Math.Clamp(_zoomFactor * zoomDelta, MinZoomFactor, MaxZoomFactor);
        if (Math.Abs(nextZoom - _zoomFactor) < 0.0001d)
        {
            return;
        }

        var sourceWidth = Math.Max(1, _sourceImage.PixelWidth);
        var sourceHeight = Math.Max(1, _sourceImage.PixelHeight);
        var currentScale = ComputeCoverScale(PreviewSize, sourceWidth, sourceHeight) * _zoomFactor;
        var nextScale = ComputeCoverScale(PreviewSize, sourceWidth, sourceHeight) * nextZoom;

        var currentWidth = sourceWidth * currentScale;
        var currentHeight = sourceHeight * currentScale;
        var currentLeft = ((PreviewSize - currentWidth) * 0.5d) + _offsetX;
        var currentTop = ((PreviewSize - currentHeight) * 0.5d) + _offsetY;
        var pointer = e.GetPosition(PreviewFrame);
        var relativeX = (pointer.X - currentLeft) / Math.Max(1d, currentWidth);
        var relativeY = (pointer.Y - currentTop) / Math.Max(1d, currentHeight);

        var nextWidth = sourceWidth * nextScale;
        var nextHeight = sourceHeight * nextScale;
        var nextLeft = pointer.X - (relativeX * nextWidth);
        var nextTop = pointer.Y - (relativeY * nextHeight);

        _zoomFactor = nextZoom;
        _offsetX = nextLeft - ((PreviewSize - nextWidth) * 0.5d);
        _offsetY = nextTop - ((PreviewSize - nextHeight) * 0.5d);
        ClampOffsets();
        UpdatePreviewLayout();
        e.Handled = true;
    }

    private void PreviewFrame_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isUiReady)
        {
            return;
        }

        _isDragging = true;
        _dragStartPoint = e.GetPosition(PreviewFrame);
        _dragStartOffsetX = _offsetX;
        _dragStartOffsetY = _offsetY;
        PreviewFrame.Cursor = System.Windows.Input.Cursors.SizeAll;
        PreviewFrame.CaptureMouse();
        e.Handled = true;
    }

    private void PreviewFrame_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var currentPoint = e.GetPosition(PreviewFrame);
        var deltaX = currentPoint.X - _dragStartPoint.X;
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        _offsetX = _dragStartOffsetX + deltaX;
        _offsetY = _dragStartOffsetY + deltaY;
        ClampOffsets();
        UpdatePreviewLayout();
    }

    private void PreviewFrame_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        EndDrag();
    }

    private void PreviewFrame_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
        }
    }

    private void GuideThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUiReady)
        {
            return;
        }

        ApplyGuideThickness();
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        PreviewFrame.ReleaseMouseCapture();
        PreviewFrame.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private void UpdatePreviewLayout()
    {
        var sourceWidth = Math.Max(1, _sourceImage.PixelWidth);
        var sourceHeight = Math.Max(1, _sourceImage.PixelHeight);
        var scale = ComputeCoverScale(PreviewSize, sourceWidth, sourceHeight) * _zoomFactor;

        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var left = ((PreviewSize - width) * 0.5d) + _offsetX;
        var top = ((PreviewSize - height) * 0.5d) + _offsetY;

        PreviewImage.Width = width;
        PreviewImage.Height = height;
        System.Windows.Controls.Canvas.SetLeft(PreviewImage, left);
        System.Windows.Controls.Canvas.SetTop(PreviewImage, top);
        ZoomValueText.Text = $"배율 {_zoomFactor:0.00}x";
        ApplyGuideThickness();
    }

    private RenderTargetBitmap RenderAdjustedBitmap()
    {
        var sourceWidth = Math.Max(1, _sourceImage.PixelWidth);
        var sourceHeight = Math.Max(1, _sourceImage.PixelHeight);
        var scale = ComputeCoverScale(OutputSize, sourceWidth, sourceHeight) * _zoomFactor;
        var previewToOutputScale = OutputSize / PreviewSize;
        var translatedX = _offsetX * previewToOutputScale;
        var translatedY = _offsetY * previewToOutputScale;

        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var left = ((OutputSize - width) * 0.5d) + translatedX;
        var top = ((OutputSize - height) * 0.5d) + translatedY;

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            context.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, OutputSize, OutputSize));
            context.DrawImage(_sourceImage, new Rect(left, top, width, height));
        }

        var renderBitmap = new RenderTargetBitmap(OutputSize, OutputSize, 96d, 96d, PixelFormats.Pbgra32);
        renderBitmap.Render(drawingVisual);
        renderBitmap.Freeze();
        return renderBitmap;
    }

    private static string? SaveBitmapToTemp(BitmapSource bitmap)
    {
        try
        {
            Directory.CreateDirectory(TempOutputDirectory);
            var outputPath = Path.Combine(TempOutputDirectory, $"adjusted_{Guid.NewGuid():N}.png");
            using var stream = File.Create(outputPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    private static double ComputeCoverScale(double targetSize, double sourceWidth, double sourceHeight)
    {
        var widthScale = targetSize / Math.Max(1d, sourceWidth);
        var heightScale = targetSize / Math.Max(1d, sourceHeight);
        return Math.Max(widthScale, heightScale);
    }

    private void ClampOffsets()
    {
        var sourceWidth = Math.Max(1, _sourceImage.PixelWidth);
        var sourceHeight = Math.Max(1, _sourceImage.PixelHeight);
        var scale = ComputeCoverScale(PreviewSize, sourceWidth, sourceHeight) * _zoomFactor;
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;

        var maxOffsetX = Math.Max(PreviewSize, width);
        var maxOffsetY = Math.Max(PreviewSize, height);
        _offsetX = Math.Clamp(_offsetX, -maxOffsetX, maxOffsetX);
        _offsetY = Math.Clamp(_offsetY, -maxOffsetY, maxOffsetY);
    }

    private void ApplyGuideThickness()
    {
        var guideThickness = Math.Clamp(GuideThicknessSlider.Value, 1.0d, 5.0d);
        GuideOuterRing.StrokeThickness = guideThickness;
        GuideInnerRing.StrokeThickness = Math.Max(1.0d, guideThickness * 0.65d);
        GuideHorizontal.StrokeThickness = Math.Max(1.0d, guideThickness * 0.65d);
        GuideVertical.StrokeThickness = Math.Max(1.0d, guideThickness * 0.65d);
        PreviewFrame.BorderThickness = new Thickness(Math.Max(1.0d, guideThickness * 0.7d));
        GuideThicknessValueText.Text = $"{guideThickness:0.0}";
    }

    private void AttachHandlers()
    {
        GuideThicknessSlider.ValueChanged += GuideThicknessSlider_ValueChanged;
    }
}
