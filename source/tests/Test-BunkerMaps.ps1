param([string]$OutputFolder,[string]$AtlasFolder,[string]$ModelFixture)
$ErrorActionPreference='Stop'
if(!$OutputFolder){throw 'Use an isolated output folder.'}
$env:TEMP=$OutputFolder;$env:TMP=$OutputFolder
New-Item -ItemType Directory -Path $OutputFolder -Force|Out-Null
$source=Join-Path (Split-Path $PSScriptRoot -Parent) 'src\BunkerMaps.cs'
$exe=Join-Path $OutputFolder 'BunkerMapsTests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$exe" $source (Join-Path $PSScriptRoot 'BunkerMapsTests.cs')
if($LASTEXITCODE -ne 0){throw 'Bunker tests compile failed'}
if($ModelFixture){& $exe $AtlasFolder $ModelFixture}elseif($AtlasFolder){& $exe $AtlasFolder}else{& $exe}
if($LASTEXITCODE -ne 0){throw 'Bunker tests failed'}
