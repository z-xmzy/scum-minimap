from pathlib import Path
import csv,struct,gzip,numpy as np
root=Path('.');world=root/'package/SCUM-MiniMap/app/fine/world';W=3804
r=np.memmap(world/'roads.f32','f','r',shape=(W,W));g=np.memmap('../SCUM-MiniMap-world3-20261112/bake/ground.f32','f','r',shape=(W,W))
with gzip.open(world/'nav-world-grid.bin','rb') as f:f.read(20);cost=np.frombuffer(f.read(W*W),'u1').reshape(W,W).copy()
with gzip.open('source/resources/nav-bridges.bin','rb') as f:
 h,w,n,m=struct.unpack('<4siii',f.read(16))
 for _ in range(n):
  cell,c,t,z,rd=struct.unpack('<iBBf?',f.read(11));cost.flat[cell]=c
c=next(c for c in csv.reader(open('checks/levels.tsv',encoding='utf8'),delimiter='\t') if c[:3]==['B082X1','实测下穿去','Quickest'])
inp=(np.array(c[3:7],float).reshape(2,2)*W).astype(int);print(inp)
x,y=(np.array([.6694269,.4120662])*W).astype(int)
for j in range(y-9,y+10):
 line=''
 for i in range(x-10,x+11):
  s='R' if np.isfinite(r[j,i]) and abs(r[j,i]-g[j,i])<240 else {1:'r',2:'r',4:'.',8:'o',16:'w',255:'#'}.get(cost[j,i],'!')
  if (i,j)==tuple(inp[0]):s='A'
  if (i,j)==tuple(inp[1]):s='B'
  if (i,j)==(x,y):s='X'
  line+=s
 print(line)
