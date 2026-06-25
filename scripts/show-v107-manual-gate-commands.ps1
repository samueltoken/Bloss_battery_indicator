param(
    [string[]]$Id,

    [switch]$All,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$checklistPath = if (-not [string]::IsNullOrWhiteSpace($env:BLOSS_MANUAL_CHECKLIST_PATH)) {
    [System.IO.Path]::GetFullPath($env:BLOSS_MANUAL_CHECKLIST_PATH)
}
else {
    Join-Path $projectRoot "manual-verification-v107.md"
}
$manualChecklistName = Split-Path -Leaf $checklistPath
$manualScriptVersion = if ($manualChecklistName -match '^manual-verification-(v\d+)\.md$') {
    $Matches[1]
}
else {
    "v107"
}
$setGateScript = ".\scripts\set-$manualScriptVersion-manual-gate.ps1"
$manualPrereqScript = ".\scripts\check-$manualScriptVersion-manual-gate-prereqs.ps1"
$autostartValueNames = @("Bloss", "BluetoothBatteryWidget")
$autostartRunKeyPath = "Software\Microsoft\Windows\CurrentVersion\Run"

function Resolve-ExistingDirectory {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
    }
    catch {
        return $null
    }

    if (Test-Path -LiteralPath $fullPath -PathType Container) {
        return $fullPath
    }

    return $null
}

function Get-OldInstallerSearchRoots {
    $parentRoot = Split-Path -Parent $projectRoot
    @(
        $projectRoot
        $parentRoot
    ) |
        ForEach-Object { Resolve-ExistingDirectory -Path $_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
}

function New-GatePlan {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Id,

        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [string[]]$Checks,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceTemplate,

        [string]$OldInstallerVersion = ""
    )

    [pscustomobject]@{
        Id = $Id
        Title = $Title
        Checks = $Checks
        EvidenceTemplate = $EvidenceTemplate
        OldInstallerVersion = $OldInstallerVersion
    }
}

function Get-TrimmedVersion {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ""
    }

    $rawVersion = (Get-Item -LiteralPath $Path).VersionInfo.ProductVersion
    if ($null -eq $rawVersion) {
        return ""
    }

    return $rawVersion.Trim()
}

function Get-Sha256OrEmpty {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ""
    }

    try {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    }
    catch {
        return ""
    }
}

