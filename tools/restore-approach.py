from pathlib import Path
p=Path('tools/bake-bridges.py');s=p.read_text()
s=s.replace("marked=(marks[ay,ax]>0)&~foot[ay,ax]&~wet[ay,ax]",'''minimum=np.full((W,W),np.inf,'f4')
np.minimum.at(minimum,(gy,gx),np.where(zs>=groundZ-50,zs,np.inf))
emerged=minimum[ay,ax]<G[ay,ax]+40
marked=(marks[ay,ax]>0)&(~foot[ay,ax]|emerged)&~wet[ay,ax]''')
s=s.replace("portal=[];endcap=np.zeros(N,bool)","portal=[];endcap=np.zeros(N,bool);axdir={}")
s=s.replace("t=p@axis;depth", "axdir[group]=axis;t=p@axis;depth")
s=s.replace("if len(edges[i])>=8:continue",'''axis=axdir.get(groups[i])
 if axis is None:continue
 if len(edges[i])>=8 and marks[b,a]==0:continue''')
s=s.replace("if not endcap[i] and not(actualroad[b,a] and info[3] and abs(z-R[b,a])<55):continue",'''forward=abs(((ex-x)*axis[0]+(ey-y)*axis[1])/dist)>.6
   buried=forward and marks[b,a]>0 and marks[yy,xx]>0 and info[2]>=z and abs(z-gz)<60 and G[yy,xx]>gz+8
   if not (endcap[i] and forward) and not buried and not(actualroad[b,a] and info[3] and abs(z-R[b,a])<55):continue''')
s=s.replace("if any(abs(float(zs[j])-z)<100","if not buried and any(abs(float(zs[j])-z)<100").replace("if any(abs(float(zs[j])-z)<155","if not buried and any(abs(float(zs[j])-z)<155")
s=s.replace("fmt=struct.Struct",'''# Match hand-drawn targets to a parallel surveyed surface. These matches are
# used for XY target selection only, never as edges between heights.
from scipy.spatial import cKDTree
eligible=np.flatnonzero((np.bincount(groups)[groups]>=25)&(zs>=groundZ-65))
tree=cKDTree(xy[eligible])
snap={};scores={};data=(world.parents[1]/'data/user-roads.bin').read_bytes();off=12
for _ in range(struct.unpack_from('<I',data,8)[0]):
 _,n=struct.unpack_from('<BI',data,off);off+=5
 path=np.frombuffer(data,'<f4',n*2,off).reshape(-1,2).astype(float)*S-.5;off+=8*n
 for a,b in zip(path,path[1:]):
  length=np.linalg.norm(b-a)
  if length<.05:continue
  tangent=(b-a)/length;pts=np.linspace(a,b,2+int(length/2))
  cells=np.floor((pts+.5)/4).astype(int);valid=near[cells[:,1],cells[:,0]]&~actualroad[cells[:,1],cells[:,0]]
  for (x,y) in np.unique(cells[valid],axis=0):
   gc=int(x+y*W);q=np.array([x*4+1.5,y*4+1.5])
   ids=eligible[tree.query_ball_point(q,14.)]
   if not len(ids):continue
   for group in set(groups[ids]):
    if abs(tangent@axdir[group])<.82:continue
    part=ids[groups[ids]==group];ds=np.linalg.norm(xy[part]-q,axis=1)
    j=np.argmin(ds);i=int(part[j]);score=ds[j]+3/np.sqrt(np.bincount(groups,minlength=0)[group]) if False else ds[j]+3/np.sqrt(len(part))
    if score<scores.get(gc,1e10):scores[gc]=score;snap[gc]=i
fmt=struct.Struct''').replace("ds[j]+3/np.sqrt(np.bincount(groups,minlength=0)[group]) if False else ","")
s=s.replace("np.savez(report",''' f.write(struct.pack('<i',len(snap)))
 for cell,node in snap.items():f.write(struct.pack('<ii',cell,groundCount+node))
np.savez(report''')
p.write_text(s)
