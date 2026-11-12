. "$PSScriptRoot/open-game.ps1"
$dest=Join-Path (Split-Path $PSScriptRoot) 'bake'
$cat.PackageCacheSize=5
$refs=@((Get-ChildItem "$PSHOME/ref/*.dll").FullName)+@(('CUE4Parse','ScumStudio.Assets','ScumStudio.Core','ScumStudio.Level','Microsoft.Extensions.Logging.Abstractions') | ForEach-Object { "$libDir/$_.dll" })
Add-Type -Path "$PSScriptRoot/WorldObjects.cs" -ReferencedAssemblies $refs -CompilerOptions "/nowarn:1701,1702"
$exporter=[WorldObjects]::new($cat,$dest)
$maps=@($cat.Provider.Files.Keys | Where-Object { $_ -like 'SCUM/Content/ConZ_Files/Maps/The_Island/*.umap' -and $_ -notmatch '_LOD[0-9]|_Interior|_Int[_.]' } | Sort-Object -Unique | ForEach-Object {$_.Replace('SCUM/Content/','/Game/').Replace('.umap','')})
$maps=@($maps | Where-Object {$_ -match '/Landscape_[ABCDZ]_[0-4]_[1-4][bcd]?$'}) + @($maps | Where-Object {$_ -notmatch '/Landscape_[ABCDZ]_[0-4]_[1-4][bcd]?$'})
[IO.File]::WriteAllLines("$dest/scenes.txt",$maps)
try{
    for ($i=0;$i -lt $maps.Length;$i++) {
        try {$exporter.Export($maps[$i],$i)}catch{ [IO.File]::AppendAllText("$dest/failed.txt","$i $($maps[$i]) $($_.Exception.GetBaseException().Message)`n") }
        if($i%12 -eq 0){[GC]::Collect()}
        if($i%100 -eq 0){Write-Output "$i/$($maps.Length) $($maps[$i])"}
    }
}finally{$exporter.Close();$cat.Dispose()}

