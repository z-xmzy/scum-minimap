from pathlib import Path
root=Path('source/src')
def change(n,a,b):
 p=root/n;s=p.read_text(encoding='utf-8-sig');assert a in s;p.write_text(s.replace(a,b),encoding='utf-8-sig')
n='GridRouter.Bridges.cs'
change(n,'internal int[] GroupSizes;', '''internal int[] GroupSizes;
            internal readonly Dictionary<int,int> MarkedDeck=new Dictionary<int,int>();''')
change(n,'return best;', '''int surveyed;
                if(best==cell && !g.Road && MarkedDeck.TryGetValue(cell,out surveyed)){
                    double error=height.HasValue?Math.Abs(height.Value-Height(surveyed)):0;
                    if(!height.HasValue || (error<200 && error+70<groundError))return surveyed;
                }
                return best;''')
change(n,'bridges=b;', '''int marked=r.ReadInt32();
                if(marked<0||marked>ng)throw new InvalidDataException("Bridge target count");
                for(int i=0;i<marked;i++)b.MarkedDeck.Add(r.ReadInt32(),r.ReadInt32());
                bridges=b;''')
change(n,'var path=guided?original.Polyline:new[]{start,end};','''var path=guided?original.Polyline:new[]{start,end};
            if(guided && path[0]!=start)path=new[]{start}.Concat(path).ToArray();
            if(guided && path[path.Length-1]!=end)path=path.Concat(new[]{end}).ToArray();''')
change(n,'output.Add(from,sg);','''if(sg==0||RoadRouter.DistanceMeters(from,b.Point(s))<=1.2)output.Add(from,sg);''')
change(n,'output.Add(to,tg);return true;', '''if(tg==0||RoadRouter.DistanceMeters(to,b.Point(t))<=1.2)output.Add(to,tg);
            return true;''')
change(n,'route.BridgeLayers=result.Layers.ToArray();return route;', 'route.BridgeLayers=result.Layers.ToArray();route.PlayerPoint=start;route.TargetPoint=end;return route;')
n='GridRouter.Local.cs'
change(n,'if(group!=0 && group==SurfaceGroup(b,endHeight)){','''double span=RoadRouter.DistanceMeters(a,b);
                bool localUnder=group==0 && span<=120 && TouchesBridge(a,b);
                if((group!=0||localUnder) && group==SurfaceGroup(b,endHeight)){''')
change(n,'route.BridgeLayers=same.Layers.ToArray();return route;', '''if(group!=0||route.TotalDistanceMeters<=span*1.45+8){
                                route.BridgeLayers=same.Layers.ToArray();route.PlayerPoint=a;route.TargetPoint=b;return route;
                            }''')
n='SquadRoutePlanner.cs'
change(n,'legs.Add(good?new[]{from}.Concat(route.Polyline).Concat(new[]{to}).ToArray():new[]{from,to});','''legs.Add(good?route.Polyline:new[]{from,to});''')
