"""Build the three runtime routing layers. Inputs are UE world geometry and URD1 only."""
import struct, gzip, json, subprocess
from pathlib import Path
import cv2
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
B=ROOT/'bake'
OUT=ROOT/'source/resources'
W=3804
def raster(name):
    return np.fromfile(B/name,'<f4').reshape(W,W)
def sample(dem,x,y):
    # Triangle interpolation, matching the game's landscape diagonal.
    a=np.clip((14.82+x)/1.5,0,10159.99999); b=np.clip((5.82+y)/1.5,0,10159.99999)
    i=a.astype('int32'); j=b.astype('int32'); fx=a-i; fy=b-j
    p=dem[j,i];q=dem[j,i+1];r=dem[j+1,i];s=dem[j+1,i+1]
    return np.where(fx+fy<=1,p+fx*(q-p)+fy*(r-p),s+(1-fx)*(r-s)+(1-fy)*(q-s))
def markings():
    data=(ROOT/'app/data/user-roads.bin').read_bytes()
    assert data[:4]==b'URD1'
    o=12
    result=np.zeros((W,W),np.uint8)
    count=struct.unpack_from('<I',data,8)[0]
    for _ in range(count):
        typ,n=struct.unpack_from('<BI',data,o);o+=5
        pts=np.frombuffer(data,'<f4',2*n,o).reshape(-1,2);o+=8*n
        assert typ in (1,2) and np.isfinite(pts).all()
        cv2.polylines(result,[np.rint((pts.astype(float)*W-.5)*256).astype('int32')],False,typ,3 if typ==1 else 2,cv2.LINE_8,8)
    assert o==len(data)
    return result,count
def dump(name,magic,layers):
    with gzip.GzipFile(str(OUT/name),'wb',compresslevel=6,mtime=0) as f:
        f.write(struct.pack('<4sHH',magic,1,len(layers)))
        for size,a in zip([4,16,64],layers):
            f.write(struct.pack('<fII',size,a.shape[1],a.shape[0]));f.write(a.tobytes())
def bake():
    dem=np.memmap(B/'height/tmp/work.f32','<f4','r',shape=(10161,10161))
    ground=np.empty((W,W),'float32');grad=np.empty((W,W,2),'int8')
    x=(np.arange(W)+.5)*15216.18/W
    for j in range(0,W,64):
        y=(np.arange(j,min(j+64,W))[:,None]+.5)*15236.18/W
        sl=slice(j,j+len(y));ground[sl]=sample(dem,x,y)
        for k,(dx,dy) in enumerate([(3,0),(0,3)]):
            rise=(sample(dem,x+dx,y+dy)-sample(dem,x-dx,y-dy))/600.0
            grad[sl,:,k]=np.clip(np.rint(rise*100),-127,127).astype('int8')
    ground.tofile(B/'ground.f32')
    marks,n=markings()
    ws=raster('water.f32');ds=raster('deck.f32');rs=raster('roads.f32')
    # Admit upper surfaces adjoining a road/hand line. Reject disconnected roofs and supports.
    near= cv2.dilate(((marks>0)|np.isfinite(rs)).astype(np.uint8),np.ones((11,11),np.uint8))>0
    deck=np.isfinite(ds)&near&(ds>ground-100)
    ds[~deck]=-np.inf;ds.tofile(B/'deck-used.f32')
    subprocess.run([B/'Footprints.exe'],cwd=B,check=True)
    collision=np.fromfile(B/'obstacles.u8',np.uint8).reshape(W,W)
    ocean=ground<=0
    water=ocean|((ws>ground+15)&np.isfinite(ws))
    road=np.isfinite(rs)&(rs>=ground-120)&(rs<=ground+300)&(~water|(rs>ws+35)&(rs>35))
    road|=deck & (~np.isfinite(ws)|(ds>ws+35))
    fine=np.full((W,W),4,np.uint8)
    # Brush adjacent to fixed solid obstacles is costlier for cross-country routes.
    dense=cv2.blur(((collision&7)!=0).astype('float32'),(9,9))>.23
    fine[dense]=8;fine[water]=16;fine[road]=1;fine[marks!=0]=marks[marks!=0]
    known=(marks!=0)|road
    fine[(collision!=0)&(~known)&(~water)]=8
    # URD1 corridors take priority over approximate mesh bounds, including bridge approaches.
    solid=((collision&128)!=0)&(marks==0)
    fine[solid]=255
    # User road at a documented water crossing has a surface distinct from the submerged bed.
    grad[known]=0
    angle=np.degrees(np.arctan(np.hypot(grad[:,:,0].astype(float),grad[:,:,1].astype(float))/100))
    cls=sum((angle>=t).astype(np.uint8) for t in [15,22,30,40]);cls[known]=0
    levels=[fine];terrain=[cls]
    for ncell in [951,238]:
        def fraction(mask):return cv2.resize(mask.astype('float32'),(ncell,ncell),interpolation=cv2.INTER_AREA)
        # Coarse cells guide the search. The fine grid decides actual clearance.
        free=np.isin(fine,[1,2,4,8]);nav=np.full((ncell,ncell),4,np.uint8)
        nav[fraction(fine==8)>.4]=8;nav[fraction(free)<.07]=255
        nav[fraction(fine==16)>.93]=16
        nav[fraction(fine==2)>.01]=2;nav[fraction(fine==1)>.01]=1
        slope=np.rint(fraction(cls)).astype('uint8');slope[nav<=2]=0
        levels.append(nav);terrain.append(slope)
    dump('nav-world-grid.bin',b'NAVG',levels);dump('nav-world-terrain.bin',b'NTR1',terrain)
    with gzip.GzipFile(str(OUT/'nav-world-grade.bin'),'wb',compresslevel=6,mtime=0) as f:
        f.write(struct.pack('<4sIII',b'GRD1',W,W,26593778));f.write(grad.tobytes())
    stats=dict(game_build=26593778,tiles=400,scenes=len((B/'scenes.txt').read_text().splitlines()),hand_roads=n,water_mesh_cells=int(np.isfinite(ws).sum()),deck_cells=int(deck.sum()),road_mesh_cells=int(road.sum()),blocked_cells=int(solid.sum()),obstacle_instances=sum(p.stat().st_size//44 for p in (B/'tiles').glob('*.bin')),classes={int(k):int(v) for k,v in zip(*np.unique(fine,return_counts=True))})
    (ROOT/'checks/world-coverage.json').write_text(json.dumps(stats,indent=2),'utf-8');print(stats)
if __name__=='__main__':bake()
