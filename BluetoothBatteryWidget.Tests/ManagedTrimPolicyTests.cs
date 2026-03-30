using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class ManagedTrimPolicyTests
{
    [Fact]
    public void ShouldRunManagedTrim_ReturnsFalse_WhenBelowThreshold()
    {
        var now = new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc);
        var result = ManagedTrimPolicy.ShouldRunManagedTrim(
            privateMb: 179.9,
            thresholdMb: 180.0,
            nowUtc: now,
            lastRunUtc: now.AddMinutes(-10),
            minInterval: TimeSpan.FromMinutes(3));

        Assert.False(result);
    }

    [Fact]
    public void ShouldRunManagedTrim_ReturnsFalse_WhenIntervalNotElapsed()
    {
        var now = new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc);
        var result = ManagedTrimPolicy.ShouldRunManagedTrim(
            privateMb: 190.0,
            thresholdMb: 180.0,
            nowUtc: now,
            lastRunUtc: now.AddMinutes(-2),
            minInterval: TimeSpan.FromMinutes(3));

        Assert.False(result);
    }

    [Fact]
    public void ShouldRunManagedTrim_ReturnsTrue_WhenThresholdExceededAndIntervalElapsed()
    {
        var now = new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc);
        var result = ManagedTrimPolicy.ShouldRunManagedTrim(
            privateMb: 190.0,
            thresholdMb: 180.0,
            nowUtc: now,
            lastRunUtc: now.AddMinutes(-3),
            minInterval: TimeSpan.FromMinutes(3));

        Assert.True(result);
    }
}
