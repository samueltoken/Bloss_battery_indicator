param(
    [string]$ChecklistPath
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
$manualScriptVersion = if ((Split-Path -Leaf $checklistPath) -eq "manual-verification-v108.md") {
    "v108"
}
else {
    "v107"
}
$updaterPath = Join-Path $PSScriptRoot "set-$manualScriptVersion-manual-gate.ps1"
$verifierPath = Join-Path $PSScriptRoot "verify-$manualScriptVersion-manual-checklist.ps1"

function Assert-ContainsText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Needle,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Text.Contains($Needle)) {
        throw $Message
    }
}

function Assert-Fails {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedMessage
    )

    $failed = $false
    try {
        & $Action
    }
    catch {
        $failed = $true
        if (-not $_.Exception.Message.Contains($ExpectedMessage)) {
            throw "Expected failure containing '$ExpectedMessage', got '$($_.Exception.Message)'."
        }
    }

    if (-not $failed) {
        throw "Expected command to fail with '$ExpectedMessage'."
    }
}

function Set-ManualGateRowForTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Id,

        [Parameter(Mandatory = $true)]
        [string]$Status,

        [string]$Evidence = ""
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.AddRange([string[]](Get-Content -Encoding UTF8 -LiteralPath $Path))
    $updated = $false
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -notmatch "^\|\s*$([regex]::Escape($Id))\s*\|") {
            continue
        }

        $cells = $lines[$index].Trim().Trim("|").Split("|") | ForEach-Object { $_.Trim() }
        if ($cells.Count -lt 4) {
            throw "Manual gate row has too few columns in test copy: $Id"
        }

        $gateCell = $cells[2]
        $evidenceCell = (($Evidence -replace "[\r\n]+", " ") -replace "\|", "/" -replace "\s+", " ").Trim()
        $lines[$index] = "| $Id | $Status | $gateCell | $evidenceCell |"
        $updated = $true
        break
    }

    if (-not $updated) {
        throw "Manual gate row not found in test copy: $Id"
    }

    Set-Content -Encoding UTF8 -LiteralPath $Path -Value $lines
}

if (-not (Test-Path -LiteralPath $checklistPath)) {
    throw "Manual verification checklist not found: $checklistPath"
}

if (-not (Test-Path -LiteralPath $updaterPath)) {
    throw "Manual gate updater not found: $updaterPath"
}

if (-not (Test-Path -LiteralPath $verifierPath)) {
    throw "Manual checklist verifier not found: $verifierPath"
}

$tempPath = Join-Path ([System.IO.Path]::GetTempPath()) "manual-verification-v107-updater-test-$([Guid]::NewGuid()).md"
try {
    Copy-Item -LiteralPath $checklistPath -Destination $tempPath -Force

    $evidence = "2026-06-14 updater self-test | evidence with newline`nsecond line"
    & $updaterPath -ChecklistPath $tempPath -Id TEST-EXE-NOTES-VISUAL -Status PASS -Evidence $evidence *> $null
    & $verifierPath -ChecklistPath $tempPath *> $null

    $content = Get-Content -Encoding UTF8 -LiteralPath $tempPath -Raw
    Assert-ContainsText -Text $content -Needle "| TEST-EXE-NOTES-VISUAL | PASS |" -Message "Updater did not mark TEST-EXE-NOTES-VISUAL as PASS."
    Assert-ContainsText -Text $content -Needle "2026-06-14 updater self-test / evidence with newline second line" -Message "Updater did not sanitize and save evidence text."

    & $updaterPath -ChecklistPath $tempPath -Id TEST-EXE-NOTES-VISUAL -Status PENDING *> $null
    $content = Get-Content -Encoding UTF8 -LiteralPath $tempPath -Raw
    Assert-ContainsText -Text $content -Needle "| TEST-EXE-NOTES-VISUAL | PENDING | Run artifacts\portable\test.exe repeatedly and visually confirm the 1.0.8 release notes popup appears every run and looks correct. |  |" -Message "Updater did not clear evidence when returning a gate to PENDING."

    Assert-Fails -ExpectedMessage "Evidence is required when setting DISPLAY-SLEEP to PASS." -Action {
        & $updaterPath -ChecklistPath $tempPath -Id DISPLAY-SLEEP -Status PASS *> $null
    }

    Assert-Fails -ExpectedMessage "Evidence is required when setting DISPLAY-SLEEP to FAIL." -Action {
        & $updaterPath -ChecklistPath $tempPath -Id DISPLAY-SLEEP -Status FAIL *> $null
    }

    Set-ManualGateRowForTest -Path $tempPath -Id DISPLAY-SLEEP -Status FAIL
    Assert-Fails -ExpectedMessage "Manual gate DISPLAY-SLEEP is FAIL but has no evidence." -Action {
        & $verifierPath -ChecklistPath $tempPath *> $null
    }

    Set-ManualGateRowForTest -Path $tempPath -Id DISPLAY-SLEEP -Status PENDING -Evidence "stale evidence from a previous check"
    Assert-Fails -ExpectedMessage "Manual gate DISPLAY-SLEEP is PENDING but still has evidence." -Action {
        & $verifierPath -ChecklistPath $tempPath *> $null
    }

    Set-ManualGateRowForTest -Path $tempPath -Id DISPLAY-SLEEP -Status PENDING
    & $verifierPath -ChecklistPath $tempPath *> $null
}
finally {
    if (Test-Path -LiteralPath $tempPath) {
        Remove-Item -LiteralPath $tempPath -Force
    }
}

[pscustomobject]@{
    ChecklistPath = $checklistPath
    UpdaterPath = $updaterPath
    TempChecklistCleaned = -not (Test-Path -LiteralPath $tempPath)
    PassEvidenceRequired = $true
    FailEvidenceRequired = $true
    PendingClearsEvidence = $true
    VerifierRejectsFailWithoutEvidence = $true
    VerifierRejectsPendingWithEvidence = $true
} | Format-List

Write-Host "Manual gate updater verification passed."
