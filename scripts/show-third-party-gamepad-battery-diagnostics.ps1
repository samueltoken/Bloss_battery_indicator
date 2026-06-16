param(
    [int]$Tail = 20,
    [switch]$ShowRawIds,
    [switch]$SkipConnectedDevices,
    [switch]$OpenLogFolder
)

$ErrorActionPreference = "Stop"

$blossData = Join-Path $env:APPDATA "Bloss"
$paths = [ordered]@{
    ProviderTrace = Join-Path $blossData "provider-traces.jsonl"
    RuntimeTrace = Join-Path $blossData "third-party-gamepad-battery-traces.jsonl"
    ProbeTrace = Join-Path $blossData "probe-traces.jsonl"
    Observations = Join-Path $blossData "battery-observations.jsonl"
    Profiles = Join-Path $blossData "gamepad-profiles.json"
    Quarantine = Join-Path $blossData "gamepad-profiles-quarantine.json"
    Health = Join-Path $blossData "gamepad-profile-health.json"
    Votes = Join-Path $blossData "gamepad-candidate-votes.json"
    Overrides = Join-Path $blossData "brand-handshake-overrides.json"
}

function ConvertTo-MaskedAddress {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $hex = ($Value -replace "[^0-9A-Fa-f]", "").ToUpperInvariant()
    if ($hex.Length -lt 8) {
        return "masked"
    }

    return "masked"
}

function ConvertTo-SafeText {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $text = [string]$Value
    if ($ShowRawIds) {
        return $text
    }

    $appData = [Environment]::GetFolderPath("ApplicationData")
    $userProfile = [Environment]::GetFolderPath("UserProfile")
    if (-not [string]::IsNullOrWhiteSpace($appData)) {
        $text = $text.Replace($appData, "%APPDATA%")
    }
    if (-not [string]::IsNullOrWhiteSpace($userProfile)) {
        $text = $text.Replace($userProfile, "%USERPROFILE%")
    }

    $text = [regex]::Replace(
        $text,
        "(?i)([0-9a-f]{2}[:-]){5}[0-9a-f]{2}",
        { param($m) ConvertTo-MaskedAddress $m.Value })
    $text = [regex]::Replace(
        $text,
        "(?i)(?<![0-9a-f])[0-9a-f]{8,16}(?![0-9a-f])",
        { param($m) ConvertTo-MaskedAddress $m.Value })
    $text = [regex]::Replace(
        $text,
        "(?i)FP=FP_[^|\s,;]+",
        "FP_MASKED")
    $text = [regex]::Replace(
        $text,
        "(?i)\bFP_[^|\s,;]+",
        "FP_MASKED")
    $text = [regex]::Replace(
        $text,
        "(?i)EP=[^|\s,;]+",
        "EP_MASKED")
    $text = [regex]::Replace(
        $text,
        "(?i)\bEP_[^|\s,;]+",
        "EP_MASKED")
    $text = [regex]::Replace(
        $text,
        "(?i)\bVID[_&=][0-9a-f]{4,6}",
        "VID_MASKED")
    $text = [regex]::Replace(
        $text,
        "(?i)\bPID[_&=][0-9a-f]{4,6}",
        "PID_MASKED")
    $text = [regex]::Replace(
        $text,
        "(?i)\\\\\?\\hid#[^\s,;]+",
        "HID_PATH_MASKED")
    $text = [regex]::Replace(
        $text,
        "(?i)(BTHLE|BTHLEDEVICE|BTHENUM|HID|USB)\\[^\s,;]+",
        '$1\MASKED')

    return $text
}

function Get-JsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties |
        Where-Object { $_.Name -ieq $Name } |
        Select-Object -First 1
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Read-JsonLines {
    param(
        [string]$Path,
        [int]$Count
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    $items = @()
    foreach ($line in Get-Content -LiteralPath $Path -Tail ([Math]::Max(1, $Count))) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        try {
            $items += ($line | ConvertFrom-Json)
        }
        catch {
            $items += [pscustomobject]@{
                parseError = $true
                raw = $line
            }
        }
    }

    return $items
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        return [pscustomobject]@{
            parseError = $true
            message = $_.Exception.Message
        }
    }
}

function Write-Section {
    param([string]$Title)

    Write-Host ""
    Write-Host "== $Title =="
}

function Write-Missing {
    param([string]$Path)

    Write-Host ("missing: {0}" -f (ConvertTo-SafeText $Path))
}

function Format-Null {
    param([object]$Value)

    if ($null -eq $Value) {
        return "null"
    }

    if ([string]::IsNullOrWhiteSpace([string]$Value)) {
        return ""
    }

    return ConvertTo-SafeText $Value
}

function Format-ProfileId {
    param([object]$Value)

    if ($ShowRawIds) {
        return Format-Null $Value
    }

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return ""
    }

    return "masked"
}

function Format-DeviceName {
    param([object]$Value)

    if ($ShowRawIds) {
        return Format-Null $Value
    }

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return ""
    }

    return "masked"
}

function Show-ConnectedGamepadHints {
    Write-Section "Connected gamepad-like PnP devices"

    $command = Get-Command Get-PnpDevice -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        Write-Host "Get-PnpDevice is unavailable in this PowerShell session."
        return
    }

    try {
        $keywords = "game|controller|xbox|wireless|flydigi|gamesir|8bit|gulikit|easysmx|joystick|pad"
        $items = @(Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object {
            (($_.Class -eq "Bluetooth") -or ($_.Class -eq "HIDClass")) -and
            (([string]$_.FriendlyName) -match $keywords -or ([string]$_.InstanceId) -match $keywords)
        } | Select-Object -First 40)

        if ($items.Count -eq 0) {
            Write-Host "No obvious connected gamepad-like PnP device was found."
            return
        }

        foreach ($item in $items) {
            Write-Host ("{0} | {1} | {2} | id={3}" -f `
                (Format-Null $item.Class), `
                (Format-Null $item.Status), `
                (Format-DeviceName $item.FriendlyName), `
                (Format-ProfileId $item.InstanceId))
        }
    }
    catch {
        Write-Host ("PnP device read failed: {0}" -f $_.Exception.Message)
    }
}

function Show-ProviderTrace {
    Write-Section "Provider timeouts"

    $items = Read-JsonLines $paths.ProviderTrace $Tail
    if ($items.Count -eq 0) {
        Write-Missing $paths.ProviderTrace
        return
    }

    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            Write-Host ("malformed: {0}" -f (Format-Null (Get-JsonProperty $item "raw")))
            continue
        }

        $timeouts = @(Get-JsonProperty $item "providerTimeoutHit")
        Write-Host ("{0} | connected={1} | timeout={2} | build={3}" -f `
            (Format-Null (Get-JsonProperty $item "ts")), `
            (Format-Null (Get-JsonProperty $item "connectedCount")), `
            (($timeouts | ForEach-Object { [string]$_ }) -join ","), `
            (Format-Null (Get-JsonProperty $item "buildStamp")))
    }
}

function Show-ProbeTrace {
    Write-Section "Recent probe traces"

    $items = Read-JsonLines $paths.ProbeTrace $Tail
    if ($items.Count -eq 0) {
        Write-Missing $paths.ProbeTrace
        Write-Host "Run test.exe and use the gamepad battery collection/probe flow first."
        return
    }

    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            Write-Host ("malformed: {0}" -f (Format-Null (Get-JsonProperty $item "raw")))
            continue
        }

        $device = Get-JsonProperty $item "device"
        $name = Format-DeviceName (Get-JsonProperty $device "displayName")
        $address = Format-Null (Get-JsonProperty $device "address")
        $top = @(Get-JsonProperty $item "topCandidates") | Select-Object -First 3
        $decisionHints = @(Get-JsonProperty $item "candidateDecisionHints") | Select-Object -First 5
        $selectedProof = Format-Null (Get-JsonProperty $item "selectedCandidateProof")

        Write-Host ("{0} | {1} | success={2} pending={3} percent={4} | {5} | addr={6}" -f `
            (Format-Null (Get-JsonProperty $item "ts")), `
            (Format-Null (Get-JsonProperty $item "outcome")), `
            (Format-Null (Get-JsonProperty $item "success")), `
            (Format-Null (Get-JsonProperty $item "isPending")), `
            (Format-Null (Get-JsonProperty $item "batteryPercent")), `
            $name, `
            $address)
        Write-Host ("  stage={0} reason={1} block={2} suppress={3}" -f `
            (Format-Null (Get-JsonProperty $item "stage")), `
            (Format-Null (Get-JsonProperty $item "reasonCode")), `
            (Format-Null (Get-JsonProperty $item "blockReason")), `
            (Format-Null (Get-JsonProperty $item "suppressionReason")))
        Write-Host ("  winner={0} reliability={1} profile={2} profileReason={3} path={4}" -f `
            (Format-Null (Get-JsonProperty $item "winnerDecoder")), `
            (Format-Null (Get-JsonProperty $item "reliabilityScore")), `
            (Format-Null (Get-JsonProperty $item "profileId")), `
            (Format-Null (Get-JsonProperty $item "profileSelectionReason")), `
            (Format-Null (Get-JsonProperty $item "pathType")))
        Write-Host ("  reports={0}" -f (Format-Null (Get-JsonProperty $item "observedReportIds")))
        if ($top.Count -gt 0) {
            Write-Host ("  top={0}" -f (ConvertTo-SafeText (($top | ForEach-Object { [string]$_ }) -join " | ")))
        }
        if ($decisionHints.Count -gt 0) {
            Write-Host ("  decisionHints={0}" -f (ConvertTo-SafeText (($decisionHints | ForEach-Object { [string]$_ }) -join " | ")))
        }
        if ($selectedProof -ne "null") {
            Write-Host ("  selectedProof={0}" -f (ConvertTo-SafeText $selectedProof))
        }
    }
}

