"""Room fills come from room-shell triangles, never a rectangle around a POI.
Safe text rectangles are entirely inside the raster room and avoid its walls.
Sector colours follow semantic seeds along existing outlines; their zone-box
extents are not exported, so inferred sector attribution is recorded separately.
"""
import collections,functools,gzip,hashlib,json,pathlib,re,struct,sys
import build_views as bv
from build_views import np,Image,ImageFilter,read_mesh,transform_vertices,transform_triangles,raster_points
import analyze
ROOT=bv.ROOT;OUT=bv.OUT;SCENES=bv.BASE/'bunker-extraction-20261031/output/scenes'
PALETTE={'卫生间':'EE923D','检查室':'AB76D4','实验室':'A6B0BA','医疗室':'CBEAF6',
 '仓库':'F4DD4A','储藏室':'F4DD4A','控制室':'EAB54A','杀戮房':'E85D62','军械室':'40B77E',
 '办公室':'4BBCE8','配电室':'C8E64C','发电机房':'C8E64C','餐厅':'C8AC75','厨房':'48D8CE',
 '物资室':'B9BBCE','休息室':'9F8EDB','宿舍':'83B66B','安检室':'D7C66C',
 '感染区':'D96B83','楼梯':'A5CF6B','防化室':'C995DE','药房':'86D3BC','维修间':'DDA475'}
SECTORS=['E96C72','6384EC','8C9CA7','CB78DC','DEBC3F','69C66D','A87BC4','4DCBD0']

@functools.lru_cache(maxsize=8)
def scene(name):return json.loads((SCENES/(name+'.json')).read_text(encoding='utf-8-sig'))

def room_name(a):
    cls=a['class'].rsplit('/',1)[-1].split('.')[0]
    if a['kind']!='blueprint' or re.search('Lights|Lamp|Door|Spawn',cls):return None
    if not cls.startswith(('BP_Room','BP_SM_Room','BP_Armory','BP_Depository','BP_Generator','BP_Infested','BP_Lobby','BP_Security')):return None
    for token,label in [('KillBoxRoom','杀戮房'),('Infested_Area','感染区'),('Generator_Room','配电室'),('Room_E_02','发电机房'),('ControlRoom','控制室'),('Armory','军械室'),('Depository','储藏室'),('Office','办公室'),('Pharmacy','药房'),('Sleeping','宿舍'),('Bathroom','卫生间'),('Restaurant','餐厅'),('Kitchen','厨房'),('LeisureRoom','休息室'),('Supply','物资室'),('WorkShop','维修间'),('SecurityCheck','安检室'),('Medical','医疗室'),('Warehouse','仓库'),('WareHouse','仓库'),('G_Lab','实验室'),('Hazmat','防化室'),('Lobby_Lab','实验室')]:
        if token in cls:return label
    meshes=' '.join(c.get('mesh','') for c in a['components'])
    for label,tokens in [('卫生间',['Toilet_01','Toilet_Stall']),('卫生间',['Shower_Wall','Mirror_Toilet']),('医疗室',['Hospital','EKG']),('实验室',['Lab_Desk','Lab_Shelf']),('办公室',['desk','Monitor']),('办公室',['Office_Table','FileCabinet']),('军械室',['GunRack','Gun_Rack']),('宿舍',['SM_Bed_','Locker']),('储藏室',['Cardboard_Box','Cardboard_Boxes'])]:
        if all(t.lower() in meshes.lower() for t in tokens):return label
    return None

def shell(c):
    name=c.get('mesh','').rsplit('/',1)[-1].lower()
    return c.get('world') and any(t in name for t in ('sm_room_','sm_roomn_','sm_armory','sm_sleepingroom','sm_tvbunker_generatorroom','sm_infested_area_room','sm_lobby_lab','sm_depository')) and not any(t in name for t in ('glass','door','laser','device','prop','desk','debris','_wall'))

