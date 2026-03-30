using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class PendingGamepadCandidateStoreTests
{
    [Fact]
    public void RegisterVote_IncrementsCountForSameModelCandidate()
    {
        var path = CreateTempPath();
        try
        {
            var store = new PendingGamepadCandidateStore(path);
            var now = new DateTimeOffset(2026, 3, 27, 1, 0, 0, TimeSpan.FromHours(9));
            var count1 = store.RegisterVote("VID_045E|PID_0B22", "RID_31|OFF_9", 61, now, evidenceType: "generic", lastValidationStats: "score=61");
            var count2 = store.RegisterVote("VID_045E|PID_0B22", "RID_31|OFF_9", 63, now.AddMinutes(1), evidenceType: "dedicated", lastValidationStats: "score=63");

            Assert.Equal(1, count1);
            Assert.Equal(2, count2);
            var json = File.ReadAllText(path);
            Assert.Contains("\"evidenceType\": \"dedicated\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"lastValidationStats\": \"score=63\"", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void Cooldown_CanBeSetAndExpires()
    {
        var path = CreateTempPath();
        try
        {
            var store = new PendingGamepadCandidateStore(path);
            var now = new DateTimeOffset(2026, 3, 27, 1, 0, 0, TimeSpan.FromHours(9));

            store.SetCooldown("VID_045E|PID_0B22", TimeSpan.FromMinutes(2), now);

            Assert.True(store.IsInCooldown("VID_045E|PID_0B22", now.AddSeconds(30)));
            Assert.False(store.IsInCooldown("VID_045E|PID_0B22", now.AddMinutes(3)));
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void RegisterVote_MigratesLegacyLengthSplitVotes_AndMergesCounts()
    {
        var path = CreateTempPath();
        try
        {
            File.WriteAllText(path, """
            {
              "votes": {
                "VID_045E|PID_02E0|VID_045E|PID_02E0|RID_04|LEN_64|OFF_1|DEC_XBOX_BT_FLAGS": {
                  "modelKey": "VID_045E|PID_02E0",
                  "candidateKey": "VID_045E|PID_02E0|RID_04|LEN_64|OFF_1|DEC_XBOX_BT_FLAGS",
                  "score": 64,
                  "voteCount": 1,
                  "firstSeenAt": "2026-03-28T15:52:30+09:00",
                  "lastSeenAt": "2026-03-28T15:52:30+09:00",
                  "evidenceType": "dedicated",
                  "lastValidationStats": "decoder=xbox_bt_flags;score=64"
                },
                "VID_045E|PID_02E0|VID_045E|PID_02E0|RID_04|LEN_256|OFF_1|DEC_XBOX_BT_FLAGS": {
                  "modelKey": "VID_045E|PID_02E0",
                  "candidateKey": "VID_045E|PID_02E0|RID_04|LEN_256|OFF_1|DEC_XBOX_BT_FLAGS",
                  "score": 64,
                  "voteCount": 1,
                  "firstSeenAt": "2026-03-28T16:12:30+09:00",
                  "lastSeenAt": "2026-03-28T16:12:30+09:00",
                  "evidenceType": "dedicated",
                  "lastValidationStats": "decoder=xbox_bt_flags;score=64"
                }
              },
              "cooldowns": {}
            }
            """);

            var store = new PendingGamepadCandidateStore(path);
            var now = new DateTimeOffset(2026, 3, 28, 17, 0, 0, TimeSpan.FromHours(9));
            var mergedCount = store.RegisterVote(
                "VID_045E|PID_02E0",
                "IDK_MODEL_VID_045E|PID_02E0|RID_04|OFF_1|DEC_XBOX_BT_FLAGS",
                64,
                now,
                evidenceType: "dedicated");

            Assert.Equal(3, mergedCount);

            var json = File.ReadAllText(path);
            Assert.DoesNotContain("LEN_64", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("LEN_256", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void RegisterVote_MigratesModelScopeToIdentityScope_WhenIdentityExists()
    {
        var path = CreateTempPath();
        try
        {
            File.WriteAllText(path, """
            {
              "votes": {
                "VID_045E|PID_0B13|IDK_ID=VID_045E|TR=VID_045E|FP=FP_TEST|RID_04|OFF_1|DEC_XBOX_BT_FLAGS": {
                  "modelKey": "VID_045E|PID_0B13",
                  "candidateKey": "IDK_ID=VID_045E|TR=VID_045E|FP=FP_TEST|RID_04|OFF_1|DEC_XBOX_BT_FLAGS",
                  "score": 66,
                  "voteCount": 2,
                  "firstSeenAt": "2026-03-28T15:52:30+09:00",
                  "lastSeenAt": "2026-03-28T16:12:30+09:00",
                  "evidenceType": "generic",
                  "lastValidationStats": "decoder=xbox_bt_flags;score=66"
                }
              },
              "cooldowns": {}
            }
            """);

            var store = new PendingGamepadCandidateStore(path);
            var now = new DateTimeOffset(2026, 3, 28, 17, 30, 0, TimeSpan.FromHours(9));
            var mergedCount = store.RegisterVote(
                "VID_045E|PID_0B13",
                "IDK_ID=VID_045E|TR=VID_045E|FP=FP_TEST|RID_04|OFF_1|DEC_XBOX_BT_FLAGS",
                69,
                now,
                evidenceType: "dedicated");

            Assert.Equal(3, mergedCount);
            var json = File.ReadAllText(path);
            Assert.Contains("\"modelKey\": \"ID=VID_045E|TR=VID_045E|FP=FP_TEST\"", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    private static string CreateTempPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bloss-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "votes.json");
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
            // ignore cleanup errors
        }
    }
}