function Get-PendingVoteIndex {
    if ($null -ne $script:PendingVoteIndex) {
        return $script:PendingVoteIndex
    }

    $script:PendingVoteIndex = @{}
    $json = Read-JsonFile $paths.Votes
    if ($null -eq $json -or (Get-JsonProperty $json "parseError")) {
        return $script:PendingVoteIndex
    }

    $votes = Get-JsonProperty $json "votes"
    if ($null -eq $votes) {
        return $script:PendingVoteIndex
    }

    foreach ($property in @($votes.PSObject.Properties)) {
        $value = $property.Value
        $candidateKey = [string](Get-JsonProperty $value "candidateKey")
        $match = [regex]::Match(
            $candidateKey,
            "(?i)RID_(?<report>[0-9A-F]{2})\|OFF_(?<offset>\d+)\|DEC_(?<decoder>[^|]+)")
        if (-not $match.Success) {
            continue
        }

        $signature = "{0}|{1}|{2}" -f `
            $match.Groups["decoder"].Value.ToUpperInvariant(), `
            $match.Groups["report"].Value.ToUpperInvariant(), `
            $match.Groups["offset"].Value
        $voteCount = 0
        [void][int]::TryParse([string](Get-JsonProperty $value "voteCount"), [ref]$voteCount)
        $minPercent = Get-JsonProperty $value "minPercent"
        $maxPercent = Get-JsonProperty $value "maxPercent"
        $current = if ($script:PendingVoteIndex.ContainsKey($signature)) { $script:PendingVoteIndex[$signature] } else { $null }
        if ($null -eq $current -or $voteCount -gt $current.VoteCount) {
            $script:PendingVoteIndex[$signature] = [pscustomobject]@{
                VoteCount = $voteCount
                MinPercent = $minPercent
                MaxPercent = $maxPercent
            }
        }
    }

    return $script:PendingVoteIndex
}

function Get-CandidateVoteInfo {
    param(
        [string]$Decoder,
        [string]$Report,
        [string]$Offset
    )

    $index = Get-PendingVoteIndex
    $signature = "{0}|{1}|{2}" -f $Decoder.ToUpperInvariant(), $Report.ToUpperInvariant(), $Offset
    if ($index.ContainsKey($signature)) {
        return $index[$signature]
    }

    return [pscustomobject]@{
        VoteCount = 0
        MinPercent = $null
        MaxPercent = $null
    }
}

function ConvertTo-NullableInt {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    $parsed = 0
    if ([int]::TryParse([string]$Value, [ref]$parsed)) {
        return $parsed
    }

    return $null
}

function Get-CandidateValidationState {
    param(
        [string]$Decoder,
        [object]$VoteInfo,
        [object]$ObservedCount = $null,
        [object]$ObservedMinPercent = $null,
        [object]$ObservedMaxPercent = $null
    )

    if ($Decoder -ieq "xbox_bt_flags") {
        return [pscustomobject]@{
            Trust = "coarse-bucket-only"
            Movement = "not-used"
        }
    }

    if ($Decoder -notmatch "^(percent100|percent255|nibble10)$") {
        return [pscustomobject]@{
            Trust = "unknown-candidate"
            Movement = "unknown"
        }
    }

    $voteCount = 0
    [void][int]::TryParse([string]$VoteInfo.VoteCount, [ref]$voteCount)
    $observedCountValue = 0
    [void][int]::TryParse([string]$ObservedCount, [ref]$observedCountValue)

    $voteMin = ConvertTo-NullableInt $VoteInfo.MinPercent
    $voteMax = ConvertTo-NullableInt $VoteInfo.MaxPercent
    $observedMin = ConvertTo-NullableInt $ObservedMinPercent
    $observedMax = ConvertTo-NullableInt $ObservedMaxPercent

    $hasVoteRange = $null -ne $voteMin -and $null -ne $voteMax
    $hasObservedRange = $null -ne $observedMin -and $null -ne $observedMax
    $hasVoteMovement = $hasVoteRange -and (($voteMax - $voteMin) -ge 2)
    $hasObservedMovement = $hasObservedRange -and (($observedMax - $observedMin) -ge 2)
    $evidenceCount = [Math]::Max($voteCount, $observedCountValue)

    $movement = "unknown"
    if ($hasVoteRange) {
        $prefix = if ($hasVoteMovement) { "moved" } else { "flat" }
        $movement = "{0}:{1}-{2}(votes)" -f $prefix, $voteMin, $voteMax
    }
    elseif ($hasObservedRange) {
        $prefix = if ($hasObservedMovement) { "moved" } else { "flat" }
        $movement = "{0}:{1}-{2}(trace)" -f $prefix, $observedMin, $observedMax
    }

    $hasMovement = $hasVoteMovement -or $hasObservedMovement
    $trust = "exact-candidate-needs-repeat"
    if ($evidenceCount -ge 3 -and $hasMovement) {
        $trust = "ready-after-repeat-and-change"
    }
    elseif ($evidenceCount -ge 3) {
        $trust = "needs-percent-change"
    }

    return [pscustomobject]@{
        Trust = $trust
        Movement = $movement
    }
}

function Resolve-CandidateTrust {
    param(
        [string]$Decoder,
        [object]$VoteInfo,
        [object]$ObservedCount = $null,
        [object]$ObservedMinPercent = $null,
        [object]$ObservedMaxPercent = $null
    )

    return (Get-CandidateValidationState $Decoder $VoteInfo $ObservedCount $ObservedMinPercent $ObservedMaxPercent).Trust
}

function Resolve-CandidateMovement {
    param(
        [object]$VoteInfo,
        [object]$ObservedCount = $null,
        [object]$ObservedMinPercent = $null,
        [object]$ObservedMaxPercent = $null
    )

    return (Get-CandidateValidationState "percent100" $VoteInfo $ObservedCount $ObservedMinPercent $ObservedMaxPercent).Movement
}

function Get-CandidateLines {
    param([object]$TraceItem)

    $observations = @(Get-JsonProperty $TraceItem "candidateObservations") |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
    if ($observations.Count -gt 0) {
        return $observations
    }

    return @(Get-JsonProperty $TraceItem "topCandidates") |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
}

function Get-ParsedBatteryCandidateGroups {
    param([int]$ReadTail)

    $items = Read-JsonLines $paths.ProbeTrace $ReadTail
    $groups = @{}
    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            continue
        }

        foreach ($candidate in @(Get-CandidateLines $item)) {
            $text = [string]$candidate
            $match = [regex]::Match(
                $text,
                "^(?<decoder>[^@]+)@0x(?<report>[0-9A-Fa-f]{2})\[off=(?<offset>\d+),pct=(?<percent>\d+),score=(?<score>\d+)")
            if (-not $match.Success) {
                continue
            }

            $decoder = $match.Groups["decoder"].Value
            $report = $match.Groups["report"].Value.ToUpperInvariant()
            $offset = $match.Groups["offset"].Value
            $key = "{0}|{1}|{2}" -f $decoder, $report, $offset
            if (-not $groups.ContainsKey($key)) {
                $groups[$key] = [pscustomobject]@{
                    Decoder = $decoder
                    Report = $report
                    Offset = $offset
                    Count = 0
                    Percents = New-Object System.Collections.Generic.List[int]
                    Scores = New-Object System.Collections.Generic.List[int]
                }
            }

            $entry = $groups[$key]
            $entry.Count++
            $entry.Percents.Add([int]$match.Groups["percent"].Value)
            $entry.Scores.Add([int]$match.Groups["score"].Value)
        }
    }

    return @($groups.Values)
}

function Format-CandidateGroupLine {
    param(
        [object]$Entry,
        [string]$Conclusion
    )

    $minPercent = ($Entry.Percents | Measure-Object -Minimum).Minimum
    $maxPercent = ($Entry.Percents | Measure-Object -Maximum).Maximum
    $maxScore = ($Entry.Scores | Measure-Object -Maximum).Maximum
    $voteInfo = Get-CandidateVoteInfo $Entry.Decoder $Entry.Report $Entry.Offset
    $validationState = Get-CandidateValidationState $Entry.Decoder $voteInfo $Entry.Count $minPercent $maxPercent
    $trust = $validationState.Trust
    $voteText = if ($Entry.Decoder -ieq "xbox_bt_flags") { "n/a" } else { "{0}/3" -f $voteInfo.VoteCount }
    $movement = $validationState.Movement

    return ("decoder={0} report=0x{1} offset={2} seen={3} percentRange={4}-{5} maxScore={6} votes={7} movement={8} trust={9} conclusion={10}" -f `
        (ConvertTo-SafeText $Entry.Decoder), `
        (ConvertTo-SafeText $Entry.Report), `
        (ConvertTo-SafeText $Entry.Offset), `
        (ConvertTo-SafeText $Entry.Count), `
        (ConvertTo-SafeText $minPercent), `
        (ConvertTo-SafeText $maxPercent), `
        (ConvertTo-SafeText $maxScore), `
        (ConvertTo-SafeText $voteText), `
        (ConvertTo-SafeText $movement), `
        (ConvertTo-SafeText $trust), `
        (ConvertTo-SafeText $Conclusion))
}

