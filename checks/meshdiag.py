from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
from collections import Counter
p=Path('checks')
d=np.load(p/'bridge-geometry.npz')
key,z,groups,ground=(d[k] for k in ('keys','z','groups','ground'))
xy=np.c_[key%15216+.5,key//15216+.5]
tree=cKDTree(xy)
opened=set(groups[d['portals'][:,0]]); counts=Counter(groups)
for row in (p/'sample.tsv').read_text(encoding='utf-8-sig').splitlines():
 c=row.split('\t')
 if c[1]!='桥上直行去':continue
 print(c[0])
 for u,v in [c[3:5],c[5:7]]:
  pos=np.array([float(u),float(v)])*15216
  ids=tree.query_ball_point(pos,4.5);g=set(groups[ids])
  for k in sorted(g,key=lambda k:-counts[k])[:6]:
   i=min((i for i in ids if groups[i]==k),key=lambda i:np.linalg.norm(pos-xy[i]))
   print(k+1,counts[k],k in opened,'dist',round(np.linalg.norm(pos-xy[i]),1),'dz', round(z[i]-ground[i]),end=' | ')
  print()