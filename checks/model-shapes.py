from pathlib import Path
import csv,numpy as np,collections
folder=Path('tools/model-review')
index=list(csv.reader((folder/'index.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
tri=np.memmap(folder/'triangles.bin','<f4','r').reshape(-1,3,3)
out=[]
for r in index:
 if r[4]!='True':continue
 t=np.array(tri[int(r[5]):int(r[5])+int(r[6])],float)
 v=t[:,1]-t[:,0];w=t[:,2]-t[:,0];normal=np.cross(v,w);nz=normal[:,2];up=(nz<-.001)&((normal[:,0]**2+normal[:,1]**2)<=.20*nz*nz)
 area=-nz[up].sum()/20000
 pos=t.reshape(-1,3);xy=pos[:,:2]/100;axes=np.linalg.eigh(np.cov(xy.T))[1]
 dims=np.ptp((xy-xy.mean(0))@axes,axis=0)
 heights=np.ptp(pos[:,2])/100
 name=r[3].split('.')[-1]
 out.append([r[0].split('/')[-1],r[1],r[2],name,int(r[6]),int(up.sum()),round(area,3),round(dims.min(),3),round(dims.max(),3),round(heights,3)])
with Path('checks/原始模型逐件形状.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['关卡','对象','组件','模型','三角形','平缓朝上三角形','水平面积平方米','宽米','长米','高米']);w.writerows(out)
for name in sorted(set(r[3] for r in out)):
 rows=[r for r in out if r[3]==name]
 print(name,'placements',len(rows),'first',rows[0][4:])
