param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "UPDATE-104",
        "UPDATE-105",
        "UPDATE-106-NOTES",
        "CLEAN-INSTALL-NOTES",
        "TEST-EXE-NOTES-VISUAL",
        "DISPLAY-SLEEP",
        "STEAM-CONTROLLER",
        "UNINSTALL-AUTOSTART",
        "SETTINGS-SECONDARY-WINDOWS")]
    [string]$Id,

    [Parameter(Mandatory = $true)]
    [ValidateSet("PENDING", "PASS", "FAIL")]
    [string]$Status,

    [string]$Evidence,

    [string]$ChecklistPath
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ChecklistPath)) {
    $ChecklistPath = Join-Path $projectRoot "manual-verification-v107.md"
}

$checklistPath = [System.IO.Path]::GetFullPath($ChecklistPath)
$status = $Status.ToUpperInvariant()

function Convert-ToManualGateCell([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    return (($Value -replace "[\r\n]+", " ") -replace "\|", "/" -replace "\s+", " ").Trim()
}

if (-not (Test-Path -LiteralPath $checklistPath)) {
    throw "Manual verification checklist not found: $checklistPath"
}

$evidenceCell = Convert-ToManualGateCell $Evidence
if ($status -in @("PASS", "FAIL") -and [string]::IsNullOrWhiteSpace($evidenceCell)) {
    throw "Evidence is required when setting $Id to $status."
}

if ($status -eq "PENDING") {
    $evidenceCell = ""
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.AddRange([string[]](Get-Content -Encoding UTF8 -LiteralPath $checklistPath))
$updated = $false

for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -notmatch "^\|\s*$([regex]::Escape($Id))\s*\|") {
        continue
    }

    $cells = $lines[$index].Trim().Trim("|").Split("|") | ForEach-Object { $_.Trim() }
    if ($cells.Count -lt 4) {
        throw "Manual gate row has too few columns: $Id"
    }

    $gateCell = Convert-ToManualGateCell $cells[2]
    $lines[$index] = "| $Id | $status | $gateCell | $evidenceCell |"
    $updated = $true
    break
}

if (-not $updated) {
    throw "Manual gate row not found: $Id"
}

Set-Content -Encoding UTF8 -LiteralPath $checklistPath -Value $lines

& (Join-Path $PSScriptRoot "verify-v107-manual-checklist.ps1") -ChecklistPath $checklistPath

Write-Host "Manual gate $Id set to $status."
if ($status -eq "PENDING") {
    Write-Host "Evidence was cleared because the gate is pending."
}
