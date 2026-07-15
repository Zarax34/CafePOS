$publishDir = "E:\my-projects\cafe-system\publish"
$outputFile = "E:\my-projects\cafe-system\setup.iss"

$allFiles = Get-ChildItem -Recurse -File $publishDir

$filesEntries = @()
$dirsToCreate = @()

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($publishDir.Length + 1)
    $dirPath = Split-Path $relativePath -Parent
    if ($dirPath) {
        $filesEntries += "Source: ""$($file.FullName)""; DestDir: ""{app}\$dirPath""; Flags: ignoreversion"
        if (-not $dirsToCreate.Contains($dirPath)) {
            $dirsToCreate += $dirPath
        }
    } else {
        $filesEntries += "Source: ""$($file.FullName)""; DestDir: ""{app}""; Flags: ignoreversion"
    }
}

$dirmap = @{}
foreach ($dir in $dirsToCreate) {
    $parts = $dir -split '\\'
    for ($i = 1; $i -le $parts.Length; $i++) {
        $sub = $parts[0..($i-1)] -join '\'
        if (-not $dirmap.ContainsKey($sub)) {
            $dirmap[$sub] = $true
        }
    }
}

$issContent = @"
#define MyAppName "CafePOS"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "CafePOS"
#define MyAppExeName "CafePOS.exe"

[Setup]
AppId={{16686C0C-1E6B-4F6D-8A3F-5E8E5F5F5E5E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=CafePOS_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableDirPage=no
UsePreviousAppDir=yes
UninstallDisplayIcon={app}\Assets\app_icon.ico
CloseApplications=force
RestartApplications=no

ShowLanguageDialog=no

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
$(($filesEntries -join "`r`n"))
Source: "E:\my-projects\cafe-system\Assets\app_icon.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Dirs]
$(($dirmap.Keys | ForEach-Object { "Name: ""{app}\$_""; Permissions: users-modify" }) -join "`r`n")

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\app_icon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\app_icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not DirExists(ExpandConstant('{app}\Data')) then
      CreateDir(ExpandConstant('{app}\Data'));
    if not DirExists(ExpandConstant('{app}\Attachments')) then
      CreateDir(ExpandConstant('{app}\Attachments'));
  end;
end;
"@

$issContent | Out-File -Encoding utf8 $outputFile
Write-Host "Generated: $outputFile"
