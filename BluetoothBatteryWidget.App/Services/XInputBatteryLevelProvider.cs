using System.Runtime.InteropServices;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

public sealed class XInputBatteryLevelProvider
{
    private const uint ErrorSuccess = 0;
    private const byte BatteryDevTypeGamepad = 0x00;
    private const byte BatteryTypeDisconnected = 0x00;
    private const byte BatteryTypeWired = 0x01;

    public Task<IReadOnlyList<PnpBatteryReading>> GetBatteryLevelsAsync(
        IReadOnlyList<ConnectedBluetoothDevice> connectedDevices,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<XInputBatteryReading> xinputReadings;
        try
        {
            xinputReadings = ReadXInputBatteryReadings(cancellationToken);
        }
        catch (DllNotFoundException)
        {
            return Task.FromResult<IReadOnlyList<PnpBatteryReading>>([]);
        }
        catch (EntryPointNotFoundException)
        {
            return Task.FromResult<IReadOnlyList<PnpBatteryReading>>([]);
        }

        var endpointSignals = XboxEndpointSignalBuilder.Build(connectedDevices, cancellationToken);
        var matched = XboxBatteryMatcher.MatchBestEffort(connectedDevices, xinputReadings, endpointSignals);
        return Task.FromResult(matched);
    }

    private static IReadOnlyList<XInputBatteryReading> ReadXInputBatteryReadings(CancellationToken cancellationToken)
    {
        var result = new List<XInputBatteryReading>(4);

        for (uint userIndex = 0; userIndex < 4; userIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = XInputGetBatteryInformation(userIndex, BatteryDevTypeGamepad, out var battery);
            if (status != ErrorSuccess)
            {
                continue;
            }

            if (battery.BatteryType is BatteryTypeDisconnected or BatteryTypeWired)
            {
                continue;
            }

            var mapped = XInputBatteryLevelMapper.ToPercent(battery.BatteryLevel);
            if (mapped is null)
            {
                continue;
            }

            result.Add(new XInputBatteryReading((int)userIndex, mapped.Value, battery.BatteryLevel));
        }

        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern uint XInputGetBatteryInformation(
        uint userIndex,
        byte devType,
        out XInputBatteryInformation batteryInformation);
}