function Get-RecommendedOldInstallerEvidence {
    param([string]$Version)

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return "recommended v$Version setup.exe SHA256 <hash>"
    }

    $searchRoots = @(Get-OldInstallerSearchRoots)
    if ($searchRoots.Count -eq 0) {
        return "recommended v$Version setup.exe SHA256 <hash>"
    }

    $candidates = @(
        foreach ($root in $searchRoots) {
            Get-ChildItem -LiteralPath $root -Recurse -File -Filter setup.exe -ErrorAction SilentlyContinue |
                Where-Object { (Get-TrimmedVersion -Path $_.FullName) -eq $Version }
        }
    )

    $candidate = @(
        $candidates |
            Sort-Object `
                @{ Expression = { if ($_.FullName -like "*\artifacts\manual-gate-old-builds\*") { 0 } else { 1 } } },
                @{ Expression = "LastWriteTime"; Descending = $true },
                @{ Expression = "FullName"; Descending = $false }
    ) | Select-Object -First 1

    if ($null -eq $candidate) {
        return "recommended v$Version setup.exe SHA256 <hash>"
    }

    $hash = Get-Sha256OrEmpty -Path $candidate.FullName
    if ([string]::IsNullOrWhiteSpace($hash)) {
        return "recommended v$Version setup.exe $($candidate.FullName) SHA256 <hash>"
    }

    return "recommended v$Version setup.exe $($candidate.FullName) SHA256 $hash"
}

function Get-CurrentTestExeEvidence {
    $testExePath = Join-Path $projectRoot "artifacts\portable\test.exe"
    if (-not (Test-Path -LiteralPath $testExePath -PathType Leaf)) {
        return "latest artifacts\portable\test.exe ProductVersion <version> SHA256 <hash>"
    }

    $version = Get-TrimmedVersion -Path $testExePath
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = "<version>"
    }

    $hash = Get-Sha256OrEmpty -Path $testExePath
    if ([string]::IsNullOrWhiteSpace($hash)) {
        $hash = "<hash>"
    }

    return "latest artifacts\portable\test.exe ProductVersion $version SHA256 $hash"
}

function Resolve-EvidenceTemplate {
    param([object]$Plan)

    $evidence = $Plan.EvidenceTemplate
    $evidence = $evidence.Replace("{Date}", (Get-Date -Format "yyyy-MM-dd"))
    if (-not [string]::IsNullOrWhiteSpace($Plan.OldInstallerVersion)) {
        $oldInstallerEvidence = Get-RecommendedOldInstallerEvidence -Version $Plan.OldInstallerVersion
        $evidence = $evidence.Replace("{OldInstallerEvidence}", $oldInstallerEvidence)
    }

    $currentTestExeEvidence = Get-CurrentTestExeEvidence
    $evidence = $evidence.Replace("{CurrentTestExeEvidence}", $currentTestExeEvidence)

    return $evidence.Replace('"', "'")
}

function Get-CurrentManualGateBlockers {
    $blockers = New-Object System.Collections.Generic.List[string]
    $runningProcesses = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -in @("test", "Bloss") })
    if ($runningProcesses.Count -gt 0) {
        $processSummary = @(
            $runningProcesses |
                ForEach-Object {
                    $path = if ([string]::IsNullOrWhiteSpace($_.Path)) { "<path unavailable>" } else { $_.Path }
                    "$($_.ProcessName)($($_.Id)) $path"
                }
        ) -join "; "
        [void]$blockers.Add("Running Bloss/test process: $processSummary")
    }

    $runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($autostartRunKeyPath, $false)
    try {
        foreach ($valueName in $autostartValueNames) {
            $data = if ($null -ne $runKey) { $runKey.GetValue($valueName, $null) } else { $null }
            if ($null -ne $data) {
                [void]$blockers.Add("Current-user startup value: HKEY_CURRENT_USER\$autostartRunKeyPath\$valueName=$([string]$data)")
            }
        }
    }
    finally {
        if ($null -ne $runKey) {
            $runKey.Dispose()
        }
    }

    return @($blockers)
}

function Get-ChecklistRows {
    if (-not (Test-Path -LiteralPath $checklistPath)) {
        throw "Manual verification checklist not found: $checklistPath"
    }

    $rows = @{}
    foreach ($line in Get-Content -Encoding UTF8 -LiteralPath $checklistPath) {
        if ($line -notmatch "^\|\s*(?<id>[A-Z0-9-]+)\s*\|\s*(?<status>PENDING|PASS|FAIL)\s*\|") {
            continue
        }

        $rows[$Matches.id] = $Matches.status
    }

    return $rows
}

$gatePlans = @(
    New-GatePlan -Id "UPDATE-104" -Title "v1.0.4 in-app update reaches 1.0.9 and restarts" -Checks @(
        "Run powershell -ExecutionPolicy Bypass -File $manualPrereqScript -RequireOldInstallers -RequireNoRunningProcesses -RequireNoCurrentAutostart.",
        "Install the recommended old v1.0.4 setup.exe shown by that prerequisite check on a real Windows account.",
        "Open Bloss and start the in-app update flow.",
        "Confirm the app restarts after setup and the visible app/ProductVersion is 1.0.9.",
        "Keep the old setup.exe path, SHA256, installer/update log path, or a short note with machine name and date."
    ) -EvidenceTemplate "{Date} <machine>: installed {OldInstallerEvidence}, in-app update completed, Bloss restarted, visible/ProductVersion 1.0.9 confirmed" -OldInstallerVersion "1.0.4"
    New-GatePlan -Id "UPDATE-105" -Title "v1.0.5 in-app update reaches 1.0.9 and restarts" -Checks @(
        "Run powershell -ExecutionPolicy Bypass -File $manualPrereqScript -RequireOldInstallers -RequireNoRunningProcesses -RequireNoCurrentAutostart.",
        "Install the recommended old v1.0.5 setup.exe shown by that prerequisite check on a real Windows account.",
        "Open Bloss and start the in-app update flow.",
        "Confirm the app restarts after setup and the visible app/ProductVersion is 1.0.9.",
        "Watch for the old silent-update/antivirus abort failure returning."
    ) -EvidenceTemplate "{Date} <machine>: installed {OldInstallerEvidence}, in-app update completed, Bloss restarted, visible/ProductVersion 1.0.9 confirmed" -OldInstallerVersion "1.0.5"
    New-GatePlan -Id "UPDATE-106-NOTES" -Title "v1.0.6 update release notes one-time behavior" -Checks @(
        "Run powershell -ExecutionPolicy Bypass -File $manualPrereqScript -RequireOldInstallers -RequireNoRunningProcesses -RequireNoCurrentAutostart.",
        "Install the recommended old v1.0.6 setup.exe shown by that prerequisite check on a real Windows account.",
        "Update to v1.0.9 from inside Bloss.",
        "Confirm the 1.0.9 release notes popup appears on first app start.",
        "Close/confirm the popup, restart Bloss, and confirm the popup does not appear again."
    ) -EvidenceTemplate "{Date} <machine>: installed {OldInstallerEvidence}, updated to 1.0.9, release notes appeared once after update, did not reappear after restart" -OldInstallerVersion "1.0.6"
    New-GatePlan -Id "CLEAN-INSTALL-NOTES" -Title "clean install release notes one-time behavior" -Checks @(
        "Run powershell -ExecutionPolicy Bypass -File $manualPrereqScript -RequireNoRunningProcesses -RequireNoCurrentAutostart.",
        "Install release\installer\setup.exe on a clean or reset Windows account.",
        "Open Bloss and confirm the 1.0.9 release notes popup appears once.",
        "Close/confirm the popup, restart Bloss, and confirm the popup does not appear again.",
        "Do not use artifacts\portable\test.exe for this gate because test.exe intentionally shows the popup every run."
    ) -EvidenceTemplate "{Date} <machine>: clean install release\installer\setup.exe, release notes appeared once, did not reappear after restart"
    New-GatePlan -Id "TEST-EXE-NOTES-VISUAL" -Title "test.exe release notes every-run visual check" -Checks @(
        "Run powershell -ExecutionPolicy Bypass -File .\scripts\verify-release-notes-popup.ps1 -Live -LaunchCount 2 -TimeoutSeconds 20.",
        "Open artifacts\release-notes-previews\release-notes-window.png and visually confirm the popup design.",
        "Confirm the red BLoss circle and update list are visible and the reference ORACUS logo is absent."
    ) -EvidenceTemplate "{Date} <machine>: {CurrentTestExeEvidence}; verify-release-notes-popup.ps1 -Live -LaunchCount 2 -TimeoutSeconds 20 passed LiveRunsPassed 2 of 2; preview PNG visually checked"
    New-GatePlan -Id "DISPLAY-SLEEP" -Title "Windows display-off/system sleep is not blocked" -Checks @(
        "Run powershell -ExecutionPolicy Bypass -File .\scripts\check-display-sleep-readiness.ps1 -NoFail and note the current power mode.",
        "If that snapshot says powercfg /requests or /waketimers needs administrator rights, rerun the snapshot from PowerShell as Administrator before judging Bloss.",
        "Set Windows display-off timeout to 1 minute, or system sleep to a short test value, for that current power mode.",
        "Run Bloss normally with no gamepad input, do not move input devices, and wait at least 90 seconds.",
        "Repeat with a real DualSense/Pico2W, Steam Controller, and third-party gamepad connected but untouched; confirm connected idle does not block display-off.",
        "Wake the screen, press a real guide/PS input, and confirm Bloss guide handling resumes without keeping the display awake permanently.",
        "Put Bloss in tray, wait again, and confirm the same display-off or sleep behavior.",
        "Restore the user's preferred Windows power setting after the test."
    ) -EvidenceTemplate "{Date} <machine>: check-display-sleep-readiness snapshot captured current power mode, Windows display-off 1 minute test passed with no gamepad plus real DualSense/Pico2W, Steam Controller, and third-party gamepad connected but untouched; guide/PS wake/resume checked; setting restored"
    New-GatePlan -Id "STEAM-CONTROLLER" -Title "real Steam Controller guide/power/custom/Quick Access stability/rename checks" -Checks @(
        "Run dotnet test BluetoothBatteryWidget.sln --configuration Release first.",
        "Run artifacts\portable\test.exe with a real Steam Controller connected.",
        "Short-press Steam three times about one second apart and confirm the battery guide appears each time.",
        "Long-hold Steam/power and confirm the controller power flow is not interrupted by repeated battery toasts.",
        "Open the custom guide trigger capture window, press Steam Quick Access and several Steam Controller buttons, and confirm the capture window does not close unexpectedly.",
        "Confirm the Quick Access highlight stays on the lower square hotspot.",
        "Rename the Steam Controller, then confirm the short Steam press still works.",
        "Use .\scripts\show-guide-button-events.ps1 -SteamPowerOffCheck while checking logs if behavior is unclear."
    ) -EvidenceTemplate "{Date} <machine>: real Steam Controller short press repeated, long power hold suppressed toast, Quick Access custom trigger stayed open and highlighted lower square, custom/rename behavior confirmed, show-guide-button-events.ps1 -SteamPowerOffCheck checked"
    New-GatePlan -Id "UNINSTALL-AUTOSTART" -Title "uninstall removes Bloss startup values" -Checks @(
        "Run powershell -ExecutionPolicy Bypass -File $manualPrereqScript -RequireNoRunningProcesses -RequireNoCurrentAutostart before starting the uninstall gate.",
        "Install Bloss in the normal way for the current Windows account.",
        "Uninstall from Windows Apps or run the generated uninstall.exe.",
        "Run powershell -ExecutionPolicy Bypass -File .\scripts\check-autostart-cleanup.ps1.",
        "Confirm HKCU Run values named Bloss and BluetoothBatteryWidget are both absent.",
        "Do not use -Delete for the release gate unless intentionally cleaning a broken developer/user PC."
    ) -EvidenceTemplate "{Date} <machine>: uninstall completed, check-autostart-cleanup.ps1 reported Bloss and BluetoothBatteryWidget startup values absent"
    New-GatePlan -Id "SETTINGS-SECONDARY-WINDOWS" -Title "settings secondary windows finish smoothly" -Checks @(
        "Run dotnet test BluetoothBatteryWidget.sln --configuration Release --no-restore --filter FullyQualifiedName~SecondaryWindows_PopInAnimationSettlesToStableFinalValues.",
        "Run powershell -ExecutionPolicy Bypass -File .\scripts\verify-secondary-window-animation.ps1.",
        "Optionally open the four secondary settings windows visually and confirm they finish without end-position stutter."
    ) -EvidenceTemplate "{Date} <machine>: SecondaryWindows_PopInAnimationSettlesToStableFinalValues and verify-secondary-window-animation.ps1 passed"
)

$checklistRows = Get-ChecklistRows
$missing = @($gatePlans | Where-Object { -not $checklistRows.ContainsKey($_.Id) } | ForEach-Object { $_.Id })
if ($missing.Count -gt 0) {
    throw "Manual gate command helper has plans not present in checklist: $($missing -join ', ')"
}

$missingPlans = @($checklistRows.Keys | Where-Object { $gatePlans.Id -notcontains $_ })
if ($missingPlans.Count -gt 0) {
    throw "Manual gate command helper is missing plans for checklist rows: $($missingPlans -join ', ')"
}

$validGateIds = @($gatePlans | ForEach-Object { $_.Id })
$requestedIds = @(
    foreach ($rawId in $Id) {
        if ([string]::IsNullOrWhiteSpace($rawId)) {
            continue
        }

        $rawId.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries) |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }
) | Select-Object -Unique

$invalidIds = @($requestedIds | Where-Object { $validGateIds -notcontains $_ })
if ($invalidIds.Count -gt 0) {
    throw "Unknown manual gate id(s): $($invalidIds -join ', '). Valid ids: $($validGateIds -join ', ')."
}

$selectedPlans = $gatePlans
if ($requestedIds.Count -gt 0) {
    $selectedPlans = @($selectedPlans | Where-Object { $requestedIds -contains $_.Id })
}

if ($requestedIds.Count -eq 0 -and -not $All) {
    $selectedPlans = @($selectedPlans | Where-Object { $checklistRows[$_.Id] -ne "PASS" })
}

$summary = [pscustomobject]@{
    ChecklistPath = $checklistPath
    Total = $gatePlans.Count
    Passed = @($gatePlans | Where-Object { $checklistRows[$_.Id] -eq "PASS" }).Count
    Remaining = @($gatePlans | Where-Object { $checklistRows[$_.Id] -ne "PASS" }).Count
    Selected = $selectedPlans.Count
    PrintsInstructionsOnly = $true
}

$summary | Format-List

if ($ValidateOnly) {
    Write-Host "Manual gate command helper verification passed."
    exit 0
}

Write-Host "This helper only prints manual check instructions. It does not install, uninstall, edit registry, or change Windows power settings."
Write-Host "Run the printed commands from this project folder:"
Write-Host "Set-Location -LiteralPath `"$projectRoot`""
Write-Host ""

$currentBlockers = @(Get-CurrentManualGateBlockers)
if ($currentBlockers.Count -gt 0) {
    Write-Host "CURRENT LOCAL MANUAL-GATE BLOCKER:"
    foreach ($blocker in $currentBlockers) {
        Write-Host "- $blocker"
    }

    Write-Host "Do not start install/update/uninstall manual gates until these are intentionally cleared or explicitly recorded."
    Write-Host "Run $manualPrereqScript -RequireOldInstallers -RequireNoRunningProcesses -RequireNoCurrentAutostart before those gates."
    Write-Host "Use .\scripts\check-autostart-cleanup.ps1 -Delete only when intentionally cleaning this developer PC before a manual gate; never count that as uninstall proof."
    Write-Host ""
}

foreach ($plan in $selectedPlans) {
    $status = $checklistRows[$plan.Id]
    $evidenceTemplate = Resolve-EvidenceTemplate -Plan $plan
    Write-Host "== $($plan.Id): $($plan.Title) [$status] =="
    Write-Host "Checks:"
    foreach ($check in $plan.Checks) {
        Write-Host "- $check"
    }

    Write-Host "PASS record command after the check really passes:"
    Write-Host "powershell -ExecutionPolicy Bypass -File `"$setGateScript`" -Id $($plan.Id) -Status PASS -Evidence `"$evidenceTemplate`""
    Write-Host "FAIL record command if the check fails:"
    Write-Host "powershell -ExecutionPolicy Bypass -File `"$setGateScript`" -Id $($plan.Id) -Status FAIL -Evidence `"<describe the failure, machine, date, and observed symptom>`""
    Write-Host ""
}

Write-Host "Manual gate command helper completed."
