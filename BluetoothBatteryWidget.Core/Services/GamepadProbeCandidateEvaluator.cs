using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class GamepadProbeCandidateEvaluator
{
    private static readonly byte[] ReportIdPreference = [0x04, 0x31, 0x11, 0x01, 0x21, 0x81, 0x82];

    public const string DecoderPercent100 = "percent100";
    public const string DecoderPercent255 = "percent255";
    public const string DecoderNibble10 = "nibble10";
    public const string DecoderXboxBluetoothFlags = XboxBluetoothBatteryDecoder.DecoderId;

    public static GamepadCandidateSelection SelectBest(IReadOnlyDictionary<byte, byte[]> reportsById)
    {
        if (reportsById.Count == 0)
        {
            return new GamepadCandidateSelection(null, IsTie: false, CandidateCount: 0);
        }

        var candidates = BuildCandidates(reportsById);
        if (candidates.Count == 0)
        {
            return new GamepadCandidateSelection(null, IsTie: false, CandidateCount: 0);
        }

        var uniqueCandidates = candidates
            .GroupBy(candidate => new { candidate.Offset, candidate.Decoder, candidate.BatteryPercent })
            .Select(group => group
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.ReportLength)
                .ThenBy(candidate => candidate.ReportId)
                .First())
            .ToList();

        var signatureFrequency = candidates
            .GroupBy(candidate => (candidate.Offset, candidate.Decoder, candidate.BatteryPercent))
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ReportId).Distinct().Count());

        var ordered = uniqueCandidates
            .OrderByDescending(candidate => string.Equals(candidate.Decoder, DecoderXboxBluetoothFlags, StringComparison.Ordinal))
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => signatureFrequency.GetValueOrDefault((candidate.Offset, candidate.Decoder, candidate.BatteryPercent), 0))
            .ThenBy(candidate => candidate.Offset <= 1 ? 1 : 0)
            .ThenBy(candidate => GetReportIdPriority(candidate.ReportId))
            .ThenByDescending(candidate => candidate.ReportLength)
            .ThenBy(candidate => candidate.Offset)
            .ThenBy(candidate => candidate.Decoder, StringComparer.Ordinal)
            .ThenByDescending(candidate => candidate.BatteryPercent)
            .ToList();

        return new GamepadCandidateSelection(ordered[0], IsTie: false, CandidateCount: uniqueCandidates.Count);
    }

    public static GamepadBatteryProfile ToProfile(
        string vendorId,
        string productId,
        GamepadBatteryCandidate candidate,
        string identityKey = "",
        BatteryConfidence confidence = BatteryConfidence.Confirmed)
    {
        return new GamepadBatteryProfile(
            VendorId: vendorId.ToUpperInvariant(),
            ProductId: productId.ToUpperInvariant(),
            ReportId: candidate.ReportId,
            ReportLength: candidate.ReportLength,
            Offset: candidate.Offset,
            Decoder: candidate.Decoder,
            Score: candidate.Score,
            Confidence: confidence,
            IdentityKey: identityKey);
    }

    private static List<GamepadBatteryCandidate> BuildCandidates(IReadOnlyDictionary<byte, byte[]> reportsById)
    {
        var rawCandidates = new List<(byte ReportId, int ReportLength, int Offset, string Decoder, int Percent, int BaseScore, bool OnUsb)>();

        foreach (var pair in reportsById)
        {
            var reportId = pair.Key;
            var report = pair.Value;
            if (report.Length == 0)
            {
                continue;
            }

            if (XboxBluetoothBatteryDecoder.TryDecode(reportId, report, out var xboxPercent, out var onUsb))
            {
                // Favor dedicated Xbox Bluetooth flags over generic byte-decoder guesses.
                rawCandidates.Add((reportId, report.Length, 1, DecoderXboxBluetoothFlags, xboxPercent, onUsb ? 80 : 94, onUsb));
            }

            for (var offset = 0; offset < report.Length; offset++)
            {
                var raw = report[offset];
                if (raw is 0x00 or 0xFF)
                {
                    continue;
                }

                if (TryDecodePercent100(raw, out var direct))
                {
                    rawCandidates.Add((reportId, report.Length, offset, DecoderPercent100, direct, 60, OnUsb: false));
                }

                if (TryDecodePercent255(raw, out var scaled))
                {
                    rawCandidates.Add((reportId, report.Length, offset, DecoderPercent255, scaled, 45, OnUsb: false));
                }

                if (TryDecodeNibble10(raw, out var nibble))
                {
                    rawCandidates.Add((reportId, report.Length, offset, DecoderNibble10, nibble, 55, OnUsb: false));
                }
            }
        }

        if (rawCandidates.Count == 0)
        {
            return [];
        }

        var stableFrequency = rawCandidates
            .GroupBy(candidate => new { candidate.Offset, candidate.Decoder, candidate.Percent })
            .ToDictionary(
                group => (group.Key.Offset, group.Key.Decoder, group.Key.Percent),
                group => group.Select(item => item.ReportId).Distinct().Count());
        var spreadByOffsetDecoder = rawCandidates
            .GroupBy(candidate => (candidate.Offset, candidate.Decoder))
            .ToDictionary(
                group => group.Key,
                group => group.Max(item => item.Percent) - group.Min(item => item.Percent));

        var result = new List<GamepadBatteryCandidate>(rawCandidates.Count);
        foreach (var candidate in rawCandidates)
        {
            var score = candidate.BaseScore;

            if (candidate.Percent is >= 5 and <= 95)
            {
                score += 10;
            }

            if (stableFrequency.TryGetValue((candidate.Offset, candidate.Decoder, candidate.Percent), out var frequency) && frequency >= 2)
            {
                score += 15;
            }
            else
            {
                score -= 8;
            }

            if (candidate.Offset <= 1)
            {
                score -= 12;
            }

            if (frequency >= 3)
            {
                score += 4;
            }

            if (spreadByOffsetDecoder.TryGetValue((candidate.Offset, candidate.Decoder), out var spread))
            {
                if (spread > 50)
                {
                    score -= 18;
                }
                else if (spread > 30)
                {
                    score -= 10;
                }
            }

            if (candidate.Decoder == DecoderXboxBluetoothFlags)
            {
                score += candidate.OnUsb ? -6 : 6;
            }

            result.Add(new GamepadBatteryCandidate(
                ReportId: candidate.ReportId,
                ReportLength: candidate.ReportLength,
                Offset: candidate.Offset,
                Decoder: candidate.Decoder,
                BatteryPercent: candidate.Percent,
                Score: Math.Max(0, score)));
        }

        return result;
    }

    private static int GetReportIdPriority(byte reportId)
    {
        var index = Array.IndexOf(ReportIdPreference, reportId);
        return index >= 0 ? index : ReportIdPreference.Length + reportId;
    }

    private static bool TryDecodePercent100(byte raw, out int percent)
    {
        if (raw > 100)
        {
            percent = 0;
            return false;
        }

        percent = raw;
        return true;
    }

    private static bool TryDecodePercent255(byte raw, out int percent)
    {
        percent = (int)Math.Round(raw / 255d * 100d, MidpointRounding.AwayFromZero);
        return percent is >= 0 and <= 100;
    }

    private static bool TryDecodeNibble10(byte raw, out int percent)
    {
        var nibble = raw & 0x0F;
        if (nibble > 10)
        {
            percent = 0;
            return false;
        }

        percent = nibble == 10 ? 100 : nibble * 10 + 5;
        return true;
    }
}
