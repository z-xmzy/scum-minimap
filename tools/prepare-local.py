from pathlib import Path
import sys,struct,json,itertools,gzip
import numpy as np
from bake import sample,raster,markings
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'tools/lib'))
from scipy.spatial import ConvexHull
b=root/'bake';out=b/'audit';W=3804;S=16
for p in out.glob('*.raw'):
    with p.open('rb') as f,p.with_suffix('.geom').open('wb') as w:
        while head:=f.read(8):
            kind,n=struct.unpack('<ii',head);v=np.frombuffer(f.read(n*12),'<f4').reshape(n,3)
            if kind==1:
                v=np.unique(v,axis=0)
                if len(v)<4:raise ValueError(p)
                axes,sv,basis=np.linalg.svd(v.astype(float)-v.mean(axis=0),full_matrices=False)
                if sv[-1]<1e-7:
                    poly=v[ConvexHull(v@basis[:2].T).vertices];kind=2
                    v=np.array([[poly[0],poly[i],poly[i+1]] for i in range(1,len(poly)-1)],dtype='<f4').reshape(-1,3)
                else:
                    h=ConvexHull(v);v=v[h.simplices].reshape(-1,3)
            w.write(struct.pack('<ii',kind,len(v)));w.write(v.tobytes())
dem=np.memmap(b/'height/tmp/work.f32','<f4','r',shape=(10161,10161))
ws=raster('water.f32');deck=raster('deck-used.f32');road=raster('roads.f32');marks,_=markings()
def ground(u,v):
    return sample(dem,u*15216.18,v*15236.18)
def field(u,v):
    i=(u*W).astype(int);j=(v*W).astype(int)
    z=ground(u,v)
    gx=(ground(u+3/15216.18,v)-ground(u-3/15216.18,v))/600
    gy=(ground(u,v+3/15236.18)-ground(u,v-3/15236.18))/600
    angle=np.degrees(np.arctan(np.hypot(gx,gy)));water=(z<=0)|(ws[j,i]>z+15)
    d=deck[j,i];r=road[j,i];known=(marks[j,i]!=0)|(np.isfinite(r)&(r>=z-120)&(r<=z+300)&(~water|((r>ws[j,i]+35)&(r>35))))|(np.isfinite(d)&(d>ws[j,i]+35))
    return np.maximum(z,d),angle,np.where(known,0,np.where(water,2,np.where(angle>=40,3,0))).astype('uint8')
regs=json.loads((out/'regions.json').read_text());manifest=[]
for r in regs:
    S=r['s'];n=r['n']*S;zmin=None;zmax=None;block=np.zeros((n+2,n+2),np.uint8)
    x=(r['x']+(np.arange(-1,n+1)+.5)/S)/W;y=(r['y']+(np.arange(-1,n+1)[:,None]+.5)/S)/W
    for a,bias in [(0,0),(-.5,-.5),(.5,-.5),(-.5,.5),(.5,.5)]:
        z,ang,flags=field(x+a/(W*S),y+bias/(W*S));zmin=z if zmin is None else np.minimum(z,zmin);zmax=z if zmax is None else np.maximum(z,zmax)
        block=np.maximum(block,flags)
    p=out/r['key'];block.tofile(p.with_suffix('.base'));zmin.astype('<f4').tofile(p.with_suffix('.low'));zmax.astype('<f4').tofile(p.with_suffix('.high'))
    z,angle,flags=field(np.array(r['u']),np.array(r['v']))
    manifest.append('\t'.join(map(str,[r['key'],r['x'],r['y'],n,S,r['u'],r['v'],float(z),float(angle),int(flags)])))
(out/'regions.tsv').write_text('\n'.join(manifest))
