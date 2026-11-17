from pathlib import Path
import csv,collections,gzip,struct,numpy as np
folder=Path('checks');data=np.load(folder/'bridge-geometry.npz');k,g,portals=[data[n] for n in ('keys','groups','portals')];g=g+1;S=15216;W=3804;xy=np.c_[k%S+.5,k//S+.5]
with gzip.open('source/resources/nav-bridges.bin') as f:
 _,_,ng,_=struct.unpack('<4siii',f.read(16));cells=np.frombuffer(f.read(11*ng),[('k','<i4'),('c','B'),('t','B'),('h','<f4'),('r','?')])
low={int(v['k']):v for v in cells};rows=[];meta=[]
for r in csv.DictReader((folder/'两岸接入口清单.csv').open(encoding='utf-8-sig')):
 group=int(r['层'])
 if r['结论']!='两端均有接入口' or float(r['跨度米'])<10:continue
 surf=xy[g==group];direction=np.linalg.eigh(np.cov(surf.T))[1][:,-1];mid=surf.mean(0);t=(surf-mid)@direction;links=portals[g[portals[:,0]]==group];l=(xy[links[:,0]]-mid)@direction
 ends=[]
 for sign in [-1,1]:
  cand=links[l*sign>=max(l*sign)-min(6,np.ptp(t)*.14)]
  i,c=min(cand,key=lambda p:(low[int(p[1])]['c'],abs(direction[0]*(xy[p[0],1]-mid[1])-direction[1]*(xy[p[0],0]-mid[0])),np.linalg.norm(xy[p[0]]-mid)*-.03))
  c=int(c);ends.append(((c%W+.5)/W,(c//W+.5)/W,float(low[c]['h'])))
 if ends[0][:2]==ends[1][:2]:continue
 id='岸%d'%group;meta.append([id,group,r['区域'],r['类别'],r['跨度米']])
 for s,(a,b) in [('去',ends),('回',ends[::-1])]:
  for mode in ['Quickest','Shortest','PavedOnly','Walking']:rows.append([id,'两岸穿桥'+s,mode,*a[:2],*b[:2],a[2],b[2]])
for name,data in [('through',rows),('through-expected',meta)]:
 with (folder/(name+'.tsv')).open('w',encoding='utf8',newline='') as f:csv.writer(f,delimiter='\t').writerows(data)
print('shores',len(meta),'calls',len(rows))
