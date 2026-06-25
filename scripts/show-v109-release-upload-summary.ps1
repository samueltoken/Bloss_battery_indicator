$ErrorActionPreference = "Stop"
$previousChecklistPath = $env:BLOSS_MANUAL_CHECKLIST_PATH
try {
    $env:BLOSS_MANUAL_CHECKLIST_PATH = Join-Path (Split-Path -Parent $PSScriptRoot) "manual-verification-v109.md"
    & (Join-Path $PSScriptRoot "show-v108-release-upload-summary.ps1") @args
}
finally {
    if ($null -eq $previousChecklistPath) {
        Remove-Item Env:\BLOSS_MANUAL_CHECKLIST_PATH -ErrorAction SilentlyContinue
    }
    else {
        $env:BLOSS_MANUAL_CHECKLIST_PATH = $previousChecklistPath
    }
}
