using System.Diagnostics;

namespace BluetoothBatteryWidget.Tests;

public sealed class ThirdPartyDiagnosticsScriptTests
{
    [Fact]
    public void DiagnosticsScript_AddsSanitizedBatteryCandidateAnalysis()
    {
        var script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "scripts",
            "show-third-party-gamepad-battery-diagnostics.ps1"));

        Assert.Contains("function Show-BatteryCandidateAnalysis", script);
        Assert.Contains("function Show-BatteryCandidateSummary", script);
        Assert.Contains("function Show-BatteryCandidateTimeline", script);
        Assert.Contains("function Show-BatteryCandidateClusters", script);
        Assert.Contains("function Show-BatteryCandidateProofReadiness", script);
        Assert.Contains("function Show-BatterySignalContrast", script);
        Assert.Contains("function Show-BatteryCandidateDecisionMatrix", script);
        Assert.Contains("function Show-BatteryNextEvidencePlan", script);
        Assert.Contains("function Show-X05ProModeTracePlan", script);
        Assert.Contains("function Show-X05ProBatteryFeasibilityLadder", script);
        Assert.Contains("function Resolve-CandidateWatchPriority", script);
        Assert.Contains("function Get-BatteryReportWatchRank", script);
        Assert.Contains("function Get-BatteryCandidateClusterSummaries", script);
        Assert.Contains("function Show-OpenSourceBatteryComparison", script);
        Assert.Contains("function Get-ParsedBatteryCandidateGroups", script);
        Assert.Contains("function Get-CandidateValidationState", script);
        Assert.Contains("function ConvertTo-NullableInt", script);
        Assert.Contains("function Resolve-ProfileRuntimeGuard", script);
        Assert.Contains("function Show-HealthFile", script);
        Assert.Contains("function Get-PendingVoteIndex", script);
        Assert.Contains("function Get-CandidateLines", script);
        Assert.Contains("function Format-DeviceName", script);
        Assert.Contains("Open-source battery comparison", script);
        Assert.Contains("X05 Pro battery feasibility ladder", script);
        Assert.Contains("Battery candidate clusters", script);
        Assert.Contains("Battery candidate proof readiness", script);
        Assert.Contains("Battery signal contrast", script);
        Assert.Contains("Battery candidate decision matrix", script);
        Assert.Contains("Battery next evidence plan", script);
        Assert.Contains("X05 Pro mode trace plan", script);
        Assert.Contains("Bluetooth-BAS", script);
        Assert.Contains("Microsoft-Bluetooth-battery-guidelines", script);
        Assert.Contains("SDL-XInput", script);
        Assert.Contains("SDL-HIDAPI-XboxOne", script);
        Assert.Contains("DeviceReport-X05Pro-manual", script);
        Assert.Contains("EasySMX-manual", script);
        Assert.Contains("Gamepadla-X05Pro", script);
        Assert.Contains("community-mode-report", script);
        Assert.Contains("standard Battery Level is 0-100 percent only if the device exposes", script);
        Assert.Contains("host UI cannot invent missing data", script);
        Assert.Contains("bluetooth packet 0x04 data[1]", script);
        Assert.Contains("xpadneo", script);
        Assert.Contains("low/normal/high/full", script);
        Assert.Contains("charging LEDs, low-battery LED warning, modes, and Xbox Wireless Controller Bluetooth naming", script);
        Assert.Contains("no HID 1% battery report layout", script);
        Assert.Contains("name alone is mode evidence", script);
        Assert.Contains("modeEvidence=public sources show X05 Pro has multiple PC modes", script);
        Assert.Contains("modePlan=xinput-name", script);
        Assert.Contains("modePlan=easysmx-name", script);
        Assert.Contains("doNotUse=name-only,gameinput-100,xbox_bt_flags", script);
        Assert.Contains("feasibility=hardware low-battery LED proves at least threshold awareness", script);
        Assert.Contains("feasibility=chipset exact 1% needs battery measurement data plus firmware filtering", script);
        Assert.Contains("feasibility=firmware if firmware only drives LEDs or Xbox bucket flags", script);
        Assert.Contains("feasibility=protocol BLE BAS or a vendor HID report must expose 0-100", script);
        Assert.Contains("x05ProLikely=protocol-or-firmware-limit", script);
        Assert.Contains("proofToUpgrade=same report+offset+decoder repeat>=3 movement>=2", script);
        Assert.Contains("proofToStop=after charge/drain traces, only xbox_bt_flags moves and exact candidates stay flat", script);
        Assert.Contains("coarse-bucket-not-1pct", script);
        Assert.Contains("exact-candidate-watch-repeat-and-change", script);
        Assert.Contains("multi-report-flat-needs-change", script);
        Assert.Contains("multi-report-ready-after-change", script);
        Assert.Contains("reports=", script);
        Assert.Contains("reportCount=", script);
        Assert.Contains("bestReportExact=", script);
        Assert.Contains("votes=", script);
        Assert.Contains("movementDetail=", script);
        Assert.Contains("clusterHint=", script);
        Assert.Contains("crossReportRepeat=", script);
        Assert.Contains("use=watch-only", script);
        Assert.Contains("candidate-ready-for-learning", script);
        Assert.Contains("display=keep-na", script);
        Assert.Contains("ready-after-runtime-revalidation", script);
        Assert.Contains("decision=keep-display-na-until-exact-repeat-and-change", script);
        Assert.Contains("ready-after-repeat-and-change", script);
        Assert.Contains("(trace)", script);
        Assert.Contains("(votes)", script);
        Assert.Contains("candidateObservations", script);
        Assert.Contains("candidateDecisionHints", script);
        Assert.Contains("decisionHints=", script);
        Assert.Contains("selectedCandidateProof", script);
        Assert.Contains("selectedProof=", script);
        Assert.Contains("percentRange=", script);
        Assert.Contains("first=", script);
        Assert.Contains("last=", script);
        Assert.Contains("movement=", script);
        Assert.Contains("needs-percent-change", script);
        Assert.Contains("noSignal=", script);
        Assert.Contains("coarse-bucket-only", script);
        Assert.Contains("coarse-moved-exact-flat", script);
        Assert.Contains("keep-na-and-watch-exact", script);
        Assert.Contains("exact-moved-needs-repeat", script);
        Assert.Contains("matrixResult=no-real-1pct-yet", script);
        Assert.Contains("xbox-bucket-open-source", script);
        Assert.Contains("status-flat-against-coarse", script);
        Assert.Contains("repeat-and-movement-ok", script);
        Assert.Contains("demote-status-flat-against-coarse", script);
        Assert.Contains("watch-dedicated-needs-repeat-change", script);
        Assert.Contains("same-report-offset repeat>=3 movement>=2", script);
        Assert.Contains("exact-candidate-needs-repeat", script);
        Assert.Contains("will-quarantine-needs-repeat", script);
        Assert.Contains("runtime-will-hold-on-xbox-name", script);
        Assert.Contains("runtime-na-coarse-bucket", script);
        Assert.Contains("runtime=", script);
        Assert.Contains("VID_MASKED", script);
        Assert.Contains("PID_MASKED", script);
        Assert.Contains("FP_MASKED", script);
        Assert.Contains("SkipConnectedDevices", script);
        Assert.Contains("function Format-ProfileId", script);
        Assert.Contains("return \"masked\"", script);
        Assert.Contains("Format-DeviceName", script);
        Assert.Contains("ConvertTo-SafeText $decoder", script);
        Assert.DoesNotContain("device" + "Path=", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticsScript_KeepsCrossReportRepeatAsWatchOnly()
    {
        var output = RunDiagnosticsWithSyntheticData(
            probeTraceLines:
            [
                """{"ts":"2026-05-28T10:00:00+09:00","candidateObservations":["xbox_bt_flags@0x04[off=1,pct=10,score=80,profile=x05]","percent100@0x04[off=13,pct=9,score=89,profile=x05]"],"candidateDecisionHints":["reject-exact:xbox_bt_flags@0x04[off=1,pct=10,score=80,seen=2,need=none,reason=xbox-bucket-open-source,profile=x05]"],"selectedCandidateProof":"selected=percent100@0x04[off=13,pct=9,score=89,votes=2/3,repeat=no,movement=no:9-9,gate=needs-repeat-and-movement]"}""",
                """{"ts":"2026-05-28T10:01:00+09:00","candidateObservations":["xbox_bt_flags@0x04[off=1,pct=100,score=80,profile=x05]","percent100@0x04[off=13,pct=9,score=89,profile=x05]"]}""",
                """{"ts":"2026-05-28T10:02:00+09:00","candidateObservations":["percent100@0x01[off=13,pct=9,score=89,profile=x05]"]}""",
                """{"ts":"2026-05-28T10:03:00+09:00","candidateObservations":["percent100@0x11[off=13,pct=9,score=89,profile=x05]"]}"""
            ],
            votesJson: """
            {
              "votes": {
                "SAFE|RID_04|OFF_13|DEC_PERCENT100": {
                  "candidateKey": "IDK_SAFE|RID_04|OFF_13|DEC_PERCENT100",
                  "score": 89,
                  "voteCount": 2,
                  "firstSeenAt": "2026-05-28T10:00:00+09:00",
                  "lastSeenAt": "2026-05-28T10:01:00+09:00",
                  "evidenceType": "generic",
                  "lastValidationStats": "decoder=percent100;score=89",
                  "minPercent": 9,
                  "maxPercent": 9
                }
              },
              "cooldowns": {}
            }
            """);

        Assert.Contains("bestReportExact=decoder=percent100 report=0x04 offset=13 seen=2 votes=2/3 percentRange=9-9", output);
        Assert.Contains("repeat=no movement=no", output);
        Assert.Contains("decision=wait-for-repeat-and-percent-change", output);
        Assert.Contains("clusterHint=decoder=percent100 offset=13 reports=0x01,0x04,0x11 reportCount=3 seen=4 percentRange=9-9 crossReportRepeat=yes movement=no use=watch-only", output);
        Assert.Contains("coarse=decoder=xbox_bt_flags report=0x04 offset=1 seen=2 percentRange=10-100 movement=yes trust=coarse-bucket-only", output);
        Assert.Contains("exact=decoder=percent100 report=0x04 offset=13 seen=2 votes=2/3 percentRange=9-9 movement=no", output);
        Assert.Contains("contrast=coarse-moved-exact-flat decision=keep-na-and-watch-exact", output);
        Assert.Contains("decisionHints=reject-exact:xbox_bt_flags@0x04[off=1,pct=10,score=80,seen=2,need=none,reason=xbox-bucket-open-source,profile=x05]", output);
        Assert.Contains("selectedProof=selected=percent100@0x04[off=13,pct=9,score=89,votes=2/3,repeat=no,movement=no:9-9,gate=needs-repeat-and-movement]", output);
        Assert.Contains("decision=reject-exact decoder=xbox_bt_flags report=0x04 offset=1 seen=2 votes=n/a percentRange=10-100 trust=coarse-bucket-only reason=xbox-bucket-open-source", output);
        Assert.Contains("decision=demote decoder=percent100 report=0x04 offset=13 seen=2 votes=2/3 percentRange=9-9 trust=exact-candidate-needs-repeat reason=status-flat-against-coarse", output);
        Assert.Contains("decision=watch decoder=percent100 report=0x11 offset=13 seen=1 votes=0/3 percentRange=9-9 trust=exact-candidate-needs-repeat reason=needs-repeat-and-change", output);
        Assert.Contains("matrixResult=no-real-1pct-yet display=keep-na", output);
        Assert.Contains("watch=decoder=percent100 report=0x01 offset=13 seen=1 votes=0/3 percentRange=9-9 maxScore=89 priority=watch-dedicated-needs-repeat-change", output);
        Assert.Contains("watch=decoder=percent100 report=0x11 offset=13 seen=1 votes=0/3 percentRange=9-9 maxScore=89 priority=watch-dedicated-needs-repeat-change", output);
        Assert.Contains("demote=decoder=percent100 report=0x04 offset=13 seen=2 votes=2/3 percentRange=9-9 maxScore=89 priority=demote-status-flat-against-coarse", output);
        Assert.Contains("required=same-report-offset repeat>=3 movement>=2 coarseMoved=yes display=keep-na-until-ready", output);
        Assert.Contains("display=keep-na", output);
        Assert.DoesNotContain("display=ready-after-runtime-revalidation", output);
        Assert.DoesNotContain("BTHENUM" + "\\", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("device" + "Path=", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticsScript_MarksReportCandidateReadyOnlyAfterRepeatAndMovement()
    {
        var output = RunDiagnosticsWithSyntheticData(
            probeTraceLines:
            [
                """{"ts":"2026-05-28T10:00:00+09:00","candidateObservations":["percent100@0x11[off=13,pct=9,score=89,profile=x05]"]}""",
                """{"ts":"2026-05-28T10:01:00+09:00","candidateObservations":["percent100@0x11[off=13,pct=10,score=89,profile=x05]"]}""",
                """{"ts":"2026-05-28T10:02:00+09:00","candidateObservations":["percent100@0x11[off=13,pct=12,score=89,profile=x05]"]}"""
            ],
            votesJson: """
            {
              "votes": {
                "SAFE|RID_11|OFF_13|DEC_PERCENT100": {
                  "candidateKey": "IDK_SAFE|RID_11|OFF_13|DEC_PERCENT100",
                  "score": 89,
                  "voteCount": 3,
                  "firstSeenAt": "2026-05-28T10:00:00+09:00",
                  "lastSeenAt": "2026-05-28T10:02:00+09:00",
                  "evidenceType": "dedicated",
                  "lastValidationStats": "decoder=percent100;score=89",
                  "minPercent": 9,
                  "maxPercent": 12
                }
              },
              "cooldowns": {}
            }
            """);

        Assert.Contains("bestReportExact=decoder=percent100 report=0x11 offset=13 seen=3 votes=3/3 percentRange=9-12", output);
        Assert.Contains("repeat=yes movement=yes movementDetail=moved:9-12(votes) decision=candidate-ready-for-learning", output);
        Assert.Contains("decision=ready decoder=percent100 report=0x11 offset=13 seen=3 votes=3/3 percentRange=9-12 trust=ready-after-repeat-and-change reason=repeat-and-movement-ok", output);
        Assert.Contains("matrixResult=ready-after-runtime-revalidation", output);
        Assert.Contains("display=ready-after-runtime-revalidation", output);
    }

    [Fact]
    public void DiagnosticsScript_MasksRuntimeNamesAndProfileKeysByDefault()
    {
        var output = RunDiagnosticsWithSyntheticData(
            probeTraceLines: [],
            votesJson: """{"votes":{},"cooldowns":{}}""",
            runtimeTraceLines:
            [
                """{"ts":"2026-05-28T10:00:00+09:00","buildStamp":"test","raw":[{"sourceKind":"GameInput","percent":100,"rawMetric":100,"confidence":"Estimated","displayState":"Estimated","isBatterySuspect":true,"reasonCode":"gameinput_scaled_full_suspect","displayName":"Secret Controller","modelKey":"TR=VID_1234|PID_ABCD|FP_ABCD|EP_EEEE","address":"12:34:56:78:9A:BC"}],"resolved":[]}"""
            ],
            observationLines:
            [
                """{"observedAt":"2026-05-28T10:00:01+09:00","sourceKind":2,"derivedPercent":100,"rawMetric":100,"isCharging":false,"reasonCode":"gameinput_scaled_full_suspect","modelKey":"TR=VID_1234|PID_ABCD|FP_ABCD|EP_EEEE","address":"12:34:56:78:9A:BC"}"""
            ],
            profilesJson: """
            {
              "TR=VID_1234|PID_ABCD|FP_ABCD|EP_EEEE": {
                "vendorId": "1234",
                "productId": "ABCD",
                "reportId": 17,
                "offset": 13,
                "decoder": "percent100",
                "score": 89,
                "confidence": "High",
                "state": "Active",
                "validationKind": "generic",
                "validationCount": 0,
                "identityKey": "FP_ABCD"
              }
            }
            """,
            healthJson: """
            {
              "TR=VID_1234|PID_ABCD|FP_ABCD|EP_EEEE": {
                "noSignalStrike": 0,
                "weakSignalStrike": 0,
                "mismatchStrike": 0,
                "consecutiveSuccessCount": 0,
                "lastHealthyAt": null
              }
            }
            """);

        Assert.Contains("name=masked", output);
        Assert.Contains("model=masked", output);
        Assert.Contains("addr=masked", output);
        Assert.Contains("masked | vid=masked pid=masked", output);
        Assert.DoesNotContain("Secret Controller", output);
        Assert.DoesNotContain("VID_1234", output);
        Assert.DoesNotContain("PID_ABCD", output);
        Assert.DoesNotContain("FP_ABCD", output);
        Assert.DoesNotContain("EP_EEEE", output);
        Assert.DoesNotContain("12:34:56:78:9A:BC", output);
    }

    private static string RunDiagnosticsWithSyntheticData(
        string[] probeTraceLines,
        string votesJson,
        string[]? runtimeTraceLines = null,
        string[]? observationLines = null,
        string? profilesJson = null,
        string? healthJson = null)
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "scripts",
            "show-third-party-gamepad-battery-diagnostics.ps1"));
        var tempRoot = Path.Combine(AppContext.BaseDirectory, "diagnostics-script-test-data", Guid.NewGuid().ToString("N"));
        var blossRoot = Path.Combine(tempRoot, "Bloss");
        Directory.CreateDirectory(blossRoot);

        try
        {
            File.WriteAllLines(Path.Combine(blossRoot, "probe-traces.jsonl"), probeTraceLines);
            File.WriteAllText(Path.Combine(blossRoot, "gamepad-candidate-votes.json"), votesJson);
            if (runtimeTraceLines is not null)
            {
                File.WriteAllLines(Path.Combine(blossRoot, "third-party-gamepad-battery-traces.jsonl"), runtimeTraceLines);
            }

            if (observationLines is not null)
            {
                File.WriteAllLines(Path.Combine(blossRoot, "battery-observations.jsonl"), observationLines);
            }

            if (profilesJson is not null)
            {
                File.WriteAllText(Path.Combine(blossRoot, "gamepad-profiles.json"), profilesJson);
                File.WriteAllText(Path.Combine(blossRoot, "gamepad-profiles-quarantine.json"), profilesJson);
            }

            if (healthJson is not null)
            {
                File.WriteAllText(Path.Combine(blossRoot, "gamepad-profile-health.json"), healthJson);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-Tail");
            startInfo.ArgumentList.Add("20");
            startInfo.ArgumentList.Add("-SkipConnectedDevices");
            startInfo.Environment["APPDATA"] = tempRoot;

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("PowerShell did not start.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);

            Assert.True(process.ExitCode == 0, $"ExitCode={process.ExitCode}{Environment.NewLine}{output}{Environment.NewLine}{error}");
            return output;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
