import collections,json,sys
from analyze import BASE,OUT,load,np
sys.path.insert(0,str(BASE/'bunker-extraction-20261031'))
from floorplans import read_mesh,transform_vertices,ray_heights
from bunker_geometry import placements,structural
rs=sorted([r for r in load() if r['bunker']=='D_1_Abandoned'],key=lambda r:r['rank'])
data=json.loads((BASE/'bunker-extraction-20261031/output/bunkers/D_1_Abandoned.json').read_text())
items=data['placements']
items += [p for p in placements(json.loads((BASE/'bunker-extraction-20261031/output/scenes/D_1_TV_Base_Entrance.json').read_text())) if structural(p['mesh']) and '/Buildings/Bunker/' not in p['mesh']]
points=[]
for r in rs:
    if r['rank'] not in (3,4,5,6,7,8,9,10,11):continue
    mask=np.isfinite(r['grid']);left=set(zip(*np.where(mask)));components=[]
    while left:
        seed=left.pop();stack=[seed];component=[seed]
        while stack:
            y,x=stack.pop()
            for dy,dx in ((0,1),(0,-1),(1,0),(-1,0)):
                neighbor=(y+dy,x+dx)
                if neighbor in left:left.remove(neighbor);stack.append(neighbor);component.append(neighbor)
        if len(component)*r['step']**2/10000>=1:
            c=np.array(component);center=np.mean(c,axis=0);y,x=c[np.argmin(np.sum((c-center)**2,axis=1))]
            points.append(dict(rank=r['rank'],areaM2=round(len(component)*r['step']**2/10000,2),p=[r['x1']-(int(x)+.5)*r['step'],r['y1']-(int(y)+.5)*r['step'],float(r['grid'][y,x])],sources=[]))
for p in items:
    v,f=read_mesh(p['mesh']);v=transform_vertices(v,p['transform']);lo=v.min(axis=0);hi=v.max(axis=0)
    matched=[q for q in points if np.all(np.array(q['p'])>=lo-1)&np.all(np.array(q['p'])<=hi+1)]
    if not matched:continue
    t=v[f]
    for q in matched:
        x,y,z=q['p'];zs=ray_heights(t,x,y)
        if np.any(abs(zs-z)<2):q['sources'].append(p['mesh'].split('/')[-1])
(OUT/'d1-components-sources.json').write_text(json.dumps(points,indent=2),encoding='utf-8')
for q in points:
    if q['rank'] in (3,4):print(q)
