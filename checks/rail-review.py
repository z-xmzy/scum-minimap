import csv,numpy as np,collections
from pathlib import Path
folder=Path('tools/model-review');tri=np.memmap(folder/'triangles.bin','<f4','r').reshape(-1,3,3)
d=np.load('checks/bridge-geometry.npz');k,z,g=[d[v] for v in ('keys','z','groups')];xy=np.c_[k%15216+.5,k//15216+.5]
ids=np.flatnonzero(g==1382);p=xy[ids];axis=np.linalg.eigh(np.cov(p.T))[1][:,-1];t=p@axis
tips=[p[t<=t.min()+1].mean(0),p[t>=t.max()-1].mean(0)]
rows=[]
for r in csv.reader((folder/'index.tsv').open(encoding='utf-8-sig'),delimiter='\t'):
 if not r[0].endswith('Landscape_A_3_1c'):continue
 raw=np.array(tri[int(r[5]):int(r[5])+int(r[6])],float)
 raw[:,:,0]=(617718-raw[:,:,0])/100;raw[:,:,1]=(618618-raw[:,:,1])/100*15216.18/15236.18
 distance=[np.linalg.norm(raw[:,:,:2]-tip,axis=2).min() for tip in tips]
 if min(distance)<20:rows.append((r[3].split('.')[-1],r[2],distance,np.min(raw[:,:,2]),np.max(raw[:,:,2])))
print('tips',tips)
for r in rows:print(r)
