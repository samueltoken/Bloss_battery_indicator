using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class GamepadProfileStoreTests
{
    [Fact]
    public void TryGetBestForModel_ReturnsHighestScoreProfile()
    {
        var path = CreateTempFilePath();
        try
        {
            var store = new GamepadProfileStore(path);
            store.Upsert(new GamepadBatteryProfile("054C", "0CE6", 0x31, 78, 10, GamepadProbeCandidateEvaluator.DecoderPercent100, 72));
            store.Upsert(new GamepadBatteryProfile("054C", "0CE6", 0x31, 78, 11, GamepadProbeCandidateEvaluator.DecoderPercent100, 85));

            var found = store.TryGetBestForModel("054C", "0CE6", out var profile);

            Assert.True(found);
            Assert.Equal(85, profile.Score);
            Assert.Equal(11, profile.Offset);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void LoadAll_InvalidJson_ReturnsEmpty()
    {
        var path = CreateTempFilePath();
        try
        {
            File.WriteAllText(path, "{ this is not valid json ");
            var store = new GamepadProfileStore(path);

            var loaded = store.LoadAll();

            Assert.Empty(loaded);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void Upsert_PersistsAndReloads()
    {
        var path = CreateTempFilePath();
        var quarantinePath = Path.Combine(Path.GetDirectoryName(path)!, "gamepad-profiles-quarantine.json");
        try
        {
            var expected = new GamepadBatteryProfile(
                "2DC8",
                "6100",
                0x21,
                64,
                14,
                GamepadProbeCandidateEvaluator.DecoderNibble10,
                77,
                BatteryConfidence.Estimated);
            var store = new GamepadProfileStore(path, quarantinePath);
            store.Upsert(expected);

            var reloaded = new GamepadProfileStore(path, quarantinePath);
            var found = reloaded.TryGetBestForModel("2DC8", "6100", out var loaded);

            Assert.True(found);
            Assert.Equal(expected.ReportId, loaded.ReportId);
            Assert.Equal(expected.ReportLength, loaded.ReportLength);
            Assert.Equal(expected.Offset, loaded.Offset);
            Assert.Equal(expected.Decoder, loaded.Decoder);
            Assert.Equal(expected.Score, loaded.Score);
            Assert.Equal(expected.Confidence, loaded.Confidence);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void TryGetBestForModel_QuarantinesXboxGenericProfile()
    {
        var path = CreateTempFilePath();
        var quarantinePath = Path.Combine(Path.GetDirectoryName(path)!, "gamepad-profiles-quarantine.json");
        try
        {
            var store = new GamepadProfileStore(path, quarantinePath);
            store.Upsert(new GamepadBatteryProfile(
                "045E",
                "02E0",
                0x01,
                16,
                13,
                GamepadProbeCandidateEvaluator.DecoderPercent100,
                90,
                BatteryConfidence.Confirmed));

            var found = store.TryGetBestForModel("045E", "02E0", out _);

            Assert.False(found);
            Assert.Empty(store.LoadAll());
            var quarantined = store.LoadQuarantined();
            Assert.Single(quarantined);
            Assert.Equal(GamepadProfileState.Quarantined, quarantined[0].State);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void Upsert_ImmediatelyQuarantinesXboxGenericProfile_WhenIdentityMissing()
    {
        var path = CreateTempFilePath();
        var quarantinePath = Path.Combine(Path.GetDirectoryName(path)!, "gamepad-profiles-quarantine.json");
        try
        {
            var store = new GamepadProfileStore(path, quarantinePath);
            store.Upsert(new GamepadBatteryProfile(
                "045E",
                "02E0",
                0x01,
                16,
                13,
                GamepadProbeCandidateEvaluator.DecoderPercent100,
                78,
                BatteryConfidence.Estimated));

            Assert.Empty(store.LoadAll());
            var quarantined = store.LoadQuarantined();
            Assert.Single(quarantined);
            Assert.Equal(GamepadProfileState.Quarantined, quarantined[0].State);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void TryGetBestForIdentity_DoesNotQuarantineXboxGenericProfile_WhenIdentityIsPresent()
    {
        var path = CreateTempFilePath();
        var quarantinePath = Path.Combine(Path.GetDirectoryName(path)!, "gamepad-profiles-quarantine.json");
        try
        {
            var store = new GamepadProfileStore(path, quarantinePath);
            store.Upsert(new GamepadBatteryProfile(
                "045E",
                "02E0",
                0x01,
                16,
                10,
                GamepadProbeCandidateEvaluator.DecoderPercent100,
                82,
                BatteryConfidence.Estimated,
                IdentityKey: "ID=VID_045E|PID_02E0|FP_TEST"));

            var found = store.TryGetBestForIdentity(
                "ID=VID_045E|PID_02E0|FP_TEST",
                "045E",
                "02E0",
                out var profile);

            Assert.True(found);
            Assert.Equal(GamepadProfileState.Active, profile.State);
            Assert.Empty(store.LoadQuarantined());
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void TryGetBestForIdentity_PrefersExactIdentityBeforeModelFallback()
    {
        var path = CreateTempFilePath();
        var quarantinePath = Path.Combine(Path.GetDirectoryName(path)!, "gamepad-profiles-quarantine.json");
        try
        {
            var store = new GamepadProfileStore(path, quarantinePath);
            store.Upsert(new GamepadBatteryProfile(
                "2DC8",
                "6100",
                0x31,
                64,
                13,
                GamepadProbeCandidateEvaluator.DecoderPercent100,
                78,
                BatteryConfidence.Estimated,
                IdentityKey: "ID=VID_2DC8|PID_6100|FP_AAAA"));
            store.Upsert(new GamepadBatteryProfile(
                "2DC8",
                "6100",
                0x31,
                64,
                11,
                GamepadProbeCandidateEvaluator.DecoderPercent100,
                88,
                BatteryConfidence.Confirmed,
                IdentityKey: "ID=VID_2DC8|PID_6100|FP_BBBB"));

            var found = store.TryGetBestForIdentity(
                "ID=VID_2DC8|PID_6100|FP_AAAA",
                "2DC8",
                "6100",
                out var profile);

            Assert.True(found);
            Assert.Equal(78, profile.Score);
            Assert.Contains("FP_AAAA", profile.IdentityKey, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    private static string CreateTempFilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bloss-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "gamepad-profiles.json");
    }

    private static void SafeDelete(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Test cleanup best effort.
        }
    }
}
