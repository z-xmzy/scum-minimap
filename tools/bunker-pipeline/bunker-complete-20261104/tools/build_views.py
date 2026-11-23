"""Build presentation views from immutable detector geometry. No live-game access."""
import collections,functools,gzip,hashlib,json,pathlib,struct,sys
ROOT=pathlib.Path(__file__).resolve().parents[1];BASE=ROOT.parent
sys.path.insert(0,str(ROOT/'geometry-review'))
from analyze import load,aligned,np,Image,ImageDraw,ImageFilter
sys.path.insert(0,str(BASE/'bunker-extraction-20261031'))
from bunker_geometry import placements,structural
from floorplans import read_mesh,transform_vertices,transform_triangles
OUT=ROOT/'app/assets/bunkers';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=BASE/'marker-icons-20261103/app/assets/bunkers'
CAT=json.loads((BASE/'bunker-extraction-20261031/output/bunker-catalogue.json').read_text(encoding='utf-8-sig'))['bunkers']
CAT+=[dict(b,floorAnchors=[]) for b in json.loads((BASE/'bunker-20261102/extracted/output/bunker-catalogue.json').read_text(encoding='utf-8-sig'))['bunkers']]
BYCAT={b['id']:b for b in CAT}

def dilate(mask,cells=2):
    return np.array(Image.fromarray((mask*255).astype('uint8')).filter(ImageFilter.MaxFilter(cells*2+1)))>0

def raster_points(tri,hi,step,size):
    xy=(hi-tri[:,:2])/step;low=np.maximum(0,np.floor(xy.min(axis=0)-.5).astype(int));high=np.minimum(size-1,np.ceil(xy.max(axis=0)-.5).astype(int))
    if np.any(high<low):return None
    a,b,c=xy;v=b-a;w=c-a;det=v[0]*w[1]-v[1]*w[0]
    if abs(det)<1e-6:return None
    yy,xx=np.mgrid[low[1]:high[1]+1,low[0]:high[0]+1];dx=xx+.5-a[0];dy=yy+.5-a[1]
    s=(dx*w[1]-dy*w[0])/det;t=(v[0]*dy-v[1]*dx)/det
    inside=(s>=-1e-6)&(t>=-1e-6)&(s+t<=1+1e-6)
    zz=tri[0,2]+s*(tri[1,2]-tri[0,2])+t*(tri[2,2]-tri[0,2])
    return yy[inside],xx[inside],zz[inside]

def unwanted(name):
    name=name.lower().split('/')[-1]
    return ('ventilation' in name or 'circular_door' in name or
            ('tvbunker_elevator' in name and 'room' not in name) or
            any(t in name for t in ('generatorbattery','dialpad','capsule_brenner','bcu_terminal','weaponslocker','bunker_entrance_03_extension')))

@functools.lru_cache(maxsize=96)
def killboxes(bunker):
    """Room ownership comes from its actual entrance child actor, not its roof Z."""
    result=[]
    for scene in BYCAT[bunker]['scenes']:
        path=BASE/'bunker-extraction-20261031/output/scenes'/(scene+'.json')
        if not path.exists():continue
        for a in json.loads(path.read_text(encoding='utf-8-sig'))['actors']:
            if a['class'].split('/')[-1].split('.')[0]!='BP_Room_N_KillBoxRoom':continue
            door=next(c for c in a['components'] if c['name']=='Entrance' and 'BP_KillboxBigDoors_01.' in c.get('childActorClass',''))
            result.append(dict(actor=a['name'],scene=scene,entrance=door['world']['location']))
    return result

@functools.lru_cache(maxsize=96)
def exterior_rooms(bunker):
    result=[]
    for name in BYCAT[bunker].get('entranceScenes',[]):
        data=json.loads((BASE/'bunker-extraction-20261031/output/scenes'/(name+'.json')).read_text(encoding='utf-8-sig'))
        for actor in data['actors']:
            if actor['class'].split('/')[-1].split('.')[0] not in ('BP_Bunker_Entrance_01','BP_Bunker_Entrance_01_A'):continue
            door=next((c for c in actor['components'] if c['name']=='BP_Tunnel_Big_Doors_021'),None)
            if door:result.append(dict(scene=name,actor=actor['name'],entrance=door['world']['location']))
    return result