function Get-BatteryCandidateClusterSummaries {
    param([object[]]$Groups)
    $clusters = @{}
    foreach ($entry in @($Groups)) {
        $key = "{0}|{1}" -f $entry.Decoder, $entry.Offset
        if (-not $clusters.ContainsKey($key)) {
            $clusters[$key] = [pscustomobject]@{
                Decoder = $entry.Decoder
                Offset = $entry.Offset
                Reports = New-Object System.Collections.Generic.List[string]
                Count = 0
                Percents = New-Object System.Collections.Generic.List[int]
                Scores = New-Object System.Collections.Generic.List[int]
            }
        }

        $cluster = $clusters[$key]
        if (-not $cluster.Reports.Contains($entry.Report)) {
            $cluster.Reports.Add($entry.Report)
        }
        $cluster.Count += $entry.Count
        foreach ($percent in $entry.Percents) {
            $cluster.Percents.Add($percent)
        }
        foreach ($score in $entry.Scores) {
            $cluster.Scores.Add($score)
        }
    }

    $summaries = @()
    foreach ($cluster in $clusters.Values) {
        $reports = @($cluster.Reports | Sort-Object | ForEach-Object { "0x{0}" -f $_ })
        $reportText = [string]::Join(",", $reports)
        $distinctReports = $reports.Count
        $minPercent = ($cluster.Percents | Measure-Object -Minimum).Minimum
        $maxPercent = ($cluster.Percents | Measure-Object -Maximum).Maximum
        $maxScore = ($cluster.Scores | Measure-Object -Maximum).Maximum
        $spread = $maxPercent - $minPercent
        $conclusion = "single-report-needs-repeat-change"
        if ($cluster.Decoder -ieq "xbox_bt_flags") {
            $conclusion = "coarse-bucket-not-1pct"
        }
        elseif ($distinctReports -ge 2 -and $cluster.Count -ge 3 -and $spread -ge 2) {
            $conclusion = "multi-report-ready-after-change"
        }
        elseif ($distinctReports -ge 2) {
            $conclusion = "multi-report-flat-needs-change"
        }

        $summaries += [pscustomobject]@{
            Decoder = $cluster.Decoder
            Offset = $cluster.Offset
            ReportText = $reportText
            ReportCount = $distinctReports
            Seen = $cluster.Count
            MinPercent = $minPercent
            MaxPercent = $maxPercent
            MaxScore = $maxScore
            Spread = $spread
            Conclusion = $conclusion
        }
    }

    return $summaries
}

