"""Verify use of the designated deck along each returned bank-to-bank route."""
from pathlib import Path
import collections
import csv
import sys
import numpy as np

source = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('checks')
dest = Path(sys.argv[2]) if len(sys.argv) > 2 else source/'两岸实际走层.csv'
with (source/'through-expected.tsv').open(encoding='utf-8-sig') as f:
    group = {r[0]: int(r[1]) for r in csv.reader(f, delimiter='\t')}
counts = collections.Counter()
with (source/'through.out').open(encoding='utf-8-sig') as f, dest.open('w', encoding='utf-8-sig', newline='') as out:
    w = csv.writer(out)
    w.writerow(['组','方向','模式','指定桥层','两端连线投影覆盖比例','最长连续桥段覆盖比例','结果'])
    for row in csv.reader(f, delimiter='\t'):
        name, direction, mode = row[:3]
        result, fraction, continuous = '无路线', 0., 0.
        if row[-3] == 'True':
            pts = np.array([p.split(',') for p in row[-1].split(';')], float)
            a, b = np.array(row[3:7],float).reshape(2,2)
            scale = np.array([15216.18, 15236.18])
            v = (b-a)*scale
            projection = ((pts[:,:2]-a)*scale)@v / (v@v)
            on = pts[:,2] == group[name]
            if np.any(on):
                limits = np.flatnonzero(np.diff(np.r_[False,on,False])).reshape(-1,2)
                fraction = np.ptp(projection[on])
                continuous = max(np.ptp(projection[l:r]) for l,r in limits)
            result = ('指定桥层覆盖两岸间至少一半' if continuous>=.5 else
                      '部分经过指定桥层' if np.any(on) else '全程采用其他路径')
        w.writerow([name,direction,mode,group[name],round(fraction,3),round(continuous,3),result])
        counts[result]+=1
print(dict(counts))
