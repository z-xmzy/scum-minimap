"""Offline-only conversion of extracted architectural meshes. Never reads a live game."""
import sys, pathlib, hashlib, struct, gzip, json
HERE=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent/'bunker-extraction-20261031'))
from bunker_geometry import ROOT,read_json,write_json,placements,structural
from floorplans import read_mesh,transform_vertices,cut_segments,transform_triangles
from build_floor_candidates import candidates
import numpy as np
from PIL import Image,ImageDraw,ImageChops

OUT=HERE/'assets-v2/bunkers'

def write_grid(f,grid):
    h,w=grid.shape;tiles=[];columns=(w+7)//8
    for row in range(0,h,8):
        for col in range(0,w,8):
            part=grid[row:row+8,col:col+8]
            if not np.isfinite(part).any():continue
            tile=np.full((8,8),np.nan,dtype='<f4');tile[:part.shape[0],:part.shape[1]]=part
            tiles.append(((row//8)*columns+col//8,tile))
    f.write(struct.pack('<i',len(tiles)))
    for index,tile in tiles:f.write(struct.pack('<i',index));f.write(tile.tobytes())

def triangles(items):
    return np.concatenate([transform_triangles(*read_mesh(p['mesh']),p['transform']) for p in items])

def raster_height(tris,hi,step,size,maximum=True,minimum_above=None):
    grid=np.full((int(size[1]),int(size[0])),np.nan,dtype=np.float32)
    for tri in tris:
        xy=(hi-tri[:,:2])/step
        low=np.maximum(0,np.floor(xy.min(axis=0)-.5).astype(int))
        high=np.minimum(size-1,np.ceil(xy.max(axis=0)-.5).astype(int))
        if np.any(high<low): continue
        a,b,c=xy; v=b-a; w=c-a; det=v[0]*w[1]-v[1]*w[0]
        if abs(det)<1e-6: continue
        if minimum_above is not None:
            block=minimum_above[low[1]:high[1]+1,low[0]:high[0]+1]
            yy,xx=np.nonzero(np.isfinite(block))
            if not len(xx):continue
            yy+=low[1];xx+=low[0]
            dx=xx+.5-a[0];dy=yy+.5-a[1]
            s=(dx*w[1]-dy*w[0])/det;t=(v[0]*dy-v[1]*dx)/det
            zz=(tri[0,2]+s*(tri[1,2]-tri[0,2])+t*(tri[2,2]-tri[0,2])).astype(np.float32)
            dst=grid[yy,xx];ground=minimum_above[yy,xx]
            choose=(s>=-1e-6)&(t>=-1e-6)&(s+t<=1+1e-6)&(zz>ground+5)&(np.isnan(dst)|(zz<dst))
            grid[yy[choose],xx[choose]]=zz[choose]
            continue
        yy,xx=np.mgrid[low[1]:high[1]+1,low[0]:high[0]+1]
        dx=xx+.5-a[0];dy=yy+.5-a[1]
        s=(dx*w[1]-dy*w[0])/det; t=(v[0]*dy-v[1]*dx)/det
        inside=(s>=-1e-6)&(t>=-1e-6)&(s+t<=1+1e-6)
        zz=(tri[0,2]+s*(tri[1,2]-tri[0,2])+t*(tri[2,2]-tri[0,2])).astype(np.float32)
        dst=grid[low[1]:high[1]+1,low[0]:high[0]+1]
        choose=inside&(np.isnan(dst)|((zz>dst) if maximum else (zz<dst)))
        dst[choose]=zz[choose]
    return grid

def build():
    OUT.mkdir(parents=True,exist_ok=True)
    all_rows=[]
    for bi,b in enumerate(read_json(ROOT/'output/bunker-catalogue.json')['bunkers'],1):
        groups=[triangles(read_json(ROOT/'output/bunkers'/(b['id']+'.json'))['placements'])]
        for name in b.get('entranceScenes',[]):
            items=[p for p in placements(read_json(ROOT/'output/scenes'/(name+'.json'))) if structural(p['mesh']) and '/Buildings/Bunker/' not in p['mesh']]
            if items: groups.append(triangles(items))
        # Collect independently so a small entrance landing is not discarded beside a large interior.
        heights=[]
        for g in groups: heights.extend(candidates(g)[0])
        levels=[]
        for z in sorted(heights):
            if not levels or z-levels[-1]>100: levels.append(z)
        t=np.concatenate(groups)
        cross=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]); norm=np.linalg.norm(cross,axis=1)
        up=t[(cross[:,2]>.5*norm)&(norm>2)]
        down=t[(cross[:,2]<-.5*norm)&(norm>2)]
        lo=t[:,:,:2].min(axis=(0,1))-100;hi=t[:,:,:2].max(axis=(0,1))+100
        step=max(20.,float(max(hi-lo))/1000)
        size=np.ceil((hi-lo)/step).astype(int)
        # Exact image rectangle and grid bounds must agree, including the rounded last pixel.
        lo=hi-size*step
        uz=up[:,:,2].mean(axis=1); dz=down[:,:,2].mean(axis=1)
        nearest=np.argmin(np.abs(uz[:,None]-np.array(levels)[None,:]),axis=1)
        rows=[]
        for li,z in enumerate(levels):
            # Flat floors and stair treads belong to their nearest height layer.
            floor_tris=up[(nearest==li)&(np.abs(uz-z)<800)]
            grid=raster_height(floor_tris,hi,step,size)
            if not np.isfinite(grid).any():continue
            # Stair treads differ from the reference layer height. Test headroom against
            # each actual surface and the FIRST overhead face, not the highest ceiling.
            roof_candidates=down[(down[:,:,2].max(axis=1)>np.nanmin(grid)+5)&(down[:,:,2].min(axis=1)<np.nanmax(grid)+3000)]
            roof=raster_height(roof_candidates,hi,step,size,maximum=False,minimum_above=grid)
            grid[(~np.isfinite(roof))|(roof-grid<140)|(roof-grid>3000)]=np.nan
            if np.count_nonzero(np.isfinite(grid))*step*step<30000: continue
            slug=hashlib.sha256((b['id']+':'+str(round(z))).encode()).hexdigest()[:20]
            mask=Image.fromarray(np.where(np.isfinite(grid),255,0).astype('uint8'))
            image=Image.new('RGBA',tuple(size),(14,22,27,255))
            image.paste((49,78,85,255),(0,0),mask)
            # Distinguish stair surfaces from the level floor without pretending to compute a route.
            stairs=Image.fromarray(np.where(np.isfinite(grid)&(np.abs(grid-z)>80),255,0).astype('uint8'))
            image.paste((117,91,53,255),(0,0),stairs)
            draw=ImageDraw.Draw(image)
            for segment in cut_segments(t,z+150):
                draw.line([tuple(p) for p in (hi-segment)/step],fill=(183,215,217,255),width=1)
            yy,xx=np.where(np.isfinite(grid));padding=max(2,int(np.ceil(100/step)))
            x0=max(0,int(xx.min())-padding);y0=max(0,int(yy.min())-padding)
            x1=min(int(size[0]),int(xx.max())+padding+1);y1=min(int(size[1]),int(yy.max())+padding+1)
            image=image.crop((x0,y0,x1,y1));grid=grid[y0:y1,x0:x1].copy()
            layer_hi=hi-np.array([x0,y0])*step;layer_lo=hi-np.array([x1,y1])*step
            path=OUT/(slug+'.png');image.save(path)
            rows.append(dict(id=slug,bunker=b['id'],kind=b['type'],z=float(z),lo=layer_lo.tolist(),hi=layer_hi.tolist(),step=step,
                             width=x1-x0,height=y1-y0,image=path.name,sha=hashlib.sha256(path.read_bytes()).digest(),grid=grid))
        for rank,row in enumerate(sorted(rows,key=lambda r:-r['z']),1): row.update(rank=rank,count=len(rows))
        assert rows,b['id']
        all_rows.extend(rows)
        if bi%10==0 or bi==93: print(f'{bi}/93 bunkers, {len(all_rows)} layers',flush=True)
    with gzip.open(OUT/'atlas.bin','wb',compresslevel=6) as f:
        def string(s):
            data=s.encode('utf-8'); f.write(struct.pack('<i',len(data)));f.write(data)
        f.write(b'SBMA');f.write(struct.pack('<iii',2,26320533,len(all_rows)))
        for r in all_rows:
            for key in ['id','bunker','kind','image']:string(r[key])
            f.write(struct.pack('<ii6dii',r['rank'],r['count'],*r['lo'],*r['hi'],r['z'],r['step'],r['width'],r['height']))
            f.write(r['sha']);write_grid(f,r['grid'])
    write_json(OUT/'index.json',{'format':2,'gameBuild':26320533,'bunkers':93,'floorLabels':'geometry height layers; not official floor numbers',
        'layers':[{k:v for k,v in r.items() if k not in ('grid','sha')} for r in all_rows]})
    # Local calibration stays out of distributed assets/source.
    checked=[]
    for path in sorted((ROOT/'calibration/samples').glob('*.json')):
        sample=read_json(path)
        if 'positionCm' not in sample:continue
        x,y,z=sample['positionCm']; hits=[]
        for r in all_rows:
            if r['bunker']!='B_2_Research_Facility':continue
            ix=int(np.floor((r['hi'][0]-x)/r['step']));iy=int(np.floor((r['hi'][1]-y)/r['step']))
            if not(0<=ix<r['width'] and 0<=iy<r['height']):continue
            g=r['grid'][max(0,iy-1):iy+2,max(0,ix-1):ix+2]
            delta=z-g;ok=(delta>=20)&(delta<=145)
            if ok.any():hits.append({'layer':r['rank'],'surface':float(g[ok][0])})
        checked.append({'sample':sample['id'],'matches':hits})
    write_json(HERE/'checks/atlas-calibration.json',checked)
    assert len(checked)==9 and all(r['matches'] for r in checked),checked
    print(f'PASS: {len(all_rows)} layers / 93 instances / nine calibration samples')

if __name__=='__main__':build()
