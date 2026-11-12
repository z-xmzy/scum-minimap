"""Fixed world1 fixtures and independent deterministic island sampling. Use the release navigation entry point."""
import csv, math, subprocess, runpy, gzip
from pathlib import Path
import numpy as np
M=runpy.run_path(str(Path(__file__).with_name('verify-routes.py')))
C=M['CHECK'];root=M['ROOT'];baseline=root.parent/'SCUM-MiniMap-world-nav-20261112';w=M['W']
def lines(p):
    with p.open(encoding='utf-8-sig') as f:return list(csv.reader(f,delimiter='\t'))
def route(app,group,name):
    out=C/(group+'-'+name+'.tsv')
    subprocess.run([C/'Probe.exe','stock',C/(group+'.csv'),out],cwd=app,check=True)
    return lines(out)
def holdout():
    with gzip.open(baseline/'source/resources/nav-world-grid.bin','rb') as f:
        f.read(20);old=np.frombuffer(f.read(w*w),np.uint8).reshape(w,w)
    rng=np.random.default_rng(340673);rows=[]
    for gy in range(5):
        for gx in range(5):
            n=0
            for _ in range(800):
                a=(np.array([gx,gy])+rng.random(2))/5
                b=a+rng.uniform(-.04,.04,2)
                if np.any(b<=0) or np.any(b>=1):continue
                u,v=(a*w).astype(int);x,y=(b*w).astype(int)
                if old[v,u]==16 or old[y,x]==16:continue
                for mode in ('Quickest','Shortest','PavedOnly'):rows.append([f'{gy}-{gx}-{n}',mode,*a,*b])
                n+=1
                if n==2:break
    with (C/'holdout.csv').open('w',newline='') as f:csv.writer(f).writerows(rows)
holdout()
with (C/'version-comparison.csv').open('w',newline='',encoding='utf-8-sig') as f:
    wr=csv.writer(f);wr.writerow(['batch','pair','mode','w1_success','w2_success','w1_metres','w2_metres','w2_blocked_m','w2_40deg_m','w1_30deg_m','w2_30deg_m'])
    for group in ('world-cases','road100','holdout'):
        old=route(baseline/'release/SCUM-MiniMap-World/app',group,'old')
        new=route(root/'app',group,'new')
        changed=ident=failed=improved=0
        for a,b in zip(old,new,strict=True):
            assert a[:2]==b[:2]
            stats=M['score'](b);assert stats[4]==0 and (b[1]=='Walking' or stats[5]==0),(group,b[:4],stats)
            def steep(r):
                pts=np.array([tuple(map(float,p.split(','))) for p in r[4].split(';') if p]);m=0
                for p,q in zip(pts,pts[1:]):
                    d=np.linalg.norm((q-p)*[15216.18,15236.18]);n=max(1,math.ceil(d/.5))
                    xy=((p+(q-p)*((np.arange(n)+.5)/n)[:,None])*w).astype(int)
                    m+=np.count_nonzero(M['terrain'][xy[:,1],xy[:,0]]>=3)*d/n
                return round(m,1)
            wr.writerow([group,*a[:2],a[2],b[2],round(float(a[3]),2),round(float(b[3]),2),*stats[4:6],steep(a),steep(b)])
            if a[2]=='True' and b[2]=='False':failed+=1
            if a[2]=='False' and b[2]=='True':improved+=1
        print(group,len(new),'available before/after',sum(r[2]=='True' for r in old),sum(r[2]=='True' for r in new),'gained/lost',improved,failed,flush=True)
