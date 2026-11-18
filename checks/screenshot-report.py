"""Summarize released executable probes and align the comparison to the exact screenshot."""
import csv,hashlib,sys
from pathlib import Path
import cv2,numpy as np
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parents[1];check=root/"checks"
A=np.loadtxt(check/"screenshot-transform.txt")
exe=root/"package/SCUM-MiniMap/app/SkynettMiniMap.exe"
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def load(path):
    with path.open(encoding="utf-8-sig") as f:
        return {(r[0],r[1],r[2]):r for r in csv.reader(f,delimiter="\t")}
def xy(row):
    uv=np.array([list(map(float,p.split(",")[:2])) for p in row[-1].split(";") if p])
    return np.linalg.solve(A[:,:2],(uv*14481-A[:,2]).T).T
old=load(check/"screenshot-before.tsv");new=load(check/"screenshot-after.tsv")
rows=[];names={"upper-connect":"上方弯道","lower-connect":"下方南北段","player-north":"箭头向北","screen-passage":"截图内长段"}
modes={"Quickest":"驾车走线","Shortest":"驾车半越野","PavedOnly":"驾车全越野","Walking":"步行"}
for scenario in names:
    for mode in modes:
        a=old[scenario,mode,"FindAtHeight"];b=new[scenario,mode,"FindAtHeight"]
        base=new[scenario,mode,"FindBaseRoute"]
        assert b[3]=="True" and b[-1]==base[-1]
        rows.append([names[scenario],modes[mode],a[3],round(float(a[4]),2),b[3],round(float(b[4]),2),len(xy(b)),"是"])
with (check/"screenshot-四组16条正式路线.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.writer(f); w.writerow(["点对","模式","修前成功","修前距离米","修后成功","修后距离米","修后折点数","修后与基础路由逐点一致"]);w.writerows(rows)
shot=Image.open(sys.argv[1]).convert("RGB");basepic=Image.open(check/"screenshot-matched-map.png").convert("RGB")
W,H=shot.size; pad=24;gap=20;top=121; image=Image.new("RGB",(2*pad+3*W+2*gap,top+H+99),"#f5f5f0")
draw=ImageDraw.Draw(image)
def font(n,bold=False):return ImageFont.truetype("C:/Windows/Fonts/msyh"+("bd" if bold else "")+".ttc",n)
draw.text((pad,15),"C3 东缘 · 隧道口导航复测",font=font(28,True),fill="#202b2c")
draw.text((pad,61),"黄色为卫星底图道路，青色为导航。中、右两图使用相同的局部起终点和驾车半越野模式。",font=font(18),fill="#425351")
columns=[("用户截图",shot),("修复前   3,742 米",basepic.copy()),("修复后   1,155 米",basepic.copy())]
for i,(title,pic) in enumerate(columns):
    x=pad+i*(W+gap)
    draw.text((x,91),title,font=font(20,True),fill="#253e3c")
    if i:
        row=(old if i==1 else new)["player-north","Shortest","FindAtHeight"]
        pp=xy(row); pic=pic.resize((W*2,H*2),Image.Resampling.BILINEAR); d=ImageDraw.Draw(pic)
        points=[tuple((pt*2).round()) for pt in pp]
        d.line(points,fill="#174246",width=10,joint="curve");d.line(points,fill="#35d6c8",width=6,joint="curve")
        for label,pt in [("起",pp[0]),("终",pp[-1])]:
            px,py=pt*2
            d.ellipse((px-18,py-18,px+18,py+18),fill="#fffaf1",outline="#193b3b",width=4)
            d.text((px,py-1),label,font=font(25,True),fill="#173536",anchor="mm")
        pic=pic.resize((W,H),Image.Resampling.LANCZOS)
    image.paste(pic,(x,top))
draw.text((pad,top+H+16),"复核位置由 193 个底图特征匹配确定。局部终点取截图上方弯道交会处，截图之外的原始目的地未知。",font=font(18),fill="#324644")
draw.text((pad,top+H+48),"同一对路点使用“驾车走线”，修复前无路线，修复后沿道路 1,702 米。",font=font(18),fill="#324644")
image.save(check/"screenshot-C3路线修复前后.png")
print("official exe",digest(exe),"cases",len(rows),"exact match",sum(r[-1]=="是" for r in rows))
