from pathlib import Path
import csv,gzip,struct,json,sys,subprocess
import numpy as np
root=Path(__file__).resolve().parents[1];C=root/'checks';B=root/'bake/audit';W=3804
def layer(p):
    with gzip.open(root/'source/resources'/p,'rb') as f:
        f.read(20);return np.frombuffer(f.read(W*W),'u1').reshape(W,W)
grid=layer('nav-world-grid.bin');slope=layer('nav-world-terrain.bin')
regs=json.loads((B/'regions.json').read_text());region=np.full((W,W),-1,'i2')
for i,r in enumerate(regs):
    x,y,n,s=[r[k] for k in ('x','y','n','s')];region[y:y+n,x:x+n]=i
    r['mask']=np.fromfile(B/(r['key']+'.mask'),np.uint8).reshape(n*s,n*s)
def cuts(a,b):
    d=b-a; ts=[0.,1.]
    for j in (0,1):
        if abs(d[j])<1e-14:continue
        lo,hi=sorted((a[j],b[j]));ts.extend((np.arange(np.floor(lo)+1,np.ceil(hi))-a[j])/d[j])
    t=np.unique(np.clip(ts,0,1));dts=np.diff(t);xy=np.floor(a+((t[:-1]+t[1:])/2)[:,None]*d).astype(int)
    return t,dts,xy
def illegal(p,q,walk):
    a=p*W;b=q*W;l=np.linalg.norm((q-p)*[15216.18,15236.18])
    if l==0:return 0
    t,ds,xy=cuts(a,b);x,y=xy.T;k=region[y,x];bad=(grid[y,x]>=16)|((slope[y,x]>=4)&(not walk))
    if walk:return l*np.sum(ds[bad])
    length=l*np.sum(ds[bad & (k<0)])
    for i in np.unique(k[k>=0]):
        r=regs[i];pos=np.nonzero(k==i)[0];d=b-a;off=[r['x'],r['y']]
        v=a+t[pos[0]]*d;w=a+t[pos[-1]+1]*d
        _,f,c=cuts((v-off)*r['s'],(w-off)*r['s'])
        c=np.clip(c,0,r['mask'].shape[0]-1)
        length+=l*(t[pos[-1]+1]-t[pos[0]])*np.sum(f[r['mask'][c[:,1],c[:,0]]!=0])
    return length
def rows(path,sep):
    with path.open(encoding='utf-8-sig') as f:return list(csv.reader(f,delimiter=sep))
def check(app,prefix):
    stats=[]
    for batch in ('road100','world-cases','holdout'):
        file=C/(prefix+'-'+batch+'.tsv')
        subprocess.run([C/'Probe.exe','stock',C/(batch+'.csv'),file],cwd=app,check=True)
        baseline=root.parent/'SCUM-MiniMap-world2-20261112/checks'/(batch+'-new.tsv')
        before=rows(baseline,'\t');after=rows(file,'\t');inputs=rows(C/(batch+'.csv'),',')
        for old,new,co in zip(before,after,inputs,strict=True):
            assert old[:2]==new[:2]==co[:2]
            pts=np.array([tuple(map(float,p.split(','))) for p in new[4].split(';') if p])
            bad=sum(illegal(p,q,co[1]=='Walking') for p,q in zip(pts,pts[1:]))
            assert bad<1e-4,(batch,new[:4],bad)
            if old[2]=='False' and new[2]=='True' and batch=='road100':
                refs=np.array(co[2:],np.float32).reshape(2,2)
                assert np.allclose(pts[[0,-1]],refs,atol=1e-8,rtol=0),(co,pts[[0,-1]])
            stats.append([batch,*co[:2],old[2],new[2],round(float(new[3]),2),round(bad,4)])
        print(batch,len(after),sum(r[2]=='True' for r in before),sum(r[2]=='True' for r in after),flush=True)
    with (C/'world3-regression.csv').open('w',newline='',encoding='utf-8-sig') as f:
        w=csv.writer(f);w.writerow(['批次','编号','模式','world2成功','world3成功','新版米数','穿禁行格米数']);w.writerows(stats)
if __name__=='__main__':check(Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'app','delivered')
