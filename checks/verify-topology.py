from pathlib import Path
import gzip,struct,csv,collections,numpy as np,math,json
p=Path('source/resources/nav-bridges.bin')
with gzip.open(p) as f:
 magic,w,ng,nd=struct.unpack('<4siii',f.read(16));ground={}
 for i in range(ng):
  cell,c,t,z,r=struct.unpack('<iBBf?',f.read(11));ground[cell]=(c,t,z,r)
 nodes=[];edges=[]
 for i in range(nd):
  key,z,group,n=struct.unpack('<ifii',f.read(16));nodes.append((key,z,group));edges.append(np.frombuffer(f.read(n*4),'<i4').copy())
 n=struct.unpack('<i',f.read(4))[0];marked=np.frombuffer(f.read(n*8),'<i4').reshape(-1,2)
 n=struct.unpack('<i',f.read(4))[0];under=np.frombuffer(f.read(n*8),'<i4').reshape(-1,2)
 assert f.read()==b''
nodes=np.array(nodes);S=w*4;N=w*w;xy=np.c_[(nodes[:,0]%S+.5)*15216.18/S,(nodes[:,0]//S+.5)*15236.18/S]
bad=[];gatecount=0;maxrise=0;maxgrade=0
for i,adj in enumerate(edges):
 for next_ in adj:
  if next_>=N:
   j=int(next_-N)
   if nodes[i,2]!=nodes[j,2] or N+i not in edges[j]:bad.append(('deck',i,j))
  else:
   gatecount+=1
   g=ground.get(int(next_))
   if g is None or g[0]>=16 or g[1]>=4:bad.append(('ground',i,int(next_)));continue
   q=np.array([(next_%w+.5)*15216.18/w,(next_//w+.5)*15236.18/w])
   span=np.linalg.norm(q-xy[i]);rise=abs(nodes[i,1]-g[2])/100
   maxrise=max(rise,maxrise);maxgrade=max(maxgrade,rise/max(span,.01))
   if span>5.02 or rise>(.45*span+.01):bad.append(('portal',i,int(next_)))
info=dict(nodes=nd,ground_cells=ng,cross_layer_links=gatecount,under_road_links=len(under),handroad_target_matches=len(marked),invalid_edges=len(bad),max_portal_rise_m=round(maxrise,3),max_portal_grade_ratio=round(maxgrade,3),examples=bad[:5])
Path('checks/topology-verification.json').write_text(json.dumps(info,indent=2))
print(json.dumps(info,indent=2))
assert not bad
