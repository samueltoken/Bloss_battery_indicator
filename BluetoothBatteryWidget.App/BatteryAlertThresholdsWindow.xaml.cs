using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace BluetoothBatteryWidget.App;

public partial class BatteryAlertThresholdsWindow : Window
{
    private string _language;
    private bool _isClosingWithPopOut;

    public BatteryAlertThresholdsWindow(string currentThresholds, string? language = null)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        InitializeComponent();
        ApplyLocalizedText(_language);
        BuildThresholdOptions(currentThresholds);
    }

    public string SelectedThresholds { get; private set; } = string.Empty;

    public bool WasAccepted { get; private set; }

    internal System.Windows.Point? PopInOriginScreenPoint { get; set; }

    internal static IReadOnlyList<int> SelectableThresholds { get; } = [30, 40, 50, 60, 70, 80];

    internal void ApplyLocalizedText(string? language)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        Title = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsWindowTitle");
        HeadingTextBlock.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsHeading");
        DescriptionLine1Run.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsDescriptionLine1");
        DescriptionLine2Run.Text = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsDescriptionLine2");
        ForcedThresholdCheckBox.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsForced");
        ClearButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsClear");
        SaveButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsSave");
        CancelButton.Content = UiLanguageCatalog.GetExtraText(_language, "BatteryAlertThresholdsCancel");
    }

    private void BuildThresholdOptions(string currentThresholds)
    {
        var selected = WidgetSettings.GetBatteryAlertThresholdPercents(currentThresholds).ToHashSet();
        foreach (var threshold in SelectableThresholds)
        {
            var checkBox = new WpfToggleButton
            {
                Content = $"{threshold}%",
                Tag = threshold,
                IsChecked = selected.Contains(threshold),
                Style = (Style)FindResource("AlertChipToggleButtonStyle")
            };
            checkBox.Click += ThresholdToggle_Click;
            ThresholdPanel.Children.Add(checkBox);
        }
    }

    private void ThresholdToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfToggleButton toggleButton)
        {
            return;
        }

        try
        {
            toggleButton.ApplyTemplate();
            if (toggleButton.Template.FindName("ChipClickPulse", toggleButton) is not FrameworkElement pulse ||
                toggleButton.Template.FindName("ChipClickPulseScale", toggleButton) is not ScaleTransform pulseScale ||
                toggleButton.Template.FindName("ChipRootScale", toggleButton) is not ScaleTransform rootScale)
            {
                return;
            }

            var isChecked = toggleButton.IsChecked == true;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var pulseDuration = TimeSpan.FromMilliseconds(isChecked ? 560 : 390);
            var pulseScaleAnimation = new DoubleAnimation(0.82d, isChecked ? 1.24d : 1.12d, pulseDuration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            var opacityAnimation = new DoubleAnimation(
                isChecked ? 0.96d : 0.72d,
                0d,
                pulseDuration)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            var rootScaleAnimation = new DoubleAnimation(0.945d, 1d, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };

            pulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            pulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            pulse.BeginAnimation(OpacityProperty, null);
            pulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseScaleAnimation);
            pulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseScaleAnimation.Clone());
            rootScale.BeginAnimation(ScaleTransform.ScaleXProperty, rootScaleAnimation);
            rootScale.BeginAnimation(ScaleTransform.ScaleYProperty, rootScaleAnimation.Clone());
            pulse.BeginAnimation(OpacityProperty, opacityAnimation);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // The selection itself is more important than the optional click animation.
        }
    }

    private IReadOnlyList<int> GetSelectedThresholds()
    {
        return ThresholdPanel.Children
            .OfType<WpfToggleButton>()
            .Where(checkBox => checkBox.IsChecked == true && checkBox.Tag is int)
            .Select(checkBox => (int)checkBox.Tag)
            .OrderBy(threshold => threshold)
            .ToArray();
    }

    private void HeaderDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Drag can be interrupted while the modal window is closing.
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowPopInAnimator.Begin(
            this,
            WindowSurface,
            WindowSurfaceScale,
            WindowSurfaceSkew,
            WindowSurfaceTranslate,
            PopInOriginScreenPoint);
    }

    internal void CloseWithPopOut()
    {
        if (_isClosingWithPopOut)
        {
            return;
        }

        _isClosingWithPopOut = true;
        WasAccepted = false;
        WindowPopInAnimator.BeginClose(
            this,
            WindowSurface,
            WindowSurfaceScale,
            WindowSurfaceSkew,
            WindowSurfaceTranslate,
            PopInOriginScreenPoint,
            Close);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var checkBox in ThresholdPanel.Children.OfType<WpfToggleButton>())
        {
            checkBox.IsChecked = false;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedThresholds = WidgetSettings.NormalizeBatteryAlertThresholds(string.Join(", ", GetSelectedThresholds()));
        CloseWithResult(accepted: true);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithResult(accepted: false);
    }

    private void CloseWithResult(bool accepted)
    {
        WasAccepted = accepted;
        try
        {
            DialogResult = accepted;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }
}
