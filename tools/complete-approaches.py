from pathlib import Path
p=Path('source/src/GridRouter.Local.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('if(bridges!=null && Finite(a.X)&&Finite(a.Y)&&Finite(b.X)&&Finite(b.Y)&&Enum.IsDefined(typeof(RoadRouteMode),mode)){','''bool valid=Finite(a.X)&&Finite(a.Y)&&Finite(b.X)&&Finite(b.Y)&&
                a.X>=0&&a.Y>=0&&b.X>=0&&b.Y>=0&&a.X<1&&a.Y<1&&b.X<1&&b.Y<1&&Enum.IsDefined(typeof(RoadRouteMode),mode);
            if(bridges!=null && valid){''')
needle='''            var first=FindUnlayered(a,b,mode,current);'''
replacement='''            // A short crossing can start and end on terrain while requiring a deck in between.
            // Search the connected surfaces together before trusting the projected route at either end.
            if(bridges!=null && valid && RoadRouter.DistanceMeters(a,b)<=800 &&
                bridges.Ground.ContainsKey(bridges.Cell(a)) && bridges.Ground.ContainsKey(bridges.Cell(b))){
                var crossing=new BridgePath();
                if(BridgeSection(a,b,mode,current,startHeight,endHeight,crossing)){
                    lock(searchLock){
                        var route=FineResult(crossing.Points,mode,new HashSet<PointF>(),crossing.Layers);
                        route.BridgeLayers=crossing.Layers.ToArray();route.PlayerPoint=a;route.TargetPoint=b;
                        return route;
                    }
                }
            }
            var first=FindUnlayered(a,b,mode,current);'''
assert needle in s;s=s.replace(needle,replacement)
s=s.replace('if(bridges==null||!Finite(a.X)||!Finite(a.Y)||!Finite(b.X)||!Finite(b.Y)||!Enum.IsDefined(typeof(RoadRouteMode),mode))return first;', 'if(bridges==null||!valid)return first;')
p.write_text(s,encoding='utf-8-sig')
p=Path('source/src/GridRouter.Bridges.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('i>=0&&i<path.Length&&!b.Ground.ContainsKey(b.Cell(path[i])))cues.Add(path[i]);', 'i>=0&&i<original.Polyline.Length&&!b.Ground.ContainsKey(b.Cell(original.Polyline[i])))cues.Add(original.Polyline[i]);')
p.write_text(s,encoding='utf-8-sig')
