using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class XboxBatteryMatcher
{
    private const int MinimumWinnerScore = 45;
    private const int MinimumWinnerLead = 8;
    private const int StickyWinnerScoreTolerance = 8;

    public static IReadOnlyList<PnpBatteryReading> MatchStrict(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        IReadOnlyList<XInputBatteryReading> xInputReadings)
    {
        return MatchBestEffort(connectedDevices, xInputReadings, endpointSignalsByAddress: null);
    }

    public static IReadOnlyList<PnpBatteryReading> MatchBestEffort(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        IReadOnlyList<XInputBatteryReading> xInputReadings,
        IReadOnlyDictionary<string, string>? endpointSignalsByAddress)
    {
        if (xInputReadings.Count == 0)
        {
            return [];
        }

        var scoreBoard = BuildScoreBoard(connectedDevices, endpointSignalsByAddress);
        if (scoreBoard.Count == 0)
        {
            return [];
        }

        if (xInputReadings.Count > 1)
        {
            // Avoid ambiguous multi-slot assignment for Bluetooth Xbox-layer devices.
            return [];
        }

        var winner = SelectUniqueWinner(scoreBoard);
        if (winner is null)
        {
            return [];
        }

        var reading = xInputReadings[0];
        string? winnerSignal = null;
        endpointSignalsByAddress?.TryGetValue(winner.Address, out winnerSignal);
        var modelScopeKey = BuildModelScopeKey(winner.Address, winner.DisplayName, winnerSignal);
        return
        [
            new PnpBatteryReading(
                InstanceId: $"XINPUT_SLOT_{reading.UserIndex}",
                Address: winner.Address,
                DisplayName: winner.DisplayName,
                BatteryPercent: reading.BatteryPercent,
                BatteryConfidence: BatteryConfidence.Confirmed,
                SourceKind: BatterySourceKind.XInput,
                RawMetric: reading.RawMetric,
                ModelKey: modelScopeKey)
        ];
    }

    public static IReadOnlyList<PnpBatteryReading> MatchGameInputBestEffort(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        IReadOnlyList<GameInputBatteryReading> gameInputReadings,
        IReadOnlyDictionary<string, string>? endpointSignalsByAddress,
        string? preferredAddress = null)
    {
        if (gameInputReadings.Count == 0)
        {
            return [];
        }

        var scoreBoard = BuildScoreBoard(connectedDevices, endpointSignalsByAddress);
        if (scoreBoard.Count == 0)
        {
            return [];
        }

        if (gameInputReadings.Count > 1)
        {
            // Avoid ambiguous multi-pad mapping unless we can identify per-device source.
            return [];
        }

        var winner = SelectPreferredWinner(scoreBoard, preferredAddress);
        if (winner is null)
        {
            return [];
        }

        var reading = gameInputReadings[0];
        string? winnerSignal = null;
        endpointSignalsByAddress?.TryGetValue(winner.Address, out winnerSignal);
        var modelScopeKey = BuildModelScopeKey(winner.Address, winner.DisplayName, winnerSignal);
        return
        [
            new PnpBatteryReading(
                InstanceId: $"GAMEINPUT_SLOT_{reading.SourceIndex}",
                Address: winner.Address,
                DisplayName: winner.DisplayName,
                BatteryPercent: reading.BatteryPercent,
                BatteryConfidence: BatteryConfidence.Estimated,
                SourceKind: BatterySourceKind.GameInput,
                RawMetric: reading.RawMetric,
                ModelKey: modelScopeKey)
        ];
    }

    private static MatchedXboxCandidate? SelectPreferredWinner(
        IReadOnlyList<MatchedXboxCandidate> scoreBoard,
        string? preferredAddress)
    {
        if (!string.IsNullOrWhiteSpace(preferredAddress))
        {
            var normalizedPreferred = AddressNormalizer.NormalizeAddress(preferredAddress);
            var preferredCandidate = scoreBoard.FirstOrDefault(candidate =>
                string.Equals(candidate.Address, normalizedPreferred, StringComparison.OrdinalIgnoreCase));
            if (preferredCandidate is not null)
            {
                var topScore = scoreBoard.Max(candidate => candidate.Score);
                if (preferredCandidate.Score >= Math.Max(MinimumWinnerScore - StickyWinnerScoreTolerance, topScore - StickyWinnerScoreTolerance))
                {
                    return preferredCandidate;
                }
            }
        }

        return SelectUniqueWinner(scoreBoard);
    }

    private static string BuildModelScopeKey(string address, string displayName, string? endpointSignal)
    {
        string? transportVendorId = null;
        string? transportProductId = null;
        if (HidProbeTextParser.TryParseVidPid(endpointSignal, out var parsedVendor, out var parsedProduct))
        {
            transportVendorId = parsedVendor;
            transportProductId = parsedProduct;
        }

        return BatteryModelKeyResolver.ResolveIdentityKey(
            identityVendorId: null,
            identityProductId: null,
            transportVendorId: transportVendorId,
            transportProductId: transportProductId,
            address: address,
            displayName: displayName,
            endpointSignature: endpointSignal);
    }

    private static IReadOnlyList<MatchedXboxCandidate> BuildScoreBoard(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        IReadOnlyDictionary<string, string>? endpointSignalsByAddress)
    {
        if (connectedDevices.Count == 0)
        {
            return [];
        }

        var gamepadCandidates = connectedDevices
            .Where(device => device.IsConnected)
            .Select(device =>
            {
                var category = DeviceCategoryClassifier.Classify(device.DisplayName, device.CategoryHint);
                return new { Device = device, Category = category };
            })
            .Where(entry => entry.Category == DeviceCategory.Gamepad)
            .ToList();

        if (gamepadCandidates.Count == 0)
        {
            return [];
        }

        var result = new List<MatchedXboxCandidate>(gamepadCandidates.Count);
        foreach (var entry in gamepadCandidates)
        {
            var device = entry.Device;
            var normalizedAddress = AddressNormalizer.NormalizeAddress(device.Address);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                continue;
            }

            string? signal = null;
            endpointSignalsByAddress?.TryGetValue(normalizedAddress, out signal);
            var score = CalculateCandidateScore(device, signal, gamepadCandidates.Count);
            var hasSignalEvidence = HasSignalEvidence(signal);
            result.Add(new MatchedXboxCandidate(
                DeviceId: device.DeviceId,
                Address: normalizedAddress,
                DisplayName: device.DisplayName,
                Score: score,
                HasSignalEvidence: hasSignalEvidence));
        }

        return result;
    }

    private static MatchedXboxCandidate? SelectUniqueWinner(IReadOnlyList<MatchedXboxCandidate> scoreBoard)
    {
        if (scoreBoard.Count == 0)
        {
            return null;
        }

        var ordered = scoreBoard
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var winner = ordered[0];
        if (winner.Score < MinimumWinnerScore)
        {
            return null;
        }

        if (ordered.Count > 1 && !winner.HasSignalEvidence)
        {
            return null;
        }

        if (ordered.Count == 1)
        {
            return winner;
        }

        var runnerUp = ordered[1];
        var lead = winner.Score - runnerUp.Score;
        if (lead < MinimumWinnerLead)
        {
            return null;
        }

        return winner;
    }

    private static int CalculateCandidateScore(
        ConnectedBluetoothDevice device,
        string? endpointSignal,
        int candidateCount)
    {
        var score = 0;
        var name = device.DisplayName ?? string.Empty;
        var source = string.IsNullOrWhiteSpace(endpointSignal)
            ? string.Empty
            : endpointSignal;

        if (name.Contains("xbox", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("xinput", StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        if (name.Contains("controller", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("gamepad", StringComparison.OrdinalIgnoreCase))
        {
            score += 14;
        }

        if (!string.IsNullOrWhiteSpace(device.CategoryHint) &&
            (device.CategoryHint.Contains("gaming", StringComparison.OrdinalIgnoreCase) ||
             device.CategoryHint.Contains("input", StringComparison.OrdinalIgnoreCase)))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            if (source.Contains("VID_", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("PID_", StringComparison.OrdinalIgnoreCase))
            {
                score += 16;
            }

            if (source.Contains("xusb", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("xinput", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("ig_", StringComparison.OrdinalIgnoreCase))
            {
                score += 14;
            }

            if (source.Contains("045E", StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }

        if (candidateCount == 1)
        {
            score += 8;
        }

        return score;
    }

    private static bool HasSignalEvidence(string? endpointSignal)
    {
        if (string.IsNullOrWhiteSpace(endpointSignal))
        {
            return false;
        }

        return endpointSignal.Contains("VID_", StringComparison.OrdinalIgnoreCase) ||
               endpointSignal.Contains("PID_", StringComparison.OrdinalIgnoreCase) ||
               endpointSignal.Contains("xusb", StringComparison.OrdinalIgnoreCase) ||
               endpointSignal.Contains("xinput", StringComparison.OrdinalIgnoreCase) ||
               endpointSignal.Contains("ig_", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MatchedXboxCandidate(
        string DeviceId,
        string Address,
        string DisplayName,
        int Score,
        bool HasSignalEvidence);
}
