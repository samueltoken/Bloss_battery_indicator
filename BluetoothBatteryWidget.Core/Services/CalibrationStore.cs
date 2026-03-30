using System.Text.Json;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public sealed class CalibrationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _storePath;
    private Dictionary<string, ModelCalibration>? _cached;

    public CalibrationStore(string? storePath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storePath = storePath ?? Path.Combine(appData, "Bloss", "gamepad-calibrations.json");
    }

    public string StorePath => _storePath;

    public bool TryGet(string modelKey, out ModelCalibration calibration)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var normalized = NormalizeModelKey(modelKey);
            return _cached!.TryGetValue(normalized, out calibration!);
        }
    }

    public void UpsertFullAnchor(string modelKey, double fullAnchorRawMetric, DateTimeOffset now)
    {
        if (fullAnchorRawMetric <= 0 || double.IsNaN(fullAnchorRawMetric) || double.IsInfinity(fullAnchorRawMetric))
        {
            return;
        }

        lock (_sync)
        {
            EnsureLoaded();
            var normalized = NormalizeModelKey(modelKey);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            _cached![normalized] = new ModelCalibration(
                ModelKey: normalized,
                FullAnchorRawMetric: fullAnchorRawMetric,
                UpdatedAt: now);
            Persist();
        }
    }

    private static string NormalizeModelKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private void EnsureLoaded()
    {
        if (_cached is not null)
        {
            return;
        }

        try
        {
            if (!File.Exists(_storePath))
            {
                _cached = new Dictionary<string, ModelCalibration>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var json = File.ReadAllText(_storePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, ModelCalibration>>(json, JsonOptions)
                         ?? new Dictionary<string, ModelCalibration>(StringComparer.OrdinalIgnoreCase);

            _cached = new Dictionary<string, ModelCalibration>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in parsed)
            {
                var normalized = NormalizeModelKey(pair.Key);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                var fullAnchor = pair.Value.FullAnchorRawMetric;
                if (fullAnchor <= 0 || double.IsNaN(fullAnchor) || double.IsInfinity(fullAnchor))
                {
                    continue;
                }

                _cached[normalized] = pair.Value with
                {
                    ModelKey = normalized
                };
            }
        }
        catch
        {
            _cached = new Dictionary<string, ModelCalibration>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        var json = JsonSerializer.Serialize(_cached, JsonOptions);
        File.WriteAllText(_storePath, json);
    }
}
