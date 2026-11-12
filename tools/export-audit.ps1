New-Item -ItemType Directory -Force "$PSScriptRoot/tmp" | Out-Null
. "$PSScriptRoot/open-game.ps1"
$dest=Join-Path (Split-Path $PSScriptRoot) 'bake'
$refs=@((Get-ChildItem "$PSHOME/ref/*.dll").FullName)+@(('CUE4Parse','ScumStudio.Assets','ScumStudio.Core','ScumStudio.Level','Microsoft.Extensions.Logging.Abstractions') | ForEach-Object { "$libDir/$_.dll" })
try {
    Add-Type -Path "$PSScriptRoot/AuditBodies.cs" -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702'
    [AuditBodies]::Export($cat,$dest)
} finally {$cat.Dispose()}

