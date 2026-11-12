. "$PSScriptRoot/open-game.ps1"
$r=[ScumStudio.Level.Reading.Cue4ParseLevelReader]::new($cat,[ScumStudio.Level.Reading.Cue4ParseLevelReaderOptions]@{ReadInstances=$false},$null)
foreach($n in 'WaterSplines','Landscape_C_2_4d','C_2_Power_Plant_Dam','A_1_Krk_Bridge','A_2_Railroad_Bridge','A_0_Dr_Tudman_Bridge','The_Island'){
 try{$d=[ScumStudio.Level.Model.LevelDocument]::Load($r,"/Game/ConZ_Files/Maps/The_Island/$n",[Threading.CancellationToken]::None);"$n actors=$($d.Actors.Count)";foreach($a in $d.Actors){foreach($c in $a.Components){ if($c.StaticMeshPath -match 'Water|Rivers|Stream|Ocean|Bridge|Dam_Main' -or $a.WaterVolume){ "$($a.Name) $($c.Name) $($c.StaticMeshPath) spline=$($null -ne $c.SplineMesh) water=$($a.WaterVolume)" }}}}catch{$_.Exception.Message}
}
