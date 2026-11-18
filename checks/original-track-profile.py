"""Profile the Z3 bridge lanes on original placed triangles, excluding slopes above the bake limit."""
import csv
from collections import Counter
from pathlib import Path
import numpy as np

p=Path("checks")
src=Path("tools/model-review")
with (src/"index.tsv").open(encoding="utf-8-sig") as f:
    rows=list(csv.reader(f,delimiter="\t"))
buf=np.memmap(src/"triangles.bin","<f4","r").reshape(-1,3,3)
with (p/"原模逐端接面.csv").open(encoding="utf-8-sig") as f:
    ends=list(csv.DictReader(f))
def hist(a,width):
    return sorted(Counter(np.round(a/width).astype(int)).items(),key=lambda v:-v[1])[:5]
out=[]
for name in ("Bridge_01","Bridge_02"):
    rr=next(r for r in rows if r[0].endswith("Z_3_Motocross_Track") and r[3].endswith("."+name))
    ends1=[r for r in ends if r["端"].startswith(name)]
    ends1.sort(key=lambda r:float(r["原世界X米"]))
    start,stop=[np.array([float(r[s]) for s in ["原世界X米","原世界Y米","桥面Z米"]]) for r in ends1]
    raw=buf[int(rr[5]):int(rr[5])+int(rr[6])].astype(float)/100
    a=raw[:,0];e=raw[:,1]-a;f=raw[:,2]-a;n=np.cross(e,f);det=n[:,2]
    grade=np.linalg.norm(n[:,:2],axis=1)/np.maximum(abs(det),1e-14)
    ok=(det< -1e-9)&(grade**2<=.20)
    a,e,f,det,grade=(v[ok] for v in (a,e,f,det,grade))
    def probe(pos):
        x,y=(pos[:2]-a[:,:2]).T
        u=(x*f[:,1]-y*f[:,0])/det
        v=(e[:,0]*y-e[:,1]*x)/det
        hit=(u>=-1e-8)&(v>=-1e-8)&(u+v<=1+1e-8)
        h=a[hit,2]+u[hit]*e[hit,2]+v[hit]*f[hit,2]
        if not len(h):return np.nan,np.nan
        idx=np.argmin(abs(h-pos[2]))
        return (h[idx],grade[hit][idx]) if abs(h[idx]-pos[2])<=1.5 else (np.nan,np.nan)
    vec=stop-start
    length=np.linalg.norm(vec[:2])
    perp=np.r_[-vec[1],vec[0],0.]/length
    pp=[]
    for d in np.arange(.2,length-.2,.04):
        mid=start+vec*d/length
        z,g=probe(mid)
        out.append([name,round(d,3),round(z,4),round(g,4)])
        pp.append((d,z,g))
    pp=np.array(pp); v=pp[np.isfinite(pp[:,1])]
    dz=np.diff(pp[:,1]); dz=dz[np.isfinite(dz)]
    print(name,"length",round(length,2),"ends",start[-1],stop[-1],"samples",len(pp),"valid",len(v),"z range",v[:,1].min(),v[:,1].max(),"grade quantiles",np.quantile(v[:,2],[0,.25,.5,.75,1]),"heights jumping >.06m",np.sum(abs(dz)>.06),"jump min/max",dz.min(),dz.max())
with (p/"Z3原模纵断面.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.writer(f);w.writerow(["模型","距负端米","缓面Z米","原三角面坡度绝对值"]);w.writerows(out)