sys.path.insert(0,str(bv.BASE/'bunker-20261102'))
import supplement_geometry as SG
SUPPLEMENT_MESH={}
for _f in (bv.BASE/'bunker-20261102/extracted').glob('*-mesh-manifest.json'):
    for _m in json.loads(_f.read_text(encoding='utf-8-sig')):SUPPLEMENT_MESH.setdefault((_m['actor'],_m['component']),_m['id'])
def room_mesh(a,c):
    try:return read_mesh(c['mesh'])
    except (KeyError,FileNotFoundError):
        ident=SUPPLEMENT_MESH.get((a['name'],c['name']))
        if ident is None:raise
        return SG.read_mesh(ident)

def room_mask(a,v):
    mask=np.zeros((v['h'],v['w']),bool);hi=np.array([v['x1'],v['y1']]);size=np.array([v['w'],v['h']])
    for c in a['components']:
        if not shell(c):continue
        try:vertices,indices=room_mesh(a,c)
        except (KeyError,FileNotFoundError):continue
        tris=transform_triangles(vertices,indices,c['world'])
        cross=np.cross(tris[:,1]-tris[:,0],tris[:,2]-tris[:,0]);length=np.linalg.norm(cross,axis=1)
        for tri in tris[(cross[:,2]>.7*length)&(length>2)]:
            hit=raster_points(tri,hi,v['step'],size)
            if hit is None:continue
            yy,xx,z=hit;match=np.isfinite(v['grid'][yy,xx])&(abs(v['grid'][yy,xx]-z)<45)
            mask[yy[match],xx[match]]=True
    return mask

def safe_rect(mask,aspect=3):
    """Largest readable axis-aligned text rectangle wholly inside a binary mask."""
    ys,xs=np.where(mask)
    if not len(xs):return None
    xbase,ybase=int(xs.min()),int(ys.min());part=mask[ybase:int(ys.max())+1,xbase:int(xs.max())+1]
    heights=np.zeros(part.shape[1],int);best=0;result=None
    for y,row in enumerate(part):
        heights=np.where(row,heights+1,0);stack=[]
        for x in range(len(heights)+1):
            h=int(heights[x]) if x<len(heights) else 0;start=x
            while stack and stack[-1][1]>h:
                left,tall=stack.pop();start=left;width=x-left;score=min(width/aspect,tall)
                if score>best:best=score;result=(left+xbase,y-tall+1+ybase,x+xbase,y+1+ybase)
            if h and (not stack or stack[-1][1]<h):stack.append((start,h))
    if result:
        x0,y0,x1,y1=result
        assert mask[y0:y1,x0:x1].all()
        if x1-x0<3 or y1-y0<3:return None
    return result

def label_row(v,mask,text,colour,kind,source):
    # One-cell safety margin includes the glyph background, rather than just its anchor.
    safe=np.array(Image.fromarray((mask*255).astype('uint8')).filter(ImageFilter.MinFilter(3)))>0
    rect=safe_rect(safe,max(2,len(text)*.7))
    if rect is None:return None
    x0,y0,x1,y1=rect;step=v['step']
    loX=v['x1']-x1*step;loY=v['y1']-y1*step;hiX=v['x1']-x0*step;hiY=v['y1']-y0*step
    return [v['id'],(loX+hiX)/2,(loY+hiY)/2,text,loX,loY,hiX,hiY,colour,kind],dict(view=v['id'],text=text,kind=kind,source=source,rectPixels=rect,insideMask=True)

