param(
    [int]$TimeoutSeconds = 90,
    [int]$PollMilliseconds = 2000,
    [switch]$RequireActivity,
    [switch]$RequirePopup,
    [switch]$RequireAutoAlert
)

$logPath = Join-Path $env:APPDATA "Bloss\guide-button-events.log"
$powerIdleLogPath = Join-Path $env:APPDATA "Bloss\power-idle-debug.log"

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class BlossIdleProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    public static int GetIdleSeconds()
    {
        var info = new LastInputInfo();
        info.cbSize = (uint)Marshal.SizeOf(typeof(LastInputInfo));
        if (!GetLastInputInfo(ref info))
        {
            return -1;
        }

        var elapsed = unchecked(GetTickCount() - info.dwTime);
        return (int)(elapsed / 1000);
    }
}
"@

function Convert-GuideButtonEventLine {
    param([string]$Line)

    $parts = $Line -split "`t", 6
    if ($parts.Count -lt 6) {
        return $null
    }

    try {
        $time = [DateTimeOffset]$parts[0]
    }
    catch {
        return $null
    }

    [pscustomobject]@{
        Time = $time
        Event = $parts[1]
        Device = $parts[2]
        Address = $parts[3]
        Name = $parts[4]
        Message = $parts[5]
    }
}

function Get-RecentGuideEvents {
    param(
        [DateTimeOffset]$Since,
        [int]$Tail = 240
    )

    if (-not (Test-Path -LiteralPath $logPath)) {
        return @()
    }

    @(Get-Content -LiteralPath $logPath -Tail $Tail | ForEach-Object {
        Convert-GuideButtonEventLine $_
    } | Where-Object {
        $_ -and $_.Time -ge $Since
    })
}

function Convert-PowerIdleDebugLine {
    param([string]$Line)

    $parts = $Line -split "`t"
    if ($parts.Count -lt 2) {
        return $null
    }

    try {
        $time = [DateTimeOffset]$parts[0]
    }
    catch {
        return $null
    }

    $values = @{}
    foreach ($part in $parts | Select-Object -Skip 1) {
        $name, $value = $part -split "=", 2
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            $values[$name] = $value
        }
    }

    [pscustomobject]@{
        Time = $time
        Mode = if ($values.ContainsKey("mode")) { $values["mode"] } else { "unknown" }
        SystemIdle = if ($values.ContainsKey("systemIdle")) { $values["systemIdle"] } else { "?" }
        LocalIdle = if ($values.ContainsKey("localIdle")) { $values["localIdle"] } else { "?" }
        GamepadIdle = if ($values.ContainsKey("gamepadIdle")) { $values["gamepadIdle"] } else { "?" }
        GuideRunning = if ($values.ContainsKey("guideRunning")) { $values["guideRunning"] } else { "?" }
        RawInputRegistered = if ($values.ContainsKey("rawInputRegistered")) { $values["rawInputRegistered"] } else { "?" }
        XInputRunning = if ($values.ContainsKey("xInputRunning")) { $values["xInputRunning"] } else { "?" }
        RawInputMode = if ($values.ContainsKey("rawInputMode")) { $values["rawInputMode"] } else { "?" }
        XInputMode = if ($values.ContainsKey("xInputMode")) { $values["xInputMode"] } else { "?" }
        NormalMonitorRemaining = if ($values.ContainsKey("normalMonitorRemaining")) { $values["normalMonitorRemaining"] } else { "?" }
    }
}

function Get-LatestPowerIdleState {
    if (-not (Test-Path -LiteralPath $powerIdleLogPath)) {
        return $null
    }

    Get-Content -LiteralPath $powerIdleLogPath -Tail 40 |
        ForEach-Object { Convert-PowerIdleDebugLine $_ } |
        Where-Object { $_ } |
        Select-Object -Last 1
}

$activityEventNames = @(
    "xinput_button_input",
    "xinput_stick_input",
    "hid_button_input",
    "pressed",
    "custom_trigger_capture_candidate",
    "custom_trigger_toast_shown"
)

$popupEventNames = @(
    "popup_shown",
    "popup_missing_device",
    "tray_fallback",
    "custom_trigger_toast_shown",
    "automatic_battery_toast_shown"
)

$autoAlertEventNames = @(
    "automatic_battery_toast_shown"
)

