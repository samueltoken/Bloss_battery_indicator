namespace BluetoothBatteryWidget.Core.Models;

public sealed record BatteryEvidence
{
    public BatteryEvidence()
    {
    }

    public BatteryEvidence(
        string Address,
        string ModelKey,
        BatterySourceKind SourceKind,
        int DerivedPercent,
        double? RawMetric,
        DateTimeOffset ObservedAt,
        bool IsCharging = false,
        bool IsChargeComplete = false,
        string ReasonCode = "")
    {
        this.Address = Address;
        this.ModelKey = ModelKey;
        this.SourceKind = SourceKind;
        this.DerivedPercent = DerivedPercent;
        this.RawMetric = RawMetric;
        this.ObservedAt = ObservedAt;
        this.IsCharging = IsCharging;
        this.IsChargeComplete = IsChargeComplete;
        this.ReasonCode = ReasonCode;
    }

    public string Address { get; init; } = string.Empty;

    public string ModelKey { get; init; } = string.Empty;

    public BatterySourceKind SourceKind { get; init; } = BatterySourceKind.Unknown;

    public int DerivedPercent { get; init; }

    public double? RawMetric { get; init; }

    public DateTimeOffset ObservedAt { get; init; }

    public bool IsCharging { get; init; }

    public bool IsChargeComplete { get; init; }

    public string ReasonCode { get; init; } = string.Empty;
}
