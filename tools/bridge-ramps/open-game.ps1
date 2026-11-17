$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$libDir = 'D:/codex/SCUM-dam-height-probe-20261111/tools/lib'
Set-Location $root
[Environment]::CurrentDirectory = $root
foreach ($n in 'TEMP','TMP','APPDATA','LOCALAPPDATA','DOTNET_CLI_HOME','DOTNET_BUNDLE_EXTRACT_BASE_DIR') { [Environment]::SetEnvironmentVariable($n, (Join-Path $root 'tmp')) }
$env:PATH = $libDir + ';' + $env:PATH
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
public static class StudioBind {
    public static void Install(string dir) {
        AssemblyLoadContext.Default.Resolving += (ctx,name) => {
            var p = Path.Combine(dir,name.Name + ".dll");
            return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
        };
    }
}
'@
[StudioBind]::Install($libDir)
foreach ($n in 'ScumStudio.Core','ScumStudio.Assets','ScumStudio.Formats','ScumStudio.Level','ScumStudio.App') {
    $null=[Reflection.Assembly]::LoadFrom((Join-Path $libDir "$n.dll"))
}
$opts=[ScumStudio.Assets.Catalog.AssetCatalogOptions]::new()
$key = [ScumStudio.App.Services.OnlineKeyFinder]::FindAsync([Threading.CancellationToken]::None).GetAwaiter().GetResult()
if (!$key) { throw 'Studio public provider returned no game entry.' }
$opts.GetType().GetProperty('AesKey').SetValue($opts,$key)
$pakDir='D:/Stea/steamapps/common/SCUM/SCUM/Content/Paks'
$cat=[ScumStudio.Assets.Catalog.AssetCatalog]::OpenPaks($pakDir,$opts)
Write-Output "Files=$($cat.Provider.Files.Count) Unmounted=$($cat.UnmountedContainerCount)"
