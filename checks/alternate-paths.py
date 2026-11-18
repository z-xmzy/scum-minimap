"""Inspect lateral deck transitions on the two via-point requests with extra portals."""
import csv,itertools,numpy as np
from pathlib import Path
ch=Path('checks')
geo=np.load(ch/'bridge-geometry.npz')
bykey=dict(( (int(kk),int(g)+1), (h,z)) for kk,g,h,z in zip(geo['keys'],geo['groups'],geo['z'],geo['ground']))
rows=list(csv.reader((ch/'via-deck.out').open(encoding='utf8'),delimiter='\t'))
for name,suffix in [('岸3256','去下桥'),('岸3858','去上桥')]:
    c=next(row for row in rows if row[0]==name and row[1].endswith(suffix) and row[2]=='Quickest')
    p=np.array([i.split(',') for i in c[-1].split(';')],float)
    dist=np.r_[0,np.linalg.norm(np.diff(p[:,:2],axis=0)*[15216.18,15236.18],axis=1)].cumsum()
    print(name)
    for layer,ii in itertools.groupby(range(len(p)),lambda i:p[i,2]):
        ix=list(ii)
        h=[]
        if layer:
            for j in [ix[0],ix[-1]]:
                x,y=(p[j,:2]*15216).astype(int)
                a,b=bykey[x+y*15216,layer];h.append((round(float(a)/100,2),round(float(a-b)/100,2)))
        print(int(layer),round(dist[ix[0]],2),round(dist[ix[-1]],2),'model and clearance',h)
