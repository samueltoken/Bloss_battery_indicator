using System.ComponentModel;
using System.Runtime.CompilerServices;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.ViewModels;

public sealed class DeviceItemViewModel : INotifyPropertyChanged
{
    private DeviceBatterySnapshot _snapshot;
    private bool _isRenaming;
    private bool _isIconEditing;
    private string _editableName = string.Empty;
    private bool _isProbing;
    private int _probeProgress;
    private string _probeStatus = string.Empty;
    private bool _isProbeActionEnabled = true;

    public DeviceItemViewModel(DeviceBatterySnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DeviceId => _snapshot.DeviceId;

    public DeviceBatterySnapshot Snapshot => _snapshot;

    public string Address => _snapshot.Address;

    public string DisplayName => _snapshot.DisplayName;

    public string BaseDisplayName => string.IsNullOrWhiteSpace(_snapshot.BaseDisplayName)
        ? _snapshot.DisplayName
        : _snapshot.BaseDisplayName!;

    public bool HasCustomName => !string.Equals(DisplayName, BaseDisplayName, StringComparison.Ordinal);

    public int? BatteryPercent => _snapshot.BatteryPercent;

    public BatteryConfidence BatteryConfidence => _snapshot.BatteryConfidence;

    public BatterySourceKind SourceKind => _snapshot.SourceKind;

    public string? ModelKey => _snapshot.ModelKey;

    public bool IsCalibrationSuggested => _snapshot.SuggestCalibration;

    public bool IsEstimatedBattery => BatteryPercent is not null && BatteryConfidence == BatteryConfidence.Estimated;

    public bool IsBatterySuspect => _snapshot.IsBatterySuspect;

    public bool IsConnected => _snapshot.IsConnected;

    public bool IsStale => _snapshot.IsStale;

    public bool IsBatteryConnecting => _snapshot.IsBatteryConnecting;

    public DeviceCategory Category => _snapshot.Category;

    public IconKey IconKey => _snapshot.IconKey;

    public string? CustomIconImagePath => _snapshot.CustomIconImagePath;

    public bool HasCustomIconImage => !string.IsNullOrWhiteSpace(_snapshot.CustomIconImagePath);

    public DateTimeOffset LastUpdated => _snapshot.LastUpdated;

    public bool IsProbeEligible => Category == DeviceCategory.Gamepad && (BatteryPercent is null || IsBatterySuspect);

    public bool IsCalibrationEligible => Category == DeviceCategory.Gamepad && BatteryPercent is null && IsCalibrationSuggested;

    public bool IsRenaming
    {
        get => _isRenaming;
        private set
        {
            if (_isRenaming == value)
            {
                return;
            }

            _isRenaming = value;
            OnPropertyChanged();
        }
    }

    public bool IsIconEditing
    {
        get => _isIconEditing;
        set
        {
            if (_isIconEditing == value)
            {
                return;
            }

            _isIconEditing = value;
            OnPropertyChanged();
        }
    }

    public string EditableName
    {
        get => _editableName;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_editableName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _editableName = normalized;
            OnPropertyChanged();
        }
    }

