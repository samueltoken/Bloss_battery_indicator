param(
    [string[]]$SearchRoot,
    [switch]$RequireOldInstallers,
    [switch]$RequireNoRunningProcesses,
    [switch]$RequireNoCurrentAutostart
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$oldInstallerVersions = @("1.0.4", "1.0.5", "1.0.6")
$autostartValueNames = @("Bloss", "BluetoothBatteryWidget")
$autostartRunKeyPath = "Software\Microsoft\Windows\CurrentVersion\Run"
$failures = New-Object System.Collections.Generic.List[string]

if ($null -eq $SearchRoot -or $SearchRoot.Count -eq 0) {
    $parentRoot = Split-Path -Parent $projectRoot
    $SearchRoot = @($projectRoot, $parentRoot)
}

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

function New-RequiredFileResult {
    param(
        [string]$Name,
        [string]$RelativePath,
        [string]$ExpectedProductVersion = ""
    )

    $path = if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        $RelativePath
    }
    else {
        Join-Path $projectRoot $RelativePath
    }
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    $productVersion = if ($exists) { Get-TrimmedVersion -Path $path } else { "" }
    $sha256 = if ($exists) { Get-Sha256OrEmpty -Path $path } else { "" }
    $versionMatches = [string]::IsNullOrWhiteSpace($ExpectedProductVersion) -or $productVersion -eq $ExpectedProductVersion

    [pscustomobject]@{
        Name = $Name
        Exists = $exists
        ExpectedProductVersion = $ExpectedProductVersion
        ProductVersion = $productVersion
        SHA256 = $sha256
        VersionMatches = $versionMatches
        Path = $path
    }
}

function Find-OldInstallerCandidates {
    param([string[]]$Roots)

    foreach ($root in $Roots) {
        Get-ChildItem -LiteralPath $root -Recurse -File -Filter *.exe -ErrorAction SilentlyContinue |
            ForEach-Object {
                if ($_.Name -match "(?i)(setup|installer)") {
                    $productVersion = $_.VersionInfo.ProductVersion
                    if ($null -ne $productVersion) {
                        $trimmedVersion = $productVersion.Trim()
                        if ($oldInstallerVersions -contains $trimmedVersion) {
                            [pscustomobject]@{
                                ProductVersion = $trimmedVersion
                                Length = $_.Length
                                LastWriteTime = $_.LastWriteTime
                                SHA256 = Get-Sha256OrEmpty -Path $_.FullName
                                Path = $_.FullName
                            }
                        }
                    }
                }
            }
    }
}

