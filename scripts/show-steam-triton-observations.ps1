param(
    [int]$Tail = 20
)

$ErrorActionPreference = "Stop"

$observationPath = Join-Path $env:APPDATA "Bloss\battery-observations.jsonl"
if (-not (Test-Path -LiteralPath $observationPath)) {
    Write-Host "No battery observation file yet:"
    Write-Host $observationPath
    exit 0
}

$items = New-Object System.Collections.Generic.List[object]
foreach ($line in Get-Content -LiteralPath $observationPath) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    try {
        $item = $line | ConvertFrom-Json
    }
    catch {
        continue
    }

    $modelKey = [string]$item.modelKey
    if ($item.sourceKind -eq 8 -or $modelKey.IndexOf("STEAM_TRITON_PUCK", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $items.Add($item)
    }
}

if ($items.Count -eq 0) {
    Write-Host "No Steam Triton battery observations found:"
    Write-Host $observationPath
    exit 0
}

$ordered = @($items | Sort-Object { [DateTimeOffset]::Parse($_.observedAt) })
$latest = $ordered | Select-Object -Last 1
$normal = @($ordered | Where-Object { $_.derivedPercent -gt 0 -and $_.derivedPercent -lt 100 })
$latestNormal = $normal | Select-Object -Last 1
$suspiciousFull = @($ordered | Where-Object { $_.derivedPercent -eq 100 })

Write-Host "Steam Triton observation summary"
Write-Host "File: $observationPath"
Write-Host "Total Steam observations: $($ordered.Count)"
Write-Host "100% observations: $($suspiciousFull.Count)"

if ($null -ne $latest) {
    Write-Host ("Latest: {0} percent={1} raw={2} charging={3} complete={4} reason={5} address={6}" -f `
        $latest.observedAt, `
        $latest.derivedPercent, `
        $latest.rawMetric, `
        $(if ($null -eq $latest.isCharging) { "False" } else { $latest.isCharging }), `
        $(if ($null -eq $latest.isChargeComplete) { "False" } else { $latest.isChargeComplete }), `
        $latest.reasonCode, `
        $latest.address)
}

if ($null -ne $latestNormal) {
    $age = [DateTimeOffset]::Now - [DateTimeOffset]::Parse($latestNormal.observedAt)
    Write-Host ("Latest non-100: {0} percent={1} raw={2} charging={3} complete={4} reason={5} ageMinutes={6:N1}" -f `
        $latestNormal.observedAt, `
        $latestNormal.derivedPercent, `
        $latestNormal.rawMetric, `
        $(if ($null -eq $latestNormal.isCharging) { "False" } else { $latestNormal.isCharging }), `
        $(if ($null -eq $latestNormal.isChargeComplete) { "False" } else { $latestNormal.isChargeComplete }), `
        $latestNormal.reasonCode, `
        $age.TotalMinutes)
}
else {
    Write-Host "Latest non-100: none"
}

Write-Host ""
Write-Host "Recent Steam observations:"
$ordered |
    Select-Object -Last ([Math]::Max(1, $Tail)) |
    ForEach-Object {
        Write-Host ("  {0} percent={1} raw={2} charging={3} complete={4} reason={5} address={6}" -f `
            $_.observedAt, `
            $_.derivedPercent, `
            $_.rawMetric, `
            $(if ($null -eq $_.isCharging) { "False" } else { $_.isCharging }), `
            $(if ($null -eq $_.isChargeComplete) { "False" } else { $_.isChargeComplete }), `
            $_.reasonCode, `
            $_.address)
    }
