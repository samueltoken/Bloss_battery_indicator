using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;
using Xunit.Abstractions;

namespace BluetoothBatteryWidget.Tests;

public sealed class SteamControllerTritonHardwareSmokeTests
{
    private readonly ITestOutputHelper _output;

    public SteamControllerTritonHardwareSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ReadSnapshots_WhenHardwareSmokeEnabled_ReturnsSteamPuckSnapshot()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BLOSS_STEAM_TRITON_HARDWARE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var reader = new SteamControllerTritonHidReader();
        var snapshots = reader.ReadSnapshots(CancellationToken.None);
        var endpoints = HidGamepadAccess.EnumerateSteamControllerTritonEndpoints(CancellationToken.None);

        foreach (var endpoint in endpoints)
        {
            using var handle = HidGamepadAccess.OpenHandle(endpoint.DevicePath);
            _output.WriteLine(
                $"endpoint product={endpoint.ProductId}, address={endpoint.Address}, instance={endpoint.InstanceId}, open={!handle.IsInvalid}");
            if (handle.IsInvalid)
            {
                continue;
            }

            if (HidGamepadAccess.TryReadInputReportSnapshot(handle, 0x43, 17, out var batteryReport, out var batteryError) &&
                BluetoothBatteryWidget.Core.Services.GamepadBatteryParser.TryParseSteamTritonBatteryStatus(batteryReport, out var batteryStatus))
            {
                var raw = BitConverter.ToString(batteryReport.Take(Math.Min(17, batteryReport.Length)).ToArray());
                _output.WriteLine(
                    $"  battery-report raw={raw}, percent={batteryStatus.BatteryPercent}, state={batteryStatus.ChargeState}, " +
                    $"charging={batteryStatus.IsCharging}, complete={batteryStatus.IsChargeComplete}, battMv={batteryStatus.BatteryVoltage}, sysMv={batteryStatus.SystemVoltage}, " +
                    $"inputMv={batteryStatus.InputVoltage}, current={batteryStatus.Current}, inputCurrent={batteryStatus.InputCurrent}, temp={batteryStatus.Temperature}");
            }
            else
            {
                _output.WriteLine($"  battery-report read=false, error={batteryError}");
            }

            using var session = new HidInputStreamSession(handle);
            if (!session.IsAvailable)
            {
                _output.WriteLine("  stream=unavailable");
                continue;
            }

            for (var index = 0; index < 8; index++)
            {
                if (!session.TryReadReport(0x00, 64, 140, out var frame, out var timedOut))
                {
                    _output.WriteLine($"  frame={index}, read=false, timedOut={timedOut}");
                    continue;
                }

                var prefix = BitConverter.ToString(frame.Data.Take(Math.Min(12, frame.Data.Length)).ToArray());
                _output.WriteLine($"  frame={index}, report=0x{frame.ReportId:X2}, bytes={frame.Data.Length}, data={prefix}");
            }
        }

        foreach (var snapshot in snapshots)
        {
            _output.WriteLine(
                $"address={snapshot.Address}, product={snapshot.ProductId}, connected={snapshot.IsConnected}, " +
                $"battery={snapshot.BatteryStatus?.BatteryPercent.ToString() ?? "none"}, " +
                $"charging={snapshot.BatteryStatus?.IsCharging.ToString() ?? "none"}, " +
                $"complete={snapshot.BatteryStatus?.IsChargeComplete.ToString() ?? "none"}, " +
                $"battMv={snapshot.BatteryStatus?.BatteryVoltage.ToString() ?? "none"}, " +
                $"sysMv={snapshot.BatteryStatus?.SystemVoltage.ToString() ?? "none"}, " +
                $"inputMv={snapshot.BatteryStatus?.InputVoltage.ToString() ?? "none"}, endpoints={snapshot.EndpointCount}");
        }

        var connectedDevices = snapshots
            .Where(snapshot => snapshot.IsConnected)
            .Select(snapshot => new ConnectedBluetoothDevice(
                DeviceId: snapshot.DeviceId,
                Address: snapshot.Address,
                DisplayName: snapshot.DisplayName,
                IsConnected: true,
                CategoryHint: "gamepad controller steam puck"))
            .ToList();
        if (connectedDevices.Count > 0)
        {
            var provider = new SteamControllerTritonBatteryProvider(reader);
            var providerReadings = await provider
                .GetBatteryLevelsAsync(connectedDevices, CancellationToken.None);
            foreach (var reading in providerReadings)
            {
                _output.WriteLine(
                    $"provider address={reading.Address}, percent={reading.BatteryPercent?.ToString() ?? "none"}, " +
                    $"raw={reading.RawMetric?.ToString() ?? "none"}, confidence={reading.BatteryConfidence}, " +
                    $"charging={reading.IsCharging}, complete={reading.IsChargeComplete}, reason={reading.ReasonCode}");
            }

            var resolver = new BatteryEvidenceResolver(new BatteryObservationStore(), new CalibrationStore());
            var resolved = resolver.ResolveAndRecord(providerReadings, DateTimeOffset.Now);
            foreach (var reading in resolved)
            {
                _output.WriteLine(
                    $"resolved address={reading.Address}, percent={reading.BatteryPercent?.ToString() ?? "none"}, " +
                    $"raw={reading.RawMetric?.ToString() ?? "none"}, confidence={reading.BatteryConfidence}, " +
                    $"charging={reading.IsCharging}, complete={reading.IsChargeComplete}, suspect={reading.IsBatterySuspect}, state={reading.DisplayState}, " +
                    $"reason={reading.ReasonCode}");
            }
        }

        Assert.NotEmpty(endpoints);
        if (snapshots.Count == 0)
        {
            _output.WriteLine("no controller snapshot exposed; this is expected when only the Puck is present");
            return;
        }

        Assert.Contains(snapshots, snapshot =>
            snapshot.ProductId.Equals("1304", StringComparison.OrdinalIgnoreCase) ||
            snapshot.ProductId.Equals("1305", StringComparison.OrdinalIgnoreCase));
    }
}
