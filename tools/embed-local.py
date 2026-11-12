from pathlib import Path
import struct,gzip,json
import numpy as np
import cv2
from bake import dump
root=Path(__file__).resolve().parents[1];out=root/'bake/audit';W=3804
# Always regenerate from the immutable world2 base, including when rebuilding.
old=root.parent/'SCUM-MiniMap-world2-20261112/source/resources'
def layers(name):
    with gzip.open(old/name,'rb') as f:
        f.read(8);ls=[]
        for _ in range(3):
            _,w,h=struct.unpack('<fII',f.read(12));ls.append(np.frombuffer(f.read(w*h),np.uint8).reshape(h,w).copy())
        return ls
grid=layers('nav-world-grid.bin');terrain=layers('nav-world-terrain.bin')
regs=json.loads((out/'regions.json').read_text());masks=[]
for r in regs:
    s=r['s'];n=r['n'];m=np.fromfile(out/(r['key']+'.mask'),np.uint8).reshape(n*s,n*s);masks.append(m)
    g=grid[0][r['y']:r['y']+n,r['x']:r['x']+n];t=terrain[0][r['y']:r['y']+n,r['x']:r['x']+n]
    b=m.reshape(n,s,n,s)
    clear=(b==0).all(axis=(1,3));solid=(b==1).any(axis=(1,3));wet=(b==2).any(axis=(1,3));steep=(b==3).any(axis=(1,3))
    # A mixed coarse cell belongs to the detailed router only. Through-traffic can use wholly clear cells.
    g[(g==255)&clear]=8;g[solid]=255;g[wet]=16;t[steep]=4
dump('nav-world-grid.bin',b'NAVG',grid);dump('nav-world-terrain.bin',b'NTR1',terrain)
_,comp,sizes,_=cv2.connectedComponentsWithStats(np.uint8((grid[0]<16)&(terrain[0]<4)),connectivity=4)
with gzip.GzipFile(str(root/'source/resources/nav-local.bin'),'wb',mtime=0) as f:
    f.write(struct.pack('<4sii',b'LCL1',W,len(regs)))
    for r,m in zip(regs,masks,strict=True):
        x=r['x'];y=r['y'];s=r['s'];n=r['n'];ids=comp[y:y+n,x:x+n]
        _,micro=cv2.connectedComponents(np.uint8(m==0),connectivity=4)
        cs=micro[s//2::s,s//2::s];areas=sizes[ids,cv2.CC_STAT_AREA];areas[ids==0]=0
        largest=np.zeros(micro.max()+1,dtype=np.int64);np.maximum.at(largest,cs,areas)
        borders=np.fromfile(out/(r['key']+'.edges'),np.uint8).reshape(4,n*s)
        exits=[]
        for k,(land,edge) in enumerate(((comp[y:y+n,x-1],micro[:,0]),(comp[y:y+n,x+n],micro[:,-1]),(comp[y-1,x:x+n],micro[0]),(comp[y+n,x:x+n],micro[-1]))):
            labels=land.repeat(s); ar=sizes[labels,cv2.CC_STAT_AREA].copy()
            ar[(labels==0)|(edge==0)|(borders[k]!=0)]=0
            np.maximum.at(largest,edge,ar);exits.append((ar,edge))
        anchor=(ids!=0)&(cs!=0)&(areas==largest[cs]);flags=m.copy()
        flags[s//2::s,s//2::s][anchor]=4
        for k,(view,(ar,edge)) in enumerate(zip((flags[:,0],flags[:,-1],flags[0],flags[-1]),exits,strict=True)):
            view[(ar>0)&(ar==largest[edge])]=5+k
        f.write(struct.pack('<iiii',x,y,n,s));f.write(flags.tobytes())
