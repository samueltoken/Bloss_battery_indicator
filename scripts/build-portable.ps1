param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$ZipOutput,
    [switch]$KeepPublish
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $projectRoot "BluetoothBatteryWidget.App\BluetoothBatteryWidget.App.csproj"
$portableRoot = Join-Path $projectRoot "artifacts\portable"
$publishDir = Join-Path $portableRoot "publish"
$finalExe = Join-Path $portableRoot "test.exe"
$zipPath = Join-Path $portableRoot "test-portable-$Runtime.zip"
$optionalBlueprintAssets = @(
    "controller-guide-blueprint.png",
    "controller-guide-blueprint.jpg",
    "controller-guide-blueprint.jpeg",
    "battery-guide-trigger-blueprint.png",
    "battery-guide-trigger-blueprint.jpg",
    "battery-guide-trigger-blueprint.jpeg"
)

function Copy-OptionalBlueprintAssets {
    foreach ($assetName in $optionalBlueprintAssets) {
        $publishedAsset = Join-Path $publishDir $assetName
        $publishedAssetsAsset = Join-Path (Join-Path $publishDir "Assets") $assetName
        $sourceAsset = Join-Path (Join-Path $projectRoot "BluetoothBatteryWidget.App\Assets") $assetName
        $destinationAsset = Join-Path $portableRoot $assetName

        if (Test-Path $publishedAsset) {
            Copy-Item $publishedAsset $destinationAsset -Force
            continue
        }

        if (Test-Path $publishedAssetsAsset) {
            Copy-Item $publishedAssetsAsset $destinationAsset -Force
            continue
        }

        if (Test-Path $sourceAsset) {
            Copy-Item $sourceAsset $destinationAsset -Force
        }
    }
}

if (Test-Path $portableRoot) {
    Remove-Item $portableRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null

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
Copy-OptionalBlueprintAssets

if (-not $KeepPublish -and (Test-Path $publishDir)) {
    Remove-Item $publishDir -Recurse -Force
}

if ($ZipOutput) {
    $portableFiles = @($finalExe)
    foreach ($assetName in $optionalBlueprintAssets) {
        $assetPath = Join-Path $portableRoot $assetName
        if (Test-Path $assetPath) {
            $portableFiles += $assetPath
        }
    }

    Compress-Archive -Path $portableFiles -DestinationPath $zipPath -Force
    Write-Host "Portable zip created:"
    Write-Host $zipPath
}

Write-Host "Portable single-file executable created:"
Write-Host $finalExe
