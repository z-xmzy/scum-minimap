$d=$PSScriptRoot
. "$d/open-game.ps1"
$cat.PackageCacheSize=4
$refs=@((Get-ChildItem "$PSHOME/ref/*.dll").FullName)+@(('CUE4Parse','ScumStudio.Assets','ScumStudio.Core','ScumStudio.Level','Microsoft.Extensions.Logging.Abstractions') | ForEach-Object { "$libDir/$_.dll" })
Add-Type -Path "$d/Collect.cs" -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702'
$r=[Collect]::new($cat,$d)
$maps=@($cat.Provider.Files.Keys | Where-Object { $_ -like 'SCUM/Content/ConZ_Files/Maps/The_Island/*.umap' -and $_ -notmatch '_LOD[0-9]|_Interior|_Int[_.]' } | Sort-Object -Unique | ForEach-Object {$_.Replace('SCUM/Content/','/Game/').Replace('.umap','')})
[IO.File]::WriteAllLines("$d/scenes.txt",$maps)
try { $i=0;foreach($p in $maps){try{$r.Read($p)}catch{[IO.File]::AppendAllText("$d/failed-scenes.txt","$p $($_.Exception.GetBaseException().Message)`n")};if((++$i)%50 -eq 0){"$i/$($maps.Count)";[GC]::Collect()}} }finally{$r.Close();$cat.Dispose()}