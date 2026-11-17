exec(open('checks/handdiag.py',encoding='utf8').read().split('for c in rows:')[0])
import zlib
world=Path('package/SCUM-MiniMap/app/fine/world');water=np.memmap(world/'water.f32','f','r',shape=ground.shape);road=np.memmap(world/'roads.f32','f','r',shape=ground.shape)
mark=np.frombuffer(zlib.decompress((world/'handroads.z').read_bytes()),'u1').reshape(ground.shape)
for c in rows:
 if c[-3]!='False' or c[0][0]!='H' or '去' not in c[1] or c[2]!='Quickest':continue
 print(c[0],end=' ')
 for u,v in [c[3:5],c[5:7]]:
  x,y=(np.array([float(u),float(v)])*3804).astype(int)
  print('mark',mark[y,x],'water',bool(water[y,x]>ground[y,x]+15 or ground[y,x]<0),'road',np.isfinite(road[y,x]),end=' | ')
 print()