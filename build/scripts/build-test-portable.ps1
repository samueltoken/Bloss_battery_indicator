param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$KeepPublish
)

$ErrorActionPreference = "Stop"

$portableScript = Join-Path $PSScriptRoot "build-portable.ps1"
if (-not (Test-Path $portableScript)) {
    throw "Missing script: $portableScript"
}

$args = @(
    "-Configuration", $Configuration,
    "-Runtime", $Runtime
)

if ($KeepPublish) {
    $args += "-KeepPublish"
}

& $portableScript @args

if ($LASTEXITCODE -ne 0) {
    throw "Portable build wrapper failed with exit code $LASTEXITCODE."
}
