param(
    [ValidateSet("1.0.4", "1.0.5", "1.0.6")]
    [string]$Version = "1.0.4",

    [string]$SourceRepo,
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceRepo)) {
    $SourceRepo = Join-Path (Split-Path -Parent $projectRoot) "bloss_battery_indicator_release_day1"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot "artifacts\manual-gate-old-builds"
}

$sourceRepoFull = [System.IO.Path]::GetFullPath($SourceRepo)
$outputRootFull = [System.IO.Path]::GetFullPath($OutputRoot)
$tagName = "v$Version"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$target = Join-Path $outputRootFull "$tagName-$stamp"
$archivePath = Join-Path $outputRootFull "$tagName-$stamp.zip"

if (-not (Test-Path -LiteralPath $sourceRepoFull -PathType Container)) {
    throw "Source repo not found: $sourceRepoFull"
}

if (-not (Test-Path -LiteralPath (Join-Path $sourceRepoFull ".git") -PathType Container)) {
    throw "Source repo is not a git repository: $sourceRepoFull"
}

$tagExists = (& git -C $sourceRepoFull tag --list $tagName) -contains $tagName
if (-not $tagExists) {
    throw "Git tag not found in source repo: $tagName"
}

New-Item -ItemType Directory -Path $outputRootFull -Force | Out-Null

& git -C $sourceRepoFull archive --format=zip -o $archivePath $tagName
if ($LASTEXITCODE -ne 0) {
    throw "git archive failed with exit code $LASTEXITCODE."
}

Expand-Archive -LiteralPath $archivePath -DestinationPath $target -Force

$buildScript = Join-Path $target "build\scripts\build-installer.ps1"
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
    throw "Old installer build script not found: $buildScript"
}

& powershell -ExecutionPolicy Bypass -File $buildScript -AppVersion $Version
if ($LASTEXITCODE -ne 0) {
    throw "Old installer build failed with exit code $LASTEXITCODE."
}

$setupPath = Join-Path $target "release\installer\setup.exe"
if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
    throw "Old installer setup.exe not found: $setupPath"
}

$setupItem = Get-Item -LiteralPath $setupPath
$productVersion = $setupItem.VersionInfo.ProductVersion
if ($null -eq $productVersion -or $productVersion.Trim() -ne $Version) {
    throw "Old installer ProductVersion mismatch. Expected '$Version', found '$productVersion'."
}

$hash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToUpperInvariant()
$result = [pscustomobject]@{
    Version = $Version
    Tag = $tagName
    SourceRepo = $sourceRepoFull
    ExtractedSource = $target
    SetupPath = $setupPath
    ProductVersion = $productVersion.Trim()
    SHA256 = $hash
    Length = $setupItem.Length
    NonDestructive = $true
}

$result | Format-List
Write-Host "Old installer prerequisite build completed."
