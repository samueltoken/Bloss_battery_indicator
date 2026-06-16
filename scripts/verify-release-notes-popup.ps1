param(
    [string]$TestExePath,
    [string]$ExpectedVersion,
    [switch]$Live,
    [int]$LaunchCount = 2,
    [int]$TimeoutSeconds = 15
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

function Add-Failure([System.Collections.Generic.List[string]]$Failures, [string]$Message) {
    [void]$Failures.Add($Message)
}

function Test-FileContains([string]$Path, [string]$Needle) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -Encoding UTF8 -LiteralPath $Path -Raw
    return $content.Contains($Needle)
}

function Test-PngTransparentCorners([string]$Path) {
    Add-Type -AssemblyName System.Drawing

    $bitmap = [System.Drawing.Bitmap]::new([string]$Path)
    try {
        $points = @(
            @(0, 0),
            @(1, 1),
            @(5, 5),
            @(($bitmap.Width - 1), 0),
            @(0, ($bitmap.Height - 1)),
            @(($bitmap.Width - 1), ($bitmap.Height - 1))
        )

        foreach ($point in $points) {
            $pixel = $bitmap.GetPixel([int]$point[0], [int]$point[1])
            if ($pixel.A -ne 0) {
                return $false
            }
        }

        return $true
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-ExistingTestProcesses([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Get-Process -Name "test" -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.Path -eq $fullPath
        }
        catch {
            $false
        }
    }
}

function Wait-ReleaseNotesWindow([int]$ProcessId, [string]$WindowNamePrefix, [int]$TimeoutSeconds) {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    do {
        try {
            $candidates = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $processCondition)
        }
        catch [System.Runtime.InteropServices.COMException] {
            Start-Sleep -Milliseconds 250
            continue
        }

        foreach ($candidate in $candidates) {
            try {
                $name = $candidate.Current.Name
            }
            catch [System.Runtime.InteropServices.COMException] {
                continue
            }

            if (-not [string]::IsNullOrWhiteSpace($name) -and $name.StartsWith($WindowNamePrefix, [System.StringComparison]::Ordinal)) {
                return $candidate
            }
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    return $null
}

function Close-ReleaseNotesWindow($Window) {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes

    try {
        $pattern = $Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
        $pattern.Close()
        return
    }
    catch {
    }

    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $button = $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    if ($null -ne $button) {
        $invoke = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
    }
}

function Test-WindowCenteredOnAnyScreen($Window, [double]$TolerancePixels = 96d) {
    Add-Type -AssemblyName System.Windows.Forms

    $bounds = $Window.Current.BoundingRectangle
    if ($bounds.IsEmpty -or $bounds.Width -le 0 -or $bounds.Height -le 0) {
        return [pscustomobject]@{
            IsCentered = $false
            Distance = [double]::PositiveInfinity
            Screen = ""
        }
    }

    $windowCenterX = $bounds.Left + ($bounds.Width / 2d)
    $windowCenterY = $bounds.Top + ($bounds.Height / 2d)
    $bestDistance = [double]::PositiveInfinity
    $bestScreen = ""

    foreach ($screen in [System.Windows.Forms.Screen]::AllScreens) {
        $area = $screen.WorkingArea
        $screenCenterX = $area.Left + ($area.Width / 2d)
        $screenCenterY = $area.Top + ($area.Height / 2d)
        $distance = [Math]::Sqrt(
            [Math]::Pow($windowCenterX - $screenCenterX, 2d) +
            [Math]::Pow($windowCenterY - $screenCenterY, 2d))

        if ($distance -lt $bestDistance) {
            $bestDistance = $distance
            $bestScreen = $screen.DeviceName
        }
    }

    return [pscustomobject]@{
        IsCentered = $bestDistance -le $TolerancePixels
        Distance = $bestDistance
        Screen = $bestScreen
    }
}

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = Get-CentralAppVersion
}

if ([string]::IsNullOrWhiteSpace($TestExePath)) {
    $TestExePath = Join-Path $projectRoot "artifacts\portable\test.exe"
}

$releaseXamlPath = Join-Path $projectRoot "BluetoothBatteryWidget.App\ReleaseNotesWindow.xaml"
$releaseCodePath = Join-Path $projectRoot "BluetoothBatteryWidget.App\ReleaseNotesWindow.xaml.cs"
$mainWindowPath = Join-Path $projectRoot "BluetoothBatteryWidget.App\MainWindow.xaml.cs"
$languageCatalogPath = Join-Path $projectRoot "BluetoothBatteryWidget.App\Services\UiLanguageCatalog.cs"
$settingsPath = Join-Path $projectRoot "BluetoothBatteryWidget.Core\Models\WidgetSettings.cs"
$settingsStorePath = Join-Path $projectRoot "BluetoothBatteryWidget.App\Services\WidgetSettingsStore.cs"
$mainWindowTestsPath = Join-Path $projectRoot "BluetoothBatteryWidget.Tests\MainWindowXamlBindingTests.cs"
$projectPath = Join-Path $projectRoot "BluetoothBatteryWidget.App\BluetoothBatteryWidget.App.csproj"
$previewPath = Join-Path $projectRoot "artifacts\release-notes-previews\release-notes-window.png"

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($check in @(
    @{ Path = $releaseXamlPath; Needle = "AbstractSignalBackdrop"; Message = "Release notes window does not include the custom abstract backdrop." },
    @{ Path = $releaseXamlPath; Needle = "WindowStartupLocation=`"CenterScreen`""; Message = "Release notes window must use screen-center startup." },
    @{ Path = $releaseXamlPath; Needle = "HeroBackdropScale"; Message = "Release notes window does not include the custom backdrop scale anchor." },
    @{ Path = $releaseXamlPath; Needle = "TileLayer"; Message = "Release notes window does not include the quiet tile texture layer." },
    @{ Path = $releaseXamlPath; Needle = "SoftSweepTranslate"; Message = "Release notes window does not include the soft sweep animation layer." },
    @{ Path = $releaseXamlPath; Needle = "BitmapCache"; Message = "Release notes window does not cache static visual layers for smoother motion." },
    @{ Path = $releaseXamlPath; Needle = "BrandCoreGlow"; Message = "Release notes window does not include the soft BLoss glow target." },
    @{ Path = $releaseXamlPath; Needle = "BrandRing"; Message = "Release notes window does not include the soft BLoss ring target." },
    @{ Path = $releaseXamlPath; Needle = "BLoss"; Message = "Release notes window does not include the red BLoss brand circle text." },
    @{ Path = $releaseXamlPath; Needle = "RoundedContentRoot"; Message = "Release notes window does not clip inner content to rounded corners." },
    @{ Path = $releaseCodePath; Needle = "RectangleGeometry"; Message = "Release notes window does not apply a rounded clip geometry." },
    @{ Path = $releaseXamlPath; Needle = "HeadingText"; Message = "Release notes heading text block is missing." },
    @{ Path = $languageCatalogPath; Needle = "ReleaseNotesHeading"; Message = "Release notes localized heading text is missing." },
    @{ Path = $releaseCodePath; Needle = 'UiLanguageCatalog.GetExtraText(language, "ReleaseNotesHeading")'; Message = "Release notes popup does not localize its heading." },
    @{ Path = $releaseCodePath; Needle = "ReleaseNotesWindow(string version, string? language = null)"; Message = "Release notes popup does not accept the saved UI language." },
    @{ Path = $releaseXamlPath; Needle = "ReleaseNoteBulletTextStyle"; Message = "Release notes window does not define bullet text styling." },
    @{ Path = $releaseXamlPath; Needle = "VersionText"; Message = "Release notes window does not include the runtime version text." },
    @{ Path = $releaseCodePath; Needle = "AutomationProperties.SetName"; Message = "Release notes window does not expose a stable automation name." },
    @{ Path = $releaseCodePath; Needle = "BeginAmbientAnimations"; Message = "Release notes window does not use the simplified ambient animation path." },
    @{ Path = $releaseCodePath; Needle = "CreateBreathingDoubleAnimation"; Message = "Release notes window does not use the reduced breathing animation path." },
    @{ Path = $releaseCodePath; Needle = "RepeatBehavior.Forever"; Message = "Release notes animation is not configured to repeat." },
    @{ Path = $mainWindowPath; Needle = "IsPortableTestExecutablePath(Environment.ProcessPath)"; Message = "MainWindow does not force release notes for test.exe." },
    @{ Path = $mainWindowPath; Needle = "new ReleaseNotesWindow(version, _viewModel.Language)"; Message = "MainWindow does not pass the saved UI language to the release notes popup." },
    @{ Path = $mainWindowPath; Needle = "if (!forceEveryRun)"; Message = "MainWindow does not guard release seen persistence behind release mode." },
    @{ Path = $mainWindowPath; Needle = "_viewModel.MarkReleaseNotesSeen(version);"; Message = "MainWindow does not mark release notes as seen for release builds." },
    @{ Path = $settingsPath; Needle = "LastSeenReleaseNotesVersion"; Message = "Widget settings do not store the last seen release-notes version." },
    @{ Path = $settingsStorePath; Needle = "NormalizeReleaseNotesVersion"; Message = "Settings store does not normalize the release-notes version field." },
    @{ Path = $mainWindowTestsPath; Needle = 'ShouldShowReleaseNotes("1.0.6", "1.0.7", forceEveryRun: false)'; Message = "Release notes tests do not cover v1.0.6 update behavior." },
    @{ Path = $mainWindowTestsPath; Needle = 'ShouldShowReleaseNotes(" 1.0.7\r\n", "1.0.7", forceEveryRun: false)'; Message = "Release notes tests do not cover normalized seen-version behavior." },
    @{ Path = $mainWindowTestsPath; Needle = 'ShouldShowReleaseNotes("1.0.7", "", forceEveryRun: false)'; Message = "Release notes tests do not cover blank release-version suppression." },
    @{ Path = $mainWindowTestsPath; Needle = 'ShouldShowReleaseNotes("1.0.7", "", forceEveryRun: true)'; Message = "Release notes tests do not cover test.exe force-every-run behavior." }
)) {
    if (-not (Test-FileContains $check.Path $check.Needle)) {
        Add-Failure $failures $check.Message
    }
}

if (Test-Path -LiteralPath (Join-Path $projectRoot "BluetoothBatteryWidget.App\Assets\release-notes-oracus.png")) {
    Add-Failure $failures "Reference ORACUS image asset must not remain in the app assets."
}

if ((Test-FileContains $projectPath "release-notes-oracus") -or
    (Test-FileContains $releaseXamlPath "release-notes-oracus") -or
    (Test-FileContains $releaseXamlPath "ORACUS")) {
    Add-Failure $failures "Release notes popup still references the original ORACUS reference image/text."
}

if ((Test-FileContains $releaseCodePath "BeginTilePulse") -or
    (Test-FileContains $releaseCodePath "DoubleAnimationUsingKeyFrames")) {
    Add-Failure $failures "Release notes popup still uses the old per-tile animation path."
}

if ((Test-FileContains $releaseCodePath "HeroBackdropScale.BeginAnimation") -or
    (Test-FileContains $releaseCodePath "TileLayer.BeginAnimation") -or
    (Test-FileContains $releaseXamlPath "BrandPulseScale") -or
    (Test-FileContains $releaseXamlPath "BrandRingRotate")) {
    Add-Failure $failures "Release notes popup must keep the reduced ambient animation path without backdrop scaling, tile opacity pulsing, or ring rotation."
}

if (Test-FileContains $releaseCodePath "AttachCentered") {
    Add-Failure $failures "Release notes popup must appear immediately and must not use the secondary-window pop-in animation."
}

$mainWindowSource = Get-Content -Encoding UTF8 -LiteralPath $mainWindowPath -Raw
$releaseNotesBlockMatch = [regex]::Match(
    $mainWindowSource,
    "var releaseNotesWindow = new ReleaseNotesWindow\(version, _viewModel\.Language\);[\s\S]*?releaseNotesWindow\.ShowDialog\(\);")
if (-not $releaseNotesBlockMatch.Success) {
    Add-Failure $failures "MainWindow release-notes show block was not found."
}
elseif ($releaseNotesBlockMatch.Value.Contains("CenterOwner")) {
    Add-Failure $failures "Release notes popup must not be centered on the widget owner."
}

if (-not (Test-Path -LiteralPath $TestExePath)) {
    Add-Failure $failures "Portable test executable not found: $TestExePath"
}
else {
    $testExeItem = Get-Item -LiteralPath $TestExePath
    $productVersion = $testExeItem.VersionInfo.ProductVersion.Trim()
    $fileVersion = $testExeItem.VersionInfo.FileVersion.Trim()
    if ($productVersion -ne $ExpectedVersion) {
        Add-Failure $failures "test.exe ProductVersion is '$productVersion', expected '$ExpectedVersion'."
    }
    if ($fileVersion -ne $ExpectedVersion) {
        Add-Failure $failures "test.exe FileVersion is '$fileVersion', expected '$ExpectedVersion'."
    }
}

if (Test-Path -LiteralPath $previewPath) {
    $previewItem = Get-Item -LiteralPath $previewPath
    if ($previewItem.Length -lt (48 * 1024)) {
        Add-Failure $failures "Release notes preview image is too small: $($previewItem.Length) bytes."
    }

    if (-not (Test-PngTransparentCorners $previewPath)) {
        Add-Failure $failures "Release notes preview image corners must be fully transparent."
    }
}

$liveRunsPassed = 0
if ($Live) {
    if (-not [Environment]::UserInteractive) {
        Add-Failure $failures "Live popup verification requires an interactive Windows desktop."
    }
    elseif (-not (Test-Path -LiteralPath $TestExePath)) {
        Add-Failure $failures "Live popup verification requires test.exe: $TestExePath"
    }
    else {
        $existing = @(Get-ExistingTestProcesses $TestExePath)
        if ($existing.Count -gt 0) {
            Add-Failure $failures "Close the already-running portable test.exe before live verification."
        }
        else {
            $expectedWindowPrefix = "Bloss $ExpectedVersion"
            for ($i = 1; $i -le $LaunchCount; $i++) {
                $process = Start-Process -FilePath $TestExePath -PassThru
                try {
                    $window = Wait-ReleaseNotesWindow -ProcessId $process.Id -WindowNamePrefix $expectedWindowPrefix -TimeoutSeconds $TimeoutSeconds
                    if ($null -eq $window) {
                        Add-Failure $failures "Live run $i did not show a release-notes window starting with '$expectedWindowPrefix' within $TimeoutSeconds seconds."
                    }
                    else {
                        $centerCheck = Test-WindowCenteredOnAnyScreen $window
                        if (-not $centerCheck.IsCentered) {
                            Add-Failure $failures "Live run $i showed the release-notes window but it was not centered. Distance from nearest work-area center: $([Math]::Round($centerCheck.Distance, 1)) px on $($centerCheck.Screen)."
                        }
                        $liveRunsPassed++
                        Close-ReleaseNotesWindow $window
                    }
                }
                finally {
                    Start-Sleep -Milliseconds 500
                    if (-not $process.HasExited) {
                        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                    }
                }
            }
        }
    }
}

$result = [pscustomobject]@{
    ExpectedVersion = $ExpectedVersion
    TestExePath = [System.IO.Path]::GetFullPath($TestExePath)
    StaticChecksPassed = $failures.Count -eq 0
    LiveChecksRequested = [bool]$Live
    LiveRunsPassed = $liveRunsPassed
    LaunchCount = if ($Live) { $LaunchCount } else { 0 }
    PreviewPath = $previewPath
}

$result | Format-List

if ($failures.Count -gt 0) {
    throw ("Release notes popup verification failed: " + ($failures -join " "))
}

Write-Host "Release notes popup verification passed."
