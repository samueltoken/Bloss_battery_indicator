param(
    [string[]]$AllowedEmail = @("lamsaiku65@gmail.com"),
    [string]$SummaryPath
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$gitIgnorePath = Join-Path $projectRoot ".gitignore"
$forbiddenTrackedPatterns = @(
    '(^|/)artifacts/',
    '(^|/)release/',
    '(^|/)scan-results/',
    '(^|/)test\.exe$',
    '(^|/)setup\.exe$',
    '(^|/)setup\.exe\.sha256$',
    '(^|/)howtorelease\.md$',
    '(^|/)gitguide\.md$',
    '(^|/)for107\.md$',
    '(^|/)manual-verification-v107\.md$',
    '(^|/)manual-verification-v108\.md$',
    '(^|/)manual-verification-v109\.md$',
    '(^|/)problems\.md$',
    '(^|/)solutions\.md$',
    '(^|/)steadystatus\.md$',
    '(^|/)steamcontrollerguide\.md$',
    '(^|/)thirdpartygamepadguide\.md$',
    '(^|/)todolist\.md$',
    '(^|/)todo\.md$'
)
$requiredIgnoreNeedles = @(
    'artifacts/',
    '/release/',
    '/problems.md',
    '/solutions.md',
    '/manual-verification-v107.md',
    '/manual-verification-v108.md',
    '/manual-verification-v109.md',
    '/steamcontrollerguide.md',
    '/thirdpartygamepadguide.md',
    '/for107.md',
    '/gitguide.md',
    '/howtorelease.md',
    '/scan-results/'
)

function Invoke-GitLines {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & git @Arguments 2>&1 | ForEach-Object { $_.ToString() }
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return @($output)
}

function Get-TrackedEmailFindings {
    $emailPattern = '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}'
    $grepOutput = Invoke-GitLines -Arguments @(
        'grep',
        '-n',
        '-E',
        $emailPattern,
        '--',
        '.',
        ':!bin',
        ':!obj'
    )

    foreach ($line in $grepOutput) {
        foreach ($match in [regex]::Matches($line, $emailPattern)) {
            [pscustomobject]@{
                Email = $match.Value
                Line = $line
                Allowed = $AllowedEmail -contains $match.Value
            }
        }
    }
}

Push-Location $projectRoot
try {
    if (-not (Test-Path -LiteralPath $gitIgnorePath -PathType Leaf)) {
        throw ".gitignore was not found: $gitIgnorePath"
    }

    $trackedFiles = Invoke-GitLines -Arguments @('ls-files')
    $trackedForbidden = foreach ($path in $trackedFiles) {
        foreach ($pattern in $forbiddenTrackedPatterns) {
            if ($path -match $pattern) {
                [pscustomobject]@{
                    Path = $path
                    Pattern = $pattern
                }
            }
        }
    }

    $gitIgnore = Get-Content -Encoding UTF8 -LiteralPath $gitIgnorePath -Raw
    $missingIgnoreRules = @($requiredIgnoreNeedles | Where-Object {
        $gitIgnore.IndexOf($_, [System.StringComparison]::Ordinal) -lt 0
    })

    $emailFindings = @(Get-TrackedEmailFindings)
    $unexpectedEmails = @($emailFindings | Where-Object { -not $_.Allowed })

    $result = [pscustomobject]@{
        ProjectRoot = $projectRoot
        TrackedFileCount = $trackedFiles.Count
        ForbiddenTrackedFiles = @($trackedForbidden).Count
        MissingIgnoreRules = $missingIgnoreRules.Count
        TrackedEmailFindings = $emailFindings.Count
        UnexpectedEmails = $unexpectedEmails.Count
        AllowedEmails = $AllowedEmail -join ", "
    }

    $result | Format-List

    if (@($trackedForbidden).Count -gt 0) {
        Write-Host "Forbidden tracked files:"
        $trackedForbidden | Format-Table -AutoSize
    }

    if ($missingIgnoreRules.Count -gt 0) {
        Write-Host "Missing .gitignore rules:"
        $missingIgnoreRules | ForEach-Object { Write-Host "- $_" }
    }

    if ($unexpectedEmails.Count -gt 0) {
        Write-Host "Unexpected tracked email findings:"
        $unexpectedEmails | Format-Table -AutoSize Email, Line
    }

    $failures = @()
    if (@($trackedForbidden).Count -gt 0) {
        $failures += "Forbidden files are tracked by git."
    }

    if ($missingIgnoreRules.Count -gt 0) {
        $failures += ".gitignore is missing release-safety rules."
    }

    if ($unexpectedEmails.Count -gt 0) {
        $failures += "Unexpected email address found in tracked files."
    }

    if ($failures.Count -gt 0) {
        throw "Git publish safety verification failed: $($failures -join ' ')"
    }

    if (-not [string]::IsNullOrWhiteSpace($SummaryPath)) {
        $summaryFullPath = [System.IO.Path]::GetFullPath($SummaryPath)
        $summaryDirectory = Split-Path -Parent $summaryFullPath
        New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
        $summary = [ordered]@{
            ProjectRoot = $projectRoot
            TrackedFileCount = $trackedFiles.Count
            ForbiddenTrackedFiles = @($trackedForbidden).Count
            MissingIgnoreRules = $missingIgnoreRules.Count
            TrackedEmailFindings = $emailFindings.Count
            UnexpectedEmails = $unexpectedEmails.Count
            AllowedEmails = $AllowedEmail
            GeneratedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss K")
        }

        $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryFullPath -Encoding UTF8
    }

    Write-Host "Git publish safety verification passed."
}
finally {
    Pop-Location
}
