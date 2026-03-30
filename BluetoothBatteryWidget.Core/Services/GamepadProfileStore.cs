using System.Text.Json;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public sealed class GamepadProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const int DecodeMismatchQuarantineThreshold = 2;
    private const int RecoverySuccessThreshold = 2;

    private readonly object _sync = new();
    private readonly string _storePath;
    private readonly string _quarantinePath;
    private readonly string _healthPath;
    private Dictionary<string, GamepadBatteryProfile>? _cachedProfiles;
    private Dictionary<string, GamepadBatteryProfile>? _cachedQuarantine;
    private Dictionary<string, GamepadProfileHealthState>? _cachedHealth;

    public GamepadProfileStore(string? storePath = null, string? quarantinePath = null, string? healthPath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storePath = storePath ?? Path.Combine(appData, "Bloss", "gamepad-profiles.json");
        _quarantinePath = quarantinePath ?? Path.Combine(appData, "Bloss", "gamepad-profiles-quarantine.json");
        _healthPath = healthPath ?? Path.Combine(appData, "Bloss", "gamepad-profile-health.json");
    }

    public string StorePath => _storePath;
    public string QuarantineStorePath => _quarantinePath;
    public string HealthStorePath => _healthPath;

    public IReadOnlyList<GamepadBatteryProfile> LoadAll()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return _cachedProfiles!.Values
                .Where(profile => profile.State == GamepadProfileState.Active)
                .ToList();
        }
    }

    public IReadOnlyList<GamepadBatteryProfile> LoadQuarantined()
    {
        lock (_sync)
        {
            EnsureQuarantineLoaded();
            return _cachedQuarantine!.Values.ToList();
        }
    }

    public GamepadProfileHealthState GetHealthState(GamepadBatteryProfile profile)
    {
        lock (_sync)
        {
            EnsureHealthLoaded();
            var key = BuildProfileKey(profile);
            if (_cachedHealth!.TryGetValue(key, out var existing))
            {
                return existing;
            }

            return new GamepadProfileHealthState();
        }
    }

    public ProfileStateTransition RegisterRevalidationFailure(
        GamepadBatteryProfile profile,
        RevalidationFailureKind failureKind,
        DateTimeOffset observedAt)
    {
        lock (_sync)
        {
            EnsureLoaded();
            EnsureQuarantineLoaded();
            EnsureHealthLoaded();

            var normalized = Normalize(profile);
            var key = BuildProfileKey(normalized);
            var beforeState = ResolveProfileStateByKey(key);
            var health = _cachedHealth!.TryGetValue(key, out var existing)
                ? existing
                : new GamepadProfileHealthState();
            var nextHealth = failureKind switch
            {
                RevalidationFailureKind.NoSignal => health with
                {
                    NoSignalStrike = health.NoSignalStrike + 1,
                    ConsecutiveSuccessCount = 0
                },
                RevalidationFailureKind.WeakSignal => health with
                {
                    WeakSignalStrike = health.WeakSignalStrike + 1,
                    ConsecutiveSuccessCount = 0
                },
                RevalidationFailureKind.DecodeMismatch => health with
                {
                    MismatchStrike = health.MismatchStrike + 1,
                    ConsecutiveSuccessCount = 0
                },
                RevalidationFailureKind.SpreadOutlier => health with
                {
                    WeakSignalStrike = health.WeakSignalStrike + 1,
                    ConsecutiveSuccessCount = 0
                },
                _ => health with { ConsecutiveSuccessCount = 0 }
            };

            var afterState = beforeState;
            if (failureKind == RevalidationFailureKind.DecodeMismatch &&
                nextHealth.MismatchStrike >= DecodeMismatchQuarantineThreshold)
            {
                var active = normalized with { State = GamepadProfileState.Active };
                QuarantineInternal(active);
                afterState = GamepadProfileState.Quarantined;
            }

            _cachedHealth[key] = nextHealth;
            PersistHealth();
            return new ProfileStateTransition(beforeState, afterState, failureKind, nextHealth);
        }
    }

    public ProfileStateTransition RegisterRevalidationSuccess(GamepadBatteryProfile profile, DateTimeOffset observedAt)
    {
        lock (_sync)
        {
            EnsureLoaded();
            EnsureQuarantineLoaded();
            EnsureHealthLoaded();

            var normalized = Normalize(profile);
            var key = BuildProfileKey(normalized);
            var beforeState = ResolveProfileStateByKey(key);
            var health = _cachedHealth!.TryGetValue(key, out var existing)
                ? existing
                : new GamepadProfileHealthState();
            var nextHealth = health with
            {
                NoSignalStrike = 0,
                WeakSignalStrike = 0,
                MismatchStrike = 0,
                ConsecutiveSuccessCount = health.ConsecutiveSuccessCount + 1,
                LastHealthyAt = observedAt
            };

            var afterState = beforeState;
            if (beforeState == GamepadProfileState.Quarantined &&
                nextHealth.ConsecutiveSuccessCount >= RecoverySuccessThreshold)
            {
                _cachedQuarantine!.Remove(key);
                _cachedProfiles![key] = normalized with { State = GamepadProfileState.Active };
                nextHealth = nextHealth with { ConsecutiveSuccessCount = 0 };
                Persist();
                PersistQuarantine();
                afterState = GamepadProfileState.Active;
            }

            _cachedHealth[key] = nextHealth;
            PersistHealth();
            return new ProfileStateTransition(beforeState, afterState, RevalidationFailureKind.None, nextHealth);
        }
    }

    public void Upsert(GamepadBatteryProfile profile)
    {
        lock (_sync)
        {
            EnsureLoaded();
            EnsureQuarantineLoaded();
            EnsureHealthLoaded();

            var normalized = Normalize(profile);
            var key = BuildProfileKey(normalized);
            if (ShouldQuarantineByPolicy(normalized))
            {
                _cachedProfiles!.Remove(key);
                _cachedQuarantine![key] = normalized with { State = GamepadProfileState.Quarantined };
            }
            else
            {
                _cachedProfiles![key] = normalized with { State = GamepadProfileState.Active };
                _cachedQuarantine!.Remove(key);
            }
            _cachedHealth![key] = new GamepadProfileHealthState();

            Persist();
            PersistQuarantine();
            PersistHealth();
        }
    }

    public void Quarantine(GamepadBatteryProfile profile)
    {
        lock (_sync)
        {
            EnsureLoaded();
            EnsureQuarantineLoaded();
            EnsureHealthLoaded();
            QuarantineInternal(profile);
        }
    }

    public bool TryGetBestForModel(string vendorId, string productId, out GamepadBatteryProfile profile)
    {
        lock (_sync)
        {
            EnsureLoaded();
            EnsureQuarantineLoaded();
            EnsureHealthLoaded();

            var normalizedVendor = NormalizeId(vendorId);
            var normalizedProduct = NormalizeId(productId);
            var matched = _cachedProfiles!.Values
                .Where(value =>
                    string.Equals(value.VendorId, normalizedVendor, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value.ProductId, normalizedProduct, StringComparison.OrdinalIgnoreCase) &&
                    value.State == GamepadProfileState.Active)
                .OrderByDescending(value => value.Score)
                .ToList();

            foreach (var candidate in matched)
            {
                if (ShouldQuarantineByPolicy(candidate))
                {
                    QuarantineInternal(candidate);
                    continue;
                }

                profile = candidate;
                return true;
            }

            profile = null!;
            return false;
        }
    }

    public bool TryGetBestForIdentity(
        string identityKey,
        string vendorId,
        string productId,
        out GamepadBatteryProfile profile)
    {
        lock (_sync)
        {
            EnsureLoaded();
            EnsureQuarantineLoaded();
            EnsureHealthLoaded();

            var normalizedIdentity = NormalizeIdentityKey(identityKey);
            var normalizedVendor = NormalizeId(vendorId);
            var normalizedProduct = NormalizeId(productId);
            var exactIdentity = _cachedProfiles!.Values
                .Where(value =>
                    string.Equals(value.IdentityKey, normalizedIdentity, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value.VendorId, normalizedVendor, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value.ProductId, normalizedProduct, StringComparison.OrdinalIgnoreCase) &&
                    value.State == GamepadProfileState.Active)
                .OrderByDescending(value => value.Score)
                .ToList();

            foreach (var candidate in exactIdentity)
            {
                if (ShouldQuarantineByPolicy(candidate))
                {
                    QuarantineInternal(candidate);
                    continue;
                }

                profile = candidate;
                return true;
            }

            if (TryGetBestForModel(vendorId, productId, out profile))
            {
                return true;
            }

            var fallbackFromQuarantine = _cachedQuarantine!.Values
                .Where(value =>
                    string.Equals(value.IdentityKey, normalizedIdentity, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value.VendorId, normalizedVendor, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value.ProductId, normalizedProduct, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(value => value.Score)
                .FirstOrDefault();
            if (fallbackFromQuarantine is not null)
            {
                profile = fallbackFromQuarantine with { State = GamepadProfileState.Quarantined };
                return true;
            }

            profile = null!;
            return false;
        }
    }

    public static string BuildProfileKey(GamepadBatteryProfile profile)
    {
        var normalized = Normalize(profile);
        var identityToken = string.IsNullOrWhiteSpace(normalized.IdentityKey)
            ? "NONE"
            : normalized.IdentityKey;
        return $"VID_{normalized.VendorId}|PID_{normalized.ProductId}|RID_{normalized.ReportId:X2}|LEN_{normalized.ReportLength}|OFF_{normalized.Offset}|DEC_{normalized.Decoder}|IDK_{identityToken}";
    }

    public static string BuildModelKey(string vendorId, string productId)
    {
        return $"VID_{NormalizeId(vendorId)}|PID_{NormalizeId(productId)}";
    }

    private static GamepadBatteryProfile Normalize(GamepadBatteryProfile profile)
    {
        var vendor = NormalizeId(profile.VendorId);
        var product = NormalizeId(profile.ProductId);
        var decoder = string.IsNullOrWhiteSpace(profile.Decoder)
            ? GamepadProbeCandidateEvaluator.DecoderPercent100
            : profile.Decoder.Trim().ToLowerInvariant();

        return profile with
        {
            VendorId = vendor,
            ProductId = product,
            Decoder = decoder,
            IdentityKey = NormalizeIdentityKey(profile.IdentityKey),
            ReportLength = Math.Max(1, profile.ReportLength),
            Offset = Math.Max(0, profile.Offset),
            Score = Math.Max(0, profile.Score),
            Confidence = profile.Confidence is BatteryConfidence.Confirmed or BatteryConfidence.Estimated
                ? profile.Confidence
                : BatteryConfidence.Confirmed,
            State = profile.State is GamepadProfileState.Active or GamepadProfileState.Quarantined
                ? profile.State
                : GamepadProfileState.Active
        };
    }

    private static string NormalizeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0000";
        }

        Span<char> buffer = stackalloc char[4];
        var index = 0;
        foreach (var ch in value)
        {
            if (!Uri.IsHexDigit(ch))
            {
                continue;
            }

            if (index >= 4)
            {
                break;
            }

            buffer[index] = char.ToUpperInvariant(ch);
            index++;
        }

        if (index == 0)
        {
            return "0000";
        }

        if (index < 4)
        {
            return new string(buffer[..index]).PadLeft(4, '0');
        }

        return new string(buffer);
    }

    private static string NormalizeIdentityKey(string? identityKey)
    {
        if (string.IsNullOrWhiteSpace(identityKey))
        {
            return string.Empty;
        }

        return identityKey.Trim().ToUpperInvariant();
    }

    private GamepadProfileState ResolveProfileStateByKey(string key)
    {
        if (_cachedProfiles!.ContainsKey(key))
        {
            return GamepadProfileState.Active;
        }

        if (_cachedQuarantine!.ContainsKey(key))
        {
            return GamepadProfileState.Quarantined;
        }

        return GamepadProfileState.Active;
    }

    private void EnsureLoaded()
    {
        if (_cachedProfiles is not null)
        {
            return;
        }

        try
        {
            if (!File.Exists(_storePath))
            {
                _cachedProfiles = new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var json = File.ReadAllText(_storePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, GamepadBatteryProfile>>(json, JsonOptions)
                         ?? new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);

            _cachedProfiles = new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in parsed)
            {
                var normalized = Normalize(entry.Value);
                var key = BuildProfileKey(normalized);
                if (ShouldQuarantineByPolicy(normalized))
                {
                    EnsureQuarantineLoaded();
                    _cachedQuarantine![key] = normalized with { State = GamepadProfileState.Quarantined };
                    continue;
                }

                _cachedProfiles[key] = normalized with { State = GamepadProfileState.Active };
            }

            PersistQuarantine();
        }
        catch
        {
            _cachedProfiles = new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void EnsureQuarantineLoaded()
    {
        if (_cachedQuarantine is not null)
        {
            return;
        }

        try
        {
            if (!File.Exists(_quarantinePath))
            {
                _cachedQuarantine = new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var json = File.ReadAllText(_quarantinePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, GamepadBatteryProfile>>(json, JsonOptions)
                         ?? new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);

            _cachedQuarantine = new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in parsed)
            {
                var normalized = Normalize(entry.Value);
                var key = BuildProfileKey(normalized);
                _cachedQuarantine[key] = normalized with { State = GamepadProfileState.Quarantined };
            }
        }
        catch
        {
            _cachedQuarantine = new Dictionary<string, GamepadBatteryProfile>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void EnsureHealthLoaded()
    {
        if (_cachedHealth is not null)
        {
            return;
        }

        try
        {
            if (!File.Exists(_healthPath))
            {
                _cachedHealth = new Dictionary<string, GamepadProfileHealthState>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var json = File.ReadAllText(_healthPath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, GamepadProfileHealthState>>(json, JsonOptions)
                         ?? new Dictionary<string, GamepadProfileHealthState>(StringComparer.OrdinalIgnoreCase);
            _cachedHealth = new Dictionary<string, GamepadProfileHealthState>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in parsed)
            {
                _cachedHealth[entry.Key] = NormalizeHealth(entry.Value);
            }
        }
        catch
        {
            _cachedHealth = new Dictionary<string, GamepadProfileHealthState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static GamepadProfileHealthState NormalizeHealth(GamepadProfileHealthState? value)
    {
        if (value is null)
        {
            return new GamepadProfileHealthState();
        }

        return value with
        {
            NoSignalStrike = Math.Max(0, value.NoSignalStrike),
            WeakSignalStrike = Math.Max(0, value.WeakSignalStrike),
            MismatchStrike = Math.Max(0, value.MismatchStrike),
            ConsecutiveSuccessCount = Math.Max(0, value.ConsecutiveSuccessCount)
        };
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(_cachedProfiles, JsonOptions);
        File.WriteAllText(_storePath, json);
    }

    private void PersistQuarantine()
    {
        var directory = Path.GetDirectoryName(_quarantinePath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(_cachedQuarantine, JsonOptions);
        File.WriteAllText(_quarantinePath, json);
    }

    private void PersistHealth()
    {
        var directory = Path.GetDirectoryName(_healthPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(_cachedHealth, JsonOptions);
        File.WriteAllText(_healthPath, json);
    }

    private void QuarantineInternal(GamepadBatteryProfile profile)
    {
        var normalized = Normalize(profile);
        var key = BuildProfileKey(normalized);

        _cachedProfiles!.Remove(key);
        _cachedQuarantine![key] = normalized with { State = GamepadProfileState.Quarantined };
        if (_cachedHealth is not null && _cachedHealth.TryGetValue(key, out var health))
        {
            _cachedHealth[key] = health with { ConsecutiveSuccessCount = 0 };
            PersistHealth();
        }
        Persist();
        PersistQuarantine();
    }

    private static bool ShouldQuarantineByPolicy(GamepadBatteryProfile profile)
    {
        if (!string.Equals(profile.VendorId, "045E", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isGenericDecoder = profile.Decoder is
            GamepadProbeCandidateEvaluator.DecoderPercent100 or
            GamepadProbeCandidateEvaluator.DecoderPercent255 or
            GamepadProbeCandidateEvaluator.DecoderNibble10;
        if (!isGenericDecoder)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(profile.IdentityKey);
    }
}
