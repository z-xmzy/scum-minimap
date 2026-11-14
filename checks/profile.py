from pathlib import Path
p=Path('source/src/WpfFieldConsole.cs')
s=p.read_text('utf-8-sig')
Path('checks/WpfFieldConsole.original').write_text(s,'utf-8')
a='gameMapWindow=diagnostic?IntPtr.Zero:Native.AdminGameWindow();'
s=s.replace(a,'var sw=System.Diagnostics.Stopwatch.StartNew();Action<string> mark=n=>Console.WriteLine(n+" "+sw.ElapsedMilliseconds);'+a)
for k in ['var openingState=engine.WpfSnapshot(true);','syncMapChrome(openingState.Bunker==null && openingState.Opacity==0,true);','tactical.WindowState=WindowState.Normal;tactical.Show();','tactical.Show();FitTacticalToMonitor();']:
 if k=='tactical.Show();FitTacticalToMonitor();':k='FitTacticalToMonitor();\n            }\n            Refresh();';s=s.replace(k,'FitTacticalToMonitor();mark("fit");\n            }\n            Refresh();',1)
 else:s=s.replace(k,k+'mark("'+('state' if 'openingState=engine' in k else 'chrome' if 'syncMapChrome' in k else 'show')+'");',1)
p.write_text(s,'utf-8-sig')