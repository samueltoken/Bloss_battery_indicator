param()

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot

function Get-Text {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $path = Join-Path $projectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file not found: $path"
    }

    return Get-Content -Encoding UTF8 -LiteralPath $path -Raw
}

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Needle,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Text.Contains($Needle)) {
        throw $Message
    }
}

function Assert-DoesNotContain {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Needle,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if ($Text.Contains($Needle)) {
        throw $Message
    }
}

$animator = Get-Text "BluetoothBatteryWidget.App\WindowPopInAnimator.cs"
$releaseNotesCode = Get-Text "BluetoothBatteryWidget.App\ReleaseNotesWindow.xaml.cs"
$iconOverrideCode = Get-Text "BluetoothBatteryWidget.App\IconOverrideWindow.xaml.cs"
$iconImageAdjustCode = Get-Text "BluetoothBatteryWidget.App\IconImageAdjustWindow.xaml.cs"
$batteryAlertCode = Get-Text "BluetoothBatteryWidget.App\BatteryAlertThresholdsWindow.xaml.cs"
$batteryAlertXaml = Get-Text "BluetoothBatteryWidget.App\BatteryAlertThresholdsWindow.xaml"
$guideCaptureCode = Get-Text "BluetoothBatteryWidget.App\BatteryGuideTriggerCaptureWindow.xaml.cs"
$guideCaptureXaml = Get-Text "BluetoothBatteryWidget.App\BatteryGuideTriggerCaptureWindow.xaml"
$previewTests = Get-Text "BluetoothBatteryWidget.Tests\BatteryToastPreviewArtifactTests.cs"
$mainWindowCode = Get-Text "BluetoothBatteryWidget.App\MainWindow.xaml.cs"

Assert-Contains $animator "GenieStartScaleX = 0.42d" "Genie start X scale changed."
Assert-Contains $animator "GenieStartScaleY = 0.24d" "Genie start Y scale changed."
Assert-Contains $animator "CenterPopStartScaleX = 0.94d" "Centered window start X scale changed."
Assert-Contains $animator "CenterPopStartScaleY = 0.86d" "Centered window start Y scale changed."
Assert-Contains $animator "SettleDuration = TimeSpan.FromMilliseconds(700)" "Secondary-window settle duration changed."
Assert-Contains $animator "BeginClose(" "Secondary-window reverse close animation is missing."
Assert-Contains $animator "BeginCloseCentered(" "Centered secondary-window reverse close animation is missing."
Assert-Contains $animator "CloseMotionDuration = TimeSpan.FromMilliseconds(300)" "Secondary-window reverse close duration changed."
Assert-Contains $animator "QuinticEase" "Secondary-window animation must keep smooth ease-out motion."
Assert-Contains $animator "FillBehavior = FillBehavior.Stop" "Secondary-window animation must leave stable final base values."
Assert-Contains $animator "HandoffBehavior.SnapshotAndReplace" "Secondary-window animation must replace old animation clocks."
Assert-Contains $animator "scale.ScaleX = 1d;" "Secondary-window final scale X reset is missing."
Assert-Contains $animator "scale.ScaleY = 1d;" "Secondary-window final scale Y reset is missing."
Assert-Contains $animator "skew.AngleX = 0d;" "Secondary-window final skew X reset is missing."
Assert-Contains $animator "skew.AngleY = 0d;" "Secondary-window final skew Y reset is missing."
Assert-Contains $animator "translate.X = 0d;" "Secondary-window final translate X reset is missing."
Assert-Contains $animator "translate.Y = 0d;" "Secondary-window final translate Y reset is missing."

foreach ($token in @(
    "DoubleAnimationUsingKeyFrames",
    "SplineDoubleKeyFrame",
    "EasingDoubleKeyFrame",
    "BackEase",
    "BounceEase",
    "ElasticEase",
    "AutoReverse",
    "RepeatBehavior",
    "1.025d")) {
    Assert-DoesNotContain $animator $token "Secondary-window animator contains a stutter/overshoot-prone token: $token"
}

