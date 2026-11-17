from pathlib import Path
import csv,collections,numpy as np
folder=Path('checks');old=Path('../SCUM-2.4.9-all-bridges')
z=np.load(folder/'bridge-geometry.npz');keys,g,portals=[z[k] for k in ('keys','groups','portals')];xy=np.c_[keys%15216+.5,keys//15216+.5];g=g+1
components={r['ID']:r for r in csv.DictReader((old/'组件逐件清单.csv').open(encoding='utf-8-sig'))}
groupmap=collections.defaultdict(list)
for p,reg,group in csv.reader((folder/'deck-coverage.tsv').open(encoding='utf8'),delimiter='\t'):
 if group.isdigit():groupmap[int(group)].append((p,reg))
stats=collections.Counter();out=[]
for group,list_ in groupmap.items():
 types={components[p]['类别'] for p,_ in list_};fields={components[p]['模型'] for p,_ in list_};regions={r for _,r in list_}
 pts=xy[g==group];u=pts.mean(0);axis=np.linalg.eigh(np.cov(pts.T))[1][:,-1];t=(pts-u)@axis;ln=np.ptp(t)
 gates=portals[g[portals[:,0]]==group,0];proj=(xy[gates]-u)@axis;lo=float((proj.min()-t.min())/ln) if len(proj) else 1.;hi=float((t.max()-proj.max())/ln) if len(proj) else 1.
 result='两端均有接入口' if max(lo,hi)<=.24 else '端部需核实'
 stats[(';'.join(sorted(types)),result)]+=1
 out.append([group,';'.join(regions),';'.join(sorted(types)),round(ln,1),len(gates),round(lo,2),round(hi,2),result,';'.join(sorted(fields))])
 if (types&{'桥面','坝顶','浮桥'}) and result=='端部需核实' and ln>=12:print(group,regions,round(ln),len(gates),round(lo,2),round(hi,2),fields)
with (folder/'两岸接入口清单.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['层','区域','类别','跨度米','接入口边数','首端无入口比例','末端无入口比例','结论','模型']);w.writerows(out)
print(stats)
