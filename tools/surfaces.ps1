. "$PSScriptRoot/open-game.ps1"
$dest=Join-Path (Split-Path $PSScriptRoot) 'bake'
$cat.PackageCacheSize=4
$refs=@((Get-ChildItem "$PSHOME/ref/*.dll").FullName)+@(('CUE4Parse','ScumStudio.Assets','ScumStudio.Core','ScumStudio.Level','Microsoft.Extensions.Logging.Abstractions') | ForEach-Object { "$libDir/$_.dll" })
Add-Type -Path "$PSScriptRoot/WorldSurfaces.cs" -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702'
$r=[WorldSurfaces]::new($cat)
$i=0
foreach($p in (Get-Content "$dest/scenes.txt")){
 try{$r.Read($p)}catch{[IO.File]::AppendAllText("$dest/surface-failures.txt","$p $($_.Exception.GetBaseException().Message)`n")}
 if((++$i)%25 -eq 0){[GC]::Collect()}
}
$r.Save($dest)
$cat.Dispose()

