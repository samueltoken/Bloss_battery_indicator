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
    private const int GameInputFixedHighPercent = 100;
    private const int GameInputHighPercentThreshold = 98;
    private const int GameInputFixedHighPatternMinSamples = 4;
    private const int GameInputFixedHighPatternWindow = 6;
    private const int GameInputHighAnchorThreshold = 85;
    private const int GameInputSevereDropThreshold = 55;
    private const int SteamTritonSuspiciousFullPercent = 100;
    private const string SteamTritonPuckModelMarker = "STEAM_TRITON_PUCK";
    private const string SteamTritonRecentNonFullHoldReason = "steam_triton_recent_nonfull_hold";
    private const string SteamTritonDockedFullPendingReason = "steam_triton_docked_full_pending";
    private const string SteamTritonVoltageEstimatedChargingReason = "steam_triton_voltage_estimated_charging";
    private const string SteamTritonChargeCompleteLatchedReason = "steam_triton_charge_complete_latched";
    private const string SteamControllerBluetoothChargeCompleteLatchedReason = "steam_controller_bluetooth_charge_complete_latched";
    private const int SteamTritonWirelessNearFullMinPercent = 95;
    private const int SteamTritonWirelessNearFullMaxPercent = 99;
    private static readonly string[] SteamTritonPuckAnchorModelKeys =
    [
        "USB\\VID_28DE&PID_1304\\STEAM_TRITON_PUCK",
        "USB\\VID_28DE&PID_1305\\STEAM_TRITON_PUCK"
    ];
    private static readonly TimeSpan SteamTritonDockedFullArtifactWindow = TimeSpan.FromHours(12);
    private static readonly TimeSpan SteamTritonChargeCompleteLatchWindow = TimeSpan.FromHours(12);

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
        var adjustedCandidates = normalizedCandidates
            .Select(candidate => ApplySteamControllerChargeCompleteLatch(candidate, now))
            .Select(candidate => ApplySteamTritonDockedFullGuard(candidate, now))
            .ToList();

        var evidences = adjustedCandidates
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
                ObservedAt: candidate.ObservedAt ?? now,
                IsCharging: candidate.IsCharging,
                IsChargeComplete: candidate.IsChargeComplete,
                ReasonCode: candidate.ReasonCode))
            .ToList();

        _observationStore.Record(evidences, now);

        var results = new List<PnpBatteryReading>();
        foreach (var group in adjustedCandidates.GroupBy(candidate => candidate.Address, StringComparer.OrdinalIgnoreCase))
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

        candidates = ApplySteamTritonDockedFullGroupGuard(candidates);

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
                            IsBatterySuspect = true,
                            DisplayState = BatteryDisplayState.NA,
                            ReasonCode = "conflict_hold_no_trusted",
                            ReliabilityScore = Math.Clamp(trusted.Score, 0, 100)
                        };
                    }

                    return fallback.Reading with
                    {
                        BatteryPercent = null,
                        BatteryConfidence = BatteryConfidence.Estimated,
                        SuggestCalibration = fallback.Reading.SuggestCalibration || fallback.Reading.SourceKind == BatterySourceKind.GameInput,
                        IsBatterySuspect = true,
                        DisplayState = BatteryDisplayState.NA,
                        ReasonCode = string.IsNullOrWhiteSpace(fallback.Reading.ReasonCode)
                            ? "conflict_hold_fallback"
                            : fallback.Reading.ReasonCode,
                        ReliabilityScore = Math.Clamp(fallback.Score, 0, 100)
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
            IsBatterySuspect = bestFallback.Reading.IsBatterySuspect || bestFallback.Reading.SourceKind == BatterySourceKind.GameInput,
            DisplayState = bestFallback.Reading.IsCharging
                ? BatteryDisplayState.Charging
                : BatteryDisplayState.NA,
            ReasonCode = string.IsNullOrWhiteSpace(bestFallback.Reading.ReasonCode)
                ? "no_percent_fallback"
                : bestFallback.Reading.ReasonCode,
            ReliabilityScore = Math.Clamp(bestFallback.Score, 0, 100)
        };
    }

    private static List<PnpBatteryReading> ApplySteamTritonDockedFullGroupGuard(List<PnpBatteryReading> candidates)
    {
        var steamPending = candidates.FirstOrDefault(IsSteamTritonDockedFullPending);
        if (steamPending is null)
        {
            var steamVoltageEstimate = candidates.FirstOrDefault(IsSteamTritonVoltageEstimatedCharging);
            if (steamVoltageEstimate is null)
            {
                return candidates;
            }

            return candidates
                .Where(candidate =>
                    candidate.BatteryPercent != SteamTritonSuspiciousFullPercent ||
                    candidate.SourceKind == BatterySourceKind.SteamHid)
                .ToList();
        }

        var filtered = candidates
            .Where(candidate =>
                candidate.BatteryPercent != SteamTritonSuspiciousFullPercent ||
                candidate.SourceKind == BatterySourceKind.SteamHid)
            .ToList();

        if (filtered.Any(candidate => candidate.BatteryPercent is not null))
        {
            return filtered;
        }

        return [steamPending];
    }

    private static bool IsSteamTritonDockedFullPending(PnpBatteryReading candidate)
    {
        return candidate.SourceKind == BatterySourceKind.SteamHid &&
               candidate.BatteryPercent is null &&
               candidate.IsCharging &&
               candidate.IsBatterySuspect &&
               string.Equals(candidate.ReasonCode, SteamTritonDockedFullPendingReason, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSteamTritonVoltageEstimatedCharging(PnpBatteryReading candidate)
    {
        return candidate.SourceKind == BatterySourceKind.SteamHid &&
               candidate.BatteryPercent is not null &&
               candidate.IsCharging &&
               string.Equals(candidate.ReasonCode, SteamTritonVoltageEstimatedChargingReason, StringComparison.OrdinalIgnoreCase);
    }

    private EvaluatedCandidate EvaluateCandidate(PnpBatteryReading candidate, DateTimeOffset now)
    {
        var reading = IsChargeCompleteLatchedReason(candidate.ReasonCode)
            ? candidate
            : ApplyCalibration(candidate);
        var confidence = reading.BatteryConfidence;
        var suggestCalibration = reading.SuggestCalibration;
        var isBatterySuspect = reading.IsBatterySuspect;
        var reasonCode = string.IsNullOrWhiteSpace(reading.ReasonCode) ? "candidate_input" : reading.ReasonCode;

        if (IsChargeCompleteLatchedReason(reasonCode) && reading.BatteryPercent is not null)
        {
            confidence = BatteryConfidence.Confirmed;
            suggestCalibration = false;
            isBatterySuspect = false;
        }
        else if (reading.SourceKind == BatterySourceKind.GameInput)
        {
            var hasCalibration = !string.IsNullOrWhiteSpace(reading.ModelKey) &&
                                 _calibrationStore.TryGet(reading.ModelKey, out _);
            var recent = string.IsNullOrWhiteSpace(reading.ModelKey)
                ? new List<BatteryEvidence>()
                : _observationStore
                    .GetRecentForModel(reading.ModelKey, BatterySourceKind.GameInput, now)
                    .Where(item => string.Equals(
                        AddressNormalizer.NormalizeAddress(item.Address),
                        reading.Address,
                        StringComparison.OrdinalIgnoreCase))
                    .Where(item => now - item.ObservedAt <= StabilityWindow)
                    .ToList();
            var reliabilityStage = EvaluateGameInputReliability(reading, recent, hasCalibration, reading.IsBatterySuspect);

            if (reliabilityStage != GameInputReliability.Use)
            {
                reasonCode = reliabilityStage == GameInputReliability.Hold
                    ? "gameinput_hold_low_confidence"
                    : "gameinput_block_unreliable";
                reading = reading with
                {
                    BatteryPercent = null,
                    BatteryConfidence = BatteryConfidence.Estimated,
                    SuggestCalibration = true,
                    IsBatterySuspect = true
                };

                var blockedScore = CalculateScore(reading);
                reading = ApplyDisplayMetadata(reading, blockedScore, reasonCode);
                return new EvaluatedCandidate(reading, blockedScore);
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
                reasonCode = "gameinput_estimated_unverified";
            }
            else
            {
                reasonCode = confidence == BatteryConfidence.Confirmed
                    ? "gameinput_confirmed"
                    : "gameinput_estimated";
            }
        }
        else if (reading.SourceKind == BatterySourceKind.SetupApi && reading.BatteryPercent is not null)
        {
            confidence = BatteryConfidence.Estimated;
            reasonCode = "setupapi_estimated";
        }
        else if (reading.BatteryPercent is not null)
        {
            if (!string.Equals(reasonCode, SteamTritonRecentNonFullHoldReason, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(reasonCode, SteamTritonVoltageEstimatedChargingReason, StringComparison.OrdinalIgnoreCase) &&
                !IsChargeCompleteLatchedReason(reasonCode))
            {
                reasonCode = $"{reading.SourceKind.ToString().ToLowerInvariant()}_direct";
            }
        }

        reading = reading with
        {
            BatteryConfidence = confidence,
            SuggestCalibration = suggestCalibration,
            IsBatterySuspect = isBatterySuspect
        };

        var score = CalculateScore(reading);
        reading = ApplyDisplayMetadata(reading, score, reasonCode);
        return new EvaluatedCandidate(reading, score);
    }

    private PnpBatteryReading ApplySteamControllerChargeCompleteLatch(PnpBatteryReading candidate, DateTimeOffset now)
    {
        if (candidate.BatteryPercent is < SteamTritonWirelessNearFullMinPercent or > SteamTritonWirelessNearFullMaxPercent ||
            candidate.IsCharging ||
            candidate.IsChargeComplete ||
            !HasNearFullRawMetric(candidate))
        {
            return candidate;
        }

        if (candidate.SourceKind == BatterySourceKind.SteamHid && IsSteamTritonPuckModel(candidate.ModelKey))
        {
            var samePuckChargeComplete = FindSamePuckChargeCompleteAnchor(candidate, now);
            return samePuckChargeComplete is null
                ? candidate
                : ApplyChargeCompleteLatch(candidate, SteamTritonChargeCompleteLatchedReason);
        }

        if (IsSteamControllerBluetoothCandidate(candidate))
        {
            var recentPuckChargeComplete = FindRecentPuckChargeCompleteAnchor(now);
            return recentPuckChargeComplete is null
                ? candidate
                : ApplyChargeCompleteLatch(candidate, SteamControllerBluetoothChargeCompleteLatchedReason);
        }

        return candidate;
    }

    private BatteryEvidence? FindSamePuckChargeCompleteAnchor(PnpBatteryReading candidate, DateTimeOffset now)
    {
        var modelKey = candidate.ModelKey ?? string.Empty;
        var normalizedAddress = AddressNormalizer.NormalizeAddress(candidate.Address);
        if (string.IsNullOrWhiteSpace(modelKey) || string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return null;
        }

        return _observationStore
            .GetRecentForModel(modelKey, BatterySourceKind.SteamHid, now)
            .Where(item =>
                string.Equals(
                    AddressNormalizer.NormalizeAddress(item.Address),
                    normalizedAddress,
                    StringComparison.OrdinalIgnoreCase))
            .Where(item => IsRecentChargeCompleteAnchor(item, now))
            .OrderByDescending(item => item.ObservedAt)
            .FirstOrDefault();
    }

    private BatteryEvidence? FindRecentPuckChargeCompleteAnchor(DateTimeOffset now)
    {
        return SteamTritonPuckAnchorModelKeys
            .SelectMany(modelKey => _observationStore.GetRecentForModel(modelKey, BatterySourceKind.SteamHid, now))
            .Where(item => IsRecentChargeCompleteAnchor(item, now))
            .OrderByDescending(item => item.ObservedAt)
            .FirstOrDefault();
    }

    private static bool IsRecentChargeCompleteAnchor(BatteryEvidence item, DateTimeOffset now)
    {
        return now - item.ObservedAt <= SteamTritonChargeCompleteLatchWindow &&
               item.DerivedPercent == SteamTritonSuspiciousFullPercent &&
               item.RawMetric is null or <= SteamTritonSuspiciousFullPercent &&
               item.IsChargeComplete;
    }

    private static PnpBatteryReading ApplyChargeCompleteLatch(PnpBatteryReading candidate, string reasonCode)
    {
        return candidate with
        {
            BatteryPercent = SteamTritonSuspiciousFullPercent,
            BatteryConfidence = BatteryConfidence.Confirmed,
            IsCharging = false,
            IsChargeComplete = false,
            IsBatterySuspect = false,
            ReasonCode = reasonCode
        };
    }

    private PnpBatteryReading ApplySteamTritonDockedFullGuard(PnpBatteryReading candidate, DateTimeOffset now)
    {
        if (candidate.SourceKind != BatterySourceKind.SteamHid ||
            candidate.BatteryPercent != SteamTritonSuspiciousFullPercent ||
            !candidate.IsCharging ||
            candidate.IsChargeComplete ||
            candidate.RawMetric is null or <= SteamTritonSuspiciousFullPercent ||
            !IsSteamTritonPuckModel(candidate.ModelKey))
        {
            return candidate;
        }

        var modelKey = candidate.ModelKey ?? string.Empty;
        var normalizedAddress = AddressNormalizer.NormalizeAddress(candidate.Address);
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return candidate;
        }

        var recentNonFull = _observationStore
            .GetRecentForModel(modelKey, BatterySourceKind.SteamHid, now)
            .Where(item =>
                string.Equals(
                    AddressNormalizer.NormalizeAddress(item.Address),
                    normalizedAddress,
                    StringComparison.OrdinalIgnoreCase))
            .Where(item => now - item.ObservedAt <= SteamTritonDockedFullArtifactWindow)
            .Where(item => item.DerivedPercent > 0 && item.DerivedPercent < SteamTritonSuspiciousFullPercent)
            .Where(item => item.RawMetric is null || item.RawMetric <= SteamTritonSuspiciousFullPercent)
            .OrderByDescending(item => item.ObservedAt)
            .FirstOrDefault();

        if (recentNonFull is null)
        {
            if (TryEstimateSteamTritonPercentFromRawMetric(candidate.RawMetric, out var estimatedPercent))
            {
                return candidate with
                {
                    BatteryPercent = estimatedPercent,
                    BatteryConfidence = BatteryConfidence.Estimated,
                    IsBatterySuspect = false,
                    ReasonCode = SteamTritonVoltageEstimatedChargingReason
                };
            }

            return candidate with
            {
                BatteryPercent = null,
                BatteryConfidence = BatteryConfidence.Estimated,
                IsBatterySuspect = true,
                ReasonCode = SteamTritonDockedFullPendingReason
            };
        }

        return candidate with
        {
            BatteryPercent = recentNonFull.DerivedPercent,
            RawMetric = recentNonFull.RawMetric ?? recentNonFull.DerivedPercent,
            BatteryConfidence = BatteryConfidence.Confirmed,
            IsBatterySuspect = false,
            ReasonCode = SteamTritonRecentNonFullHoldReason
        };
    }

    private static bool TryEstimateSteamTritonPercentFromRawMetric(double? rawMetric, out int batteryPercent)
    {
        batteryPercent = 0;
        if (rawMetric is null)
        {
            return false;
        }

        var millivolts = (int)Math.Round(rawMetric.Value, MidpointRounding.AwayFromZero);
        return SteamControllerBatteryEstimator.TryEstimatePercentFromVoltage(millivolts, out batteryPercent);
    }

    private static bool IsSteamTritonPuckModel(string? modelKey)
    {
        return !string.IsNullOrWhiteSpace(modelKey) &&
               modelKey.Contains(SteamTritonPuckModelMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChargeCompleteLatchedReason(string? reasonCode)
    {
        return string.Equals(reasonCode, SteamTritonChargeCompleteLatchedReason, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reasonCode, SteamControllerBluetoothChargeCompleteLatchedReason, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNearFullRawMetric(PnpBatteryReading candidate)
    {
        if (candidate.RawMetric is null)
        {
            return true;
        }

        if (candidate.SourceKind == BatterySourceKind.GameInput)
        {
            return true;
        }

        return candidate.RawMetric is >= SteamTritonWirelessNearFullMinPercent and <= SteamTritonWirelessNearFullMaxPercent;
    }

    private static bool IsSteamControllerBluetoothCandidate(PnpBatteryReading candidate)
    {
        if (!IsSteamControllerBluetoothSource(candidate.SourceKind))
        {
            return false;
        }

        if (ContainsSteamControllerBluetoothVidPid(candidate.ModelKey) ||
            ContainsSteamControllerBluetoothVidPid(candidate.InstanceId))
        {
            return true;
        }

        return candidate.DisplayName.Contains("Steam Ctrl (BT)", StringComparison.OrdinalIgnoreCase) ||
               (candidate.DisplayName.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) &&
                candidate.DisplayName.Contains("(BT)", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSteamControllerBluetoothSource(BatterySourceKind sourceKind)
    {
        return sourceKind is BatterySourceKind.BleGatt or
            BatterySourceKind.HidFeature or
            BatterySourceKind.LearnedHid or
            BatterySourceKind.GameInput or
            BatterySourceKind.SetupApi;
    }

    private static bool ContainsSteamControllerBluetoothVidPid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hasSteamVendor = value.Contains("VID_28DE", StringComparison.OrdinalIgnoreCase) ||
                             value.Contains("VID&0228DE", StringComparison.OrdinalIgnoreCase) ||
                             value.Contains("VID=28DE", StringComparison.OrdinalIgnoreCase) ||
                             value.Contains("VID=0228DE", StringComparison.OrdinalIgnoreCase);
        var hasBluetoothPid = value.Contains("PID_1303", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("PID&1303", StringComparison.OrdinalIgnoreCase) ||
                              value.Contains("PID=1303", StringComparison.OrdinalIgnoreCase);

        return hasSteamVendor && hasBluetoothPid;
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
            IsBatterySuspect = false,
            ReasonCode = "calibrated_anchor"
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
            BatterySourceKind.SteamHid => 98,
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
        bool hasCalibration,
        bool isMarkedSuspect)
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

        if (isMarkedSuspect &&
            (currentPercent >= GameInputHighPercentThreshold ||
             LooksLikeFixedHighPattern(currentPercent, recent)))
        {
            return GameInputReliability.Hold;
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

    private static bool LooksLikeFixedHighPattern(int currentPercent, IReadOnlyList<BatteryEvidence> recent)
    {
        if (currentPercent < GameInputHighPercentThreshold)
        {
            return false;
        }

        var sampled = recent
            .OrderBy(item => item.ObservedAt)
            .TakeLast(GameInputFixedHighPatternWindow)
            .ToList();
        if (sampled.Count < GameInputFixedHighPatternMinSamples)
        {
            return false;
        }

        var percentMin = sampled.Min(item => item.DerivedPercent);
        var percentMax = sampled.Max(item => item.DerivedPercent);
        if (percentMin < GameInputHighPercentThreshold || percentMax < GameInputFixedHighPercent)
        {
            return false;
        }

        if (percentMax - percentMin > 1)
        {
            return false;
        }

        var rawMetrics = sampled
            .Where(item => item.RawMetric is not null)
            .Select(item => item.RawMetric!.Value)
            .ToList();
        if (rawMetrics.Count < GameInputFixedHighPatternMinSamples - 1)
        {
            return true;
        }

        var rawMin = rawMetrics.Min();
        var rawMax = rawMetrics.Max();
        return rawMax - rawMin <= 1.5d;
    }

    private static PnpBatteryReading ApplyDisplayMetadata(PnpBatteryReading reading, int score, string reasonCode)
    {
        return reading with
        {
            ReliabilityScore = Math.Clamp(score, 0, 100),
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? "resolved" : reasonCode.Trim().ToLowerInvariant(),
            ActiveSource = ResolveActiveSource(reading.SourceKind),
            PathType = ResolvePathType(reading.ModelKey),
            DisplayState = ResolveDisplayState(reading)
        };
    }

    private static string ResolveActiveSource(BatterySourceKind sourceKind)
    {
        return sourceKind switch
        {
            BatterySourceKind.SetupApi => "setupapi",
            BatterySourceKind.GameInput => "gameinput",
            BatterySourceKind.XInput => "xinput",
            BatterySourceKind.SonyHid => "sonyhid",
            BatterySourceKind.SteamHid => "steamhid",
            BatterySourceKind.LearnedHid => "learnedhid",
            BatterySourceKind.BleGatt => "blegatt",
            BatterySourceKind.HidFeature => "hidfeature",
            _ => "unknown"
        };
    }

    private static string ResolvePathType(string? modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return "unknown";
        }

        var normalized = modelKey.ToUpperInvariant();
        if (normalized.Contains("TR=VID_"))
        {
            return "bluetooth";
        }

        if (normalized.Contains("XUSB", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("IG_", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("USB", StringComparison.OrdinalIgnoreCase))
        {
            return "receiver";
        }

        return "unknown";
    }

    private static BatteryDisplayState ResolveDisplayState(PnpBatteryReading reading)
    {
        if (reading.IsCharging)
        {
            return BatteryDisplayState.Charging;
        }

        if (reading.BatteryPercent is null)
        {
            return BatteryDisplayState.NA;
        }

        return reading.BatteryConfidence == BatteryConfidence.Confirmed
            ? BatteryDisplayState.Verified
            : BatteryDisplayState.Estimated;
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
        var reasonCode = string.IsNullOrWhiteSpace(candidate.ReasonCode)
            ? string.Empty
            : candidate.ReasonCode.Trim().ToLowerInvariant();
        var activeSource = string.IsNullOrWhiteSpace(candidate.ActiveSource)
            ? string.Empty
            : candidate.ActiveSource.Trim().ToLowerInvariant();
        var pathType = string.IsNullOrWhiteSpace(candidate.PathType)
            ? string.Empty
            : candidate.PathType.Trim().ToLowerInvariant();
        var displayState = candidate.DisplayState == BatteryDisplayState.Unknown
            ? (batteryPercent is null
                ? BatteryDisplayState.NA
                : candidate.BatteryConfidence == BatteryConfidence.Confirmed
                    ? BatteryDisplayState.Verified
                    : BatteryDisplayState.Estimated)
            : candidate.DisplayState;

        return candidate with
        {
            Address = normalizedAddress,
            BatteryPercent = batteryPercent,
            RawMetric = rawMetric,
            ModelKey = modelKey,
            ObservedAt = candidate.ObservedAt ?? now,
            ReliabilityScore = Math.Clamp(candidate.ReliabilityScore, 0, 100),
            ReasonCode = reasonCode,
            ActiveSource = activeSource,
            PathType = pathType,
            DisplayState = displayState
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
