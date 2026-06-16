using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App;

public partial class IconOverrideWindow : Window
{
    private string _language;
    private bool _isClosingWithPopOut;

    public IconOverrideWindow(
        IReadOnlyCollection<DeviceBatterySnapshot> snapshots,
        IReadOnlyDictionary<string, IconKey> existingOverrides,
        IReadOnlyDictionary<string, string> existingImageOverrides,
        string? language = null)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        InitializeComponent();
        ApplyLocalizedText(_language);
        WindowPopInAnimator.AttachCentered(this);

        IconChoices = BuildIconChoices(_language);
        IconItems = new ObservableCollection<IconOverrideItem>(
            snapshots
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                {
                    var iconItem = new IconOverrideItem
                    {
                        Address = item.Address,
                        DisplayName = item.DisplayName
                    };

                    if (!existingOverrides.TryGetValue(item.Address, out var selectedIcon))
                    {
                        selectedIcon = IconKey.Unknown;
                    }

                    iconItem.SetValueWithoutNotify(selectedIcon);
                    if (existingImageOverrides.TryGetValue(item.Address, out var customIconPath))
                    {
                        iconItem.SetCustomIconPathWithoutNotify(customIconPath);
                    }

                    return iconItem;
                }));

        DataContext = this;
        Closed += (_, _) =>
        {
            if (!WasAccepted)
            {
                CleanupAllTemporaryAdjustedImages();
            }
        };
    }

    public ObservableCollection<IconOverrideItem> IconItems { get; }

    public IReadOnlyList<IconChoiceItem> IconChoices { get; }

    public string TextIconOverrideChooseImage => UiLanguageCatalog.GetExtraText(_language, "IconOverrideChooseImage");

    public string TextIconOverrideClearImage => UiLanguageCatalog.GetExtraText(_language, "IconOverrideClearImage");

    public Dictionary<string, IconKey> SelectedOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> SelectedImageOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool WasAccepted { get; private set; }

    internal void ApplyLocalizedText(string? language)
    {
        _language = WidgetSettings.NormalizeLanguage(language);
        Title = UiLanguageCatalog.GetExtraText(_language, "IconOverrideWindowTitle");
        HeadingTextBlock.Text = UiLanguageCatalog.GetExtraText(_language, "IconOverrideHeading");
        DeviceColumn.Header = UiLanguageCatalog.GetExtraText(_language, "IconOverrideDeviceColumn");
        AddressColumn.Header = UiLanguageCatalog.GetExtraText(_language, "IconOverrideAddressColumn");
        IconColumn.Header = UiLanguageCatalog.GetExtraText(_language, "IconOverrideIconColumn");
        CustomImageColumn.Header = UiLanguageCatalog.GetExtraText(_language, "IconOverrideCustomImageColumn");
        CancelButton.Content = UiLanguageCatalog.GetExtraText(_language, "IconOverrideCancel");
        SaveButton.Content = UiLanguageCatalog.GetExtraText(_language, "IconOverrideSave");
    }

    internal void CloseWithPopOutAsCancel()
    {
        if (_isClosingWithPopOut)
        {
            return;
        }

        _isClosingWithPopOut = true;
        WasAccepted = false;
        CleanupAllTemporaryAdjustedImages();
        WindowPopInAnimator.BeginCloseCentered(this, Close);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedOverrides.Clear();
        SelectedImageOverrides.Clear();
        foreach (var item in IconItems)
        {
            if (item.SelectedIcon != IconKey.Unknown)
            {
                SelectedOverrides[item.Address] = item.SelectedIcon;
            }

            if (!string.IsNullOrWhiteSpace(item.CustomIconPath))
            {
                SelectedImageOverrides[item.Address] = item.CustomIconPath.Trim();
            }
        }

        CloseWithResult(accepted: true);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CleanupAllTemporaryAdjustedImages();
        CloseWithResult(accepted: false);
    }

    private void PickImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not IconOverrideItem item)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = UiLanguageCatalog.GetExtraText(_language, "IconOverrideImageSelectTitle"),
            Filter = UiLanguageCatalog.GetExtraText(_language, "IconOverrideImageFilter"),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var adjustWindow = new IconImageAdjustWindow(dialog.FileName, _language)
        {
            Owner = this
        };

        if (adjustWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(adjustWindow.ResultImagePath))
        {
            return;
        }

        CleanupTemporaryAdjustedImage(item.CustomIconPath);
        item.CustomIconPath = adjustWindow.ResultImagePath;
    }

    private void ClearImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not IconOverrideItem item)
        {
            return;
        }

        CleanupTemporaryAdjustedImage(item.CustomIconPath);
        item.CustomIconPath = string.Empty;
    }

    private void CleanupAllTemporaryAdjustedImages()
    {
        foreach (var item in IconItems)
        {
            CleanupTemporaryAdjustedImage(item.CustomIconPath);
        }
    }

    private static void CleanupTemporaryAdjustedImage(string? path)
    {
        if (!IconImageAdjustWindow.IsGeneratedTempPath(path))
        {
            return;
        }

        try
        {
            File.Delete(path!);
        }
        catch
        {
            // Ignore temporary file cleanup failures.
        }
    }

    private static IReadOnlyList<IconChoiceItem> BuildIconChoices(string? language)
    {
        return new[]
        {
            new IconChoiceItem(IconKey.Unknown, UiLanguageCatalog.GetExtraText(language, "IconChoiceAuto")),
            new IconChoiceItem(IconKey.Mouse, UiLanguageCatalog.GetExtraText(language, "IconChoiceMouse")),
            new IconChoiceItem(IconKey.Keyboard, UiLanguageCatalog.GetExtraText(language, "IconChoiceKeyboard")),
            new IconChoiceItem(IconKey.Headset, UiLanguageCatalog.GetExtraText(language, "IconChoiceHeadset")),
            new IconChoiceItem(IconKey.Earbuds, UiLanguageCatalog.GetExtraText(language, "IconChoiceEarbuds")),
            new IconChoiceItem(IconKey.Speaker, UiLanguageCatalog.GetExtraText(language, "IconChoiceSpeaker")),
            new IconChoiceItem(IconKey.Gamepad, UiLanguageCatalog.GetExtraText(language, "IconChoiceGamepad")),
            new IconChoiceItem(IconKey.Phone, UiLanguageCatalog.GetExtraText(language, "IconChoicePhone")),
            new IconChoiceItem(IconKey.Tablet, UiLanguageCatalog.GetExtraText(language, "IconChoiceTablet")),
            new IconChoiceItem(IconKey.Laptop, UiLanguageCatalog.GetExtraText(language, "IconChoiceLaptop"))
        };
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

public sealed record IconChoiceItem(IconKey Key, string Label);
