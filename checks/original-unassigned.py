"""Verify 38 unassigned placed components against the complete original triangle arrays."""
import csv
from collections import Counter
from pathlib import Path
import numpy as np

base=Path("checks")
def rows(path,sep=",",named=True):
    with Path(path).open(encoding="utf-8-sig",newline="") as f:
        return list((csv.DictReader if named else csv.reader)(f,delimiter=sep))
cat={r["ID"]:r for r in rows("../SCUM-2.4.9-all-bridges/组件逐件清单.csv")}
placements=rows("tools/bridge-meshes/placements.tsv","\t",False)
index=rows("tools/model-review/index.tsv","\t",False)
tri=np.memmap("tools/model-review/triangles.bin","<f4","r").reshape(-1,3,3)
def key(r):
    return (r[0].split("/")[-1],r[1],r[3].split(".")[-1])
def stats(row):
    t=tri[int(row[5]):int(row[5])+int(row[6])].astype(float)/100
    q=t.reshape(-1,3)
    axes=np.linalg.eigh(np.cov(q[:,:2].T))[1]
    dims=np.ptp((q[:,:2]-q[:,:2].mean(0))@axes,axis=0)
    n=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]); z=n[:,2]
    good=(z<-.0000001)&(np.square(n[:,:2]).sum(1)<=.2*z*z)
    return len(t),int(good.sum()),round(float(-z[good].sum()/2),4),*np.round(dims,4),round(float(np.ptp(q[:,2])),4)
details={key(r):r for r in index if r[4]=="True"}
places={key(r):r for r in placements}
out=[]
for pid,reg,group in rows(base/"deck-coverage.tsv","\t",False):
    if group.isdigit():continue
    r=cat[pid]
    k=(r["关卡"],r["对象"],r["模型"])
    st=stats(details[k])
    siblings=[q for q in placements if key(q)[:2]==k[:2] and key(q)[2]=="SM_SB_Bridge_A"]
    sibling_area=gap=""
    if siblings:
        q=siblings[0]
        gap=round(np.linalg.norm(np.array(q[4:7],float)-np.array(places[k][4:7],float))/100,5)
        sibling_area=stats(details[key(q)])[2]
    out.append([pid,reg,*k,*st,places[k][-1],sibling_area,gap,
                "同蓝图同原点闭合配件；启用状态未知" if siblings else "横跨车道的窄接缝"])
with (base/"原模未独立成层38件.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.writer(f)
    w.writerow(["ID","区域","关卡","对象","模型","三角形","平缓上表面三角形","上表面投影面积合计平方米","小轴跨度米","大轴跨度米","高差米","旧采样条数","同蓝图主桥平缓投影面积平方米","同蓝图主桥原点差米","组件性质"])
    w.writerows(out)
assert len(out)==38 and sum(r[-1].startswith("同蓝图") for r in out)==36
for name,count in Counter(r[4] for r in out).items():
    group=[r for r in out if r[4]==name]
    print(name,count,[(min(r[i] for r in group),max(r[i] for r in group)) for i in (5,6,7,8,9,10,12,13)])

# Probe both sides of each seam, along three parallel lines through the road width.
road_rows=[r for r in index if r[0].endswith("Landscape_D_4_1d") and "/Road/" in r[3] and "Transition" not in r[3]]
def surfaces(row):
    raw=tri[int(row[5]):int(row[5])+int(row[6])].astype(float)/100
    a=raw[:,0];e=raw[:,1]-a;f=raw[:,2]-a
    n=np.cross(e,f);det=n[:,2];ok=(det< -1e-8)&(np.sum(n[:,:2]**2,axis=1)<=.2*det**2)
    return a[ok],e[ok],f[ok],det[ok]
road=[(r,surfaces(r)) for r in road_rows]
def intersect(pt,arrays):
    a,e,f,det=arrays
    x,y=(pt-a[:,:2]).T
    u=(x*f[:,1]-y*f[:,0])/det;v=(e[:,0]*y-e[:,1]*x)/det
    ok=(u>=-1e-7)&(v>=-1e-7)&(u+v<=1+1e-7)
    return a[ok,2]+u[ok]*e[ok,2]+v[ok]*f[ok,2]
paths=[]
for row in out:
    if "Transition" not in row[4]:continue
    k=tuple(row[2:5]); r=details[k]
    points=tri[int(r[5]):int(r[5])+int(r[6])].reshape(-1,3).astype(float)/100
    axes=np.linalg.eigh(np.cov(points[:,:2].T))[1]
    center=points[:,:2].mean(0); x,y=axes.T
    if x[0]<0:x=-x
    z=points[:,2].mean()
    for shift in (-2.,0.,2.):
        cp=center+shift*y
        for d in (-3.,-2.,-1.,-.5,-.25,.25,.5,1.,2.,3.):
            near=[]
            for rr,arrays in road:
                for h in intersect(cp+d*x,arrays):
                    near.append((abs(h-z),h,key(rr)[-1]))
            if near:
                gap,h,name=min(near)
                paths.append([row[0],shift,d,round(z,3),name,round(h,3),round(h-z,3)])
            else:paths.append([row[0],shift,d,round(z,3),"","",""])
with (base/"原模两处窄缝两侧.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.writer(f);w.writerow(["ID","横移米","垂直于接缝的有符号距离米","接缝全顶点均高米","最近高度道路模型","探针处路面高米","路面减接缝均高米"]);w.writerows(paths)
print("seam sides",Counter((r[0],r[2]>0,r[4]) for r in paths))
