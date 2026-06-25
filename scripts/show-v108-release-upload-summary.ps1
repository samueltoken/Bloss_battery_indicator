param(
    [switch]$RequireReady,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$expectedVersion = "1.0.9"
$checklistPath = if (-not [string]::IsNullOrWhiteSpace($env:BLOSS_MANUAL_CHECKLIST_PATH)) {
    [System.IO.Path]::GetFullPath($env:BLOSS_MANUAL_CHECKLIST_PATH)
}
else {
    Join-Path $projectRoot "manual-verification-v108.md"
}
$setupPath = Join-Path $projectRoot "release\installer\setup.exe"
$setupHashPath = Join-Path $projectRoot "release\installer\setup.exe.sha256"
$testExePath = Join-Path $projectRoot "artifacts\portable\test.exe"
$gitSafetySummaryPath = Join-Path $projectRoot "artifacts\manual-gate-evidence\git-publish-safety-upload-summary.json"
$autostartRunKeyPath = "Software\Microsoft\Windows\CurrentVersion\Run"
$autostartValueNames = @("Bloss", "BluetoothBatteryWidget")

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

function Get-ProductVersionOrEmpty {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ""
    }

    $version = (Get-Item -LiteralPath $Path).VersionInfo.ProductVersion
    if ($null -eq $version) {
        return ""
    }

    return $version.Trim()
}

function Get-DeclaredSetupHash {
    if (-not (Test-Path -LiteralPath $setupHashPath -PathType Leaf)) {
        return ""
    }

    $raw = (Get-Content -Encoding UTF8 -LiteralPath $setupHashPath -Raw).Trim()
    $match = [regex]::Match($raw, "[A-Fa-f0-9]{64}")
    if (-not $match.Success) {
        return ""
    }

    return $match.Value.ToUpperInvariant()
}

function Get-ManualGateRows {
    if (-not (Test-Path -LiteralPath $checklistPath -PathType Leaf)) {
        throw "Manual verification checklist not found: $checklistPath"
    }

    foreach ($line in Get-Content -Encoding UTF8 -LiteralPath $checklistPath) {
        if ($line -notmatch '^\|\s*(?<id>[A-Z0-9-]+)\s*\|\s*(?<status>PENDING|PASS|FAIL)\s*\|\s*(?<gate>.*?)\s*\|\s*(?<evidence>.*?)\s*\|$') {
            continue
        }

        [pscustomobject]@{
            Id = $Matches.id
            Status = $Matches.status
            Gate = $Matches.gate.Trim()
            Evidence = $Matches.evidence.Trim()
        }
    }
}

function Get-CurrentUserAutostartValues {
    $runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($autostartRunKeyPath, $false)
    try {
        foreach ($valueName in $autostartValueNames) {
            $data = if ($null -ne $runKey) { $runKey.GetValue($valueName, $null) } else { $null }
            if ($null -ne $data) {
                [pscustomobject]@{
                    ValueName = $valueName
                    Data = [string]$data
                }
            }
        }
    }
    finally {
        if ($null -ne $runKey) {
            $runKey.Dispose()
        }
    }
}

function Test-PublishRelevantPath {
    param([string]$Path)

    return $Path -match '^(BluetoothBatteryWidget\.App|BluetoothBatteryWidget\.Core|BluetoothBatteryWidget\.Tests|scripts)/' -or
        $Path -match '^(\.gitignore|Directory\.Build\.props|package\.json|README(\..+)?\.md|installer/|build/installer/)'
}

function Get-UntrackedPublishCandidates {
    $untracked = @(& git ls-files --others --exclude-standard 2>&1 | ForEach-Object { $_.ToString() })
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files --others --exclude-standard failed: $($untracked -join [Environment]::NewLine)"
    }

    foreach ($path in $untracked) {
        if (Test-PublishRelevantPath -Path $path) {
            $path
        }
    }
}

function Get-PendingPublishChanges {
    $statusLines = @(& git status --porcelain=v1 --untracked-files=no 2>&1 | ForEach-Object { $_.ToString() })
    if ($LASTEXITCODE -ne 0) {
        throw "git status --porcelain=v1 --untracked-files=no failed: $($statusLines -join [Environment]::NewLine)"
    }

    foreach ($line in $statusLines) {
        if ($line.Length -lt 4) {
            continue
        }

        $status = $line.Substring(0, 2)
        $path = $line.Substring(3)
        if ($path -match ' -> ') {
            $path = ($path -split ' -> ')[-1]
        }

        $path = $path.Trim('"')
        if (Test-PublishRelevantPath -Path $path) {
            [pscustomobject]@{
                Status = $status
                Path = $path
            }
        }
    }
}

