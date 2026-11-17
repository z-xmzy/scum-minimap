param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$testExe=Join-Path $OutputDirectory 'InventoryResponsivenessTests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /reference:System.Windows.Forms.dll "/out:$testExe" (Join-Path $PSScriptRoot 'InventoryResponsivenessTests.cs')
if($LASTEXITCODE -ne 0) { throw 'Inventory test compilation failed.' }
& $testExe $AssemblyPath
if($LASTEXITCODE -ne 0) { throw 'Inventory responsiveness tests failed.' }