$startedAt = [DateTimeOffset]::Now
$deadline = $startedAt.AddSeconds([Math]::Max(1, $TimeoutSeconds))
$pollDelay = [Math]::Max(250, $PollMilliseconds)
$sawActivity = $false
$sawPopup = $false
$sawAutoAlert = $false

Write-Host "Watching Bloss gamepad idle activity for $TimeoutSeconds seconds."
Write-Host "Do not touch the mouse or keyboard after this starts."
Write-Host "Use only the controller, then press the configured guide/notification button near the end."
Write-Host "Log: $logPath"
Write-Host "Power idle log: $powerIdleLogPath"
Write-Host ""

while ([DateTimeOffset]::Now -lt $deadline) {
    $events = Get-RecentGuideEvents -Since $startedAt
    $activityEvents = @($events | Where-Object { $_.Event -in $activityEventNames })
    $popupEvents = @($events | Where-Object { $_.Event -in $popupEventNames })
    $autoAlertEvents = @($events | Where-Object { $_.Event -in $autoAlertEventNames })
    $sawActivity = $sawActivity -or $activityEvents.Count -gt 0
    $sawPopup = $sawPopup -or $popupEvents.Count -gt 0
    $sawAutoAlert = $sawAutoAlert -or $autoAlertEvents.Count -gt 0
    $latest = $events | Select-Object -Last 1
    $latestText = if ($latest) { "$($latest.Event)/$($latest.Device)" } else { "none" }
    $powerIdle = Get-LatestPowerIdleState
    $powerMode = if ($powerIdle) { $powerIdle.Mode } else { "unknown" }
    $monitorState = if ($powerIdle) {
        "guide=$($powerIdle.GuideRunning),raw=$($powerIdle.RawInputMode),xinput=$($powerIdle.XInputMode),normalLeft=$($powerIdle.NormalMonitorRemaining)"
    } else {
        "guide=?,raw=?,xinput=?,normalLeft=?"
    }
    $idleSeconds = [BlossIdleProbe]::GetIdleSeconds()
    $remaining = [Math]::Max(0, [int]($deadline - [DateTimeOffset]::Now).TotalSeconds)

    Write-Host ("{0} idle={1}s mode={2} monitors={3} activity={4} popup={5} auto={6} latest={7} remaining={8}s" -f `
        (Get-Date -Format "HH:mm:ss"),
        $idleSeconds,
        $powerMode,
        $monitorState,
        $activityEvents.Count,
        $popupEvents.Count,
        $autoAlertEvents.Count,
        $latestText,
        $remaining)

    Start-Sleep -Milliseconds $pollDelay
}

$events = Get-RecentGuideEvents -Since $startedAt
$activityEvents = @($events | Where-Object { $_.Event -in $activityEventNames })
$popupEvents = @($events | Where-Object { $_.Event -in $popupEventNames })
$autoAlertEvents = @($events | Where-Object { $_.Event -in $autoAlertEventNames })
$sawActivity = $sawActivity -or $activityEvents.Count -gt 0
$sawPopup = $sawPopup -or $popupEvents.Count -gt 0
$sawAutoAlert = $sawAutoAlert -or $autoAlertEvents.Count -gt 0

Write-Host ""
Write-Host "Latest power idle state:"
$latestPowerIdle = Get-LatestPowerIdleState
if ($latestPowerIdle) {
    $latestPowerIdle | Format-List Time, Mode, SystemIdle, LocalIdle, GamepadIdle, GuideRunning, RawInputRegistered, RawInputMode, XInputRunning, XInputMode, NormalMonitorRemaining
} else {
    Write-Host "No power idle debug log found yet."
}

Write-Host ""
Write-Host "Recent matching events:"
$events |
    Where-Object { $_.Event -in ($activityEventNames + $popupEventNames + $autoAlertEventNames) } |
    Select-Object Time, Event, Device, Message |
    Format-Table -AutoSize -Wrap

if ($RequireActivity -and -not $sawActivity) {
    Write-Host "No gamepad activity event was recorded."
    exit 1
}

if ($RequirePopup -and -not $sawPopup) {
    Write-Host "No guide/notification popup event was recorded."
    exit 1
}

if ($RequireAutoAlert -and -not $sawAutoAlert) {
    Write-Host "No automatic battery alert event was recorded."
    exit 1
}

Write-Host "Gamepad idle activity watch finished."
