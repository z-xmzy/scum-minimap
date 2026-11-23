"""Independent model-centroid probes against the packaged atlas (not a gameplay test)."""
import pathlib,sys,gzip,struct,collections,time
HERE=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent/'bunker-extraction-20261031'))
from bunker_geometry import ROOT,read_json,write_json,placements,structural
from floorplans import read_mesh,transform_vertices,ray_heights,transform_triangles
import numpy as np

def load(folder):
    layers=collections.defaultdict(list)
    with gzip.open(folder/'atlas.bin','rb') as f:
        magic,version,build,count=struct.unpack('<4siii',f.read(16));assert magic==b'SBMA' and version in (2,3)
        for _ in range(count):
            strings=[f.read(struct.unpack('<i',f.read(4))[0]).decode() for _ in range(4)]
            rank,total,x0,y0,x1,y1,z,step,w,h=struct.unpack('<ii6dii',f.read(64));f.read(32)
            grid=np.full((h,w),np.nan,dtype='<f4');columns=(w+7)//8
            for j in range(struct.unpack('<i',f.read(4))[0]):
                index=struct.unpack('<i',f.read(4))[0];tile=np.frombuffer(f.read(256),dtype='<f4').reshape(8,8)
                row=index//columns*8;col=index%columns*8;grid[row:row+8,col:col+8]=tile[:min(8,h-row),:min(8,w-col)]
            stairs=np.empty((0,3,3),dtype='<f4')
            if version>=3:stairs=np.frombuffer(f.read(struct.unpack('<i',f.read(4))[0]*36),dtype='<f4').reshape(-1,3,3)
            layers[strings[1]].append(dict(id=strings[0],rank=rank,x0=x0,y0=y0,x1=x1,y1=y1,step=step,w=w,h=h,grid=grid,stairs=stairs))
    return layers

def hit(layers,p,offset=92.15):
    x,y,z=p;hits=[]
    for l in layers:
        if not(l['x0']<=x<l['x1'] and l['y0']<=y<l['y1']):continue
        col=int((l['x1']-x)/l['step']);row=int((l['y1']-y)/l['step'])
        delta=z+offset-l['grid'][max(0,row-1):row+2,max(0,col-1):col+2]
        if np.any((delta>=20)&(delta<=145)):hits.append(l['rank']);continue
        if len(l['stairs']):
            delta=z+offset-ray_heights(l['stairs'],x,y)
            if np.any((delta>=20)&(delta<=145)):hits.append(l['rank'])
    return hits

def probe():
    folder=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else HERE/'app/assets/bunkers'
    name=sys.argv[2] if len(sys.argv)>2 else 'geometry-coverage'
    atlas=load(folder);reports=[];rng=np.random.default_rng(20261101)
    catalogue=read_json(ROOT/'output/bunker-catalogue.json')['bunkers']
    for index,b in enumerate(catalogue,1):
        items=read_json(ROOT/'output/bunkers'/(b['id']+'.json'))['placements']
        for scene in b.get('entranceScenes',[]):
            items += [p for p in placements(read_json(ROOT/'output/scenes'/(scene+'.json'))) if structural(p['mesh']) and '/Buildings/Bunker/' not in p['mesh']]
        chunks=[];stairs=[]
        for p in items:
            v,faces=read_mesh(p['mesh']);t=transform_triangles(v,faces,p['transform']);chunks.append(t)
            if 'stair' in p['mesh'].lower():stairs.append(t)
        t=np.concatenate(chunks);cross=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]);norm=np.linalg.norm(cross,axis=1)
        down=t[(cross[:,2]<-.5*norm)&(norm>2)];up=t[(cross[:,2]>.97*norm)&(norm>2000)]
        upward=t[(cross[:,2]>.5*norm)&(norm>2)];umin=upward[:,:,:2].min(axis=1);umax=upward[:,:,:2].max(axis=1)
        dmin=down[:,:,:2].min(axis=1);dmax=down[:,:,:2].max(axis=1)
        probes=[]
        if len(up):
            for tri in up[rng.choice(len(up),min(120,len(up)),replace=False)]:probes.append(('floor',tri.mean(axis=0)))
        if stairs:
            st=np.concatenate(stairs);normal=np.cross(st[:,1]-st[:,0],st[:,2]-st[:,0]);area=np.linalg.norm(normal,axis=1)
            st=st[(normal[:,2]>.97*area)&(area>1000)]
            # Centroids derive from original triangle data, independent of raster cells and layer bins.
            if len(st)>600:st=st[rng.choice(len(st),600,replace=False)]
            for tri in st:probes.append(('stair',tri.mean(axis=0)))
        eligible=collections.Counter();matched=collections.Counter();misses=[];posture=[];samples=[]
        for kind,p in probes:
            mask=np.all(dmin<=p[:2],axis=1)&np.all(dmax>=p[:2],axis=1)
            ceilings=ray_heights(down[mask],p[0],p[1]);above=ceilings[ceilings>p[2]+10]
            if not len(above) or not(185<=above[0]-p[2]<=3000):continue
            # Foundations and underside faces are not standing locations when another
            # solid upward face intersects the character's body at the same XY.
            umask=np.all(umin<=p[:2],axis=1)&np.all(umax>=p[:2],axis=1)
            obstruction=ray_heights(upward[umask],p[0],p[1])
            if np.any((obstruction>p[2]+5)&(obstruction<p[2]+185)):continue
            eligible[kind]+=1;standing=hit(atlas[b['id']],p);crouching=hit(atlas[b['id']],p,62.15)
            samples.append({'kind':kind,'surfaceCm':p.tolist()})
            if standing:matched[kind]+=1
            else:misses.append({'kind':kind,'surfaceCm':p.tolist(),'ceilingCm':float(above[0])})
            if standing!=crouching:posture.append({'surfaceCm':p.tolist(),'standing':standing,'crouching':crouching})
        reports.append({'bunker':b['id'],'eligible':dict(eligible),'matched':dict(matched),'misses':misses,'postureDifferences':posture,'samples':samples})
        if index%10==0 or index==len(catalogue): print(index,b['id'],'misses',sum(len(r['misses']) for r in reports),flush=True)
    write_json(HERE/'checks'/(name+'.json'),reports)
    print('Complete',len(reports),'instances; eligible',sum(sum(r['eligible'].values()) for r in reports),'misses',sum(len(r['misses']) for r in reports),'posture variants',sum(len(r['postureDifferences']) for r in reports),flush=True)
if __name__=='__main__':probe()
