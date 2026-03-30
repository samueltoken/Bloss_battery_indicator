using System.ComponentModel;
using System.Runtime.CompilerServices;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.ViewModels;

public sealed class IconOverrideItem : INotifyPropertyChanged
{
    private IconKey _selectedIcon;
    private string _customIconPath = string.Empty;

    public required string Address { get; init; }

    public required string DisplayName { get; init; }

    public IconKey SelectedIcon
    {
        get => _selectedIcon;
        set
        {
            if (_selectedIcon == value)
            {
                return;
            }

            _selectedIcon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIcon)));
        }
    }

    public string CustomIconPath
    {
        get => _customIconPath;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_customIconPath, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _customIconPath = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomIconPath)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCustomIconPath)));
        }
    }

    public bool HasCustomIconPath => !string.IsNullOrWhiteSpace(_customIconPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetValueWithoutNotify(IconKey value)
    {
        _selectedIcon = value;
    }

    public void SetCustomIconPathWithoutNotify(string? path)
    {
        _customIconPath = path?.Trim() ?? string.Empty;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
