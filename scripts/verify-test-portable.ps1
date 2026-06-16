param(
    [string]$TestExePath,
    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot

function Get-CentralAppVersion {
    $propsPath = Join-Path $projectRoot "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $propsPath)) {
        throw "Central version file not found: $propsPath"
    }

    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $version = $props.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Version was not found in $propsPath"
    }

    return $version.Trim()
}

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = Get-CentralAppVersion
}

if ([string]::IsNullOrWhiteSpace($TestExePath)) {
    $TestExePath = Join-Path $projectRoot "artifacts\portable\test.exe"
}

if (-not (Test-Path -LiteralPath $TestExePath)) {
    throw "Portable test executable not found: $TestExePath"
}

$testExeItem = Get-Item -LiteralPath $TestExePath
$testExeFileName = [System.IO.Path]::GetFileName($testExeItem.FullName)
$productVersion = $testExeItem.VersionInfo.ProductVersion.Trim()
$fileVersion = $testExeItem.VersionInfo.FileVersion.Trim()
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $TestExePath).Hash.ToUpperInvariant()

$result = [pscustomobject]@{
    TestExePath = $testExeItem.FullName
    Size = $testExeItem.Length
    LastWriteTime = $testExeItem.LastWriteTime
    ExpectedVersion = $ExpectedVersion
    ProductVersion = $productVersion
    FileVersion = $fileVersion
    SHA256 = $hash
}

$result | Format-List

$failures = @()
if ($testExeItem.Length -le 0) { $failures += "test.exe is empty." }
if (-not $testExeFileName.Equals("test.exe", [System.StringComparison]::OrdinalIgnoreCase)) {
    $failures += "Portable test executable must be named test.exe so release notes are forced every run."
}
if ($productVersion -ne $ExpectedVersion) { $failures += "ProductVersion is '$productVersion', expected '$ExpectedVersion'." }
if ($fileVersion -ne $ExpectedVersion) { $failures += "FileVersion is '$fileVersion', expected '$ExpectedVersion'." }

if ($failures.Count -gt 0) {
    throw ("Portable test executable verification failed: " + ($failures -join " "))
}

Write-Host "Portable test executable verification passed."
