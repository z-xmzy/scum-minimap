"""Bake independent surface nodes and terrain-matched entrances from island mesh samples."""
from pathlib import Path
from collections import defaultdict
import csv, gzip, struct, math, sys
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from scipy import ndimage as nd
root=Path(__file__).resolve().parents[1]
mesh=root/'tools/bridge-meshes'
world=root/'package/SCUM-MiniMap/app/fine/world'
terrain=root.parent/'SCUM-MiniMap-world3-20261112/bake'
W=3804; S=W*4; groundCount=W*W
D=lambda f:np.memmap(f,'<f4','r',shape=(W,W))
G=D(terrain/'ground.f32');R=D(world/'roads.f32');water=D(world/'water.f32');old=D(world/'deck-used.f32')
obs=np.memmap(terrain/'obstacles.u8','u1','r',shape=(W,W))
with gzip.open(world/'nav-world-grid.bin','rb') as f:f.read(20);cost=np.frombuffer(f.read(W*W),'u1').reshape(W,W)
import zlib
marks=np.frombuffer(zlib.decompress((world/'handroads.z').read_bytes()),'u1').reshape(W,W)
dem=np.memmap(world/'height.f32','<f4','r',shape=(10161,10161))
def ground(x,y):
 a=np.clip((14.82+(x+.5)*15216.18/S)/1.5,0,10159.99999);b=np.clip((5.82+(y+.5)*15236.18/S)/1.5,0,10159.99999)
 i=a.astype(int);j=b.astype(int);fx=a-i;fy=b-j
 p=dem[j,i];q=dem[j,i+1];r=dem[j+1,i];t=dem[j+1,i+1]
 return np.where(fx+fy<=1,p+fx*(q-p)+fy*(r-p),t+(1-fx)*(r-t)+(1-fy)*(q-t))
def is_deck(n):
 n=n.lower()
 return not any(k in n for k in ('_wm','_es','_fn','fence','rope','cable','pillar','railing','support','traffic_light','cement_curb','_door','_wall','windows','_rvt'))
buf=np.fromfile(mesh/'cells.bin',[('key','<i4'),('z','<f4')])
bins=defaultdict(list);keys=[];zs=[];off=0
for r in csv.reader(open(mesh/'placements.tsv',encoding='utf-8-sig'),delimiter='\t'):
 n=int(r[-1]);part=buf[off:off+n];off+=n
 if not is_deck(r[3].split('.')[-1]):continue
 for pt in part:
  k=int(pt['key']);z=float(pt['z'])
  if any(abs(zs[i]-z)<=20 for i in bins[k]):continue
  bins[k].append(len(keys));keys.append(k);zs.append(z)
