"""Measure original mesh support across reported deck endpoints. Run from project root."""
import csv
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

CHECK = Path("checks")
OLD = Path("../SCUM-MiniMap-2.4.10-map/checks")
SOURCE = Path("tools/model-review")
scale = np.array([15216.18, 15236.18]) / 15216
origin = np.array([6177.18, 6186.18])
mesh = np.memmap(SOURCE / "triangles.bin", "<f4", "r").reshape(-1, 3, 3)
dem = np.memmap("package/SCUM-MiniMap/app/fine/world/height.f32", "<f4", "r", shape=(10161, 10161))
data = np.load(OLD / "bridge-geometry.npz")
k, layer, height = data["keys"], data["groups"]+1, data["z"]/100
xy = origin - np.c_[k%15216+.5,k//15216+.5]*scale

def records(path, delim=",", named=True):
    with Path(path).open(encoding="utf-8-sig",newline="") as f:
        return list((csv.DictReader if named else csv.reader)(f,delimiter=delim))

def write(name, header, rows):
    with (CHECK / name).open("w",encoding="utf-8-sig",newline="") as f:
        csv.writer(f).writerows([header,*rows])

def ground(p):
    a,b=(np.array([14.82,5.82])+origin-p)/1.5
    i,j=int(a),int(b)
    x,y=a-i,b-j
    p,q,r,t=dem[j:j+2,i:i+2].flatten()
    return (p+x*(q-p)+y*(r-p) if x+y<=1 else t+(1-x)*(r-t)+(1-y)*(q-t))/100

groups = {int(r["层"]):r for r in records(OLD/"两岸接入口清单.csv")
          if r["结论"]=="端部需核实" and r["类别"]!="铁路桥" and r["模型"]!="SM_RailBridge_Arch"}
catalog={r["ID"]:r for r in records("../SCUM-2.4.9-all-bridges/组件逐件清单.csv")}
members=defaultdict(list)
for pid,reg,g in records(CHECK/"deck-coverage.tsv","\t",False):
    if g.isdigit() and int(g) in groups:
        members[int(g)].append(catalog[pid])
index=records(SOURCE/"index.tsv","\t",False)
by_scene=defaultdict(list)
names=[]
def original(i):
    row=index[i]
    return mesh[int(row[5]):int(row[5])+int(row[6])]
for i,row in enumerate(index):
    name=row[3].split(".")[-1]
    names.append(name)
    if not int(row[6]):
        continue
    if name.lower().endswith(("_wm","_es","_fn","_rvt")):
        continue
    points=original(i).reshape(-1,3)
    bounds=[points[:,a].min()/100 for a in (0,1)]+[points[:,a].max()/100 for a in (0,1)]
    by_scene[row[0].split("/")[-1]].append((i,*bounds))

def nearby(scene, center, radius):
    local=[]
    for i,x0,y0,x1,y1 in by_scene[scene]:
        x,y=center
        if x0>x+radius or x1<x-radius or y0>y+radius or y1<y-radius:
            continue
        t=original(i).astype(float)/100
        mask=np.all((t[:,:,:2].min(1)<=center+radius)&(t[:,:,:2].max(1)>=center-radius),axis=1)
        t=t[mask]
        e=t[:,1]-t[:,0]
        f=t[:,2]-t[:,0]
        n=np.cross(e,f)
        good=(n[:,2]<-1.e-8)&(np.square(n[:,:2]).sum(1)<=.20*n[:,2]**2)
        if good.any():
            local.append((i,t[good,0],e[good],f[good],n[good,2]))
    return local

def vertical(local, point):
    hits=[]
    for i,a,e,f,det in local:
        x=point[0]-a[:,0]
        y=point[1]-a[:,1]
        u=(x*f[:,1]-y*f[:,0])/det
        v=(e[:,0]*y-e[:,1]*x)/det
        inside=(u>=-1e-6)&(v>=-1e-6)&(u+v<=1+1e-6)
        hits.extend((i,z) for z in (a[inside,2]+u[inside]*e[inside,2]+v[inside]*f[inside,2]))
    return hits

def tipcheck(group,scene,tag,seed,direction,z0,own,extra=()):
    local=nearby(scene,seed,8)
    ownids={i for i,*_ in local if (index[i][1],names[i]) in own or names[i] in extra}
    lateral=direction[::-1]*[-1,1]
    runs=[]
    for shift in [0,-.4,.4,-.8,.8]:
        center=seed+shift*lateral
        # Locate physical end of bridge floor independently on each transect.
        coords=np.arange(-2.,2.51,.25)
        inside=[]
        for s in coords:
            hits=vertical(local,center+s*direction)
            possible=[(abs(z-z0),z,i) for i,z in hits if i in ownids and abs(z-z0)<=.57]
            if possible: inside.append((s,*min(possible)[1:]))
        if not inside: continue
        s,z,i=inside[-1]
        if s>=coords[-1]: continue
        # Refine the last in-mesh station to within one centimetre.
        left,right=s,s+.25
        for _ in range(5):
            mid=(left+right)/2
            in_mesh=any(j in ownids and abs(h-z)<=.25 for j,h in vertical(local,center+mid*direction))
            if in_mesh:left=mid
            else:right=mid
        end=center+(left+right)*.5*direction
        samples=[]
        # Record missing immediate join separately from a nearby floor across a gap.
        for d in np.arange(.125,3.,.25):
            hits=vertical(local,end+d*direction)
            candidates=[(abs(h-z),h,j) for j,h in hits if j not in ownids and abs(h-z)<=.25]
            samples.append(min(candidates) if candidates else None)
        count=next((a for a,item in enumerate(samples) if item is None),len(samples))
        first=next((a for a,item in enumerate(samples) if item is not None),len(samples))
        runs.append((count,-first,-abs(shift),end,z,i,shift,samples))
    if runs:
        length,negfirst,_,end,z,i,shift,samples=max(runs,key=lambda v:v[:3])
        evidence=";".join(sorted({index[j][1]+"/"+names[j] for _,_,j in samples[:length]}))
        first=-negfirst
        later=next((index[x[2]][1]+"/"+names[x[2]] for x in samples if x is not None),"")
        return [group,scene,tag,*np.round(end,3),round(z,3),round(ground(end),3),round(z-ground(end),3),round(shift,2),index[i][1]+"/"+names[i],round(length*.25,2),evidence,
                "" if first==len(samples) else first*.25+.125,later,
                "邻件同高面连续取样" if length>=4 else ("邻件同高面不足1米" if length else ("隔空命中邻件同高面" if later else "探线未命中邻件同高面"))]
    return [group,scene,tag,*np.round(seed,3),round(z0,3),round(ground(seed),3),round(z0-ground(seed),3),"","","","","","","本体端线未定位"]

header=["层","关卡","端","原世界X米","原世界Y米","桥面Z米","地形Z米","桥面减地形米","横移米","桥端模型","邻件连续取样长度米上限3","邻件对象及模型","首次命中距离米","首次命中对象及模型","判读"]
out=[]
for group,entry in groups.items():
    match=members[group]
    scene=match[0]["关卡"]
    assert len({r["关卡"] for r in match})==1
    own={(r["对象"],r["模型"]) for r in match}
    ids=np.flatnonzero(layer==group)
    p=xy[ids]
    axis=np.linalg.eigh(np.cov(p.T))[1][:,-1]
    if axis[0]<0:axis=-axis
    for side,s in [("主轴负", -1),("主轴正",1)]:
        direction=axis*s
        t=p@direction
        front=t.max()
        end_ids=t>=front-min(1.6,np.ptp(t)*.24)
        seed=p[end_ids].mean(0)
        seed+=(front-seed@direction)*direction
        h=height[ids][end_ids]
        z0=np.median(h)
        if "SM_SB_Bridge_A" in entry["模型"]:
            # The continuous stone floor occupies the modal height band.
            bins=np.round(height[ids]/.15).astype(int)
            bin0=Counter(bins).most_common(1)[0][0]
            z0=np.median(height[ids][bins==bin0])
        out.append(tipcheck(group,scene,side,seed,direction,z0,own,
                            ["Platform_01","Platform_01_End","Platform_02"] if group==4324 else []))
    if group==4324:
        for actor,name in sorted(own):
            item=next(i for i,r in enumerate(index) if r[0].endswith(scene) and r[1]==actor and names[i]==name)
            t=original(item).astype(float)/100
            v=t.reshape(-1,3)
            a=np.linalg.eigh(np.cov(v[:,:2].T))[1][:,-1]
            if a[0]<0:a=-a
            for s in [-1,1]:
                dr=a*s
                proj=v[:,:2]@dr
                front=proj.max()
                seed=v[proj>front-.9,:2].mean(0)
                seed+=(front-seed@dr)*dr
                distance=np.linalg.norm(p-seed,axis=1)
                z0=np.median(height[ids][distance<=distance.min()+1])
                out.append(tipcheck(group,scene,name+("负端" if s<0 else "正端"),seed,dr,z0,{(actor,name)}))
write("原模逐端接面.csv",header,out)
print("axis endpoints",len(out)-4,"component endpoints",len(out[-4:]),"results",Counter(r[-1] for r in out))
print("Z3",*[r for r in out if r[0]==4324],sep="\n")
