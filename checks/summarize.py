from pathlib import Path
from statistics import median
from collections import defaultdict
import re
p=Path('checks/final-timing.txt')
groups=defaultdict(list)
for line in p.read_text(encoding='utf-8-sig').splitlines():
 if re.fullmatch(r'\d+-[78]-(idle|immediate)',line):
  _,version,mode=line.split('-')
 else:
  hit=re.fullmatch(r'(\w+) sync=(\d+) maxGap=(\d+) renderEvents=(\d+) terrain=(\d+) annotations=(\d+)',line)
  if hit:
   metric,_,gap,_,terr,anno=hit.groups()
   groups[mode,version,metric].append(int(gap))
for k,v in sorted(groups.items()):
 print(' '.join(k),v,median(v))