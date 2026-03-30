using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Text.Json;

namespace BluetoothBatteryWidget.App.Services;

public sealed class GamepadProbeService
{
    private static readonly byte[] ProbeReportIds =
    [
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06,
        0x10, 0x11, 0x12,
        0x20, 0x21, 0x22,
        0x30, 0x31, 0x32,
        0x81, 0x82
    ];
    private static readonly byte[] FeatureProbeReportIds = [0x02, 0x03, 0x05, 0x11, 0x21, 0x31, 0x81, 0x82];

    private static readonly byte[] QuickScanReportIds = [0x01, 0x11, 0x21, 0x31];
    private static readonly int[] StreamTimeoutLadderMs = [180, 260, 420, 700];
    private static readonly int[] QuickTimeoutLadderMs = [180, 260];
    private static readonly int[] ExpandTimeoutLadderMs = [260, 420];
    private static readonly int[] DeepTimeoutLadderMs = [700];

    private const int QuickReadRetryCount = 1;
    private const int ExpandReadRetryCount = 2;
    private const int DeepReadRetryCount = 3;
    private const int EndpointAttemptBudget = 300;
    private const int NoSignalStopAttempts = 120;
    private const int MinimumScoreForDeepPhase = 40;
    private const int ConfirmedScoreThreshold = 70;
    private const int PendingScoreThreshold = 55;
    private const int ImmediateEstimatedScoreThreshold = 60;
    private const int ImmediateEstimatedMinReportRepeats = 2;
    private const int MaxGlobalEndpoints = 12;
    private const int XboxGenericMinimumScore = 60;
    private const int WarmupFrameBudget = 10;
    private const int WarmupTimeoutMs = 120;
    private const int NoSignalRecoveryNoSignalStopAttempts = 180;
    private const int NoSignalRecoveryMinimumScoreForDeepPhase = 20;
    private const long ProbeTraceMaxBytes = 10L * 1024L * 1024L;
    private static readonly TimeSpan HardFailCooldownDuration = TimeSpan.FromMinutes(2);
    private static readonly ProbePolicy ActivePolicy = new(
        XboxGenericAcceptanceMode: XboxGenericAcceptanceMode.AllowEstimated,
        GenericMinScore: XboxGenericMinimumScore,
        RevalidationRule: new RevalidationRule(
            SampleCount: 3,
            MinSuccessCount: 2,
            MaxSpread: 18));
    private static readonly string ProbeTraceLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "probe-traces.jsonl");
    private static readonly string ProcessPath = Environment.ProcessPath ?? string.Empty;
    private static readonly string BuildStamp = ResolveBuildStamp();

    internal static IReadOnlyList<int> StreamTimeoutsForTesting => StreamTimeoutLadderMs;

    private readonly GamepadProfileStore _profileStore;
    private readonly PendingGamepadCandidateStore _pendingCandidateStore;

    public GamepadProbeService(
        GamepadProfileStore profileStore,
        PendingGamepadCandidateStore pendingCandidateStore)
    {
        _profileStore = profileStore;
        _pendingCandidateStore = pendingCandidateStore;
    }

    public Task<ProbeResult> ProbeAsync(
        ConnectedBluetoothDevice connectedDevice,
        Action<ProbeProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var diagnostics = new ProbeDiagnostics();
            var currentStage = ProbeStage.None;
            var normalizedAddress = string.Empty;
            var observedProbeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var traceTopCandidates = new List<string>();
            var traceWinnerDecoder = string.Empty;
            var traceBlockReason = string.Empty;

            try
            {
                currentStage = ProbeStage.DeviceCheck;
                Report(onProgress, ProbeStage.DeviceCheck, ProbeProgressCalculator.DeviceCheck(0), "Checking device...");
                cancellationToken.ThrowIfCancellationRequested();

                normalizedAddress = AddressNormalizer.NormalizeAddress(connectedDevice.Address);
                if (string.IsNullOrWhiteSpace(normalizedAddress))
                {
                    Report(onProgress, ProbeStage.Failed, 100, "Address validation failed");
                    var failure = BuildFailureResult(
                        "Unable to validate Bluetooth address.",
                        currentStage,
                        diagnostics);
                    AppendProbeTrace(connectedDevice, normalizedAddress, currentStage, "address_validation_failed", failure, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return failure;
                }

                var isGamepad = DeviceCategoryClassifier.Classify(connectedDevice.DisplayName, connectedDevice.CategoryHint) == DeviceCategory.Gamepad;
                if (!isGamepad)
                {
                    Report(onProgress, ProbeStage.Failed, 100, "Not a gamepad device");
                    var failure = BuildFailureResult(
                        "Collection is only available for gamepad devices.",
                        currentStage,
                        diagnostics);
                    AppendProbeTrace(connectedDevice, normalizedAddress, currentStage, "not_gamepad", failure, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return failure;
                }

                Report(onProgress, ProbeStage.DeviceCheck, ProbeProgressCalculator.DeviceCheck(1), "Device check complete");

                cancellationToken.ThrowIfCancellationRequested();
                currentStage = ProbeStage.EnumerateInterfaces;
                Report(onProgress, ProbeStage.EnumerateInterfaces, ProbeProgressCalculator.EnumerateInterfaces(0), "Enumerating HID endpoints...");

                var strictEndpoints = HidGamepadAccess
                    .EnumerateProbeEndpoints(normalizedAddress, HidEndpointDiscoveryStage.Strict, cancellationToken)
                    .ToList();
                var relaxedEndpoints = HidGamepadAccess
                    .EnumerateProbeEndpoints(normalizedAddress, HidEndpointDiscoveryStage.Relaxed, cancellationToken)
                    .ToList();
                var globalCandidates = strictEndpoints.Count == 0 && relaxedEndpoints.Count == 0
                    ? HidGamepadAccess
                        .EnumerateProbeEndpoints(addressFilter: null, HidEndpointDiscoveryStage.GlobalAggressive, cancellationToken)
                        .Take(MaxGlobalEndpoints * 3)
                        .ToList()
                    : [];
                var globalEndpoints = globalCandidates
                    .Where(endpoint => string.Equals(
                        AddressNormalizer.NormalizeAddress(endpoint.Address),
                        normalizedAddress,
                        StringComparison.OrdinalIgnoreCase))
                    .Take(MaxGlobalEndpoints)
                    .ToList();
                diagnostics.GlobalExcludedCount = Math.Max(0, globalCandidates.Count - globalEndpoints.Count);

                var selectedEndpoints = strictEndpoints.Count > 0
                    ? DistinctByDevicePath(strictEndpoints.Concat(relaxedEndpoints).ToList())
                    : relaxedEndpoints.Count > 0
                        ? DistinctByDevicePath(relaxedEndpoints)
                        : DistinctByDevicePath(globalEndpoints);

                diagnostics.StrictEndpointCount = strictEndpoints.Count;
                diagnostics.RelaxedEndpointCount = relaxedEndpoints.Count;
                diagnostics.GlobalEndpointCount = globalEndpoints.Count;

                if (selectedEndpoints.Count == 0)
                {
                    Report(onProgress, ProbeStage.Failed, 100, "No HID endpoint found");
                    var failure = BuildFailureResult(
                        BuildNoEndpointMessage(diagnostics),
                        currentStage,
                        diagnostics);
                    AppendProbeTrace(connectedDevice, normalizedAddress, currentStage, "no_endpoint", failure, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return failure;
                }

                Report(
                    onProgress,
                    ProbeStage.EnumerateInterfaces,
                    ProbeProgressCalculator.EnumerateInterfaces(1),
                    $"Endpoints selected: {selectedEndpoints.Count} (strict {diagnostics.StrictEndpointCount}, relaxed {diagnostics.RelaxedEndpointCount}, global {diagnostics.GlobalEndpointCount})");

                cancellationToken.ThrowIfCancellationRequested();
                currentStage = ProbeStage.CollectReports;
                Report(onProgress, ProbeStage.CollectReports, ProbeProgressCalculator.CollectReports(0), "Collecting input reports...");

                var totalAttemptBudget = Math.Max(1, selectedEndpoints.Count * EndpointAttemptBudget);
                var consumedAttempts = 0;
                var endpointSelections = new List<EndpointSelectionCandidate>();

                foreach (var endpoint in selectedEndpoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CollectEndpointCandidate(
                        endpoint,
                        connectedDevice,
                        normalizedAddress,
                        observedProbeKeys,
                        diagnostics,
                        endpointSelections,
                        onProgress,
                        totalAttemptBudget,
                        ref consumedAttempts,
                        recoveryRound: false,
                        cancellationToken);
                }

                diagnostics.RegisterObservedScore(ComputeBestObservedScore(endpointSelections));
                if (ShouldRunNoSignalRecovery(diagnostics.ReportReadSuccessCount, diagnostics.BestObservedScore))
                {
                    diagnostics.NoSignalRecoveryAttempted = true;
                    Report(
                        onProgress,
                        ProbeStage.CollectReports,
                        ProbeProgressCalculator.CollectReports(consumedAttempts / (double)Math.Max(1, totalAttemptBudget)),
                        "No signal detected. Retrying with expanded probe round...");

                    var recoveryEndpoints = BuildRecoveryEndpoints(selectedEndpoints, normalizedAddress, cancellationToken);
                    foreach (var endpoint in recoveryEndpoints)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        CollectEndpointCandidate(
                            endpoint,
                            connectedDevice,
                            normalizedAddress,
                            observedProbeKeys,
                            diagnostics,
                            endpointSelections,
                            onProgress,
                            totalAttemptBudget,
                            ref consumedAttempts,
                            recoveryRound: true,
                            cancellationToken);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                currentStage = ProbeStage.EvaluateCandidates;
                Report(onProgress, ProbeStage.EvaluateCandidates, ProbeProgressCalculator.EvaluateCandidates(0), "Evaluating candidates...");

                var acceptedCandidates = endpointSelections
                    .Where(candidate => candidate.Selection.Winner is not null)
                    .Select(candidate =>
                    {
                        var winner = candidate.Selection.Winner!;
                        var adjustedScore = AdjustScoreBySource(winner.Score, candidate.Endpoint.DiscoveryStage);
                        return candidate with { Selection = candidate.Selection with { Winner = winner with { Score = adjustedScore } } };
                    })
                    .Where(candidate => candidate.Selection.Winner!.Score >= PendingScoreThreshold)
                    .OrderByDescending(candidate => candidate.Selection.Winner!.Score)
                    .ThenBy(candidate => candidate.Endpoint.DiscoveryStage)
                    .ToList();

                if (acceptedCandidates.Count == 0)
                {
                    diagnostics.RegisterObservedScore(ComputeBestObservedScore(endpointSelections));
                    ApplyHardFailCooldownIfNeeded(observedProbeKeys, diagnostics);
                    Report(onProgress, ProbeStage.Failed, 100, "Candidate evaluation failed");
                    diagnostics.TopCandidatesText = BuildTopCandidatesText(endpointSelections, limit: 5);
                    traceTopCandidates = SplitTopCandidates(diagnostics.TopCandidatesText);
                    var failure = BuildFailureResult(
                        BuildEvaluationFailureMessage(diagnostics, endpointSelections),
                        currentStage,
                        diagnostics);
                    AppendProbeTrace(connectedDevice, normalizedAddress, currentStage, "candidate_evaluation_failed", failure, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return failure;
                }

                var rankedCandidates = acceptedCandidates
                    .OrderByDescending(candidate => IsDedicatedXboxCandidate(connectedDevice.DisplayName, candidate.VendorId, candidate.Selection.Winner!))
                    .ThenByDescending(candidate => candidate.Selection.Winner!.Score)
                    .ThenBy(candidate => candidate.Endpoint.DiscoveryStage)
                    .ThenBy(candidate => candidate.Endpoint.DevicePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                diagnostics.TopCandidatesText = BuildTopCandidatesText(rankedCandidates, limit: 5);
                traceTopCandidates = SplitTopCandidates(diagnostics.TopCandidatesText);

                var selected = SelectPreferredCandidate(rankedCandidates, diagnostics, out var selectedSuppressionReason);
                var selectedWinner = selected.Selection.Winner!;
                traceWinnerDecoder = selectedWinner.Decoder;
                diagnostics.WinnerDecoder = traceWinnerDecoder;
                diagnostics.IdentityKey = selected.IdentityKey;
                diagnostics.HandshakeProfileId = selected.HandshakeProfileId;
                diagnostics.DecoderConfidence = CalculateDecoderConfidence(selectedWinner, rankedCandidates);
                diagnostics.SuppressionReason = selectedSuppressionReason;
                diagnostics.ActiveSource = $"probe:{selectedWinner.Decoder}";
                if (!string.IsNullOrWhiteSpace(selectedSuppressionReason))
                {
                    traceBlockReason = selectedSuppressionReason;
                }

                if (string.IsNullOrWhiteSpace(selected.VendorId) || string.IsNullOrWhiteSpace(selected.ProductId))
                {
                    ApplyHardFailCooldownIfNeeded(observedProbeKeys, diagnostics);
                    Report(onProgress, ProbeStage.Failed, 100, "Unable to resolve VID/PID");
                    var failure = BuildFailureResult(
                        "Battery candidate found, but VID/PID could not be resolved for profile saving.",
                        currentStage,
                        diagnostics);
                    AppendProbeTrace(connectedDevice, normalizedAddress, currentStage, "missing_vid_pid", failure, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return failure;
                }

                if (ShouldRejectXboxGenericCandidate(connectedDevice.DisplayName, selected.VendorId, selectedWinner, ActivePolicy, out var rejectReason))
                {
                    diagnostics.BlockReason = rejectReason;
                    diagnostics.SuppressionReason = rejectReason;
                    traceBlockReason = rejectReason;
                    ApplyHardFailCooldownIfNeeded(observedProbeKeys, diagnostics);
                    Report(onProgress, ProbeStage.Failed, 100, "Xbox generic candidate blocked");
                    var rejectMessage = rejectReason == "xbox_generic_score_below_min"
                        ? $"Xbox-layer 일반 후보 점수 {selectedWinner.Score}가 기준 {ActivePolicy.GenericMinScore} 미만입니다."
                        : "Xbox-layer 장치는 전용 배터리 패킷 근거가 필요합니다. 일반 바이트 후보는 저장하지 않습니다.";
                    var failure = BuildFailureResult(
                        rejectMessage,
                        currentStage,
                        diagnostics);
                    AppendProbeTrace(connectedDevice, normalizedAddress, currentStage, "xbox_generic_rejected", failure, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return failure;
                }

                Report(onProgress, ProbeStage.EvaluateCandidates, ProbeProgressCalculator.EvaluateCandidates(1), "Candidate evaluation complete");

                cancellationToken.ThrowIfCancellationRequested();
                currentStage = ProbeStage.PersistProfile;
                Report(onProgress, ProbeStage.PersistProfile, ProbeProgressCalculator.PersistProfile(0), "Saving learned profile...");
                var modelKey = ResolveEndpointModelKey(selected, normalizedAddress, connectedDevice.DisplayName);
                if (string.IsNullOrWhiteSpace(modelKey))
                {
                    modelKey = GamepadProfileStore.BuildModelKey(selected.VendorId, selected.ProductId);
                }
                var voteIdentityKey = ResolveVoteIdentityKey(selected, normalizedAddress, connectedDevice.DisplayName);
                var pendingKey = !string.IsNullOrWhiteSpace(voteIdentityKey) &&
                                 !string.Equals(voteIdentityKey, "IDENTITY_UNKNOWN", StringComparison.OrdinalIgnoreCase)
                    ? voteIdentityKey
                    : modelKey;
                var shouldDelayConfirmation = ShouldDelayImmediateAcceptance(selectedWinner, rankedCandidates, diagnostics) ||
                                              !string.IsNullOrWhiteSpace(selectedSuppressionReason);
                var allowImmediateEstimated = ShouldAllowImmediateEstimatedAcceptance(
                    selectedWinner.Score,
                    diagnostics.GetObservedReportCount(selectedWinner.ReportId),
                    diagnostics.DecoderConfidence,
                    hasStrongCompetingCandidate: HasCompetingHighBatteryCandidate(selectedWinner, rankedCandidates, margin: 10, delta: 50),
                    isSuspiciousXboxLowCandidate: selectedWinner.Decoder == GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags && selectedWinner.BatteryPercent <= 10,
                    suppressionReason: selectedSuppressionReason);
                if (shouldDelayConfirmation && !allowImmediateEstimated && string.IsNullOrWhiteSpace(diagnostics.SuppressionReason))
                {
                    diagnostics.SuppressionReason = "delayed_acceptance_low_confidence";
                    traceBlockReason = diagnostics.SuppressionReason;
                }

                if (selectedWinner.Score >= ConfirmedScoreThreshold && !shouldDelayConfirmation)
                {
                    var confirmedProfile = GamepadProbeCandidateEvaluator.ToProfile(
                        selected.VendorId,
                        selected.ProductId,
                        selectedWinner,
                        identityKey: selected.IdentityKey,
                        confidence: BatteryConfidence.Confirmed);
                    _profileStore.Upsert(confirmedProfile);
                    diagnostics.ProfileStateBefore = GamepadProfileState.Active.ToString();
                    diagnostics.ProfileStateAfter = GamepadProfileState.Active.ToString();
                    diagnostics.AcceptancePath = "confirmed_after_recheck";
                    _pendingCandidateStore.ClearVotesForModel(pendingKey);
                    if (!string.Equals(modelKey, pendingKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _pendingCandidateStore.ClearVotesForModel(modelKey);
                    }

                    Report(onProgress, ProbeStage.Completed, ProbeProgressCalculator.PersistProfile(1), "Collection complete");
                    var success = new ProbeResult(
                        Success: true,
                        BatteryPercent: selectedWinner.BatteryPercent,
                        Message: BuildSuccessMessage(selected, diagnostics),
                        Profile: confirmedProfile,
                        ErrorDetail: null,
                        IsPending: false);
                    AppendProbeTrace(connectedDevice, normalizedAddress, ProbeStage.Completed, "completed_confirmed", success, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return success;
                }

                var estimatedProfile = GamepadProbeCandidateEvaluator.ToProfile(
                    selected.VendorId,
                    selected.ProductId,
                    selectedWinner,
                    identityKey: selected.IdentityKey,
                    confidence: BatteryConfidence.Estimated);
                var evidenceType = IsGenericDecoder(selectedWinner.Decoder) ? "generic" : "dedicated";
                if (allowImmediateEstimated)
                {
                    _profileStore.Upsert(estimatedProfile);
                    diagnostics.ProfileStateBefore = GamepadProfileState.Active.ToString();
                    diagnostics.ProfileStateAfter = GamepadProfileState.Active.ToString();
                    diagnostics.AcceptancePath = "immediate_estimated";
                    _pendingCandidateStore.ClearVotesForModel(pendingKey);
                    if (!string.Equals(modelKey, pendingKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _pendingCandidateStore.ClearVotesForModel(modelKey);
                    }

                    Report(onProgress, ProbeStage.Completed, ProbeProgressCalculator.PersistProfile(1), "Collection complete (estimated immediate)");
                    var immediateEstimated = new ProbeResult(
                        Success: true,
                        BatteryPercent: selectedWinner.BatteryPercent,
                        Message: "수집 완료 (estimated 즉시 적용, 백그라운드 재검증 진행).",
                        Profile: estimatedProfile,
                        ErrorDetail: null,
                        IsPending: false);
                    AppendProbeTrace(connectedDevice, normalizedAddress, ProbeStage.Completed, "completed_immediate_estimated", immediateEstimated, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return immediateEstimated;
                }

                if (IsXboxLayerCandidate(connectedDevice.DisplayName, selected.VendorId) &&
                    IsGenericDecoder(selectedWinner.Decoder) &&
                    selectedWinner.Score >= ActivePolicy.GenericMinScore &&
                    ActivePolicy.XboxGenericAcceptanceMode == XboxGenericAcceptanceMode.AllowEstimated &&
                    !shouldDelayConfirmation)
                {
                    _profileStore.Upsert(estimatedProfile);
                    diagnostics.ProfileStateBefore = GamepadProfileState.Active.ToString();
                    diagnostics.ProfileStateAfter = GamepadProfileState.Active.ToString();
                    diagnostics.AcceptancePath = "immediate_estimated";
                    _pendingCandidateStore.ClearVotesForModel(pendingKey);
                    if (!string.Equals(modelKey, pendingKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _pendingCandidateStore.ClearVotesForModel(modelKey);
                    }
                    Report(onProgress, ProbeStage.Completed, ProbeProgressCalculator.PersistProfile(1), "Collection complete (xbox generic estimated)");
                    var immediateEstimated = new ProbeResult(
                        Success: true,
                        BatteryPercent: selectedWinner.BatteryPercent,
                        Message: $"수집 완료 (estimated, runtime revalidation {ActivePolicy.RevalidationRule.MinSuccessCount}/{ActivePolicy.RevalidationRule.SampleCount}).",
                        Profile: estimatedProfile,
                        ErrorDetail: null,
                        IsPending: false);
                    AppendProbeTrace(connectedDevice, normalizedAddress, ProbeStage.Completed, "completed_xbox_generic_estimated", immediateEstimated, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return immediateEstimated;
                }

                var candidateKey = BuildVoteCandidateKey(
                    voteIdentityKey,
                    selectedWinner.Decoder,
                    selectedWinner.ReportId,
                    selectedWinner.Offset);
                var votes = _pendingCandidateStore.RegisterVote(
                    pendingKey,
                    candidateKey,
                    selectedWinner.Score,
                    DateTimeOffset.Now,
                    evidenceType: evidenceType,
                    lastValidationStats: $"decoder={selectedWinner.Decoder};score={selectedWinner.Score}");
                var requiredVotes = RequiresExtraVotes(selected, selectedWinner, diagnostics, selectedSuppressionReason)
                    ? 3
                    : 2;
                var learningDecision = ResolveLearningDecision(selectedWinner.Score, votes, requiredVotes);

                if (learningDecision.PersistProfile)
                {
                    _profileStore.Upsert(estimatedProfile);
                    diagnostics.ProfileStateBefore = GamepadProfileState.Active.ToString();
                    diagnostics.ProfileStateAfter = GamepadProfileState.Active.ToString();
                    diagnostics.AcceptancePath = "confirmed_after_recheck";
                    _pendingCandidateStore.ClearVotesForModel(pendingKey);
                    if (!string.Equals(modelKey, pendingKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _pendingCandidateStore.ClearVotesForModel(modelKey);
                    }

                    Report(onProgress, ProbeStage.Completed, ProbeProgressCalculator.PersistProfile(1), "Collection complete (estimated)");
                    var persistedEstimated = new ProbeResult(
                        Success: true,
                        BatteryPercent: selectedWinner.BatteryPercent,
                        Message: "수집 완료 (재측정 검증 2/2).",
                        Profile: estimatedProfile,
                        ErrorDetail: null,
                        IsPending: false);
                    AppendProbeTrace(connectedDevice, normalizedAddress, ProbeStage.Completed, "completed_estimated", persistedEstimated, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                    return persistedEstimated;
                }

                Report(onProgress, ProbeStage.Completed, ProbeProgressCalculator.PersistProfile(1), "Collection pending verification");
                var voteTarget = Math.Max(2, requiredVotes);
                var pending = new ProbeResult(
                    Success: true,
                    BatteryPercent: null,
                    Message: $"임시 후보 저장 ({votes}/{voteTarget}). 동일 기기에서 추가 수집 후 적용됩니다.",
                    Profile: estimatedProfile,
                    ErrorDetail: null,
                    IsPending: learningDecision.IsPending);
                AppendProbeTrace(connectedDevice, normalizedAddress, ProbeStage.Completed, "completed_pending_vote", pending, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                return pending;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                diagnostics.RegisterException(currentStage, ex, "Unhandled probe exception");
                // cooldown is applied only when we observed model keys and hard-fail storm happened.
                Report(onProgress, ProbeStage.Failed, 100, "Probe failed with exception");
                var failure = BuildFailureResult(
                    "수집 처리 중 예외가 발생했습니다.",
                    currentStage,
                    diagnostics,
                    ex);
                AppendProbeTrace(connectedDevice, normalizedAddress, currentStage, "exception", failure, diagnostics, observedProbeKeys, traceTopCandidates, traceWinnerDecoder, traceBlockReason);
                return failure;
            }
        }, cancellationToken);
    }

    private void CollectEndpointCandidate(
        HidGamepadEndpoint endpoint,
        ConnectedBluetoothDevice connectedDevice,
        string normalizedAddress,
        ISet<string> observedProbeKeys,
        ProbeDiagnostics diagnostics,
        List<EndpointSelectionCandidate> endpointSelections,
        Action<ProbeProgress>? onProgress,
        int totalAttemptBudget,
        ref int consumedAttempts,
        bool recoveryRound,
        CancellationToken cancellationToken)
    {
        try
        {
            using var handle = HidGamepadAccess.OpenHandle(endpoint.DevicePath);
            if (handle.IsInvalid)
            {
                diagnostics.HandleOpenFailureCount++;
                consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
                ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, "open failure");
                return;
            }

            diagnostics.HandleOpenSuccessCount++;

            var identityVendorId = endpoint.VendorId;
            var identityProductId = endpoint.ProductId;
            var transportVendorId = endpoint.VendorId;
            var transportProductId = endpoint.ProductId;
            if (HidGamepadAccess.TryGetDeviceAttributes(handle, out var attrVid, out var attrPid))
            {
                transportVendorId = attrVid;
                transportProductId = attrPid;
            }

            var vendorId = !string.IsNullOrWhiteSpace(identityVendorId) ? identityVendorId : transportVendorId;
            var productId = !string.IsNullOrWhiteSpace(identityProductId) ? identityProductId : transportProductId;
            var endpointModelKey = BatteryModelKeyResolver.ResolveNormalizedModelKey(
                identityVendorId,
                identityProductId,
                transportVendorId,
                transportProductId,
                normalizedAddress,
                connectedDevice.DisplayName);
            if (string.IsNullOrWhiteSpace(endpointModelKey) &&
                !string.IsNullOrWhiteSpace(vendorId) &&
                !string.IsNullOrWhiteSpace(productId))
            {
                endpointModelKey = GamepadProfileStore.BuildModelKey(vendorId, productId);
            }

            var endpointSignature = BatteryModelKeyResolver.BuildEndpointSignature(
                endpoint.InstanceId,
                endpoint.DevicePath);
            var endpointIdentityKey = BatteryModelKeyResolver.ResolveIdentityKey(
                identityVendorId,
                identityProductId,
                transportVendorId,
                transportProductId,
                normalizedAddress,
                connectedDevice.DisplayName,
                endpointSignature);
            var probeKey = !string.IsNullOrWhiteSpace(endpointIdentityKey)
                ? endpointIdentityKey
                : endpointModelKey;

            if (!string.IsNullOrWhiteSpace(probeKey))
            {
                observedProbeKeys.Add(probeKey);
                if (_pendingCandidateStore.IsInCooldown(probeKey, DateTimeOffset.Now))
                {
                    diagnostics.CooldownSkipCount++;
                    consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
                    ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, "cooldown skip");
                    return;
                }
            }

            var endpointSignal = $"{endpoint.InstanceId} {endpoint.DevicePath}";
            var handshakeSelection = ThirdPartyHandshakeProfileCatalog.ResolveSelection(
                vendorId,
                productId,
                connectedDevice.DisplayName,
                endpointSignal,
                normalizedAddress);
            var handshakeProfile = handshakeSelection.Profile;
            diagnostics.RegisterHandshakeSelection(
                handshakeSelection.BrandHint,
                handshakeProfile.ProfileId,
                handshakeSelection.ProfileSelectionReason);

            var endpointAddress = AddressNormalizer.NormalizeAddress(endpoint.Address);
            if (!string.IsNullOrWhiteSpace(endpointAddress) &&
                !string.Equals(endpointAddress, normalizedAddress, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.RegisterEndpointSkip("address-mismatch");
                consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
                ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, "skip address mismatch");
                return;
            }

            if (endpoint.DiscoveryStage == HidEndpointDiscoveryStage.GlobalAggressive &&
                string.IsNullOrWhiteSpace(endpointAddress))
            {
                diagnostics.RegisterEndpointSkip("global-no-address");
                consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
                ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, "skip global no-address");
                return;
            }

            foreach (var initPacket in handshakeProfile.InitPackets)
            {
                _ = HidGamepadAccess.TrySendOutputPacket(handle, initPacket.Payload);
                if (initPacket.DelayAfterMs > 0)
                {
                    Thread.Sleep(initPacket.DelayAfterMs);
                }
            }

            var perEndpointReports = new Dictionary<byte, byte[]>();
            var reportSizes = HidGamepadAccess.BuildProbeReportSizes(handle);
            var featureSizes = HidGamepadAccess.BuildProbeFeatureSizes(handle);
            var noSignalStopBudget = recoveryRound
                ? NoSignalRecoveryNoSignalStopAttempts
                : NoSignalStopAttempts;
            var minimumDeepScore = recoveryRound
                ? NoSignalRecoveryMinimumScoreForDeepPhase
                : MinimumScoreForDeepPhase;
            var budget = new ProbeReadBudget(EndpointAttemptBudget, noSignalStopBudget, minimumDeepScore);
            using var streamSession = new HidInputStreamSession(handle);
            var warmupFrames = streamSession.CaptureWarmupFrames(
                minimumReportSize: reportSizes.FirstOrDefault(handshakeProfile.MinimumReportSize),
                frameBudget: WarmupFrameBudget,
                timeoutMs: WarmupTimeoutMs,
                cancellationToken);
            foreach (var frame in warmupFrames)
            {
                diagnostics.RegisterObservedReportId(frame.ReportId);
                if (!perEndpointReports.TryGetValue(frame.ReportId, out var existingWarmup) ||
                    existingWarmup.Length < frame.Data.Length)
                {
                    perEndpointReports[frame.ReportId] = frame.Data;
                }
            }

            var warmupReportPriority = warmupFrames
                .GroupBy(frame => frame.ReportId)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .ToList();
            var quickReportIds = recoveryRound
                ? BuildRecoveryQuickReportIds(handshakeProfile, warmupReportPriority)
                : BuildQuickReportIds(handshakeProfile, warmupReportPriority);
            var expandReportIds = recoveryRound
                ? BuildRecoveryExpandReportIds(handshakeProfile)
                : BuildExpandReportIds(handshakeProfile);

            var readSuccessBefore = diagnostics.ReportReadSuccessCount;
            ExecuteReadPhase(
                ReadPhase.Quick,
                handle,
                streamSession,
                quickReportIds,
                reportSizes,
                QuickTimeoutLadderMs,
                QuickReadRetryCount,
                perEndpointReports,
                budget,
                diagnostics,
                onProgress,
                totalAttemptBudget,
                ref consumedAttempts,
                cancellationToken);

            var canExpand = recoveryRound
                ? !budget.IsExhausted
                : budget.CanEnterExpandPhase;
            if (canExpand)
            {
                ExecuteReadPhase(
                    ReadPhase.Expand,
                    handle,
                    streamSession,
                    expandReportIds,
                    reportSizes,
                    ExpandTimeoutLadderMs,
                    ExpandReadRetryCount,
                    perEndpointReports,
                    budget,
                    diagnostics,
                    onProgress,
                    totalAttemptBudget,
                    ref consumedAttempts,
                    cancellationToken);
            }

            var canDeep = recoveryRound
                ? !budget.IsExhausted && budget.HasSuccessfulRead && budget.BestObservedScore >= NoSignalRecoveryMinimumScoreForDeepPhase
                : budget.CanEnterDeepPhase;
            if (canDeep && streamSession.HasCaptured)
            {
                ExecuteReadPhase(
                    ReadPhase.Deep,
                    handle,
                    streamSession,
                    expandReportIds,
                    reportSizes,
                    DeepTimeoutLadderMs,
                    DeepReadRetryCount,
                    perEndpointReports,
                    budget,
                    diagnostics,
                    onProgress,
                    totalAttemptBudget,
                    ref consumedAttempts,
                    cancellationToken);
            }

            ExecuteFeaturePhase(
                handle,
                handshakeProfile,
                featureSizes,
                perEndpointReports,
                budget,
                diagnostics,
                onProgress,
                totalAttemptBudget,
                ref consumedAttempts,
                cancellationToken);

            if (recoveryRound && diagnostics.ReportReadSuccessCount > readSuccessBefore)
            {
                diagnostics.NoSignalRecoveryRecovered = true;
            }

            var selection = GamepadProbeCandidateEvaluator.SelectBest(perEndpointReports);
            endpointSelections.Add(new EndpointSelectionCandidate(
                Endpoint: endpoint,
                VendorId: vendorId,
                ProductId: productId,
                IdentityVendorId: identityVendorId,
                IdentityProductId: identityProductId,
                TransportVendorId: transportVendorId,
                TransportProductId: transportProductId,
                IdentityKey: endpointIdentityKey,
                ProbeKey: probeKey,
                ModelKey: endpointModelKey,
                HandshakeProfileId: handshakeProfile.ProfileId,
                ReportsById: perEndpointReports.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
                Selection: selection));
        }
        catch (Exception ex)
        {
            diagnostics.EndpointExceptionCount++;
            diagnostics.RegisterException(ProbeStage.CollectReports, ex, $"Endpoint {endpoint.DevicePath}");
            consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
            ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, "endpoint exception");
        }
    }

    private static List<HidGamepadEndpoint> BuildRecoveryEndpoints(
        IReadOnlyList<HidGamepadEndpoint> selectedEndpoints,
        string normalizedAddress,
        CancellationToken cancellationToken)
    {
        var strictEndpoints = HidGamepadAccess.EnumerateProbeEndpoints(
            normalizedAddress,
            HidEndpointDiscoveryStage.Strict,
            cancellationToken);
        var relaxedEndpoints = HidGamepadAccess.EnumerateProbeEndpoints(
            normalizedAddress,
            HidEndpointDiscoveryStage.Relaxed,
            cancellationToken);
        var globalEndpoints = strictEndpoints.Count == 0 && relaxedEndpoints.Count == 0
            ? HidGamepadAccess
                .EnumerateProbeEndpoints(addressFilter: null, HidEndpointDiscoveryStage.GlobalAggressive, cancellationToken)
                .Where(endpoint => string.Equals(
                    AddressNormalizer.NormalizeAddress(endpoint.Address),
                    normalizedAddress,
                    StringComparison.OrdinalIgnoreCase))
                .Take(MaxGlobalEndpoints)
                .ToList()
            : [];
        return DistinctByDevicePath(
            selectedEndpoints
                .Concat(strictEndpoints)
                .Concat(relaxedEndpoints)
                .Concat(globalEndpoints)
                .ToList());
    }

    private static void ExecuteReadPhase(
        ReadPhase phase,
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        HidInputStreamSession streamSession,
        IReadOnlyList<byte> reportIds,
        IReadOnlyList<int> reportSizes,
        IReadOnlyList<int> timeoutLadderMs,
        int retryCount,
        Dictionary<byte, byte[]> perEndpointReports,
        ProbeReadBudget budget,
        ProbeDiagnostics diagnostics,
        Action<ProbeProgress>? onProgress,
        int totalAttemptBudget,
        ref int consumedAttempts,
        CancellationToken cancellationToken)
    {
        foreach (var reportId in reportIds)
        {
            if (budget.IsExhausted || budget.ShouldStopForNoSignal)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var remainingAttempts = budget.RemainingAttempts;
                if (remainingAttempts <= 0)
                {
                    break;
                }

                var readSuccess = TryReadWithSizes(
                    handle,
                    streamSession,
                    reportId,
                    reportSizes,
                    timeoutLadderMs,
                    retryCount,
                    remainingAttempts,
                    allowSnapshotFallback: phase == ReadPhase.Quick && !budget.HasSuccessfulRead,
                    out var report,
                    out var readStats);

                diagnostics.AccumulateReadStats(readStats);
                budget.RegisterAttempt(readStats.AttemptCount, readSuccess);
                consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + Math.Max(1, readStats.AttemptCount));

                if (readSuccess)
                {
                    diagnostics.ReportReadSuccessCount++;
                    var observedReportId = report[0] != 0 ? report[0] : reportId;
                    diagnostics.RegisterObservedReportId(observedReportId);
                    if (!perEndpointReports.TryGetValue(observedReportId, out var existing) || existing.Length < report.Length)
                    {
                        perEndpointReports[observedReportId] = report;
                    }
                }
                else
                {
                    diagnostics.ReportReadFailureCount++;
                }

                var score = TryGetBestObservedScore(perEndpointReports);
                budget.RegisterScore(score);
                diagnostics.RegisterObservedScore(score);

                if (phase == ReadPhase.Expand && !budget.HasSuccessfulRead && score < 25)
                {
                    break;
                }

                ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, phase.ToString().ToLowerInvariant());
            }
            catch (Exception ex)
            {
                diagnostics.ReportReadFailureCount++;
                diagnostics.ReportReadExceptionCount++;
                diagnostics.RegisterException(ProbeStage.CollectReports, ex, $"Read reportId=0x{reportId:X2} phase={phase}");
                budget.RegisterAttempt(1, success: false);
                consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
                ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, $"{phase} exception");
            }
        }
    }

    private static void ExecuteFeaturePhase(
        SafeFileHandle handle,
        ThirdPartyHandshakeProfile handshakeProfile,
        IReadOnlyList<int> featureSizes,
        Dictionary<byte, byte[]> perEndpointReports,
        ProbeReadBudget budget,
        ProbeDiagnostics diagnostics,
        Action<ProbeProgress>? onProgress,
        int totalAttemptBudget,
        ref int consumedAttempts,
        CancellationToken cancellationToken)
    {
        if (budget.IsExhausted)
        {
            return;
        }

        var featureIds = handshakeProfile.FeatureReportIds.Count > 0
            ? handshakeProfile.FeatureReportIds
            : FeatureProbeReportIds;

        foreach (var reportId in featureIds)
        {
            if (budget.IsExhausted)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var readSuccess = false;
            foreach (var size in featureSizes)
            {
                if (budget.IsExhausted)
                {
                    break;
                }

                var minimumSize = Math.Max(size, handshakeProfile.MinimumReportSize);
                if (HidGamepadAccess.TryReadFeatureReport(handle, reportId, minimumSize, out var report, retryCount: 1))
                {
                    readSuccess = true;
                    if (!perEndpointReports.TryGetValue(reportId, out var existing) || existing.Length < report.Length)
                    {
                        perEndpointReports[reportId] = report;
                    }

                    break;
                }

                diagnostics.ReportReadFailureCount++;
                budget.RegisterAttempt(1, success: false);
                consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
            }

            if (readSuccess)
            {
                diagnostics.ReportReadSuccessCount++;
                diagnostics.RegisterObservedReportId(reportId);
                budget.RegisterAttempt(1, success: true);
                consumedAttempts = Math.Min(totalAttemptBudget, consumedAttempts + 1);
                ReportCollectProgress(onProgress, diagnostics, consumedAttempts, totalAttemptBudget, "feature");
            }
        }
    }

    private static int TryGetBestObservedScore(Dictionary<byte, byte[]> reports)
    {
        if (reports.Count == 0)
        {
            return 0;
        }

        var selection = GamepadProbeCandidateEvaluator.SelectBest(reports);
        return selection.Winner?.Score ?? 0;
    }

    internal static LearningDecision ResolveLearningDecision(int bestScore, int votes)
    {
        return ResolveLearningDecision(bestScore, votes, requiredVotes: 2);
    }

    internal static LearningDecision ResolveLearningDecision(int bestScore, int votes, int requiredVotes)
    {
        if (bestScore >= ConfirmedScoreThreshold)
        {
            return new LearningDecision(
                Accepted: true,
                PersistProfile: true,
                IsPending: false,
                Confidence: BatteryConfidence.Confirmed);
        }

        if (bestScore < PendingScoreThreshold)
        {
            return new LearningDecision(
                Accepted: false,
                PersistProfile: false,
                IsPending: false,
                Confidence: BatteryConfidence.Estimated);
        }

        return new LearningDecision(
            Accepted: true,
            PersistProfile: votes >= Math.Max(2, requiredVotes),
            IsPending: votes < Math.Max(2, requiredVotes),
            Confidence: BatteryConfidence.Estimated);
    }

    internal static bool ShouldRunNoSignalRecovery(int readSuccessCount, int bestObservedScore)
    {
        return readSuccessCount <= 0 && bestObservedScore <= 0;
    }

    internal static string BuildVoteCandidateKey(
        string? identityKey,
        string decoder,
        byte reportId,
        int offset)
    {
        var normalizedIdentity = string.IsNullOrWhiteSpace(identityKey)
            ? "IDENTITY_NONE"
            : identityKey.Trim().ToUpperInvariant();
        var normalizedDecoder = string.IsNullOrWhiteSpace(decoder)
            ? "UNKNOWN"
            : decoder.Trim().ToUpperInvariant();
        return $"IDK_{normalizedIdentity}|RID_{reportId:X2}|OFF_{Math.Max(0, offset)}|DEC_{normalizedDecoder}";
    }

    private static ProbeResult BuildFailureResult(
        string message,
        ProbeStage stage,
        ProbeDiagnostics diagnostics,
        Exception? exception = null)
    {
        return new ProbeResult(
            Success: false,
            BatteryPercent: null,
            Message: message,
            Profile: null,
            ErrorDetail: BuildErrorDetail(stage, message, diagnostics, exception));
    }

    private static ProbeErrorDetail BuildErrorDetail(
        ProbeStage stage,
        string fallbackMessage,
        ProbeDiagnostics diagnostics,
        Exception? exception)
    {
        var effectiveStage = stage != ProbeStage.None
            ? stage
            : diagnostics.LastExceptionStage;

        var effectiveException = exception ?? diagnostics.LastException;
        var exceptionType = effectiveException?.GetType().Name ?? string.Empty;
        var exceptionMessage = string.IsNullOrWhiteSpace(effectiveException?.Message)
            ? fallbackMessage
            : effectiveException!.Message;
        var failureKind = ClassifyFailureKind(effectiveStage, fallbackMessage, diagnostics);

        return new ProbeErrorDetail(
            Stage: effectiveStage,
            ExceptionType: exceptionType,
            ExceptionMessage: exceptionMessage,
            DiagnosticsText: diagnostics.ToDiagnosticText(),
            Timestamp: DateTimeOffset.Now,
            OpenSuccessCount: diagnostics.HandleOpenSuccessCount,
            OpenFailureCount: diagnostics.HandleOpenFailureCount,
            ReadSuccessCount: diagnostics.ReportReadSuccessCount,
            ReadFailureCount: diagnostics.ReportReadFailureCount,
            StrictEndpointCount: diagnostics.StrictEndpointCount,
            RelaxedEndpointCount: diagnostics.RelaxedEndpointCount,
            GlobalEndpointCount: diagnostics.GlobalEndpointCount,
            StreamTimeoutCount: diagnostics.StreamTimeoutCount,
            Context: diagnostics.LastExceptionContext,
            ObservedReportIds: diagnostics.ToObservedReportText(),
            TopCandidates: diagnostics.TopCandidatesText,
            WinnerDecoder: diagnostics.WinnerDecoder,
            BlockReason: diagnostics.BlockReason,
            IdentityKey: diagnostics.IdentityKey,
            SuppressionReason: diagnostics.SuppressionReason,
            DecoderConfidence: diagnostics.DecoderConfidence,
            HandshakeProfileId: diagnostics.HandshakeProfileId,
            BrandHint: diagnostics.BrandHint,
            AliasMatched: !string.IsNullOrWhiteSpace(diagnostics.AliasMatchSource),
            IdleSuppressed: diagnostics.IdleSuppressed,
            FailureKind: failureKind);
    }

    private static ProbeFailureKind ClassifyFailureKind(
        ProbeStage stage,
        string message,
        ProbeDiagnostics diagnostics)
    {
        if (stage == ProbeStage.DeviceCheck &&
            message.Contains("address", StringComparison.OrdinalIgnoreCase))
        {
            return ProbeFailureKind.AddressInvalid;
        }

        if (message.Contains("Xbox-layer", StringComparison.OrdinalIgnoreCase) ||
            diagnostics.BlockReason.StartsWith("xbox_generic", StringComparison.OrdinalIgnoreCase))
        {
            return ProbeFailureKind.PolicyBlocked;
        }

        if (diagnostics.BestObservedScore == 0 && diagnostics.ReportReadSuccessCount <= 0 && diagnostics.ReportReadFailureCount > 0)
        {
            return ProbeFailureKind.NoSignal;
        }

        if (diagnostics.BestObservedScore == 0 && diagnostics.ReportReadSuccessCount > 0)
        {
            return ProbeFailureKind.WeakSignal;
        }

        return ProbeFailureKind.Unknown;
    }

    private static bool TryReadWithSizes(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        HidInputStreamSession streamSession,
        byte reportId,
        IReadOnlyList<int> reportSizes,
        IReadOnlyList<int> timeoutLadderMs,
        int retryCount,
        int maxAttempts,
        bool allowSnapshotFallback,
        out byte[] report,
        out ReportAttemptStats accumulatedStats)
    {
        accumulatedStats = default;

        var snapshotAttempted = false;

        foreach (var size in reportSizes)
        {
            foreach (var timeoutMs in timeoutLadderMs)
            {
                var remainingAttempts = Math.Max(0, maxAttempts - accumulatedStats.AttemptCount);
                if (remainingAttempts <= 0)
                {
                    report = [];
                    return false;
                }

                if (TryReadByStream(
                        streamSession,
                        reportId,
                        size,
                        timeoutMs,
                        retryCount,
                        remainingAttempts,
                        out report,
                        out var readStats))
                {
                    accumulatedStats.Add(readStats);
                    return true;
                }

                accumulatedStats.Add(readStats);
            }
        }

        if (allowSnapshotFallback &&
            !snapshotAttempted &&
            maxAttempts - accumulatedStats.AttemptCount > 0)
        {
            snapshotAttempted = true;
            if (HidGamepadAccess.TryReadInputReportSnapshot(handle, reportId, reportSizes.FirstOrDefault(64), out report, out var errorCode))
            {
                accumulatedStats.Add(new HidReportReadStatistics(
                    GetInputSuccessCount: 1,
                    GetInputFailureCount: 0,
                    StreamSuccessCount: 0,
                    StreamFailureCount: 0,
                    StreamTimeoutCount: 0,
                    AttemptCount: 1,
                    HardFailCount: 0));
                return true;
            }

            var hardFail = errorCode is 1 or 5 or 6 or 50 or 87 or 1167 ? 1 : 0;
            accumulatedStats.Add(new HidReportReadStatistics(
                GetInputSuccessCount: 0,
                GetInputFailureCount: 1,
                StreamSuccessCount: 0,
                StreamFailureCount: 0,
                StreamTimeoutCount: 0,
                AttemptCount: 1,
                HardFailCount: hardFail));
        }

        report = [];
        return false;
    }

    private static bool TryReadByStream(
        HidInputStreamSession streamSession,
        byte reportId,
        int minimumReportSize,
        int timeoutMs,
        int retryCount,
        int maxAttempts,
        out byte[] report,
        out ReportAttemptStats accumulatedStats)
    {
        accumulatedStats = default;

        if (!streamSession.IsAvailable || maxAttempts <= 0)
        {
            report = [];
            return false;
        }

        var attemptsRemaining = Math.Max(1, Math.Min(maxAttempts, retryCount + 1));
        for (var attempt = 0; attempt < attemptsRemaining; attempt++)
        {
            var timedOut = false;
            if (streamSession.TryReadReport(reportId, minimumReportSize, timeoutMs, out var frame, out timedOut))
            {
                accumulatedStats.Add(new HidReportReadStatistics(
                    GetInputSuccessCount: 0,
                    GetInputFailureCount: 0,
                    StreamSuccessCount: 1,
                    StreamFailureCount: 0,
                    StreamTimeoutCount: 0,
                    AttemptCount: 1,
                    HardFailCount: 0));
                report = frame.Data;
                return true;
            }

            accumulatedStats.Add(new HidReportReadStatistics(
                GetInputSuccessCount: 0,
                GetInputFailureCount: 0,
                StreamSuccessCount: 0,
                StreamFailureCount: 1,
                StreamTimeoutCount: timedOut ? 1 : 0,
                AttemptCount: 1,
                HardFailCount: 0));
        }

        report = [];
        return false;
    }

    private static List<HidGamepadEndpoint> DistinctByDevicePath(IReadOnlyList<HidGamepadEndpoint> endpoints)
    {
        var byPath = new Dictionary<string, HidGamepadEndpoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in endpoints)
        {
            byPath[endpoint.DevicePath] = endpoint;
        }

        return byPath.Values.ToList();
    }

    private static IReadOnlyList<byte> BuildQuickReportIds(
        ThirdPartyHandshakeProfile profile,
        IReadOnlyList<byte>? warmupPreferredReportIds = null)
    {
        var warmupCount = warmupPreferredReportIds?.Count ?? 0;
        var ordered = new List<byte>(profile.PreferredInputReportIds.Count + QuickScanReportIds.Length + warmupCount);
        AddReportIds(ordered, warmupPreferredReportIds);
        AddReportIds(ordered, profile.PreferredInputReportIds);
        AddReportIds(ordered, QuickScanReportIds);
        return ordered.Count == 0
            ? QuickScanReportIds
            : ordered;
    }

    private static IReadOnlyList<byte> BuildExpandReportIds(ThirdPartyHandshakeProfile profile)
    {
        var ordered = new List<byte>(ProbeReportIds.Length + profile.PreferredInputReportIds.Count);
        AddReportIds(ordered, profile.PreferredInputReportIds);
        AddReportIds(ordered, ProbeReportIds);
        return ordered;
    }

    private static IReadOnlyList<byte> BuildRecoveryQuickReportIds(
        ThirdPartyHandshakeProfile profile,
        IReadOnlyList<byte>? warmupPreferredReportIds = null)
    {
        var warmupCount = warmupPreferredReportIds?.Count ?? 0;
        var ordered = new List<byte>(profile.RecoveryInputReportIds.Count + profile.PreferredInputReportIds.Count + QuickScanReportIds.Length + warmupCount);
        AddReportIds(ordered, warmupPreferredReportIds);
        AddReportIds(ordered, profile.RecoveryInputReportIds);
        AddReportIds(ordered, profile.PreferredInputReportIds);
        AddReportIds(ordered, QuickScanReportIds);
        return ordered.Count == 0
            ? QuickScanReportIds
            : ordered;
    }

    private static IReadOnlyList<byte> BuildRecoveryExpandReportIds(ThirdPartyHandshakeProfile profile)
    {
        var ordered = new List<byte>(profile.RecoveryInputReportIds.Count + ProbeReportIds.Length + profile.PreferredInputReportIds.Count);
        AddReportIds(ordered, profile.RecoveryInputReportIds);
        AddReportIds(ordered, profile.PreferredInputReportIds);
        AddReportIds(ordered, ProbeReportIds);
        return ordered.Count == 0
            ? ProbeReportIds
            : ordered;
    }

    private static void AddReportIds(ICollection<byte> target, IEnumerable<byte>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var reportId in source)
        {
            if (!target.Contains(reportId))
            {
                target.Add(reportId);
            }
        }
    }

    private static int AdjustScoreBySource(int score, HidEndpointDiscoveryStage stage)
    {
        return stage switch
        {
            HidEndpointDiscoveryStage.Strict => score + 5,
            HidEndpointDiscoveryStage.Relaxed => score,
            HidEndpointDiscoveryStage.GlobalAggressive => score - 6,
            _ => score
        };
    }

    private static string BuildNoEndpointMessage(ProbeDiagnostics diagnostics)
    {
        return $"수집 가능한 HID 엔드포인트를 찾지 못했습니다. {diagnostics.ToDiagnosticText()}";
    }

    private static string BuildEvaluationFailureMessage(ProbeDiagnostics diagnostics, IReadOnlyList<EndpointSelectionCandidate> endpointSelections)
    {
        var bestObservedScore = ComputeBestObservedScore(endpointSelections);

        return $"유효한 배터리 후보를 확정하지 못했습니다 (bestScore {bestObservedScore}). {diagnostics.ToDiagnosticText()}";
    }

    private static int ComputeBestObservedScore(IReadOnlyList<EndpointSelectionCandidate> endpointSelections)
    {
        return endpointSelections
            .Where(candidate => candidate.Selection.Winner is not null)
            .Select(candidate => candidate.Selection.Winner!.Score)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string BuildTieFailureMessage(ProbeDiagnostics diagnostics, int tieCount)
    {
        return $"동일 점수 후보가 {tieCount}개로 충돌했습니다. {diagnostics.ToDiagnosticText()}";
    }

    private static string BuildSuccessMessage(EndpointSelectionCandidate selected, ProbeDiagnostics diagnostics)
    {
        var stageLabel = selected.Endpoint.DiscoveryStage switch
        {
            HidEndpointDiscoveryStage.Strict => "strict",
            HidEndpointDiscoveryStage.Relaxed => "relaxed",
            _ => "global"
        };

        return $"수집 완료 ({stageLabel}, open {diagnostics.HandleOpenSuccessCount}, readSuccess {diagnostics.ReportReadSuccessCount}).";
    }

    private static string BuildTopCandidatesText(IEnumerable<EndpointSelectionCandidate> candidates, int limit)
    {
        var text = candidates
            .Where(candidate => candidate.Selection.Winner is not null)
            .OrderByDescending(candidate => candidate.Selection.Winner!.Score)
            .ThenBy(candidate => candidate.Endpoint.DiscoveryStage)
            .ThenBy(candidate => candidate.Endpoint.DevicePath, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, limit))
            .Select(candidate =>
            {
                var winner = candidate.Selection.Winner!;
                var model = string.IsNullOrWhiteSpace(candidate.ModelKey)
                    ? ResolveEndpointModelKey(candidate, candidate.Endpoint.Address, candidate.Endpoint.DisplayName)
                    : candidate.ModelKey;
                var identity = string.IsNullOrWhiteSpace(candidate.IdentityKey) ? "none" : candidate.IdentityKey;
                return $"{winner.Decoder}@0x{winner.ReportId:X2}[off={winner.Offset},pct={winner.BatteryPercent},score={winner.Score},model={model},identity={identity}]";
            });
        return string.Join(" | ", text);
    }

    private static List<string> SplitTopCandidates(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string ResolveEndpointModelKey(
        EndpointSelectionCandidate endpointSelection,
        string? address,
        string? displayName)
    {
        return BatteryModelKeyResolver.ResolveNormalizedModelKey(
            endpointSelection.IdentityVendorId,
            endpointSelection.IdentityProductId,
            endpointSelection.TransportVendorId,
            endpointSelection.TransportProductId,
            address,
            displayName);
    }

    private static EndpointSelectionCandidate SelectPreferredCandidate(
        IReadOnlyList<EndpointSelectionCandidate> rankedCandidates,
        ProbeDiagnostics diagnostics,
        out string suppressionReason)
    {
        suppressionReason = string.Empty;
        if (rankedCandidates.Count == 0)
        {
            throw new InvalidOperationException("Ranked candidates cannot be empty.");
        }

        foreach (var candidate in rankedCandidates)
        {
            var winner = candidate.Selection.Winner;
            if (winner is null)
            {
                continue;
            }

            if (!TryGetSuppressionReason(candidate, winner, rankedCandidates, diagnostics, out var reason))
            {
                suppressionReason = string.Empty;
                diagnostics.BlockReason = string.Empty;
                return candidate;
            }

            if (string.IsNullOrWhiteSpace(suppressionReason))
            {
                suppressionReason = reason;
            }
        }

        return rankedCandidates[0];
    }

    private static bool ShouldDelayImmediateAcceptance(
        GamepadBatteryCandidate winner,
        IReadOnlyList<EndpointSelectionCandidate> rankedCandidates,
        ProbeDiagnostics diagnostics)
    {
        if (winner.Decoder == GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags && winner.BatteryPercent <= 10)
        {
            return true;
        }

        if (diagnostics.DecoderConfidence < 0.72d)
        {
            return true;
        }

        return HasCompetingHighBatteryCandidate(winner, rankedCandidates, margin: 12, delta: 55);
    }

    internal static bool ShouldAllowImmediateEstimatedAcceptance(
        int score,
        int reportRepeatCount,
        double decoderConfidence,
        bool hasStrongCompetingCandidate,
        bool isSuspiciousXboxLowCandidate,
        string? suppressionReason)
    {
        if (score < ImmediateEstimatedScoreThreshold)
        {
            return false;
        }

        if (reportRepeatCount < ImmediateEstimatedMinReportRepeats)
        {
            return false;
        }

        if (decoderConfidence < 0.50d)
        {
            return false;
        }

        if (hasStrongCompetingCandidate || isSuspiciousXboxLowCandidate)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(suppressionReason))
        {
            return true;
        }

        return string.Equals(
            suppressionReason,
            "delayed_acceptance_low_confidence",
            StringComparison.OrdinalIgnoreCase);
    }

    private static double CalculateDecoderConfidence(
        GamepadBatteryCandidate winner,
        IReadOnlyList<EndpointSelectionCandidate> rankedCandidates)
    {
        var scoreComponent = Math.Clamp(winner.Score / 100d, 0d, 1d);
        var spreadComponent = 0d;
        if (rankedCandidates.Count > 1 && rankedCandidates[1].Selection.Winner is not null)
        {
            var next = rankedCandidates[1].Selection.Winner!;
            spreadComponent = Math.Clamp((winner.Score - next.Score) / 40d, 0d, 1d);
        }

        var decoderBase = winner.Decoder switch
        {
            GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags => 0.80d,
            GamepadProbeCandidateEvaluator.DecoderPercent100 => 0.72d,
            GamepadProbeCandidateEvaluator.DecoderPercent255 => 0.64d,
            GamepadProbeCandidateEvaluator.DecoderNibble10 => 0.68d,
            _ => 0.60d
        };
        var reportLengthBonus = winner.ReportLength >= 16 ? 0.06d : 0d;
        var confidence = (scoreComponent * 0.55d) + (spreadComponent * 0.20d) + (decoderBase * 0.25d) + reportLengthBonus;
        return Math.Round(Math.Clamp(confidence, 0d, 1d), 3);
    }

    private static bool TryGetSuppressionReason(
        EndpointSelectionCandidate candidate,
        GamepadBatteryCandidate winner,
        IReadOnlyList<EndpointSelectionCandidate> rankedCandidates,
        ProbeDiagnostics diagnostics,
        out string suppressionReason)
    {
        suppressionReason = string.Empty;

        if (!string.Equals(winner.Decoder, GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags, StringComparison.Ordinal))
        {
            return false;
        }

        if (!candidate.ReportsById.TryGetValue(winner.ReportId, out var report) || report.Length < 2)
        {
            suppressionReason = "xbox_flags_low_frame_evidence";
            diagnostics.BlockReason = suppressionReason;
            return true;
        }

        if (diagnostics.GetObservedReportCount(winner.ReportId) < 2)
        {
            suppressionReason = "xbox_flags_low_repeat_evidence";
            diagnostics.BlockReason = suppressionReason;
            return true;
        }

        if (winner.BatteryPercent <= 10 &&
            HasCompetingHighBatteryCandidate(winner, rankedCandidates, margin: 10, delta: 55))
        {
            suppressionReason = "xbox_flags_competing_high_candidate";
            diagnostics.BlockReason = suppressionReason;
            return true;
        }

        return false;
    }

    private static bool RequiresExtraVotes(
        EndpointSelectionCandidate selected,
        GamepadBatteryCandidate winner,
        ProbeDiagnostics diagnostics,
        string suppressionReason)
    {
        if (!string.Equals(winner.Decoder, GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(suppressionReason))
        {
            if (string.Equals(
                    suppressionReason,
                    "xbox_flags_low_repeat_evidence",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Repeat evidence only: 2-vote flow is enough.
                return false;
            }

            return true;
        }

        if (winner.BatteryPercent > 10)
        {
            return diagnostics.GetObservedReportCount(winner.ReportId) < 2;
        }

        if (!selected.ReportsById.TryGetValue(winner.ReportId, out var report) || report.Length < 2)
        {
            return true;
        }

        if (!XboxBluetoothBatteryDecoder.TryDecode(winner.ReportId, report, out _, out var onUsb))
        {
            return true;
        }

        if (onUsb)
        {
            return true;
        }

        return diagnostics.GetObservedReportCount(winner.ReportId) < 2;
    }

    private static string ResolveVoteIdentityKey(
        EndpointSelectionCandidate selected,
        string normalizedAddress,
        string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(selected.IdentityKey))
        {
            return selected.IdentityKey;
        }

        var derivedIdentity = BatteryModelKeyResolver.ResolveIdentityKey(
            selected.IdentityVendorId,
            selected.IdentityProductId,
            selected.TransportVendorId,
            selected.TransportProductId,
            normalizedAddress,
            displayName,
            endpointSignature: null);
        if (!string.IsNullOrWhiteSpace(derivedIdentity) &&
            !string.Equals(derivedIdentity, "IDENTITY_UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            return derivedIdentity;
        }

        var fallbackModel = ResolveEndpointModelKey(selected, normalizedAddress, displayName);
        return string.IsNullOrWhiteSpace(fallbackModel)
            ? "IDENTITY_UNKNOWN"
            : $"MODEL_{fallbackModel}";
    }

    private static bool HasCompetingHighBatteryCandidate(
        GamepadBatteryCandidate winner,
        IReadOnlyList<EndpointSelectionCandidate> rankedCandidates,
        int margin,
        int delta)
    {
        return rankedCandidates
            .Select(candidate => candidate.Selection.Winner)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .Any(candidate =>
                candidate != winner &&
                !string.Equals(candidate.Decoder, winner.Decoder, StringComparison.Ordinal) &&
                candidate.Score >= winner.Score - margin &&
                candidate.BatteryPercent - winner.BatteryPercent >= delta);
    }

    private static bool ShouldRejectXboxGenericCandidate(
        string? displayName,
        string vendorId,
        GamepadBatteryCandidate candidate,
        ProbePolicy policy,
        out string rejectReason)
    {
        rejectReason = string.Empty;
        if (!IsXboxLayerCandidate(displayName, vendorId) || !IsGenericDecoder(candidate.Decoder))
        {
            return false;
        }

        if (policy.XboxGenericAcceptanceMode == XboxGenericAcceptanceMode.BlockGeneric)
        {
            rejectReason = "xbox_generic_blocked";
            return true;
        }

        if (candidate.Score < policy.GenericMinScore)
        {
            rejectReason = "xbox_generic_score_below_min";
            return true;
        }

        return false;
    }

    private static bool IsDedicatedXboxCandidate(string? displayName, string vendorId, GamepadBatteryCandidate candidate)
    {
        return IsXboxLayerCandidate(displayName, vendorId) &&
               string.Equals(candidate.Decoder, GamepadProbeCandidateEvaluator.DecoderXboxBluetoothFlags, StringComparison.Ordinal);
    }

    private static bool IsGenericDecoder(string decoder)
    {
        return decoder is
            GamepadProbeCandidateEvaluator.DecoderPercent100 or
            GamepadProbeCandidateEvaluator.DecoderPercent255 or
            GamepadProbeCandidateEvaluator.DecoderNibble10;
    }

    private static bool IsXboxLayerCandidate(string? displayName, string vendorId)
    {
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName.Contains("xbox", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(vendorId, "045E", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyHardFailCooldownIfNeeded(
        IReadOnlyCollection<string> modelKeys,
        ProbeDiagnostics diagnostics)
    {
        if (modelKeys.Count == 0)
        {
            return;
        }

        if (diagnostics.ReportReadSuccessCount > 0 || diagnostics.HardFailCount < 30)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        foreach (var modelKey in modelKeys)
        {
            _pendingCandidateStore.SetCooldown(modelKey, HardFailCooldownDuration, now);
        }
    }

    private static void AppendProbeTrace(
        ConnectedBluetoothDevice device,
        string normalizedAddress,
        ProbeStage stage,
        string outcome,
        ProbeResult result,
        ProbeDiagnostics diagnostics,
        IReadOnlyCollection<string> observedModelKeys,
        IReadOnlyList<string> topCandidates,
        string winnerDecoder,
        string blockReason)
    {
        try
        {
            var directory = Path.GetDirectoryName(ProbeTraceLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            RotateTraceLogIfNeeded();
            var payload = new
            {
                ts = DateTimeOffset.Now,
                outcome,
                stage = stage.ToString(),
                processPath = ProcessPath,
                buildStamp = BuildStamp,
                device = new
                {
                    device.DisplayName,
                    address = normalizedAddress,
                    categoryHint = device.CategoryHint
                },
                success = result.Success,
                message = result.Message,
                isPending = result.IsPending,
                batteryPercent = result.BatteryPercent,
                observedReportIds = diagnostics.ToObservedReportText(),
                topCandidates,
                winnerDecoder,
                blockReason,
                suppressionReason = diagnostics.SuppressionReason,
                decoderConfidence = diagnostics.DecoderConfidence,
                identityKey = diagnostics.IdentityKey,
                handshakeProfileId = diagnostics.HandshakeProfileId,
                brandHint = diagnostics.BrandHint,
                aliasMatchSource = diagnostics.AliasMatchSource,
                profileSelectionReason = diagnostics.ProfileSelectionReason,
                skippedAddressMismatchCount = diagnostics.AddressMismatchSkipCount,
                skippedGlobalNoAddressCount = diagnostics.GlobalNoAddressSkipCount,
                globalExcludedCount = diagnostics.GlobalExcludedCount,
                idleKeepReason = diagnostics.IdleKeepReason,
                revalidationFailureKind = diagnostics.RevalidationFailureKind,
                profileStateBefore = diagnostics.ProfileStateBefore,
                profileStateAfter = diagnostics.ProfileStateAfter,
                acceptancePath = diagnostics.AcceptancePath,
                activeSource = diagnostics.ActiveSource,
                noSignalRecoveryAttempted = diagnostics.NoSignalRecoveryAttempted,
                noSignalRecoveryRecovered = diagnostics.NoSignalRecoveryRecovered,
                observedModelKeys = observedModelKeys.ToArray(),
                diagnostics = diagnostics.ToDiagnosticText()
            };

            var json = JsonSerializer.Serialize(payload);
            File.AppendAllText(ProbeTraceLogPath, json + Environment.NewLine);
        }
        catch
        {
            // Ignore trace logging failures.
        }
    }

    private static void RotateTraceLogIfNeeded()
    {
        if (!File.Exists(ProbeTraceLogPath))
        {
            return;
        }

        var info = new FileInfo(ProbeTraceLogPath);
        if (info.Length < ProbeTraceMaxBytes)
        {
            return;
        }

        var backupPath = ProbeTraceLogPath + ".1";
        try
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(ProbeTraceLogPath, backupPath);
        }
        catch
        {
            // Ignore roll failures and keep app flow unaffected.
        }
    }

    private static string ResolveBuildStamp()
    {
        try
        {
            var assemblyVersion = typeof(GamepadProbeService).Assembly.GetName().Version?.ToString();
            if (!string.IsNullOrWhiteSpace(assemblyVersion))
            {
                return assemblyVersion;
            }
        }
        catch
        {
            // Ignore and continue to fallback.
        }

        return "unknown";
    }

    private static void ReportCollectProgress(
        Action<ProbeProgress>? onProgress,
        ProbeDiagnostics diagnostics,
        int consumedAttempts,
        int totalAttemptBudget,
        string stageLabel)
    {
        var ratio = consumedAttempts / (double)Math.Max(1, totalAttemptBudget);
        var status = $"{stageLabel} collecting... success {diagnostics.ReportReadSuccessCount}, fail {diagnostics.ReportReadFailureCount}";
        Report(
            onProgress,
            ProbeStage.CollectReports,
            ProbeProgressCalculator.CollectReports(ratio),
            status);
    }

    private static void Report(Action<ProbeProgress>? onProgress, ProbeStage stage, int percent, string status)
    {
        if (onProgress is null)
        {
            return;
        }

        try
        {
            onProgress(new ProbeProgress(stage, Math.Clamp(percent, 0, 100), status));
        }
        catch
        {
            // Ignore callback failures.
        }
    }

    private sealed record EndpointSelectionCandidate(
        HidGamepadEndpoint Endpoint,
        string VendorId,
        string ProductId,
        string IdentityVendorId,
        string IdentityProductId,
        string TransportVendorId,
        string TransportProductId,
        string IdentityKey,
        string ProbeKey,
        string ModelKey,
        string HandshakeProfileId,
        IReadOnlyDictionary<byte, byte[]> ReportsById,
        GamepadCandidateSelection Selection);

    internal readonly record struct LearningDecision(
        bool Accepted,
        bool PersistProfile,
        bool IsPending,
        BatteryConfidence Confidence);

    private enum XboxGenericAcceptanceMode
    {
        BlockGeneric = 0,
        AllowEstimated = 1
    }

    private readonly record struct RevalidationRule(
        int SampleCount,
        int MinSuccessCount,
        int MaxSpread);

    private readonly record struct ProbePolicy(
        XboxGenericAcceptanceMode XboxGenericAcceptanceMode,
        int GenericMinScore,
        RevalidationRule RevalidationRule);

    private struct ReportAttemptStats
    {
        public int GetInputSuccessCount { get; private set; }

        public int GetInputFailureCount { get; private set; }

        public int StreamSuccessCount { get; private set; }

        public int StreamFailureCount { get; private set; }

        public int StreamTimeoutCount { get; private set; }

        public int AttemptCount { get; private set; }

        public int HardFailCount { get; private set; }

        public void Add(HidReportReadStatistics stats)
        {
            GetInputSuccessCount += stats.GetInputSuccessCount;
            GetInputFailureCount += stats.GetInputFailureCount;
            StreamSuccessCount += stats.StreamSuccessCount;
            StreamFailureCount += stats.StreamFailureCount;
            StreamTimeoutCount += stats.StreamTimeoutCount;
            AttemptCount += stats.AttemptCount;
            HardFailCount += stats.HardFailCount;
        }

        public void Add(ReportAttemptStats stats)
        {
            GetInputSuccessCount += stats.GetInputSuccessCount;
            GetInputFailureCount += stats.GetInputFailureCount;
            StreamSuccessCount += stats.StreamSuccessCount;
            StreamFailureCount += stats.StreamFailureCount;
            StreamTimeoutCount += stats.StreamTimeoutCount;
            AttemptCount += stats.AttemptCount;
            HardFailCount += stats.HardFailCount;
        }
    }

    private sealed class ProbeDiagnostics
    {
        public int StrictEndpointCount { get; set; }

        public int RelaxedEndpointCount { get; set; }

        public int GlobalEndpointCount { get; set; }

        public int HandleOpenSuccessCount { get; set; }

        public int HandleOpenFailureCount { get; set; }

        public int ReportReadSuccessCount { get; set; }

        public int ReportReadFailureCount { get; set; }

        public int GetInputSuccessCount { get; set; }

        public int GetInputFailureCount { get; set; }

        public int StreamSuccessCount { get; set; }

        public int StreamFailureCount { get; set; }

        public int StreamTimeoutCount { get; set; }

        public int HardFailCount { get; set; }

        public int EndpointExceptionCount { get; set; }

        public int ReportReadExceptionCount { get; set; }

        public int CooldownSkipCount { get; set; }

        public int GlobalExcludedCount { get; set; }

        public int AddressMismatchSkipCount { get; private set; }

        public int GlobalNoAddressSkipCount { get; private set; }

        public string TopCandidatesText { get; set; } = string.Empty;

        public string WinnerDecoder { get; set; } = string.Empty;

        public string BlockReason { get; set; } = string.Empty;

        public string IdentityKey { get; set; } = string.Empty;

        public string SuppressionReason { get; set; } = string.Empty;

        public string HandshakeProfileId { get; set; } = string.Empty;

        public string BrandHint { get; private set; } = string.Empty;

        public string AliasMatchSource { get; private set; } = string.Empty;

        public string ProfileSelectionReason { get; private set; } = string.Empty;

        public string IdleKeepReason { get; set; } = string.Empty;

        public bool IdleSuppressed { get; set; }

        public string RevalidationFailureKind { get; set; } = string.Empty;

        public string ProfileStateBefore { get; set; } = string.Empty;

        public string ProfileStateAfter { get; set; } = string.Empty;

        public string AcceptancePath { get; set; } = string.Empty;

        public string ActiveSource { get; set; } = "probe";

        public bool NoSignalRecoveryAttempted { get; set; }

        public bool NoSignalRecoveryRecovered { get; set; }

        public double DecoderConfidence { get; set; }

        public int BestObservedScore { get; private set; }

        public ProbeStage LastExceptionStage { get; private set; }

        public Exception? LastException { get; private set; }

        public string LastExceptionContext { get; private set; } = string.Empty;
        private Dictionary<byte, int> ObservedReportIdCounts { get; } = new();

        public void RegisterException(ProbeStage stage, Exception exception, string context)
        {
            LastExceptionStage = stage;
            LastException = exception;
            LastExceptionContext = context;
        }

        public void RegisterObservedReportId(byte reportId)
        {
            if (reportId == 0)
            {
                return;
            }

            if (!ObservedReportIdCounts.TryGetValue(reportId, out var count))
            {
                ObservedReportIdCounts[reportId] = 1;
                return;
            }

            ObservedReportIdCounts[reportId] = count + 1;
        }

        public void RegisterHandshakeSelection(
            string brandHint,
            string profileId,
            string profileSelectionReason)
        {
            if (!string.IsNullOrWhiteSpace(profileId))
            {
                HandshakeProfileId = profileId;
            }

            if (!string.IsNullOrWhiteSpace(brandHint) &&
                string.IsNullOrWhiteSpace(BrandHint))
            {
                BrandHint = brandHint.Trim();
            }

            if (!string.IsNullOrWhiteSpace(profileSelectionReason) &&
                string.IsNullOrWhiteSpace(ProfileSelectionReason))
            {
                ProfileSelectionReason = profileSelectionReason.Trim();
            }
        }

        public void RegisterAliasMatch(string aliasMatchSource)
        {
            if (string.IsNullOrWhiteSpace(aliasMatchSource))
            {
                return;
            }

            AliasMatchSource = aliasMatchSource.Trim();
        }

        public void RegisterEndpointSkip(string reason)
        {
            if (string.Equals(reason, "address-mismatch", StringComparison.OrdinalIgnoreCase))
            {
                AddressMismatchSkipCount++;
                RegisterAliasMatch("endpoint-address");
                return;
            }

            if (string.Equals(reason, "global-no-address", StringComparison.OrdinalIgnoreCase))
            {
                GlobalNoAddressSkipCount++;
            }
        }

        public int GetObservedReportCount(byte reportId)
        {
            if (reportId == 0)
            {
                return 0;
            }

            return ObservedReportIdCounts.TryGetValue(reportId, out var count)
                ? count
                : 0;
        }

        public void RegisterObservedScore(int score)
        {
            if (score > BestObservedScore)
            {
                BestObservedScore = score;
            }
        }

        public void AccumulateReadStats(ReportAttemptStats stats)
        {
            GetInputSuccessCount += stats.GetInputSuccessCount;
            GetInputFailureCount += stats.GetInputFailureCount;
            StreamSuccessCount += stats.StreamSuccessCount;
            StreamFailureCount += stats.StreamFailureCount;
            StreamTimeoutCount += stats.StreamTimeoutCount;
            HardFailCount += stats.HardFailCount;
        }

        public string ToDiagnosticText()
        {
            var contextPart = string.IsNullOrWhiteSpace(LastExceptionContext)
                ? string.Empty
                : $", context={LastExceptionContext}";
            var identityPart = string.IsNullOrWhiteSpace(IdentityKey)
                ? string.Empty
                : $", identity={IdentityKey}";
            var suppressionPart = string.IsNullOrWhiteSpace(SuppressionReason)
                ? string.Empty
                : $", suppression={SuppressionReason}";
            var handshakePart = string.IsNullOrWhiteSpace(HandshakeProfileId)
                ? string.Empty
                : $", handshake={HandshakeProfileId}";
            var brandPart = string.IsNullOrWhiteSpace(BrandHint)
                ? string.Empty
                : $", brand={BrandHint}";
            var aliasPart = string.IsNullOrWhiteSpace(AliasMatchSource)
                ? string.Empty
                : $", alias={AliasMatchSource}";
            var globalExcludedPart = GlobalExcludedCount > 0
                ? $", globalExcluded={GlobalExcludedCount}"
                : string.Empty;
            var skipAddressPart = AddressMismatchSkipCount > 0
                ? $", skipAddress={AddressMismatchSkipCount}"
                : string.Empty;
            var skipGlobalNoAddressPart = GlobalNoAddressSkipCount > 0
                ? $", skipGlobalNoAddress={GlobalNoAddressSkipCount}"
                : string.Empty;
            var profilePart = string.IsNullOrWhiteSpace(ProfileSelectionReason)
                ? string.Empty
                : $", profileReason={ProfileSelectionReason}";
            var idleKeepPart = string.IsNullOrWhiteSpace(IdleKeepReason)
                ? string.Empty
                : $", idleKeep={IdleKeepReason}";
            var idleSuppressedPart = IdleSuppressed
                ? ", idleSuppressed=1"
                : string.Empty;
            var revalidationPart = string.IsNullOrWhiteSpace(RevalidationFailureKind)
                ? string.Empty
                : $", revalidation={RevalidationFailureKind}";
            var stateBeforePart = string.IsNullOrWhiteSpace(ProfileStateBefore)
                ? string.Empty
                : $", stateBefore={ProfileStateBefore}";
            var stateAfterPart = string.IsNullOrWhiteSpace(ProfileStateAfter)
                ? string.Empty
                : $", stateAfter={ProfileStateAfter}";
            var acceptancePathPart = string.IsNullOrWhiteSpace(AcceptancePath)
                ? string.Empty
                : $", accept={AcceptancePath}";
            var sourcePart = string.IsNullOrWhiteSpace(ActiveSource)
                ? string.Empty
                : $", source={ActiveSource}";
            var recoveryPart = NoSignalRecoveryAttempted
                ? NoSignalRecoveryRecovered ? ", recoveryRound=1,recovered=1" : ", recoveryRound=1,recovered=0"
                : string.Empty;
            var bestScorePart = BestObservedScore > 0
                ? $", bestScore={BestObservedScore}"
                : string.Empty;

            return
                $"strict={StrictEndpointCount}, relaxed={RelaxedEndpointCount}, global={GlobalEndpointCount}, " +
                $"openOk={HandleOpenSuccessCount}, openFail={HandleOpenFailureCount}, " +
                $"readOk={ReportReadSuccessCount}, readFail={ReportReadFailureCount}, " +
                $"getInputOk={GetInputSuccessCount}, getInputFail={GetInputFailureCount}, " +
                $"streamOk={StreamSuccessCount}, streamFail={StreamFailureCount}, streamTimeout={StreamTimeoutCount}, " +
                $"hardFail={HardFailCount}, cooldownSkip={CooldownSkipCount}, endpointEx={EndpointExceptionCount}, readEx={ReportReadExceptionCount}, confidence={DecoderConfidence:0.000}{bestScorePart}{identityPart}{suppressionPart}{handshakePart}{brandPart}{aliasPart}{globalExcludedPart}{skipAddressPart}{skipGlobalNoAddressPart}{profilePart}{idleKeepPart}{idleSuppressedPart}{revalidationPart}{stateBeforePart}{stateAfterPart}{acceptancePathPart}{sourcePart}{recoveryPart}{contextPart}";
        }

        public string ToObservedReportText()
        {
            if (ObservedReportIdCounts.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",",
                ObservedReportIdCounts
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key)
                    .Select(pair => $"0x{pair.Key:X2}:{pair.Value}"));
        }
    }
}





