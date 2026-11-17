from pathlib import Path
import numpy as np,csv,collections
rt=Path(__file__).resolve().parents[1];folder=rt/'checks'
def rows(f):return list(csv.reader((folder/f).open(encoding='utf-8-sig'),delimiter='\t'))
expected={(r[0],r[1]):(int(r[2]),np.array(r[3:],float)) for r in rows('levels-expected.tsv')}
groups={r[0]:int(r[2]) for r in rows('deck-coverage.tsv') if r[2].isdigit()}
for f in ['levels','decks']:
 failed=[];counts=collections.Counter(); meta=[]
 for c in rows(f+'.out'):
  if len(c)<12:continue
  test=c[1]
  up=test.startswith('实测桥面');group,center=expected[(c[0],'上' if up else '下')] if f=='levels' else (groups[c[0]],None)
  ok=c[-3]=='True';pts=np.array([p.split(',') for p in c[-1].split(';')],dtype=float) if ok else None
  span=np.linalg.norm(np.array(c[3:5],float)*15216-np.array(c[5:7],float)*15216)
  deviation=None
  if ok and center is not None:
   pa=pts[:-1,:2]*15216;v=np.diff(pts[:,:2]*15216,axis=0)
   t=np.clip((v*(center-pa)).sum(axis=1)/np.maximum(1.e-15,(v*v).sum(axis=1)),0,1)
   deviation=np.linalg.norm(pa+t[:,None]*v-center,axis=1).min(initial=1e6)
  flags=[]
  if not ok:flags.append('无路线')
  if ok and not np.all(pts[:,2]==group):flags.append('异层')
  if ok and float(c[-2])>1.5*span+8:flags.append('绕行')
  if ok and deviation is not None and deviation>12:flags.append('偏离交叉点')
  counts['、'.join(flags) or '通过']+=1
  meta.append([c[0],c[1],c[2],group,ok,round(float(c[-2]),2),round(span,2),round(deviation,1) if deviation is not None else '',','.join(flags) or '通过'])
  if flags and c[2]=='Quickest' and c[1].endswith('去'):failed.append((c[0],test,flags,group,sorted(set(pts[:,2].astype(int))) if ok else [],round(deviation,1) if deviation is not None else '',round(span,1),c[-2]))
 with (folder/(f+'-verification.csv')).open('w',encoding='utf-8-sig',newline='') as out:
  w=csv.writer(out);w.writerow(['编号','测试','模式','预期层','返回','路线米','直线米','距交叉点米','结论']);w.writerows(meta)
 print(f,len(meta),counts)
 for issue in failed[:25]:print(issue)
