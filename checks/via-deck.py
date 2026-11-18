"""Query the normal router from each bank to a waypoint on the original bridge deck."""
from pathlib import Path
import collections
import csv
import numpy as np
import sys
p=Path('checks')
def rows(name):
    with (p/name).open(encoding='utf-8-sig', newline='') as f:
        yield from csv.reader(f, delimiter='\t')
groups={r[0]:int(r[1]) for r in rows('through-expected.tsv')}
if '--verify' in sys.argv:
    results=collections.Counter()
    with (p/'经桥面途经点复核.csv').open('w', encoding='utf-8-sig', newline='') as f:
        w=csv.writer(f)
        w.writerow(['桥组','方向和段','模式','指定层','实际起始层','实际结束层','结果'])
        for r in rows('via-deck.out'):
            g=groups[r[0]]
            enter=r[1].endswith('上桥')
            status='无路线'
            a=b=''
            if r[-3]=='True':
                layers=[int(pt.split(',')[-1]) for pt in r[-1].split(';')]
                a,b=layers[0],layers[-1]
                expected=[0,g] if enter else [g,0]
                collapsed=[v for i,v in enumerate(layers) if not i or v!=layers[i-1]]
                status='按指定桥头连续换层' if collapsed==expected else '其他走层'
            w.writerow([*r[:3],g,a,b,status])
            results[status]+=1
    print(dict(results))
else:
    geo=np.load(p/'bridge-geometry.npz')
    k,z,g=geo['keys'],geo['z'],geo['groups']+1
    xy=np.c_[k%15216+.5,k//15216+.5]/15216
    with (p/'via-deck.tsv').open('w',encoding='utf8',newline='') as f:
        w=csv.writer(f, delimiter='\t')
        for r in rows('through.tsv'):
            a=np.array([r[3],r[4],r[7]],float)
            b=np.array([r[5],r[6],r[8]],float)
            ids=np.flatnonzero(g==groups[r[0]])
            mid=(a[:2]+b[:2])/2
            ix=ids[np.argmin(np.linalg.norm(xy[ids]-mid,axis=1))]
            stop=np.r_[xy[ix],z[ix]]
            for name,start,end in [('上桥',a,stop),('下桥',stop,b)]:
                w.writerow([r[0],r[1]+name,r[2],*start[:2],*end[:2],start[2],end[2]])
