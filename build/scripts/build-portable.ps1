param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$ZipOutput,
    [switch]$KeepPublish
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$appProject = Join-Path $projectRoot "BluetoothBatteryWidget.App\BluetoothBatteryWidget.App.csproj"
$portableRoot = Join-Path $projectRoot "release\portable"
$publishDir = Join-Path $projectRoot "artifacts\staging\portable-publish"
$finalExe = Join-Path $portableRoot "Bloss.exe"
$zipPath = Join-Path $portableRoot "Bloss-portable-$Runtime.zip"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null

if (Test-Path $finalExe) {
    Remove-Item $finalExe -Force
}

if ($ZipOutput -and (Test-Path $zipPath)) {
    Remove-Item $zipPath -Force
}

dotnet publish $appProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

$publishedExe = Join-Path $publishDir "Bloss.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Single-file publish failed. Executable not found: $publishedExe"
}

Copy-Item $publishedExe $finalExe -Force

if (-not $KeepPublish -and (Test-Path $publishDir)) {
    Remove-Item $publishDir -Recurse -Force
}

if ($ZipOutput) {
    Compress-Archive -Path $finalExe -DestinationPath $zipPath -Force
    Write-Host "Portable zip created:"
    Write-Host $zipPath
}

Write-Host "Portable single-file executable created:"
Write-Host $finalExe
