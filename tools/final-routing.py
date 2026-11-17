from pathlib import Path
p=Path('tools/bake-bridges.py');s=p.read_text();a='keys=np.array(keys,dtype='
s=s.replace(a,'''for k,z in np.fromfile(root/'tools/bridge-ramps/joined.bin',[('key','<i4'),('z','<f4')]):
 k=int(k);z=float(z)
 if any(abs(zs[i]-z)<=20 for i in bins[k]):continue
 bins[k].append(len(keys));keys.append(k);zs.append(z)
'''+a)
s=s.replace('submetre-grid nodes','1m bridge nodes');p.write_text(s)
def edit(name,a,b):
 p=Path('source/src')/name;s=p.read_text(encoding='utf-8-sig');assert a in s;p.write_text(s.replace(a,b),encoding='utf-8-sig')
edit('SquadTools.cs','destination)>=25)) { squadTarget=', 'destination)>=25||!NavigationRouter.SameSurface(squadTarget.Centroid,squadTarget.RouteHeightCm,destination,peer.Position.Z))) { squadTarget=')
edit('SquadTools.cs','Points=new[]{destination} };engine.WpfSquadTarget', 'Points=new[]{destination},RouteHeightCm=peer.Position.Z };engine.WpfSquadTarget')
edit('RoadRouter.cs','internal int[] BridgeLayers;', 'internal int[] BridgeLayers;\n        internal bool BridgeBlocked;')
edit('GridRouter.Bridges.cs','new RoadRoute{Mode=mode,PlayerPoint=a,TargetPoint=b,Polyline=new PointF[0]', 'new RoadRoute{BridgeBlocked=true,Mode=mode,PlayerPoint=a,TargetPoint=b,Polyline=new PointF[0]')
edit('SquadRoutePlanner.cs','legs.Add(good?route.Polyline:new[]{from,to});', '''if(good)legs.Add(route.BridgeLayers==null?new[]{from}.Concat(route.Polyline).Concat(new[]{to}).ToArray():route.Polyline);
                    else legs.Add(route!=null&&route.BridgeBlocked?new PointF[0]:new[]{from,to});''')
edit('WpfFieldConsole.cs','if(State.Route!=null&&State.Route.Success&&State.Position!=null) {','if(State.Route!=null&&State.Route.Success&&State.Route.BridgeLayers==null&&State.Position!=null) {')
edit('WpfFieldConsole.cs','if(State.Target!=null)DrawLine(dc,new[]{point(path[path.Length-1])','if(State.Target!=null&&State.Route.BridgeLayers==null)DrawLine(dc,new[]{point(path[path.Length-1])')
p=Path('tools/package-ui.py');s=p.read_text(encoding='utf8').replace("if '__pycache__' in v.parts", "if p.name=='BridgeProbe.exe':continue\n        if '__pycache__' in v.parts");p.write_text(s,encoding='utf8')
