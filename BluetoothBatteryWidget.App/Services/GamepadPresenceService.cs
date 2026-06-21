using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class GamepadPresenceChangedEventArgs(int previousCount, int currentCount)
    : EventArgs
{
    public int PreviousCount { get; } = previousCount;

    public int CurrentCount { get; } = currentCount;
}

internal sealed class GamepadPresenceService
{
    public event EventHandler<GamepadPresenceChangedEventArgs>? GamepadConnected;

    public event EventHandler<GamepadPresenceChangedEventArgs>? GamepadDisconnected;

    public int ConnectedGamepadCount { get; private set; }

    public bool HasConnectedGamepad => ConnectedGamepadCount > 0;

    public int Refresh(IEnumerable<DeviceBatterySnapshot> snapshots)
    {
        var count = snapshots.Count(IsConnectedGamepadLikeDevice);
        if (count == ConnectedGamepadCount)
        {
            return ConnectedGamepadCount;
        }

        var previous = ConnectedGamepadCount;
        ConnectedGamepadCount = count;
        var args = new GamepadPresenceChangedEventArgs(previous, count);
        if (count > previous)
        {
            GamepadConnected?.Invoke(this, args);
        }
        else
        {
            GamepadDisconnected?.Invoke(this, args);
        }

        return ConnectedGamepadCount;
    }

    internal static bool IsConnectedGamepadLikeDevice(DeviceBatterySnapshot snapshot)
    {
        if (!snapshot.IsConnected || snapshot.IsStale)
        {
            return false;
        }

        if (snapshot.Category == DeviceCategory.Gamepad || snapshot.IconKey == IconKey.Gamepad)
        {
            return true;
        }

        var text = string.Join(
                ' ',
                snapshot.DisplayName,
                snapshot.BaseDisplayName ?? string.Empty,
                snapshot.ModelKey ?? string.Empty)
            .ToLowerInvariant();

        return text.Contains("dualsense", StringComparison.Ordinal) ||
               text.Contains("dual sense", StringComparison.Ordinal) ||
               text.Contains("wireless controller", StringComparison.Ordinal) ||
               text.Contains("steam controller", StringComparison.Ordinal) ||
               text.Contains("steamcon", StringComparison.Ordinal) ||
               text.Contains("xbox", StringComparison.Ordinal) ||
               text.Contains("gamepad", StringComparison.Ordinal) ||
               text.Contains("controller", StringComparison.Ordinal) ||
               text.Contains("pico2w", StringComparison.Ordinal) ||
               text.Contains("ds5", StringComparison.Ordinal);
    }
}
