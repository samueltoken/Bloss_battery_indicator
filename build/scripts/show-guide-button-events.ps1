param(
    [int]$Tail = 30,
    [switch]$PressedOnly,
    [switch]$Wait,
    [int]$TimeoutSeconds = 30,
    [ValidateSet("Any", "DualSense", "SteamController")]
    [string]$Device = "Any",
    [switch]$RequirePopup,
    [switch]$Diagnostics,
    [switch]$SteamPowerOffCheck
)

$logPath = Join-Path $env:APPDATA "Bloss\guide-button-events.log"

if (-not (Test-Path -LiteralPath $logPath)) {
    Write-Host "No guide button event log found yet: $logPath"
    Write-Host "Run test.exe, press the DualSense PS button or Steam Controller Steam button, then run this script again."
    exit 0
}

function Convert-GuideButtonEventLine {
    param([string]$Line)

    $parts = $Line -split "`t", 6
    if ($parts.Count -lt 6) {
        return $null
    }

    [pscustomobject]@{
        Time = $parts[0]
        Event = $parts[1]
        Device = $parts[2]
        Address = $parts[3]
        Name = $parts[4]
        Message = $parts[5]
    }
}

function Get-GuideButtonEvents {
    param([int]$Count)

    Get-Content -LiteralPath $logPath -Tail $Count | ForEach-Object {
        Convert-GuideButtonEventLine $_
    } | Where-Object {
        $isSteamPowerOffSignal =
            $_ -and
            $_.Device -eq "SteamController" -and
            $_.Event -in @(
                "pressed",
                "popup_shown",
                "duplicate_press_suppressed",
                "raw_long_press_secondary_blocked",
                "raw_hid_long_press_suppressed",
                "secondary_press_suppressed",
                "secondary_fallback_pending",
                "secondary_fallback_accepted"
            )

        $_ -and
        (-not $SteamPowerOffCheck -or $isSteamPowerOffSignal) -and
        ($SteamPowerOffCheck -or $Device -eq "Any" -or $_.Device -eq $Device) -and
        (-not $PressedOnly -or $SteamPowerOffCheck -or $_.Event -in @("pressed", "popup_shown", "popup_missing_device", "tray_fallback")) -and
        (-not $RequirePopup -or $SteamPowerOffCheck -or $_.Event -in @("popup_shown", "popup_missing_device", "tray_fallback")) -and
        ($Diagnostics -or $_.Event -notin @("service_started", "discovery_no_endpoints"))
    }
}

if ($Wait) {
    $startedAt = [DateTimeOffset]::Now
    $deadline = $startedAt.AddSeconds([Math]::Max(1, $TimeoutSeconds))
    $target = if ($SteamPowerOffCheck) { "Steam Controller power-off" } elseif ($Device -eq "Any") { "guide-button" } else { $Device }
    $goal = if ($SteamPowerOffCheck) { "popup/suppression event" } elseif ($RequirePopup) { "popup result" } else { "press/popup event" }
    Write-Host "Waiting for $target $goal for $TimeoutSeconds seconds..."

    while ([DateTimeOffset]::Now -lt $deadline) {
        $events = Get-GuideButtonEvents -Count 80 | Where-Object {
            ($SteamPowerOffCheck -or $_.Event -in @("pressed", "popup_shown", "popup_missing_device", "tray_fallback")) -and
            ([DateTimeOffset]$_.Time) -ge $startedAt
        }

        if ($events) {
            $events | Format-Table -AutoSize
            exit 0
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "No matching guide-button event was recorded."
    Write-Host "Make sure test.exe is running, then press the requested controller guide button."
    Write-Host ""
    Write-Host "Recent diagnostic events:"
    Get-GuideButtonEvents -Count 120 | Where-Object {
        ([DateTimeOffset]$_.Time) -ge $startedAt.AddSeconds(-5)
    } | Format-Table -AutoSize
    exit 1
}

Get-GuideButtonEvents -Count $Tail | Format-Table -AutoSize
