param(
    [int]$Tail = 10,
    [switch]$Raw
)

$ErrorActionPreference = "Stop"

$tracePath = Join-Path $env:APPDATA "Bloss\steam-triton-traces.jsonl"
if (-not (Test-Path -LiteralPath $tracePath)) {
    Write-Host "No Steam Triton trace file yet:"
    Write-Host $tracePath
    Write-Host "Run artifacts\portable\test.exe in the docked Steam Controller scenario first."
    exit 0
}

$lines = Get-Content -LiteralPath $tracePath -Tail ([Math]::Max(1, $Tail))
foreach ($line in $lines) {
    if ($Raw) {
        Write-Host $line
        continue
    }

    try {
        $entry = $line | ConvertFrom-Json
    }
    catch {
        Write-Host "Malformed trace line:"
        Write-Host $line
        continue
    }

    Write-Host ""
    Write-Host "Time: $($entry.ts)"
    Write-Host "Process: $($entry.processPath)"
    Write-Host "Build: $($entry.buildStamp)"
    if ($null -ne $entry.traceReason) {
        Write-Host "Trace reason: $($entry.traceReason)"
    }

    Write-Host "Raw candidates:"
    foreach ($item in @($entry.raw)) {
        Write-Host ("  {0} percent={1} raw={2} charging={3} complete={4} confidence={5} active={6} path={7} suspect={8} reason={9} model={10}" -f `
            $item.sourceKind, `
            $(if ($null -eq $item.percent) { "null" } else { $item.percent }), `
            $(if ($null -eq $item.rawMetric) { "null" } else { $item.rawMetric }), `
            $item.isCharging, `
            $(if ($null -eq $item.isChargeComplete) { "False" } else { $item.isChargeComplete }), `
            $item.confidence, `
            $item.activeSource, `
            $item.pathType, `
            $item.isBatterySuspect, `
            $item.reasonCode, `
            $item.modelKey)
    }

    Write-Host "Resolved:"
    foreach ($item in @($entry.resolved)) {
        Write-Host ("  {0} percent={1} raw={2} charging={3} complete={4} confidence={5} active={6} path={7} suspect={8} reason={9} model={10}" -f `
            $item.sourceKind, `
            $(if ($null -eq $item.percent) { "null" } else { $item.percent }), `
            $(if ($null -eq $item.rawMetric) { "null" } else { $item.rawMetric }), `
            $item.isCharging, `
            $(if ($null -eq $item.isChargeComplete) { "False" } else { $item.isChargeComplete }), `
            $item.confidence, `
            $item.activeSource, `
            $item.pathType, `
            $item.isBatterySuspect, `
            $item.reasonCode, `
            $item.modelKey)
    }
}
