using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public sealed class BatteryEvidenceResolver
{
    private static readonly TimeSpan StabilityWindow = TimeSpan.FromDays(7);
    private const int ConflictThreshold = 35;
    private const int GameInputFixedLowPercent = 10;
    private const int GameInputLowPercentThreshold = 20;
    private const int GameInputFixedPatternMinSamples = 3;
    private const int GameInputFixedPatternWindow = 5;
    private const int GameInputHighAnchorThreshold = 85;
    private const int GameInputSevereDropThreshold = 55;

    private readonly BatteryObservationStore _observationStore;
    private readonly CalibrationStore _calibrationStore;

    public BatteryEvidenceResolver(
        BatteryObservationStore observationStore,
        CalibrationStore calibrationStore)
    {
        _observationStore = observationStore;
        _calibrationStore = calibrationStore;
    }

    public IReadOnlyList<PnpBatteryReading> ResolveAndRecord(
        IReadOnlyList<PnpBatteryReading> rawReadings,
        DateTimeOffset now)
    {
        var normalizedCandidates = rawReadings
            .Select(candidate => NormalizeCandidate(candidate, now))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Address))
            .ToList();

        var evidences = normalizedCandidates
            .Where(candidate =>
                candidate.BatteryPercent is not null &&
                !string.IsNullOrWhiteSpace(candidate.ModelKey) &&
                candidate.SourceKind != BatterySourceKind.Unknown)
            .Select(candidate => new BatteryEvidence(
                Address: candidate.Address,
                ModelKey: candidate.ModelKey!,
                SourceKind: candidate.SourceKind,
                DerivedPercent: candidate.BatteryPercent!.Value,
                RawMetric: candidate.RawMetric,
                ObservedAt: candidate.ObservedAt ?? now))
            .ToList();

        _observationStore.Record(evidences, now);

        var results = new List<PnpBatteryReading>();
        foreach (var group in normalizedCandidates.GroupBy(candidate => candidate.Address, StringComparer.OrdinalIgnoreCase))
        {
            var resolved = ResolveAddressGroup(group.ToList(), now);
            if (resolved is not null)
            {
                results.Add(resolved);
            }
        }

        return results;
    }

    private PnpBatteryReading? ResolveAddressGroup(
        List<PnpBatteryReading> candidates,
        DateTimeOffset now)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var evaluated = candidates
            .Select(candidate => EvaluateCandidate(candidate, now))
            .OrderByDescending(item => item.Score)
            .ToList();

        var nonNullCandidates = evaluated
            .Where(item => item.Reading.BatteryPercent is not null)
            .ToList();

        if (nonNullCandidates.Count > 1)
        {
            var min = nonNullCandidates.Min(item => item.Reading.BatteryPercent ?? 0);
            var max = nonNullCandidates.Max(item => item.Reading.BatteryPercent ?? 0);
            if (max - min >= ConflictThreshold)
            {
                var trusted = nonNullCandidates.OrderByDescending(item => item.Score).First();
                var canTrustOutput = trusted.Score >= 90;
                if (!canTrustOutput)
                {
                    var fallback = evaluated
                        .Where(item => item.Reading.SuggestCalibration || item.Reading.SourceKind == BatterySourceKind.GameInput)
                        .OrderByDescending(item => item.Score)
                        .FirstOrDefault();
                    if (fallback is null)
                    {
                        return trusted.Reading with
                        {
                            BatteryPercent = null,
                            SuggestCalibration = false,
                            IsBatterySuspect = true
                        };
                    }

                    return fallback.Reading with
                    {
                        BatteryPercent = null,
                        BatteryConfidence = BatteryConfidence.Estimated,
                        SuggestCalibration = fallback.Reading.SuggestCalibration || fallback.Reading.SourceKind == BatterySourceKind.GameInput,
                        IsBatterySuspect = true
                    };
                }
            }
        }

        var bestWithPercent = evaluated.FirstOrDefault(item => item.Reading.BatteryPercent is not null);
        if (bestWithPercent is not null)
        {
            return bestWithPercent.Reading;
        }

        var bestFallback = evaluated.First();
        return bestFallback.Reading with
        {
            SuggestCalibration = bestFallback.Reading.SuggestCalibration || bestFallback.Reading.SourceKind == BatterySourceKind.GameInput,
            IsBatterySuspect = bestFallback.Reading.SourceKind == BatterySourceKind.GameInput
        };
    }

    private EvaluatedCandidate EvaluateCandidate(PnpBatteryReading candidate, DateTimeOffset now)
    {
        var reading = ApplyCalibration(candidate);
        var confidence = reading.BatteryConfidence;
        var suggestCalibration = reading.SuggestCalibration;
        var isBatterySuspect = reading.IsBatterySuspect;

        if (reading.SourceKind == BatterySourceKind.GameInput)
        {
            var hasCalibration = !string.IsNullOrWhiteSpace(reading.ModelKey) &&
                                 _calibrationStore.TryGet(reading.ModelKey, out _);
            var recent = string.IsNullOrWhiteSpace(reading.ModelKey)
                ? new List<BatteryEvidence>()
                : _observationStore
                    .GetRecentForModel(reading.ModelKey, BatterySourceKind.GameInput, now)
                    .Where(item => now - item.ObservedAt <= StabilityWindow)
                    .ToList();
            var reliabilityStage = EvaluateGameInputReliability(reading, recent, hasCalibration);

            if (reliabilityStage != GameInputReliability.Use)
            {
                reading = reading with
                {
                    BatteryPercent = null,
                    BatteryConfidence = BatteryConfidence.Estimated,
                    SuggestCalibration = true,
                    IsBatterySuspect = true
                };

                return new EvaluatedCandidate(reading, CalculateScore(reading));
            }

            var confirmed = IsStableEnough(recent);
            if (confirmed)
            {
                confidence = BatteryConfidence.Confirmed;
            }
            else
            {
                confidence = hasCalibration
                    ? BatteryConfidence.Confirmed
                    : BatteryConfidence.Estimated;
            }

            if (!hasCalibration && confidence == BatteryConfidence.Estimated)
            {
                suggestCalibration = true;
                isBatterySuspect = true;
            }
        }
        else if (reading.SourceKind == BatterySourceKind.SetupApi && reading.BatteryPercent is not null)
        {
            confidence = BatteryConfidence.Estimated;
        }

        reading = reading with
        {
            BatteryConfidence = confidence,
            SuggestCalibration = suggestCalibration,
            IsBatterySuspect = isBatterySuspect
        };

        return new EvaluatedCandidate(reading, CalculateScore(reading));
    }

    private PnpBatteryReading ApplyCalibration(PnpBatteryReading candidate)
    {
        if (candidate.RawMetric is null || candidate.RawMetric <= 0)
        {
            return candidate;
        }

        if (string.IsNullOrWhiteSpace(candidate.ModelKey))
        {
            return candidate;
        }

        if (!_calibrationStore.TryGet(candidate.ModelKey, out var calibration))
        {
            return candidate with
            {
                SuggestCalibration = candidate.SourceKind == BatterySourceKind.GameInput
            };
        }

        if (calibration.FullAnchorRawMetric <= 0)
        {
            return candidate;
        }

        var calibratedPercent = (int)Math.Round(
            candidate.RawMetric.Value / calibration.FullAnchorRawMetric * 100d,
            MidpointRounding.AwayFromZero);
        calibratedPercent = Math.Clamp(calibratedPercent, 0, 100);

        return candidate with
        {
            BatteryPercent = calibratedPercent,
            BatteryConfidence = BatteryConfidence.Confirmed,
            SuggestCalibration = false,
            IsBatterySuspect = false
        };
    }

    private static bool IsStableEnough(IReadOnlyList<BatteryEvidence> observations)
    {
        if (observations.Count < 3)
        {
            return false;
        }

        var ordered = observations
            .OrderBy(item => item.ObservedAt)
            .TakeLast(5)
            .ToList();

        for (var index = 1; index < ordered.Count; index++)
        {
            var delta = Math.Abs(ordered[index].DerivedPercent - ordered[index - 1].DerivedPercent);
            if (delta > 45)
            {
                return false;
            }
        }

        var min = ordered.Min(item => item.DerivedPercent);
        var max = ordered.Max(item => item.DerivedPercent);
        return max - min <= 35;
    }

    private static int CalculateScore(PnpBatteryReading reading)
    {
        var score = reading.SourceKind switch
        {
            BatterySourceKind.SonyHid => 100,
            BatterySourceKind.XInput => 95,
            BatterySourceKind.BleGatt => 92,
            BatterySourceKind.HidFeature => 88,
            BatterySourceKind.LearnedHid => 82,
            BatterySourceKind.GameInput => 65,
            BatterySourceKind.SetupApi => 55,
            _ => 40
        };

        if (reading.BatteryConfidence == BatteryConfidence.Estimated)
        {
            score -= 18;
        }

        if (reading.SourceKind == BatterySourceKind.GameInput && reading.BatteryPercent is null)
        {
            score -= 24;
        }

        if (reading.SourceKind == BatterySourceKind.GameInput && !reading.SuggestCalibration)
        {
            score += 8;
        }

        return score;
    }

    private static GameInputReliability EvaluateGameInputReliability(
        PnpBatteryReading reading,
        IReadOnlyList<BatteryEvidence> recent,
        bool hasCalibration)
    {
        if (reading.BatteryPercent is null ||
            string.IsNullOrWhiteSpace(reading.ModelKey) ||
            reading.RawMetric is null ||
            reading.RawMetric <= 0)
        {
            return GameInputReliability.Block;
        }

        if (hasCalibration)
        {
            return GameInputReliability.Use;
        }

        var currentPercent = reading.BatteryPercent.Value;
        if (LooksLikeFixedLowPattern(currentPercent, recent) ||
            LooksLikeSevereDrop(currentPercent, recent))
        {
            return GameInputReliability.Block;
        }

        if (currentPercent <= GameInputLowPercentThreshold)
        {
            return GameInputReliability.Hold;
        }

        return GameInputReliability.Use;
    }

    private static bool LooksLikeFixedLowPattern(int currentPercent, IReadOnlyList<BatteryEvidence> recent)
    {
        if (currentPercent != GameInputFixedLowPercent)
        {
            return false;
        }

        var sampled = recent
            .OrderBy(item => item.ObservedAt)
            .TakeLast(GameInputFixedPatternWindow)
            .Select(item => item.DerivedPercent)
            .ToList();
        if (sampled.Count < GameInputFixedPatternMinSamples)
        {
            return false;
        }

        var min = sampled.Min();
        var max = sampled.Max();
        return max - min <= 2 &&
               sampled.All(value => value >= GameInputFixedLowPercent - 1 && value <= GameInputFixedLowPercent + 1);
    }

    private static bool LooksLikeSevereDrop(int currentPercent, IReadOnlyList<BatteryEvidence> recent)
    {
        if (currentPercent > GameInputLowPercentThreshold)
        {
            return false;
        }

        var sampled = recent
            .OrderBy(item => item.ObservedAt)
            .TakeLast(GameInputFixedPatternWindow + 1)
            .Select(item => item.DerivedPercent)
            .ToList();
        if (sampled.Count < 2)
        {
            return false;
        }

        var previousHigh = sampled
            .Take(sampled.Count - 1)
            .LastOrDefault(value => value >= GameInputHighAnchorThreshold);
        if (previousHigh < GameInputHighAnchorThreshold)
        {
            return false;
        }

        return previousHigh - currentPercent >= GameInputSevereDropThreshold;
    }

    private static PnpBatteryReading NormalizeCandidate(PnpBatteryReading candidate, DateTimeOffset now)
    {
        var normalizedAddress = AddressNormalizer.NormalizeAddress(candidate.Address);
        var batteryPercent = candidate.BatteryPercent;
        if (batteryPercent is < 0 or > 100)
        {
            batteryPercent = null;
        }

        var rawMetric = candidate.RawMetric;
        if (rawMetric is not null && (double.IsNaN(rawMetric.Value) || double.IsInfinity(rawMetric.Value)))
        {
            rawMetric = null;
        }

        var modelKey = string.IsNullOrWhiteSpace(candidate.ModelKey)
            ? string.Empty
            : candidate.ModelKey.Trim().ToUpperInvariant();

        return candidate with
        {
            Address = normalizedAddress,
            BatteryPercent = batteryPercent,
            RawMetric = rawMetric,
            ModelKey = modelKey,
            ObservedAt = candidate.ObservedAt ?? now
        };
    }

    private sealed record EvaluatedCandidate(PnpBatteryReading Reading, int Score);

    private enum GameInputReliability
    {
        Use,
        Hold,
        Block
    }
}
