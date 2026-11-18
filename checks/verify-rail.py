"""Compare routing on the original railway arch and the two grade-matched banks."""
from pathlib import Path
from itertools import groupby
import csv, subprocess, os
import numpy as np
from scipy.spatial import cKDTree

root=Path(__file__).resolve().parents[1]
checks=root/'checks'
old=root.parent/'SCUM-MiniMap-2.4.10-map'
os.environ['TMP']=os.environ['TEMP']=str(root/'temp')
def read(path):
    with path.open(encoding='utf-8-sig',newline='') as f:
        return list(csv.reader(f,delimiter='\t'))
def save(path,rows):
    with path.open('w',encoding='utf-8-sig',newline='') as f:
        csv.writer(f,delimiter='\t').writerows(rows)
def geometry(base):
    return np.load(base/'checks/bridge-geometry.npz')
def xy(k):
    return np.c_[k%15216,k//15216]+.5
def placements(folder):
    samples=np.fromfile(folder/'cells.bin',[('k','<i4'),('z','<f4')]);off=0
    for row in read(folder/'placements.tsv'):
        n=int(row[-1]);yield row,samples[off:off+n];off+=n
    assert off==len(samples)
before,after=geometry(old),geometry(root)
names={int(r[1]):r[2] for r in read(checks/'through-expected.tsv') if r[2] in {'B107','B102','B098','B097'}}
assert len(names)==4
through=[r for r in read(checks/'through.tsv') if int(r[0][1:]) in names]
assert len(through)==32
save(checks/'rail-through.tsv',through)
via=[];mates=[]
archive_samples=list(placements(root/'tools/bridge-rail-approaches'))
for group,region in names.items():
    ids=np.flatnonzero(before['groups']==group-1);p=xy(before['keys'][ids]);mid=p.mean(axis=0)
    direction=np.linalg.eigh(np.cov(p.T))[1][:,-1]
    old_tree=cKDTree(np.c_[p,before['z'][ids]/40.])
    idx=ids[np.argmin(np.linalg.norm(p-mid,axis=1))]
    center=[*(xy(before['keys'][[idx]])[0]/15216),before['z'][idx]]
    for row in through:
        if int(row[0][1:])!=group:continue
        for label,start,end in [('入桥',[*row[3:5],row[7]],center),('离桥',center,[*row[5:7],row[8]])]:
            via.append([row[0],row[1]+label,row[2],*start[:2],*end[:2],start[2],end[2]])
    newmask=after['groups']==group-1
    after_edges=after['portals'][newmask[after['portals'][:,0]]]
    old_edges=before['portals'][before['groups'][before['portals'][:,0]]==group-1]
    new_points=xy(after['keys'][after_edges[:,0]])
    old_points=xy(before['keys'][old_edges[:,0]])
    all_new=[]
    for row,dat in archive_samples:
        v=xy(dat['k']);h=dat['z'];dist,ix=old_tree.query(np.c_[v,h/40.])
        dh=h-before['z'][ids[ix]]
        touch=(dist<=1.75)&(abs(dh)<=40)
        if len(np.unique(dat['k'][touch]))<3:continue
        along_dir=np.linalg.eigh(np.cov(v.T))[1][:,-1]
        t=(v-v.mean(0))@along_dir
        if t[touch].max()<=t.min()+2:out=along_dir
        elif t[touch].min()>=t.max()-2:out=-along_dir
        else:continue
        bank=1 if ((v.mean(0)-mid)@direction)>0 else -1
        bank_edges=(new_points-mid)@direction*bank>0
        this=new_points[bank_edges];all_new.extend(map(tuple,after_edges[bank_edges]))
        assert len(this)>0 and len(this)==sum(np.isin(after['keys'][after_edges[bank_edges,0]],dat['k']))
        along=(v-this.mean(0))@out
        assert along.max()-((this-this.mean(0))@out).min()<3.01
        gc=after_edges[bank_edges,1];land=np.c_[(gc%3804+.5)*4,(gc//3804+.5)*4]
        diff=land-this
        assert np.all((diff@out)/np.linalg.norm(diff,axis=1)>.599)
        bank_ids=after_edges[bank_edges,0];rise=after['z'][bank_ids]-after['ground'][bank_ids]
        old_count=np.count_nonzero((old_points-mid)@direction*bank>0)
        mates.append([region,group,bank,row[0].split('/')[-1],row[2],len(np.unique(dat['k'][touch])),round(float(np.max(abs(dh[touch]))),2),old_count,len(this),round(float(np.min(rise)),2),round(float(np.max(rise)),2)])
    assert len(all_new)==len(after_edges) and len(set(all_new))==len(after_edges)
assert len(mates)==8
save(checks/'rail-banks.tsv',[['区域','层','岸','关卡','原始铁轨样条','与桥拱相接的1米格数量','相接格最大绝对高差厘米','旧版入口边数','新版入口边数','入口格轨面减地形最小厘米','入口格轨面减地形最大厘米'],*mates])
save(checks/'rail-via.tsv',via)
summary=[]
for version,base in [('old',old),('new',root)]:
    app=base/'package/SCUM-MiniMap/app'
    for suite in ['through','via']:
        path=checks/f'rail-{suite}-{version}.out'
        subprocess.run([str(app/'BridgeProbe.exe'),str(app/'SkynettMiniMap.exe'),str(checks/f'rail-{suite}.tsv'),str(path)],cwd=app,check=True)
        data=read(path);assert len(data)==(32 if suite=='through' else 64)
        for row in data:
            group=int(row[0][1:])
            points=np.array([p.split(',') for p in row[-1].split(';') if p],float).reshape(-1,3)
            layers=[int(k) for k,_ in groupby(points[:,2])]
            want=[0,group,0] if suite=='through' else ([0,group] if row[1].endswith('入桥') else [group,0])
            upper=points[points[:,2]==group,:2]
            bank_distance=None
            if len(upper):
                ends=np.array(row[3:7],float).reshape(2,2)
                distances=np.linalg.norm((upper[[0,-1]]-ends)*[15216.18,15236.18],axis=1)
                bank_distance=float(distances.max() if suite=='through' else distances[0 if want[0]==0 else 1])
            local=bool(layers==want and bank_distance is not None and bank_distance<=5.02)
            if version=='new' and suite=='via':assert local,(row[:3],bank_distance,layers)
            summary.append([version,suite,names[group],row[1],row[2],row[-3],round(float(row[-2]),2),','.join(map(str,layers)),round(bank_distance,2) if bank_distance is not None else '',int(local),int(layers==want)])
save(checks/'rail-routes.tsv',[['版本','套件','区域','方向','模式','成功','距离米','层序','过渡距请求岸点米','在请求岸点换层','目标层序吻合'],*summary])
for version in ['old','new']:
    for suite in ['through','via']:
        for region in names.values():
            rs=[r for r in summary if r[:3]==[version,suite,region]]
            print(version,suite,region,sum(r[-1] for r in rs),len(rs))
