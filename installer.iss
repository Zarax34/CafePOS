; Inno Setup script for CafePOS
; Compile with: ISCC installer.iss

#define MyAppName "كافيه - نظام نقاط البيع"
#define MyAppShortName "CafePOS"
#define MyAppVersion "1.0"
#define MyAppPublisher "Cafe System"
#define MyAppExeName "CafePOS.exe"

[Setup]
AppId={{8A2E5B3C-9D4F-4E6B-8A1C-3F5D7E9B2C4A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://cafe-system.example.com
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=نظام إدارة الكافيه الذكي
DefaultDirName={autopf64}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer-output
OutputBaseFilename=CafePOS-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
UsedUserAreasWarning=no
SetupIconFile=Assets\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "اختصارات:"; Flags: checkedonce

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Give Users group modify permission on Data folder so the app can write the database
Name: "{app}\Data"; Permissions: users-modify

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\إلغاء تثبيت {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "تشغيل التطبيق الآن"; Flags: postinstall nowait skipifsilent shellexec

[UninstallRun]
; Clean up user data on uninstall (optional - commented out to preserve data)
; Filename: "{cmd}"; Parameters: "/C rmdir /S /Q ""{app}\Data"""

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Warm-up: start app briefly, wait 5s, then kill it
    // This lets Defender scan the files while installer is finishing
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), '', '', SW_HIDE,
         ewNoWait, ResultCode);
    Sleep(5000);
    Exec('taskkill', ExpandConstant('/f /im {#MyAppExeName}'), '',
         SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