assert off==len(buf)
# A bridge end piece can be a U-shaped mesh around a separate asphalt insert.
# Join only surveyed road samples that meet the deck in XYZ; a road underneath
# with several metres of clearance cannot contribute to this upper surface.
from scipy.spatial import cKDTree
original=np.asarray(keys);xyz=np.c_[original%S,original//S,np.asarray(zs)/35]
road=np.fromfile(root/'tools/bridge-underroads/cells.bin',[('key','<i4'),('z','<f4')])
rk=road['key'];rh=road['z'];distance,idx=cKDTree(xyz).query(np.c_[rk%S,rk//S,rh/35])
on_ground=ground(rk%S,rk//S)
bridge_road=(distance<=6.5)&(abs(rh-np.asarray(zs)[idx])<=38)
bridge_road|=rh>on_ground+150
for k,z in zip(rk[bridge_road],rh[bridge_road]):
 k=int(k);z=float(z)
 if any(abs(zs[i]-z)<=20 for i in bins[k]):continue
 bins[k].append(len(keys));keys.append(k);zs.append(z)
for k,z in np.fromfile(root/'tools/bridge-ramps/joined.bin',[('key','<i4'),('z','<f4')]):
 k=int(k);z=float(z)
 if any(abs(zs[i]-z)<=20 for i in bins[k]):continue
 bins[k].append(len(keys));keys.append(k);zs.append(z)
keys=np.array(keys,dtype='i4');zs=np.array(zs,dtype='f4');xs=keys%S;ys=keys//S
N=len(keys);edges=[[] for _ in keys]
for i,(k,z) in enumerate(zip(keys.tolist(),zs.tolist())):
 for dx,dy in [(1,0),(0,1),(1,1),(-1,1)]:
  for j in bins.get(k+dx+dy*S,()):
   if abs(z-float(zs[j]))>20+48*math.hypot(dx,dy):continue
   edges[i].append(j);edges[j].append(i)
orig=np.repeat(np.arange(N),[len(t) for t in edges]);dest=np.fromiter((j for v in edges for j in v),np.int32)
graph=coo_matrix((np.ones(len(orig),'u1'),(orig,dest)),shape=(N,N)).tocsr();count,groups=connected_components(graph,directed=False)
del graph
xy=np.c_[xs,ys];groundZ=ground(xs,ys);gx=xs//4;gy=ys//4
wet=(G<=0)|(water>G+15);actualroad=np.isfinite(R)&(abs(R-G)<240)&(~wet | (R>water+35))
foot=np.zeros((W,W),bool);foot[gy,gx]=True;foot|=np.isfinite(old)
# Correct the borrowed road and water classifications around the physical structure.
near=nd.binary_dilation(foot,iterations=5);ay,ax=np.where(near)
grade=np.hypot((G[ay,ax+1]-G[ay,ax-1])/800,(G[ay+1,ax]-G[ay-1,ax])/800)
cls=sum((grade>np.tan(np.radians(t))).astype('u1') for t in [15,22,30,40])
base=np.where(obs[ay,ax]!=0,8,4)
base=np.where((obs[ay,ax]&128)!=0,255,base)
base=np.where(wet[ay,ax],16,base)
# Outside the footprint, surveyed dry approach roads retain their declared surface.
minimum=np.full((W,W),np.inf,'f4')
np.minimum.at(minimum,(gy,gx),np.where(zs>=groundZ-50,zs,np.inf))
emerged=minimum[ay,ax]<G[ay,ax]+40
marked=(marks[ay,ax]>0)&(~foot[ay,ax]|emerged)&~wet[ay,ax]
road=actualroad[ay,ax]|marked
base=np.where(road,np.where(marks[ay,ax]==2,2,1),base);cls[road]=0
# Hold ordinary open terrain outside the affected footprint to its published cost.
base=np.where(~foot[ay,ax]&~wet[ay,ax]&~road,cost[ay,ax],base)
lowerZ=np.where(actualroad[ay,ax],R[ay,ax],G[ay,ax])
lower=dict(zip((ax+ay*W).tolist(),zip(base.tolist(),cls.tolist(),lowerZ.tolist(),actualroad[ay,ax].tolist())))
# Read the supporting surface along each connection, including gaps between
# its endpoints. A similar height at the far bank cannot bridge an air gap.
road_samples=defaultdict(list)
for k,z in np.fromfile(root/'tools/bridge-underroads/cells.bin',[('key','<i4'),('z','<f4')]):
 road_samples[int(k)].append(float(z))
def supported_entry(node,ex,ey,target_height):
 x=float(xs[node]);y=float(ys[node]);z=float(zs[node])
 distance=math.hypot(ex-x,ey-y)
 if abs(z-target_height)>45*distance:return False
 t=np.linspace(0,1,max(3,int(math.ceil(distance*2))+1))
 px=x+(ex-x)*t;py=y+(ey-y)*t;expected=z+(target_height-z)*t
 terrain_z=ground(px,py)
 for xx,yy,want,h in zip(px[1:-1],py[1:-1],expected[1:-1],terrain_z[1:-1]):
  cell=int(round(xx))+int(round(yy))*S
  choices=[float(zs[j]) for j in bins.get(cell,()) if groups[j]==groups[node]]
  choices.extend(road_samples.get(cell,()))
  a=int((xx+.5)//4);b=int((yy+.5)//4)
  if h>0 and h>=water[b,a]-15:choices.append(float(h))
  if not choices or min(abs(v-want) for v in choices)>55:return False
 return True
portal=[];endcap=np.zeros(N,bool);axdir={}
for group in range(count):
 ids=np.flatnonzero(groups==group)
 if len(ids)<4:continue
 p=xy[ids];axis=np.linalg.eigh(np.cov(p.T))[1][:,-1]
 axdir[group]=axis;t=p@axis;depth=min(4.,max(1.,np.ptp(t)*.12))
 endcap[ids]=(t<=t.min()+depth)|(t>=t.max()-depth)
# A portal must be on the boundary, joined to a dry exterior ground cell at nearly the
# same physical height. Interior side entrances also require a continuous road mesh.
for i in range(N):
 k=int(keys[i]);z=float(zs[i]);x=int(xs[i]);y=int(ys[i]);a=x//4;b=y//4
 axis=axdir.get(groups[i])
 if axis is None:continue
 if len(edges[i])>=8 and marks[b,a]==0:continue
 gz=float(groundZ[i]);surface=R[b,a] if actualroad[b,a] else gz
 if abs(z-surface)>85:continue
 for yy in range(b-1,b+2):
  for xx in range(a-1,a+2):
   gc=xx+yy*W;info=lower.get(gc)
   if info is None or info[0]>=16 or info[1]>=4:continue
   ex=xx*4+1.5;ey=yy*4+1.5;dist=math.hypot(x-ex,y-ey)
   if dist>5 or dist<.1 or abs(z-info[2])>45+32*dist:continue
   forward=abs(((ex-x)*axis[0]+(ey-y)*axis[1])/dist)>.6
   buried=forward and marks[b,a]>0 and marks[yy,xx]>0 and info[2]>=z and abs(z-gz)<60 and G[yy,xx]>gz+8
   if not (endcap[i] and forward) and not buried and not(actualroad[b,a] and info[3] and abs(z-R[b,a])<55):continue
   # Require the ground centre outside this deck at this height, with the mesh
   # actually ending in its direction. Never join through an interior deck cell.
   beyond=k+int(round((ex-x)/dist))+S*int(round((ey-y)/dist))
   if not buried and any(abs(float(zs[j])-z)<100 for j in bins.get(beyond,())):continue
   if not buried and any(abs(float(zs[j])-z)<155 for j in bins.get(round(ex)+round(ey)*S,())):continue
   if not supported_entry(i,ex,ey,info[2]):continue
   edges[i].append(gc+N);portal.append((i,gc))
# Match hand-drawn targets to a parallel surveyed surface. These matches are
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
    j=np.argmin(ds);i=int(part[j]);score=ds[j]+3/np.sqrt(len(part))
    if score<scores.get(gc,1e10):scores[gc]=score;snap[gc]=i
fmt=struct.Struct('<iffi');report=root/'checks'
with gzip.open(root/'source/resources/nav-bridges.bin','wb',compresslevel=6) as f:
 f.write(struct.pack('<4siii',b'BRG1',W,len(lower),N))
 for gc,(cost_,cl,height,rd) in lower.items():f.write(struct.pack('<iBBf?',gc,cost_,cl,height,rd))
 for i,row in enumerate(edges):
  f.write(struct.pack('<ifii',int(keys[i]),float(zs[i]),int(groups[i])+1,len(row)))
  for j in row:f.write(struct.pack('<i',groundCount+j if j<N else j-N))
 f.write(struct.pack('<i',len(snap)))
 for cell,node in snap.items():f.write(struct.pack('<ii',cell,groundCount+node))
 f.write((root/'tools/bridge-underroads/links.bin').read_bytes())
np.savez(report/'bridge-geometry.npz',keys=keys,z=zs,groups=groups,portals=np.array(portal),ground=groundZ)
print('1m bridge nodes',N,'components',count,'ground cells',len(lower),'portal links',len(portal),'components with portals',len(set(groups[i] for i,j in portal)))
