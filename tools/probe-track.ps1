$d=Join-Path $PSScriptRoot bridge-meshes
. "$d/open-game.ps1"
$r=[ScumStudio.Level.Reading.Cue4ParseLevelReader]::new($cat)
$doc=[ScumStudio.Level.Model.LevelDocument]::Load($r,'/Game/ConZ_Files/Maps/The_Island/Z_3_Motocross_Track')
$doc.Actors.Components.StaticMeshPath | Where-Object {$_} | Sort-Object -Unique | Set-Content "$d/track-models.txt" -Encoding utf8
$cat.Dispose()