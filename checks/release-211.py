"""Validate actual release outputs and publish the archive under its own version name."""
from pathlib import Path
from collections import Counter
import csv, hashlib, json, shutil, zipfile

r=Path(__file__).resolve().parents[1]
d=r/'checks'
def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest().upper()
def tsv(path):
    with path.open(encoding='utf-8-sig',newline='') as f:
        return list(csv.reader(f,delimiter='\t'))
def csvrows(name):
    with (d/name).open(encoding='utf-8-sig',newline='') as f:
        return list(csv.DictReader(f))

checks={}
for name in ['area','extra','approach','other','stacked','levels','decks','heads','through','via-deck']:
    inputs=r.parent/'SCUM-2.4.9-all-bridges'/(name+'.tsv')
    if not inputs.exists():
        inputs=d/(name+'.tsv')
    rows=tsv(d/(name+'.out'))
    expected=tsv(inputs)
    assert len(rows)==len(expected)>0,name
    assert all(row[:-3]==inp for row,inp in zip(rows,expected)),name
    checks[name]={'requests':len(rows),'success':sum(row[-3]=='True' for row in rows)}
for name in ['decks','levels']:
    tests=csvrows(name+'-verification.csv')
    assert len(tests)==checks[name]['requests'] and all(x['结论']=='通过' for x in tests),name
heads=csvrows('heads-verification.csv')
assert all(x['结论'] in ('经入口换层','安全拒绝') for x in heads)
routes=csvrows('逐条复测.csv')
assert len(routes)==sum(v['requests'] for v in checks.values())
assert not any(int(x['无入口的换层']) for x in routes)
topology=json.loads((d/'topology-verification.json').read_text())
assert topology['invalid_edges']==0
bank=csvrows('两岸实际走层.csv')
via=csvrows('经桥面途经点复核.csv')
assert len(bank)==checks['through']['requests'] and len(via)==checks['via-deck']['requests']
exits=dict(x.split() for x in (d/'release-exits.txt').read_text().splitlines())
assert set(exits)=={'Startup','Packaged','SquadUi','MutedNavigation','BridgeIntegration','BridgeVoice'} and set(exits.values())=={'0'},exits
member='SCUM-MiniMap/app/SkynettMiniMap.exe'
exe=digest(r/'package'/member)
assert exe==digest(r/'verified-final'/member)
before=r.parent/'SCUM-MiniMap-2.4.10-桥面分层版.zip'
assert digest(before)=='9459FA49B62BF77E993795AE403659A6FD81C3EAFFE70312C3B9EF8E5D239055'
part=r/'SCUM-MiniMap-2.4.11-ui.part'
with zipfile.ZipFile(part) as z:
    assert z.testzip() is None and hashlib.sha256(z.read(member)).hexdigest().upper()==exe
    assert all(not any(x in name for x in ('BridgeProbe.exe','fine-work/','nav-trails.bin','__pycache__','automatic.log')) for name in z.namelist())
    with zipfile.ZipFile(before) as old:
        diff=set(old.namelist())^set(z.namelist())
        assert not diff,sorted(diff)
        changed=[n for n in z.namelist() if (old.getinfo(n).CRC,old.getinfo(n).file_size)!=(z.getinfo(n).CRC,z.getinfo(n).file_size)]
    entries=len(z.infolist())
dest=r.parent/'SCUM-MiniMap-2.4.11-桥区语音修复版.zip'
h=digest(part)
if dest.exists():
    assert digest(dest)==h,'A different existing archive has this version name'
else:
    shutil.copyfile(part,dest)
assert digest(dest)==h
info={'package':str(dest),'sha256':h,'size_bytes':dest.stat().st_size,'entries':entries,'exe_sha256':exe,
      'old_package_sha256':digest(before),'changed_files':changed,'cases':checks,'transitions_checked':sum(int(x['换层数']) for x in routes),
      'invalid_transitions':0,'topology':topology,'bank_route_actual':dict(Counter(x['结果'] for x in bank)),
      'via_route_actual':dict(Counter(x['结果'] for x in via)), 'release_checks':exits}
(d/'release-manifest.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(info,ensure_ascii=False,indent=2))
