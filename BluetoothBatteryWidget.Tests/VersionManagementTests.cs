using System.Xml.Linq;
using System.Text.Json;

namespace BluetoothBatteryWidget.Tests;

public sealed class VersionManagementTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        ".."));

    private static string? ReadOptionalProjectNote(string fileName)
    {
        var path = Path.Combine(ProjectRoot, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    [Fact]
    public void DirectoryBuildProps_CentralizesReleaseVersion()
    {
        var propsPath = Path.Combine(ProjectRoot, "Directory.Build.props");
        var document = XDocument.Load(propsPath);
        var propertyGroup = document.Root?.Element("PropertyGroup");

        Assert.NotNull(propertyGroup);
        Assert.Equal("1.0.8", propertyGroup!.Element("Version")?.Value);
        Assert.Equal("1.0.8", propertyGroup.Element("AssemblyVersion")?.Value);
        Assert.Equal("1.0.8", propertyGroup.Element("FileVersion")?.Value);
        Assert.Equal("1.0.8", propertyGroup.Element("InformationalVersion")?.Value);
        Assert.Equal("false", propertyGroup.Element("IncludeSourceRevisionInInformationalVersion")?.Value);
    }

    [Fact]
    public void AppProject_DoesNotOverrideCentralVersion()
    {
        var projectPath = Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "BluetoothBatteryWidget.App.csproj");
        var source = File.ReadAllText(projectPath);

        Assert.DoesNotContain("<Version>", source);
        Assert.DoesNotContain("<AssemblyVersion>", source);
        Assert.DoesNotContain("<FileVersion>", source);
        Assert.DoesNotContain("<InformationalVersion>", source);
    }

    [Fact]
    public void RuntimeFallbackVersion_MatchesCentralReleaseVersion()
    {
        var source = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "Services",
            "AppVersionInfo.cs"));
        var mainWindowSource = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "MainWindow.xaml.cs"));
        var releaseNotesSource = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "ReleaseNotesWindow.xaml.cs"));

        Assert.Contains("internal const string FallbackVersion = \"1.0.8\";", source);
        Assert.Contains("AssemblyInformationalVersionAttribute", source);
        Assert.Contains("AppVersionInfo.DisplayVersion", mainWindowSource);
        Assert.Contains("AppVersionInfo.DisplayVersion", releaseNotesSource);
        Assert.DoesNotContain("private const string FallbackVersion", mainWindowSource);
        Assert.DoesNotContain("? \"1.0.8\"", releaseNotesSource);
    }

    [Fact]
    public void DiagnosticBuildStamps_PreferInformationalThreePartVersion()
    {
        var providerSource = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "Services",
            "CompositeBatteryLevelProvider.cs"));
        var probeSource = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "BluetoothBatteryWidget.App",
            "Services",
            "GamepadProbeService.cs"));

        Assert.Contains("AssemblyInformationalVersionAttribute", providerSource);
        Assert.Contains("AssemblyInformationalVersionAttribute", probeSource);
        Assert.Contains("assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}", providerSource);
        Assert.Contains("assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}", probeSource);
        Assert.DoesNotContain("GetName().Version?.ToString()", providerSource);
        Assert.DoesNotContain("GetName().Version?.ToString()", probeSource);
    }

    [Fact]
    public void InstallerBuildDefaults_ReadCentralVersion()
    {
        var script = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "scripts",
            "build-installer.ps1"));
        var installer = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "installer",
            "BluetoothBatteryWidget.iss"));

        Assert.Contains("Directory.Build.props", script);
        Assert.Contains("Get-CentralAppVersion", script);
        Assert.Contains("[string]$AppVersion", script);
        Assert.DoesNotContain("[string]$AppVersion = \"1.0.1\"", script);
        Assert.Contains("setup.exe.sha256", script);
        Assert.Contains("#define AppVersion \"1.0.8\"", installer);
        Assert.Contains("OutputBaseFilename=setup", installer);
        Assert.Contains("CloseApplications=no", installer);
        Assert.Contains("RestartApplications=no", installer);
        Assert.Contains("CloseApplicationsFilterExcludes=*.exe,*.dll,*.chm", installer);
        Assert.DoesNotContain("DisablePrecompiledFileVerifications", installer);
    }

    [Fact]
    public void InstallerUninstall_RemovesAutostartValuesFromCurrentAndLoadedUsers()
    {
        var installer = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "installer",
            "BluetoothBatteryWidget.iss"));

        const string runKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

        Assert.Contains("procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);", installer);
        Assert.Contains("CurUninstallStep = usUninstall", installer);
        Assert.Contains("RegDeleteValue", installer);
        Assert.Contains("RegGetSubkeyNames(HKEY_USERS", installer);
        Assert.Contains("HKEY_CURRENT_USER", installer);
        Assert.Contains("HKEY_USERS", installer);
        Assert.Contains(runKey, installer);
        Assert.Contains("'Bloss'", installer);
        Assert.Contains("'BluetoothBatteryWidget'", installer);
        Assert.Contains("Pos('S-1-5-21-', HiveName) = 1", installer);
        Assert.DoesNotContain("Pos('S-1-5-', HiveName) = 1", installer);
        Assert.False(installer.Contains("uninsdeletekey", StringComparison.OrdinalIgnoreCase));
        Assert.False(installer.Contains("deletekey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReleaseGuide_UsesCurrentStagingFolderPaths()
    {
        var guide = ReadOptionalProjectNote("howtorelease.md");
        if (guide is null)
        {
            return;
        }

        Assert.Contains("bloss_battery_indicator_test_ver107", guide);
        Assert.Contains("modification and verification staging folder", guide);
        Assert.Contains("scripts\\build-installer.ps1", guide);
        Assert.Contains("scripts\\build-test-portable.ps1", guide);
        Assert.Contains("scripts\\verify-installer.ps1", guide);
        Assert.Contains("scripts\\verify-test-portable.ps1", guide);
        Assert.Contains("scripts\\verify-release-notes-popup.ps1", guide);
        Assert.Contains("scripts\\verify-secondary-window-animation.ps1", guide);
        Assert.Contains("manual-verification-v108.md", guide);
        Assert.Contains("scripts\\verify-v108-manual-checklist.ps1", guide);
        Assert.Contains("scripts\\show-v108-manual-gate-commands.ps1", guide);
        Assert.Contains("scripts\\verify-v108-manual-gate-updater.ps1", guide);
        Assert.Contains("scripts\\verify-v108-release-ready.ps1", guide);
        Assert.Contains("scripts\\check-autostart-cleanup.ps1", guide);
        Assert.Contains("build-v108-old-installer-prereq.ps1\" -Version 1.0.4", guide);
        Assert.Contains("build-v108-old-installer-prereq.ps1\" -Version 1.0.5", guide);
        Assert.Contains("build-v108-old-installer-prereq.ps1\" -Version 1.0.6", guide);
        Assert.Contains("recreate every missing version", guide);
        Assert.Contains("portable test executable must stay named `test.exe`", guide);
        Assert.Contains("This is a read-only check.", guide);
        Assert.Contains("Install `v1.0.6`, update from inside the app", guide);
        Assert.Contains("release notes popup appears once", guide);
        Assert.Contains("release notes popup appears every run", guide);
        Assert.Contains("target-version update list", guide);
        Assert.Contains("$targetTag = \"v1.0.8\"", guide);
        Assert.Contains("Auto (Windows)", guide);
        Assert.Contains("old one-minute saved settings", guide);
        Assert.Contains("display-off timeout to 1 minute", guide);
        Assert.Contains("DualSense/Pico2W", guide);
        Assert.Contains("connected-but-untouched gamepads do not block display-off or sleep", guide);
        Assert.Contains("real gamepad input delays display-off like keyboard/mouse", guide);
        Assert.Contains("guide/PS input", guide);
        Assert.Contains("real Steam Controller", guide);
        Assert.Contains("Quick Access capture window stability", guide);
        Assert.Contains("lower square hotspot", guide);
        Assert.Contains("settings secondary windows", guide);
        Assert.Contains("RequireManualGatePasses", guide);
        Assert.Contains("powershell -ExecutionPolicy Bypass -File \".\\scripts\\verify-v108-release-ready.ps1\" -LiveReleaseNotes -DisplaySleepSnapshot -RequireManualGatePasses -RequireNoRunningBlossOrTest -RequireNoCurrentAutostart", guide);
        Assert.Contains("-RequireNoCurrentAutostart", guide);
        Assert.Contains("Do not run `gh release upload` while this fails", guide);
        Assert.Contains("installer\\BluetoothBatteryWidget.iss", guide);
        Assert.Contains("Uninstall autostart cleanup", guide);
        Assert.Contains("CurUninstallStepChanged", guide);
        Assert.Contains("RegDeleteValue", guide);
        Assert.Contains("HKEY_CURRENT_USER", guide);
        Assert.Contains("HKEY_USERS", guide);
        Assert.Contains("Bloss", guide);
        Assert.Contains("BluetoothBatteryWidget", guide);
        Assert.DoesNotContain("bloss_battery_indicator_release_day1", guide);
        Assert.DoesNotContain("build\\scripts\\build-installer.ps1", guide);
        Assert.DoesNotContain("build\\installer\\BluetoothBatteryWidget.iss", guide);
        Assert.DoesNotContain("For v1.0.7, verify the update notes popup", guide);
        Assert.DoesNotContain("Bloss 1.0.7", guide);
        Assert.DoesNotContain("download/v1.0.7", guide);
    }

    [Fact]
    public void SteadyStatus_UsesCurrentReleaseScriptPaths()
    {
        var steadyStatus = ReadOptionalProjectNote("steadystatus.md");
        if (steadyStatus is null)
        {
            return;
        }

        Assert.Contains("scripts\\build-installer.ps1", steadyStatus);
        Assert.Contains("installer\\BluetoothBatteryWidget.iss", steadyStatus);
        Assert.DoesNotContain("build/scripts/build-installer.ps1", steadyStatus);
        Assert.DoesNotContain("build/installer/BluetoothBatteryWidget.iss", steadyStatus);
        Assert.DoesNotContain("bloss_battery_indicator_release_day1", steadyStatus);
    }

    [Fact]
    public void SteamControllerGuide_UsesCurrentStagingFolderPaths()
    {
        var guide = ReadOptionalProjectNote("steamcontrollerguide.md");
        if (guide is null)
        {
            return;
        }

        Assert.Contains("대상 폴더: `bloss_battery_indicator_test_ver107`", guide);
        Assert.Contains(@"bluetooth widget\bloss_battery_indicator_test_ver107", guide);
        Assert.Contains(@".\scripts\show-guide-button-events.ps1 -SteamPowerOffCheck", guide);
        Assert.Contains("성공하면 실제 릴리즈 폴더로 이식한다.", guide);
        Assert.Contains(@"BluetoothBatteryWidget.Tests\GuideButtonReportParserTests.cs", guide);
        Assert.Contains(@"BluetoothBatteryWidget.Tests\SteamRawHidGuideButtonStateTrackerTests.cs", guide);
        Assert.Contains(@"BluetoothBatteryWidget.Tests\MainWindowXamlBindingTests.cs", guide);
        Assert.DoesNotContain("대상 폴더: `bloss_battery_indicator_test`", guide);
        Assert.DoesNotContain(@"bluetooth widget\bloss_battery_indicator_test""", guide);
        Assert.DoesNotContain(@".\build\scripts\show-guide-button-events.ps1", guide);
        Assert.DoesNotContain("`bloss_battery_indicator_test`에서 고친다", guide);
    }

    [Fact]
    public void ReleaseVerificationScripts_GuardInstallerPortableAndAutostartOutputs()
    {
        var guide = ReadOptionalProjectNote("howtorelease.md");
        var verifyInstaller = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-installer.ps1"));
        var verifyTestPortable = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-test-portable.ps1"));
        var verifyReleaseNotes = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-release-notes-popup.ps1"));
        var verifyV107Ready = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-v107-release-ready.ps1"));
        var verifyManualChecklist = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-v107-manual-checklist.ps1"));
        var showManualGateCommands = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "show-v107-manual-gate-commands.ps1"));
        var buildOldInstallerPrereq = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "build-v107-old-installer-prereq.ps1"));
        var checkManualGatePrereqs = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "check-v107-manual-gate-prereqs.ps1"));
        var exportManualGateEvidence = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "export-v107-manual-gate-evidence.ps1"));
        var verifyGitPublishSafety = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-git-publish-safety.ps1"));
        var showReleaseUploadSummary = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "show-v108-release-upload-summary.ps1"));
        var verifyManualGateUpdater = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-v107-manual-gate-updater.ps1"));
        var setManualGate = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "set-v107-manual-gate.ps1"));
        var manualChecklist = ReadOptionalProjectNote("manual-verification-v108.md");
        var checkAutostart = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "check-autostart-cleanup.ps1"));
        var checkDisplaySleep = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "check-display-sleep-readiness.ps1"));

        Assert.Contains("Directory.Build.props", verifyInstaller);
        Assert.Contains("setup.exe.sha256", verifyInstaller);
        Assert.Contains("Bloss update setup wrapper", verifyInstaller);
        Assert.Contains("BlossSetupInner", verifyInstaller);
        Assert.Contains("BlossPublishPayload", verifyInstaller);
        Assert.Contains("Inno Setup", verifyInstaller);
        Assert.Contains("Get-ReleaseAssetSetFailures", verifyInstaller);
        Assert.Contains("Unexpected release installer asset", verifyInstaller);
        Assert.Contains("setup.exe", verifyInstaller);
        Assert.Contains("setup.exe.sha256", verifyInstaller);
        Assert.Contains("Installer verification passed.", verifyInstaller);

        Assert.Contains("Directory.Build.props", verifyTestPortable);
        Assert.Contains("artifacts\\portable\\test.exe", verifyTestPortable);
        Assert.Contains("ProductVersion", verifyTestPortable);
        Assert.Contains("FileVersion", verifyTestPortable);
        Assert.Contains("Portable test executable must be named test.exe", verifyTestPortable);
        Assert.Contains("Portable test executable verification passed.", verifyTestPortable);

        Assert.Contains("ReleaseNotesWindow.xaml", verifyReleaseNotes);
        Assert.Contains("WindowStartupLocation=`\"CenterScreen`\"", verifyReleaseNotes);
        Assert.Contains("LastSeenReleaseNotesVersion", verifyReleaseNotes);
        Assert.Contains("IsPortableTestExecutablePath(Environment.ProcessPath)", verifyReleaseNotes);
        Assert.Contains("AutomationProperties.SetName", verifyReleaseNotes);
        Assert.Contains("[switch]$Live", verifyReleaseNotes);
        Assert.Contains("UIAutomationClient", verifyReleaseNotes);
        Assert.Contains("catch [System.Runtime.InteropServices.COMException]", verifyReleaseNotes);
        Assert.Contains("Test-WindowCenteredOnAnyScreen", verifyReleaseNotes);
        Assert.Contains("must not use the secondary-window pop-in animation", verifyReleaseNotes);
        Assert.Contains("must not be centered on the widget owner", verifyReleaseNotes);
        Assert.Contains("v1.0.7 update behavior", verifyReleaseNotes);
        Assert.Contains("normalized seen-version behavior", verifyReleaseNotes);
        Assert.Contains("blank release-version suppression", verifyReleaseNotes);
        Assert.Contains("test.exe force-every-run behavior", verifyReleaseNotes);
        Assert.Contains("Release notes popup verification passed.", verifyReleaseNotes);

        Assert.Contains("dotnet test \"BluetoothBatteryWidget.sln\" --configuration Release --no-restore", verifyV107Ready);
        Assert.Contains("verify-test-portable.ps1", verifyV107Ready);
        Assert.Contains("verify-release-notes-popup.ps1", verifyV107Ready);
        Assert.Contains("verify-secondary-window-animation.ps1", verifyV107Ready);
        Assert.Contains("verify-v107-manual-checklist.ps1", verifyV107Ready);
        Assert.Contains("show-v107-manual-gate-commands.ps1", verifyV107Ready);
        Assert.Contains("build-v108-old-installer-prereq.ps1", verifyV107Ready);
        Assert.Contains("check-v107-manual-gate-prereqs.ps1", verifyV107Ready);
        Assert.Contains("export-v107-manual-gate-evidence.ps1", verifyV107Ready);
        Assert.Contains("verify-git-publish-safety.ps1", verifyV107Ready);
        Assert.Contains("verify-v107-manual-gate-updater.ps1", verifyV107Ready);
        Assert.Contains("[switch]$RequireManualGatePasses", verifyV107Ready);
        Assert.Contains("verify-installer.ps1", verifyV107Ready);
        Assert.Contains("build-installer.ps1", verifyV107Ready);
        Assert.Contains("build-test-portable.ps1", verifyV107Ready);
        Assert.Contains("installer\\BluetoothBatteryWidget.iss", verifyV107Ready);
        Assert.Contains("bloss_battery_indicator_release_day1", verifyV107Ready);
        Assert.Contains("Release guide still points at the old release_day1 folder.", verifyV107Ready);
        Assert.Contains("[switch]$LiveReleaseNotes", verifyV107Ready);
        Assert.Contains("[switch]$CheckCurrentAutostart", verifyV107Ready);
        Assert.Contains("[switch]$DisplaySleepSnapshot", verifyV107Ready);
        Assert.Contains("[switch]$RequireNoCurrentAutostart", verifyV107Ready);
        Assert.Contains("check-autostart-cleanup.ps1", verifyV107Ready);
        Assert.Contains("check-display-sleep-readiness.ps1", verifyV107Ready);
        Assert.Contains("Assert-ArtifactFreshEnough", verifyV107Ready);
        Assert.Contains("Power idle defaults and settings migration", verifyV107Ready);
        Assert.Contains("DefaultPowerIdlePauseMinutes = AutoPowerIdlePauseMinutes", verifyV107Ready);
        Assert.Contains("LegacyDefaultPowerIdlePauseMinutes = 1", verifyV107Ready);
        Assert.Contains("WindowsPowerIdleAutoSettingsSchemaVersion", verifyV107Ready);
        Assert.Contains("Load_MigratesLegacyPowerIdleOneMinuteDefaultToWindowsAuto", verifyV107Ready);
        Assert.Contains("Load_PreservesCurrentSchemaPowerIdleOneMinuteUserChoice", verifyV107Ready);
        Assert.Contains("PowerIdleSourceSafetyTests.cs", verifyV107Ready);
        Assert.Contains("Assert.DoesNotContain(\"TryNotifyDisplayUserActivity\"", verifyV107Ready);
        Assert.Contains("ES_CONTINUOUS", verifyV107Ready);
        Assert.Contains("ES_SYSTEM_REQUIRED", verifyV107Ready);
        Assert.Contains("PowerSetRequest", verifyV107Ready);
        Assert.Contains("SendInput(", verifyV107Ready);
        Assert.Contains("display-off timeout to 1 minute", verifyV107Ready);
        Assert.Contains("system sleep", verifyV107Ready);
        Assert.Contains("current power mode", verifyV107Ready);
        Assert.Contains("DualSense/Pico2W", verifyV107Ready);
        Assert.Contains("connected but untouched", verifyV107Ready);
        Assert.Contains("guide/PS input resumes", verifyV107Ready);
        Assert.Contains("real Steam Controller", verifyV107Ready);
        Assert.Contains("Quick Access capture-window stability", verifyV107Ready);
        Assert.Contains("lower-square highlight", verifyV107Ready);
        Assert.Contains("release notes popup appears every run", verifyV107Ready);
        Assert.Contains("Get-ManualGateStatuses", verifyV107Ready);
        Assert.Contains("RequireNoCurrentAutostart", verifyV107Ready);
        Assert.Contains("remainingManualGates", verifyV107Ready);
        Assert.Contains("Git publish safety", verifyV107Ready);
        Assert.Contains("All manual gates are marked PASS.", verifyV107Ready);
        Assert.Contains("v1.0.8 release readiness gate passed", verifyV107Ready);
        Assert.DoesNotContain("-Delete", verifyV107Ready);

        Assert.Contains("git", verifyGitPublishSafety);
        Assert.Contains("ls-files", verifyGitPublishSafety);
        Assert.Contains("grep", verifyGitPublishSafety);
        Assert.Contains("lamsaiku65@gmail.com", verifyGitPublishSafety);
        Assert.Contains("howtorelease\\.md", verifyGitPublishSafety);
        Assert.Contains("gitguide\\.md", verifyGitPublishSafety);
        Assert.Contains("for107\\.md", verifyGitPublishSafety);
        Assert.Contains("manual-verification-v107\\.md", verifyGitPublishSafety);
        Assert.Contains("manual-verification-v108\\.md", verifyGitPublishSafety);
        Assert.Contains("test\\.exe", verifyGitPublishSafety);
        Assert.Contains("setup\\.exe", verifyGitPublishSafety);
        Assert.Contains("artifacts/", verifyGitPublishSafety);
        Assert.Contains("release/", verifyGitPublishSafety);
        Assert.Contains("Unexpected email address found in tracked files", verifyGitPublishSafety);
        Assert.Contains("Git publish safety verification passed.", verifyGitPublishSafety);
        Assert.Contains("[switch]$RequireReady", showReleaseUploadSummary);
        Assert.Contains("[switch]$Json", showReleaseUploadSummary);
        Assert.Contains("manual-verification-v108.md", showReleaseUploadSummary);
        Assert.Contains("verify-git-publish-safety.ps1", showReleaseUploadSummary);
        Assert.Contains("if ($Json)", showReleaseUploadSummary);
        Assert.Contains("*>&1", showReleaseUploadSummary);
        Assert.Contains("setup.exe.sha256", showReleaseUploadSummary);
        Assert.Contains("git ls-files --others --exclude-standard", showReleaseUploadSummary);
        Assert.Contains("Get-UntrackedPublishCandidates", showReleaseUploadSummary);
        Assert.Contains("NoUntrackedPublishCandidates", showReleaseUploadSummary);
        Assert.Contains("Untracked files that must be reviewed before push", showReleaseUploadSummary);
        Assert.Contains("git status --porcelain=v1 --untracked-files=no", showReleaseUploadSummary);
        Assert.Contains("Get-PendingPublishChanges", showReleaseUploadSummary);
        Assert.Contains("NoPendingGitPublishChanges", showReleaseUploadSummary);
        Assert.Contains("PendingGitPublishChanges", showReleaseUploadSummary);
        Assert.Contains("Pending git files that must be committed before upload", showReleaseUploadSummary);
        Assert.Contains("BluetoothBatteryWidget\\.Tests", showReleaseUploadSummary);
        Assert.Contains("ManualGatesAllPassed", showReleaseUploadSummary);
        Assert.Contains("NoRunningBlossOrTest", showReleaseUploadSummary);
        Assert.Contains("NoCurrentUserAutostart", showReleaseUploadSummary);
        Assert.Contains("Release upload is blocked", showReleaseUploadSummary);
        Assert.Contains("v1.0.8 Release Upload Summary", showReleaseUploadSummary);
        Assert.Contains("NonDestructive = $true", showReleaseUploadSummary);
        if (guide is not null)
        {
            Assert.Contains("show-v108-release-upload-summary.ps1", guide);
            Assert.Contains("-RequireReady", guide);
        }

        var verifySecondaryWindowAnimation = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "verify-secondary-window-animation.ps1"));
        Assert.Contains("WindowPopInAnimator.cs", verifySecondaryWindowAnimation);
        Assert.Contains("SettleDuration = TimeSpan.FromMilliseconds(700)", verifySecondaryWindowAnimation);
        Assert.Contains("DoubleAnimationUsingKeyFrames", verifySecondaryWindowAnimation);
        Assert.Contains("BackEase", verifySecondaryWindowAnimation);
        Assert.Contains("ElasticEase", verifySecondaryWindowAnimation);
        Assert.Contains("WindowPopInAnimator.AttachCentered(this)", verifySecondaryWindowAnimation);
        Assert.Contains("WindowPopInAnimator.Begin(", verifySecondaryWindowAnimation);
        Assert.Contains("SecondaryWindows_PopInAnimationSettlesToStableFinalValues", verifySecondaryWindowAnimation);
        Assert.Contains("window.Show();", verifySecondaryWindowAnimation);
        Assert.Contains("WaitForDispatcher(TimeSpan.FromMilliseconds(1000))", verifySecondaryWindowAnimation);
        Assert.Contains("Release notes popup must appear immediately", verifySecondaryWindowAnimation);
        Assert.Contains("Secondary window animation verification passed.", verifySecondaryWindowAnimation);

        Assert.Contains("[switch]$RequirePassed", verifyManualChecklist);
        Assert.Contains("[string]$ChecklistPath", verifyManualChecklist);
        Assert.Contains("BLOSS_MANUAL_CHECKLIST_PATH", verifyManualChecklist);
        Assert.Contains("Manual verification checklist structure passed.", verifyManualChecklist);
        Assert.Contains("Use -RequirePassed before release upload.", verifyManualChecklist);
        Assert.Contains("RequiredGateText", verifyManualChecklist);
        Assert.Contains("Manual gate $id text is missing required fragment", verifyManualChecklist);
        Assert.Contains("Manual gate $id is $status but has no evidence.", verifyManualChecklist);
        Assert.Contains("Manual gate $id is PENDING but still has evidence.", verifyManualChecklist);
        Assert.Contains("PrintsInstructionsOnly", showManualGateCommands);
        Assert.Contains("$manualScriptVersion", showManualGateCommands);
        Assert.Contains("manual-verification-v108.md", showManualGateCommands);
        Assert.Contains("Manual gate command helper verification passed.", showManualGateCommands);
        Assert.Contains("It does not install, uninstall, edit registry, or change Windows power settings.", showManualGateCommands);
        Assert.Contains("archive --format=zip", buildOldInstallerPrereq);
        Assert.Contains("artifacts\\manual-gate-old-builds", buildOldInstallerPrereq);
        Assert.Contains("Old installer prerequisite build completed.", buildOldInstallerPrereq);
        Assert.Contains("[ValidateSet(\"1.0.4\", \"1.0.5\", \"1.0.6\")]", buildOldInstallerPrereq);
        Assert.Contains("[switch]$RequireOldInstallers", checkManualGatePrereqs);
        Assert.Contains("[switch]$RequireNoCurrentAutostart", checkManualGatePrereqs);
        Assert.Contains("This can relaunch the wrong build after restart", checkManualGatePrereqs);
        Assert.Contains("scripts\\check-autostart-cleanup.ps1 -Delete", checkManualGatePrereqs);
        Assert.Contains("Do not use -Delete as release proof", checkManualGatePrereqs);
        Assert.Contains("MissingOldInstallerVersions", checkManualGatePrereqs);
        Assert.Contains("CurrentUserAutostartValues", checkManualGatePrereqs);
        Assert.Contains("Current-user Bloss startup values", checkManualGatePrereqs);
        Assert.Contains("Get-Sha256OrEmpty", checkManualGatePrereqs);
        Assert.Contains("SHA256 = Get-Sha256OrEmpty", checkManualGatePrereqs);
        Assert.Contains("Format-List ProductVersion, Length, LastWriteTime, SHA256, Path", checkManualGatePrereqs);
        Assert.Contains("Recommended old installers for manual update gates", checkManualGatePrereqs);
        Assert.Contains("manual-gate-old-builds", checkManualGatePrereqs);
        Assert.Contains("This script only inspects files, current-user startup values, and running processes.", checkManualGatePrereqs);
        Assert.Contains("Manual gate prerequisite check completed.", checkManualGatePrereqs);
        Assert.Contains("1.0.4", checkManualGatePrereqs);
        Assert.Contains("1.0.5", checkManualGatePrereqs);
        Assert.Contains("1.0.6", checkManualGatePrereqs);
        Assert.Contains("v107-manual-gate-evidence.md", exportManualGateEvidence);
        Assert.Contains("This report only reads files, current-user startup values, and running processes", exportManualGateEvidence);
        Assert.Contains("Recommended Old Installers", exportManualGateEvidence);
        Assert.Contains("Manual Gate Status", exportManualGateEvidence);
        Assert.Contains("Current-User Bloss Startup Values", exportManualGateEvidence);
        Assert.Contains("CurrentUserAutostartValues", exportManualGateEvidence);
        Assert.Contains("NonDestructive = $true", exportManualGateEvidence);
        Assert.Contains("Manual gate evidence report exported.", exportManualGateEvidence);
        Assert.Contains("show-guide-button-events.ps1 -SteamPowerOffCheck", showManualGateCommands);
        Assert.Contains("capture window does not close unexpectedly", showManualGateCommands);
        Assert.Contains("Quick Access highlight stays on the lower square hotspot", showManualGateCommands);
        Assert.Contains("real Steam Controller guide/power/custom/Quick Access stability/rename checks", showManualGateCommands);
        Assert.Contains("real Steam Controller guide/power/custom/Quick Access stability/rename checks", verifyManualChecklist);
        Assert.Contains("Get-Date -Format \"yyyy-MM-dd\"", showManualGateCommands);
        Assert.Contains("{Date} <machine>", showManualGateCommands);
        Assert.DoesNotContain("2026-06-14 <machine>", showManualGateCommands);
        Assert.Contains("check-display-sleep-readiness.ps1 -NoFail", showManualGateCommands);
        Assert.Contains("connected but untouched", showManualGateCommands);
        Assert.Contains("guide/PS wake/resume checked", showManualGateCommands);
        Assert.Contains("PowerShell as Administrator", showManualGateCommands);
        if (guide is not null)
        {
            Assert.Contains("powercfg /waketimers", guide);
            Assert.Contains("snapshot permission limit", guide);
        }
        Assert.Contains("check-autostart-cleanup.ps1", showManualGateCommands);
        Assert.Contains("check-$manualScriptVersion-manual-gate-prereqs.ps1", showManualGateCommands);
        Assert.Contains("manual-verification-v108.md", showManualGateCommands);
        Assert.Contains("-RequireNoCurrentAutostart", showManualGateCommands);
        Assert.Contains("recommended old v1.0.4 setup.exe", showManualGateCommands);
        Assert.Contains("recommended old v1.0.5 setup.exe", showManualGateCommands);
        Assert.Contains("recommended old v1.0.6 setup.exe", showManualGateCommands);
        Assert.Contains("Get-RecommendedOldInstallerEvidence", showManualGateCommands);
        Assert.Contains("Get-OldInstallerSearchRoots", showManualGateCommands);
        Assert.Contains("Split-Path -Parent $projectRoot", showManualGateCommands);
        Assert.Contains("manual-gate-old-builds", showManualGateCommands);
        Assert.Contains("Get-ChildItem -LiteralPath $root -Recurse -File -Filter setup.exe", showManualGateCommands);
        Assert.Contains("if ($_.FullName -like \"*\\artifacts\\manual-gate-old-builds\\*\")", showManualGateCommands);
        Assert.Contains("{OldInstallerEvidence}", showManualGateCommands);
        Assert.Contains("Get-CurrentTestExeEvidence", showManualGateCommands);
        Assert.Contains("{CurrentTestExeEvidence}", showManualGateCommands);
        Assert.Contains("Get-CurrentManualGateBlockers", showManualGateCommands);
        Assert.Contains("CURRENT LOCAL MANUAL-GATE BLOCKER", showManualGateCommands);
        Assert.Contains("Set-Location -LiteralPath", showManualGateCommands);
        Assert.Contains("Do not start install/update/uninstall manual gates", showManualGateCommands);
        Assert.Contains("check-$manualScriptVersion-manual-gate-prereqs.ps1", showManualGateCommands);
        Assert.Contains("never count that as uninstall proof", showManualGateCommands);
        Assert.Contains(@"latest artifacts\portable\test.exe", showManualGateCommands);
        Assert.Contains("LiveRunsPassed 2 of 2", showManualGateCommands);
        Assert.Contains("Split(\",\", [System.StringSplitOptions]::RemoveEmptyEntries)", showManualGateCommands);
        Assert.Contains("Unknown manual gate id(s)", showManualGateCommands);
        Assert.Contains("if ($requestedIds.Count -eq 0 -and -not $All)", showManualGateCommands);
        Assert.Contains("OldInstallerVersion", showManualGateCommands);
        Assert.Contains("set-$manualScriptVersion-manual-gate.ps1", showManualGateCommands);
        if (guide is not null)
        {
            Assert.Contains("set-v108-manual-gate.ps1", guide);
            Assert.Contains("scripts\\export-v108-manual-gate-evidence.ps1", guide);
            Assert.Contains("do not count that as uninstall proof", guide);
            Assert.Contains("Only use `PASS` after the gate's real install, device, display, or visual check has actually passed.", guide);
        }
        Assert.Contains("Evidence is required when setting $Id to $status.", setManualGate);
        Assert.Contains("verify-v107-manual-checklist.ps1", setManualGate);
        Assert.Contains("Evidence was cleared because the gate is pending.", setManualGate);
        Assert.Contains("connected-but-untouched gamepads do not block display-off or sleep", checkDisplaySleep);
        Assert.Contains("show-gamepad-idle-activity.ps1", checkDisplaySleep);
        Assert.Contains("real gamepad input delays display-off like keyboard/mouse", checkDisplaySleep);
        Assert.Contains("manual-verification-v107-updater-test", verifyManualGateUpdater);
        Assert.Contains("Evidence is required when setting DISPLAY-SLEEP to PASS.", verifyManualGateUpdater);
        Assert.Contains("Evidence is required when setting DISPLAY-SLEEP to FAIL.", verifyManualGateUpdater);
        Assert.Contains("Manual gate DISPLAY-SLEEP is FAIL but has no evidence.", verifyManualGateUpdater);
        Assert.Contains("Manual gate DISPLAY-SLEEP is PENDING but still has evidence.", verifyManualGateUpdater);
        Assert.Contains("PendingClearsEvidence", verifyManualGateUpdater);
        Assert.Contains("VerifierRejectsFailWithoutEvidence", verifyManualGateUpdater);
        Assert.Contains("VerifierRejectsPendingWithEvidence", verifyManualGateUpdater);
        Assert.Contains("Manual gate updater verification passed.", verifyManualGateUpdater);
        if (manualChecklist is not null)
        {
            Assert.Contains("Do not mark an item `PASS` or `FAIL` unless the Evidence cell names the machine/path/version/date", manualChecklist);
            Assert.Contains("Keep the Evidence cell empty while an item is `PENDING`", manualChecklist);
            Assert.Contains("verify-v108-release-ready.ps1", manualChecklist);
            Assert.Contains("-RequireManualGatePasses", manualChecklist);
            Assert.Contains("-RequireNoRunningBlossOrTest", manualChecklist);
            Assert.Contains("-RequireNoCurrentAutostart", manualChecklist);
            Assert.Contains("Do not upload while either command fails.", manualChecklist);
            Assert.Contains("UPDATE-104", manualChecklist);
            Assert.Contains("UPDATE-105", manualChecklist);
            Assert.Contains("UPDATE-106-NOTES", manualChecklist);
            Assert.Contains("CLEAN-INSTALL-NOTES", manualChecklist);
            Assert.Contains("DISPLAY-SLEEP", manualChecklist);
            Assert.Contains("STEAM-CONTROLLER", manualChecklist);
            Assert.Contains("Quick Access capture window stability", manualChecklist);
            Assert.Contains("lower square hotspot highlight", manualChecklist);
            Assert.Contains("UNINSTALL-AUTOSTART", manualChecklist);
            Assert.Contains("SETTINGS-SECONDARY-WINDOWS", manualChecklist);
        }

        Assert.Contains("Software\\Microsoft\\Windows\\CurrentVersion\\Run", checkAutostart);
        Assert.Contains("\"Bloss\"", checkAutostart);
        Assert.Contains("\"BluetoothBatteryWidget\"", checkAutostart);
        Assert.Contains("HKEY_CURRENT_USER", checkAutostart);
        Assert.Contains("S-1-5-21-", checkAutostart);
        Assert.Contains("Read-only check. No registry values will be changed unless -Delete is specified.", checkAutostart);
        Assert.Contains("Delete mode requested. Only Bloss startup values named Bloss or BluetoothBatteryWidget will be removed.", checkAutostart);
        Assert.Contains("$deleteRequested = $Delete.IsPresent", checkAutostart);
        Assert.Matches(@"if \(\$deleteRequested\)\s*\{[\s\S]*\$key\.DeleteValue\(\$valueName, \$false\)", checkAutostart);

        Assert.Contains("SUB_VIDEO", checkDisplaySleep);
        Assert.Contains("VIDEOIDLE", checkDisplaySleep);
        Assert.Contains("SUB_SLEEP", checkDisplaySleep);
        Assert.Contains("STANDBYIDLE", checkDisplaySleep);
        Assert.Contains("Current power line status", checkDisplaySleep);
        Assert.Contains("current power mode", checkDisplaySleep);
        Assert.Contains("powercfg /requests", checkDisplaySleep);
        Assert.Contains("Test-PowerCfgAdministratorRequired", checkDisplaySleep);
        Assert.Contains("PowerShell as Administrator", checkDisplaySleep);
        Assert.Contains("not evidence that Bloss is keeping the display awake", checkDisplaySleep);
        Assert.Contains("HKEY_USERS", checkAutostart);
        Assert.Contains("[switch]$Delete", checkAutostart);
        Assert.Contains("No Bloss autostart values found.", checkAutostart);
    }

    [Fact]
    public void PackageJson_DoesNotAdvertiseOldVersion()
    {
        var packageJson = File.ReadAllText(Path.Combine(ProjectRoot, "package.json"));
        using var document = JsonDocument.Parse(packageJson);

        Assert.Equal("1.0.8", document.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void PortableBuildScripts_CopyOptionalControllerBlueprintAssetsNextToTestExe()
    {
        foreach (var scriptName in new[] { "build-test-portable.ps1", "build-portable.ps1" })
        {
            var script = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", scriptName));

            Assert.Contains("$optionalBlueprintAssets", script);
            Assert.Contains("controller-guide-blueprint.png", script);
            Assert.Contains("controller-guide-blueprint.jpg", script);
            Assert.Contains("controller-guide-blueprint.jpeg", script);
            Assert.Contains("battery-guide-trigger-blueprint.png", script);
            Assert.Contains("battery-guide-trigger-blueprint.jpg", script);
            Assert.Contains("battery-guide-trigger-blueprint.jpeg", script);
            Assert.Contains("Copy-OptionalBlueprintAssets", script);
            Assert.Contains("$destinationAsset = Join-Path $portableRoot $assetName", script);
        }

        var releaseScript = File.ReadAllText(Path.Combine(ProjectRoot, "scripts", "build-portable.ps1"));
        Assert.Contains("$portableFiles += $assetPath", releaseScript);
        Assert.Contains("Compress-Archive -Path $portableFiles", releaseScript);
    }

    [Fact]
    public void V108ManualGateAliasScripts_UseV108ChecklistAndForwardToHistoricalV107Implementation()
    {
        var aliases = new Dictionary<string, string>
        {
            ["verify-v108-release-ready.ps1"] = "verify-v107-release-ready.ps1",
            ["verify-v108-manual-checklist.ps1"] = "verify-v107-manual-checklist.ps1",
            ["show-v108-manual-gate-commands.ps1"] = "show-v107-manual-gate-commands.ps1",
            ["check-v108-manual-gate-prereqs.ps1"] = "check-v107-manual-gate-prereqs.ps1",
            ["export-v108-manual-gate-evidence.ps1"] = "export-v107-manual-gate-evidence.ps1",
            ["set-v108-manual-gate.ps1"] = "set-v107-manual-gate.ps1",
            ["verify-v108-manual-gate-updater.ps1"] = "verify-v107-manual-gate-updater.ps1",
            ["build-v108-old-installer-prereq.ps1"] = "build-v107-old-installer-prereq.ps1"
        };

        foreach (var (alias, target) in aliases)
        {
            var aliasPath = Path.Combine(ProjectRoot, "scripts", alias);
            Assert.True(File.Exists(aliasPath), $"{alias} should exist.");
            var source = File.ReadAllText(aliasPath);
            Assert.Contains("Join-Path $PSScriptRoot", source);
            Assert.Contains(target, source);
            Assert.Contains("@args", source);
            Assert.Contains("BLOSS_MANUAL_CHECKLIST_PATH", source);
            Assert.Contains("manual-verification-v108.md", source);
            Assert.Contains("previousChecklistPath", source);
        }

        var guide = ReadOptionalProjectNote("howtorelease.md");
        if (guide is not null)
        {
            Assert.Contains("v108", guide);
            Assert.Contains("manual-verification-v108.md", guide);
            Assert.Contains("verify-v108-release-ready.ps1", guide);
        }
    }
}
