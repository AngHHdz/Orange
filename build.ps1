# Builds Orange for release and packages the installer.
#   .\build.ps1            → dist\Orange-Setup-<version>.exe
#   .\build.ps1 -NoInstaller → only the self-contained app in .\publish
param([switch]$NoInstaller)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$version = ([xml](Get-Content 'Orange\Orange.csproj')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "Orange $version" -ForegroundColor DarkYellow

# Self-contained: runs on PCs without .NET installed.
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish Orange\Orange.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -o publish -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
$mb = [math]::Round((Get-ChildItem publish -Recurse | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "Published to .\publish ($mb MB)"

if ($NoInstaller) { return }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup not found. Install it with: winget install JRSoftware.InnoSetup' }

& $iscc /Q "/DMyAppVersion=$version" installer\Orange.iss
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }
Get-Item "dist\Orange-Setup-$version.exe" | ForEach-Object { Write-Host "Installer: $($_.FullName) ($([math]::Round($_.Length / 1MB)) MB)" -ForegroundColor Green }
