param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$oldInstallerVersions = @("1.0.4", "1.0.5", "1.0.6")
$autostartValueNames = @("Bloss", "BluetoothBatteryWidget")
$autostartRunKeyPath = "Software\Microsoft\Windows\CurrentVersion\Run"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $projectRoot "artifacts\manual-gate-evidence\v107-manual-gate-evidence.md"
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

function Convert-ToMarkdownCell {
    param([string]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ($Value -replace "\|", "/" -replace "`r?`n", " ").Trim()
}

function New-ArtifactRow {
    param(
        [string]$Name,
        [string]$RelativePath,
        [string]$ExpectedProductVersion = ""
    )

    $path = Join-Path $projectRoot $RelativePath
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    $item = if ($exists) { Get-Item -LiteralPath $path } else { $null }
    $productVersion = if ($exists) { Get-TrimmedVersion -Path $path } else { "" }
    $versionMatches = [string]::IsNullOrWhiteSpace($ExpectedProductVersion) -or $productVersion -eq $ExpectedProductVersion

    [pscustomobject]@{
        Name = $Name
        Exists = $exists
        ProductVersion = $productVersion
        ExpectedProductVersion = $ExpectedProductVersion
        VersionMatches = $versionMatches
        SHA256 = if ($exists) { Get-Sha256OrEmpty -Path $path } else { "" }
        LastWriteTime = if ($item) { $item.LastWriteTime } else { $null }
        Path = $path
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
            $candidate
        }
    }
}

function Get-ChecklistRows {
    $checklistPath = Join-Path $projectRoot "manual-verification-v107.md"
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

function Add-MarkdownTable {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string[]]$Headers,
        [object[]]$Rows,
        [scriptblock]$RowFormatter
    )

    $Lines.Add("| $($Headers -join ' | ') |")
    $Lines.Add("| $((@($Headers | ForEach-Object { '---' })) -join ' | ') |")
    foreach ($row in $Rows) {
        $cells = @(& $RowFormatter $row) | ForEach-Object { Convert-ToMarkdownCell $_ }
        $Lines.Add("| $($cells -join ' | ') |")
    }

    if ($Rows.Count -eq 0) {
        $Lines.Add("|  |  |  |")
    }
}

$parentRoot = Split-Path -Parent $projectRoot
$searchRoots = @($projectRoot, $parentRoot) |
    ForEach-Object { Resolve-ExistingDirectory -Path $_ } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Unique

$releaseArtifacts = @(
    New-ArtifactRow -Name "v1.0.7 installer" -RelativePath "release\installer\setup.exe" -ExpectedProductVersion "1.0.7"
    New-ArtifactRow -Name "v1.0.7 installer hash file" -RelativePath "release\installer\setup.exe.sha256"
    New-ArtifactRow -Name "portable test executable" -RelativePath "artifacts\portable\test.exe" -ExpectedProductVersion "1.0.7"
    New-ArtifactRow -Name "release notes preview" -RelativePath "artifacts\release-notes-previews\release-notes-window.png"
)

$oldInstallerCandidates = @(
    Find-OldInstallerCandidates -Roots $searchRoots |
        Sort-Object Path -Unique
)
$recommendedOldInstallers = @(Get-RecommendedOldInstallerCandidates -Candidates $oldInstallerCandidates)
$checklistRows = @(Get-ChecklistRows)
$runningProcesses = @(Get-Process | Where-Object { $_.ProcessName -in @("test", "Bloss") } | Select-Object Id, ProcessName, Path)
$currentAutostartValues = @(Get-CurrentUserAutostartValues)
$presentAutostartValues = @($currentAutostartValues | Where-Object { $_.Present })

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# v1.0.7 Manual Gate Evidence")
$lines.Add("")
$lines.Add("- GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')")
$lines.Add("- ProjectRoot: $projectRoot")
$lines.Add("- NonDestructive: true")
$lines.Add("- Note: This report only reads files, current-user startup values, and running processes. It does not install, uninstall, edit registry, or change Windows power settings.")
$lines.Add("")
$lines.Add("## Release Artifacts")
Add-MarkdownTable -Lines $lines -Headers @("Name", "Exists", "ProductVersion", "Expected", "VersionMatches", "SHA256", "Path") -Rows $releaseArtifacts -RowFormatter {
    param($row)
    @($row.Name, $row.Exists, $row.ProductVersion, $row.ExpectedProductVersion, $row.VersionMatches, $row.SHA256, $row.Path)
}
$lines.Add("")
$lines.Add("## Recommended Old Installers")
Add-MarkdownTable -Lines $lines -Headers @("Version", "SHA256", "LastWriteTime", "Path") -Rows $recommendedOldInstallers -RowFormatter {
    param($row)
    @($row.ProductVersion, $row.SHA256, $row.LastWriteTime, $row.Path)
}
$lines.Add("")
$lines.Add("## Manual Gate Status")
Add-MarkdownTable -Lines $lines -Headers @("ID", "Status", "Gate", "Evidence") -Rows $checklistRows -RowFormatter {
    param($row)
    @($row.Id, $row.Status, $row.Gate, $row.Evidence)
}
$lines.Add("")
$lines.Add("## Running Bloss/Test Processes")
if ($runningProcesses.Count -eq 0) {
    $lines.Add("- None")
}
else {
    Add-MarkdownTable -Lines $lines -Headers @("ProcessName", "Id", "Path") -Rows $runningProcesses -RowFormatter {
        param($row)
        @($row.ProcessName, $row.Id, $row.Path)
    }
}
$lines.Add("")
$lines.Add("## Current-User Bloss Startup Values")
Add-MarkdownTable -Lines $lines -Headers @("Root", "SubKey", "ValueName", "Present", "Data") -Rows $currentAutostartValues -RowFormatter {
    param($row)
    @($row.Root, $row.SubKey, $row.ValueName, $row.Present, $row.Data)
}
$lines.Add("")
$lines.Add("## Useful Commands")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add('powershell -ExecutionPolicy Bypass -File ".\scripts\show-v107-manual-gate-commands.ps1"')
$lines.Add('powershell -ExecutionPolicy Bypass -File ".\scripts\check-v107-manual-gate-prereqs.ps1" -RequireOldInstallers -RequireNoRunningProcesses -RequireNoCurrentAutostart')
$lines.Add('powershell -ExecutionPolicy Bypass -File ".\scripts\verify-v107-release-ready.ps1" -LiveReleaseNotes -DisplaySleepSnapshot -RequireManualGatePasses -RequireNoRunningBlossOrTest -RequireNoCurrentAutostart')
$lines.Add('```')

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFullPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
[System.IO.File]::WriteAllLines($outputFullPath, $lines, [System.Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    EvidencePath = $outputFullPath
    ReleaseArtifactCount = $releaseArtifacts.Count
    RecommendedOldInstallerCount = $recommendedOldInstallers.Count
    ManualGateCount = $checklistRows.Count
    RunningBlossOrTestProcesses = $runningProcesses.Count
    CurrentUserAutostartValues = $presentAutostartValues.Count
    NonDestructive = $true
} | Format-List

Write-Host "Manual gate evidence report exported."
