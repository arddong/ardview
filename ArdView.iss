; arddong ArdView 설치 파일 = 사용자 권한 설치 + 그림 파일 「연결 프로그램」 후보 등록
#define AppVer "1.1.0"

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
Root: HKCU; Subkey: "Software\Classes\ArdView.Image"; ValueType: string; ValueName: ""; ValueData: "ArdView 그림"; Flags: uninsdeletekey
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
Root: HKCU; Subkey: "Software\Classes\.apng\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".apng"; ValueData: "ArdView.Image"
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
Root: HKCU; Subkey: "Software\Classes\.wbmp\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wbmp"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.heic\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heic"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.heif\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heif"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.avif\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avif"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.jxr\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jxr"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.wdp\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wdp"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.hdp\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".hdp"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.dds\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dds"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.psd\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".psd"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.tga\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tga"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.pnm\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pnm"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.pbm\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pbm"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.pgm\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pgm"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.ppm\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ppm"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.dng\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dng"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.cr2\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cr2"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.cr3\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cr3"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.crw\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".crw"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.nef\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".nef"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.nrw\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".nrw"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.orf\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".orf"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.rw2\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rw2"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.pef\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pef"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.sr2\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".sr2"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.arw\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".arw"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.raf\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".raf"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.srw\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".srw"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.cbz\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cbz"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.cbr\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cbr"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.cb7\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cb7"; ValueData: "ArdView.Image"
Root: HKCU; Subkey: "Software\Classes\.cbt\OpenWithProgids"; ValueType: string; ValueName: "ArdView.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\ArdView\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cbt"; ValueData: "ArdView.Image"

[Run]
Filename: "{app}\ArdView.exe"; Description: "ArdView 열기"; Flags: nowait postinstall skipifsilent
