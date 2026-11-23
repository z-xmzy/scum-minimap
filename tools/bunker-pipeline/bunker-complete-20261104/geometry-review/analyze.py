import collections,gzip,json,pathlib,struct,sys
BASE=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(BASE/'bunker-extraction-20261031/tools/python-libs'))
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
OUT=pathlib.Path(__file__).resolve().parent
FOLDER=BASE/'marker-icons-20261103/app/assets/bunkers'
def load(filename='atlas.bin'):
    rows=[]
    with gzip.open(FOLDER/filename,'rb') as f:
        magic,version,build,count=struct.unpack('<4siii',f.read(16));assert magic==b'SBMA' and version==3
        for _ in range(count):
            strings=[f.read(struct.unpack('<i',f.read(4))[0]).decode() for j in range(4)]
            rank,total,x0,y0,x1,y1,z,step,w,h=struct.unpack('<ii6dii',f.read(64));f.read(32)
            grid=np.full((h,w),np.nan,dtype='<f4');columns=(w+7)//8
            for j in range(struct.unpack('<i',f.read(4))[0]):
                index=struct.unpack('<i',f.read(4))[0];tile=np.frombuffer(f.read(256),dtype='<f4').reshape(8,8)
                row=index//columns*8;col=index%columns*8;grid[row:row+8,col:col+8]=tile[:min(8,h-row),:min(8,w-col)]
            stairs=np.frombuffer(f.read(struct.unpack('<i',f.read(4))[0]*36),dtype='<f4').reshape(-1,3,3)
            rows.append(dict(id=strings[0],bunker=strings[1],kind=strings[2],image=strings[3],rank=rank,total=total,x0=x0,y0=y0,x1=x1,y1=y1,z=z,step=step,w=w,h=h,grid=grid,stairs=stairs))
        assert f.read()==b''
    return rows
def aligned(rows):
    step=rows[0]['step'];assert all(abs(r['step']-step)<1e-6 for r in rows)
    hi=np.max([[r['x1'],r['y1']] for r in rows],axis=0)
    lo=np.min([[r['x0'],r['y0']] for r in rows],axis=0)
    size=np.rint((hi-lo)/step).astype(int)
    result=[]
    for r in rows:
        offset=(hi-[r['x1'],r['y1']])/step
        assert np.max(abs(offset-np.rint(offset)))<1e-4
        x,y=np.rint(offset).astype(int);g=np.full((size[1],size[0]),np.nan,dtype='f4');g[y:y+r['h'],x:x+r['w']]=r['grid'];result.append(g)
    return hi,step,result
def main():
    rows=load();groups=collections.defaultdict(list)
    for r in rows:groups[r['bunker']].append(r)
    metrics=[]
    for name,rs in groups.items():
        rs.sort(key=lambda r:r['rank']);hi,step,gs=aligned(rs)
        details=[];overlap=[]
        for r,g in zip(rs,gs):
            im=np.array(Image.open(FOLDER/r['image']).convert('RGB'));mask=np.isfinite(r['grid'])
            ground=Image.fromarray((mask*255).astype('uint8'))
            d=ImageDraw.Draw(ground)
            for tri in r['stairs']:d.polygon([tuple(p) for p in (np.array([r['x1'],r['y1']])-tri[:,:2])/step],fill=255)
            near=np.array(ground.filter(ImageFilter.MaxFilter(11)))>0
            walls=np.all(im==[183,215,217],axis=2)
            details.append(dict(rank=r['rank'],id=r['id'],z=r['z'],gridAreaM2=round(float(np.isfinite(g).sum()*step**2/10000),2),surfaceFaces=len(r['stairs']),zRange=[float(np.nanmin(g)),float(np.nanmax(g))],wallPixels=int(walls.sum()),wallsFartherThan5Cells=int((walls&~near).sum())))
        for i in range(len(rs)):
            for j in range(i+1,len(rs)):
                shared=np.isfinite(gs[i])&np.isfinite(gs[j]);a=float(shared.sum()*step**2/10000)
                if a:overlap.append(dict(ranks=[rs[i]['rank'],rs[j]['rank']],areaM2=round(a,2),verticalGapMedianCm=float(np.median(abs(gs[i][shared]-gs[j][shared])))))
        metrics.append(dict(bunker=name,kind=rs[0]['kind'],layers=details,overlaps=overlap))
        if name=='D_1_Abandoned':
            sheet=Image.new('RGB',(1200,4*330),'#172027');draw=ImageDraw.Draw(sheet)
            for i,(r,g) in enumerate(zip(rs,gs)):
                im=Image.open(FOLDER/r['image']).convert('RGB');im.thumbnail((390,285));x=i%3*400;y=i//3*330
                sheet.paste(im,(x+(400-im.width)//2,y+40));draw.text((x+10,y+10),f"{r['rank']}/11  Z={r['z']:.1f}  {details[i]['gridAreaM2']}m2",fill='white')
            sheet.save(OUT/'d1-original-slices.jpg')
    cats=[]
    for path in [BASE/'bunker-extraction-20261031/output/bunker-catalogue.json',BASE/'bunker-20261102/extracted/output/bunker-catalogue.json']:
        cats.extend(json.loads(path.read_text(encoding='utf-8-sig'))['bunkers'])
    semantics=[]
    for b in cats:
        anchors=b.get('floorAnchors',[]);floors=collections.defaultdict(list)
        for a in anchors:floors[a['floor']].append(a)
        semantics.append(dict(bunker=b['id'],kind=b['type'],anchorCount=len(anchors),floorDefinition=b.get('floorDefinition'),floors={str(k):dict(count=len(v),zRange=[min(a['transform']['location'][2] for a in v),max(a['transform']['location'][2] for a in v)]) for k,v in floors.items()}))
    result=dict(atlasLayers=len(rows),atlasBunkers=len(groups),kinds=dict(collections.Counter(r['kind'] for r in metrics)),metrics=metrics,semantics=semantics)
    (OUT/'geometry-metrics.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('metrics','semantics')},indent=2))
    print(json.dumps(next(m for m in metrics if m['bunker']=='D_1_Abandoned'),indent=2))
    print('Semantic anchor coverage:',[s for s in semantics if s['anchorCount']])
if __name__=='__main__':main()
