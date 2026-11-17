param([Parameter(Mandatory=$true)][string]$AssemblyPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$program=$assembly.GetType('ScumMiniMap.Program',$true)
$bootstrap=$assembly.GetType('ScumMiniMap.UpdateBootstrap',$true)
$rejected=$false
try { $null=$bootstrap.GetMethod('ReadStableVersion',$flags).Invoke($null,@($AssemblyPath)) }
catch {
    $failure=$_.Exception
    while($null -ne $failure.InnerException) { $failure=$failure.InnerException }
    if($failure -isnot [IO.InvalidDataException]) { throw }
    $rejected=$true
    Write-Output ('Confirmed stable-release rejection: '+$failure.Message)
}
if(-not $rejected) { throw 'A custom build must not masquerade as a stable release.' }
# Fail safely on the old build before invoking its modal updater path.
$local=$program.GetField('IsLocalBuild',$flags)
if($null -eq $local -or -not $local.GetValue($null)) { throw 'Custom package is not marked for standalone startup; normal startup would enter the stable-only installer.' }
if($program.GetField('IsTestBuild',$flags).GetValue($null)) { throw 'Startup fix must not switch the user to a test profile.' }
if($program.GetProperty('DataFolderName',$flags).GetValue($null,$null) -ne 'ScumMiniMap') { throw 'Existing user settings profile changed.' }
$startup=$program.GetMethod('TryInstallDownloadedUpdate',$flags)
foreach($mode in @('normal','null','installer')) {
    $arguments=New-Object object[] 1
    if($mode -eq 'null') { $arguments[0]=$null }
    elseif($mode -eq 'installer') { $arguments[0]=[string[]]@('-InstallUpdate') }
    else { $arguments[0]=[string[]]@() }
    $timer=[Diagnostics.Stopwatch]::StartNew()
    if($startup.Invoke($null,$arguments)) { throw 'Custom startup was consumed by the release installer.' }
    if($timer.Elapsed.TotalSeconds -gt 2) { throw 'Standalone startup did not return promptly.' }
    Write-Output "PASS: $mode startup continues without a release installer dialog"
}
$data=$program.GetProperty('UserDataDirectory',$flags).GetValue($null,$null)
if($data -ne (Join-Path (Split-Path ([IO.Path]::GetFullPath($AssemblyPath)) -Parent) 'data')){throw 'Local candidate must keep its profile beside the executable.'}
Write-Output 'PASS: Local profile stays beside the executable; stable release validation remains strict.'
