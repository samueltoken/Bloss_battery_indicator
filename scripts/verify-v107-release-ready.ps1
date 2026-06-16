param(
    [switch]$SkipTests,
    [switch]$LiveReleaseNotes,
    [switch]$CheckCurrentAutostart,
    [switch]$DisplaySleepSnapshot,
    [switch]$RequireManualGatePasses,
    [switch]$RequireNoRunningBlossOrTest,
    [switch]$RequireNoCurrentAutostart,
    [int]$ReleaseNotesLaunchCount = 2,
    [int]$ReleaseNotesTimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$manualGates = @(
    [pscustomobject]@{ Id = "UPDATE-104"; Description = "Install v1.0.4, update from inside the app, confirm the app reaches 1.0.7 and restarts." },
    [pscustomobject]@{ Id = "UPDATE-105"; Description = "Install v1.0.5, update from inside the app, confirm the app reaches 1.0.7 and restarts." },
    [pscustomobject]@{ Id = "UPDATE-106-NOTES"; Description = "Install v1.0.6, update from inside the app, confirm the release notes popup appears once." },
    [pscustomobject]@{ Id = "CLEAN-INSTALL-NOTES"; Description = "Clean install release\installer\setup.exe and confirm the release notes popup appears once." },
    [pscustomobject]@{ Id = "TEST-EXE-NOTES-VISUAL"; Description = "Run artifacts\portable\test.exe repeatedly and confirm the release notes popup appears every run." },
    [pscustomobject]@{ Id = "DISPLAY-SLEEP"; Description = "Run scripts\check-display-sleep-readiness.ps1, set Windows display-off timeout to 1 minute or system sleep to a short test value for the current power mode shown by the script, and confirm Bloss does not keep the monitor or sleep state awake." },
    [pscustomobject]@{ Id = "STEAM-CONTROLLER"; Description = "Use a real Steam Controller to confirm short Steam, long power hold, custom guide trigger, Quick Access capture-window stability, lower-square highlight, and renamed-device behavior." },
    [pscustomobject]@{ Id = "UNINSTALL-AUTOSTART"; Description = "Uninstall Bloss and run scripts\check-autostart-cleanup.ps1 to confirm Bloss and BluetoothBatteryWidget startup values are gone." },
    [pscustomobject]@{ Id = "SETTINGS-SECONDARY-WINDOWS"; Description = "Open settings secondary windows and confirm the pressed-open animation finishes smoothly." }
)

function Invoke-ReadinessStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "== $Name =="
    & $Action
    Write-Host "PASS: $Name"
}

function Test-FileContains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Needle
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -Encoding UTF8 -LiteralPath $Path -Raw
    return $content.Contains($Needle)
}

function Assert-FileContains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Needle,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not (Test-FileContains -Path $Path -Needle $Needle)) {
        throw $Message
    }
}

function Get-ManualGateStatuses {
    $checklistPath = Join-Path $projectRoot "manual-verification-v107.md"
    $statuses = @{}
    if (-not (Test-Path -LiteralPath $checklistPath)) {
        return $statuses
    }

    foreach ($line in Get-Content -Encoding UTF8 -LiteralPath $checklistPath) {
        if ($line -match "^\|\s*(?<id>[A-Z0-9-]+)\s*\|\s*(?<status>PENDING|PASS|FAIL)\s*\|") {
            $statuses[$Matches.id] = $Matches.status
        }
    }

    return $statuses
}

function Get-LatestSourceWriteTime {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Paths
    )

    $items = foreach ($path in $Paths) {
        $fullPath = Join-Path $projectRoot $path
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            Get-Item -LiteralPath $fullPath
        }
        elseif (Test-Path -LiteralPath $fullPath -PathType Container) {
            Get-ChildItem -LiteralPath $fullPath -Recurse -File | Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\'
            }
        }
        else {
            throw "Required source path not found: $fullPath"
        }
    }

    return ($items | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
}

