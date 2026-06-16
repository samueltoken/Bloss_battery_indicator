param(
    [string]$SetupPath,
    [string]$ShaPath,
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

function Test-BinaryContainsAscii {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Needle
    )

    $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).Path)
    $text = [System.Text.Encoding]::ASCII.GetString($bytes)
    return $text.Contains($Needle)
}

function Get-RelativePathFromDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $directoryFullPath = [System.IO.Path]::GetFullPath($DirectoryPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $targetFullPath = [System.IO.Path]::GetFullPath($Path)
    $directoryPrefix = $directoryFullPath + [System.IO.Path]::DirectorySeparatorChar
    if ($targetFullPath.StartsWith($directoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $targetFullPath.Substring($directoryPrefix.Length)
    }

    return $targetFullPath
}

function Get-ReleaseAssetSetFailures {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SetupPath,

        [Parameter(Mandatory = $true)]
        [string]$ShaPath
    )

    $releaseInstallerDir = Join-Path $projectRoot "release\installer"
    if (-not (Test-Path -LiteralPath $releaseInstallerDir -PathType Container)) {
        return @("Release installer directory not found: $releaseInstallerDir")
    }

    $setupItem = Get-Item -LiteralPath $SetupPath
    $shaItem = Get-Item -LiteralPath $ShaPath
    $releaseInstallerItem = Get-Item -LiteralPath $releaseInstallerDir
    if ($setupItem.DirectoryName -ne $releaseInstallerItem.FullName -or $shaItem.DirectoryName -ne $releaseInstallerItem.FullName) {
        return @()
    }

    $expectedAssets = @("setup.exe", "setup.exe.sha256")
    $actualAssets = Get-ChildItem -LiteralPath $releaseInstallerDir -Force | ForEach-Object {
        Get-RelativePathFromDirectory -DirectoryPath $releaseInstallerDir -Path $_.FullName
    }

    $failures = @()
    foreach ($expectedAsset in $expectedAssets) {
        if ($expectedAsset -notin $actualAssets) {
            $failures += "Release installer asset is missing: $expectedAsset"
        }
    }

    foreach ($actualAsset in $actualAssets) {
        if ($actualAsset -notin $expectedAssets) {
            $failures += "Unexpected release installer asset: $actualAsset"
        }
    }

    return $failures
}

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = Get-CentralAppVersion
}

if ([string]::IsNullOrWhiteSpace($SetupPath)) {
    $SetupPath = Join-Path $projectRoot "release\installer\setup.exe"
}

if ([string]::IsNullOrWhiteSpace($ShaPath)) {
    $ShaPath = Join-Path $projectRoot "release\installer\setup.exe.sha256"
}

if (-not (Test-Path -LiteralPath $SetupPath)) {
    throw "Installer not found: $SetupPath"
}

if (-not (Test-Path -LiteralPath $ShaPath)) {
    throw "Installer SHA file not found: $ShaPath"
}

$setupItem = Get-Item -LiteralPath $SetupPath
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $SetupPath).Hash.ToUpperInvariant()
$declaredHash = ((Get-Content -LiteralPath $ShaPath -Raw).Trim() -split "\s+")[0].ToUpperInvariant()
$productVersion = $setupItem.VersionInfo.ProductVersion.Trim()
$fileVersion = $setupItem.VersionInfo.FileVersion.Trim()
$hasUpdateWrapperText = Test-BinaryContainsAscii -Path $SetupPath -Needle "Bloss update setup wrapper"
$hasEmbeddedInnerSetupName = Test-BinaryContainsAscii -Path $SetupPath -Needle "BlossSetupInner"
$hasEmbeddedPayloadName = Test-BinaryContainsAscii -Path $SetupPath -Needle "BlossPublishPayload"
$hasInnoText = Test-BinaryContainsAscii -Path $SetupPath -Needle "Inno Setup"
$signatureStatus = (Get-AuthenticodeSignature -LiteralPath $SetupPath).Status

$result = [pscustomobject]@{
    SetupPath = $setupItem.FullName
    Size = $setupItem.Length
    ExpectedVersion = $ExpectedVersion
    ProductVersion = $productVersion
    FileVersion = $fileVersion
    SHA256 = $actualHash
    DeclaredHash = $declaredHash
    HashMatches = ($actualHash -eq $declaredHash)
    HasUpdateWrapperText = $hasUpdateWrapperText
    HasEmbeddedInnerSetupName = $hasEmbeddedInnerSetupName
    HasEmbeddedPayloadName = $hasEmbeddedPayloadName
    HasInnoText = $hasInnoText
    SignatureStatus = $signatureStatus
}

$result | Format-List

$failures = @()
if ($productVersion -ne $ExpectedVersion) { $failures += "ProductVersion is '$productVersion', expected '$ExpectedVersion'." }
if ($actualHash -ne $declaredHash) { $failures += "setup.exe.sha256 does not match setup.exe." }
if ($hasUpdateWrapperText) { $failures += "Installer contains update wrapper marker text." }
if ($hasEmbeddedInnerSetupName) { $failures += "Installer contains BlossSetupInner marker." }
if ($hasEmbeddedPayloadName) { $failures += "Installer contains BlossPublishPayload marker." }
if (-not $hasInnoText) { $failures += "Installer does not contain Inno Setup marker text." }
$failures += Get-ReleaseAssetSetFailures -SetupPath $SetupPath -ShaPath $ShaPath

if ($failures.Count -gt 0) {
    throw ("Installer verification failed: " + ($failures -join " "))
}

Write-Host "Installer verification passed."
