"""Island-wide vehicle fallback. The desktop router supplies only failed coarse queries."""
from pathlib import Path
import gzip, subprocess, sys, time, os, math, zlib
from collections import OrderedDict
import numpy as np
import cv2
from scipy.spatial import cKDTree
from landscape import sample
R=Path(__file__).resolve().parent
D=R/'world'
W=3804
T=8
S=16
def uvcell(p):return np.floor(np.asarray(p)*W).astype(int)
def layer(name):
    with gzip.open(D/name,'rb') as f:
        f.read(20);return np.frombuffer(f.read(W*W),np.uint8).reshape(W,W)
def talk(proc,request):
    proc.stdin.write(request+'\n');proc.stdin.flush()
    response=proc.stdout.readline()
    if response=='':raise RuntimeError('worker stopped: '+request)
    return response.rstrip('\r\n')
class Engine:
    def __init__(self,work):
        self.work=work
        self.cache=OrderedDict();self.bakes=0;self.used={}
        self.instances=np.load(D/'instances.npy',mmap_mode='r')
        self.radii=np.load(D/'radius.npy',mmap_mode='r')
        self.bins=[]
        cats=np.ceil(np.log2(np.maximum(1,self.radii))).astype(int)
        for cat in np.unique(cats):
            ii=np.flatnonzero(cats==cat);self.bins.append((2.**cat,ii,cKDTree(self.instances['p'][ii,:2])))
        self.dem=np.memmap(D/'height.f32','<f4','r',shape=(10161,10161))
        self.water,self.deck,self.road=[np.memmap(D/f,'<f4','r',shape=(W,W)) for f in ['water.f32','deck-used.f32','roads.f32']]
        self.marks=np.frombuffer(zlib.decompress((D/'handroads.z').read_bytes()),'u1').reshape(W,W); self.cost=layer('nav-world-grid.bin');self.terrain=layer('nav-world-terrain.bin')
        _,self.comp,stats,_=cv2.connectedComponentsWithStats(np.uint8((self.cost<16)&(self.cost>0)&(self.terrain<4)),connectivity=4)
        self.area=stats[:,-1];self.area[0]=0;self.bounds=stats[:,:4]
        opts=dict(text=True,stdin=subprocess.PIPE,stdout=subprocess.PIPE,bufsize=1)
        self.worker=subprocess.Popen([R/'WorldRaster.exe',D],cwd=self.work,creationflags=subprocess.CREATE_NO_WINDOW,**opts)
    def close(self):
        self.worker.stdin.close()
        self.worker.wait()
    def coarse(self,a,b,mode):
        print('BASE '+' '.join(format(float(v),'.17g') for v in (*a,*b)),flush=True)
        value=input()
        if value=='-':return None
        return [tuple(map(float,pt.split(','))) for pt in value.split(';')]
    def field(self,x,y):
        u=np.clip(x/W,0,np.nextafter(1.,0.));v=np.clip(y/W,0,np.nextafter(1.,0.))
        i=(u*W).astype(int);j=(v*W).astype(int)
        xm=u*15216.18;ym=v*15236.18
        z=sample(self.dem,xm,ym);gx=(sample(self.dem,xm+3,ym)-sample(self.dem,xm-3,ym))/600;gy=(sample(self.dem,xm,ym+3)-sample(self.dem,xm,ym-3))/600
        d=self.deck[j,i];r=self.road[j,i];w=self.water[j,i]
        water=(z<=0)|(w>z+15)
        known=(self.marks[j,i]!=0)|(np.isfinite(r)&(r>=z-120)&(r<=z+300)&(~water|((r>w+35)&(r>35))))|(np.isfinite(d)&(d>w+35))
        flags=np.where(known,0,np.where(water,2,np.where(np.hypot(gx,gy)>=math.tan(math.radians(40)),3,0))).astype('u1')
        return np.maximum(z,d),flags
    def tile(self,x,y,n=T,s=S):
        key=(x,y,n,s)
        if key in self.cache:
            self.cache.move_to_end(key);self.used[key]=self.cache[key];return self.cache[key]
        width=n*s; indices=np.arange(width+1,dtype=float)/s
        # All four corners share one lattice; evaluate each corner exactly once.
        z,f=self.field(x+indices,y+indices[:,None])
        zs=[z[:-1,:-1],z[1:,:-1],z[:-1,1:],z[1:,1:]]
        fs=[f[:-1,:-1],f[1:,:-1],f[:-1,1:],f[1:,1:]]
        mid=indices[:-1]+.5/s;zc,fc=self.field(x+mid,y+mid[:,None])
        zs.append(zc);fs.append(fc);lo=np.minimum.reduce(zs);hi=np.maximum.reduce(zs);flags=np.maximum.reduce(fs)
        flags[(np.arange(width)[:,None]/s+y>=W)|(np.arange(width)[None,:]/s+x>=W)]=3
        half=np.array([1521618,1523618])*n/(2*W);center=np.array([617718,618618])-np.array([x+n/2,y+n/2])*[1521618/W,1523618/W]
        selected=[]
        for radius,ii,tree in self.bins:
            ids=ii[tree.query_ball_point(center,np.linalg.norm(half)+radius)]
            if len(ids):selected.extend(ids[np.all(np.abs(self.instances['p'][ids,:2]-center)<=half+self.radii[ids,None],axis=1)])
        data=self.instances[np.asarray(selected,dtype=int)]
        for suffix,arr in [('low',lo.astype('<f4')),('high',hi.astype('<f4')),('base',flags),('instances',data)]:arr.tofile(self.work/('work.'+suffix))
        if talk(self.worker,f'R {x} {y} {width} {s}')!='OK':raise RuntimeError('raster')
        mask=np.fromfile(self.work/'work.mask',np.uint8).reshape(width,width)
        self.cache[key]=mask;self.used[key]=mask;self.bakes+=1
        while len(self.cache)>192:self.cache.popitem(last=False)
        return mask
    def mosaic(self,l,t,r,b):
        l=max(0,l);t=max(0,t);r=min((W-1)//T,r);b=min((W-1)//T,b)
        return np.block([[self.tile(i*T,j*T) for i in range(l,r+1)] for j in range(t,b+1)]),np.array([l*T,t*T]),S
    def search(self,patch,p,goals):
        m,origin,s=patch
        q=np.floor((np.array(p)*W-origin)*s).astype(int)
        if np.any(q<0) or q[0]>=m.shape[1] or q[1]>=m.shape[0] or m[q[1],q[0]]:return None
        flags=np.uint8(m!=0);flags[(m==0)&goals]=2;flags.tofile(self.work/'work.path')
        raw=talk(self.worker,f'P {m.shape[1]} {q[1]*m.shape[1]+q[0]}')
        if not raw:return None
        ids=np.fromstring(raw,sep=',',dtype=int);xy=np.column_stack([ids%m.shape[1],ids//m.shape[1]])
        # Only merge collinear steps. Native BFS disallows diagonal corner cutting.
        if len(xy)>2:xy=xy[np.r_[True,np.any(np.diff(xy,n=2,axis=0)!=0,axis=1),True]]
        return [p,*map(tuple,(origin+(xy+.5)/s)/W)]
    def approach(self,p):
        c=uvcell(p);tx,ty=c//T;pre=[p];cur=p
        for halo in (0,1,2):
            patch=self.mosaic(tx-halo,ty-halo,tx+halo,ty+halo);m,o,s=patch
            q=np.floor((np.array(cur)*W-o)*s).astype(int)
            if m[q[1],q[0]]:
                if halo or len(pre)>1:return None
                # Recheck a boundary case at 6.25 cm, then return to fully clear 25 cm cells.
                hf=self.tile(*c,1,64);go=np.zeros_like(hf,bool)
                dx,dy=(c-o)*s
                free=m[dy:dy+s,dx:dx+s]==0
                go[:]=free.repeat(4,axis=0).repeat(4,axis=1)
                pre=self.search((hf,c,64),p,go)
                if pre is None:return None
                cur=pre[-1]
            hh,ww=m.shape;cy=np.minimum(np.arange(hh//s)+o[1],W-1);cx=np.minimum(np.arange(ww//s)+o[0],W-1)
            labs=self.comp[np.ix_(cy,cx)]
            valid=(labs!=0)&(self.area[labs]>=min(5000,max(1,self.area[labs].max())))
            go=np.zeros_like(m,bool);go[s//2::s,s//2::s]=valid
            route=self.search(patch,cur,go)
            if route:return pre[:-1]+route,patch
            _,connected=cv2.connectedComponents(np.uint8(m==0),connectivity=4)
            q=np.floor((np.array(cur)*W-o)*s).astype(int);label=connected[q[1],q[0]]
            if label not in np.r_[connected[0],connected[-1],connected[:,0],connected[:,-1]]:break
        return None
    def link(self,a,b,pad=0):
        ta=uvcell(a)//T;tb=uvcell(b)//T
        lo=np.minimum(ta,tb)-pad;hi=np.maximum(ta,tb)+pad
        if (hi-lo+1).prod()>25:return None
        m,o,s=self.mosaic(lo[0],lo[1],hi[0],hi[1]);q=np.floor((np.asarray(b)*W-o)*s).astype(int)
        goals=np.zeros_like(m,bool);goals[q[1],q[0]]=True
        path=self.search((m,o,s),a,goals)
        return path+[b] if path else None


    def splice(self,access,core):
        approach,patch=access;m,o,s=patch
        def pos(p):return (np.asarray(p)*W-o)*s
        def contains(q):return 0<=q[0]<m.shape[1] and 0<=q[1]<m.shape[0]
        k=next(i for i,p in enumerate(approach) if contains(pos(p)) and m[int(pos(p)[1]),int(pos(p)[0])]==0)
        # Preserve the base route beyond its first exit. Recheck the whole prefix inside this patch.
        tail=[];target=core[-1]
        for j in range(1,len(core)):
            a=pos(core[j-1]);b=pos(core[j])
            if contains(b):continue
            ds=b-a;fac=min([(size-v)/d if d>0 else -v/d for size,v,d in zip([m.shape[1],m.shape[0]],a,ds) if d!=0])
            # Only consider the rectangle boundary in the direction of travel.
            q=a+ds*max(0,min(1,fac)-1e-7/max(1,abs(ds).max()))
            q=np.clip(q,0,[m.shape[1]-1e-8,m.shape[0]-1e-8])
            target=tuple((o+q/s)/W);tail=core[j:];break
        g=np.zeros_like(m,bool);i,j=np.clip(np.floor(pos(target)).astype(int),0,[m.shape[1]-1,m.shape[0]-1]);g[j,i]=True
        link=self.search(patch,approach[k],g)
        return approach[:k]+link+[target]+tail if link else None
    def frontier(self,small,large):
        x,y,w,h=self.bounds[small];margin=14
        x0=max(0,x-margin);y0=max(0,y-margin)
        view=self.comp[y0:min(W,y+h+margin),x0:min(W,x+w+margin)]
        if not (view==large).any():return []
        def edge(label):
            mask=np.uint8(view==label)
            yy,xx=np.nonzero(mask & (cv2.erode(mask,np.ones((3,3),np.uint8))==0))
            return np.column_stack([xx+x0,yy+y0])
        p=edge(small);q=edge(large)
        if not len(p) or not len(q):return []
        dist,idx=cKDTree(q).query(p)
        pairs=[]
        for i in np.argsort(dist):
            if dist[i]>margin:break
            if any(np.linalg.norm(p[i]-a)<3 for a,b in pairs):continue
            pairs.append((p[i],q[idx[i]]))
            if len(pairs)==5:break
        return pairs
    def boundary_approaches(self,access,towards):
        path,patch=access;m,o,s=patch;h,w=m.shape
        def pos(p):return np.floor((np.array(p)*W-o)*s).astype(int)
        k=next(i for i,p in enumerate(path) if m[tuple(pos(p)[::-1])]==0)
        _,labels=cv2.connectedComponents(np.uint8(m==0),connectivity=4)
        label=labels[tuple(pos(path[k])[::-1])]
        eligible=labels==label;eligible[1:-1,1:-1]=False
        yy,xx=np.nonzero(eligible);cells=np.column_stack((xx,yy))
        uv=(o+(cells+.5)/s)/W
        if not len(uv):return
        coarse=np.floor(uv*W).astype(int)
        valid=(coarse<W).all(axis=1)
        coarse=np.clip(coarse,0,W-1)
        valid &= self.area[self.comp[coarse[:,1],coarse[:,0]]]>=5000
        order=np.flatnonzero(valid);order=order[np.argsort(np.linalg.norm(uv[order]-towards,axis=1))]
        tried=[]
        for j in order:
            if any(np.linalg.norm(cells[j]-c)<32 for c in tried):continue
            tried.append(cells[j]);g=np.zeros_like(m,bool);g[yy[j],xx[j]]=True
            alt=self.search(patch,path[k],g)
            if alt:yield path[:k]+alt,patch
            if len(tried)>=6:return
    def enclosed(self,access,other):
        path,(m,o,s)=access
        target=(np.asarray(other)*W-o)*s
        if 0<=target[0]<m.shape[1] and 0<=target[1]<m.shape[0]:return False
        q=((np.asarray(path[-1])*W-o)*s).astype(int)
        _,labels=cv2.connectedComponents(np.uint8(m==0),connectivity=4)
        c=labels[q[1],q[0]]
        return c not in np.r_[labels[0],labels[-1],labels[:,0],labels[:,-1]]
    def middle(self,a,b,mode):
        ax,ay=uvcell(a);bx,by=uvcell(b);ca=self.comp[ay,ax];cb=self.comp[by,bx]
        if not ca or not cb or ca==cb:return None
        flip=self.area[ca]>self.area[cb]
        for left,right in self.frontier(cb,ca) if flip else self.frontier(ca,cb):
            if flip:left,right=right,left
            if mode=='Quickest' and (self.cost[left[1],left[0]]>2 or self.cost[right[1],right[0]]>2):continue
            l=tuple((left+.5)/W);r=tuple((right+.5)/W)
            mid=self.link(l,r)
            if not mid:continue
            if mode=='Quickest' and not self.road_test(mid,True):continue
            before=self.coarse(a,l,mode);after=self.coarse(r,b,mode)
            if before and after:return before+mid+after
        return None
    def route(self,a,b,mode):
        self.used={}
        if not all(np.isfinite(v) and 0<=v<1 for v in (*a,*b)):raise ValueError('UV must be in [0,1)')
        a=tuple(map(float,np.asarray(a,np.float32)));b=tuple(map(float,np.asarray(b,np.float32)))
        base_ms=0
        mpath=self.middle(a,b,mode) if np.linalg.norm((np.asarray(a)-b)*W)>16 else None
        if mpath:
            checked=self.checked(mpath,mode)
            if checked:return checked,'interior-crossing',base_ms
        if mode!='Quickest' and np.linalg.norm((np.asarray(a)-b)*W)<=16:
            for pad in (0,1):
                local=self.link(a,b,pad)
                if local:
                    checked=self.checked(local,mode)
                    if checked:return checked,'local-crossing',base_ms
                if pad==0:
                    # Enlarging a window cannot clear a solid endpoint at this resolution.
                    if any(self.tile(*(uvcell(p)//T*T))[tuple(((np.asarray(p)*W-uvcell(p)//T*T)*S).astype(int)[::-1])]!=0 for p in (a,b)):break
        aa=self.approach(a);bb=self.approach(b) if aa else None
        if not aa or not bb:return None,'blocked-or-no-local-exit',base_ms
        if self.enclosed(aa,b) or self.enclosed(bb,a):return None,'enclosed-at-resolution',base_ms
        l=aa[0][-1];r=bb[0][-1];core=self.coarse(l,r,mode);reason='access'
        if not core:
            core=self.middle(l,r,mode);reason='interior-crossing'
        if core:
            start_join=self.splice(aa,core)
            joined=start_join or (aa[0]+core)
            if joined:
                end_join=self.splice(bb,joined[::-1])
                joined=end_join or (bb[0]+joined[::-1])
                if joined:
                    checked=self.checked(joined[::-1],mode)
                    if checked:return checked,reason,base_ms
            if not start_join or not end_join:
                flip=bool(start_join);first,last=(bb,aa) if flip else (aa,bb)
                for alt in self.boundary_approaches(first,last[0][-1]):
                    retry=self.coarse(alt[0][-1],last[0][-1],mode)
                    if not retry:continue
                    h=self.splice(alt,retry) or (alt[0]+retry)
                    h=self.splice(last,h[::-1]) or (last[0]+h[::-1])
                    checked=self.checked(h if flip else h[::-1],mode)
                    if checked:return checked,'alternate-exit',base_ms
        return None,'no-verified-connection',base_ms
    def checked(self,path,mode):
        from clearance import violations
        for attempt in range(5):
            bad=violations(self,path)
            if not bad:return path
            if attempt==4:break
            pts=np.asarray(path);lengths=np.linalg.norm(np.diff(pts,axis=0)*[15216.18,15236.18],axis=1)
            cumulative=np.r_[0,np.cumsum(lengths)]
            first=None
            for i,start,end in bad:
                lower=cumulative[i]+start*lengths[i];upper=cumulative[i]+end*lengths[i]
                if first is None:first=lower
                elif lower>last+5:break
                last=upper
            if last-first>60:break
            lo=max(0,first-3);hi=min(cumulative[-1],last+3)
            def at(v):return tuple(np.interp(v,cumulative,pts[:,j]) for j in (0,1))
            replacement=self.link(at(lo),at(hi),1)
            if not replacement:break
            # Preserve the road network through an interior on-road segment.
            if mode=='Quickest':
                removed=[at(lo)]+[p for p,d in zip(path,cumulative) if lo<d<hi]+[at(hi)]
                if not self.road_test(removed,False) and not self.road_test(replacement,True):return None
            path=[p for p,d in zip(path,cumulative) if d<lo]+replacement+[p for p,d in zip(path,cumulative) if d>hi]
        return None
    def road_test(self,points,on_road):
        from clearance import cells
        for p,q in zip(points,points[1:]):
            xy=cells(np.asarray(p)*W,np.asarray(q)*W)[1]
            if np.any((self.cost[xy[:,1],xy[:,0]]<=2)!=on_road):return False
        return True
