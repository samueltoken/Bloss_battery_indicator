<p align="center">
  <img src="BluetoothBatteryWidget.App/Assets/app.ico" alt="Bloss Icon" width="96" />
</p>

<h1 align="center">Bloss</h1>
<p align="center">Bluetooth Battery Indicator for Windows</p>

<p align="center">
  <a href="./README.ko.md"><b>KOR</b></a>
  &nbsp;|&nbsp;
  <a href="./README.en.md"><b>ENG</b></a>
</p>

## Overview
Bloss is a desktop widget that helps users quickly check battery levels of connected Bluetooth devices.

## Folder Layout
- `BluetoothBatteryWidget.App`: app UI and runtime logic
- `BluetoothBatteryWidget.Core`: shared core logic
- `BluetoothBatteryWidget.Tests`: test project
- `build/scripts`: build scripts
- `build/installer`: installer template
- `release`: final distributable outputs

## Build
```powershell
dotnet build .\BluetoothBatteryWidget.sln -c Release
dotnet test .\BluetoothBatteryWidget.Tests\BluetoothBatteryWidget.Tests.csproj -c Release
```

## Build Portable EXE
```powershell
.\build\scripts\build-portable.ps1 -Configuration Release -Runtime win-x64
```

Output:
- `release\portable\Bloss.exe`

## Build Installer
Requires Inno Setup 6 (`ISCC.exe`).

```powershell
.\build\scripts\build-installer.ps1 -AppVersion 1.0.1
```

Output:
- `release\installer\setup.exe`
- Start menu entry: `Uninstall Bloss`
- Install folder file: `uninstall.exe`

## Update Flow
- The in-app `Update` button downloads the latest `setup.exe` from GitHub Releases and installs it.
- Each new release must include `setup.exe` as a release asset for auto-update to work.
