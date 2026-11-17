from pathlib import Path
import numpy as np,csv,struct,gzip
from scipy import ndimage
from scipy.spatial import cKDTree
from scipy.sparse.csgraph import connected_components
from scipy.sparse import coo_matrix
r=Path(__file__).resolve().parents[1];checks=r/'checks';d=np.load(checks/'bridge-geometry.npz');k,z,ground,g,portals=[d[v] for v in ('keys','z','ground','groups','portals')];S=15216;W=3804;xy=np.c_[k%S+.5,k//S+.5];sizes=np.bincount(g)
world=r/'package/SCUM-MiniMap/app/fine/world';gr=np.memmap(r.parent/'SCUM-MiniMap-world3-20261112/bake/ground.f32','f','r',shape=(W,W))
with gzip.open(r/'source/resources/nav-bridges.bin') as f:
 _,_,ng,_=struct.unpack('<4siii',f.read(16));low=np.frombuffer(f.read(ng*11),[('k','<i4'),('c','B'),('t','B'),('h','<f4'),('road','?')])
heights=dict(zip(low['k'],low['h']));roads=set(low['k'][low['road']]);cells=k%S//4+(k//S//4)*W
inputs=[];meta=[];untested=[]
def add(name,label,group,a,b):
 meta.append([name,label,int(group)+1])
 for suffix,(p,q) in [('去',(a,b)),('回',(b,a))]:
  for mode in ['Quickest','Shortest','PavedOnly','Walking']:
   inputs.append([name,label+suffix,mode,p[0],p[1],q[0],q[1],p[2],q[2]])
def clusters(points,radius):
 if len(points)==1:return [np.array([0])]
 pairs=cKDTree(points).query_pairs(radius,output_type='ndarray').reshape(-1,2)
 graph=coo_matrix((np.ones(len(pairs)),(pairs[:,0],pairs[:,1])),shape=(len(points),len(points)))
 count,labels=connected_components(graph,directed=False);return [np.flatnonzero(labels==n) for n in range(count)]
def upper(i):return (*list(xy[i]/S),float(z[i]))
# Same XY, different measured Z, at every distinct overlap of a continuous bridge component and lower road.
ids=np.array([i for i,c in enumerate(cells) if sizes[g[i]]>=25 and c in roads and z[i]-heights[c]>=350],dtype=int)
for group in np.unique(g[ids]):
 members=ids[g[ids]==group]
 for n,part in enumerate(clusters(xy[members],5)):
  sel=members[part];mid=xy[sel].mean(0);i=sel[np.argmin(np.linalg.norm(xy[sel]-mid,axis=1))];p=upper(i)
  add('X%d-%d'%(group+1,n),'同坐标换层',group,(*p[:2],float(heights[cells[i]])),p)
for group in np.unique(g[portals[:,0]]):
 links=portals[g[portals[:,0]]==group];members=np.flatnonzero((g==group)&(z-ground>=210))
 for n,arr in enumerate(clusters(xy[links[:,0]],9)):
  row=links[arr];loc=xy[row[:,0]];mid=loc.mean(0)
  if not len(members):untested.append([group+1,n,'入口处无可区分的上层']);continue
  ds=np.linalg.norm(xy[members]-mid,axis=1);sel=members[ds<=max(65,ds.min()+1)]
  i=sel[np.argmin(abs(np.linalg.norm(xy[sel]-mid,axis=1)-16))]
  q=row[np.argmin(np.linalg.norm(loc-mid,axis=1))];cell=q[1]
  add('E%d-%d'%(group+1,n),'接坡',group,((cell%W+.5)/W,(cell//W+.5)/W,float(heights[cell])),upper(i))
for name,arr in [('heads',inputs),('heads-expected',meta),('heads-skipped',untested)]:
 with (checks/(name+'.tsv')).open('w',encoding='utf8',newline='') as f:csv.writer(f,delimiter='\t').writerows(arr)
print('per group/crossing tests',len(meta),'calls',len(inputs),'no significant clearance',len(untested))
