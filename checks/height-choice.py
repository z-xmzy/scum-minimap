import csv,numpy as np,collections
d=np.load('checks/bridge-geometry.npz');k,z,g=[d[n] for n in ('keys','z','groups')];xy=np.c_[k%15216+.5,k//15216+.5];sizes=np.bincount(g)
for c in csv.reader(open('checks/heads.tsv',encoding='utf8'),delimiter='\t'):
 if c[0] not in ['X3964-0','X3964-1'] or c[1]!='同坐标换层去' or c[2]!='Quickest':continue
 pos=np.array(c[3:5],float)*15216;ids=np.flatnonzero(np.linalg.norm(xy-pos,axis=1)<1.15);print(c[0],c[7:9])
 for i in ids:print('group',g[i]+1,'size',sizes[g[i]],'z',z[i],'offsetfromdeck',z[i]-float(c[8]))
