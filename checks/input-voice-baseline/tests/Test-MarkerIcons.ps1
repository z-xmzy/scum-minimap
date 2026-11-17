param([string]$AssemblyPath,[string]$OutputFolder)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
$env:TEMP=$OutputFolder;$env:TMP=$OutputFolder
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path $OutputFolder 'MarkerIconTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" ('/out:'+$harness) (Join-Path $PSScriptRoot 'MarkerIconTests.cs')
if($LASTEXITCODE -ne 0){throw 'Regression compilation failed'}
& $harness $AssemblyPath (Join-Path $OutputFolder 'isolated-profile')
if($LASTEXITCODE -ne 0){throw 'Interaction regression failed'}
