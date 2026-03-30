# Bloss (Bluetooth Battery Indicator)

Windows desktop widget that shows battery level for connected Bluetooth devices.

## Folder Layout

- `BluetoothBatteryWidget.App` : app source code
- `BluetoothBatteryWidget.Core` : shared logic
- `BluetoothBatteryWidget.Tests` : test code
- `build\scripts` : build commands
- `build\installer` : installer template (`.iss`)
- `release` : final output files (`Bloss.exe`, `setup.exe`)
- `artifacts\staging` : temporary build files (auto cleanup)

## Quick Build

```powershell
dotnet build .\BluetoothBatteryWidget.sln -c Release
dotnet test .\BluetoothBatteryWidget.Tests\BluetoothBatteryWidget.Tests.csproj -c Release
```

## Portable EXE Build

```powershell
.\build\scripts\build-portable.ps1 -Configuration Release -Runtime win-x64
```

Output:
- `release\portable\Bloss.exe`

Optional zip:

```powershell
.\build\scripts\build-portable.ps1 -Configuration Release -Runtime win-x64 -ZipOutput
```

Zip output:
- `release\portable\Bloss-portable-win-x64.zip`

## Installer Build

Inno Setup 6 (`ISCC.exe`) is required.

```powershell
.\build\scripts\build-installer.ps1 -AppVersion 1.0.1
```

Output:
- `release\installer\setup.exe`
- Start Menu item: `Uninstall Bloss`
- Install folder file: `uninstall.exe`

## Notes

- Main app executable name: `Bloss.exe`
- User settings path: `%AppData%\Bloss\settings.json`
