param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersion = "1.0.1"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$appProject = Join-Path $projectRoot "BluetoothBatteryWidget.App\BluetoothBatteryWidget.App.csproj"
$publishDir = Join-Path $projectRoot "artifacts\staging\installer-publish"
$outputDir = Join-Path $projectRoot "release\installer"
$issPath = Join-Path $projectRoot "build\installer\BluetoothBatteryWidget.iss"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $appProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    /p:UseAppHost=true `
    -o $publishDir

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $fallback = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $fallback) {
        $iscc = $fallback
    }
}

if (-not $iscc) {
    $fallback = "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    if (Test-Path $fallback) {
        $iscc = $fallback
    }
}

if (-not $iscc) {
    $fallback = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    if (Test-Path $fallback) {
        $iscc = $fallback
    }
}

if (-not $iscc) {
    throw "ISCC.exe not found. Install Inno Setup 6 or add ISCC.exe to PATH."
}

& $iscc `
    "/DAppVersion=$AppVersion" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$outputDir" `
    $issPath

if ($LASTEXITCODE -ne 0) {
    throw "ISCC compile failed with exit code $LASTEXITCODE."
}

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Write-Host "Installer package created in:"
Write-Host $outputDir
