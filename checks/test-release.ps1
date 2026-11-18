$ErrorActionPreference='Stop'
$root=(Get-Item "$PSScriptRoot/..").FullName
Set-Location $root
$env:TEMP="$root/temp"
$env:TMP=$env:TEMP
$env:APPDATA="$root/temp/qa-roaming"
$env:LOCALAPPDATA="$root/temp/qa-local"
$null=New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA
$exe=(Get-Item 'verified-final/SCUM-MiniMap/app/SkynettMiniMap.exe').FullName
$ui=Join-Path $PSScriptRoot ('ui-release-'+[Guid]::NewGuid().ToString('N'))
$exits=Join-Path $PSScriptRoot 'release-exits.txt'
[IO.File]::WriteAllText($exits,'')
function RunCheck($name,$program,$arguments){
    & $program @arguments *> "checks/$name-release.log"
    $code=$LASTEXITCODE
    [IO.File]::AppendAllText($exits,"$name $code"+[Environment]::NewLine)
    if ($code -ne 0) {throw "$name failed with $code"}
}
$ps='powershell.exe'
$opt=@('-NoProfile','-STA','-ExecutionPolicy','Bypass','-File')
RunCheck 'Packaged' $ps ($opt+@('source/scripts/Test-PackagedMiniMap.ps1','-AssemblyPath',$exe))
RunCheck 'Startup' $ps ($opt+@('source/tests/Test-LocalStartup.ps1','-AssemblyPath',$exe))
RunCheck 'SquadUi' $ps ($opt+@('source/tests/Test-SquadUi.ps1','-Executable',$exe,'-OutputDirectory',$ui))
RunCheck 'MutedNavigation' $ps ($opt+@('source/tests/Test-MutedNavigation.ps1','-AssemblyPath',$exe,'-OutputDirectory',$ui))
RunCheck 'BridgeIntegration' "$root/checks/BridgeIntegration.exe" @($exe,"$root/checks/heads.tsv")
RunCheck 'BridgeVoice' "$root/checks/LayerVoiceRegression.exe" @($exe)
Get-Content -LiteralPath $exits
