using System.Text.Json;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public sealed class BatteryObservationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly TimeSpan ObservationTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan PersistDebounce = TimeSpan.FromSeconds(1);
    private const int MaxSamplesPerModel = 64;

    private readonly object _sync = new();
    private readonly string _storePath;
    private List<BatteryEvidence>? _cached;
    private DateTimeOffset _lastPersistAt;
    private bool _dirty;

    public BatteryObservationStore(string? storePath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storePath = storePath ?? Path.Combine(appData, "Bloss", "battery-observations.jsonl");
    }

    public string StorePath => _storePath;

    public void Record(IEnumerable<BatteryEvidence> observations, DateTimeOffset now)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var added = false;

            foreach (var observation in observations)
            {
                if (!IsValid(observation))
                {
                    continue;
                }

                _cached!.Add(Normalize(observation));
                added = true;
            }

            if (!added)
            {
                return;
            }

            Prune(now);
            _dirty = true;
            TryPersist(now);
        }
    }

    public IReadOnlyList<BatteryEvidence> GetRecentForModel(
        string modelKey,
        BatterySourceKind sourceKind,
        DateTimeOffset now)
    {
        lock (_sync)
        {
            EnsureLoaded();
            Prune(now);
            TryPersist(now);

            var normalizedModel = NormalizeModelKey(modelKey);
            return _cached!
                .Where(item =>
                    string.Equals(item.ModelKey, normalizedModel, StringComparison.OrdinalIgnoreCase) &&
                    item.SourceKind == sourceKind)
                .OrderBy(item => item.ObservedAt)
                .ToList();
        }
    }

    private static bool IsValid(BatteryEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.ModelKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(evidence.Address))
        {
            return false;
        }

        if (evidence.DerivedPercent is < 0 or > 100)
        {
            return false;
        }

        if (evidence.SourceKind == BatterySourceKind.Unknown)
        {
            return false;
        }

        return true;
    }

    private static BatteryEvidence Normalize(BatteryEvidence evidence)
    {
        return evidence with
        {
            Address = AddressNormalizer.NormalizeAddress(evidence.Address),
            ModelKey = NormalizeModelKey(evidence.ModelKey)
        };
    }

    private static string NormalizeModelKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private void Prune(DateTimeOffset now)
    {
        var beforeCount = _cached!.Count;
        _cached = _cached
            .Where(item => now - item.ObservedAt <= ObservationTtl)
            .ToList();

        var trimmed = new List<BatteryEvidence>(_cached.Count);
        foreach (var group in _cached.GroupBy(item => item.ModelKey, StringComparer.OrdinalIgnoreCase))
        {
            var recent = group
                .OrderByDescending(item => item.ObservedAt)
                .Take(MaxSamplesPerModel)
                .OrderBy(item => item.ObservedAt);
            trimmed.AddRange(recent);
        }

        if (trimmed.Count != _cached.Count || beforeCount != _cached.Count)
        {
            _dirty = true;
        }

        _cached = trimmed;
    }

    private void TryPersist(DateTimeOffset now)
    {
        if (!_dirty)
        {
            return;
        }

        if (now - _lastPersistAt < PersistDebounce)
        {
            return;
        }

        Persist();
        _lastPersistAt = now;
        _dirty = false;
    }

    private void EnsureLoaded()
    {
        if (_cached is not null)
        {
            return;
        }

        _cached = new List<BatteryEvidence>();
        try
        {
            if (!File.Exists(_storePath))
            {
                return;
            }

            foreach (var line in File.ReadLines(_storePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                BatteryEvidence? parsed = null;
                try
                {
                    parsed = JsonSerializer.Deserialize<BatteryEvidence>(line, JsonOptions);
                }
                catch
                {
                    // Skip malformed lines.
                }

                if (parsed is null || !IsValid(parsed))
                {
                    continue;
                }

                _cached.Add(Normalize(parsed));
            }
        }
        catch
        {
            _cached = new List<BatteryEvidence>();
        }
    }

    private void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        using var writer = new StreamWriter(_storePath, append: false);
        foreach (var observation in _cached!.OrderBy(item => item.ObservedAt))
        {
            writer.WriteLine(JsonSerializer.Serialize(observation, JsonOptions));
        }
    }
}
