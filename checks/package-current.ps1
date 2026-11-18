$ErrorActionPreference='Stop'
$root=(Get-Item "$PSScriptRoot/..").FullName
Set-Location -LiteralPath $root
$env:TEMP="$root/temp"
$env:TMP=$env:TEMP
$extract=Join-Path $root 'verified-final'
if(Test-Path -LiteralPath $extract){
    if((Resolve-Path -LiteralPath $extract).Path -ne [IO.Path]::GetFullPath((Join-Path $root 'verified-final'))){throw 'Unexpected extraction location'}
    Rename-Item -LiteralPath $extract -NewName ('verified-previous-'+[Guid]::NewGuid().ToString('N'))
}
python -X utf8 -B tools/package-ui.py *> checks/package.log
if($LASTEXITCODE -ne 0){throw 'Archive build or extraction failed'}
& pwsh -NoProfile -File checks/test-release.ps1 *> checks/qa-final.log
if($LASTEXITCODE -ne 0){throw 'Extracted application checks failed'}
Get-Content checks/package.log,checks/qa-final.log
