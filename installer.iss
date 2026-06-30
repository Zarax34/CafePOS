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
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer-output
OutputBaseFilename=CafePOS-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableProgramGroupPage=yes
SetupIconFile=Assets\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "اختصارات:"; Flags: checkedonce

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Ensure Data directory exists
Source: "Data\*"; DestDir: "{app}\Data"; Flags: ignoreversion recursesubdirs createallsubdirs onlyifdoesntexist

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