function Show-BatteryCandidateClusters {
    Write-Section "Battery candidate clusters"

    $groups = Get-ParsedBatteryCandidateGroups ([Math]::Max($Tail, 200))
    if ($groups.Count -eq 0) {
        Write-Host "No parsed candidate clusters in recent probe traces."
        return
    }

    $clusters = Get-BatteryCandidateClusterSummaries $groups
    foreach ($cluster in ($clusters | Sort-Object -Property Seen -Descending | Select-Object -First ([Math]::Max(1, $Tail)))) {
        Write-Host ("decoder={0} offset={1} reports={2} reportCount={3} seen={4} percentRange={5}-{6} maxScore={7} conclusion={8}" -f `
            (ConvertTo-SafeText $cluster.Decoder), `
            (ConvertTo-SafeText $cluster.Offset), `
            (ConvertTo-SafeText $cluster.ReportText), `
            (ConvertTo-SafeText $cluster.ReportCount), `
            (ConvertTo-SafeText $cluster.Seen), `
            (ConvertTo-SafeText $cluster.MinPercent), `
            (ConvertTo-SafeText $cluster.MaxPercent), `
            (ConvertTo-SafeText $cluster.MaxScore), `
            (ConvertTo-SafeText $cluster.Conclusion))
    }
}

function Show-BatteryCandidateProofReadiness {
    Write-Section "Battery candidate proof readiness"

    $groups = Get-ParsedBatteryCandidateGroups ([Math]::Max($Tail, 200))
    if ($groups.Count -eq 0) {
        Write-Host "bestReportExact=none decision=wait-for-hid-candidate"
        Write-Host "clusterHint=none"
        Write-Host "display=keep-na"
        return
    }

    $clusters = Get-BatteryCandidateClusterSummaries $groups
    $exactClusters = @($clusters |
        Where-Object { $_.Decoder -match "^(percent100|percent255|nibble10)$" } |
        Sort-Object `
            @{ Expression = "ReportCount"; Descending = $true }, `
            @{ Expression = "Seen"; Descending = $true }, `
            @{ Expression = "MaxScore"; Descending = $true } |
        Select-Object -First 1)
    if ($exactClusters.Count -eq 0) {
        Write-Host "bestReportExact=none decision=only-coarse-or-unknown-candidates"
        Write-Host "clusterHint=none"
        Write-Host "display=keep-na"
        return
    }

    $exactReports = @($groups |
        Where-Object { $_.Decoder -match "^(percent100|percent255|nibble10)$" } |
        Sort-Object `
            @{ Expression = "Count"; Descending = $true }, `
            @{ Expression = { if ($_.Report -match "^(01|11|12|21|31|81|82)$") { 1 } else { 0 } }; Descending = $true }, `
            @{ Expression = { Get-BatteryReportWatchRank $_.Report } }, `
            @{ Expression = { ($_.Scores | Measure-Object -Maximum).Maximum }; Descending = $true } |
        Select-Object -First 1)

    $best = $exactReports[0]
    $minPercent = ($best.Percents | Measure-Object -Minimum).Minimum
    $maxPercent = ($best.Percents | Measure-Object -Maximum).Maximum
    $maxScore = ($best.Scores | Measure-Object -Maximum).Maximum
    $spread = $maxPercent - $minPercent
    $voteInfo = Get-CandidateVoteInfo $best.Decoder $best.Report $best.Offset
    $voteCount = 0
    [void][int]::TryParse([string]$voteInfo.VoteCount, [ref]$voteCount)
    $validationState = Get-CandidateValidationState $best.Decoder $voteInfo $best.Count $minPercent $maxPercent
    $evidenceCount = [Math]::Max($best.Count, $voteCount)
    $repeatOk = $evidenceCount -ge 3
    $movementOk = $validationState.Movement -like "moved:*"
    if (-not $movementOk -and $spread -ge 2) {
        $movementOk = $true
    }
    $repeatText = if ($repeatOk) { "yes" } else { "no" }
    $movementText = if ($movementOk) { "yes" } else { "no" }
    $voteText = "{0}/3" -f $voteCount
    $decision = "wait-for-repeat-and-percent-change"
    if ($repeatOk -and $movementOk) {
        $decision = "candidate-ready-for-learning"
    }
    elseif ($repeatOk) {
        $decision = "wait-for-percent-change"
    }
    elseif ($movementOk) {
        $decision = "wait-for-repeat"
    }

    Write-Host ("bestReportExact=decoder={0} report=0x{1} offset={2} seen={3} votes={4} percentRange={5}-{6} maxScore={7} repeat={8} movement={9} movementDetail={10} decision={11}" -f `
        (ConvertTo-SafeText $best.Decoder), `
        (ConvertTo-SafeText $best.Report), `
        (ConvertTo-SafeText $best.Offset), `
        (ConvertTo-SafeText $best.Count), `
        (ConvertTo-SafeText $voteText), `
        (ConvertTo-SafeText $minPercent), `
        (ConvertTo-SafeText $maxPercent), `
        (ConvertTo-SafeText $maxScore), `
        (ConvertTo-SafeText $repeatText), `
        (ConvertTo-SafeText $movementText), `
        (ConvertTo-SafeText $validationState.Movement), `
        (ConvertTo-SafeText $decision))

    $cluster = $exactClusters[0]
    $clusterRepeatText = if ($cluster.Seen -ge 3) { "yes" } else { "no" }
    $clusterMovementText = if ($cluster.Spread -ge 2) { "yes" } else { "no" }
    Write-Host ("clusterHint=decoder={0} offset={1} reports={2} reportCount={3} seen={4} percentRange={5}-{6} crossReportRepeat={7} movement={8} use=watch-only" -f `
        (ConvertTo-SafeText $cluster.Decoder), `
        (ConvertTo-SafeText $cluster.Offset), `
        (ConvertTo-SafeText $cluster.ReportText), `
        (ConvertTo-SafeText $cluster.ReportCount), `
        (ConvertTo-SafeText $cluster.Seen), `
        (ConvertTo-SafeText $cluster.MinPercent), `
        (ConvertTo-SafeText $cluster.MaxPercent), `
        (ConvertTo-SafeText $clusterRepeatText), `
        (ConvertTo-SafeText $clusterMovementText))

    if ($decision -eq "candidate-ready-for-learning") {
        Write-Host "display=ready-after-runtime-revalidation"
    }
    else {
        Write-Host "display=keep-na"
    }
}

function Show-BatterySignalContrast {
    Write-Section "Battery signal contrast"

    $groups = Get-ParsedBatteryCandidateGroups ([Math]::Max($Tail, 200))
    if ($groups.Count -eq 0) {
        Write-Host "contrast=no-parsed-candidates decision=keep-na"
        return
    }

    $coarseGroups = @($groups |
        Where-Object { $_.Decoder -ieq "xbox_bt_flags" } |
        Sort-Object `
            @{ Expression = { (($_.Percents | Measure-Object -Maximum).Maximum) - (($_.Percents | Measure-Object -Minimum).Minimum) }; Descending = $true }, `
            @{ Expression = "Count"; Descending = $true } |
        Select-Object -First 1)

    $exactGroups = @($groups |
        Where-Object { $_.Decoder -match "^(percent100|percent255|nibble10)$" } |
        Sort-Object `
            @{ Expression = "Count"; Descending = $true }, `
            @{ Expression = { if ($_.Report -match "^(01|11|12|21|31|81|82)$") { 1 } else { 0 } }; Descending = $true }, `
            @{ Expression = { Get-BatteryReportWatchRank $_.Report } }, `
            @{ Expression = { ($_.Scores | Measure-Object -Maximum).Maximum }; Descending = $true } |
        Select-Object -First 1)

    if ($coarseGroups.Count -eq 0 -and $exactGroups.Count -eq 0) {
        Write-Host "contrast=no-known-battery-shaped-candidates decision=keep-na"
        return
    }

    $coarseSpread = 0
    if ($coarseGroups.Count -gt 0) {
        $coarse = $coarseGroups[0]
        $coarseMin = ($coarse.Percents | Measure-Object -Minimum).Minimum
        $coarseMax = ($coarse.Percents | Measure-Object -Maximum).Maximum
        $coarseSpread = $coarseMax - $coarseMin
        $coarseMoved = if ($coarseSpread -ge 2) { "yes" } else { "no" }
        Write-Host ("coarse=decoder={0} report=0x{1} offset={2} seen={3} percentRange={4}-{5} movement={6} trust=coarse-bucket-only" -f `
            (ConvertTo-SafeText $coarse.Decoder), `
            (ConvertTo-SafeText $coarse.Report), `
            (ConvertTo-SafeText $coarse.Offset), `
            (ConvertTo-SafeText $coarse.Count), `
            (ConvertTo-SafeText $coarseMin), `
            (ConvertTo-SafeText $coarseMax), `
            (ConvertTo-SafeText $coarseMoved))
    }
    else {
        Write-Host "coarse=none"
    }

    $exactSpread = 0
    $exactReady = $false
    if ($exactGroups.Count -gt 0) {
        $exact = $exactGroups[0]
        $exactMin = ($exact.Percents | Measure-Object -Minimum).Minimum
        $exactMax = ($exact.Percents | Measure-Object -Maximum).Maximum
        $exactSpread = $exactMax - $exactMin
        $voteInfo = Get-CandidateVoteInfo $exact.Decoder $exact.Report $exact.Offset
        $voteCount = 0
        [void][int]::TryParse([string]$voteInfo.VoteCount, [ref]$voteCount)
        $validationState = Get-CandidateValidationState $exact.Decoder $voteInfo $exact.Count $exactMin $exactMax
        $exactReady = $validationState.Trust -ieq "ready-after-repeat-and-change"
        $exactMoved = if ($validationState.Movement -like "moved:*" -or $exactSpread -ge 2) { "yes" } else { "no" }
        Write-Host ("exact=decoder={0} report=0x{1} offset={2} seen={3} votes={4}/3 percentRange={5}-{6} movement={7} movementDetail={8} trust={9}" -f `
            (ConvertTo-SafeText $exact.Decoder), `
            (ConvertTo-SafeText $exact.Report), `
            (ConvertTo-SafeText $exact.Offset), `
            (ConvertTo-SafeText $exact.Count), `
            (ConvertTo-SafeText $voteCount), `
            (ConvertTo-SafeText $exactMin), `
            (ConvertTo-SafeText $exactMax), `
            (ConvertTo-SafeText $exactMoved), `
            (ConvertTo-SafeText $validationState.Movement), `
            (ConvertTo-SafeText $validationState.Trust))
    }
    else {
        Write-Host "exact=none"
    }

    $contrast = "insufficient-reference"
    $decision = "keep-na"
    if ($exactReady) {
        $contrast = "exact-repeat-and-moved"
        $decision = "ready-after-runtime-revalidation"
    }
    elseif ($coarseGroups.Count -gt 0 -and $exactGroups.Count -gt 0 -and $coarseSpread -ge 2 -and $exactSpread -lt 2) {
        $contrast = "coarse-moved-exact-flat"
        $decision = "keep-na-and-watch-exact"
    }
    elseif ($exactGroups.Count -gt 0 -and $exactSpread -ge 2) {
        $contrast = "exact-moved-needs-repeat"
        $decision = "keep-na-until-repeat"
    }
    elseif ($coarseGroups.Count -gt 0 -and $exactGroups.Count -gt 0) {
        $contrast = "both-flat-or-not-enough-range"
        $decision = "keep-na-and-collect-more"
    }
    elseif ($coarseGroups.Count -gt 0) {
        $contrast = "coarse-only"
        $decision = "coarse-is-not-1pct"
    }

    Write-Host ("contrast={0} decision={1}" -f `
        (ConvertTo-SafeText $contrast), `
        (ConvertTo-SafeText $decision))
}

