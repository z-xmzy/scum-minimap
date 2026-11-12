. "$PSScriptRoot/open-game.ps1"
$dest=Join-Path (Split-Path $PSScriptRoot) 'bake'
$refs=@((Get-ChildItem "$PSHOME/ref/*.dll").FullName)+@(('CUE4Parse','ScumStudio.Assets','ScumStudio.Core','ScumStudio.Level','Microsoft.Extensions.Logging.Abstractions') | ForEach-Object { "$libDir/$_.dll" })
Add-Type -Path "$PSScriptRoot/AuditProps.cs" -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702'
$cat.PackageCacheSize=5
$p=[AuditProps]::new($cat,$dest)
$i=0
try {foreach($line in Get-Content "$dest/scenes.txt"){
    $p.Scene($line)
    if((++$i)%25 -eq 0){[GC]::Collect()}
    if($i%200 -eq 0){Write-Output $i}
}}finally{$p.Close();$cat.Dispose()}
