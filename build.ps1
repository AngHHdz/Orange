# Builds Orange for release and packages the installer.
#   .\build.ps1              -> dist\Orange-Setup-<version>.exe (with ffmpeg + Pandoc bundled)
#   .\build.ps1 -NoInstaller -> only the self-contained app in .\publish
#   .\build.ps1 -RefreshTools -> download ffmpeg / Pandoc again (newest versions)
param([switch]$NoInstaller, [switch]$RefreshTools)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is very slow with the progress bar
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Set-Location $PSScriptRoot

$version = ([xml](Get-Content 'Orange\Orange.csproj')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "Orange $version" -ForegroundColor DarkYellow

# -- Third-party tools bundled with the installer (cached in .\vendor, not in git) --------------

function Get-Ffmpeg {
    $dir = 'vendor\ffmpeg'
    if ((Test-Path "$dir\ffmpeg.exe") -and -not $RefreshTools) { return }
    Write-Host 'Downloading ffmpeg (gyan.dev release essentials)...'
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "orange-ffmpeg-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory $tmp | Out-Null
    try {
        Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile "$tmp\ffmpeg.zip"
        Expand-Archive "$tmp\ffmpeg.zip" "$tmp\x"
        $root = Get-ChildItem "$tmp\x" -Directory | Select-Object -First 1
        New-Item -ItemType Directory -Force $dir | Out-Null
        Copy-Item "$($root.FullName)\bin\ffmpeg.exe", "$($root.FullName)\LICENSE" $dir -Force
        Set-Content "$dir\VERSION.txt" $root.Name
    }
    finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}

function Get-Pandoc {
    $dir = 'vendor\pandoc'
    if ((Test-Path "$dir\pandoc.exe") -and -not $RefreshTools) { return }
    Write-Host 'Downloading Pandoc (GitHub release)...'
    $release = Invoke-RestMethod 'https://api.github.com/repos/jgm/pandoc/releases/latest' -Headers @{ 'User-Agent' = 'orange-build' }
    $asset = $release.assets | Where-Object { $_.name -like '*windows-x86_64.zip' } | Select-Object -First 1
    if (-not $asset) { throw 'No Windows build in the latest Pandoc release.' }
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "orange-pandoc-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory $tmp | Out-Null
    try {
        Invoke-WebRequest $asset.browser_download_url -OutFile "$tmp\pandoc.zip"
        Expand-Archive "$tmp\pandoc.zip" "$tmp\x"
        $exe = Get-ChildItem "$tmp\x" -Recurse -Filter pandoc.exe | Select-Object -First 1
        New-Item -ItemType Directory -Force $dir | Out-Null
        Copy-Item $exe.FullName $dir -Force
        Get-ChildItem $exe.DirectoryName -Filter 'COPY*' | Copy-Item -Destination $dir -Force
        Set-Content "$dir\VERSION.txt" $release.tag_name
    }
    finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}

Get-Ffmpeg
Get-Pandoc
Write-Host "Tools: ffmpeg $(Get-Content vendor\ffmpeg\VERSION.txt), Pandoc $(Get-Content vendor\pandoc\VERSION.txt)"

# -- App ---------------------------------------------------------------------------------------

# Self-contained: runs on PCs without .NET installed.
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish Orange\Orange.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -o publish -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# Orange looks in its own "tools" folder before PATH (see Core\Tools.cs).
New-Item -ItemType Directory -Force 'publish\tools\licenses' | Out-Null
Copy-Item 'vendor\ffmpeg\ffmpeg.exe', 'vendor\pandoc\pandoc.exe' 'publish\tools'
Copy-Item 'vendor\ffmpeg\LICENSE' 'publish\tools\licenses\ffmpeg-LICENSE.txt'
Get-ChildItem 'vendor\pandoc' -Filter 'COPY*' | ForEach-Object { Copy-Item $_.FullName "publish\tools\licenses\pandoc-$($_.Name)" }

$mb = [math]::Round((Get-ChildItem publish -Recurse | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "Published to .\publish ($mb MB)"

if ($NoInstaller) { return }

# -- Installer ---------------------------------------------------------------------------------

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup not found. Install it with: winget install JRSoftware.InnoSetup' }

& $iscc /Q "/DMyAppVersion=$version" installer\Orange.iss
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }
Get-Item "dist\Orange-Setup-$version.exe" | ForEach-Object { Write-Host "Installer: $($_.FullName) ($([math]::Round($_.Length / 1MB)) MB)" -ForegroundColor Green }
