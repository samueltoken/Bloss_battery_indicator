param(
    [switch]$Delete,
    [switch]$NoFail
)

$ErrorActionPreference = "Stop"

$runKeyPath = "Software\Microsoft\Windows\CurrentVersion\Run"
$valueNames = @("Bloss", "BluetoothBatteryWidget")
$deleteRequested = $Delete.IsPresent
$currentUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value

if ($deleteRequested) {
    Write-Host "Delete mode requested. Only Bloss startup values named Bloss or BluetoothBatteryWidget will be removed."
}
else {
    Write-Host "Read-only check. No registry values will be changed unless -Delete is specified."
}

function Get-RunValueState {
    param(
        [Parameter(Mandatory = $true)]
        [Microsoft.Win32.RegistryKey]$RootKey,

        [Parameter(Mandatory = $true)]
        [string]$RootName,

        [Parameter(Mandatory = $true)]
        [string]$SubKeyPath
    )

    $key = $RootKey.OpenSubKey($SubKeyPath, $deleteRequested)
    foreach ($valueName in $valueNames) {
        $present = $false
        $data = $null
        $deleted = $false

        if ($key -ne $null) {
            $value = $key.GetValue($valueName, $null)
            $present = $null -ne $value
            if ($present) {
                $data = [string]$value
                if ($deleteRequested) {
                    $key.DeleteValue($valueName, $false)
                    $deleted = $true
                    $present = $false
                }
            }
        }

        [pscustomobject]@{
            Root = $RootName
            SubKey = $SubKeyPath
            ValueName = $valueName
            Present = $present
            Deleted = $deleted
            Data = $data
        }
    }

    if ($key -ne $null) {
        $key.Dispose()
    }
}

function Should-InspectLoadedUserHive {
    param([string]$HiveName)

    return (
        -not [string]::IsNullOrWhiteSpace($HiveName) -and
        -not $HiveName.Equals($currentUserSid, [System.StringComparison]::OrdinalIgnoreCase) -and
        $HiveName.StartsWith("S-1-5-21-", [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $HiveName.EndsWith("_Classes", [System.StringComparison]::OrdinalIgnoreCase)
    )
}

$results = @()
$currentUser = [Microsoft.Win32.Registry]::CurrentUser
$usersRoot = [Microsoft.Win32.Registry]::Users

$results += Get-RunValueState -RootKey $currentUser -RootName "HKEY_CURRENT_USER" -SubKeyPath $runKeyPath

foreach ($hiveName in $usersRoot.GetSubKeyNames()) {
    if (Should-InspectLoadedUserHive -HiveName $hiveName) {
        $results += Get-RunValueState `
            -RootKey $usersRoot `
            -RootName "HKEY_USERS\$hiveName" `
            -SubKeyPath "$hiveName\$runKeyPath"
    }
}

$found = @($results | Where-Object { $_.Present })

if ($results.Count -gt 0) {
    $results | Sort-Object Root, ValueName | Format-Table -AutoSize
}

if ($deleteRequested) {
    $deletedCount = @($results | Where-Object { $_.Deleted }).Count
    Write-Host "Autostart cleanup attempted. Deleted values: $deletedCount"
}

if ($found.Count -eq 0) {
    Write-Host "No Bloss autostart values found."
    exit 0
}

if (-not $NoFail) {
    $names = ($found | ForEach-Object { "$($_.Root)\$($_.SubKey)\$($_.ValueName)" }) -join "; "
    throw "Bloss autostart values remain: $names"
}

Write-Host "Bloss autostart values were found, but -NoFail was specified."
