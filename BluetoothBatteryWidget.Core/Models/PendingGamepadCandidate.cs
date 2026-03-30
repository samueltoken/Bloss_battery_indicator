namespace BluetoothBatteryWidget.Core.Models;

public sealed record PendingGamepadCandidate(
    string ModelKey,
    string CandidateKey,
    int Score,
    int VoteCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    string EvidenceType = "unknown",
    string LastValidationStats = ""
);
