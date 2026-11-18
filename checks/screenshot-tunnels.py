"""Locate all tunnel samples previously promoted into bridge layers and probe road continuity."""
import csv,gzip,os,subprocess,sys
from collections import Counter,defaultdict
from pathlib import Path
import numpy as np
root=Path(__file__).resolve().parents[1]; out=root/"checks"; old=root.parent/"SCUM-MiniMap-2.4.10-map"
source=root/"tools/bridge-underroads"
data=np.load(old/"checks/bridge-geometry.npz"); k,z,g=data["keys"],data["z"],data["groups"]+1
lookup=defaultdict(list)
for cell,h,grp in zip(k,z,g):lookup[int(cell)].append((float(h),int(grp)))
mesh=np.memmap(source/"cells.bin",dtype=[("key","<i4"),("z","<f4")],mode="r")
with gzip.open(root/"source/resources/nav-world-grid.bin","rb") as f:f.read(20);cost=np.frombuffer(f.read(3804**2),"u1").reshape(3804,3804)
with gzip.open(old/"source/resources/nav-bridges.bin","rb") as f:
    _,_,ng,_=__import__("struct").unpack("<4siii",f.read(16));grounds=np.frombuffer(f.read(ng*11),[("k","<i4"),("cost","u1"),("t","u1"),("h","<f4"),("r","?")]); limit={int(r["k"]):(int(r["cost"]),int(r["t"])) for r in grounds}
details=[];cases=[];off=0;W=3804
with (source/"placements.tsv").open(encoding="utf-8-sig") as f:
    for r in csv.reader(f,delimiter="\t"):
        n=int(r[-1]); part=mesh[off:off+n];off+=n;name=r[3].split(".")[-1]
        if "tunnel" not in name.lower():continue
        hits={int(kk):gr for kk,hh in part for zz,gr in lookup[int(kk)] if abs(zz-hh)<=1.5}
        if not hits:continue
        kk=np.array(list(hits));pt=np.c_[kk%15216+.5,kk//15216+.5];groups=sorted(set(hits.values()))
        cid=set((int(v[0]//4)+int(v[1]//4)*W) for v in pt)
        invalid={c for c in cid if cost.flat[c]<=2 and c in limit and (limit[c][0]>2 or limit[c][1]>=4)}
        details.append([r[0].split("/")[-1],r[1],r[2],name,";".join(map(str,groups)),len(kk),len(invalid)])
        if "entrance" not in name.lower() or len(pt)<4:continue
        # Find a mapped road on either side of the portal crossbeam.
        center=pt.mean(0);orient=np.linalg.eigh(np.cov(pt.T))[1][:,0]
        pair=[]
        for s in [-1,1]:
            expect=center+s*orient*24
            x,y=(expect/4).astype(int);ys,xs=np.nonzero(cost[y-5:y+6,x-5:x+6]<=2)
            if not len(xs):break
            coords=np.c_[xs+x-5+.5,ys+y-5+.5]*4
            pair.append(coords[np.argmin(np.linalg.norm(coords-expect,axis=1))]/15216)
        if len(pair)==2:
            cases.append([groups[0],"双侧路面","Quickest",*pair[0],*pair[1]])
assert off==len(mesh)
for filename,data,delim,enc in [("screenshot-tunnel-provenance.csv",[["关卡","对象","组件","模型","旧桥层","同高度1米格数","旧桥层取消的原道路4米格数"],*details],",","utf-8-sig"),("screenshot-tunnel-tests.tsv",cases,"\t","utf-8")]:
    with (out/filename).open("w",encoding=enc,newline="") as f:csv.writer(f,delimiter=delim).writerows(data)
app=(old if sys.argv[1]=="before" else root)/"package/SCUM-MiniMap/app"
os.environ["TEMP"]=os.environ["TMP"]=str(root/"temp")
subprocess.run([app/"BridgeProbe.exe",app/"SkynettMiniMap.exe",out/"screenshot-tunnel-tests.tsv",out/("screenshot-tunnel-"+sys.argv[1]+".out")],cwd=app,check=True)
print("placements",len(details),"tunnel models",Counter(r[3] for r in details),"fake nodes",sum(r[5] for r in details),"removed road cells by original footprint",sum(r[6] for r in details))
with (out/("screenshot-tunnel-"+sys.argv[1]+".out")).open(encoding="utf-8-sig") as f: print([(r[0],r[-3],round(float(r[-2]))) for r in csv.reader(f,delimiter="\t")])
