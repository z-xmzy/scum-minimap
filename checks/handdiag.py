from pathlib import Path
import csv,collections,struct,gzip,numpy as np
from scipy.spatial import cKDTree
d=np.load('checks/bridge-geometry.npz');key,gr,z,g=[d[k] for k in ('keys','groups','z','ground')];xy=np.c_[key%15216+.5,key//15216+.5];tree=cKDTree(xy);cnt=collections.Counter(gr);open_=set(gr[d['portals'][:,0]])
rows=list(csv.reader(open('checks/area.out',encoding='utf8'),delimiter='\t'));ground=np.memmap('../SCUM-MiniMap-world3-20261112/bake/ground.f32','f','r',shape=(3804,3804))
for c in rows:
 if c[-3]!='False' or c[0][0]!='H' or '去' not in c[1] or c[2]!='Quickest':continue
 print(c[0],end=' ')
 for u,v in [c[3:5],c[5:7]]:
  pos=np.array([float(u),float(v)])*15216
  ds,ids=tree.query(pos,k=32);i=next((i for i in ids if cnt[gr[i]]>25),ids[0])
  print('offset',round(np.linalg.norm(pos-xy[i]),1),'group',gr[i]+1,'size',cnt[gr[i]],'open',gr[i] in open_,'clear',round((z[i]-g[i])/100,1),end=';')
 print()