$publishDir = "E:\my-projects\cafe-system\publish"
$outputFile = "E:\my-projects\cafe-system\installer.wxs"

$allFiles = Get-ChildItem -Recurse -File $publishDir

# Build directory tree and collect files per directory
$dirFiles = @{}
foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($publishDir.Length + 1)
    $dirPath = Split-Path $relativePath -Parent
    if (-not $dirPath) { $dirPath = "" }
    if (-not $dirFiles.ContainsKey($dirPath)) {
        $dirFiles[$dirPath] = @()
    }
    $dirFiles[$dirPath] += @{FullName = $file.FullName; Name = $file.Name; RelPath = $relativePath}
}

function Sanitize-Id {
    param([string]$id)
    $id = $id -replace '-', '_'
    $id = $id -replace '[^a-zA-Z0-9_.]', ''
    return $id
}

# Generate directory IDs
$dirIds = @{ "" = "INSTALLDIR" }
$dirXml = ""

# Root directories
$rootDirs = @{}
foreach ($dirPath in $dirFiles.Keys) {
    if ($dirPath -ne "" -and $dirPath -notmatch '\\') {
        $sanitized = Sanitize-Id $dirPath
        $dirId = "dir_$sanitized"
        $dirIds[$dirPath] = $dirId
        $rootDirs[$dirPath] = $dirPath
    }
}

# Nested directories
$nestedDirs = @{}
foreach ($dirPath in $dirFiles.Keys) {
    if ($dirPath -match '\\') {
        $parts = $dirPath -split '\\'
        $joined = ($parts | ForEach-Object { Sanitize-Id $_ }) -join '_'
        $dirId = "dir_$joined"
        $dirIds[$dirPath] = $dirId
        $parentPath = ($parts[0..($parts.Length-2)] -join '\')
        $parentId = $dirIds[$parentPath]
        $dirName = $parts[-1]
        if (-not $nestedDirs.ContainsKey($parentPath)) {
            $nestedDirs[$parentPath] = @()
        }
        $nestedDirs[$parentPath] += @{Id = $dirId; Name = $dirName}
    }
}

# Build directory XML
$dirXml += "      <Directory Id=""INSTALLDIR"" Name=""CafePOS"">`r`n"
foreach ($kv in $rootDirs.GetEnumerator()) {
    $sanitized = Sanitize-Id $kv.Key
    $dirXml += "        <Directory Id=""dir_$sanitized"" Name=""$($kv.Key)"" />`r`n"
}
# Handle nested dirs of INSTALLDIR
if ($nestedDirs.ContainsKey("")) {
    foreach ($child in $nestedDirs[""]) {
        $dirXml += "        <Directory Id=""$($child.Id)"" Name=""$($child.Name)"" />`r`n"
    }
}
$dirXml += "      </Directory>`r`n"

# Build components XML (all files grouped by directory)
$fileCounter = 0
$componentRefs = @()
$componentsXml = ""

foreach ($dirEntry in $dirFiles.GetEnumerator()) {
    $dirPath = $dirEntry.Key
    $dirId = $dirIds[$dirPath]
    $files = $dirEntry.Value

    foreach ($file in $files) {
        $fileCounter++
        $compId = "cmp$fileCounter"
        $fileId = "fil$fileCounter"
        $componentRefs += "      <ComponentRef Id=""$compId"" />"

        $componentsXml += "    <DirectoryRef Id=""$dirId"">`r`n"
        $componentsXml += "      <Component Id=""$compId"" Guid=""*"">`r`n"
        $componentsXml += "        <File Id=""$fileId"" Name=""$($file.Name)"" Source=""$($file.FullName)"" KeyPath=""yes"" />`r`n"
        $componentsXml += "      </Component>`r`n"
        $componentsXml += "    </DirectoryRef>`r`n"
    }
}

$wxsContent = @"
<?xml version='1.0' encoding='utf-8'?>
<Wix xmlns='http://wixtoolset.org/schemas/v4/wxs'>
  <Package
    Name='CafePOS'
    Manufacturer='CafePOS'
    Version='2.0.0'
    UpgradeCode='16686C0C-1E6B-4F6D-8A3F-5E8E5F5F5E5E'
    Scope='perMachine'>

    <MajorUpgrade DowngradeErrorMessage='A newer version of CafePOS is already installed.' Schedule='afterInstallFinalize' />

    <Icon Id='app.ico' SourceFile='E:\my-projects\cafe-system\Assets\app_icon.ico' />
    <Property Id='ARPPRODUCTICON' Value='app.ico' />
    <Property Id='ApplicationFolderName' Value='CafePOS' />
    <Property Id='WixAppFolder' Value='WixPerMachineFolder' />

    <StandardDirectory Id='ProgramMenuFolder'>
      <Directory Id='ApplicationProgramsFolder' Name='CafePOS'>
        <Component Id='Shortcuts' Guid='*'>
          <Shortcut Id='AppShortcut'
                    Name='CafePOS'
                    Description='CafePOS - POS System'
                    Target='[INSTALLDIR]CafePOS.exe'
                    WorkingDirectory='INSTALLDIR'
                    Icon='app.ico' />
          <RemoveFolder Id='RemoveProgramsFolder' Directory='ApplicationProgramsFolder' On='uninstall' />
          <RegistryValue Root='HKLM' Key='Software\CafePOS' Name='installed' Type='integer' Value='1' KeyPath='yes' />
        </Component>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id='DesktopFolder'>
      <Component Id='DesktopShortcut' Guid='*'>
        <Shortcut Id='DesktopAppShortcut'
                  Name='CafePOS'
                  Description='CafePOS - POS System'
                  Target='[INSTALLDIR]CafePOS.exe'
                  WorkingDirectory='INSTALLDIR'
                  Icon='app.ico' />
          <RegistryValue Root='HKLM' Key='Software\CafePOS' Name='desktop' Type='integer' Value='1' KeyPath='yes' />
      </Component>
    </StandardDirectory>

    <Feature Id='Main' Title='CafePOS' Level='1'>
      <ComponentRef Id='Shortcuts' />
      <ComponentRef Id='DesktopShortcut' />
$($componentRefs -join "`r`n")
    </Feature>

    <StandardDirectory Id='ProgramFiles6432Folder'>
$dirXml
    </StandardDirectory>
$componentsXml
  </Package>
</Wix>
"@

$wxsContent | Out-File -Encoding utf8 $outputFile
Write-Host "Generated: $outputFile"
