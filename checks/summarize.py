from pathlib import Path
import numpy as np,csv,re,collections,itertools,math
from scipy.spatial import cKDTree
root=Path(__file__).resolve().parents[1]; old=root.parent/'SCUM-2.4.9-all-bridges'; folder=root/'checks'
d=np.load(folder/'bridge-geometry.npz'); key=d['keys']; gr=d['groups']+1; gates=d['portals']; S=15216;W=3804
# Validate each reported layer change against independently decoded physical endpoints of its link.
portals=collections.defaultdict(set)
for ix,g in gates:
 portals[gr[ix]].add((int(key[ix]),int(g)))
def pixel(u,v,w):return min(w-1,max(0,int(float(u)*w)))+w*min(w-1,max(0,int(float(v)*w)))
records=[]; trans=bad=0
for file in ['area','extra','approach','other','stacked','levels','decks','heads','through','via-deck']:
 f=folder/(file+'.out')
 if not f.exists():continue
 for row in f.open(encoding='utf-8-sig'):
  c=row.rstrip('\r\n').split('\t')
  if len(c)<10:continue
  id,test,mode=c[:3]; ok=c[-3]=='True';groups=[];cross=invalid=0
  if ok:
   pts=[i.split(',') for i in c[-1].split(';')]
   groups=[int(p[2]) for p in pts]
   for p,q in zip(pts,pts[1:]):
    if p[2]==q[2]:continue
    cross+=1
    if p[2]=='0':p,q=q,p
    if q[2]!='0' or (pixel(*p[:2],S),pixel(*q[:2],W)) not in portals[int(p[2])]:invalid+=1
  trans+=cross;bad+=invalid
  label='无路线' if not ok else '桥下' if not any(groups) else '桥面' if len(set(groups))==1 else '经接入口'
  direct=math.hypot((float(c[3])-float(c[5]))*15216.18,(float(c[4])-float(c[6]))*15236.18)
  records.append([file,id,test,mode,ok,round(float(c[-2]),1),round(direct,1),label,groups[0] if groups else '',groups[-1] if groups else '',cross,invalid])
with (folder/'逐条复测.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['用例集','ID','测试','模式','规划成功','路线米','直线米','实际走层','起始层','结束层','换层数','无入口的换层']);w.writerows(records)
print('rows',len(records),'transitions',trans,'bad',bad)
for file,arr in itertools.groupby(records,key=lambda r:r[0]):
 rows=list(arr); print(file,len(rows),sum(r[4] for r in rows))
def summary(pred):return dict(collections.Counter(r[7] for r in records if pred(r)))
for label in ('桥上直行','桥下直行','换层','全长','模型端到端','实测桥面','实测下穿','接坡'):
 print(label,summary(lambda r:r[2].startswith(label) and r[3]=='Quickest'))
for r in records:
 if r[3]=='Quickest' and r[2] in ('桥上直行去','桥下直行去'):print(*r[1:3],r[7],r[5:7],r[8:10])