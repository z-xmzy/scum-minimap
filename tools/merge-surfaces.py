from pathlib import Path
p=Path('tools/bake-bridges.py');s=p.read_text().replace("assert off==len(buf)","""assert off==len(buf)
# A bridge end piece can be a U-shaped mesh around a separate asphalt insert.
# Join only surveyed road samples that meet the deck in XYZ; a road underneath
# with several metres of clearance cannot contribute to this upper surface.
from scipy.spatial import cKDTree
original=np.asarray(keys);xyz=np.c_[original%S,original//S,np.asarray(zs)/35]
road=np.fromfile(root/'tools/bridge-underroads/cells.bin',[('key','<i4'),('z','<f4')])
rk=road['key'];rh=road['z'];distance,idx=cKDTree(xyz).query(np.c_[rk%S,rk//S,rh/35])
for k,z in zip(rk[(distance<=6.5)&(abs(rh-np.asarray(zs)[idx])<=38)],rh[(distance<=6.5)&(abs(rh-np.asarray(zs)[idx])<=38)]):
 k=int(k);z=float(z)
 if any(abs(zs[i]-z)<=20 for i in bins[k]):continue
 bins[k].append(len(keys));keys.append(k);zs.append(z)""")
p.write_text(s)
p=Path('source/src/GridRouter.Local.cs');s=p.read_text(encoding='utf-8-sig').replace('if((group!=0||localUnder) && group==SurfaceGroup(b,endHeight)){', '''int endGroup=SurfaceGroup(b,endHeight);
                bool localChange=group!=endGroup && span<=140 && bridges.Ground.ContainsKey(bridges.Cell(a)) && bridges.Ground.ContainsKey(bridges.Cell(b));
                if(localChange||((group!=0||localUnder) && group==endGroup)){''').replace('startHeight,endHeight,same,true)', 'startHeight,endHeight,same,!localChange)').replace('if(group!=0||route.TotalDistanceMeters<=span*1.45+8)', 'if(localChange||group!=0||route.TotalDistanceMeters<=span*1.45+8)')
p.write_text(s,encoding='utf-8-sig')
