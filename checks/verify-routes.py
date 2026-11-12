"""Exercise the public navigation facade in the old and world EXEs, then sample both on the same game geometry."""
import csv, gzip, math, struct, subprocess
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
CHECK=ROOT/'checks'
OLD=ROOT.parent/'SCUM-slope-route-test-20261111'
W=3804
def layer(name):
    with gzip.open(ROOT/'source/resources'/name,'rb') as f:
        f.read(20)
        return np.frombuffer(f.read(W*W),np.uint8).reshape(W,W)
grid=layer('nav-world-grid.bin')
terrain=layer('nav-world-terrain.bin')
obs=np.fromfile(ROOT/'bake/obstacles.u8',np.uint8).reshape(W,W)
def run(folder,variant):
    out=CHECK/(variant+'-routes.tsv')
    subprocess.run([CHECK/'Probe.exe','stock',CHECK/'world-cases.csv',out],cwd=folder,check=True)
    return out
def score(row):
    name,mode,ok,length,poly=row
    blocked=steep=0.;k=0
    pts=[tuple(map(float,p.split(','))) for p in poly.split(';') if p]
    for a,b in zip(pts,pts[1:]):
        dx=b[0]-a[0];dy=b[1]-a[1];m=math.hypot(dx*15216.18,dy*15236.18)
        n=max(1,math.ceil(m/.25)); ts=(np.arange(n)+.5)/n
        x=((a[0]+ts*dx)*W).astype(int);y=((a[1]+ts*dy)*W).astype(int)
        blocked+=m/n*np.isin(grid[y,x],[0,16,255]).sum()
        steep+=m/n*((terrain[y,x]>=4)&(grid[y,x]>2)).sum();k+=n
    return [name,mode,ok,round(float(length),2),round(blocked,2),round(steep,2),k]
if __name__=='__main__':
    # world-cases.csv is the fixed world1 fixture; keep the exact same endpoints.
    results=[]
    for variant,path in [('navA28',OLD),('world',ROOT/'app')]:
        with run(path,variant).open(encoding='utf-8-sig') as f:
            rows=[score(r) for r in csv.reader(f,delimiter='\t')]
        results.extend([[variant,*r] for r in rows])
        if variant=='world':
            assert all((r[2]=='True')==(r[0]!='open-water') for r in rows),rows
            assert all(r[4]==0 and (r[1]=='Walking' or r[5]==0) for r in rows),rows
    with (CHECK/'route-validation.csv').open('w',newline='',encoding='utf-8-sig') as f:
        w=csv.writer(f);w.writerow(['variant','case','mode','success','metres','water_or_solid_m','offroad_40deg_m','samples']);w.writerows(results)
