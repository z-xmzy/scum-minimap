. "$PSScriptRoot/open-game.ps1"
$dest=Join-Path (Split-Path $PSScriptRoot) 'bake'
$row=Get-Content "$dest/models.tsv" | Where-Object {($_ -split "`t")[2] -like '*.SM_Dam_Outflow'} | Select-Object -First 1
if(!$row){throw 'Missing spillway model'}
$p=($row -split "`t")[2]
$mesh=[ScumStudio.Assets.Meshes.MeshExtractor]::Extract($cat.LoadObject($p),0,$false)
$writer=[IO.BinaryWriter]::new([IO.File]::Create("$dest/outflow-triangles.f32.part"))
try {
    foreach($idx in $mesh.Indices) {
        $i=$idx*3
        $writer.Write([single]$mesh.Positions[$i]);$writer.Write([single]$mesh.Positions[$i+1]);$writer.Write([single]$mesh.Positions[$i+2])
    }
} finally { $writer.Dispose(); $cat.Dispose() }
if($mesh.Indices.Length -lt 3) {throw 'Empty spillway mesh'}
Move-Item -LiteralPath "$dest/outflow-triangles.f32.part" -Destination "$dest/outflow-triangles.f32" -Force
Write-Output "Spillway triangle vertices: $($mesh.Indices.Length)"
