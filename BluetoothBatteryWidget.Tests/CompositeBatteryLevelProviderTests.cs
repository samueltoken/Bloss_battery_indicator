using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Tests;

public sealed class CompositeBatteryLevelProviderTests
{
    [Fact]
    public async Task RunProviderSafelyAsync_CompletesWithoutTimeout()
    {
        var result = await CompositeBatteryLevelProvider.RunProviderSafelyAsync(
            providerName: "test",
            timeout: TimeSpan.FromMilliseconds(300),
            provider: _ => Task.FromResult<IReadOnlyList<PnpBatteryReading>>(
            [
                new PnpBatteryReading(
                    InstanceId: "id",
                    Address: "AABBCCDD0011",
                    DisplayName: "Pad",
                    BatteryPercent: 77)
            ]),
            cancellationToken: CancellationToken.None);

        Assert.False(result.TimedOut);
        Assert.Single(result.Readings);
        Assert.Equal(77, result.Readings[0].BatteryPercent);
    }

    [Fact]
    public async Task RunProviderSafelyAsync_ReturnsEmptyWhenTimedOut()
    {
        var result = await CompositeBatteryLevelProvider.RunProviderSafelyAsync(
            providerName: "test",
            timeout: TimeSpan.FromMilliseconds(80),
            provider: async token =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return Array.Empty<PnpBatteryReading>();
            },
            cancellationToken: CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Empty(result.Readings);
    }
}