def face_key(tri):
    return tuple(sorted(tuple(row) for row in np.rint(np.asarray(tri,dtype='f4')*10).astype('int64')))

def clean(bunker,gs,hi,step,excluded=None):
    """Remove only explicitly identified device/door upper faces from display data."""
    if BYCAT[bunker]['type'] not in ('abandoned','ordinary','secret','research'):return 0
    data=json.loads((BASE/'bunker-extraction-20261031/output/bunkers'/f'{bunker}.json').read_text(encoding='utf-8-sig'))
    items=data['placements'];removed=0;size=np.array([gs[0].shape[1],gs[0].shape[0]]);rooms={(r['scene'],r['actor']):r for r in killboxes(bunker)}
    exterior={(r['scene'],r['actor']):r for r in exterior_rooms(bunker)}
    for name in BYCAT[bunker].get('entranceScenes',[]):
        scene=json.loads((BASE/'bunker-extraction-20261031/output/scenes'/(name+'.json')).read_text(encoding='utf-8-sig'))
        items.extend(dict(p,scene=name) for p in placements(scene) if structural(p['mesh']) and '/Buildings/Bunker/' not in p['mesh'])
    for p in items:
        room=rooms.get((p['scene'],p['actor'])) if 'SM_Room_N_KillBoxRoom_01.' in p['mesh'] else None
        entry=exterior.get((p['scene'],p['actor']))
        if not unwanted(p['mesh']) and room is None and entry is None:continue
        vertices,indices=read_mesh(p['mesh']);triangles=transform_triangles(vertices,indices,p['transform'])
        cross=np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0]);norm=np.linalg.norm(cross,axis=1)
        for tri in triangles[(cross[:,2]>.5*norm)&(norm>2)]:
            z=float(tri[:,2].mean())
            if excluded is not None and (room is None and entry is None or room is not None and (z<room['entrance'][2]-140 or z>room['entrance'][2]+100) or entry is not None and z<entry['entrance'][2]-300):excluded.add(face_key(tri))
            hit=raster_points(tri,hi,step,size)
            if hit is None:continue
            yy,xx,zz=hit
            for g in gs:
                match=np.abs(g[yy,xx]-zz)<2
                # The upper spawn tubes and sub-floor shell are not additional rooms.
                # Keep the walkable floor band connected to the room's entrance.
                if room is not None:match&=(zz<room['entrance'][2]-140)|(zz>room['entrance'][2]+100)
                if entry is not None:match&=zz<entry['entrance'][2]-300 # Closed foundation underneath the only entrance doorway.
                removed+=int(match.sum());g[yy[match],xx[match]]=np.nan
    return removed

def room_ownership(bunker,gs,hi,step,assignment,abandoned):
    result=[];rooms=killboxes(bunker)
    if not rooms:return result
    data=json.loads((BASE/'bunker-extraction-20261031/output/bunkers'/f'{bunker}.json').read_text(encoding='utf-8-sig'))
    size=np.array([gs[0].shape[1],gs[0].shape[0]])
    for room in rooms:
        x,y,z=room['entrance'];candidates=[]
        for i,g in enumerate(gs):
            yy,xx=np.where(np.isfinite(g));wx=hi[0]-(xx+.5)*step;wy=hi[1]-(yy+.5)*step
            near=(wx-x)**2+(wy-y)**2<300**2
            if near.any():candidates.append((float(np.min((wx[near]-x)**2+(wy[near]-y)**2+9*(g[yy[near],xx[near]]-z)**2)),i))
        assert candidates,(bunker,room)
        _,source=min(candidates)
        page=int(abandoned_classify(np.array([x]),np.array([y]),np.array([z]),BYCAT[bunker]['floorAnchors'])[0]) if abandoned else assignment[source]
        masks=[np.zeros(g.shape,bool) for g in gs]
        for p in data['placements']:
            if p['scene']!=room['scene'] or p['actor']!=room['actor'] or 'SM_Room_N_KillBoxRoom_01.' not in p['mesh']:continue
            vertices,indices=read_mesh(p['mesh']);tris=transform_triangles(vertices,indices,p['transform'])
            cross=np.cross(tris[:,1]-tris[:,0],tris[:,2]-tris[:,0]);norm=np.linalg.norm(cross,axis=1)
            for tri in tris[(cross[:,2]>.5*norm)&(norm>2)]:
                hit=raster_points(tri,hi,step,size)
                if hit is None:continue
                yy,xx,zz=hit
                for mask,g in zip(masks,gs):mask[yy,xx]|=np.abs(g[yy,xx]-zz)<2
        result.append(dict(room=room,page=page,masks=masks,source=source))
    return result

