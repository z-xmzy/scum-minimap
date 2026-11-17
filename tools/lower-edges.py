"""Certify short ground-road arcs from the continuous 1 m road surface under each bridge."""
from pathlib import Path
import gzip,struct,numpy as np
from scipy.spatial import cKDTree
from scipy import ndimage as nd
world=Path('package/SCUM-MiniMap/app/fine/world');W=3804;S=W*4
rd=np.fromfile('tools/bridge-underroads/cells.bin',[('k','<i4'),('z','<f4')]);k=rd['k'];x=k%S;y=k//S
dem=np.memmap(world/'height.f32','<f4','r',shape=(10161,10161))
u=np.clip((14.82+(x+.5)*15216.18/S)/1.5,0,10159.999);v=np.clip((5.82+(y+.5)*15236.18/S)/1.5,0,10159.999)
a=u.astype(int);b=v.astype(int);du=u-a;dv=v-b
p=dem[b,a];q=dem[b,a+1];r=dem[b+1,a];t=dem[b+1,a+1]
gz=np.where(du+dv<=1,p+du*(q-p)+dv*(r-p),t+(1-du)*(r-t)+(1-dv)*(q-t))
water=np.memmap(world/'water.f32','<f4','r',shape=(W,W));m=(abs(rd['z']-gz)<185)&(rd['z']>0)&(rd['z']>water[y//4,x//4]+35)
road=np.zeros(S*S,'?');road[k[m]]=True
with gzip.open('source/resources/nav-bridges.bin','rb') as f:
 _,_,ng,_=struct.unpack('<4siii',f.read(16))
 cells=np.frombuffer(f.read(11*ng),[('k','<i4'),('c','B'),('t','B'),('h','<f4'),('r','?')])
 good=cells[cells['r']&(cells['c']<=2)&(cells['t']<4)]['k'].astype(int)
mask=np.zeros(W*W,'?');mask[good]=True
links=[]
for dx,dy in [(1,1),(-1,1),(2,1),(-2,1),(1,2),(-1,2),(2,0),(0,2),(3,1),(-3,1),(1,3),(-1,3),(3,2),(-3,2),(2,3),(-2,3)]:
 other=good+dx+dy*W;aa=good[mask[other]];bb=aa+dx+dy*W
 # Test the full chord with roughly 1.4 m of supported width, across the missing corner.
 start=np.c_[(aa%W)*4+1.5,(aa//W)*4+1.5];dv=np.array([dx,dy]);norm=np.linalg.norm(dv)
 normal=np.array([-dy,dx])/norm;valid=np.ones(len(aa),bool)
 for t in np.linspace(0,1,int(norm*8)+2):
  for side in (-.70,0,.70):
   pos=np.floor(start+4*t*dv+normal*side+.5).astype(int)
   valid &= road[pos[:,0]+pos[:,1]*S]
 links.extend(zip(aa[valid],bb[valid]))
with open('tools/bridge-underroads/links.bin','wb') as f:
 f.write(struct.pack('<i',len(links)));np.array(links,dtype='<i4').tofile(f)
print('lower road fine samples',m.sum(),'certified lower edges',len(links))
