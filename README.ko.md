<p align="center">
  <img src="BluetoothBatteryWidget.App/Assets/app.ico" alt="Bloss 아이콘" width="96" />
</p>

<h1 align="center">Bloss</h1>
<p align="center">Windows용 블루투스 배터리 표시 위젯</p>

<p align="center">
  <a href="./README.ko.md"><b>KOR</b></a>
  &nbsp;|&nbsp;
  <a href="./README.en.md"><b>ENG</b></a>
</p>

## 소개
Bloss는 연결된 블루투스 기기의 배터리 상태를 바탕화면에서 빠르게 확인할 수 있도록 만든 앱입니다.

## 폴더 구조
- `BluetoothBatteryWidget.App`: 앱 화면/동작 코드
- `BluetoothBatteryWidget.Core`: 공통 로직
- `BluetoothBatteryWidget.Tests`: 테스트 코드
- `build/scripts`: 빌드 스크립트
- `build/installer`: 설치 파일 템플릿
- `release`: 최종 배포 파일 출력 위치

## 빌드
```powershell
dotnet build .\BluetoothBatteryWidget.sln -c Release
dotnet test .\BluetoothBatteryWidget.Tests\BluetoothBatteryWidget.Tests.csproj -c Release
```

## 무설치 실행 파일 만들기
```powershell
.\build\scripts\build-portable.ps1 -Configuration Release -Runtime win-x64
```

출력:
- `release\portable\Bloss.exe`

## 설치 파일 만들기
Inno Setup 6 (`ISCC.exe`) 필요

```powershell
.\build\scripts\build-installer.ps1 -AppVersion 1.0.1
```

출력:
- `release\installer\setup.exe`
- 시작 메뉴: `Uninstall Bloss`
- 설치 폴더: `uninstall.exe`

## 업데이트
- 앱 설정창의 `업데이트` 버튼은 GitHub Release의 최신 `setup.exe`를 내려받아 설치합니다.
- 릴리즈마다 `setup.exe`를 자산(Asset)으로 첨부해야 자동 업데이트가 정상 동작합니다.
