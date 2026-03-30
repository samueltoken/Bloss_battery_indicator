using BluetoothBatteryWidget.App.Services;
using BluetoothBatteryWidget.App.ViewModels;
using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class ProbeFailureDetailTests
{
    [Fact]
    public async Task ProbeAsync_InvalidAddress_FillsErrorDetail()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"bloss-profile-{Guid.NewGuid():N}.json");
        var profileStore = new GamepadProfileStore(tempPath);
        var pendingStorePath = Path.Combine(Path.GetTempPath(), $"bloss-votes-{Guid.NewGuid():N}.json");
        var pendingStore = new PendingGamepadCandidateStore(pendingStorePath);
        var service = new GamepadProbeService(profileStore, pendingStore);
        var device = new ConnectedBluetoothDevice(
            DeviceId: "test-device",
            Address: "invalid-address",
            DisplayName: "Test Controller",
            IsConnected: true,
            CategoryHint: "gamepad");

        var result = await service.ProbeAsync(device, onProgress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorDetail);
        Assert.Equal(ProbeStage.DeviceCheck, result.ErrorDetail!.Stage);
        Assert.Equal(ProbeFailureKind.AddressInvalid, result.ErrorDetail.FailureKind);
        Assert.Contains("strict=", result.ErrorDetail.DiagnosticsText);
    }

    [Fact]
    public void BuildProbeFailureStatus_ShowsSummaryWithoutFullDiagnostics()
    {
        var result = new ProbeResult(
            Success: false,
            BatteryPercent: null,
            Message: "수집 후보 해석 실패",
            Profile: null,
            ErrorDetail: new ProbeErrorDetail(
                Stage: ProbeStage.EvaluateCandidates,
                ExceptionType: "ObjectDisposedException",
                ExceptionMessage: "cannot access disposed object",
                DiagnosticsText: "strict=1, relaxed=0, global=2, openOk=1, openFail=0, readOk=2, readFail=11, context=Read reportId=0x82",
                Timestamp: DateTimeOffset.Now,
                OpenSuccessCount: 1,
                OpenFailureCount: 0,
                ReadSuccessCount: 2,
                ReadFailureCount: 11));

        var status = MainViewModel.BuildProbeFailureStatus(result);

        Assert.Contains("수집 후보 해석 실패", status);
        Assert.Contains("stage: EvaluateCandidates", status);
        Assert.Contains("ex: ObjectDisposedException", status);
        Assert.Contains("openOk=1", status);
        Assert.Contains("readFail=11", status);
        Assert.DoesNotContain("diag:", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("context=", status, StringComparison.OrdinalIgnoreCase);
    }
}
