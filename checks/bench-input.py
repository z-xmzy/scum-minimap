from pathlib import Path
import csv
src=Path('../SCUM-2.4.9-all-bridges')
a=list(csv.reader((src/'area.tsv').open(encoding='utf8'),delimiter='\t'))
p=list(csv.reader((src/'approach.tsv').open(encoding='utf8'),delimiter='\t'))
selected=[r for r in a if r[0].startswith('B') and r[1]=='桥下直行去' and r[2]=='Quickest']
selected += [r for r in p if r[1]=='两端接入去' and r[2]=='Quickest'][::3]
selected += [r for r in a if r[0].startswith('H') and r[1]=='全长去' and r[2]=='Quickest'][::3]
with Path('checks/bench.tsv').open('w',encoding='utf8',newline='') as f:csv.writer(f,delimiter='\t').writerows(selected)
print(len(selected))
