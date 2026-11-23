# Read-only re-dump of bunker scene packages from the current game install, for diffing.
$ErrorActionPreference = 'Continue'
$root = $PSScriptRoot
$tool = 'D:\codex\SCUM-MiniMap-v2.3.25\bunker-extraction-20261031'
$paks = 'D:\Stea\steamapps\common\SCUM\SCUM\Content\Paks'
New-Item -ItemType Directory -Force "$root\temp","$root\tool-home","$root\scenes","$root\logs" | Out-Null
$env:TEMP = "$root\temp"; $env:TMP = $env:TEMP
$env:SCUMSTUDIO_HOME = "$root\tool-home"
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = "$root\temp\dotnet-bundle"
$env:SCUMSTUDIO_AES_KEY = [IO.File]::ReadAllText("$tool\temp\scum-pak-key.txt").Trim()
$cli = "$tool\tools\scumstudio-3.6.0\scumstudio.exe"
try {
  & $cli level list $paks --json 1> "$root\levels-streamed.json" 2> "$root\logs\level-list.log"
  if ($LASTEXITCODE -ne 0) { throw 'Level catalogue failed' }
  $levels = Get-Content "$root\levels-streamed.json" -Raw | ConvertFrom-Json
  $selected = @($levels | Where-Object { $_.name -match 'TV_Base|WW2_Bunker|ApexHunt_Arena|Military_Base_Underground|D_?4_Airport_Zeljava|Safe_Zone_D_2|Military_Harbour|Military_Base' })
  Write-Output "selected $($selected.Count)"
  foreach ($level in $selected) {
    $target = "$root\scenes\$($level.name).json"
    if (Test-Path -LiteralPath $target) { continue }
    & $cli level dump $paks $level.packagePath --json --instances 1> "$target.partial" 2> "$root\logs\$($level.name).log"
    if ($LASTEXITCODE -ne 0) { Write-Output "FAILED $($level.name)"; continue }
    Move-Item -LiteralPath "$target.partial" -Destination $target
  }
} finally { Remove-Item Env:SCUMSTUDIO_AES_KEY }
Write-Output 'done'
