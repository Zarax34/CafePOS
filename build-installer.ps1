$ProjectRoot = $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot "publish"
$IssFile = Join-Path $ProjectRoot "setup.iss"
$IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"

Write-Host "=== CafePOS Installer Builder ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Publish the app
Write-Host "[1/3] Publishing application..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir\* -ErrorAction SilentlyContinue
}
dotnet publish "$ProjectRoot\CafePOS.csproj" -c Release -r win-x64 --self-contained true -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: dotnet publish failed." -ForegroundColor Red
    exit 1
}
Write-Host "OK" -ForegroundColor Green
Write-Host ""

# Step 2: Generate Inno Setup script
Write-Host "[2/3] Generating setup.iss..." -ForegroundColor Yellow
& "$ProjectRoot\generate-iss.ps1"
if (-not (Test-Path $IssFile)) {
    Write-Host "FAILED: setup.iss not generated." -ForegroundColor Red
    exit 1
}
Write-Host "OK" -ForegroundColor Green
Write-Host ""

# Step 3: Compile installer
Write-Host "[3/3] Compiling CafePOS_Setup.exe..." -ForegroundColor Yellow
if (-not (Test-Path $IsccPath)) {
    Write-Host "FAILED: Inno Setup not found at $IsccPath" -ForegroundColor Red
    exit 1
}
& $IsccPath $IssFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: ISCC compilation failed." -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "=== Done! ===" -ForegroundColor Cyan
$exe = Get-Item "$ProjectRoot\CafePOS_Setup.exe" -ErrorAction SilentlyContinue
if ($exe) {
    Write-Host "Installer: $($exe.FullName)" -ForegroundColor Green
    Write-Host ("Size: {0:N2} MB" -f ($exe.Length / 1MB)) -ForegroundColor Green
}
