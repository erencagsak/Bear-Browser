; =====================================================================
; Bear Browser - Inno Setup Kurulum Komut Dosyası
; =====================================================================

#define MyAppName "Bear Browser"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Bear Browser"
#define MyAppExeName "Bear-Browser.exe"
#define MyAppIcon "Assets\Bear-Browser-Logo.ico"

[Setup]
; Uygulama Kimliği (Benzersiz GUID)
AppId={{5E2A89F0-18D2-4E57-9A1A-BEARBROWSER01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=BearBrowserSetup
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Publish klasöründeki tüm dosyaları ve alt klasörleri kurulum paketine dahil et
Source: "bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\Bear-Browser-Logo.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\Bear-Browser-Logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Windows Varsayılan Tarayıcı (Default Apps) Kayıtları
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\{#MyAppName}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\{#MyAppName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Assets\Bear-Browser-Logo.ico,0"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\{#MyAppName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\Clients\StartMenuInternet\{#MyAppName}\Capabilities"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Hızlı, gizlilik odaklı modern web tarayıcısı."
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\{#MyAppName}\Capabilities\URLAssociations"; ValueType: string; ValueName: "http"; ValueData: "BearHTML"
Root: HKCU; Subkey: "Software\Clients\StartMenuInternet\{#MyAppName}\Capabilities\URLAssociations"; ValueType: string; ValueName: "https"; ValueData: "BearHTML"
Root: HKCU; Subkey: "Software\Classes\BearHTML"; ValueType: string; ValueName: ""; ValueData: "Bear Browser HTML Belgesi"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\BearHTML\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Assets\Bear-Browser-Logo.ico,0"
Root: HKCU; Subkey: "Software\Classes\BearHTML\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
