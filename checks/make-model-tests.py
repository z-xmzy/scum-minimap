from pathlib import Path
import numpy as np, struct,csv,collections
from scipy.spatial import cKDTree
rt=Path(__file__).resolve().parents[1];folder=rt/'checks';mesh=rt/'tools/bridge-meshes';old=rt.parent/'SCUM-2.4.9-all-bridges'
d=np.load(folder/'bridge-geometry.npz');key,z,g,dem=(d[n] for n in ['keys','z','groups','ground']);xy=np.c_[key%15216+.5,key//15216+.5];tree=cKDTree(xy);sizes=np.bincount(g);modes=['Quickest','Shortest','PavedOnly','Walking']
world=rt/'package/SCUM-MiniMap/app/fine/world'
road=np.memmap(world/'roads.f32','f','r',shape=(3804,3804)); ground=np.memmap(rt.parent/'SCUM-MiniMap-world3-20261112/bake/ground.f32','f','r',shape=road.shape)
def probe(out,name,test,a,b,modes=modes):
 for way,(p,q) in [('去',(a,b)),('回',(b,a))]:
  for mode in modes:out.append([name,test+way,mode,p[0],p[1],q[0],q[1],p[2],q[2]])
def pt(i):return (*list(xy[i]/15216),float(z[i]))
def ends(ids):
 v=xy[ids];cent=v.mean(0);w=np.linalg.eigh(np.cov(v.T))[1][:,-1];t=(v-cent)@w;lo,hi=np.quantile(t,[.04,.96]);pa=cent+w*lo;pb=cent+w*hi
 return [int(ids[np.argmin(np.linalg.norm(v-wanted,axis=1))]) for wanted in (pa,pb)]
def write(name,arr):
 with (folder/(name+'.tsv')).open('w',encoding='utf8',newline='') as f:csv.writer(f,delimiter='\t').writerows(arr)
rows=[];index=collections.defaultdict(list)
for i,k in enumerate(key):index[int(k)].append(i)
catalog=list(csv.DictReader((old/'组件逐件清单.csv').open(encoding='utf-8-sig')));lookup=collections.defaultdict(list)
for r in catalog:lookup[(r['关卡'],r['对象'],r['模型'])].append(r)
cells=np.fromfile(mesh/'cells.bin',[('k','<i4'),('h','<f4')]);off=0;coverage=[]
for r in csv.reader((mesh/'placements.tsv').open(encoding='utf-8-sig'),delimiter='\t'):
 length=int(r[-1]);sub=cells[off:off+length];off+=length
 identity=(r[0].split('/')[-1],r[1],r[3].split('.')[-1]);found=lookup.get(identity)
 if not found:continue
 base=found.pop(0)
 if base['类别']=='附属或代理' or base['测试']!='已发起测试':continue
 ids=set()
 for k,h in sub:
  i=min(index.get(int(k),[]),key=lambda i:abs(z[i]-h),default=-1)
  if i>=0 and abs(z[i]-h)<=20 and z[i]>=dem[i]-60:ids.add(i)
 tally=collections.Counter(g[i] for i in ids if sizes[g[i]]>=4)
 chosen=tally.most_common(1)
 if not chosen:coverage.append([base['ID'],base['区域'],'无可独立取点的连续上表面']);continue
 group,n=chosen[0];sel=np.array([i for i in ids if g[i]==group]); 
 if len(sel)<2:coverage.append([base['ID'],base['区域'],'仅单点']);continue
 a,b=ends(sel)
 if a==b:coverage.append([base['ID'],base['区域'],'端点重合']);continue
 probe(rows,base['ID'],'实测桥面',pt(a),pt(b));coverage.append([base['ID'],base['区域'],int(group)+1])
write('decks',rows);write('deck-coverage',coverage)
area=list(csv.reader((old/'area.tsv').open(encoding='utf8'),delimiter='\t')); checks=[];meta=[]
for r in csv.DictReader((old/'上下层交叉清单.csv').open(encoding='utf-8-sig')):
 name=r['ID'];p=np.array([float(r['U']),float(r['V'])])*15216
 near=np.array(tree.query_ball_point(p,24));above=near[(z[near]>dem[near]+180)&(sizes[g[near]]>=4)]
 inside=above[np.linalg.norm(xy[above]-p,axis=1)<5];choices=collections.Counter(g[inside])
 if choices:
  group=choices.most_common(1)[0][0];sel=above[g[above]==group]; a,b=ends(sel);probe(checks,name,'实测桥面',pt(a),pt(b));meta.append([name,'上',int(group)+1,*p])
 for c in area:
  if c[:3]!=[name,'桥下直行去','Quickest']:continue
  a,b=map(lambda v:np.array(v,dtype=float),[c[3:5],c[5:7]])
  def low(uv):
   x,y=(uv*3804).astype(int);h=road[y,x];return (*uv,float(h if np.isfinite(h) else ground[y,x]))
  probe(checks,name,'实测下穿',low(a),low(b));meta.append([name,'下',0,*p]);break
write('levels',checks);write('levels-expected',meta)
print('physical',len(coverage),'calls',len(rows),'levels',len(checks))