Assert-DoesNotContain $releaseNotesCode "AttachCentered" "Release notes popup must appear immediately, not with secondary-window pop-in."
Assert-Contains $iconOverrideCode "WindowPopInAnimator.AttachCentered(this)" "Manual icon override window is missing centered pop-in."
Assert-Contains $iconOverrideCode "CloseWithPopOutAsCancel" "Manual icon override window is missing reverse close."
Assert-Contains $iconImageAdjustCode "WindowPopInAnimator.AttachCentered(this)" "Icon image adjustment window is missing centered pop-in."
Assert-Contains $batteryAlertCode "WindowPopInAnimator.Begin(" "Battery alert thresholds window is missing button-origin pop-in."
Assert-Contains $batteryAlertCode "CloseWithPopOut" "Battery alert thresholds window is missing reverse close."
Assert-Contains $batteryAlertCode "WindowPopInAnimator.BeginClose(" "Battery alert thresholds window does not use the reverse close animation."
Assert-Contains $guideCaptureCode "WindowPopInAnimator.Begin(" "Guide trigger capture window is missing button-origin pop-in."
Assert-Contains $guideCaptureCode "CloseWithPopOut" "Guide trigger capture window is missing reverse close."
Assert-Contains $guideCaptureCode "WindowPopInAnimator.BeginClose(" "Guide trigger capture window does not use the reverse close animation."
Assert-Contains $mainWindowCode "_batteryAlertThresholdsWindow.CloseWithPopOut();" "Settings battery alert button does not toggle-close the open window."
Assert-Contains $mainWindowCode "CancelBatteryGuideTriggerCapture(closeWindow: true, animateClose: true);" "Settings guide trigger button does not toggle-close the open window."
Assert-Contains $mainWindowCode "_iconOverrideWindow.CloseWithPopOutAsCancel();" "Settings manual icon button does not toggle-close the open window."
Assert-Contains $previewTests "SecondaryWindows_PopInAnimationSettlesToStableFinalValues" "Secondary-window live settle test is missing."
Assert-Contains $previewTests "window.Show();" "Secondary-window live settle test does not exercise the Loaded path."
Assert-Contains $previewTests "WaitForDispatcher(TimeSpan.FromMilliseconds(1000))" "Secondary-window live settle test does not wait through the button-origin pop-in animation."

foreach ($entry in @(
    @{ Name = "Battery alert thresholds"; Xaml = $batteryAlertXaml },
    @{ Name = "Guide trigger capture"; Xaml = $guideCaptureXaml })) {
    Assert-Contains $entry.Xaml "Loaded=`"Window_Loaded`"" "$($entry.Name) window does not start pop-in on Loaded."
    Assert-Contains $entry.Xaml "x:Name=`"WindowSurface`"" "$($entry.Name) window surface is missing."
    Assert-Contains $entry.Xaml "x:Name=`"WindowSurfaceScale`"" "$($entry.Name) scale transform is missing."
    Assert-Contains $entry.Xaml "x:Name=`"WindowSurfaceSkew`"" "$($entry.Name) skew transform is missing."
    Assert-Contains $entry.Xaml "x:Name=`"WindowSurfaceTranslate`"" "$($entry.Name) translate transform is missing."
    Assert-Contains $entry.Xaml "RenderTransformOrigin=`"0.5,0.5`"" "$($entry.Name) transform origin is missing."
}

[pscustomobject]@{
    SecondaryWindowAnimationChecksPassed = $true
    CheckedWindows = "BatteryAlertThresholdsWindow, BatteryGuideTriggerCaptureWindow, IconOverrideWindow, IconImageAdjustWindow"
    ReleaseNotesImmediate = $true
} | Format-List

Write-Host "Secondary window animation verification passed."