function Show-BatteryCandidateDecisionMatrix {
    Write-Section "Battery candidate decision matrix"

    $groups = Get-ParsedBatteryCandidateGroups ([Math]::Max($Tail, 200))
    if ($groups.Count -eq 0) {
        Write-Host "matrixResult=no-candidates display=keep-na"
        return
    }

    $coarseMoved = $false
    foreach ($coarse in @($groups | Where-Object { $_.Decoder -ieq "xbox_bt_flags" })) {
        $coarseMin = ($coarse.Percents | Measure-Object -Minimum).Minimum
        $coarseMax = ($coarse.Percents | Measure-Object -Maximum).Maximum
        if (($coarseMax - $coarseMin) -ge 2) {
            $coarseMoved = $true
            break
        }
    }

    $readyFound = $false
    $ordered = @($groups | Sort-Object `
        @{ Expression = { if ($_.Decoder -ieq "xbox_bt_flags") { 0 } elseif ($_.Report -ieq "04") { 2 } else { 1 } } }, `
        @{ Expression = { Get-BatteryReportWatchRank $_.Report } }, `
        @{ Expression = "Count"; Descending = $true }, `
        @{ Expression = { ($_.Scores | Measure-Object -Maximum).Maximum }; Descending = $true })

    foreach ($entry in ($ordered | Select-Object -First ([Math]::Max(1, $Tail)))) {
        $minPercent = ($entry.Percents | Measure-Object -Minimum).Minimum
        $maxPercent = ($entry.Percents | Measure-Object -Maximum).Maximum
        $maxScore = ($entry.Scores | Measure-Object -Maximum).Maximum
        $spread = $maxPercent - $minPercent
        $voteInfo = Get-CandidateVoteInfo $entry.Decoder $entry.Report $entry.Offset
        $voteText = if ($entry.Decoder -ieq "xbox_bt_flags") { "n/a" } else { "{0}/3" -f $voteInfo.VoteCount }
        $validationState = Get-CandidateValidationState $entry.Decoder $voteInfo $entry.Count $minPercent $maxPercent

        $decision = "watch"
        $reason = "needs-repeat-and-change"
        if ($entry.Decoder -ieq "xbox_bt_flags") {
            $decision = "reject-exact"
            $reason = "xbox-bucket-open-source"
        }
        elseif ($validationState.Trust -ieq "ready-after-repeat-and-change") {
            $decision = "ready"
            $reason = "repeat-and-movement-ok"
            $readyFound = $true
        }
        elseif ($entry.Report -ieq "04" -and $coarseMoved -and $spread -lt 2) {
            $decision = "demote"
            $reason = "status-flat-against-coarse"
        }
        elseif ($spread -ge 2) {
            $reason = "movement-needs-repeat"
        }

        Write-Host ("decision={0} decoder={1} report=0x{2} offset={3} seen={4} votes={5} percentRange={6}-{7} trust={8} reason={9}" -f `
            (ConvertTo-SafeText $decision), `
            (ConvertTo-SafeText $entry.Decoder), `
            (ConvertTo-SafeText $entry.Report), `
            (ConvertTo-SafeText $entry.Offset), `
            (ConvertTo-SafeText $entry.Count), `
            (ConvertTo-SafeText $voteText), `
            (ConvertTo-SafeText $minPercent), `
            (ConvertTo-SafeText $maxPercent), `
            (ConvertTo-SafeText $validationState.Trust), `
            (ConvertTo-SafeText $reason))
    }

    if ($readyFound) {
        Write-Host "matrixResult=ready-after-runtime-revalidation"
    }
    else {
        Write-Host "matrixResult=no-real-1pct-yet display=keep-na"
    }
}

function Resolve-CandidateWatchPriority {
    param(
        [object]$Entry,
        [int]$Spread,
        [bool]$CoarseMoved
    )

    $isStatusReport = $Entry.Report -ieq "04"
    if ($isStatusReport -and $CoarseMoved -and $Spread -lt 2) {
        return "demote-status-flat-against-coarse"
    }

    if ($isStatusReport) {
        return "status-report-needs-stronger-proof"
    }

    if ($Spread -ge 2) {
        return "watch-dedicated-needs-repeat"
    }

    return "watch-dedicated-needs-repeat-change"
}

function Get-BatteryReportWatchRank {
    param([string]$Report)

    switch ($Report.ToUpperInvariant()) {
        "31" { return 0 }
        "11" { return 1 }
        "12" { return 2 }
        "01" { return 3 }
        "21" { return 4 }
        "81" { return 5 }
        "82" { return 6 }
        "04" { return 50 }
        default { return 100 }
    }
}

function Show-BatteryNextEvidencePlan {
    Write-Section "Battery next evidence plan"

    $groups = Get-ParsedBatteryCandidateGroups ([Math]::Max($Tail, 200))
    if ($groups.Count -eq 0) {
        Write-Host "watch=none reason=no-parsed-candidates"
        Write-Host "required=collect-hid-probe-trace display=keep-na"
        return
    }

    $coarseMoved = $false
    foreach ($coarse in @($groups | Where-Object { $_.Decoder -ieq "xbox_bt_flags" })) {
        $coarseMin = ($coarse.Percents | Measure-Object -Minimum).Minimum
        $coarseMax = ($coarse.Percents | Measure-Object -Maximum).Maximum
        if (($coarseMax - $coarseMin) -ge 2) {
            $coarseMoved = $true
            break
        }
    }

    $exactCandidates = @($groups |
        Where-Object { $_.Decoder -match "^(percent100|percent255|nibble10)$" } |
        Sort-Object `
            @{ Expression = { if ($_.Report -match "^(01|11|12|21|31|81|82)$") { 1 } else { 0 } }; Descending = $true }, `
            @{ Expression = "Count"; Descending = $true }, `
            @{ Expression = { Get-BatteryReportWatchRank $_.Report } }, `
            @{ Expression = { ($_.Scores | Measure-Object -Maximum).Maximum }; Descending = $true })

    if ($exactCandidates.Count -eq 0) {
        Write-Host ("watch=none reason=no-exact-shaped-candidate coarseMoved={0}" -f (if ($coarseMoved) { "yes" } else { "no" }))
        Write-Host "required=collect-hid-probe-trace display=keep-na"
        return
    }

    foreach ($entry in ($exactCandidates | Select-Object -First 6)) {
        $minPercent = ($entry.Percents | Measure-Object -Minimum).Minimum
        $maxPercent = ($entry.Percents | Measure-Object -Maximum).Maximum
        $maxScore = ($entry.Scores | Measure-Object -Maximum).Maximum
        $spread = $maxPercent - $minPercent
        $voteInfo = Get-CandidateVoteInfo $entry.Decoder $entry.Report $entry.Offset
        $voteCount = 0
        [void][int]::TryParse([string]$voteInfo.VoteCount, [ref]$voteCount)
        $priority = Resolve-CandidateWatchPriority $entry $spread $coarseMoved
        $label = if ($priority -like "demote-*") { "demote" } else { "watch" }

        Write-Host ("{0}=decoder={1} report=0x{2} offset={3} seen={4} votes={5}/3 percentRange={6}-{7} maxScore={8} priority={9}" -f `
            $label, `
            (ConvertTo-SafeText $entry.Decoder), `
            (ConvertTo-SafeText $entry.Report), `
            (ConvertTo-SafeText $entry.Offset), `
            (ConvertTo-SafeText $entry.Count), `
            (ConvertTo-SafeText $voteCount), `
            (ConvertTo-SafeText $minPercent), `
            (ConvertTo-SafeText $maxPercent), `
            (ConvertTo-SafeText $maxScore), `
            (ConvertTo-SafeText $priority))
    }

    $coarseMovedText = if ($coarseMoved) { "yes" } else { "no" }
    Write-Host ("required=same-report-offset repeat>=3 movement>=2 coarseMoved={0} display=keep-na-until-ready" -f $coarseMovedText)
}