    public bool IsProbing
    {
        get => _isProbing;
        private set
        {
            if (_isProbing == value)
            {
                return;
            }

            _isProbing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanStartProbe));
            OnPropertyChanged(nameof(CanStartCalibration));
            OnPropertyChanged(nameof(ShowProbeArea));
        }
    }

    public int ProbeProgress
    {
        get => _probeProgress;
        private set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (_probeProgress == normalized)
            {
                return;
            }

            _probeProgress = normalized;
            OnPropertyChanged();
        }
    }

    public string ProbeStatus
    {
        get => _probeStatus;
        private set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_probeStatus, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _probeStatus = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowProbeArea));
        }
    }

    public bool IsProbeActionEnabled
    {
        get => _isProbeActionEnabled;
        private set
        {
            if (_isProbeActionEnabled == value)
            {
                return;
            }

            _isProbeActionEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanStartProbe));
            OnPropertyChanged(nameof(CanStartCalibration));
        }
    }

    public bool CanStartProbe => IsProbeEligible && IsProbeActionEnabled && !IsProbing;

    public bool CanStartCalibration => IsCalibrationEligible && IsProbeActionEnabled && !IsProbing;

    public bool ShowProbeArea => IsProbeEligible || IsProbing || !string.IsNullOrWhiteSpace(ProbeStatus);

    public void UpdateSnapshot(DeviceBatterySnapshot snapshot)
    {
        _snapshot = snapshot;
        OnPropertyChanged(nameof(DeviceId));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(BaseDisplayName));
        OnPropertyChanged(nameof(HasCustomName));
        OnPropertyChanged(nameof(BatteryPercent));
        OnPropertyChanged(nameof(BatteryConfidence));
        OnPropertyChanged(nameof(SourceKind));
        OnPropertyChanged(nameof(ModelKey));
        OnPropertyChanged(nameof(IsCalibrationSuggested));
        OnPropertyChanged(nameof(IsEstimatedBattery));
        OnPropertyChanged(nameof(IsBatterySuspect));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(IsBatteryConnecting));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(IconKey));
        OnPropertyChanged(nameof(CustomIconImagePath));
        OnPropertyChanged(nameof(HasCustomIconImage));
        OnPropertyChanged(nameof(LastUpdated));
        OnPropertyChanged(nameof(IsProbeEligible));
        OnPropertyChanged(nameof(IsCalibrationEligible));
        OnPropertyChanged(nameof(CanStartProbe));
        OnPropertyChanged(nameof(CanStartCalibration));
        OnPropertyChanged(nameof(ShowProbeArea));

        if (!IsRenaming)
        {
            EditableName = DisplayName;
        }
    }

    public void SetProbeActionEnabled(bool enabled)
    {
        IsProbeActionEnabled = enabled;
    }

    public void BeginProbe(string status)
    {
        IsProbing = true;
        ProbeProgress = 0;
        ProbeStatus = status;
    }

    public void UpdateProbeProgress(int percent, string status)
    {
        ProbeProgress = percent;
        ProbeStatus = status;
    }

    public void CompleteProbe(string status, int percent = 100)
    {
        ProbeProgress = percent;
        ProbeStatus = status;
        IsProbing = false;
    }

    public void RestoreProbeState(bool isRunning, int progress, string status)
    {
        if (isRunning)
        {
            IsProbing = true;
            ProbeProgress = progress;
            ProbeStatus = status;
            return;
        }

        if (progress > 0 || !string.IsNullOrWhiteSpace(status))
        {
            ProbeProgress = progress;
            ProbeStatus = status;
        }
        else if (!IsProbeEligible)
        {
            ProbeProgress = 0;
            ProbeStatus = string.Empty;
        }

        IsProbing = false;
    }

    public void BeginRename()
    {
        EditableName = DisplayName;
        IsRenaming = true;
    }

    public void BeginIconEdit()
    {
        IsIconEditing = true;
    }

    public void CancelIconEdit()
    {
        IsIconEditing = false;
    }

    public void CancelRename()
    {
        EditableName = DisplayName;
        IsRenaming = false;
    }

    public void ApplyRenamedDisplayName(string displayName)
    {
        var normalized = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _snapshot = _snapshot with { DisplayName = normalized };
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasCustomName));
        EditableName = normalized;
        IsRenaming = false;
    }

    public void RestoreDefaultDisplayName()
    {
        _snapshot = _snapshot with { DisplayName = BaseDisplayName };
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasCustomName));
        EditableName = BaseDisplayName;
        IsRenaming = false;
    }

    public void ApplyCustomIconImagePath(string iconPath)
    {
        var normalized = iconPath?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _snapshot = _snapshot with { CustomIconImagePath = normalized };
        OnPropertyChanged(nameof(CustomIconImagePath));
        OnPropertyChanged(nameof(HasCustomIconImage));
        IsIconEditing = false;
    }

    public void RestoreDefaultIcon()
    {
        _snapshot = _snapshot with { CustomIconImagePath = null };
        OnPropertyChanged(nameof(CustomIconImagePath));
        OnPropertyChanged(nameof(HasCustomIconImage));
        IsIconEditing = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
