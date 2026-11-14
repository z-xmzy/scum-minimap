from zipfile import ZipFile
from pathlib import Path
r=Path.cwd()
with ZipFile(next(r.parent.glob('*2.4.7-*.zip'))) as a,ZipFile(next(r.glob('*.part'))) as b:
 a1={p.filename:(p.file_size,p.CRC) for p in a.infolist()};b1={p.filename:(p.file_size,p.CRC) for p in b.infolist()}
 print("added",b1.keys()-a1.keys(),"removed",a1.keys()-b1.keys())
 print("changed",[k for k in a1.keys()&b1.keys() if a1[k]!=b1[k]])
 print("profile included",[k for k in b1 if 'nav-trails' in k])
