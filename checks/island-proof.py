"""Independently identify local sea-level islands on the original 1.5 m Landscape."""
from pathlib import Path
import numpy as np,cv2,csv
root=Path(__file__).resolve().parents[1]
dem=np.memmap(root/'bake/height/tmp/work.f32','<f4','r',shape=(10161,10161))
pairs=list(csv.reader((root/'checks/road100.csv').open()))
with (root/'checks/islands.tsv').open('w',encoding='utf-8') as f:
    for pair,side in ((27,0),(42,1)):
        u,v=map(float,pairs[pair][2+2*side:4+2*side]);x=round((14.82+u*15216.18)/1.5);y=round((5.82+v*15236.18)/1.5)
        x0=max(0,x-1300);y0=max(0,y-1300);dry=np.uint8(dem[y0:y+1301,x0:x+1301]>0)
        _,label,stats,_=cv2.connectedComponentsWithStats(dry,connectivity=8)
        c=label[y-y0,x-x0];edge=np.unique(np.r_[label[0],label[-1],label[:,0],label[:,-1]])
        assert c>0 and c not in edge
        other=(label!=0)&(label!=c)
        assert other.any()
        gap=cv2.distanceTransform(np.uint8(~other),cv2.DIST_L2,cv2.DIST_MASK_PRECISE)[label==c].min()*1.5
        print(pair,side,'dry samples',stats[c,4],'nearest other dry terrain centres',gap,file=f,sep='\t')
