from pathlib import Path
import csv
r=Path('checks');old=Path('../SCUM-2.4.9-all-bridges')
queries=[]
for name in ['area','approach']:
 for line in (old/(name+'.tsv')).read_text(encoding='utf8').splitlines():
  c=line.split('\t')
  if c[2]=='Quickest':queries.append(c)
for name in ['through','heads']:
 for line in (r/(name+'.tsv')).read_text(encoding='utf8').splitlines():
  c=line.split('\t')
  if c[2]=='Quickest':queries.append(c)
queries.append(['A28','original acceptance','Quickest',.2640,.1254,.6748,.0414])
with (r/'voice.tsv').open('w',encoding='utf8',newline='') as f:csv.writer(f,delimiter='\t').writerows(queries)
print(len(queries))
