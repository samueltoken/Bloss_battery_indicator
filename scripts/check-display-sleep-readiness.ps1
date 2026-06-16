param(
    [string]$TestExePath,
    [switch]$NoFail
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot

function Invoke-PowerCfg {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & powercfg @Arguments 2>&1 | ForEach-Object { $_.ToString() }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    [pscustomobject]@{
        Arguments = $Arguments -join " "
        ExitCode = $LASTEXITCODE
        Output = @($output)
    }
}

function Write-Section([string]$Title) {
    Write-Host ""
    Write-Host "== $Title =="
}

function Get-PowerLineStatusText {
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        return [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus.ToString()
    }
    catch {
        return "Unknown"
    }
}

function Convert-HexSecondsToText {
    param([int]$Seconds)

    if ($Seconds -eq 0) {
        return "Never"
    }

    if (($Seconds % 60) -eq 0) {
        return "$Seconds seconds ($($Seconds / 60) minutes)"
    }

    return "$Seconds seconds"
}

function Get-IdleTimeoutCandidates {
    param([string[]]$PowerCfgOutput)

    $hexValues = foreach ($line in $PowerCfgOutput) {
        foreach ($match in [regex]::Matches($line, "0x[0-9a-fA-F]+")) {
            $match.Value
        }
    }

    # In `powercfg /query SCHEME_CURRENT <subgroup> <setting>`, the last two hex values
    # are normally the current AC and DC idle timeout values.
    $tail = @($hexValues | Select-Object -Last 2)
    for ($i = 0; $i -lt $tail.Count; $i++) {
        [pscustomobject]@{
            Slot = if ($i -eq 0 -and $tail.Count -eq 2) { "AC" } elseif ($i -eq 1 -and $tail.Count -eq 2) { "DC" } else { "Unknown" }
            Hex = $tail[$i]
            Seconds = [Convert]::ToInt32($tail[$i], 16)
        }
    }
}

function Test-OutputHasNonNoneRequest {
    param([string[]]$PowerCfgOutput)

    $text = ($PowerCfgOutput -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $false
    }

    return $text -match ":\s*\S" -and
        $text -notmatch "(?im)^\s*(NONE|없음|없습니다)\s*$"
}

function Test-PowerCfgAdministratorRequired {
    param([string[]]$PowerCfgOutput)

    $text = $PowerCfgOutput -join "`n"
    return $text -match "(?i)administrator|elevated|admin rights|관리자\s*권한"
}

if ([string]::IsNullOrWhiteSpace($TestExePath)) {
    $TestExePath = Join-Path $projectRoot "artifacts\portable\test.exe"
}

$failures = [System.Collections.Generic.List[string]]::new()

Write-Section "Bloss display sleep readiness"
Write-Host "Project root: $projectRoot"
Write-Host "Portable test executable: $([System.IO.Path]::GetFullPath($TestExePath))"
if (Test-Path -LiteralPath $TestExePath) {
    $item = Get-Item -LiteralPath $TestExePath
    Write-Host "test.exe ProductVersion: $($item.VersionInfo.ProductVersion)"
    Write-Host "test.exe LastWriteTime: $($item.LastWriteTime)"
}
else {
    [void]$failures.Add("Portable test executable not found: $TestExePath")
}

Write-Section "Active power scheme"
$activeScheme = Invoke-PowerCfg -Arguments @("/getactivescheme")
$activeScheme.Output | ForEach-Object { Write-Host $_ }
if ($activeScheme.ExitCode -ne 0) {
    [void]$failures.Add("powercfg /getactivescheme failed.")
}
Write-Host "Current power line status: $(Get-PowerLineStatusText)"

Write-Section "Display idle timeout"
$displayTimeout = Invoke-PowerCfg -Arguments @("/query", "SCHEME_CURRENT", "SUB_VIDEO", "VIDEOIDLE")
$displayTimeout.Output | ForEach-Object { Write-Host $_ }
if ($displayTimeout.ExitCode -ne 0) {
    [void]$failures.Add("powercfg /query SCHEME_CURRENT SUB_VIDEO VIDEOIDLE failed.")
}
else {
    $candidates = @(Get-IdleTimeoutCandidates -PowerCfgOutput $displayTimeout.Output)
    if ($candidates.Count -gt 0) {
        Write-Host ""
        Write-Host "Parsed display timeout candidates:"
        $candidates | ForEach-Object {
            Write-Host "- $($_.Slot): $($_.Hex) = $(Convert-HexSecondsToText $_.Seconds)"
        }

        if (-not ($candidates | Where-Object { $_.Seconds -eq 60 })) {
            Write-Host "NOTE: No parsed display timeout candidate is exactly 60 seconds."
            Write-Host "For the manual bug check, set Windows display-off timeout to 1 minute before waiting."
        }
    }
    else {
        Write-Host "NOTE: Could not parse display timeout values from powercfg output."
    }
}

Write-Section "System sleep idle timeout"
$sleepTimeout = Invoke-PowerCfg -Arguments @("/query", "SCHEME_CURRENT", "SUB_SLEEP", "STANDBYIDLE")
$sleepTimeout.Output | ForEach-Object { Write-Host $_ }
if ($sleepTimeout.ExitCode -ne 0) {
    [void]$failures.Add("powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE failed.")
}
else {
    $sleepCandidates = @(Get-IdleTimeoutCandidates -PowerCfgOutput $sleepTimeout.Output)
    if ($sleepCandidates.Count -gt 0) {
        Write-Host ""
        Write-Host "Parsed system sleep timeout candidates:"
        $sleepCandidates | ForEach-Object {
            Write-Host "- $($_.Slot): $($_.Hex) = $(Convert-HexSecondsToText $_.Seconds)"
        }
    }
    else {
        Write-Host "NOTE: Could not parse sleep timeout values from powercfg output."
    }
}

Write-Section "Power requests"
$requests = Invoke-PowerCfg -Arguments @("/requests")
$requests.Output | ForEach-Object { Write-Host $_ }
if ($requests.ExitCode -ne 0) {
    if (Test-PowerCfgAdministratorRequired -PowerCfgOutput $requests.Output) {
        Write-Host "NOTE: powercfg /requests requires PowerShell as Administrator on this PC."
    }
    else {
        Write-Host "NOTE: powercfg /requests could not be read from this shell. On some Windows setups, this requires PowerShell as Administrator."
    }

    Write-Host "This is a limitation of the snapshot, not evidence that Bloss is keeping the display awake."
}
elseif (Test-OutputHasNonNoneRequest -PowerCfgOutput $requests.Output) {
    Write-Host "NOTE: powercfg reported at least one request-like line. Check whether it is unrelated to Bloss before testing."
}

Write-Section "Wake timers"
$wakeTimers = Invoke-PowerCfg -Arguments @("/waketimers")
$wakeTimers.Output | ForEach-Object { Write-Host $_ }
if ($wakeTimers.ExitCode -ne 0) {
    if (Test-PowerCfgAdministratorRequired -PowerCfgOutput $wakeTimers.Output) {
        Write-Host "NOTE: powercfg /waketimers requires PowerShell as Administrator on this PC."
    }
    else {
        Write-Host "NOTE: powercfg /waketimers could not be read from this shell. On some Windows setups, this requires PowerShell as Administrator."
    }

    Write-Host "This is a limitation of the snapshot, not evidence that Bloss is scheduling wake timers."
}

Write-Section "Manual display-off and sleep test checklist"
Write-Host "1. Set Windows display-off timeout to 1 minute, or set system sleep to a short test value."
Write-Host "2. Make sure you changed the value for the current power mode shown above, then run Bloss normally."
Write-Host "3. Do not move the mouse or press keys, and wait at least 90 seconds."
Write-Host "4. Confirm the monitor turns off or Windows enters sleep according to the selected Windows setting."
Write-Host "5. Wake the screen, put Bloss in tray, wait again, and confirm the same behavior."
Write-Host "6. If it does not turn off, run this script again and compare powercfg /requests output."
Write-Host "7. If powercfg /requests or /waketimers says administrator rights are required, rerun only the snapshot from PowerShell as Administrator."

if ($failures.Count -gt 0) {
    if ($NoFail) {
        Write-Host ""
        Write-Host "Display sleep readiness completed with non-fatal issues:"
        $failures | ForEach-Object { Write-Host "- $_" }
        exit 0
    }

    throw ("Display sleep readiness failed: " + ($failures -join " "))
}

Write-Host ""
Write-Host "Display sleep readiness snapshot completed."
