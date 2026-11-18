$ErrorActionPreference='Stop'
$root=(Get-Item "$PSScriptRoot/..").FullName
Set-Location $root
$env:TEMP="$root/temp"
$env:TMP=$env:TEMP
$env:APPDATA="$root/temp/qa-roaming"
$env:LOCALAPPDATA="$root/temp/qa-local"
$py=(Get-Item 'package/SCUM-MiniMap/app/fine/runtime/python.exe').FullName
function RunPython($name,$opts) {
    & $py -X utf8 -B @opts *> "checks/$name.log"
    if ($LASTEXITCODE -ne 0) { throw "$name failed" }
}
RunPython 'generate-via' @('checks/via-deck.py')
RunPython 'final-routes' @('checks/run-suites.py','levels','heads','decks','through','area','extra','approach','other','stacked','via-deck')
RunPython 'levels-decks-verdict' @('checks/verdict.py')
RunPython 'heads-verdict' @('checks/gate-verdict.py')
RunPython 'through-verdict' @('checks/through-verification.py')
RunPython 'via-verdict' @('checks/via-deck.py','--verify')
RunPython 'all-route-changes' @('checks/summarize.py')
RunPython 'topology' @('checks/verify-topology.py')
Get-Content checks/final-routes.log,checks/levels-decks-verdict.log,checks/heads-verdict.log,checks/through-verdict.log,checks/via-verdict.log