function Show-OpenSourceBatteryComparison {
    Write-Section "Open-source battery comparison"
    Write-Host "checked=2026-05-28 sources=Bluetooth-BAS,Microsoft-Bluetooth-battery-guidelines,SDL-XInput,SDL-HIDAPI-XboxOne,xpadneo,Microsoft-XInput,Microsoft-GameInput,EasySMX-product,EasySMX-manual,DeviceReport-X05Pro-manual,Gamepadla-X05Pro,community-mode-report"
    Write-Host "external=bluetooth_bas standard Battery Level is 0-100 percent only if the device exposes that service/characteristic."
    Write-Host "external=microsoft_bluetooth_guidelines battery percent requires firmware measurement/filtering; host UI cannot invent missing data."
    Write-Host "external=sdl_hidapi_xboxone bluetooth packet 0x04 data[1] uses Xbox-style battery flags mapped to 10/40/70/100."
    Write-Host "external=xpadneo uses low two flag bits as low/normal/high/full capacity level, not exact percent."
    Write-Host "external=xbox_bt_flags report=0x04 offset=1 maps Xbox-style battery buckets only; do not treat as exact 1%."
    Write-Host "external=easysmx_manual documents charging LEDs, low-battery LED warning, modes, and Xbox Wireless Controller Bluetooth naming, but no HID 1% battery report layout."
    Write-Host "external=gamepadla_x05pro documents X05 Pro as multi-platform/multi-mode, but no battery report offset."
    Write-Host "external=community_mode_report says X-input Bluetooth can appear as Xbox Wireless Controller while D-input can appear as EasySMX X05 Pro; name alone is mode evidence, not exact battery proof."

    $groups = Get-ParsedBatteryCandidateGroups ([Math]::Max($Tail, 200))
    if ($groups.Count -eq 0) {
        Write-Host "trace=no parsed HID battery candidates yet"
        Write-Host "decision=keep-display-na-until-exact-repeat-and-change"
        return
    }

    $coarse = @($groups | Where-Object { $_.Decoder -ieq "xbox_bt_flags" -and $_.Report -ieq "04" -and $_.Offset -eq "1" })
    foreach ($entry in ($coarse | Sort-Object -Property Count -Descending | Select-Object -First 3)) {
        Write-Host (Format-CandidateGroupLine $entry "coarse-bucket-not-1pct")
    }

    $genericXboxStatus = @($groups | Where-Object {
        $_.Decoder -match "^(percent100|percent255|nibble10)$" -and $_.Report -ieq "04"
    })
    foreach ($entry in ($genericXboxStatus | Sort-Object -Property Count -Descending | Select-Object -First 3)) {
        Write-Host (Format-CandidateGroupLine $entry "status-report-byte-needs-stronger-proof")
    }

    $dedicatedExact = @($groups | Where-Object {
        $_.Decoder -match "^(percent100|percent255|nibble10)$" -and $_.Report -match "^(01|11|12|21|31|81|82)$"
    })
    if ($dedicatedExact.Count -eq 0) {
        Write-Host "trace=no dedicated exact-percent candidate observed yet"
    }
    else {
        foreach ($entry in ($dedicatedExact | Sort-Object -Property @{ Expression = "Count"; Descending = $true }, @{ Expression = { Get-BatteryReportWatchRank $_.Report } } | Select-Object -First 5)) {
            Write-Host (Format-CandidateGroupLine $entry "exact-candidate-watch-repeat-and-change")
        }
    }

    Write-Host "decision=keep-display-na-until-exact-repeat-and-change"
}

function Show-X05ProModeTracePlan {
    Write-Section "X05 Pro mode trace plan"
    Write-Host "modeEvidence=public sources show X05 Pro has multiple PC modes; Xbox Wireless Controller name is mode evidence only."
    Write-Host "modePlan=xinput-name collect current Xbox-name trace and keep GameInput 100 as suspect unless HID proof is ready."
    Write-Host "modePlan=easysmx-name if D-input/EasySMX-name trace is available, compare the same report/offset candidates against the Xbox-name trace."
    Write-Host "compare=watch report=0x11 offset=13 first, then report=0x01 offset=13; require selectedProof gate=ready-after-repeat-and-movement."
    Write-Host "doNotUse=name-only,gameinput-100,xbox_bt_flags as exact battery proof."
}

function Show-X05ProBatteryFeasibilityLadder {
    Write-Section "X05 Pro battery feasibility ladder"
    Write-Host "feasibility=hardware low-battery LED proves at least threshold awareness, not host-visible percent."
    Write-Host "feasibility=chipset exact 1% needs battery measurement data plus firmware filtering; voltage-only estimates can be rough and non-linear."
    Write-Host "feasibility=firmware if firmware only drives LEDs or Xbox bucket flags, Windows cannot recover hidden 1% from software."
    Write-Host "feasibility=protocol BLE BAS or a vendor HID report must expose 0-100; current public X05 Pro docs do not publish either layout."
    Write-Host "x05ProLikely=protocol-or-firmware-limit unless report=0x11 offset=13 or report=0x01 offset=13 repeats and moves."
    Write-Host "proofToUpgrade=same report+offset+decoder repeat>=3 movement>=2 then runtime revalidation."
    Write-Host "proofToStop=after charge/drain traces, only xbox_bt_flags moves and exact candidates stay flat => exact 1% unsupported-by-exposed-protocol."
}

function Show-BatteryCandidateAnalysis {
    Write-Section "Battery candidate analysis"

    $items = Read-JsonLines $paths.ProbeTrace $Tail
    if ($items.Count -eq 0) {
        Write-Missing $paths.ProbeTrace
        return
    }

    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            continue
        }

        $top = @(Get-CandidateLines $item) | Select-Object -First 20
        if ($top.Count -eq 0) {
            continue
        }

        foreach ($candidate in $top) {
            $text = [string]$candidate
            $match = [regex]::Match(
                $text,
                "^(?<decoder>[^@]+)@0x(?<report>[0-9A-Fa-f]{2})\[off=(?<offset>\d+),pct=(?<percent>\d+),score=(?<score>\d+)")
            if (-not $match.Success) {
                continue
            }

            $decoder = $match.Groups["decoder"].Value
            $voteInfo = Get-CandidateVoteInfo $decoder $match.Groups["report"].Value $match.Groups["offset"].Value
            $trust = Resolve-CandidateTrust $decoder $voteInfo
            $voteText = if ($decoder -ieq "xbox_bt_flags") { "n/a" } else { "{0}/3" -f $voteInfo.VoteCount }
            $movement = if ($decoder -ieq "xbox_bt_flags") { "not-used" } else { Resolve-CandidateMovement $voteInfo }

            Write-Host ("{0} | decoder={1} report=0x{2} offset={3} percent={4} score={5} votes={6} movement={7} repeat={8} trust={9}" -f `
                (Format-Null (Get-JsonProperty $item "ts")), `
                (ConvertTo-SafeText $decoder), `
                (ConvertTo-SafeText $match.Groups["report"].Value), `
                (ConvertTo-SafeText $match.Groups["offset"].Value), `
                (ConvertTo-SafeText $match.Groups["percent"].Value), `
                (ConvertTo-SafeText $match.Groups["score"].Value), `
                (ConvertTo-SafeText $voteText), `
                (ConvertTo-SafeText $movement), `
                (Format-Null (Get-JsonProperty $item "observedReportIds")), `
                (ConvertTo-SafeText $trust))
        }
    }
}

function Show-BatteryCandidateSummary {
    Write-Section "Battery candidate summary"

    $items = Read-JsonLines $paths.ProbeTrace ([Math]::Max($Tail, 100))
    if ($items.Count -eq 0) {
        Write-Missing $paths.ProbeTrace
        return
    }

    $groups = @{}
    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            continue
        }

        foreach ($candidate in @(Get-CandidateLines $item)) {
            $text = [string]$candidate
            $match = [regex]::Match(
                $text,
                "^(?<decoder>[^@]+)@0x(?<report>[0-9A-Fa-f]{2})\[off=(?<offset>\d+),pct=(?<percent>\d+),score=(?<score>\d+)")
            if (-not $match.Success) {
                continue
            }

            $decoder = $match.Groups["decoder"].Value
            $report = $match.Groups["report"].Value.ToUpperInvariant()
            $offset = $match.Groups["offset"].Value
            $key = "{0}|{1}|{2}" -f $decoder, $report, $offset
            if (-not $groups.ContainsKey($key)) {
                $groups[$key] = [pscustomobject]@{
                    Decoder = $decoder
                    Report = $report
                    Offset = $offset
                    Count = 0
                    Percents = New-Object System.Collections.Generic.List[int]
                    Scores = New-Object System.Collections.Generic.List[int]
                }
            }

            $entry = $groups[$key]
            $entry.Count++
            $entry.Percents.Add([int]$match.Groups["percent"].Value)
            $entry.Scores.Add([int]$match.Groups["score"].Value)
        }
    }

    if ($groups.Count -eq 0) {
        Write-Host "No parsed candidates in recent probe traces."
        return
    }

    foreach ($entry in ($groups.Values | Sort-Object -Property Count -Descending | Select-Object -First ([Math]::Max(1, $Tail)))) {
        $minPercent = ($entry.Percents | Measure-Object -Minimum).Minimum
        $maxPercent = ($entry.Percents | Measure-Object -Maximum).Maximum
        $maxScore = ($entry.Scores | Measure-Object -Maximum).Maximum
        $voteInfo = Get-CandidateVoteInfo $entry.Decoder $entry.Report $entry.Offset
        $validationState = Get-CandidateValidationState $entry.Decoder $voteInfo $entry.Count $minPercent $maxPercent
        $trust = $validationState.Trust
        $voteText = if ($entry.Decoder -ieq "xbox_bt_flags") { "n/a" } else { "{0}/3" -f $voteInfo.VoteCount }
        $movement = $validationState.Movement
        Write-Host ("decoder={0} report=0x{1} offset={2} seen={3} percentRange={4}-{5} maxScore={6} votes={7} movement={8} trust={9}" -f `
            (ConvertTo-SafeText $entry.Decoder), `
            (ConvertTo-SafeText $entry.Report), `
            (ConvertTo-SafeText $entry.Offset), `
            (ConvertTo-SafeText $entry.Count), `
            (ConvertTo-SafeText $minPercent), `
            (ConvertTo-SafeText $maxPercent), `
            (ConvertTo-SafeText $maxScore), `
            (ConvertTo-SafeText $voteText), `
            (ConvertTo-SafeText $movement), `
            (ConvertTo-SafeText $trust))
    }
}

