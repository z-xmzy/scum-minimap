from pathlib import Path
p=Path('source/tests/InputVoiceTests.cs');s=p.read_text(encoding='utf-8-sig')
needle='[STAThread] static void Main() {'
s=s.replace(needle,'''[STAThread] static void Main() {
        try { RunAll(); }
        catch (Exception error) {
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("TEMP"),"input-voice-failure.txt"),
                error.GetType().FullName+"\\\\n"+error.Message+"\\\\n"+error.StackTrace);
            Environment.ExitCode=1;
        }
    }
    static void RunAll() {'''.replace('\\\\n','\\n'))
Path('checks/InputVoiceDiagnostic.cs').write_text(s,encoding='utf-8-sig')
script=Path('source/scripts/Test-InputVoice.ps1').read_text(encoding='utf-8-sig')
script=script.replace("$root=Split-Path $PSScriptRoot -Parent","$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../source'))")
script=script.replace("(Join-Path $PSScriptRoot 'Get-", "(Join-Path \"$root/scripts\" 'Get-")
script=script.replace("(Join-Path $root 'tests\\InputVoiceTests.cs')","(Join-Path $PSScriptRoot 'InputVoiceDiagnostic.cs')")
Path('checks/input-diagnostic.ps1').write_text(script,encoding='utf-8-sig')