function Get-RecommendedOldInstallerCandidates {
    param([object[]]$Candidates)

    foreach ($version in $oldInstallerVersions) {
        $candidate = @(
            $Candidates |
                Where-Object { $_.ProductVersion -eq $version } |
                Sort-Object `
                    @{ Expression = { if ($_.Path -like "*\artifacts\manual-gate-old-builds\*") { 0 } else { 1 } } },
                    @{ Expression = "LastWriteTime"; Descending = $true },
                    @{ Expression = "Path"; Descending = $false }
        ) | Select-Object -First 1

        if ($candidate) {
            [pscustomobject]@{
                ProductVersion = $candidate.ProductVersion
                Length = $candidate.Length
                LastWriteTime = $candidate.LastWriteTime
                SHA256 = $candidate.SHA256
                Path = $candidate.Path
            }
        }
    }
}

function Get-CurrentUserAutostartValues {
    $runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($autostartRunKeyPath, $false)
    try {
        foreach ($valueName in $autostartValueNames) {
            $data = if ($null -ne $runKey) { $runKey.GetValue($valueName, $null) } else { $null }
            [pscustomobject]@{
                Root = "HKEY_CURRENT_USER"
                SubKey = $autostartRunKeyPath
                ValueName = $valueName
                Present = $null -ne $data
                Data = if ($null -ne $data) { [string]$data } else { "" }
            }
        }
    }
    finally {
        if ($null -ne $runKey) {
            $runKey.Dispose()
        }
    }
}

Write-Host "This script only inspects files, current-user startup values, and running processes. It does not install, uninstall, edit registry, or change Windows power settings."
Write-Host ""

$searchRootsFull = @(
    $SearchRoot |
        ForEach-Object { Resolve-ExistingDirectory -Path $_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
)

if ($searchRootsFull.Count -eq 0) {
    throw "No valid search root was found."
}

$manualChecklistPath = if (-not [string]::IsNullOrWhiteSpace($env:BLOSS_MANUAL_CHECKLIST_PATH)) {
    [System.IO.Path]::GetFullPath($env:BLOSS_MANUAL_CHECKLIST_PATH)
}
else {
    "manual-verification-v107.md"
}
$manualChecklistName = Split-Path -Leaf $manualChecklistPath
$manualScriptVersion = if ($manualChecklistName -match '^manual-verification-(v\d+)\.md$') {
    $Matches[1]
}
else {
    "v107"
}

$requiredFiles = @(
    New-RequiredFileResult -Name "v1.0.9 installer" -RelativePath "release\installer\setup.exe" -ExpectedProductVersion "1.0.9"
    New-RequiredFileResult -Name "v1.0.9 installer hash" -RelativePath "release\installer\setup.exe.sha256"
    New-RequiredFileResult -Name "portable visual test executable" -RelativePath "artifacts\portable\test.exe" -ExpectedProductVersion "1.0.9"
    New-RequiredFileResult -Name "manual verification checklist" -RelativePath $manualChecklistPath
    New-RequiredFileResult -Name "manual gate command helper" -RelativePath "scripts\show-$manualScriptVersion-manual-gate-commands.ps1"
    New-RequiredFileResult -Name "manual gate updater" -RelativePath "scripts\set-$manualScriptVersion-manual-gate.ps1"
    New-RequiredFileResult -Name "display sleep readiness check" -RelativePath "scripts\check-display-sleep-readiness.ps1"
    New-RequiredFileResult -Name "autostart cleanup check" -RelativePath "scripts\check-autostart-cleanup.ps1"
    New-RequiredFileResult -Name "Steam Controller guide event helper" -RelativePath "scripts\show-guide-button-events.ps1"
    New-RequiredFileResult -Name "gamepad idle activity helper" -RelativePath "scripts\show-gamepad-idle-activity.ps1"
)

foreach ($requiredFile in $requiredFiles) {
    if (-not $requiredFile.Exists) {
        $failures.Add("Missing required manual-gate prerequisite file: $($requiredFile.Path)")
    }

    if ($requiredFile.Exists -and -not $requiredFile.VersionMatches) {
        $failures.Add("ProductVersion mismatch for $($requiredFile.Name): expected '$($requiredFile.ExpectedProductVersion)', found '$($requiredFile.ProductVersion)'.")
    }
}

$oldInstallerCandidates = @(
    Find-OldInstallerCandidates -Roots $searchRootsFull |
        Sort-Object Path -Unique
)
$recommendedOldInstallerCandidates = @(Get-RecommendedOldInstallerCandidates -Candidates $oldInstallerCandidates)
$foundOldVersions = @($oldInstallerCandidates | Select-Object -ExpandProperty ProductVersion -Unique | Sort-Object)
$missingOldVersions = @($oldInstallerVersions | Where-Object { $foundOldVersions -notcontains $_ })
$runningProcesses = @(Get-Process | Where-Object { $_.ProcessName -in @("test", "Bloss") } | Select-Object Id, ProcessName, Path)
$currentAutostartValues = @(Get-CurrentUserAutostartValues)
$presentAutostartValues = @($currentAutostartValues | Where-Object { $_.Present })

if ($RequireOldInstallers -and $missingOldVersions.Count -gt 0) {
    $failures.Add("Missing old installer versions required for update gates: $($missingOldVersions -join ', ').")
}

if ($RequireNoRunningProcesses -and $runningProcesses.Count -gt 0) {
    $runningSummary = @($runningProcesses | ForEach-Object { "$($_.ProcessName)($($_.Id)) $($_.Path)" }) -join "; "
    $failures.Add("Running Bloss/test processes found. Close them before manual install/update/uninstall gates: $runningSummary")
}

if ($RequireNoCurrentAutostart -and $presentAutostartValues.Count -gt 0) {
    $autostartSummary = @($presentAutostartValues | ForEach-Object { "$($_.Root)\$($_.SubKey)\$($_.ValueName)=$($_.Data)" }) -join "; "
    $failures.Add("Current-user Bloss autostart values found. This can relaunch the wrong build after restart and contaminate manual install/update/uninstall gates. Inspect with scripts\check-autostart-cleanup.ps1. If this developer PC intentionally needs cleanup before the manual gates, run scripts\check-autostart-cleanup.ps1 -Delete, then rerun this prerequisite check. Do not use -Delete as release proof; the uninstall gate must still pass after a real uninstall. Values: $autostartSummary")
}

$summary = [pscustomobject]@{
    ProjectRoot = $projectRoot
    SearchRoots = $searchRootsFull -join "; "
    RequiredFilesPresent = @($requiredFiles | Where-Object { $_.Exists }).Count
    RequiredFilesTotal = $requiredFiles.Count
    OldInstallerVersionsFound = if ($foundOldVersions.Count -gt 0) { $foundOldVersions -join ", " } else { "" }
    MissingOldInstallerVersions = if ($missingOldVersions.Count -gt 0) { $missingOldVersions -join ", " } else { "" }
    RunningBlossOrTestProcesses = $runningProcesses.Count
    CurrentUserAutostartValues = $presentAutostartValues.Count
    RequireOldInstallers = $RequireOldInstallers.IsPresent
    RequireNoRunningProcesses = $RequireNoRunningProcesses.IsPresent
    RequireNoCurrentAutostart = $RequireNoCurrentAutostart.IsPresent
    NonDestructive = $true
}

$summary | Format-List

Write-Host "== Required local files =="
$requiredFiles |
    Sort-Object Name |
    Format-List Name, Exists, ExpectedProductVersion, ProductVersion, SHA256, VersionMatches, Path

Write-Host ""
Write-Host "== Recommended old installers for manual update gates =="
if ($recommendedOldInstallerCandidates.Count -gt 0) {
    $recommendedOldInstallerCandidates |
        Sort-Object ProductVersion |
        Format-List ProductVersion, Length, LastWriteTime, SHA256, Path
}
else {
    Write-Host "No recommended old installer was found."
}

Write-Host ""
Write-Host "== Old installer candidates for update gates =="
if ($oldInstallerCandidates.Count -gt 0) {
    $oldInstallerCandidates |
        Sort-Object ProductVersion, Path |
        Format-List ProductVersion, Length, LastWriteTime, SHA256, Path
}
else {
    Write-Host "No old installer candidates found under the selected search roots."
}

if ($missingOldVersions.Count -gt 0) {
    Write-Host "WARN: Missing old installer versions: $($missingOldVersions -join ', ')."
}

Write-Host ""
Write-Host "== Running Bloss/test processes =="
if ($runningProcesses.Count -gt 0) {
    $runningProcesses | Format-List Id, ProcessName, Path
}
else {
    Write-Host "No running Bloss or test.exe process was found."
}

Write-Host ""
Write-Host "== Current-user Bloss startup values =="
$currentAutostartValues | Sort-Object ValueName | Format-List Root, SubKey, ValueName, Present, Data
if ($presentAutostartValues.Count -gt 0) {
    Write-Host "WARN: Current-user Bloss autostart values are present."
    Write-Host "WARN: Do not start install/update/uninstall manual gates until these values are intentionally cleared or explicitly recorded."
    Write-Host "WARN: Use scripts\check-autostart-cleanup.ps1 to inspect, and use scripts\check-autostart-cleanup.ps1 -Delete only when cleaning this developer PC before a manual gate."
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "FAIL: $failure"
    }

    throw "Manual gate prerequisite check has $($failures.Count) issue(s)."
}

Write-Host ""
Write-Host "Manual gate prerequisite check completed."
