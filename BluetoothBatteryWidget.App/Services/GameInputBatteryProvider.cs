using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using Windows.Devices.Power;
using Windows.Gaming.Input;

namespace BluetoothBatteryWidget.App.Services;

public sealed class GameInputBatteryProvider
{
    private static readonly TimeSpan StickyMatchTtl = TimeSpan.FromMinutes(3);
    private readonly object _stickySync = new();
    private readonly Dictionary<int, StickyMatchState> _stickyBySourceIndex = new();

    public Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<GameInputBatteryReading> readings;
        try
        {
            readings = ReadGameInputReadings(cancellationToken);
        }
        catch
        {
            return Task.FromResult<IReadOnlyList<PnpBatteryReading>>([]);
        }

        if (readings.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PnpBatteryReading>>([]);
        }

        var endpointSignals = XboxEndpointSignalBuilder.Build(connectedDevices, cancellationToken);
        var reading = readings.Count == 1 ? readings[0] : null;
        var preferredAddress = reading is null
            ? null
            : TryGetStickyAddress(reading.SourceIndex, connectedDevices, DateTimeOffset.Now);
        var matched = XboxBatteryMatcher.MatchGameInputBestEffort(
            connectedDevices,
            readings,
            endpointSignals,
            preferredAddress);
        if (reading is not null)
        {
            UpdateStickyAddress(reading.SourceIndex, matched.FirstOrDefault()?.Address, DateTimeOffset.Now);
        }

        return Task.FromResult(matched);
    }

    private static IReadOnlyList<GameInputBatteryReading> ReadGameInputReadings(CancellationToken cancellationToken)
    {
        var result = new List<GameInputBatteryReading>();
        var index = 0;

        foreach (var pad in Gamepad.Gamepads)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batteryReport = pad.TryGetBatteryReport();
            if (!TryMapBattery(batteryReport, out var percent, out var remaining, out var full))
            {
                index++;
                continue;
            }

            result.Add(new GameInputBatteryReading(
                SourceIndex: index,
                BatteryPercent: percent,
                RawMetric: remaining,
                FullMetric: full));
            index++;
        }

        return result;
    }

    private static bool TryMapBattery(
        BatteryReport batteryReport,
        out int percent,
        out double remaining,
        out double full)
    {
        percent = 0;
        remaining = 0;
        full = 0;

        var remainingValue = batteryReport.RemainingCapacityInMilliwattHours;
        var fullValue = batteryReport.FullChargeCapacityInMilliwattHours;
        if (!remainingValue.HasValue || !fullValue.HasValue || fullValue.Value <= 0)
        {
            return false;
        }

        var ratio = remainingValue.Value / (double)fullValue.Value;
        var mapped = (int)Math.Round(ratio * 100d, MidpointRounding.AwayFromZero);
        percent = NormalizePercent(mapped, remainingValue.Value, fullValue.Value);
        remaining = remainingValue.Value;
        full = fullValue.Value;
        return true;
    }

    internal static int NormalizePercent(int mappedPercent, double remainingCapacity, double fullCapacity)
    {
        var normalizedMapped = Math.Clamp(mappedPercent, 0, 100);

        // Some third-party Xbox-layer controllers report full=~1000 while remaining is already 0..100 scale.
        // In this case ratio-based mapping under-reports by about 10x (100% -> 10%).
        var isLikelyScaledByTen =
            fullCapacity >= 900d &&
            fullCapacity <= 1100d &&
            remainingCapacity >= 0d &&
            remainingCapacity <= 100d;
        if (!isLikelyScaledByTen)
        {
            return normalizedMapped;
        }

        return Math.Clamp((int)Math.Round(remainingCapacity, MidpointRounding.AwayFromZero), 0, 100);
    }

    private string? TryGetStickyAddress(
        int sourceIndex,
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        DateTimeOffset now)
    {
        lock (_stickySync)
        {
            CleanupExpiredStickyUnsafe(now);
            if (!_stickyBySourceIndex.TryGetValue(sourceIndex, out var sticky))
            {
                return null;
            }

            var normalizedAddress = AddressNormalizer.NormalizeAddress(sticky.Address);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                return null;
            }

            var isStillConnected = connectedDevices.Any(device =>
                device.IsConnected &&
                string.Equals(
                    AddressNormalizer.NormalizeAddress(device.Address),
                    normalizedAddress,
                    StringComparison.OrdinalIgnoreCase));
            if (!isStillConnected)
            {
                _stickyBySourceIndex.Remove(sourceIndex);
                return null;
            }

            return normalizedAddress;
        }
    }

    private void UpdateStickyAddress(int sourceIndex, string? winnerAddress, DateTimeOffset now)
    {
        lock (_stickySync)
        {
            CleanupExpiredStickyUnsafe(now);
            if (string.IsNullOrWhiteSpace(winnerAddress))
            {
                return;
            }

            var normalizedAddress = AddressNormalizer.NormalizeAddress(winnerAddress);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                return;
            }

            _stickyBySourceIndex[sourceIndex] = new StickyMatchState(
                normalizedAddress,
                now + StickyMatchTtl);
        }
    }

    private void CleanupExpiredStickyUnsafe(DateTimeOffset now)
    {
        var expired = _stickyBySourceIndex
            .Where(pair => pair.Value.ExpiresAt <= now)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in expired)
        {
            _stickyBySourceIndex.Remove(key);
        }
    }

    private readonly record struct StickyMatchState(string Address, DateTimeOffset ExpiresAt);
}
