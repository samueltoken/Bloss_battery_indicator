using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using MediaColor = System.Windows.Media.Color;

namespace BluetoothBatteryWidget.App;

public partial class ReleaseNotesWindow : Window
{
    private const int TileColumns = 11;
    private const int TileRows = 6;
    private const double WindowCornerRadius = 26d;

    public ReleaseNotesWindow(string version, string? language = null)
    {
        InitializeComponent();
        var displayVersion = string.IsNullOrWhiteSpace(version) ? AppVersionInfo.DisplayVersion : version.Trim();
        var normalizedLanguage = WidgetSettings.NormalizeLanguage(language);
        ApplyLocalizedText(displayVersion, normalizedLanguage);
        RoundedContentRoot.SizeChanged += (_, _) => ApplyRoundedContentClip();
        VersionText.Text = $"Bloss {displayVersion}";
        BuildQuietTiles();
        Loaded += (_, _) =>
        {
            ApplyRoundedContentClip();
            BeginAmbientAnimations();
        };
    }

    private void ApplyLocalizedText(string displayVersion, string language)
    {
        Title = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            UiLanguageCatalog.GetExtraText(language, "ReleaseNotesWindowTitleFormat"),
            displayVersion);
        AutomationProperties.SetName(this, Title);

        HeadingText.Text = UiLanguageCatalog.GetExtraText(language, "ReleaseNotesHeading");
        SetReleaseNoteText(CleanupAutostartText, UiLanguageCatalog.GetExtraText(language, "ReleaseNotesCleanupAutostart"));
        SetReleaseNoteText(SleepGuardText, UiLanguageCatalog.GetExtraText(language, "ReleaseNotesSleepGuard"));
        SetReleaseNoteText(CustomGuideTriggerText, UiLanguageCatalog.GetExtraText(language, "ReleaseNotesCustomGuideTrigger"));
        SetReleaseNoteText(InstallUpdateValidationText, UiLanguageCatalog.GetExtraText(language, "ReleaseNotesInstallUpdateValidation"));
        FooterText.Text = UiLanguageCatalog.GetExtraText(language, "ReleaseNotesFooter");
        ConfirmButton.Content = UiLanguageCatalog.GetExtraText(language, "ReleaseNotesConfirm");
    }

    private static void SetReleaseNoteText(TextBlock target, string text)
    {
        target.Text = text;
        target.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyRoundedContentClip()
    {
        if (RoundedContentRoot.ActualWidth <= 0d || RoundedContentRoot.ActualHeight <= 0d)
        {
            return;
        }

        RoundedContentRoot.Clip = new RectangleGeometry(
            new Rect(0d, 0d, RoundedContentRoot.ActualWidth, RoundedContentRoot.ActualHeight),
            WindowCornerRadius,
            WindowCornerRadius);
    }

    private void BuildQuietTiles()
    {
        TileLayer.ColumnDefinitions.Clear();
        TileLayer.RowDefinitions.Clear();
        TileLayer.Children.Clear();

        for (var column = 0; column < TileColumns; column++)
        {
            TileLayer.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < TileRows; row++)
        {
            TileLayer.RowDefinitions.Add(new RowDefinition());
        }

        for (var row = 0; row < TileRows; row++)
        {
            for (var column = 0; column < TileColumns; column++)
            {
                var tile = CreateTile(row, column);
                Grid.SetRow(tile, row);
                Grid.SetColumn(tile, column);
                TileLayer.Children.Add(tile);
            }
        }
    }

    private static System.Windows.Shapes.Rectangle CreateTile(int row, int column)
    {
        var fillBrush = new SolidColorBrush(MediaColor.FromArgb(46, 255, 32, 32));
        var strokeBrush = new SolidColorBrush(MediaColor.FromArgb(70, 255, 58, 58));
        var tile = new System.Windows.Shapes.Rectangle
        {
            RadiusX = 7,
            RadiusY = 7,
            Margin = new Thickness(1.6),
            Fill = fillBrush,
            Stroke = strokeBrush,
            StrokeThickness = 0.8,
            Opacity = ComputeBaseOpacity(row, column)
        };

        return tile;
    }

    private static double ComputeBaseOpacity(int row, int column)
    {
        var wave = Math.Sin((row * 0.9d) + (column * 0.55d));
        return 0.11d + Math.Max(0d, wave) * 0.16d;
    }

    private void BeginAmbientAnimations()
    {
        SoftSweepTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(-360d, 860d, TimeSpan.FromSeconds(13.5))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        BrandCoreGlow.BeginAnimation(
            OpacityProperty,
            CreateBreathingDoubleAnimation(0.90d, 1d, TimeSpan.FromSeconds(6.4)));
        BrandRing.BeginAnimation(
            OpacityProperty,
            CreateBreathingDoubleAnimation(0.36d, 0.54d, TimeSpan.FromSeconds(8.8)));
    }

    private static DoubleAnimation CreateBreathingDoubleAnimation(double from, double to, TimeSpan duration)
    {
        return new DoubleAnimation(from, to, duration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
