import csv
import cv2
import numpy as np
import runpy
import subprocess
from pathlib import Path
M=runpy.run_path(str(Path(__file__).with_name('verify-routes.py')))
C=M['CHECK']
with (C/'road100.csv').open(encoding='utf-8-sig') as f:
    pairs=list(csv.reader(f))
assert len(pairs)==100
for edition,app in [('world',M['ROOT']/'app'),('navA28',M['OLD'])]:
    file=C/('road100-'+edition+'.tsv')
    subprocess.run([C/'Probe.exe','stock',C/'road100.csv',file],cwd=app,check=True)
def records(edition):
    with (C/('road100-'+edition+'.tsv')).open(encoding='utf-8-sig') as f:
        return list(csv.reader(f,delimiter='\t'))
# Diagonal travel requires both side cells free, so four-neighbour components
# describe fine-grid reachability even when the two endpoint cells are open.
_,labels=cv2.connectedComponents(((M['grid']<16)&(M['terrain']<4)).astype(np.uint8),connectivity=4)
def endpoint(u,v):
    w=M['W'];x=int(w*float(u));y=int(w*float(v))
    g=M['grid'][y,x];h=M['terrain'][y,x]
    return ('solid' if g==255 else 'water' if g==16 else 'steep' if h>=4 else 'open'),labels[y,x]
with (C/'road100-summary.csv').open('w',newline='',encoding='utf-8-sig') as f:
    wr=csv.writer(f);wr.writerow(['pair','start','end','connected','old_success','world_success','world_metres','water_or_solid_m','offroad_40deg_m'])
    for pair,old,new in zip(pairs,records('navA28'),records('world'),strict=True):
        (a,ca),(b,cb)=endpoint(*pair[2:4]),endpoint(*pair[4:6]);fields=M['score'](new)
        assert old[0]==new[0]==pair[0]
        assert (new[2]=='True')==(a==b=='open' and ca==cb) and fields[4]==0 and fields[5]==0
        wr.writerow([pair[0],a,b,ca==cb and ca!=0,old[2],new[2],*fields[3:6]])
