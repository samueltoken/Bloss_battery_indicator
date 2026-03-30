using System.Text.Json;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public sealed class PendingGamepadCandidateStore
{
    private sealed class StorePayload
    {
        public Dictionary<string, PendingGamepadCandidate> Votes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, DateTimeOffset> Cooldowns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly TimeSpan DefaultVoteTtl = TimeSpan.FromDays(14);

    private readonly object _sync = new();
    private readonly string _storePath;
    private StorePayload? _cached;

    public PendingGamepadCandidateStore(string? storePath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storePath = storePath ?? Path.Combine(appData, "Bloss", "gamepad-candidate-votes.json");
    }

    public string StorePath => _storePath;

    public int RegisterVote(
        string modelKey,
        string candidateKey,
        int score,
        DateTimeOffset timestamp,
        string evidenceType = "unknown",
        string lastValidationStats = "")
    {
        lock (_sync)
        {
            EnsureLoaded();
            PruneExpired(timestamp);

            var normalizedModel = NormalizeKey(modelKey);
            var normalizedCandidate = NormalizeVoteCandidateKey(candidateKey, normalizedModel);
            var scopedModel = ResolveModelScopeKey(normalizedModel, normalizedCandidate);
            var voteKey = BuildVoteKey(scopedModel, normalizedCandidate);

            if (_cached!.Votes.TryGetValue(voteKey, out var existing))
            {
                var mergedEvidenceType = MergeEvidenceType(existing.EvidenceType, evidenceType);
                _cached.Votes[voteKey] = existing with
                {
                    Score = Math.Max(existing.Score, score),
                    VoteCount = existing.VoteCount + 1,
                    LastSeenAt = timestamp,
                    EvidenceType = mergedEvidenceType,
                    LastValidationStats = string.IsNullOrWhiteSpace(lastValidationStats)
                        ? existing.LastValidationStats
                        : lastValidationStats.Trim()
                };
            }
            else
            {
                _cached.Votes[voteKey] = new PendingGamepadCandidate(
                    ModelKey: scopedModel,
                    CandidateKey: normalizedCandidate,
                    Score: Math.Max(0, score),
                    VoteCount: 1,
                    FirstSeenAt: timestamp,
                    LastSeenAt: timestamp,
                    EvidenceType: NormalizeEvidenceType(evidenceType),
                    LastValidationStats: NormalizeValidationStats(lastValidationStats));
            }

            Persist();
            return _cached.Votes[voteKey].VoteCount;
        }
    }

    public void ClearVotesForModel(string modelKey)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var normalized = NormalizeKey(modelKey);

            var keys = _cached!.Votes
                .Where(pair => string.Equals(pair.Value.ModelKey, normalized, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToList();

            foreach (var key in keys)
            {
                _cached.Votes.Remove(key);
            }

            if (keys.Count > 0)
            {
                Persist();
            }
        }
    }

    public bool IsInCooldown(string modelKey, DateTimeOffset now)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var normalized = NormalizeKey(modelKey);
            if (!_cached!.Cooldowns.TryGetValue(normalized, out var until))
            {
                return false;
            }

            if (until > now)
            {
                return true;
            }

            _cached.Cooldowns.Remove(normalized);
            Persist();
            return false;
        }
    }

    public void SetCooldown(string modelKey, TimeSpan duration, DateTimeOffset now)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var normalized = NormalizeKey(modelKey);
            _cached!.Cooldowns[normalized] = now + duration;
            Persist();
        }
    }

    private static string BuildVoteKey(string modelKey, string candidateKey)
    {
        return $"{modelKey}|{candidateKey}";
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "UNKNOWN"
            : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeVoteCandidateKey(string? candidateKey, string normalizedModelKey)
    {
        var source = string.IsNullOrWhiteSpace(candidateKey)
            ? string.Empty
            : candidateKey.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(source))
        {
            source = "IDK_IDENTITY_NONE|RID_00|OFF_0|DEC_UNKNOWN";
        }

        var reportId = ExtractTokenValue(source, "RID_") ?? "00";
        if (reportId.Length == 1)
        {
            reportId = $"0{reportId}";
        }

        var offsetValue = ExtractTokenValue(source, "OFF_");
        var offset = 0;
        if (!string.IsNullOrWhiteSpace(offsetValue) && int.TryParse(offsetValue, out var parsedOffset))
        {
            offset = Math.Max(0, parsedOffset);
        }

        var decoder = ExtractTokenValue(source, "DEC_") ?? "UNKNOWN";
        var identity = ExtractIdentityToken(source);
        identity = NormalizeIdentityToken(identity, normalizedModelKey);

        return $"IDK_{identity}|RID_{reportId}|OFF_{offset}|DEC_{decoder}";
    }

    private static string ExtractIdentityToken(string source)
    {
        var idkIndex = source.IndexOf("IDK_", StringComparison.Ordinal);
        if (idkIndex < 0)
        {
            return string.Empty;
        }

        var ridSeparator = source.IndexOf("|RID_", idkIndex, StringComparison.Ordinal);
        if (ridSeparator < 0)
        {
            var tail = source[(idkIndex + 4)..].Trim();
            return string.IsNullOrWhiteSpace(tail) ? string.Empty : tail;
        }

        var length = ridSeparator - (idkIndex + 4);
        if (length <= 0)
        {
            return string.Empty;
        }

        return source.Substring(idkIndex + 4, length).Trim();
    }

    private static string? ExtractTokenValue(string source, string tokenPrefix)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(tokenPrefix))
        {
            return null;
        }

        var tokenIndex = source.LastIndexOf($"|{tokenPrefix}", StringComparison.Ordinal);
        var tokenStart = tokenIndex >= 0
            ? tokenIndex + 1
            : source.StartsWith(tokenPrefix, StringComparison.Ordinal) ? 0 : -1;
        if (tokenStart < 0)
        {
            return null;
        }

        var valueStart = tokenStart + tokenPrefix.Length;
        if (valueStart >= source.Length)
        {
            return null;
        }

        var valueEnd = source.IndexOf('|', valueStart);
        if (valueEnd < 0)
        {
            valueEnd = source.Length;
        }

        var value = source[valueStart..valueEnd].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string NormalizeIdentityToken(string? identityToken, string normalizedModelKey)
    {
        if (string.IsNullOrWhiteSpace(identityToken))
        {
            return $"MODEL_{normalizedModelKey}";
        }

        var normalized = identityToken.Trim().ToUpperInvariant();
        if (string.Equals(normalized, "IDENTITY_NONE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "IDENTITY_UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            return $"MODEL_{normalizedModelKey}";
        }

        var segments = normalized
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(segment =>
            {
                if (segment.StartsWith("LEN_", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (segment.StartsWith("ID=", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("TR=", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("FP=", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("EP=", StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith("MODEL_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            })
            .ToList();
        if (segments.Count == 0)
        {
            return $"MODEL_{normalizedModelKey}";
        }

        normalized = string.Join("|", segments);
        if (normalized.StartsWith("MODEL_VID_", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("VID_", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("PID_", StringComparison.OrdinalIgnoreCase))
        {
            return $"MODEL_{normalizedModelKey}";
        }

        return normalized;
    }

    private static string ResolveModelScopeKey(string normalizedModelKey, string normalizedCandidateKey)
    {
        var identity = ExtractIdentityToken(normalizedCandidateKey);
        if (string.IsNullOrWhiteSpace(identity))
        {
            return normalizedModelKey;
        }

        var normalizedIdentity = identity.Trim().ToUpperInvariant();
        if (normalizedIdentity.StartsWith("MODEL_", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedIdentity, "IDENTITY_NONE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedIdentity, "IDENTITY_UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedModelKey;
        }

        return normalizedIdentity;
    }

    private static string NormalizeEvidenceType(string? evidenceType)
    {
        if (string.IsNullOrWhiteSpace(evidenceType))
        {
            return "unknown";
        }

        var normalized = evidenceType.Trim().ToLowerInvariant();
        return normalized is "dedicated" or "generic"
            ? normalized
            : "unknown";
    }

    private static string NormalizeValidationStats(string? stats)
    {
        return string.IsNullOrWhiteSpace(stats)
            ? string.Empty
            : stats.Trim();
    }

    private static string MergeEvidenceType(string existing, string incoming)
    {
        var normalizedExisting = NormalizeEvidenceType(existing);
        var normalizedIncoming = NormalizeEvidenceType(incoming);
        if (normalizedExisting == "dedicated" || normalizedIncoming == "dedicated")
        {
            return "dedicated";
        }

        if (normalizedExisting == "generic" || normalizedIncoming == "generic")
        {
            return "generic";
        }

        return "unknown";
    }

    private void PruneExpired(DateTimeOffset now)
    {
        var expiredVotes = _cached!.Votes
            .Where(pair => now - pair.Value.LastSeenAt > DefaultVoteTtl)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expiredVotes)
        {
            _cached.Votes.Remove(key);
        }

        var expiredCooldown = _cached.Cooldowns
            .Where(pair => pair.Value <= now)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expiredCooldown)
        {
            _cached.Cooldowns.Remove(key);
        }
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
                _cached = new StorePayload();
                return;
            }

            var json = File.ReadAllText(_storePath);
            _cached = JsonSerializer.Deserialize<StorePayload>(json, JsonOptions) ?? new StorePayload();
            _cached.Votes ??= new Dictionary<string, PendingGamepadCandidate>(StringComparer.OrdinalIgnoreCase);
            _cached.Cooldowns ??= new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
            if (MigrateLegacyVoteKeys())
            {
                Persist();
            }
        }
        catch
        {
            _cached = new StorePayload();
        }
    }

    private bool MigrateLegacyVoteKeys()
    {
        if (_cached is null)
        {
            return false;
        }

        var changed = false;
        var mergedVotes = new Dictionary<string, PendingGamepadCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var vote in _cached.Votes.Values)
        {
            var normalizedModel = NormalizeKey(vote.ModelKey);
            var normalizedCandidate = NormalizeVoteCandidateKey(vote.CandidateKey, normalizedModel);
            var scopedModel = ResolveModelScopeKey(normalizedModel, normalizedCandidate);
            var mergedKey = BuildVoteKey(scopedModel, normalizedCandidate);

            var normalizedVote = vote with
            {
                ModelKey = scopedModel,
                CandidateKey = normalizedCandidate,
                Score = Math.Max(0, vote.Score),
                VoteCount = Math.Max(1, vote.VoteCount),
                FirstSeenAt = vote.FirstSeenAt == default ? vote.LastSeenAt : vote.FirstSeenAt,
                LastSeenAt = vote.LastSeenAt == default ? vote.FirstSeenAt : vote.LastSeenAt,
                EvidenceType = NormalizeEvidenceType(vote.EvidenceType),
                LastValidationStats = NormalizeValidationStats(vote.LastValidationStats)
            };

            if (normalizedVote.LastSeenAt < normalizedVote.FirstSeenAt)
            {
                normalizedVote = normalizedVote with
                {
                    FirstSeenAt = normalizedVote.LastSeenAt,
                    LastSeenAt = normalizedVote.FirstSeenAt
                };
            }

            if (mergedVotes.TryGetValue(mergedKey, out var existing))
            {
                var merged = existing with
                {
                    Score = Math.Max(existing.Score, normalizedVote.Score),
                    VoteCount = existing.VoteCount + normalizedVote.VoteCount,
                    FirstSeenAt = existing.FirstSeenAt <= normalizedVote.FirstSeenAt ? existing.FirstSeenAt : normalizedVote.FirstSeenAt,
                    LastSeenAt = existing.LastSeenAt >= normalizedVote.LastSeenAt ? existing.LastSeenAt : normalizedVote.LastSeenAt,
                    EvidenceType = MergeEvidenceType(existing.EvidenceType, normalizedVote.EvidenceType),
                    LastValidationStats = string.IsNullOrWhiteSpace(normalizedVote.LastValidationStats)
                        ? existing.LastValidationStats
                        : normalizedVote.LastValidationStats
                };
                mergedVotes[mergedKey] = merged;
                changed = true;
                continue;
            }

            mergedVotes[mergedKey] = normalizedVote;
            if (!string.Equals(vote.ModelKey, scopedModel, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(vote.CandidateKey, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                changed = true;
            }
        }

        var mergedCooldowns = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        foreach (var cooldown in _cached.Cooldowns)
        {
            var normalizedModel = NormalizeKey(cooldown.Key);
            if (mergedCooldowns.TryGetValue(normalizedModel, out var existingUntil))
            {
                if (cooldown.Value > existingUntil)
                {
                    mergedCooldowns[normalizedModel] = cooldown.Value;
                }
            }
            else
            {
                mergedCooldowns[normalizedModel] = cooldown.Value;
            }

            if (!string.Equals(cooldown.Key, normalizedModel, StringComparison.OrdinalIgnoreCase))
            {
                changed = true;
            }
        }

        if (_cached.Votes.Count != mergedVotes.Count || _cached.Cooldowns.Count != mergedCooldowns.Count)
        {
            changed = true;
        }

        _cached.Votes = mergedVotes;
        if (ConsolidateModelVotesIntoIdentity(_cached.Votes))
        {
            changed = true;
        }
        _cached.Cooldowns = mergedCooldowns;
        return changed;
    }

    private static bool ConsolidateModelVotesIntoIdentity(Dictionary<string, PendingGamepadCandidate> votes)
    {
        if (votes.Count == 0)
        {
            return false;
        }

        var changed = false;
        var groups = votes
            .Select(entry => new
            {
                entry.Key,
                Vote = entry.Value,
                Signature = BuildCandidateSignature(entry.Value.CandidateKey),
                IsModelScoped = entry.Value.ModelKey.StartsWith("VID_", StringComparison.OrdinalIgnoreCase) ||
                                entry.Value.ModelKey.StartsWith("MODEL_", StringComparison.OrdinalIgnoreCase)
            })
            .GroupBy(entry => entry.Signature, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                continue;
            }

            var identityTarget = group
                .Where(entry => !entry.IsModelScoped)
                .OrderByDescending(entry => entry.Vote.VoteCount)
                .ThenByDescending(entry => entry.Vote.Score)
                .FirstOrDefault();
            if (identityTarget is null)
            {
                continue;
            }

            var modelEntries = group.Where(entry => entry.IsModelScoped).ToList();
            foreach (var modelEntry in modelEntries)
            {
                if (!votes.TryGetValue(identityTarget.Key, out var existingIdentityVote) ||
                    !votes.TryGetValue(modelEntry.Key, out var modelVote))
                {
                    continue;
                }

                var merged = existingIdentityVote with
                {
                    VoteCount = existingIdentityVote.VoteCount + Math.Max(1, modelVote.VoteCount),
                    Score = Math.Max(existingIdentityVote.Score, modelVote.Score),
                    FirstSeenAt = existingIdentityVote.FirstSeenAt <= modelVote.FirstSeenAt ? existingIdentityVote.FirstSeenAt : modelVote.FirstSeenAt,
                    LastSeenAt = existingIdentityVote.LastSeenAt >= modelVote.LastSeenAt ? existingIdentityVote.LastSeenAt : modelVote.LastSeenAt,
                    EvidenceType = MergeEvidenceType(existingIdentityVote.EvidenceType, modelVote.EvidenceType),
                    LastValidationStats = string.IsNullOrWhiteSpace(modelVote.LastValidationStats)
                        ? existingIdentityVote.LastValidationStats
                        : modelVote.LastValidationStats
                };

                votes[identityTarget.Key] = merged;
                votes.Remove(modelEntry.Key);
                changed = true;
            }
        }

        return changed;
    }

    private static string BuildCandidateSignature(string candidateKey)
    {
        var reportId = ExtractTokenValue(candidateKey, "RID_") ?? "00";
        var offset = ExtractTokenValue(candidateKey, "OFF_") ?? "0";
        var decoder = ExtractTokenValue(candidateKey, "DEC_") ?? "UNKNOWN";
        return $"RID_{reportId}|OFF_{offset}|DEC_{decoder}";
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(_cached, JsonOptions);
        File.WriteAllText(_storePath, json);
    }
}