def abandoned_classify(x,y,z,anchors):
    best=np.full(np.shape(z),np.inf);which=np.zeros(np.shape(z),dtype=np.int32)
    # Component centres are semantic seeds, not verified collision boxes.
    for a in anchors:
        ax,ay,az=a['transform']['location']
        score=(x-ax)**2+(y-ay)**2+9*(z+180-az)**2
        pick=score<best;best[pick]=score[pick];which[pick]=0 if a['floor']==-1 else 1
    return which

def coherent_planes(grid,chosen):
    """Do not split a connected flat floor merely because zone seeds compete.
    Stairs remain separate: a neighbouring tread must be within 6 cm of the
    component's starting elevation, not an accumulated ramp tolerance.
    """
    h,w=grid.shape;flat=grid.ravel();result=chosen.ravel().copy();seen=~np.isfinite(flat);changes=0;components=0
    for start in np.flatnonzero(~seen):
        if seen[start]:continue
        z=float(flat[start]);seen[start]=True;todo=[int(start)];cells=[]
        while todo:
            index=todo.pop();cells.append(index);row,col=divmod(index,w)
            for other in ((index-w if row else -1),(index+w if row+1<h else -1),(index-1 if col else -1),(index+1 if col+1<w else -1)):
                if other>=0 and not seen[other] and abs(float(flat[other])-z)<=6:
                    seen[other]=True;todo.append(other)
        values=result[cells];valid=values[values>=0]
        if not len(valid):continue
        page=int(np.bincount(valid).argmax());changes+=int((values!=page).sum());result[cells]=page;components+=1
    return result.reshape(grid.shape),changes,components

def conflict(a,b,step):
    share=np.isfinite(a)&np.isfinite(b)&(np.abs(a-b)>185)
    area=share.sum()*step**2/10000;small=min(np.isfinite(a).sum(),np.isfinite(b).sum())*step**2/10000
    return area>max(2 if small<80 else 8,small*.04)

def grouping(rs,gs,step):
    areas=np.array([np.isfinite(g).sum()*step**2/10000 for g in gs]);largest=float(max(areas))
    major=[i for i in np.argsort(-areas) if areas[i]>=max(8 if largest<400 else 80,largest*.60)]
    if not major:major=[int(np.argmax(areas))]
    pages=[];assign={}
    for i in major:
        found=next((p for p,members in enumerate(pages) if all(not conflict(gs[i],gs[j],step) for j in members)),None)
        if found is None:found=len(pages);pages.append([])
        pages[found].append(i);assign[i]=found
    for i in np.argsort(-areas):
        i=int(i)
        if i in assign:continue
        candidates=[p for p,members in enumerate(pages) if all(not conflict(gs[i],gs[j],step) for j in members)]
        if not candidates:
            p=len(pages);pages.append([])
        else:
            def contact(p):
                hits=0
                for j in pages[p]:
                    for dy,dx in ((0,1),(0,-1),(1,0),(-1,0)):
                        shifted=np.roll(gs[j],(dy,dx),(0,1));valid=np.isfinite(gs[i])&np.isfinite(shifted)&(np.abs(gs[i]-shifted)<=60)
                        if dy==1:valid[0]=False
                        elif dy==-1:valid[-1]=False
                        elif dx==1:valid[:,0]=False
                        else:valid[:,-1]=False
                        hits+=int(valid.sum())
                return hits
            # A landing belongs with the corridor it joins, not the closest median Z.
            p=min(candidates,key=lambda p:(-contact(p),min(abs(rs[i]['z']-rs[j]['z']) for j in pages[p])))
        pages[p].append(i);assign[i]=p
    order=sorted(range(len(pages)),key=lambda p:-np.median([rs[j]['z'] for j in pages[p] if j in major] or [rs[j]['z'] for j in pages[p]]))
    remap={p:q for q,p in enumerate(order)}
    return {i:remap[p] for i,p in assign.items()},len(pages)

