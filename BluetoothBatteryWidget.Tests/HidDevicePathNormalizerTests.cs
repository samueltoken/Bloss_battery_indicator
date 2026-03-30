using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class HidDevicePathNormalizerTests
{
    [Fact]
    public void Normalize_RepairsMissingUncPrefix()
    {
        var raw = @"?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0ce6#9&367e92d7&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        var normalized = HidDevicePathNormalizer.Normalize(raw);

        Assert.StartsWith(@"\\?\hid#", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_KeepsValidPrefix()
    {
        var raw = @"\\?\hid#vid_0b05&pid_1866&mi_01#8&5c07641&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}\kbd";
        var normalized = HidDevicePathNormalizer.Normalize(raw);

        Assert.Equal(raw, normalized);
    }

    [Fact]
    public void Normalize_NormalizesSlashDirection()
    {
        var raw = @"?\hid#/path/with/slash";
        var normalized = HidDevicePathNormalizer.Normalize(raw);

        Assert.Equal(@"\\?\hid#\path\with\slash", normalized);
    }
}
