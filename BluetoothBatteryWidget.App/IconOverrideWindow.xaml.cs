using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App;

public partial class IconOverrideWindow : Window
{
    public IconOverrideWindow(
        IReadOnlyCollection<DeviceBatterySnapshot> snapshots,
        IReadOnlyDictionary<string, IconKey> existingOverrides,
        IReadOnlyDictionary<string, string> existingImageOverrides)
    {
        InitializeComponent();

        IconChoices = BuildIconChoices();
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
    }

    public ObservableCollection<IconOverrideItem> IconItems { get; }

    public IReadOnlyList<IconChoiceItem> IconChoices { get; }

    public Dictionary<string, IconKey> SelectedOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> SelectedImageOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

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

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CleanupAllTemporaryAdjustedImages();
        DialogResult = false;
        Close();
    }

    private void PickImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not IconOverrideItem item)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "아이콘 이미지 선택",
            Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var adjustWindow = new IconImageAdjustWindow(dialog.FileName)
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

    private static IReadOnlyList<IconChoiceItem> BuildIconChoices()
    {
        return new[]
        {
            new IconChoiceItem(IconKey.Unknown, "자동"),
            new IconChoiceItem(IconKey.Mouse, "마우스"),
            new IconChoiceItem(IconKey.Keyboard, "키보드"),
            new IconChoiceItem(IconKey.Headset, "헤드셋"),
            new IconChoiceItem(IconKey.Earbuds, "이어버드"),
            new IconChoiceItem(IconKey.Speaker, "스피커"),
            new IconChoiceItem(IconKey.Gamepad, "게임패드"),
            new IconChoiceItem(IconKey.Phone, "휴대폰"),
            new IconChoiceItem(IconKey.Tablet, "태블릿"),
            new IconChoiceItem(IconKey.Laptop, "노트북")
        };
    }
}

public sealed record IconChoiceItem(IconKey Key, string Label);
