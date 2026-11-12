param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$testExe=Join-Path $OutputDirectory 'MutedNavigationTests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /reference:System.Drawing.dll "/out:$testExe" (Join-Path $PSScriptRoot 'MutedNavigationTests.cs')
if($LASTEXITCODE -ne 0) { throw 'Muted navigation test compilation failed.' }
& $testExe $AssemblyPath
if($LASTEXITCODE -ne 0) { throw 'Muted navigation tests failed.' }
