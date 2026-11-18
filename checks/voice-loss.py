import csv,numpy as np
from pathlib import Path
rows=list(csv.reader(open('checks/voice-before.tsv',encoding='utf8'),delimiter='\t'))
out=[]
for c in rows:
 if len(c)<12 or c[-5]!='True' or not c[-3] or float(c[-4])<80:continue
 p=np.array([s.split(',') for s in c[-1].split(';')],float)
 for r in c[-3].split(';'):
  xy=np.array(r.split(','),float)
  ds=np.linalg.norm((p[:,:2]-xy)*15216,axis=1);i=ds.argmin()
  if ds[i]<8:out.append([*c[:3],round(float(c[-4]),1),r,round(ds[i],1),int(p[i,2]),c[-2]])
with Path('checks/voice-loss.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['ID','case','mode','metres','old_cue_uv','distance','layer','new_cues']);w.writerows(out)
print('candidate cues',len(out))
for row in out[:10]:print(row)
