import runpy, math, heapq
from pathlib import Path
m=runpy.run_path(str(Path(__file__).with_name('verify-routes.py')))
w=m['W'];ok=(m['grid']<16)&(m['terrain']<4)
s=(int(.235*w),int(.4006*w));t=(int(.2382*w),int(.4235*w))
q=[(0,0,s)];dist={s:0}
xm,ym=15216.18/w,15236.18/w
steps=[(dy,dx,math.hypot(dx*xm,dy*ym)) for dy in (-1,0,1) for dx in (-1,0,1) if dx or dy]
while q:
    _,d,k=heapq.heappop(q)
    if d!=dist[k]:continue
    if k==t:print('Shortest legal cell-centre path, metres:',d);break
    y,x=k
    for dy,dx,c in steps:
        ny,nx=y+dy,x+dx
        if not(0<=ny<w and 0<=nx<w and ok[ny,nx] and ok[ny,x] and ok[y,nx]):continue
        n=ny,nx;nd=d+c
        if nd>=dist.get(n,float('inf')):continue
        dist[n]=nd
        heapq.heappush(q,(nd+math.hypot((t[0]-ny)*ym,(t[1]-nx)*xm),nd,n))
