param(
    # Reuse an already built package\SCUM-MiniMap\app instead of compiling again.
    [switch]$SkipAppBuild,
    # Build a test installer with its own AppId into release	est (never published).
    [switch]$TestInstall
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$mainSource=Join-Path $repo 'source\src\MiniMap.cs'
$version=([regex]::Match([IO.File]::ReadAllText($mainSource),'public const string VersionString\s*=\s*"(\d+\.\d+\.\d+)"')).Groups[1].Value
if(!$version){throw 'VersionString not found in MiniMap.cs.'}
$package=Join-Path $repo 'package\SCUM-MiniMap'
$release=Join-Path $repo 'release'
if($TestInstall){$release=Join-Path $release 'test'}

if(!$SkipAppBuild) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'Build-LocalCandidate.ps1')
    if($LASTEXITCODE -ne 0){throw 'App build failed.'}
}
$exe=Join-Path $package 'app\SkynettMiniMap.exe'
if(!(Test-Path -LiteralPath $exe)){throw "Built app not found: $exe"}
foreach($required in 'app\assets\bunkers','app\fine\world\models.bin','app\fine\runtime','dist\RoadMarker.exe','使用说明.md') {
    if(!(Test-Path -LiteralPath (Join-Path $package $required))){throw "Package is incomplete, missing $required"}
}
$built=(Get-Item -LiteralPath $exe).VersionInfo.FileVersion
if($built -ne "$version.0"){throw "SkynettMiniMap.exe is version $built, expected $version.0. Rebuild without -SkipAppBuild."}

$iscc=@(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if(!$iscc){throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup'}

New-Item -ItemType Directory -Path $release -Force | Out-Null
$defines=@("/DAppVersion=$version");if($TestInstall){$defines+="/DTestInstall"}
& $iscc /Qp @defines "/DPackageDir=$package" "/DOutputDir=$release" (Join-Path $PSScriptRoot 'SCUM-MiniMap.iss')
if($LASTEXITCODE -ne 0){throw 'Inno Setup compilation failed.'}

$setup=Join-Path $release "SCUM-MiniMap-Setup-$version.exe"
$hash=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
$size=(Get-Item -LiteralPath $setup).Length
# update.txt is uploaded next to the installer on the GitHub release; the app reads it from
# releases/latest/download/update.txt and verifies the installer against this hash and size.
$manifest="schema=3`nversion=$version`ninstaller-sha256=$hash`ninstaller-size=$size`n"
[IO.File]::WriteAllText((Join-Path $release 'update.txt'),$manifest,[Text.UTF8Encoding]::new($false))
# files.txt + files.pack let 2.6.0+ download only the files that changed.
& python -I (Join-Path $PSScriptRoot 'build_update_pack.py') $package (Join-Path $PSScriptRoot 'LICENSE-installer.txt') $version $release
if($LASTEXITCODE -ne 0){throw 'Update pack build failed.'}
Write-Output "Installer: $setup"
Write-Output ("Size: {0:N0} bytes ({1:N0} MB)" -f $size,($size/1MB))
Write-Output "SHA256: $hash"
Write-Output "Manifest: $(Join-Path $release 'update.txt')"
