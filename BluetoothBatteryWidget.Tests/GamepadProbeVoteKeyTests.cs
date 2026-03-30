using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GamepadProbeVoteKeyTests
{
    [Fact]
    public void BuildVoteCandidateKey_NormalizesCoreIdentityFields()
    {
        var key = GamepadProbeService.BuildVoteCandidateKey(
            identityKey: "id=vid_045e|pid_02e0|fp_test",
            decoder: "xbox_bt_flags",
            reportId: 0x04,
            offset: 1);

        Assert.Contains("IDK_ID=VID_045E|PID_02E0|FP_TEST", key, StringComparison.Ordinal);
        Assert.Contains("RID_04", key, StringComparison.Ordinal);
        Assert.Contains("OFF_1", key, StringComparison.Ordinal);
        Assert.Contains("DEC_XBOX_BT_FLAGS", key, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildVoteCandidateKey_DoesNotDependOnReportLength()
    {
        var keyA = GamepadProbeService.BuildVoteCandidateKey(
            identityKey: "ID_TEST",
            decoder: "xbox_bt_flags",
            reportId: 0x04,
            offset: 1);
        var keyB = GamepadProbeService.BuildVoteCandidateKey(
            identityKey: "ID_TEST",
            decoder: "xbox_bt_flags",
            reportId: 0x04,
            offset: 1);

        Assert.Equal(keyA, keyB);
    }
}