function Assert-ArtifactFreshEnough {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArtifactPath,

        [Parameter(Mandatory = $true)]
        [string[]]$SourcePaths
    )

    $fullArtifactPath = Join-Path $projectRoot $ArtifactPath
    if (-not (Test-Path -LiteralPath $fullArtifactPath)) {
        throw "Artifact not found: $fullArtifactPath"
    }

    $artifact = Get-Item -LiteralPath $fullArtifactPath
    $latestSourceWriteTime = Get-LatestSourceWriteTime -Paths $SourcePaths
    if ($artifact.LastWriteTime -lt $latestSourceWriteTime) {
        throw "$ArtifactPath is older than app/installer source files. Rebuild it before release."
    }

    [pscustomobject]@{
        Artifact = $artifact.FullName
        ArtifactLastWriteTime = $artifact.LastWriteTime
        LatestSourceWriteTime = $latestSourceWriteTime
        FreshEnough = $true
    } | Format-List
}

Push-Location $projectRoot
try {
    Invoke-ReadinessStep -Name "Project root and release guide" -Action {
        $guidePath = Join-Path $projectRoot "howtorelease.md"
        $steadyStatusPath = Join-Path $projectRoot "steadystatus.md"
        $setManualGatePath = Join-Path $projectRoot "scripts\set-v107-manual-gate.ps1"
        Assert-FileContains -Path $guidePath -Needle "bloss_battery_indicator_test_ver107" -Message "Release guide does not point at this staging folder."
        Assert-FileContains -Path $guidePath -Needle "modification and verification staging folder" -Message "Release guide does not say this is a staging folder."
        Assert-FileContains -Path $guidePath -Needle "scripts\build-test-portable.ps1" -Message "Release guide does not mention the portable test build script."
        Assert-FileContains -Path $guidePath -Needle "scripts\verify-test-portable.ps1" -Message "Release guide does not mention the portable test verification script."
        Assert-FileContains -Path $guidePath -Needle "scripts\verify-release-notes-popup.ps1" -Message "Release guide does not mention the release-notes verification script."
        Assert-FileContains -Path $guidePath -Needle "scripts\verify-secondary-window-animation.ps1" -Message "Release guide does not mention the secondary-window animation verification script."
        Assert-FileContains -Path $guidePath -Needle "scripts\show-v107-manual-gate-commands.ps1" -Message "Release guide does not mention the manual gate command helper."
        Assert-FileContains -Path $guidePath -Needle "scripts\build-v107-old-installer-prereq.ps1" -Message "Release guide does not mention the old installer prerequisite builder."
        Assert-FileContains -Path $guidePath -Needle "scripts\check-v107-manual-gate-prereqs.ps1" -Message "Release guide does not mention the manual gate prerequisite check."
        Assert-FileContains -Path $guidePath -Needle "scripts\export-v107-manual-gate-evidence.ps1" -Message "Release guide does not mention the manual gate evidence export."
        Assert-FileContains -Path $guidePath -Needle "scripts\verify-v107-manual-gate-updater.ps1" -Message "Release guide does not mention the manual gate updater verification script."
        Assert-FileContains -Path $guidePath -Needle "scripts\set-v107-manual-gate.ps1" -Message "Release guide does not mention the manual gate updater script."
        Assert-FileContains -Path $setManualGatePath -Needle "Evidence is required when setting" -Message "Manual gate updater does not require evidence for PASS/FAIL."
        Assert-FileContains -Path $setManualGatePath -Needle "verify-v107-manual-checklist.ps1" -Message "Manual gate updater does not re-run checklist validation."
        Assert-FileContains -Path $guidePath -Needle "scripts\build-installer.ps1" -Message "Release guide does not mention the installer build script."
        Assert-FileContains -Path $guidePath -Needle "scripts\verify-installer.ps1" -Message "Release guide does not mention the installer verification script."
        Assert-FileContains -Path $guidePath -Needle "scripts\verify-v107-release-ready.ps1" -Message "Release guide does not mention the one-shot readiness gate."
        Assert-FileContains -Path $guidePath -Needle "scripts\check-autostart-cleanup.ps1" -Message "Release guide does not mention the autostart cleanup check."
        Assert-FileContains -Path $guidePath -Needle "installer\BluetoothBatteryWidget.iss" -Message "Release guide does not mention the current installer script."
        Assert-FileContains -Path $guidePath -Needle "display-off timeout to 1 minute" -Message "Release guide does not include the display-off manual gate."
        Assert-FileContains -Path $guidePath -Needle "system sleep" -Message "Release guide does not include the system sleep manual gate."
        Assert-FileContains -Path $guidePath -Needle "real Steam Controller" -Message "Release guide does not include the real Steam Controller manual gate."
        Assert-FileContains -Path $guidePath -Needle "Quick Access capture window" -Message "Release guide does not include the Steam Quick Access capture-window stability gate."
        Assert-FileContains -Path $guidePath -Needle "lower square hotspot" -Message "Release guide does not include the Steam Quick Access lower-square hotspot gate."
        Assert-FileContains -Path $guidePath -Needle "release notes popup appears once" -Message "Release guide does not include release-notes one-time behavior."
        Assert-FileContains -Path $guidePath -Needle "release notes popup appears every run" -Message "Release guide does not include test.exe every-run behavior."
        Assert-FileContains -Path $guidePath -Needle 'powershell -ExecutionPolicy Bypass -File ".\scripts\verify-v107-release-ready.ps1" -LiveReleaseNotes -DisplaySleepSnapshot -RequireManualGatePasses -RequireNoRunningBlossOrTest -RequireNoCurrentAutostart' -Message "Release guide does not include the final manual-gate upload guard command."
        Assert-FileContains -Path $guidePath -Needle 'Do not run `gh release upload` while this fails' -Message "Release guide does not block upload while manual gates are incomplete."
        Assert-FileContains -Path $guidePath -Needle '-RequireNoRunningProcesses' -Message "Release guide does not mention the no-running-process manual gate prerequisite guard."
        Assert-FileContains -Path $guidePath -Needle '-RequireNoCurrentAutostart' -Message "Release guide does not mention the current-user autostart prerequisite guard."
        Assert-FileContains -Path $guidePath -Needle '-RequireNoRunningBlossOrTest' -Message "Release guide does not mention the no-running-process upload guard."
        Assert-FileContains -Path $steadyStatusPath -Needle "scripts\build-installer.ps1" -Message "Steady status does not mention the current installer build script."
        Assert-FileContains -Path $steadyStatusPath -Needle "installer\BluetoothBatteryWidget.iss" -Message "Steady status does not mention the current installer script."
        if (Test-FileContains -Path $guidePath -Needle "bloss_battery_indicator_release_day1") {
            throw "Release guide still points at the old release_day1 folder."
        }
        if (Test-FileContains -Path $guidePath -Needle "build\scripts\build-installer.ps1") {
            throw "Release guide still points at the old build scripts path."
        }
        if (Test-FileContains -Path $guidePath -Needle "build\installer\BluetoothBatteryWidget.iss") {
            throw "Release guide still points at the old build installer path."
        }
        if (Test-FileContains -Path $steadyStatusPath -Needle "build/scripts/build-installer.ps1") {
            throw "Steady status still points at the old build/scripts installer path."
        }
        if (Test-FileContains -Path $steadyStatusPath -Needle "build/installer/BluetoothBatteryWidget.iss") {
            throw "Steady status still points at the old build/installer path."
        }
    }

    Invoke-ReadinessStep -Name "Power idle defaults and settings migration" -Action {
        $settingsModelPath = Join-Path $projectRoot "BluetoothBatteryWidget.Core\Models\WidgetSettings.cs"
        $settingsStorePath = Join-Path $projectRoot "BluetoothBatteryWidget.App\Services\WidgetSettingsStore.cs"
        $mainViewModelPath = Join-Path $projectRoot "BluetoothBatteryWidget.App\ViewModels\MainViewModel.cs"
        $systemDisplayIdleTimeoutPath = Join-Path $projectRoot "BluetoothBatteryWidget.App\Services\SystemDisplayIdleTimeout.cs"
        $settingsStoreTestsPath = Join-Path $projectRoot "BluetoothBatteryWidget.Tests\WidgetSettingsStoreTests.cs"
        $systemDisplayIdleTimeoutTestsPath = Join-Path $projectRoot "BluetoothBatteryWidget.Tests\SystemDisplayIdleTimeoutTests.cs"
        $powerIdleSourceSafetyTestsPath = Join-Path $projectRoot "BluetoothBatteryWidget.Tests\PowerIdleSourceSafetyTests.cs"

        Assert-FileContains -Path $settingsModelPath -Needle "public const int AutoPowerIdlePauseMinutes = -1;" -Message "Power idle auto sentinel is missing."
        Assert-FileContains -Path $settingsModelPath -Needle "public const int DefaultPowerIdlePauseMinutes = AutoPowerIdlePauseMinutes;" -Message "New installs/test.exe do not default to Windows auto power idle."
        Assert-FileContains -Path $settingsModelPath -Needle "public const int LegacyDefaultPowerIdlePauseMinutes = 1;" -Message "Legacy one-minute power idle default marker is missing."
        Assert-FileContains -Path $settingsModelPath -Needle "public const int WindowsPowerIdleAutoSettingsSchemaVersion = 2;" -Message "Power idle auto migration schema marker is missing."
        Assert-FileContains -Path $settingsStorePath -Needle "NormalizeLoaded" -Message "Settings load migration hook is missing."
        Assert-FileContains -Path $settingsStorePath -Needle "WindowsPowerIdleAutoSettingsSchemaVersion" -Message "Settings store does not apply the power idle auto migration schema."
        Assert-FileContains -Path $mainViewModelPath -Needle "SystemDisplayIdleTimeout.GetCurrentDisplayOrSleepTimeout()" -Message "Power idle auto does not read the current Windows display/sleep timeout."
        Assert-FileContains -Path $systemDisplayIdleTimeoutPath -Needle "GetCurrentDisplayOrSleepTimeout" -Message "Windows display/sleep timeout reader is missing."
        Assert-FileContains -Path $systemDisplayIdleTimeoutPath -Needle "SelectShortestPositiveTimeout(GetCurrentTimeout(), GetCurrentSleepTimeout())" -Message "Power idle auto does not follow the earlier Windows display-off or sleep timeout."
        Assert-FileContains -Path $settingsStoreTestsPath -Needle "Load_MigratesLegacyPowerIdleOneMinuteDefaultToWindowsAuto" -Message "Legacy one-minute setting migration test is missing."
        Assert-FileContains -Path $settingsStoreTestsPath -Needle "Load_PreservesCurrentSchemaPowerIdleOneMinuteUserChoice" -Message "Manual one-minute setting preservation test is missing."
        Assert-FileContains -Path $systemDisplayIdleTimeoutTestsPath -Needle "SelectShortestPositiveTimeout_UsesEarlierDisplayOrSleepTimeout" -Message "Windows display/sleep earliest-timeout test is missing."
        Assert-FileContains -Path $powerIdleSourceSafetyTestsPath -Needle "SetThreadExecutionState" -Message "Display/system-awake source safety guard is missing SetThreadExecutionState."
        Assert-FileContains -Path $powerIdleSourceSafetyTestsPath -Needle "ES_DISPLAY_REQUIRED" -Message "Display-awake source safety guard is missing ES_DISPLAY_REQUIRED."
        Assert-FileContains -Path $powerIdleSourceSafetyTestsPath -Needle "ES_SYSTEM_REQUIRED" -Message "System-awake source safety guard is missing ES_SYSTEM_REQUIRED."
        Assert-FileContains -Path $powerIdleSourceSafetyTestsPath -Needle "PowerSetRequest" -Message "Power request source safety guard is missing PowerSetRequest."
        Assert-FileContains -Path $powerIdleSourceSafetyTestsPath -Needle "SendInput(" -Message "Synthetic input source safety guard is missing SendInput."
        Assert-FileContains -Path $powerIdleSourceSafetyTestsPath -Needle "mouse_event(" -Message "Synthetic mouse source safety guard is missing mouse_event."
        Assert-FileContains -Path $powerIdleSourceSafetyTestsPath -Needle "keybd_event(" -Message "Synthetic keyboard source safety guard is missing keybd_event."
    }

    if (-not $SkipTests) {
        Invoke-ReadinessStep -Name "Release test suite" -Action {
            & dotnet test "BluetoothBatteryWidget.sln" --configuration Release --no-restore
        }
    }
    else {
        Write-Host ""
        Write-Host "SKIP: Release test suite (-SkipTests was supplied)."
    }

    Invoke-ReadinessStep -Name "Portable test executable" -Action {
        & (Join-Path $PSScriptRoot "verify-test-portable.ps1")
    }

    Invoke-ReadinessStep -Name "Release notes popup" -Action {
        $releaseNotesParams = @{}
        if ($LiveReleaseNotes) {
            $releaseNotesParams.Live = $true
            $releaseNotesParams.LaunchCount = $ReleaseNotesLaunchCount
            $releaseNotesParams.TimeoutSeconds = $ReleaseNotesTimeoutSeconds
        }

        & (Join-Path $PSScriptRoot "verify-release-notes-popup.ps1") @releaseNotesParams
    }

    Invoke-ReadinessStep -Name "Secondary window animation" -Action {
        & (Join-Path $PSScriptRoot "verify-secondary-window-animation.ps1")
    }

    Invoke-ReadinessStep -Name "Manual gate updater" -Action {
        & (Join-Path $PSScriptRoot "verify-v107-manual-gate-updater.ps1")
    }

    Invoke-ReadinessStep -Name "Manual gate command helper" -Action {
        & (Join-Path $PSScriptRoot "show-v107-manual-gate-commands.ps1") -ValidateOnly
    }

    Invoke-ReadinessStep -Name "Manual gate prerequisites" -Action {
        $manualGatePrereqParams = @{}
        if ($RequireNoRunningBlossOrTest) {
            $manualGatePrereqParams.RequireNoRunningProcesses = $true
        }

        if ($RequireNoCurrentAutostart) {
            $manualGatePrereqParams.RequireNoCurrentAutostart = $true
        }

        & (Join-Path $PSScriptRoot "check-v107-manual-gate-prereqs.ps1") @manualGatePrereqParams
    }

    Invoke-ReadinessStep -Name "Manual gate evidence report" -Action {
        & (Join-Path $PSScriptRoot "export-v107-manual-gate-evidence.ps1")
    }

    Invoke-ReadinessStep -Name "Manual verification checklist" -Action {
        $manualChecklistParams = @{}
        if ($RequireManualGatePasses) {
            $manualChecklistParams.RequirePassed = $true
        }

        & (Join-Path $PSScriptRoot "verify-v107-manual-checklist.ps1") @manualChecklistParams
    }

    Invoke-ReadinessStep -Name "Installer package" -Action {
        & (Join-Path $PSScriptRoot "verify-installer.ps1")
    }

    Invoke-ReadinessStep -Name "Artifact freshness" -Action {
        $appSourcePaths = @(
            "Directory.Build.props",
            "BluetoothBatteryWidget.App",
            "BluetoothBatteryWidget.Core"
        )
        $installerSourcePaths = @(
            "Directory.Build.props",
            "BluetoothBatteryWidget.App",
            "BluetoothBatteryWidget.Core",
            "installer"
        )

        Assert-ArtifactFreshEnough -ArtifactPath "artifacts\portable\test.exe" -SourcePaths $appSourcePaths
        Assert-ArtifactFreshEnough -ArtifactPath "release\installer\setup.exe" -SourcePaths $installerSourcePaths
    }

    if ($CheckCurrentAutostart) {
        Invoke-ReadinessStep -Name "Current user autostart cleanup state" -Action {
            & (Join-Path $PSScriptRoot "check-autostart-cleanup.ps1")
        }
    }
    else {
        Write-Host ""
        Write-Host "SKIP: Current user autostart cleanup state (-CheckCurrentAutostart was not supplied)."
        Write-Host "This is intentionally optional because a developer PC may legitimately have Bloss autostart enabled."
    }

    if ($DisplaySleepSnapshot) {
        Invoke-ReadinessStep -Name "Display sleep readiness snapshot" -Action {
            & (Join-Path $PSScriptRoot "check-display-sleep-readiness.ps1") -NoFail
        }
    }
    else {
        Write-Host ""
        Write-Host "SKIP: Display sleep readiness snapshot (-DisplaySleepSnapshot was not supplied)."
        Write-Host "Use it before the manual 1-minute monitor-off test to capture powercfg state."
    }

    $manualGateStatuses = Get-ManualGateStatuses
    $remainingManualGates = @($manualGates | Where-Object { $manualGateStatuses[$_.Id] -ne "PASS" })

    Write-Host ""
    if ($remainingManualGates.Count -gt 0) {
        Write-Host "Manual gates still required before release upload:"
        foreach ($gate in $remainingManualGates) {
            $status = $manualGateStatuses[$gate.Id]
            if ([string]::IsNullOrWhiteSpace($status)) {
                $status = "MISSING"
            }

            Write-Host "- [$status] $($gate.Id): $($gate.Description)"
        }
    }
    else {
        Write-Host "All manual gates are marked PASS."
    }

    Write-Host ""
    Write-Host "v1.0.7 release readiness gate passed for non-destructive local checks."
}
finally {
    Pop-Location
}