Push-Location $projectRoot
try {
    if ($Json) {
        $null = @(& (Join-Path $PSScriptRoot "verify-git-publish-safety.ps1") -SummaryPath $gitSafetySummaryPath *>&1)
    }
    else {
        & (Join-Path $PSScriptRoot "verify-git-publish-safety.ps1") -SummaryPath $gitSafetySummaryPath
    }

    $gitSafety = Get-Content -Encoding UTF8 -LiteralPath $gitSafetySummaryPath -Raw | ConvertFrom-Json

    $manualRows = @(Get-ManualGateRows)
    $manualPending = @($manualRows | Where-Object { $_.Status -eq "PENDING" })
    $manualFailed = @($manualRows | Where-Object { $_.Status -eq "FAIL" })

    $setupHash = Get-Sha256OrEmpty -Path $setupPath
    $declaredSetupHash = Get-DeclaredSetupHash
    $setupProductVersion = Get-ProductVersionOrEmpty -Path $setupPath
    $testProductVersion = Get-ProductVersionOrEmpty -Path $testExePath
    $runningProcesses = @(Get-Process -Name Bloss,test -ErrorAction SilentlyContinue)
    $autostartValues = @(Get-CurrentUserAutostartValues)
    $untrackedPublishCandidates = @(Get-UntrackedPublishCandidates)
    $pendingPublishChanges = @(Get-PendingPublishChanges)

    $readyChecks = [ordered]@{
        SetupExists = Test-Path -LiteralPath $setupPath -PathType Leaf
        SetupVersionMatches = $setupProductVersion -eq $expectedVersion
        SetupHashFileExists = Test-Path -LiteralPath $setupHashPath -PathType Leaf
        SetupHashMatches = -not [string]::IsNullOrWhiteSpace($setupHash) -and $setupHash -eq $declaredSetupHash
        TestExeExists = Test-Path -LiteralPath $testExePath -PathType Leaf
        TestExeVersionMatches = $testProductVersion -eq $expectedVersion
        ManualGatesAllPassed = $manualRows.Count -gt 0 -and $manualPending.Count -eq 0 -and $manualFailed.Count -eq 0
        GitPublishSafetyPassed = [int]$gitSafety.ForbiddenTrackedFiles -eq 0 -and [int]$gitSafety.MissingIgnoreRules -eq 0 -and [int]$gitSafety.UnexpectedEmails -eq 0
        NoUntrackedPublishCandidates = $untrackedPublishCandidates.Count -eq 0
        NoPendingGitPublishChanges = $pendingPublishChanges.Count -eq 0
        NoRunningBlossOrTest = $runningProcesses.Count -eq 0
        NoCurrentUserAutostart = $autostartValues.Count -eq 0
    }

    $blockedReasons = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $readyChecks.GetEnumerator()) {
        if (-not [bool]$entry.Value) {
            [void]$blockedReasons.Add($entry.Key)
        }
    }

    $summary = [pscustomobject]@{
        ProjectRoot = $projectRoot
        ExpectedVersion = $expectedVersion
        ReadyForUpload = $blockedReasons.Count -eq 0
        BlockedReasons = @($blockedReasons)
        SetupPath = $setupPath
        SetupProductVersion = $setupProductVersion
        SetupSHA256 = $setupHash
        DeclaredSetupSHA256 = $declaredSetupHash
        TestExePath = $testExePath
        TestExeProductVersion = $testProductVersion
        ManualChecklistPath = $checklistPath
        ManualGateTotal = $manualRows.Count
        ManualGatePassed = @($manualRows | Where-Object { $_.Status -eq "PASS" }).Count
        ManualGatePending = $manualPending.Count
        ManualGateFailed = $manualFailed.Count
        GitForbiddenTrackedFiles = [int]$gitSafety.ForbiddenTrackedFiles
        GitUnexpectedEmails = [int]$gitSafety.UnexpectedEmails
        UntrackedPublishCandidates = $untrackedPublishCandidates.Count
        UntrackedPublishCandidatePaths = @($untrackedPublishCandidates)
        PendingGitPublishChanges = $pendingPublishChanges.Count
        PendingGitPublishChangePaths = @($pendingPublishChanges | ForEach-Object { "$($_.Status) $($_.Path)" })
        RunningBlossOrTestProcesses = $runningProcesses.Count
        CurrentUserAutostartValues = $autostartValues.Count
        NonDestructive = $true
    }

    if ($Json) {
        $summary | ConvertTo-Json -Depth 5
    }
    else {
        Write-Host ""
        Write-Host "== v$expectedVersion Release Upload Summary =="
        $summary | Format-List

        if ($manualPending.Count -gt 0 -or $manualFailed.Count -gt 0) {
            Write-Host "Manual gates not ready:"
            @($manualPending + $manualFailed) | Sort-Object Id | Format-Table -AutoSize Id, Status, Gate
        }

        if ($runningProcesses.Count -gt 0) {
            Write-Host "Running Bloss/test processes:"
            $runningProcesses | Format-Table -AutoSize ProcessName, Id, Path
        }

        if ($untrackedPublishCandidates.Count -gt 0) {
            Write-Host "Untracked files that must be reviewed before push:"
            $untrackedPublishCandidates | ForEach-Object { Write-Host "- $_" }
        }

        if ($pendingPublishChanges.Count -gt 0) {
            Write-Host "Pending git files that must be committed before upload:"
            $pendingPublishChanges | ForEach-Object { Write-Host "- $($_.Status) $($_.Path)" }
        }

        if ($autostartValues.Count -gt 0) {
            Write-Host "Current-user startup values:"
            $autostartValues | Format-Table -AutoSize ValueName, Data
        }
    }

    if ($RequireReady -and $blockedReasons.Count -gt 0) {
        throw "Release upload is blocked: $($blockedReasons -join ', ')"
    }
}
finally {
    Pop-Location
}
