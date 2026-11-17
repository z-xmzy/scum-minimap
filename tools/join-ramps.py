from pathlib import Path
from collections import defaultdict
import numpy as np
from scipy.spatial import cKDTree
from scipy.sparse.csgraph import connected_components
from scipy.sparse import coo_matrix
d=np.load('checks/bridge-geometry.npz');S=15216;keys,z=d['keys'],d['z']
raw=np.fromfile('tools/bridge-ramps/cells.bin',[('k','<i4'),('h','<f4')]);xyz=np.c_[keys%S,keys//S,z/65]
rk,rh=raw['k'],raw['h'];coords=np.c_[rk%S,rk//S,rh/65];pairs=cKDTree(coords).query_pairs(1.78,output_type='ndarray')
dist,idx=cKDTree(xyz).query(coords)
tight=(dist<=1.62)&(abs(rh-z[idx])<=58)
labels=connected_components(coo_matrix((np.ones(len(pairs)),(pairs[:,0],pairs[:,1])),shape=(len(raw),len(raw))),directed=False)[1]
sel=np.isin(labels,np.unique(labels[tight]));raw[sel].tofile('tools/bridge-ramps/joined.bin')
print('ramp samples attached',sel.sum(),'of',len(sel))