function Show-BatteryCandidateTimeline {
    Write-Section "Battery candidate timeline"

    $items = Read-JsonLines $paths.ProbeTrace ([Math]::Max($Tail, 200))
    if ($items.Count -eq 0) {
        Write-Missing $paths.ProbeTrace
        return
    }

    $groups = @{}
    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            continue
        }

        $timestamp = [string](Get-JsonProperty $item "ts")
        foreach ($candidate in @(Get-CandidateLines $item)) {
            $text = [string]$candidate
            $match = [regex]::Match(
                $text,
                "^(?<decoder>[^@]+)@0x(?<report>[0-9A-Fa-f]{2})\[off=(?<offset>\d+),pct=(?<percent>\d+),score=(?<score>\d+)")
            if (-not $match.Success) {
                continue
            }

            $decoder = $match.Groups["decoder"].Value
            $report = $match.Groups["report"].Value.ToUpperInvariant()
            $offset = $match.Groups["offset"].Value
            $key = "{0}|{1}|{2}" -f $decoder, $report, $offset
            if (-not $groups.ContainsKey($key)) {
                $groups[$key] = [pscustomobject]@{
                    Decoder = $decoder
                    Report = $report
                    Offset = $offset
                    FirstSeen = $timestamp
                    LastSeen = $timestamp
                    Count = 0
                    Percents = New-Object System.Collections.Generic.List[int]
                    Scores = New-Object System.Collections.Generic.List[int]
                }
            }

            $entry = $groups[$key]
            if ([string]::CompareOrdinal($timestamp, [string]$entry.FirstSeen) -lt 0) {
                $entry.FirstSeen = $timestamp
            }
            if ([string]::CompareOrdinal($timestamp, [string]$entry.LastSeen) -gt 0) {
                $entry.LastSeen = $timestamp
            }
            $entry.Count++
            $entry.Percents.Add([int]$match.Groups["percent"].Value)
            $entry.Scores.Add([int]$match.Groups["score"].Value)
        }
    }

    if ($groups.Count -eq 0) {
        Write-Host "No parsed candidate timeline in recent probe traces."
        return
    }

    foreach ($entry in ($groups.Values | Sort-Object -Property Count -Descending | Select-Object -First ([Math]::Max(1, $Tail)))) {
        $minPercent = ($entry.Percents | Measure-Object -Minimum).Minimum
        $maxPercent = ($entry.Percents | Measure-Object -Maximum).Maximum
        $maxScore = ($entry.Scores | Measure-Object -Maximum).Maximum
        $voteInfo = Get-CandidateVoteInfo $entry.Decoder $entry.Report $entry.Offset
        $validationState = Get-CandidateValidationState $entry.Decoder $voteInfo $entry.Count $minPercent $maxPercent
        $trust = $validationState.Trust
        $voteText = if ($entry.Decoder -ieq "xbox_bt_flags") { "n/a" } else { "{0}/3" -f $voteInfo.VoteCount }
        $movement = $validationState.Movement
        Write-Host ("decoder={0} report=0x{1} offset={2} first={3} last={4} seen={5} percentRange={6}-{7} maxScore={8} votes={9} movement={10} trust={11}" -f `
            (ConvertTo-SafeText $entry.Decoder), `
            (ConvertTo-SafeText $entry.Report), `
            (ConvertTo-SafeText $entry.Offset), `
            (ConvertTo-SafeText $entry.FirstSeen), `
            (ConvertTo-SafeText $entry.LastSeen), `
            (ConvertTo-SafeText $entry.Count), `
            (ConvertTo-SafeText $minPercent), `
            (ConvertTo-SafeText $maxPercent), `
            (ConvertTo-SafeText $maxScore), `
            (ConvertTo-SafeText $voteText), `
            (ConvertTo-SafeText $movement), `
            (ConvertTo-SafeText $trust))
    }
}

