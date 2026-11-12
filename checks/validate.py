import csv,json,subprocess,sys
from pathlib import Path
import numpy as np
R=Path(__file__).resolve().parents[1];test=R.parent/'SCUM-MiniMap-world4-test/checks'
app=R/'extracted/SCUM-MiniMap-World4/app'
rows=[json.loads(s) for s in (test/'routes.jsonl').read_text().splitlines()]
# Start with a known ordinary route to prove a cold process does not start the fallback for success.
rows.sort(key=lambda r:0 if (r['group'],r['id'])==('road','1') else 1)
inp=R/'checks/release-input.csv';out=R/'checks/release-routes.tsv'
with inp.open('w',newline='') as f:
    csv.writer(f).writerows((r['group']+'/'+r['id'],r['mode'],*r['start'],*r['end']) for r in rows)
subprocess.run([R/'checks/ReleaseRoutes.exe',app/'SkynettMiniMap.exe',inp,out],cwd=app,check=True)
counts={};times={};summary=[]
for i,(ln,r) in enumerate(zip(out.read_text().splitlines(),rows,strict=True)):
    key,mode,ok,ms,worker,cues,line=ln.split('\t'); assert key==r['group']+'/'+r['id'] and mode==r['mode']
    if i==0:assert worker=='False'
    assert (ok=='True')==bool(r['uv']),(key,mode,ok)
    if r['uv']:
        a=np.array(r['uv'],np.float32);a=a[np.r_[True,np.any(np.diff(a,axis=0)!=0,axis=1)]]
        b=np.array([p.split(',') for p in line.split(';')],np.float32)
        b=b[np.r_[True,np.any(np.diff(b,axis=0)!=0,axis=1)]]
        assert np.array_equal(a,b),(key,mode,'geometry',a.shape,b.shape)
    c=counts.setdefault(r['group'],[0,0]);c[0]+=ok=='True';c[1]+=1
    summary.append([key,mode,ok,ms,cues,worker])
with (R/'checks/解压正式版逐路线验证.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f);w.writerow(['用例','模式','成功','耗时毫秒','转向提示数','已启动细算']);w.writerows(summary)
print('All',len(rows),'outcomes and geometry matched',counts)

