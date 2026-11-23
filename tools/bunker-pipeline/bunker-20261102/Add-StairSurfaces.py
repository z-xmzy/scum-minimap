"""Supplement projected height grids with covered architectural surfaces they omit."""
import pathlib,importlib.util,gzip,struct,hashlib
HERE=pathlib.Path(__file__).resolve().parent
def module(name,file):
    spec=importlib.util.spec_from_file_location(name,HERE/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
build=module('build','Build-BunkerAtlas.py');probe=module('probe','Check-GeometryCoverage.py')
np=build.np;ROOT=build.ROOT

def run():
    folder=build.OUT;index=build.read_json(folder/'index.json');assert index['format'] in (2,3)
    grids=probe.load(folder);by_id={f['id']:f for group in grids.values() for f in group}
    records=index['layers'];by_bunker={}
    for r in records:r['stairs']=[];by_bunker.setdefault(r['bunker'],[]).append(r)
    for bi,b in enumerate(build.read_json(ROOT/'output/bunker-catalogue.json')['bunkers'],1):
        items=build.read_json(ROOT/'output/bunkers'/(b['id']+'.json'))['placements']
        for scene in b.get('entranceScenes',[]):items += [p for p in build.placements(build.read_json(ROOT/'output/scenes'/(scene+'.json'))) if build.structural(p['mesh']) and '/Buildings/Bunker/' not in p['mesh']]
        all_t=build.triangles(items);cross=np.cross(all_t[:,1]-all_t[:,0],all_t[:,2]-all_t[:,0]);norm=np.linalg.norm(cross,axis=1)
        down=all_t[(cross[:,2]<-.5*norm)&(norm>2)];up=all_t[(cross[:,2]>.5*norm)&(norm>2)]
        dmin=down[:,:,:2].min(axis=1);dmax=down[:,:,:2].max(axis=1);umin=up[:,:,:2].min(axis=1);umax=up[:,:,:2].max(axis=1)
        stair=all_t[(cross[:,2]>.97*norm)&(norm>1000)]
        points=stair.mean(axis=1);covered=np.zeros(len(points),dtype=bool)
        # Batch the cheap grid lookup. Precise geometry is needed for overlapping
        # floors as well as stair flights (for example, a room below a catwalk).
        for layer in grids[b['id']]:
            valid=(points[:,0]>=layer['x0'])&(points[:,0]<layer['x1'])&(points[:,1]>=layer['y0'])&(points[:,1]<layer['y1'])
            ids=np.flatnonzero(valid&~covered)
            col=((layer['x1']-points[ids,0])/layer['step']).astype(int);row=((layer['y1']-points[ids,1])/layer['step']).astype(int)
            standing=np.zeros(len(ids),dtype=bool);crouching=standing.copy()
            reach=(-1,0,1) if layer['step']<=30 else (0,)
            for dy in reach:
                for dx in reach:
                    cx=np.clip(col+dx,0,layer['w']-1);ry=np.clip(row+dy,0,layer['h']-1)
                    dz=points[ids,2]-layer['grid'][ry,cx]
                    standing|=(dz+92.15>=20)&(dz+92.15<=145)
                    crouching|=(dz+62.15>=20)&(dz+62.15<=145)
            covered[ids]|=standing&crouching
        stair=stair[~covered]
        candidates={}
        for tri in stair:
            p=tri.mean(axis=0)
            key=tuple(np.floor(p[:2]/1000).astype(int))
            if key not in candidates:
                lo=np.array(key)*1000;hi=lo+1000
                candidates[key]=(down[np.all(dmin<=hi,axis=1)&np.all(dmax>=lo,axis=1)],up[np.all(umin<=hi,axis=1)&np.all(umax>=lo,axis=1)])
            local_down,local_up=candidates[key]
            ceilings=probe.ray_heights(local_down,p[0],p[1]);above=ceilings[ceilings>p[2]+10]
            if not len(above) or not(185<=above[0]-p[2]<=3000):continue
            obstruction=probe.ray_heights(local_up,p[0],p[1])
            if np.any((obstruction>p[2]+5)&(obstruction<p[2]+185)):continue
            layer=min(by_bunker[b['id']],key=lambda r:abs(r['z']-p[2]))
            layer['stairs'].append(tri)
        print(b['id'],sum(len(r['stairs']) for r in by_bunker[b['id']]),'precise surface faces',flush=True)
    for r in records:
        r['grid']=by_id[r['id']]['grid'];r['stairs']=np.array(r['stairs'],dtype='<f4').reshape(-1,3,3)
        image=build.Image.open(folder/r['image']).convert('RGBA')
        if len(r['stairs']):
            old_hi=np.array(r['hi']);old_lo=np.array(r['lo']);step=r['step']
            extra_hi=r['stairs'][:,:,:2].max(axis=(0,1))+100;extra_lo=r['stairs'][:,:,:2].min(axis=(0,1))-100
            before=np.maximum(0,np.ceil((extra_hi-old_hi)/step).astype(int));after=np.maximum(0,np.ceil((old_lo-extra_lo)/step).astype(int))
            r['grid']=np.pad(r['grid'],((before[1],after[1]),(before[0],after[0])),constant_values=np.nan)
            r['hi']=(old_hi+before*step).tolist();r['lo']=(old_lo-after*step).tolist();r['height'],r['width']=r['grid'].shape
            expanded=build.Image.new('RGBA',(r['width'],r['height']),(14,22,27,255));expanded.paste(image,tuple(before));image=expanded
            draw=build.ImageDraw.Draw(image)
            for tri in r['stairs']:
                draw.polygon([tuple(p) for p in (np.array(r['hi'])-tri[:,:2])/step],fill=(117,91,53,255) if abs(tri[:,2].mean()-r['z'])>80 else (49,78,85,255))
            image.save(folder/r['image'])
        r['sha']=hashlib.sha256((folder/r['image']).read_bytes()).digest()
    with gzip.open(folder/'atlas-precise.bin','wb',compresslevel=6) as f:
        f.write(struct.pack('<4siii',b'SBMA',3,index['gameBuild'],len(records)))
        for r in records:
            for key in ['id','bunker','kind','image']:
                data=r[key].encode();f.write(struct.pack('<i',len(data)));f.write(data)
            f.write(struct.pack('<ii6dii',r['rank'],r['count'],*r['lo'],*r['hi'],r['z'],r['step'],r['width'],r['height']))
            f.write(r['sha']);build.write_grid(f,r['grid']);f.write(struct.pack('<i',len(r['stairs'])));f.write(r['stairs'].tobytes())
    (folder/'atlas-precise.bin').replace(folder/'atlas.bin')
    index['format']=3;index.pop('preciseStairFaces',None);index['preciseSurfaceFaces']=sum(len(r['stairs']) for r in records)
    index['layers']=[{k:v for k,v in r.items() if k not in ('stairs','grid','sha')} for r in records]
    build.write_json(folder/'index.json',index)
    print('Wrote format 3:',index['preciseSurfaceFaces'],'additional surface faces',flush=True)
if __name__=='__main__':run()
