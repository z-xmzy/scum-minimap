import csv,gzip,zlib,struct,os,subprocess
from pathlib import Path
import cv2,numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[1]; check=root/"checks";app=root/"package/SCUM-MiniMap/app";world=app/"fine/world"
os.environ["TEMP"]=os.environ["TMP"]=str(root/"temp")
A=np.loadtxt(check/"screenshot-transform.txt")
def uv(p):
    return A@[*p,1]/14481
def pixel(p):
    return np.linalg.solve(A[:,:2],np.array(p)*14481-A[:,2])
with gzip.open(world/"nav-world-grid.bin","rb") as f:
    f.read(20);cost=np.frombuffer(f.read(3804**2),"u1").reshape(3804,3804)
road=np.memmap(world/"roads.f32","<f4","r",shape=(3804,3804))
hand=np.frombuffer(zlib.decompress((world/"handroads.z").read_bytes()),"u1").reshape(3804,3804)
xy=np.indices((498,496))[::-1].reshape(2,-1)
xx,yy=(3804*(A[:,:2]@xy+A[:,2:])/14481).astype(int).reshape(2,498,496)
image=np.asarray(Image.open(check/"screenshot-matched-map.png").convert("RGB")).copy()
green=np.isfinite(road[yy,xx]); red=(hand[yy,xx]>0)
for mask,col in [(red,[245,77,226]),(green,[37,187,255])]:
    image[mask]=(image[mask]*.4+np.array(col)*.6).astype("u1")
Image.fromarray(image).save(check/"screenshot-road-evidence.png")
with (check/"screenshot-road-samples.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.writer(f);w.writerow(["位置","截图X","截图Y","世界格X","世界格Y","导航基础耗费","原始路面高厘米","手工道路掩膜"])
    for label,pts in {"上方偏线":[(216,36),(276,33),(329,44),(357,76),(315,118),(275,170)],"下方偏线":[(268,315),(290,336),(316,368),(321,387),(315,408),(329,432)],"底图道路":[(164,122),(174,146),(248,185),(260,215),(276,255),(253,309),(259,340),(272,388),(291,422)]}.items():
        for x,y in pts:
            i,j=xx[y,x],yy[y,x];w.writerow([label,x,y,i,j,cost[j,i],round(float(road[j,i]),2),hand[j,i]])
tests=[("upper-connect",(191,44),(248,185)),("lower-connect",(254,311),(313,444)),("player-north",(261,359),(193,44)),("screen-passage",(10,118),(325,444))]
with (check/"screenshot-tests.tsv").open("w",encoding="utf8",newline="") as f:
    w=csv.writer(f,delimiter="\t");w.writerows([key,"forward",mode,*uv(start),*uv(end)] for key,start,end in tests for mode in ["Quickest","Shortest","PavedOnly","Walking"])
subprocess.run([app/"BridgeProbe.exe",app/"SkynettMiniMap.exe",check/"screenshot-tests.tsv",check/"screenshot-routes.out"],cwd=app,check=True)
with (check/"screenshot-routes.out").open(encoding="utf-8-sig") as f:
    for r in csv.reader(f,delimiter="\t"):
        pts=np.array([pixel(tuple(map(float,p.split(",")[:2]))) for p in r[-1].split(";") if p])
        if len(pts):
            img=cv2.cvtColor(np.asarray(Image.open(check/"screenshot-matched-map.png").convert("RGB")),cv2.COLOR_RGB2BGR)
            cv2.polylines(img,[np.round(pts).astype("i4")],False,(220,220,44),2)
            cv2.imencode(".png",img)[1].tofile(check/("screenshot-"+r[0]+"-"+r[2]+".png"))
            print(r[0],r[2],r[-2],len(pts),pts.round().min(0),pts.round().max(0))
