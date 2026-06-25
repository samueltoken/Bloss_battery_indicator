param(
    [string]$ChecklistPath,
    [switch]$RequirePassed
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ChecklistPath)) {
    if (-not [string]::IsNullOrWhiteSpace($env:BLOSS_MANUAL_CHECKLIST_PATH)) {
        $ChecklistPath = $env:BLOSS_MANUAL_CHECKLIST_PATH
    }
    else {
        $ChecklistPath = Join-Path $projectRoot "manual-verification-v107.md"
    }
}

$checklistPath = [System.IO.Path]::GetFullPath($ChecklistPath)
$validStatuses = @("PENDING", "PASS", "FAIL")
$requiredGates = @(
    @{ Id = "UPDATE-104"; Description = "v1.0.4 in-app update reaches 1.0.9 and restarts" },
    @{ Id = "UPDATE-105"; Description = "v1.0.5 in-app update reaches 1.0.9 and restarts" },
    @{ Id = "UPDATE-106-NOTES"; Description = "v1.0.6 update release notes one-time behavior" },
    @{ Id = "CLEAN-INSTALL-NOTES"; Description = "clean install release notes one-time behavior" },
    @{ Id = "TEST-EXE-NOTES-VISUAL"; Description = "test.exe release notes every-run visual check" },
    @{ Id = "DISPLAY-SLEEP"; Description = "Windows display-off/system sleep is not blocked"; RequiredGateText = @("DualSense/Pico2W", "Steam Controller", "connected but untouched", "guide/PS") },
    @{ Id = "STEAM-CONTROLLER"; Description = "real Steam Controller guide/power/custom/Quick Access stability/rename checks"; RequiredGateText = @("short Steam press", "long power hold", "user-selected guide trigger", "Quick Access capture window stability", "lower square hotspot highlight", "renamed-device behavior") },
    @{ Id = "UNINSTALL-AUTOSTART"; Description = "uninstall removes Bloss startup values" },
    @{ Id = "SETTINGS-SECONDARY-WINDOWS"; Description = "settings secondary windows finish smoothly" }
)

if (-not (Test-Path -LiteralPath $checklistPath)) {
    throw "Manual verification checklist not found: $checklistPath"
}

$lines = Get-Content -Encoding UTF8 -LiteralPath $checklistPath
$failures = New-Object System.Collections.Generic.List[string]
$results = New-Object System.Collections.Generic.List[object]

foreach ($gate in $requiredGates) {
    $id = [string]$gate.Id
    $line = $lines | Where-Object {
        $_ -match "^\|\s*$([regex]::Escape($id))\s*\|"
    } | Select-Object -First 1

    if (-not $line) {
        $failures.Add("Missing manual gate row: $id")
        continue
    }

    $cells = $line.Trim().Trim("|").Split("|") | ForEach-Object { $_.Trim() }
    if ($cells.Count -lt 4) {
        $failures.Add("Manual gate row has too few columns: $id")
        continue
    }

    $status = $cells[1].ToUpperInvariant()
    $gateText = $cells[2]
    $evidence = $cells[3]
    if ($validStatuses -notcontains $status) {
        $failures.Add("Manual gate $id has invalid status '$status'. Use PENDING, PASS, or FAIL.")
    }

    foreach ($requiredText in @($gate.RequiredGateText)) {
        if (-not [string]::IsNullOrWhiteSpace($requiredText) -and
            $gateText.IndexOf([string]$requiredText, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            $failures.Add("Manual gate $id text is missing required fragment '$requiredText'.")
        }
    }

    if ($status -in @("PASS", "FAIL") -and [string]::IsNullOrWhiteSpace($evidence)) {
        $failures.Add("Manual gate $id is $status but has no evidence.")
    }

    if ($status -eq "PENDING" -and -not [string]::IsNullOrWhiteSpace($evidence)) {
        $failures.Add("Manual gate $id is PENDING but still has evidence. Clear evidence before leaving it pending.")
    }

    if ($RequirePassed -and $status -ne "PASS") {
        $failures.Add("Manual gate $id is $status, not PASS.")
    }

    $results.Add([pscustomobject]@{
        Id = $id
        Status = $status
        Description = [string]$gate.Description
        Gate = $gateText
        Evidence = $evidence
    })
}

$summary = [pscustomobject]@{
    ChecklistPath = $checklistPath
    RequirePassed = $RequirePassed.IsPresent
    Total = $requiredGates.Count
    Passed = @($results | Where-Object { $_.Status -eq "PASS" }).Count
    Pending = @($results | Where-Object { $_.Status -eq "PENDING" }).Count
    Failed = @($results | Where-Object { $_.Status -eq "FAIL" }).Count
}

$summary | Format-List
$results | Sort-Object Id | Format-Table -AutoSize

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "FAIL: $failure"
    }

    throw "Manual verification checklist has $($failures.Count) issue(s): $($failures -join '; ')"
}

Write-Host "Manual verification checklist structure passed."
if ($RequirePassed) {
    Write-Host "All manual release gates are marked PASS."
}
else {
    Write-Host "Manual gates may still be pending. Use -RequirePassed before release upload."
}
