from pathlib import Path
src=Path('source/src')
def edit(name,a,b):
 p=src/name;s=p.read_text(encoding='utf-8-sig');assert a in s,(name,a);p.write_text(s.replace(a,b),encoding='utf-8-sig')
n='GridRouter.Bridges.cs'
edit(n,'internal BridgeDeck[] Deck;', 'internal BridgeDeck[] Deck;\n            internal int[] GroupSizes;')
edit(n,'Endpoint(PointF p,double? height)', 'Endpoint(PointF p,double? height,bool deckOnly=false)')
edit(n,'double z=Height(id);','''int size=GroupSizes[Group(id)];
                        if(size<4)continue;
                        double z=Height(id);''')
edit(n,'if(error>=groundError && g.Cost<16)continue;', 'if(error>320 || (error>=groundError && g.Cost<16))continue;')
edit(n,'if(g.Road&&z>g.Height+120)continue;', 'if(!deckOnly && g.Road&&z>g.Height+120)continue;')
edit(n,'dist-=Math.Min(100.0,Math.Max(0.0,(z-g.Height)*.001));','''if(z<g.Height-70)continue;
                            // Continuous driving surfaces take precedence over small pieces of overhead steel.
                            dist+=12.0/Math.Sqrt(size);''')
edit(n,'b.Deck=new BridgeDeck[nd];','b.Deck=new BridgeDeck[nd];b.GroupSizes=new int[nd+1];')
edit(n,'b.Add(b.ByCell,pixel', 'b.GroupSizes[group]++;b.Add(b.ByCell,pixel')
edit(n,'b.Endpoint(point,1000000)','b.Endpoint(point,null,true)')
edit(n,'current,double? hz1,double? hz2,BridgePath output)', 'current,double? hz1,double? hz2,BridgePath output,bool sameLevel=false)')
edit(n,'BridgeSearch(s,t,mode,pad,current)', 'BridgeSearch(s,t,mode,pad,current,sameLevel)')
edit(n,'int pad,Func<bool> current)', 'int pad,Func<bool> current,bool sameLevel)')
edit(n,'if(closed.Contains(next))return;', 'if((sameLevel && b.Group(next)!=b.Group(start))||closed.Contains(next))return;')
edit(n,'FineResult(result.Points,mode,cues)', 'FineResult(result.Points,mode,cues,result.Layers)')
n='GridRouter.Local.cs'
edit(n,'var first=FindUnlayered(a,b,mode,current);','''if(bridges!=null && Finite(a.X)&&Finite(a.Y)&&Finite(b.X)&&Finite(b.Y)&&Enum.IsDefined(typeof(RoadRouteMode),mode)){
                int group=SurfaceGroup(a,startHeight);
                if(group!=0 && group==SurfaceGroup(b,endHeight)){
                    var same=new BridgePath();
                    if(BridgeSection(a,b,mode,current,startHeight,endHeight,same,true)){
                        lock(searchLock){
                            var route=FineResult(same.Points,mode,new HashSet<PointF>(),same.Layers);
                            route.BridgeLayers=same.Layers.ToArray();return route;
                        }
                    }
                }
            }
            var first=FindUnlayered(a,b,mode,current);''')
edit(n,'mode,HashSet<PointF> cues) {','mode,HashSet<PointF> cues,List<int> layers=null) {')
edit(n,'byte cost=TrailCost(overlay,id,fine.Data[id]);','''byte cost=TrailCost(overlay,id,fine.Data[id]);
                        if(layers!=null){
                            BridgeGround ground;
                            if(layers[i-1]>0||layers[i]>0)cost=CostPaved;
                            else if(bridges.Ground.TryGetValue(id,out ground))cost=ground.Cost;
                        }''')
n='SquadRoutePlanner.cs'
edit(n,'internal PointF? Origin;', 'internal double? OriginHeight;internal PointF? Origin;')
edit(n,'readonly Func<PointF,PointF,RoadRouteMode,Func<bool>,RoadRoute> calculate;', 'readonly Func<PointF,PointF,RoadRouteMode,Func<bool>,double?,double?,RoadRoute> calculate;')
edit(n,'RoadRoute> calculate) { this.calculate=calculate; }','''RoadRoute> calculate):this((a,b,m,current,z,endZ)=>calculate(a,b,m,current)) {}
        internal SquadRoutePlanner(Func<PointF,PointF,RoadRouteMode,Func<bool>,double?,double?,RoadRoute> calculate) { this.calculate=calculate; }''')
edit(n,'if(!Enum.IsDefined', '''UpdateAtHeight(origin,stops,mode,null);
        }
        internal void UpdateAtHeight(PointF? origin,PointF[] stops,RoadRouteMode mode,double? height) {
            if(!Enum.IsDefined''')
edit(n,'Origin=origin,Stops=', 'Origin=origin,OriginHeight=height,Stops=')
edit(n,'to,Request request)', 'to,Request request,double? height)')
edit(n,'()=>Current(request));','()=>Current(request),height,null);')
edit(n,'CalculateBackground(from,to,request)', 'CalculateBackground(from,to,request,i==0?request.OriginHeight:null)')
n='SquadRouteControls.cs'
edit(n,'NavigationRouter.FindCurrentRoute','NavigationRouter.FindAtHeight')
edit(n,'DrawingPoint? PlanningOrigin() { var state=engine.WpfSnapshot(false);return', 'DrawingPoint? PlanningOrigin(out double? z) { var state=engine.WpfSnapshot(false);z=state.Position==null?(double?)null:state.Position.Z;return')
edit(n,'var origin=PlanningOrigin();', 'double? z;var origin=PlanningOrigin(out z);string layer=origin.HasValue?NavigationRouter.SurfaceGroup(origin.Value,z).ToString():"";')
for a in ('own','remaining'):
 edit(n,'+StopSignature('+a+')','+layer+":"+StopSignature('+a+')')
 edit(n,'.Update(origin,StopPoints('+a+'),engine.WpfRouteMode)', '.UpdateAtHeight(origin,StopPoints('+a+'),engine.WpfRouteMode,z)')
edit(n,'+StopSignature(route);', '+(from.HasValue?NavigationRouter.SurfaceGroup(from.Value,peer.Position.Z):0)+":"+StopSignature(route);')
edit(n,'.Update(from,StopPoints(route),engine.WpfRouteMode)', '.UpdateAtHeight(from,StopPoints(route),engine.WpfRouteMode,peer==null?(double?)null:peer.Position.Z)')