function Show-RuntimeTrace {
    Write-Section "Runtime raw/resolved battery traces"

    $items = Read-JsonLines $paths.RuntimeTrace $Tail
    if ($items.Count -eq 0) {
        Write-Missing $paths.RuntimeTrace
        Write-Host "Run the rebuilt test.exe for at least 60 seconds first."
        return
    }

    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            Write-Host ("malformed: {0}" -f (Format-Null (Get-JsonProperty $item "raw")))
            continue
        }

        Write-Host ("{0} | build={1}" -f `
            (Format-Null (Get-JsonProperty $item "ts")), `
            (Format-Null (Get-JsonProperty $item "buildStamp")))
        foreach ($stage in @("raw", "resolved")) {
            foreach ($reading in @(Get-JsonProperty $item $stage)) {
                Write-Host ("  {0}: {1} percent={2} raw={3} confidence={4} state={5} suspect={6} reason={7} name={8} model={9} addr={10}" -f `
                    $stage, `
                    (Format-Null (Get-JsonProperty $reading "sourceKind")), `
                    (Format-Null (Get-JsonProperty $reading "percent")), `
                    (Format-Null (Get-JsonProperty $reading "rawMetric")), `
                    (Format-Null (Get-JsonProperty $reading "confidence")), `
                    (Format-Null (Get-JsonProperty $reading "displayState")), `
                    (Format-Null (Get-JsonProperty $reading "isBatterySuspect")), `
                    (Format-Null (Get-JsonProperty $reading "reasonCode")), `
                    (Format-DeviceName (Get-JsonProperty $reading "displayName")), `
                    (Format-ProfileId (Get-JsonProperty $reading "modelKey")), `
                    (Format-Null (Get-JsonProperty $reading "address")))
            }
        }
    }
}

function Show-Observations {
    Write-Section "Recent battery observations"

    $items = Read-JsonLines $paths.Observations $Tail
    if ($items.Count -eq 0) {
        Write-Missing $paths.Observations
        return
    }

    foreach ($item in $items) {
        if (Get-JsonProperty $item "parseError") {
            Write-Host ("malformed: {0}" -f (Format-Null (Get-JsonProperty $item "raw")))
            continue
        }

        Write-Host ("{0} | source={1} percent={2} raw={3} charging={4} reason={5} model={6} addr={7}" -f `
            (Format-Null (Get-JsonProperty $item "observedAt")), `
            (Format-Null (Get-JsonProperty $item "sourceKind")), `
            (Format-Null (Get-JsonProperty $item "derivedPercent")), `
            (Format-Null (Get-JsonProperty $item "rawMetric")), `
            (Format-Null (Get-JsonProperty $item "isCharging")), `
            (Format-Null (Get-JsonProperty $item "reasonCode")), `
            (Format-ProfileId (Get-JsonProperty $item "modelKey")), `
            (Format-Null (Get-JsonProperty $item "address")))
    }
}

function Resolve-ProfilePolicy {
    param([object]$Value)

    $vid = [string](Get-JsonProperty $Value "vendorId")
    $decoder = [string](Get-JsonProperty $Value "decoder")
    $identity = [string](Get-JsonProperty $Value "identityKey")
    $validationKind = [string](Get-JsonProperty $Value "validationKind")
    $validationCountRaw = Get-JsonProperty $Value "validationCount"
    $validationCount = 0
    if ($null -ne $validationCountRaw) {
        [void][int]::TryParse([string]$validationCountRaw, [ref]$validationCount)
    }

    if ($decoder -ieq "xbox_bt_flags") {
        return "will-quarantine-coarse"
    }

    if ($vid -ieq "045E" -and $decoder -match "^(percent100|percent255|nibble10)$") {
        if ([string]::IsNullOrWhiteSpace($identity)) {
            return "will-quarantine-no-identity"
        }

        if ($validationKind -ine "repeated_exact_hid" -or $validationCount -lt 3) {
            return "will-quarantine-needs-repeat"
        }
    }

    return "active-ok"
}

function Resolve-ProfileRuntimeGuard {
    param([object]$Value)

    $decoder = [string](Get-JsonProperty $Value "decoder")
    $validationKind = [string](Get-JsonProperty $Value "validationKind")
    $validationCountRaw = Get-JsonProperty $Value "validationCount"
    $validationCount = 0
    if ($null -ne $validationCountRaw) {
        [void][int]::TryParse([string]$validationCountRaw, [ref]$validationCount)
    }

    if ($decoder -ieq "xbox_bt_flags") {
        return "runtime-na-coarse-bucket"
    }

    if ($decoder -match "^(percent100|percent255|nibble10)$") {
        if ($validationKind -ieq "repeated_exact_hid" -and $validationCount -ge 3) {
            return "runtime-can-use-if-revalidated"
        }

        return "runtime-will-hold-on-xbox-name"
    }

    return "runtime-unknown-decoder"
}

function Show-ProfileFile {
    param(
        [string]$Title,
        [string]$Path
    )

    Write-Section $Title

    $json = Read-JsonFile $Path
    if ($null -eq $json) {
        Write-Missing $Path
        return
    }

    if (Get-JsonProperty $json "parseError") {
        Write-Host ("parse failed: {0}" -f (Format-Null (Get-JsonProperty $json "message")))
        return
    }

    $properties = @($json.PSObject.Properties)
    if ($properties.Count -eq 0) {
        Write-Host "empty"
        return
    }

    Write-Host ("entries={0}" -f $properties.Count)
    foreach ($property in ($properties | Select-Object -First ([Math]::Max(1, $Tail)))) {
        $value = $property.Value
        Write-Host ("{0} | vid={1} pid={2} report={3} offset={4} decoder={5} score={6} confidence={7} state={8} validation={9}/{10} policy={11} runtime={12} identity={13}" -f `
            (Format-ProfileId $property.Name), `
            (Format-ProfileId (Get-JsonProperty $value "vendorId")), `
            (Format-ProfileId (Get-JsonProperty $value "productId")), `
            (Format-Null (Get-JsonProperty $value "reportId")), `
            (Format-Null (Get-JsonProperty $value "offset")), `
            (Format-Null (Get-JsonProperty $value "decoder")), `
            (Format-Null (Get-JsonProperty $value "score")), `
            (Format-Null (Get-JsonProperty $value "confidence")), `
            (Format-Null (Get-JsonProperty $value "state")), `
            (Format-Null (Get-JsonProperty $value "validationKind")), `
            (Format-Null (Get-JsonProperty $value "validationCount")), `
            (Format-Null (Resolve-ProfilePolicy $value)), `
            (Format-Null (Resolve-ProfileRuntimeGuard $value)), `
            (Format-ProfileId (Get-JsonProperty $value "identityKey")))
    }
}

function Show-HealthFile {
    param(
        [string]$Path
    )

    Write-Section "Profile health"

    $json = Read-JsonFile $Path
    if ($null -eq $json) {
        Write-Missing $Path
        return
    }

    if (Get-JsonProperty $json "parseError") {
        Write-Host ("parse failed: {0}" -f (Format-Null (Get-JsonProperty $json "message")))
        return
    }

    $properties = @($json.PSObject.Properties)
    if ($properties.Count -eq 0) {
        Write-Host "empty"
        return
    }

    Write-Host ("entries={0}" -f $properties.Count)
    foreach ($property in ($properties | Select-Object -First ([Math]::Max(1, $Tail)))) {
        $value = $property.Value
        Write-Host ("{0} | noSignal={1} weak={2} mismatch={3} success={4} lastHealthy={5}" -f `
            (Format-ProfileId $property.Name), `
            (Format-Null (Get-JsonProperty $value "noSignalStrike")), `
            (Format-Null (Get-JsonProperty $value "weakSignalStrike")), `
            (Format-Null (Get-JsonProperty $value "mismatchStrike")), `
            (Format-Null (Get-JsonProperty $value "consecutiveSuccessCount")), `
            (Format-Null (Get-JsonProperty $value "lastHealthyAt")))
    }
}

function Show-Votes {
    Write-Section "Pending candidate votes and cooldowns"

    $json = Read-JsonFile $paths.Votes
    if ($null -eq $json) {
        Write-Missing $paths.Votes
        return
    }

    if (Get-JsonProperty $json "parseError") {
        Write-Host ("parse failed: {0}" -f (Format-Null (Get-JsonProperty $json "message")))
        return
    }

    $votes = Get-JsonProperty $json "votes"
    $cooldowns = Get-JsonProperty $json "cooldowns"
    $voteProps = if ($null -eq $votes) { @() } else { @($votes.PSObject.Properties) }
    $cooldownProps = if ($null -eq $cooldowns) { @() } else { @($cooldowns.PSObject.Properties) }

    Write-Host ("votes={0} cooldowns={1}" -f $voteProps.Count, $cooldownProps.Count)
    foreach ($property in ($voteProps | Select-Object -First ([Math]::Max(1, $Tail)))) {
        $value = $property.Value
        Write-Host ("vote {0} | score={1} count={2} evidence={3} last={4} stats={5}" -f `
            (Format-Null $property.Name), `
            (Format-Null (Get-JsonProperty $value "score")), `
            (Format-Null (Get-JsonProperty $value "voteCount")), `
            (Format-Null (Get-JsonProperty $value "evidenceType")), `
            (Format-Null (Get-JsonProperty $value "lastSeenAt")), `
            (Format-Null (Get-JsonProperty $value "lastValidationStats")))
    }
    foreach ($property in ($cooldownProps | Select-Object -First ([Math]::Max(1, $Tail)))) {
        Write-Host ("cooldown {0} until {1}" -f (Format-Null $property.Name), (Format-Null $property.Value))
    }
}

Write-Host "Third-party gamepad battery diagnostics"
Write-Host "Data folder: %APPDATA%\Bloss"
Write-Host "Raw IDs: $($ShowRawIds.IsPresent)"

if ($OpenLogFolder) {
    if (Test-Path -LiteralPath $blossData) {
        Invoke-Item -LiteralPath $blossData
    }
}

if (-not $SkipConnectedDevices) {
    Show-ConnectedGamepadHints
}
Show-ProviderTrace
Show-RuntimeTrace
Show-ProbeTrace
Show-OpenSourceBatteryComparison
Show-X05ProModeTracePlan
Show-X05ProBatteryFeasibilityLadder
Show-BatteryCandidateClusters
Show-BatteryCandidateProofReadiness
Show-BatterySignalContrast
Show-BatteryCandidateDecisionMatrix
Show-BatteryNextEvidencePlan
Show-BatteryCandidateAnalysis
Show-BatteryCandidateSummary
Show-BatteryCandidateTimeline
Show-Observations
Show-ProfileFile -Title "Learned HID profiles" -Path $paths.Profiles
Show-ProfileFile -Title "Quarantined HID profiles" -Path $paths.Quarantine
Show-HealthFile -Path $paths.Health
Show-Votes

Write-Section "Override profile file"
if (Test-Path -LiteralPath $paths.Overrides) {
    Write-Host ("exists: {0}" -f (ConvertTo-SafeText $paths.Overrides))
}
else {
    Write-Missing $paths.Overrides
}

Write-Host ""
Write-Host "Next check:"
Write-Host "1. If probe traces are missing, run test.exe and use the battery collection/probe flow once."
Write-Host "2. If probe succeeds but profiles are missing, the candidate was not trusted enough to save."
Write-Host "3. If profiles exist but observations are missing, runtime HID revalidation is failing."
Write-Host "4. If observations exist but the UI shows N/A, BatteryEvidenceResolver likely rejected conflicting or weak evidence."
