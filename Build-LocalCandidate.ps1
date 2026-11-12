$ErrorActionPreference='Stop'
$env:TEMP=Join-Path $PSScriptRoot 'temp'
$env:TMP=$env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
$root=Join-Path $PSScriptRoot 'source'
$resources=Join-Path $root 'resources'
$output=Join-Path $PSScriptRoot 'app'
if (!(Test-Path -LiteralPath (Join-Path $output 'assets/map-tiles.bin'))) { throw 'Prepare the local app assets in app/ before building.' }
# The complete existing bunker atlas is retained in this isolated app/assets directory.
Copy-Item -LiteralPath (Join-Path $resources 'marker-icons\LICENSE.txt') -Destination (Join-Path $output 'Lucide-LICENSE.txt') -Force
$manifest=Join-Path $PSScriptRoot 'checks\installation-assets.txt'
$assetLines=@('map-tiles.bin','roads.bin' | ForEach-Object { $_+'='+(Get-FileHash -LiteralPath (Join-Path $output ('assets\'+$_)) -Algorithm SHA256).Hash.ToLowerInvariant() })
[IO.File]::WriteAllLines($manifest,$assetLines,[Text.UTF8Encoding]::new($false))
$sources=@(& (Join-Path $root 'scripts\Get-MiniMapSources.ps1'))
$mainSource=Join-Path $root 'src\MiniMap.cs'
$stamped=[IO.File]::ReadAllText($mainSource) -replace '\[assembly:\s*AssemblyVersion\("[^"]+"\)\]','[assembly: AssemblyVersion("2.4.2.0")]'
$stamped=$stamped -replace '\[assembly:\s*AssemblyFileVersion\("[^"]+"\)\]','[assembly: AssemblyFileVersion("2.4.2.0")]'
$stamped=$stamped -replace '\[assembly:\s*AssemblyInformationalVersion\("[^"]+"\)\]','[assembly: AssemblyInformationalVersion("2.4.2-local-world3")]'
$stampedPath=Join-Path $PSScriptRoot 'checks\MiniMap.build.cs'
[IO.File]::WriteAllText($stampedPath,$stamped,[Text.UTF8Encoding]::new($false))
$sources=@($sources | ForEach-Object { if([IO.Path]::GetFullPath($_) -eq $mainSource) { $stampedPath } else { $_ } })
$embedded=@('nav-world-grid.bin','nav-world-terrain.bin','nav-world-grade.bin','nav-local.bin','zones.tsv','water-mask.bin','scummap.bin','spawn-items.tsv','spawn-compatibility.tsv','spawn-item-names-pl.tsv','keyboard-setup.png','steam-logo.png' | ForEach-Object { '/resource:'+(Join-Path $resources $_)+','+$_ })
$embedded+=@('BarlowSemiCondensed-Regular.ttf','BarlowSemiCondensed-SemiBold.ttf' | ForEach-Object { '/resource:'+(Join-Path $resources ('fonts\'+$_))+','+$_ })
$embedded+='/resource:'+$manifest+',installation-assets.txt'
$embedded+='/resource:'+(Join-Path $root 'packaging\detect_zones.py')+',detect_zones.py'
$embedded+=@(& (Join-Path $root 'scripts\Get-LocalizationResources.ps1'))
$embedded+=@(& (Join-Path $root 'scripts\Get-VoiceResources.ps1'))
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references=@('/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:Accessibility.dll','/reference:System.Xaml.dll','/reference:System.IO.Compression.dll')
$references+=@('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll' | ForEach-Object { '/reference:'+(Join-Path $framework ('WPF\'+$_)) })
$exe=Join-Path $output 'SkynettMiniMap.exe'
& (Join-Path $framework 'csc.exe') /nologo /codepage:65001 /optimize+ /target:winexe /platform:x64 /define:MINIMAP_LOCAL @embedded "/out:$exe" @references "/win32icon:$resources\App-Icon.ico" "/win32manifest:$root\packaging\MiniMap.manifest" @sources
if($LASTEXITCODE -ne 0) { throw 'Candidate compilation failed.' }
$package=@('version=2.4.2',('SkynettMiniMap.exe='+(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()))+@($assetLines | ForEach-Object { 'assets/'+$_ })
[IO.File]::WriteAllLines((Join-Path $output 'package.txt'),$package,[Text.UTF8Encoding]::new($false))
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\Test-PackagedMiniMap.ps1') -AssemblyPath $exe
if($LASTEXITCODE -ne 0) { throw 'Candidate packaged check failed.' }
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $root 'tests\Test-LocalStartup.ps1') -AssemblyPath $exe
if($LASTEXITCODE -ne 0) { throw 'Candidate standalone startup check failed.' }
Write-Output "Local candidate built: $exe"
