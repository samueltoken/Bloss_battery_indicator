using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using System.IO;

namespace BluetoothBatteryWidget.Core.Services;

public sealed class DeviceSnapshotComposer
{
    private readonly IIconResolver _iconResolver;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CachedCategory> _categoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CachedIcon> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    public DeviceSnapshotComposer(IIconResolver iconResolver)
    {
        _iconResolver = iconResolver;
    }

    public IReadOnlyList<DeviceBatterySnapshot> Compose(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        IReadOnlyList<PnpBatteryReading> batteryReadings,
        IReadOnlyDictionary<string, IconKey> overrides,
        IReadOnlyDictionary<string, string> imageOverrides,
        IReadOnlyDictionary<string, string> nameOverrides,
        DateTimeOffset timestamp)
    {
        var batteryByAddress = BuildBatteryLookup(batteryReadings);
        var snapshots = new List<DeviceBatterySnapshot>(connectedDevices.Count);

        foreach (var connected in connectedDevices)
        {
            var normalizedAddress = AddressNormalizer.NormalizeAddress(connected.Address);
            if (string.IsNullOrEmpty(normalizedAddress))
            {
                continue;
            }

            batteryByAddress.TryGetValue(normalizedAddress, out var batteryReading);
            var baseDisplayName = string.IsNullOrWhiteSpace(connected.DisplayName)
                ? batteryReading?.DisplayName ?? $"Bluetooth {normalizedAddress[^4..]}"
                : connected.DisplayName.Trim();
            var displayName = nameOverrides.TryGetValue(normalizedAddress, out var customName)
                ? customName
                : baseDisplayName;
            var batteryConfidence = batteryReading?.BatteryConfidence ?? BatteryConfidence.Confirmed;
            var sourceKind = batteryReading?.SourceKind ?? BatterySourceKind.Unknown;
            var modelKey = batteryReading?.ModelKey;
            var suggestCalibration = batteryReading?.SuggestCalibration ?? false;
            var isBatterySuspect = batteryReading?.IsBatterySuspect ?? false;

            var category = ResolveCategory(normalizedAddress, baseDisplayName, connected.CategoryHint);
            var icon = ResolveIcon(normalizedAddress, category, baseDisplayName, overrides);
            var customIconImagePath = ResolveCustomIconImagePath(normalizedAddress, imageOverrides);

            snapshots.Add(
                new DeviceBatterySnapshot(
                    DeviceId: connected.DeviceId,
                    Address: normalizedAddress,
                    DisplayName: displayName,
                    BatteryPercent: batteryReading?.BatteryPercent,
                    BatteryConfidence: batteryConfidence,
                    SourceKind: sourceKind,
                    ModelKey: modelKey,
                    SuggestCalibration: suggestCalibration,
                    IsBatterySuspect: isBatterySuspect,
                    IsConnected: connected.IsConnected,
                    Category: category,
                    IconKey: icon,
                    LastUpdated: timestamp,
                    BaseDisplayName: baseDisplayName,
                    CustomIconImagePath: customIconImagePath));
        }

        snapshots.Sort(CompareSnapshots);
        return snapshots;
    }

    private DeviceCategory ResolveCategory(string address, string displayName, string? categoryHint)
    {
        lock (_cacheLock)
        {
            if (_categoryCache.TryGetValue(address, out var cached) &&
                string.Equals(cached.DisplayName, displayName, StringComparison.Ordinal) &&
                string.Equals(cached.CategoryHint, categoryHint, StringComparison.Ordinal))
            {
                return cached.Category;
            }
        }

        var computed = DeviceCategoryClassifier.Classify(displayName, categoryHint);
        lock (_cacheLock)
        {
            _categoryCache[address] = new CachedCategory(displayName, categoryHint, computed);
        }

        return computed;
    }

    private IconKey ResolveIcon(
        string address,
        DeviceCategory category,
        string displayName,
        IReadOnlyDictionary<string, IconKey> overrides)
    {
        if (overrides.ContainsKey(address))
        {
            return _iconResolver.Resolve(address, category, displayName, overrides);
        }

        lock (_cacheLock)
        {
            if (_iconCache.TryGetValue(address, out var cached) && cached.Category == category)
            {
                return cached.Icon;
            }
        }

        var computed = _iconResolver.Resolve(address, category, displayName, overrides);
        lock (_cacheLock)
        {
            _iconCache[address] = new CachedIcon(category, computed);
        }

        return computed;
    }

    private static Dictionary<string, PnpBatteryReading> BuildBatteryLookup(IReadOnlyList<PnpBatteryReading> readings)
    {
        var result = new Dictionary<string, PnpBatteryReading>(StringComparer.OrdinalIgnoreCase);
        foreach (var reading in readings)
        {
            var normalizedAddress = AddressNormalizer.NormalizeAddress(reading.Address);
            if (string.IsNullOrEmpty(normalizedAddress))
            {
                continue;
            }

            if (!result.TryGetValue(normalizedAddress, out var existing))
            {
                result[normalizedAddress] = reading;
                continue;
            }

            if (existing.BatteryPercent is null && reading.BatteryPercent is not null)
            {
                result[normalizedAddress] = reading;
            }
        }

        return result;
    }

    private static string? ResolveCustomIconImagePath(
        string address,
        IReadOnlyDictionary<string, string> imageOverrides)
    {
        if (!imageOverrides.TryGetValue(address, out var path) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmedPath = path.Trim();
        return File.Exists(trimmedPath) ? trimmedPath : null;
    }

    private static int CompareSnapshots(DeviceBatterySnapshot left, DeviceBatterySnapshot right)
    {
        var leftRank = left.BatteryPercent ?? int.MaxValue;
        var rightRank = right.BatteryPercent ?? int.MaxValue;
        var byBattery = leftRank.CompareTo(rightRank);
        if (byBattery != 0)
        {
            return byBattery;
        }

        return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct CachedCategory(string DisplayName, string? CategoryHint, DeviceCategory Category);

    private readonly record struct CachedIcon(DeviceCategory Category, IconKey Icon);
}