def lost_rectangles(g,chosen,hi,step):
    """Preserve stacked source heights as coalesced cell surfaces for view selection."""
    lost=np.isfinite(g)&np.isfinite(chosen)&(np.abs(g-chosen)>2)
    faces=[]
    for y in np.flatnonzero(lost.any(axis=1)):
        xs=np.flatnonzero(lost[y]);k=0
        while k<len(xs):
            start=int(xs[k]);end=start+1;z=float(g[y,start]);k+=1
            while k<len(xs) and xs[k]==end and abs(float(g[y,end])-z)<.5:end+=1;k+=1
            x1,y1=hi-np.array([start,y])*step;x0,y0=hi-np.array([end,y+1])*step
            faces.extend([[[x0,y0,z],[x1,y0,z],[x1,y1,z]],[[x0,y0,z],[x1,y1,z],[x0,y1,z]]])
    return faces

def write_grid(f,g):
    h,w=g.shape;tiles=[];columns=(w+7)//8
    for row in range(0,h,8):
        for col in range(0,w,8):
            part=g[row:row+8,col:col+8]
            if not np.isfinite(part).any():continue
            tile=np.full((8,8),np.nan,dtype='<f4');tile[:part.shape[0],:part.shape[1]]=part;tiles.append(((row//8)*columns+col//8,tile))
    f.write(struct.pack('<i',len(tiles)))
    for index,tile in tiles:f.write(struct.pack('<i',index));f.write(tile.tobytes())

def build(one=False):
    raw=load();groups=collections.defaultdict(list)
    for r in raw:groups[r['bunker']].append(r)
    views=[];mapping=[];labels=[];report=[]
    annotations=json.loads((ROOT/'research/abandoned-annotation-evidence.json').read_text(encoding='utf-8-sig'))['bunkers']
    other=json.loads((ROOT/'research/other-annotation-evidence.json').read_text(encoding='utf-8-sig'))['annotations']
    for bunker,rs in groups.items():
        if one and bunker!='D_1_Abandoned':continue
        rs.sort(key=lambda r:r['rank']);hi,step,gs=aligned(rs);gs=[g.copy() for g in gs]
        excluded=set();before=[np.isfinite(g) for g in gs];removed=clean(bunker,gs,hi,step,excluded);removed_masks=[old&~np.isfinite(g) for old,g in zip(before,gs)];b=BYCAT[bunker];abandoned=b['type']=='abandoned'
        if abandoned:assignment={};count=2
        else:assignment,count=grouping(rs,gs,step)
        owners=room_ownership(bunker,gs,hi,step,assignment,abandoned)
        shape=gs[0].shape;parts=[[] for _ in range(count)];tri_parts=[[] for _ in range(count)];walls=[np.zeros(shape,dtype=bool) for _ in range(count)];rawvotes={}
        yy,xx=np.indices(shape);wx=hi[0]-(xx+.5)*step;wy=hi[1]-(yy+.5)*step;coherence_changes=0;component_count=0
        for i,(r,g) in enumerate(zip(rs,gs)):
            mask=np.isfinite(g);chosen=np.full(shape,-1,dtype=np.int16)
            if abandoned:
                chosen[mask]=abandoned_classify(wx[mask],wy[mask],g[mask],b['floorAnchors'])
                chosen,changed,components=coherent_planes(g,chosen);coherence_changes+=changed;component_count+=components
            else:chosen[mask]=assignment[i]
            for owner in owners:chosen[owner['masks'][i]]=owner['page']
            votes=collections.Counter(int(v) for v in chosen[mask]);rawvotes[r['id']]=votes
            image=np.array(Image.open(SOURCE/r['image']).convert('RGB'));wall=np.all(image==[183,215,217],axis=2)
            offset=np.rint((hi-[r['x1'],r['y1']])/step).astype(int);ox,oy=offset
            fullwall=np.zeros(shape,dtype=bool);fullwall[oy:oy+r['h'],ox:ox+r['w']]=wall
            for p in range(count):
                pmask=chosen==p
                part=np.where(pmask,g,np.nan).astype('f4');parts[p].append(part)
                walls[p]|=fullwall&dilate(pmask,2)
            for tri in r['stairs']:
                if face_key(tri) in excluded:continue
                c=tri.mean(axis=0)
                if abandoned:
                    p=int(abandoned_classify(np.array([c[0]]),np.array([c[1]]),np.array([c[2]]),b['floorAnchors'])[0])
                    col,row=np.floor((hi-c[:2])/step).astype(int)
                    if 0<=row<shape[0] and 0<=col<shape[1] and chosen[row,col]>=0 and abs(float(g[row,col])-float(c[2]))<=6:p=int(chosen[row,col])
                else:p=assignment[i]
                # Skip device faces identified by removed grid cells at the triangle centroid.
                col,row=np.floor((hi-c[:2])/step).astype(int)
                if 0<=row<shape[0] and 0<=col<shape[1]:
                    if removed_masks[i][row,col]:continue
                    for owner in owners:
                        if owner['masks'][i][row,col]:p=owner['page']
                tri_parts[p].append(tri)
        local=[]
        for p in range(count):
            # Largest component gets visual precedence; all hidden surfaces are retained below.
            ordering=sorted(range(len(rs)),key=lambda i:np.isfinite(parts[p][i]).sum())
            g=np.full(shape,np.nan,dtype='f4')
            for i in ordering:
                part=parts[p][i];mask=np.isfinite(part);g[mask]=part[mask]
            faces=list(tri_parts[p])
            for part in parts[p]:faces.extend(lost_rectangles(part,g,hi,step))
            faces=np.array(faces,dtype='<f4').reshape(-1,3,3)
            assert len(faces)<=20000,(bunker,p,len(faces))
            valid=np.isfinite(g)
            for tri in tri_parts[p]:
                hit=raster_points(tri,hi,step,np.array([shape[1],shape[0]]))
                if hit is not None:
                    ry,cx,zz=hit;vacant=~np.isfinite(g[ry,cx]);g[ry[vacant],cx[vacant]]=zz[vacant]
            valid=np.isfinite(g);assert valid.any(),(bunker,p)
            ys,xs=np.where(valid);pad=max(2,int(np.ceil(100/step)));x0=max(0,int(xs.min())-pad);y0=max(0,int(ys.min())-pad);x1=min(shape[1],int(xs.max())+pad+1);y1=min(shape[0],int(ys.max())+pad+1)
            # Precise faces must remain inside record bounds for Score's early bounds check.
            if len(faces):
                pixels=(hi-faces[:,:,:2])/step
                x0=max(0,min(x0,int(np.floor(pixels[:,:,0].min()))-1));y0=max(0,min(y0,int(np.floor(pixels[:,:,1].min()))-1))
                x1=min(shape[1],max(x1,int(np.ceil(pixels[:,:,0].max()))+1));y1=min(shape[0],max(y1,int(np.ceil(pixels[:,:,1].max()))+1))
            base=np.zeros((*shape,4),dtype='uint8');base[:]=[14,22,27,255]
            base[valid]=[49,78,85,255]
            for i in range(len(rs)):
                ramps=np.isfinite(parts[p][i])&(np.abs(parts[p][i]-rs[i]['z'])>80);base[ramps&valid]=[105,84,52,255]
            # Only nearby wall outlines survive; far-through-air cuts are excluded.
            base[walls[p]&dilate(valid,2)]=[183,215,217,255]
            image=Image.fromarray(base[y0:y1,x0:x1]);ident=hashlib.sha256((bunker+':view:'+str(p)).encode()).hexdigest()[:20]
            filename='view-'+ident+'.png';image.save(OUT/filename)
            v=dict(id=ident,bunker=bunker,kind=b['type'],image=filename,rank=p+1,count=count,lo=(hi-np.array([x1,y1])*step).tolist(),hi=(hi-np.array([x0,y0])*step).tolist(),z=float(np.nanmedian(g)),step=step,w=x1-x0,h=y1-y0,grid=g[y0:y1,x0:x1].copy(),stairs=faces,sha=hashlib.sha256((OUT/filename).read_bytes()).digest())
            v['name']=('地下'+str(p+1)+'层') if abandoned else ('区域总览' if count==1 else ('上部区域' if p==0 else '下部区域' if p==count-1 else '中部区域 '+str(p+1)))
            views.append(v);local.append(v)
        for r in rs:
            votes=rawvotes[r['id']];p=max(votes,key=votes.get) if votes else min(range(count),key=lambda p:abs(r['z']-local[p]['z']))
            mapping.append((r['id'],local[p]['id']))
        if abandoned:
            evidence=next(a for a in annotations if a['scene']=='TV_Base_'+bunker)
            for a in evidence['annotations']:
                p=a['floor']-1;x,y,z=a['anchorWorld'];v=local[p]
                if v['lo'][0]<=x<v['hi'][0] and v['lo'][1]<=y<v['hi'][1]:labels.append((v['id'],x,y,a['label']))
        else:
            scenes=set(b.get('scenes',[]))|set(b.get('entranceScenes',[]))
            for a in other:
                if a['scene'] not in scenes:continue
                x,y,z=a['anchorWorld'];candidates=[]
                for v in local:
                    col=int((v['hi'][0]-x)/step);row=int((v['hi'][1]-y)/step)
                    if 0<=row<v['h'] and 0<=col<v['w']:
                        patch=v['grid'][max(0,row-5):row+6,max(0,col-5):col+6]
                        if np.isfinite(patch).any():candidates.append((float(np.nanmin(abs(patch-z))),v))
                if candidates:
                    _,v=min(candidates,key=lambda t:t[0]);labels.append((v['id'],x,y,a['label']))
        report.append(dict(bunker=bunker,rawSlices=len(rs),views=count,removedDeviceCells=removed,coherentPlaneCellsReassigned=coherence_changes,flatComponents=component_count,viewNames=[v['name'] for v in local],surfaceFaces=[len(v['stairs']) for v in local],roomEntranceOwnership=[dict(actor=o['room']['actor'],scene=o['room']['scene'],entrance=o['room']['entrance'],view=local[o['page']]['id'],rank=o['page']+1,ownedCells=sum(int(m.sum()) for m in o['masks'])) for o in owners]))
        print(bunker,len(rs),'->',count,'views; removed',removed,flush=True)
    with gzip.open(OUT/'views.bin','wb',compresslevel=6) as f:
        f.write(struct.pack('<4siii',b'SBMA',3,26320533,len(views)))
        for v in views:
            for key in ('id','bunker','kind','image'):
                data=v[key].encode();f.write(struct.pack('<i',len(data)));f.write(data)
            f.write(struct.pack('<ii6dii',v['rank'],v['count'],*v['lo'],*v['hi'],v['z'],v['step'],v['w'],v['h']));f.write(v['sha']);write_grid(f,v['grid']);f.write(struct.pack('<i',len(v['stairs'])));f.write(v['stairs'].tobytes())
    for name,rows in [('views',[(v['id'],v['name']) for v in views]),('mapping',mapping),('labels',list(dict.fromkeys(labels)))]:
        (OUT/(name+'.tsv')).write_text('\n'.join('\t'.join(str(c) for c in row) for row in rows)+'\n',encoding='utf-8')
    (ROOT/'checks/view-build-report.json').write_text(json.dumps(dict(bunkers=len(report),views=len(views),labels=len(labels),records=report,limitations=['Static geometry and semantic seed assignment; not live verified.','Unverified zone extents are not used as collision volumes.']),ensure_ascii=False,indent=2),encoding='utf-8')
    print('WROTE',len(views),'views',len(labels),'labels',flush=True)
if __name__=='__main__':build('--d1' in sys.argv)

