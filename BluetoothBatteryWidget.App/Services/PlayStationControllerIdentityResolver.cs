using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal readonly record struct PlayStationControllerIdentity(
    string ControllerAddress,
    string BridgeAddress,
    bool HasVerifiedControllerAddress);

internal sealed class PlayStationControllerIdentityResolver
{
    private static readonly TimeSpan DefaultCacheLifetime = TimeSpan.FromSeconds(5);
    private const int MaximumCacheEntries = 16;

    private readonly object _cacheSync = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, byte[]?> _pairingInfoReader;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _cacheLifetime;

    public PlayStationControllerIdentityResolver()
        : this(ReadPairingInfoReport, () => DateTimeOffset.UtcNow, DefaultCacheLifetime)
    {
    }

    internal PlayStationControllerIdentityResolver(
        Func<string, byte[]?> pairingInfoReader,
        Func<DateTimeOffset>? utcNow = null,
        TimeSpan? cacheLifetime = null)
    {
        _pairingInfoReader = pairingInfoReader ?? throw new ArgumentNullException(nameof(pairingInfoReader));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _cacheLifetime = cacheLifetime is { } requested && requested > TimeSpan.Zero
            ? requested
            : DefaultCacheLifetime;
    }

    public PlayStationControllerIdentity Resolve(
        string? instanceId,
        string? devicePath,
        string? productId)
    {
        var bridgeAddress = PlayStationUsbBridgeSupport.BuildSyntheticAddress(instanceId, devicePath, productId);
        if (string.IsNullOrWhiteSpace(bridgeAddress))
        {
            return new PlayStationControllerIdentity(string.Empty, string.Empty, false);
        }

        var endpointKey = BuildEndpointKey(instanceId, devicePath);
        var now = _utcNow();
        if (!string.IsNullOrWhiteSpace(endpointKey) &&
            TryGetCachedAddress(endpointKey, now, out var cachedAddress))
        {
            return new PlayStationControllerIdentity(cachedAddress, bridgeAddress, true);
        }

        byte[]? report = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(devicePath))
            {
                report = _pairingInfoReader(devicePath);
            }
        }
        catch
        {
            // Identity lookup must never remove a controller that worked in v1.1.0.
        }

        if (report is not null &&
            DualSensePairingInfoParser.TryParseControllerAddress(report, out var controllerAddress))
        {
            if (!string.IsNullOrWhiteSpace(endpointKey))
            {
                StoreCachedAddress(endpointKey, controllerAddress, now);
            }

            return new PlayStationControllerIdentity(controllerAddress, bridgeAddress, true);
        }

        if (!string.IsNullOrWhiteSpace(endpointKey))
        {
            lock (_cacheSync)
            {
                _cache.Remove(endpointKey);
            }
        }

        return new PlayStationControllerIdentity(bridgeAddress, bridgeAddress, false);
    }

    public void RetainPresentEndpoints(IEnumerable<HidGamepadEndpoint> endpoints)
    {
        var presentKeys = endpoints
            .Select(endpoint => BuildEndpointKey(endpoint.InstanceId, endpoint.DevicePath))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = _utcNow();

        lock (_cacheSync)
        {
            foreach (var key in _cache.Keys.ToArray())
            {
                if (!presentKeys.Contains(key) || !IsFresh(_cache[key], now))
                {
                    _cache.Remove(key);
                }
            }
        }
    }

    private bool TryGetCachedAddress(string endpointKey, DateTimeOffset now, out string address)
    {
        lock (_cacheSync)
        {
            if (_cache.TryGetValue(endpointKey, out var cached) && IsFresh(cached, now))
            {
                address = cached.Address;
                return true;
            }

            _cache.Remove(endpointKey);
        }

        address = string.Empty;
        return false;
    }

    private void StoreCachedAddress(string endpointKey, string address, DateTimeOffset now)
    {
        lock (_cacheSync)
        {
            foreach (var key in _cache
                         .Where(pair => !IsFresh(pair.Value, now))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _cache.Remove(key);
            }

            if (!_cache.ContainsKey(endpointKey) && _cache.Count >= MaximumCacheEntries)
            {
                var oldestKey = _cache
                    .OrderBy(pair => pair.Value.ObservedAt)
                    .Select(pair => pair.Key)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(oldestKey))
                {
                    _cache.Remove(oldestKey);
                }
            }

            _cache[endpointKey] = new CacheEntry(address, now);
        }
    }

    private bool IsFresh(CacheEntry entry, DateTimeOffset now)
    {
        var age = now - entry.ObservedAt;
        return age >= TimeSpan.Zero && age <= _cacheLifetime;
    }

    private static string BuildEndpointKey(string? instanceId, string? devicePath)
    {
        var normalizedPath = HidDevicePathNormalizer.Normalize(devicePath);
        var normalizedInstanceId = instanceId?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath) && string.IsNullOrWhiteSpace(normalizedInstanceId))
        {
            return string.Empty;
        }

        return $"{normalizedInstanceId}|{normalizedPath.ToUpperInvariant()}";
    }

    private static byte[]? ReadPairingInfoReport(string devicePath)
    {
        using var handle = HidGamepadAccess.OpenHandle(devicePath);
        if (handle.IsInvalid || handle.IsClosed)
        {
            return null;
        }

        return HidGamepadAccess.TryReadFeatureReportExact(
            handle,
            DualSensePairingInfoParser.ReportId,
            DualSensePairingInfoParser.ReportSize,
            out var report,
            retryCount: 0)
            ? report
            : null;
    }

    private readonly record struct CacheEntry(string Address, DateTimeOffset ObservedAt);
}
