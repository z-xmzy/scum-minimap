from pathlib import Path
import csv,struct,json
import numpy as np
root=Path(__file__).resolve().parents[1];b=root/'bake';out=b/'audit';out.mkdir(exist_ok=True)
def rows(path,sep=','):
    with path.open(encoding='utf-8-sig') as f:return list(csv.reader(f,delimiter=sep))
pairs=rows(root/'checks/road100.csv');failed=[r for r in csv.DictReader((root/'checks/road100-summary.csv').open(encoding='utf-8-sig')) if r['world_success']=='False']
models=rows(b/'models.tsv','\t');radii=np.zeros(max(int(r[0]) for r in models)+1)
for r in models:
    v=np.array(r[5:11],float)
    radii[int(r[0])]=max(radii[int(r[0])],np.linalg.norm(v[:3])+np.linalg.norm(v[3:])/2)
# Use the actual rendered vertex extent for structures already corrected in world2.
for r in rows(b/'detailed-models.tsv','\t'):
    pts=np.fromfile(b/'detailed'/(r[0]+'.bin'),'<f4').reshape(-1,3)
    radii[int(r[0])]=np.sqrt((pts**2).sum(axis=1).max())
dtype=np.dtype([('id','<i4'),('p','<f4',3),('q','<f4',4),('s','<f4',3)])
data=np.concatenate([np.fromfile(p,dtype) for p in sorted((b/'tiles').glob('*.bin'))]);rad=radii[data['id']]*np.abs(data['s']).max(axis=1)+250
regs=[];ids=set()
for f in failed:
    pp=pairs[int(f['pair'])];assert pp[0]==f['pair']
    for side in range(2):
        uv=np.array(pp[2+2*side:4+2*side],dtype=np.float32).astype(float)
        c=(uv*3804).astype(int);key=pp[0]+('B' if side else 'A');n=8 if key in ('49B','75B') else 24;x0,y0=(c-n//2).tolist()
        # Include 48 metres on all sides for a local exit from a coarse pocket.
        X=617718-(x0+n/2)*1521618/3804;Y=618618-(y0+n/2)*1523618/3804
        take=(np.abs(data['p'][:,0]-X)<=n/2*1521618/3804+rad)&(np.abs(data['p'][:,1]-Y)<=n/2*1523618/3804+rad)
        d=data[take];key=pp[0]+('B' if side else 'A');d.tofile(out/(key+'.instances'))
        ids.update(d['id'].tolist());regs.append(dict(key=key,u=uv[0],v=uv[1],x=x0,y=y0,n=n,s=64 if n==8 else 16))
(out/'regions.json').write_text(json.dumps(regs));(out/'model-ids.txt').write_text('\n'.join(map(str,sorted(ids))))
print('regions',len(regs),'models',len(ids),'placements',sum(p.stat().st_size//44 for p in out.glob('*.instances')))

(out/'areas.tsv').write_text('\n'.join('\t'.join(map(str,(r['key'],r['x'],r['y'],r['n']*r['s'],r['s']))) for r in regs))
