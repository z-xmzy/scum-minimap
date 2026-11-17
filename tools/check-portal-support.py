from pathlib import Path
p=Path('tools/bake-bridges.py');s=p.read_text()
needle='portal=[];endcap=np.zeros(N,bool);axdir={}'
assert needle in s
s=s.replace(needle,'''# Read the supporting surface along each connection, including gaps between
# its endpoints. A similar height at the far bank cannot bridge an air gap.
road_samples=defaultdict(list)
for k,z in np.fromfile(root/'tools/bridge-underroads/cells.bin',[('key','<i4'),('z','<f4')]):
 road_samples[int(k)].append(float(z))
def supported_entry(node,ex,ey,target_height):
 x=float(xs[node]);y=float(ys[node]);z=float(zs[node])
 distance=math.hypot(ex-x,ey-y)
 if abs(z-target_height)>45*distance:return False
 t=np.linspace(0,1,max(3,int(math.ceil(distance*2))+1))
 px=x+(ex-x)*t;py=y+(ey-y)*t;expected=z+(target_height-z)*t
 terrain_z=ground(px,py)
 for xx,yy,want,h in zip(px[1:-1],py[1:-1],expected[1:-1],terrain_z[1:-1]):
  cell=int(round(xx))+int(round(yy))*S
  choices=[float(zs[j]) for j in bins.get(cell,()) if groups[j]==groups[node]]
  choices.extend(road_samples.get(cell,()))
  a=int((xx+.5)//4);b=int((yy+.5)//4)
  if h>0 and h>=water[b,a]-15:choices.append(float(h))
  if not choices or min(abs(v-want) for v in choices)>55:return False
 return True
portal=[];endcap=np.zeros(N,bool);axdir={}''')
s=s.replace('edges[i].append(gc+N);portal.append((i,gc))','''if not supported_entry(i,ex,ey,info[2]):continue
   edges[i].append(gc+N);portal.append((i,gc))''')
p.write_text(s)
