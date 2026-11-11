; arddong ArdView 설치 파일 = 사용자 권한 설치 + 그림 파일 「연결 프로그램」 후보 등록
#define AppVer "1.0.0"

[Setup]
AppId={{6B1F3C2A-8E47-4D9B-9C55-A2D7E1F40B63}
AppName=ArdView
AppVersion={#AppVer}
AppPublisher=arddong
DefaultDirName={autopf}\ArdView
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=yes
OutputDir=dist
OutputBaseFilename=ArdView_Setup_{#AppVer}
SetupIconFile=src\ardview.ico
UninstallDisplayIcon={app}\ArdView.exe
UninstallDisplayName=ArdView
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
CloseApplications=yes

[Languages]
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
Source: "publish\ArdView.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\ArdView"; Filename: "{app}\ArdView.exe"

[Registry]
; 연결 프로그램 종류
Root: HKCU; Subkey: "Software\Classes\ArdView.Image"; ValueType: string; ValueName: ""; ValueData: "그림 파일"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ArdView.Image\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ArdView.exe,0"
Root: HKCU; Subkey: "Software\Classes\ArdView.Image\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ArdView.exe"" ""%1"""

; 「연결 프로그램」 목록과 설정 → 기본 앱 에 이름이 나오게
Root: HKCU; Subkey: "Software\Classes\Applications\ArdView.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "ArdView"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\ArdView.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ArdView.exe"" ""%1"""
Root: HKCU; Subkey: "Software\ArdView"; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\ArdView\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "ArdView"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\ArdView\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "광고 없는 이미지 보기"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "ArdView"; ValueData: "Software\ArdView\Capabilities"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.jpg\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.jpeg\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.jfif\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jfif"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.jpe\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpe"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.png\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.gif\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.webp\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.bmp\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.dib\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dib"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.ico\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ico"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.tif\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.tiff\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "ArdView.Image"

[Run]
Filename: "{app}\ArdView.exe"; Description: "ArdView 열기"; Flags: nowait postinstall skipifsilent
