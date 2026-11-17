from pathlib import Path
import numpy as np,csv,collections
from scipy.spatial import cKDTree
d=np.load('checks/bridge-geometry.npz');k,z,g,ground=[d[n] for n in ('keys','z','groups','ground')];xy=np.c_[k%15216+.5,k//15216+.5];ids=np.flatnonzero(g==4323);v=xy[ids];axis=np.linalg.eigh(np.cov(v.T))[1][:,-1];t=v@axis
raw=np.fromfile('tools/bridge-ramps/cells.bin',[('k','i4'),('z','f4')]);rxy=np.c_[raw['k']%15216+.5,raw['k']//15216+.5]
for sign in [-1,1]:
 sel=ids[t*sign>=max(t*sign)-3];i=sel[np.argmin(abs((xy[sel]-xy[sel].mean(0))@axis[::-1]))]
 print('end xy',xy[i],'z',z[i],'ground',ground[i], 'dz',z[i]-ground[i])
 dist=np.linalg.norm(rxy-xy[i],axis=1);near=np.flatnonzero(dist<15);print('ramps nearby',len(near))
 for j in near[np.argsort(dist[near])[:12]]:print('offset',rxy[j]-xy[i],'z',raw['z'][j],'gap',raw['z'][j]-z[i])
# through suite does not require crossing if a level dry road continues at grade.
exp={r[0]:r for r in csv.reader(open('checks/through-expected.tsv',encoding='utf8'),delimiter='\t')}
calls=list(csv.reader(open('checks/through.out',encoding='utf8'),delimiter='\t'))
for c in calls:
 if c[2]!='Quickest' or not c[1].endswith('去'):continue
 groups=[int(p.split(',')[-1]) for p in c[-1].split(';')] if c[-3]=='True' else []
 if c[-3]!='True':print('failed shores',exp[c[0]],c[3:7])
print('shore calls',len(calls),'successful',sum(c[-3]=='True' for c in calls))
