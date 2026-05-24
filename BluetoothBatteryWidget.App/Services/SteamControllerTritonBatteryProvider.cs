using BluetoothBatteryWidget.Core.Interfaces;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class SteamControllerTritonBatteryProvider : IBatteryLevelProvider
{
    private readonly SteamControllerTritonHidReader _reader;

    public SteamControllerTritonBatteryProvider(SteamControllerTritonHidReader reader)
    {
        _reader = reader;
    }

    public async Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        var steamAddresses = connectedDevices
            .Where(device =>
                device.DeviceId.StartsWith("steam-triton:", StringComparison.OrdinalIgnoreCase) ||
                device.DisplayName.Contains("steam", StringComparison.OrdinalIgnoreCase))
            .Select(device => device.Address)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (steamAddresses.Count == 0)
        {
            return [];
        }

        var snapshots = await Task.Run(
            () => _reader.ReadSnapshots(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var readings = new List<PnpBatteryReading>();
        foreach (var snapshot in snapshots)
        {
            if (!steamAddresses.Contains(snapshot.Address) || snapshot.BatteryStatus is null)
            {
                continue;
            }

            var status = snapshot.BatteryStatus;
            if (!SteamControllerTritonHidReader.IsDisplayableControllerBattery(status))
            {
                continue;
            }

            var batteryPercent = status.BatteryPercent;
            var rawMetric = (double?)status.BatteryPercent;
            var confidence = BatteryConfidence.Confirmed;
            var reliabilityScore = 98;
            var reasonCode = status.IsChargeComplete
                ? "steam_triton_charge_complete"
                : status.IsCharging ? "steam_triton_charging" : "steam_triton_battery";
            if (SteamControllerTritonHidReader.IsSuspiciousDockedFullBattery(status) &&
                SteamControllerBatteryEstimator.TryEstimatePercentFromVoltage(status, out _))
            {
                rawMetric = status.BatteryVoltage;
            }

            readings.Add(new PnpBatteryReading(
                InstanceId: snapshot.DeviceId,
                Address: snapshot.Address,
                DisplayName: snapshot.DisplayName,
                BatteryPercent: batteryPercent,
                BatteryConfidence: confidence,
                SourceKind: BatterySourceKind.SteamHid,
                RawMetric: rawMetric,
                ModelKey: snapshot.ModelKey,
                ObservedAt: DateTimeOffset.Now,
                ReliabilityScore: reliabilityScore,
                ReasonCode: reasonCode,
                ActiveSource: "steamhid",
                PathType: "receiver",
                DisplayState: status.IsCharging ? BatteryDisplayState.Charging : BatteryDisplayState.Verified,
                IsCharging: status.IsCharging,
                IsChargeComplete: status.IsChargeComplete));
        }

        return readings;
    }
}
