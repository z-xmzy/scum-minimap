from pathlib import Path
import csv,gzip,zlib,struct
import numpy as np,cv2
from PIL import Image
root=Path("checks");W=3804
A=np.loadtxt(root/"screenshot-transform.txt"); base=np.asarray(Image.open(root/"screenshot-matched-map.png").convert("RGB")).copy()
with gzip.open("source/resources/nav-world-grid.bin","rb") as f:
    f.read(20);cost=np.frombuffer(f.read(W*W),"u1").reshape(W,W).copy()
with gzip.open("source/resources/nav-world-terrain.bin","rb") as f:
    f.read(20);terrain=np.frombuffer(f.read(W*W),"u1").reshape(W,W).copy()
print("base data hashes", flush=True)
with gzip.open("source/resources/nav-bridges.bin","rb") as f:
    h,w,n,m=struct.unpack("<4siii",f.read(16))
    overrides=np.frombuffer(f.read(n*11),[("k","<i4"),("c","u1"),("t","u1"),("z","<f4"),("r","?")])
xx,yy=(A[:,:2]@np.indices((498,496))[::-1].reshape(2,-1)+A[:,2:]).reshape(2,498,496)*W/14481
xx,yy=xx.astype(int),yy.astype(int)
v=(overrides["k"]%W >=xx.min())&(overrides["k"]%W<=xx.max())&(overrides["k"]//W>=yy.min())&(overrides["k"]//W<=yy.max())
print("ROI override count",v.sum(),"of",len(overrides), "road marks before override",np.sum(cost[yy,xx]<=2),"terrain4onroads",np.sum((cost[yy,xx]<=2)&(terrain[yy,xx]>=4)))
c0=cost.copy(); t0=terrain.copy()
cost.flat[overrides["k"]]=overrides["c"]; terrain.flat[overrides["k"]]=overrides["t"]
print("ROI road marks after override",np.sum(cost[yy,xx]<=2),"tr4road",np.sum((cost[yy,xx]<=2)&(terrain[yy,xx]>=4)))
for name,a in [("roads-before",c0<=2),("roads-after",cost<=2),("terrain4",terrain>=4),("blocked",cost==255)]:
    b=base.copy();mask=a[yy,xx];b[mask]=[255,0,0];Image.fromarray(b).save(root/("screenshot-"+name+".png"))
print("changes",np.sum(c0[yy,xx]!=cost[yy,xx]), "terrain changes",np.sum(t0[yy,xx]!=terrain[yy,xx]))
for x,y in [(191,44),(248,185),(254,311),(313,444),(261,359),(193,44)]:
    i,j=xx[y,x],yy[y,x];print((x,y),i,j,c0[j,i],t0[j,i],cost[j,i],terrain[j,i])
d=np.load("checks/bridge-geometry.npz");k=d["keys"]; p0=np.c_[k%15216+.5,k//15216+.5]/15216*14481
pix=np.linalg.solve(A[:,:2],(p0-A[:,2]).T).T
from collections import Counter
inroi=(pix.min(1)>=0)&(pix[:,0]<496)&(pix[:,1]<498)
print("screen decks",Counter(d["groups"][inroi]+1))
for n in sorted(set(d["groups"][inroi]+1)):
    rows0=d["groups"]+1==n;pix0=pix[rows0]; print(n,"deck (pixel range)",pix0.min(0).round(),pix0.max(0).round(),"height delta cm",np.round(np.quantile(d["z"][rows0]-d["ground"][rows0],[0,.5,1])))
for cond in [(cost!=c0)&(c0<=2), (terrain>=4)&(t0<4)]:
    v=np.argwhere(cond[yy.min():yy.max()+1,xx.min():xx.max()+1])+[yy.min(),xx.min()]
    for (j,i) in v[:60]:
        sp=np.linalg.solve(A[:,:2],(np.array([i+.5,j+.5])*14481/W-A[:,2]))
        print("change",i,j,sp.round(),c0[j,i],cost[j,i],t0[j,i],terrain[j,i])
groups=[4370,4371,4373,4374,4375,4376]; d=np.load("checks/bridge-geometry.npz"); roi=np.isin(d["groups"]+1,groups); index0={int(k):(float(z),int(g+1)) for k,z,g in zip(d["keys"][roi],d["z"][roi],d["groups"][roi])}
import csv
for folder in ["bridge-meshes","bridge-ramps","bridge-underroads"]:
    off=0;b=np.memmap("tools/"+folder+"/cells.bin",dtype=[("k","<i4"),("z","<f4")],mode="r")
    for row in csv.reader(open("tools/"+folder+"/placements.tsv",encoding="utf-8-sig"),delimiter="\t"):
        n=int(row[-1]);p=b[off:off+n];off+=n
        gs=Counter(index0[k][1] for k,z in p if k in index0 and abs(z-index0[k][0])<21)
        if gs:print(folder,row[0].split("/")[-1],row[1],row[2],row[3].split(".")[-1],gs)
