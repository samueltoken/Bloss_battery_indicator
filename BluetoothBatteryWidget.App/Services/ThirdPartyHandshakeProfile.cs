namespace BluetoothBatteryWidget.App.Services;

internal sealed record ThirdPartyInitPacket(
    byte[] Payload,
    int DelayAfterMs = 0);

internal sealed record ThirdPartyHandshakeProfile(
    string ProfileId,
    IReadOnlyList<ThirdPartyInitPacket> InitPackets,
    IReadOnlyList<byte> FeatureReportIds,
    IReadOnlyList<byte> PreferredInputReportIds,
    IReadOnlyList<byte> RecoveryInputReportIds,
    int MinimumReportSize = 64
);

internal sealed record ThirdPartyHandshakeSelection(
    ThirdPartyHandshakeProfile Profile,
    string BrandHint,
    string ProfileSelectionReason
);