def main():
    analyze.FOLDER=OUT;views=analyze.load('views.bin');all_labels=[];audit=[];counts=[]
    evidence=json.loads((ROOT/'research/abandoned-annotation-evidence.json').read_text(encoding='utf-8-sig'))['bunkers']
    ownership={(r['bunker'],o['scene'],o['actor']):o['view'] for r in json.loads((ROOT/'checks/view-build-report.json').read_text(encoding='utf-8'))['records'] for o in r.get('roomEntranceOwnership',[])}
    old_labels=collections.defaultdict(list)
    for line in (OUT/'labels.tsv').read_text(encoding='utf-8').splitlines():
        ident,x,y,text=line.split('\t');old_labels[ident].append((float(x),float(y),text))
    for v in views:
        b=bv.BYCAT[v['bunker']];valid=np.isfinite(v['grid']);image=np.array(Image.open(OUT/v['image']).convert('RGBA'))
        # Preserve exact existing wall cuts; white floor is independent from room colours.
        cache=ROOT/'checks/room-walls';cache.mkdir(exist_ok=True)
        cache_file=cache/(v['id']+'.npy')
        fresh='--fresh-walls' in sys.argv
        walls=np.load(cache_file) if cache_file.exists() and not fresh else np.all(image[:,:,:3]==[183,215,217],axis=2)|np.all(image[:,:,:3]==[48,59,67],axis=2)
        if fresh or not cache_file.exists():np.save(cache_file,walls)
        image[:]=[14,22,27,255];image[valid]=[240,242,240,255]
        taken=np.zeros(valid.shape,bool);room_entries=[];used=set();room_count=0;shell_less=[]
        names=set(b.get('scenes',[]))|set(b.get('entranceScenes',[]))
        if b['type']=='abandoned':names.add('TV_Base_'+v['bunker'])
        for name in sorted(names):
            if not (SCENES/(name+'.json')).exists():continue
            for actor in scene(name)['actors']:
                text=room_name(actor)
                if not text:continue
                owner=ownership.get((v['bunker'],name,actor['name']))
                if owner is not None and owner!=v['id']:continue
                if not any(shell(c) for c in actor['components']):
                    # No room shell exported: name the room at its blueprint origin on the
                    # floor just below it, without a fill or a guessed outline.
                    if text in used:continue
                    root=next((c for c in actor['components'] if c.get('world') and not c.get('parent')),None)
                    if root is None:continue
                    x,y,z=root['world']['location'];col=int((v['x1']-x)/v['step']);row=int((v['y1']-y)/v['step']);radius=max(2,int(350/v['step']))
                    if not(0<=col<v['w'] and 0<=row<v['h']):continue
                    near=np.zeros(valid.shape,bool);near[max(0,row-radius):row+radius+1,max(0,col-radius):col+radius+1]=True
                    g=np.nan_to_num(v['grid'],nan=-1e9);near&=valid&(g>z-320)&(g<z+60)
                    if near.sum()*v['step']**2<40000:continue
                    shell_less.append((near,text,name+'/'+actor['name']));used.add(text);continue
                mask=room_mask(actor,v)&valid
                if mask.sum()*v['step']**2<10000:continue
                colour=PALETTE[text];image[mask]=[*bytes.fromhex(colour),255];taken|=mask;used.add(text);room_count+=1
                room_entries.append((mask,text,colour,name+'/'+actor['name']))
        image[walls]=[48,59,67,255]
        # Sector names and colours are from the game resource names. Without box
        # extents, tint existing contours only; do not invent a new wall or doorway.
        if b['type']=='abandoned':
            e=next(e for e in evidence if e['scene']=='TV_Base_'+v['bunker'])
            yy,xx=np.indices(valid.shape);wx=v['x1']-(xx+.5)*v['step'];wy=v['y1']-(yy+.5)*v['step']
            best=np.full(valid.shape,np.inf);sector=np.full(valid.shape,-1,int)
            for a in e['annotations']:
                if a['floor']!=v['rank'] or not a['key'].startswith('Sector_'):continue
                index=ord(a['key'][-1])-65
                for c in a['components']:
                    x,y,z=c['world']['location'];score=(wx-x)**2+(wy-y)**2+9*(np.nan_to_num(v['grid'],nan=z)+180-z)**2
                    pick=valid&(score<best);best[pick]=score[pick];sector[pick]=index
            sector[best>2500**2]=-1
            # Attribute whole rooms together so a seed bisector cannot split a room.
            for mask,text,colour,source in room_entries:
                votes=sector[mask];votes=votes[votes>=0]
                if len(votes):sector[mask]=np.bincount(votes).argmax()
            for index,colour in enumerate(SECTORS):
                mask=(sector==index)&valid
                outline=(bv.dilate(mask,1)&walls)
                image[outline]=[*bytes.fromhex(colour),255]
                placement=mask&~taken&~bv.dilate(walls,1)
                result=label_row(v,placement,chr(65+index)+'区',colour,'sector','game zone seeds; contour attribution inferred')
                if result:all_labels.append(result[0]);audit.append(result[1])
        # Neutral walls for normal rooms; sector contour tint remains distinct.
        neutral=walls&np.all(image[:,:,:3]==[14,22,27],axis=2)
        image[neutral]=[48,59,67,255]
        for mask,text,colour,source in room_entries:
            result=label_row(v,mask,text,colour,'room',source)
            if result:all_labels.append(result[0]);audit.append(result[1])
        for near,text,source in shell_less:
            result=label_row(v,near&~taken&~bv.dilate(walls,1),text,PALETTE[text],'point',source+' (no room shell; origin label only)')
            if result:all_labels.append(result[0]);audit.append(result[1])
        # Existing equipment / region anchors have no certified room polygon.
        # Give them a conservative nearby free-floor text slot, never a room fill.
        for x,y,text in old_labels[v['id']]:
            if text=='杀戮房':continue # Only the verified room shell on its entrance level may carry this name.
            if text in used or text.startswith('感染区') and '感染区' in used or b['type']=='abandoned' and re.fullmatch('[A-H]区',text):continue
            col=int((v['x1']-x)/v['step']);row=int((v['y1']-y)/v['step']);radius=max(2,int(350/v['step']))
            mask=np.zeros(valid.shape,bool)
            if 0<=col<v['w'] and 0<=row<v['h']:
                mask[max(0,row-radius):row+radius+1,max(0,col-radius):col+radius+1]=True
                mask&=valid&~taken&~bv.dilate(walls,1)
                result=label_row(v,mask,text,'C4D0D5','point','existing evidence anchor; no room boundary claim')
                if result:all_labels.append(result[0]);audit.append(result[1])
        Image.fromarray(image).save(OUT/v['image']);counts.append(dict(bunker=v['bunker'],view=v['rank'],rooms=room_count))
        print(v['bunker'],v['rank'],'rooms',room_count,flush=True)
    (OUT/'labels-bounded.tsv').write_text('\n'.join('\t'.join(map(str,r)) for r in all_labels)+'\n',encoding='utf-8')
    # Update only the image hashes in the presentation atlas; detection remains immutable.
    data=bytearray(gzip.decompress((OUT/'views.bin').read_bytes()));pos=16
    for v in views:
        for _ in range(4):n=struct.unpack_from('<i',data,pos)[0];pos+=4+n
        pos+=64;data[pos:pos+32]=hashlib.sha256((OUT/v['image']).read_bytes()).digest();pos+=32
        n=struct.unpack_from('<i',data,pos)[0];pos+=4+n*260
        n=struct.unpack_from('<i',data,pos)[0];pos+=4+n*36
    assert pos==len(data)
    (OUT/'views.bin').write_bytes(gzip.compress(bytes(data),compresslevel=6))
    (ROOT/'checks/room-style-audit.json').write_text(json.dumps(dict(labels=len(all_labels),rooms=sum(x['rooms'] for x in counts),records=counts,labelChecks=audit,limitations=['Sector outline attribution uses semantic seeds, not unexported collision-box extents.','Unresolved room shells remain white; no guessed room fill.']),ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'room-colours.tsv').write_text('\n'.join(k+'\t#'+v for k,v in PALETTE.items())+'\n',encoding='utf-8')
    print('BOUNDED LABELS',len(all_labels),'ROOMS',sum(x['rooms'] for x in counts))
if __name__=='__main__':main()
