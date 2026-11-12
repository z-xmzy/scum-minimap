param([string]$AssemblyPath,[string]$OutputFolder)
$ErrorActionPreference='Stop'
$env:TEMP=$OutputFolder;$env:TMP=$OutputFolder
New-Item -ItemType Directory -Path $OutputFolder -Force|Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Xaml.dll',('/reference:'+$AssemblyPath))
$refs+=@('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll'|ForEach-Object{'/reference:'+(Join-Path $framework ('WPF\'+$_))})
$exe=Join-Path $OutputFolder 'BunkerRenderingTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$exe" @refs (Join-Path $PSScriptRoot 'BunkerRenderingTests.cs')
if($LASTEXITCODE -ne 0){throw 'Render test compilation failed'}
# Resolve the referenced candidate assembly without copying the large app to the test directory.
if(Test-Path -LiteralPath ($exe+'.config')){Remove-Item -LiteralPath ($exe+'.config')}
& $exe $OutputFolder $AssemblyPath
if($LASTEXITCODE -ne 0){throw 'Render test failed'}
