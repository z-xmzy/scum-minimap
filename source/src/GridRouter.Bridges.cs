using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ScumMiniMap {
    internal sealed partial class GridRouter {
        struct BridgeGround { internal byte Cost,Class;internal float Height;internal bool Road; }
        struct BridgeDeck { internal int Pixel,Group;internal float Height;internal int[] Neighbours; }
        sealed class BridgeMap {
            internal int Width,GroundCount,Scale;
            internal readonly Dictionary<int,BridgeGround> Ground=new Dictionary<int,BridgeGround>();
            internal readonly Dictionary<int,List<int>> ByCell=new Dictionary<int,List<int>>(),Entrances=new Dictionary<int,List<int>>();
            internal readonly HashSet<int> OpenGroups=new HashSet<int>();
            internal readonly Dictionary<int,List<int>> UnderRoads=new Dictionary<int,List<int>>();
            internal readonly HashSet<int> LowerRoadPixels=new HashSet<int>();
            internal BridgeDeck[] Deck;
            internal int[] GroupSizes;
            internal readonly Dictionary<int,int> MarkedDeck=new Dictionary<int,int>();
            internal void Add(Dictionary<int,List<int>> table,int key,int value) {
                List<int> row;if(!table.TryGetValue(key,out row))table.Add(key,row=new List<int>());row.Add(value);
            }
            internal int Cell(PointF p) {return ClampCell((int)(p.X*Width),Width)+Width*ClampCell((int)(p.Y*Width),Width);}
            internal PointF Point(int id) {
                bool ground=id<GroundCount;int w=ground?Width:Scale,cell=ground?id:Deck[id-GroundCount].Pixel;
                return new PointF((float)((cell%w+.5)/w),(float)((cell/w+.5)/w));
            }
            internal float Height(int id) {
                if(id>=GroundCount)return Deck[id-GroundCount].Height;
                BridgeGround g;return Ground.TryGetValue(id,out g)?g.Height:float.NaN;
            }
            internal int Group(int id) {return id<GroundCount?0:Deck[id-GroundCount].Group;}
            internal int Endpoint(PointF p,double? height,bool deckOnly=false) {
                int cell=Cell(p);BridgeGround g;if(!Ground.TryGetValue(cell,out g))return cell;
                int pixel=ClampCell((int)(p.X*Scale),Scale)+Scale*ClampCell((int)(p.Y*Scale),Scale);
                bool lowerRoad=g.Road||(!height.HasValue&&!deckOnly&&g.Cost<16&&LowerRoadPixels.Contains(pixel));
                double groundError=height.HasValue?Math.Abs(height.Value-g.Height):0;
                int best=cell;double score=double.MaxValue;
                int x=cell%Width,y=cell/Width;
                for(int j=y-1;j<=y+1;j++)for(int i=x-1;i<=x+1;i++){
                    List<int> nodes;if(!ByCell.TryGetValue(i+j*Width,out nodes))continue;
                    foreach(int id in nodes) {
                        var q=Point(id);double dx=(q.X-p.X)*Scale,dy=(q.Y-p.Y)*Scale,dist=dx*dx+dy*dy;
                        // A point on the deck must fall within its sampled footprint.
                        if(dist>1.30)continue;
                        int size=GroupSizes[Group(id)];
                        if(size<4)continue;
                        double z=Height(id);
                        if(height.HasValue){
                            double error=Math.Abs(z-height.Value);
                            if(error>320 || (error>=groundError && g.Cost<16))continue;
                            // Position Z may be above the tyres or feet. Prefer a supporting floor
                            // below the position over a nearly equidistant ceiling above it.
                            dist+=(error+2*Math.Max(0,z-height.Value-35))*.08;
                        }else{
                            if(!deckOnly && lowerRoad&&z>g.Height+120)continue;
                            // Prefer the surveyed outer surface when picking an isolated deck in plan view.
                            if(z<g.Height-70)continue;
                            // Continuous driving surfaces take precedence over small pieces of overhead steel.
                            dist+=12.0/Math.Sqrt(size);
                        }
                        if(dist<score){best=id;score=dist;}
                    }
                }
                int surveyed;
                if(best==cell && !lowerRoad && MarkedDeck.TryGetValue(cell,out surveyed)){
                    double error=height.HasValue?Math.Abs(height.Value-Height(surveyed)):0;
                    if(!height.HasValue || (error<200 && error+70<groundError))return surveyed;
                }
                return best;
            }
        }
        BridgeMap bridges;
        void LoadBridgeMap() {
            using(var r=OpenWorldLayer("nav-bridges.bin")){
                uint header=r.ReadUInt32();
                if(header!=0x31475242 && header!=0x32475242)throw new InvalidDataException("Bridge topology header");
                var b=new BridgeMap();b.Width=r.ReadInt32();if(b.Width!=levels[LevelFine].Width)throw new InvalidDataException("Bridge map resolution");
                b.GroundCount=b.Width*b.Width;b.Scale=b.Width*4;int ng=r.ReadInt32(),nd=r.ReadInt32();
                if(ng<1||ng>b.GroundCount||nd<1||nd>2000000)throw new InvalidDataException("Bridge topology dimensions");
                for(int i=0;i<ng;i++){int gc=r.ReadInt32();b.Ground.Add(gc,new BridgeGround{Cost=r.ReadByte(),Class=r.ReadByte(),Height=r.ReadSingle(),Road=r.ReadBoolean()});}
                b.Deck=new BridgeDeck[nd];b.GroupSizes=new int[nd+1];
                for(int i=0;i<nd;i++){
                    int pixel=r.ReadInt32();float z=r.ReadSingle();int group=r.ReadInt32(),n=r.ReadInt32();if(n<0||n>200)throw new InvalidDataException("Bridge adjacency");
                    var adj=new int[n];int id=b.GroundCount+i;
                    for(int k=0;k<n;k++){int to=adj[k]=r.ReadInt32();if(to<0||to>=b.GroundCount+nd)throw new InvalidDataException("Bridge neighbour");if(to<b.GroundCount){b.Add(b.Entrances,to,id);b.OpenGroups.Add(group);}}
                    b.Deck[i]=new BridgeDeck{Pixel=pixel,Height=z,Group=group,Neighbours=adj};
                    b.GroupSizes[group]++;b.Add(b.ByCell,pixel%b.Scale/4+pixel/b.Scale/4*b.Width,id);
                }
                int marked=r.ReadInt32();
                if(marked<0||marked>ng)throw new InvalidDataException("Bridge target count");
                for(int i=0;i<marked;i++)b.MarkedDeck.Add(r.ReadInt32(),r.ReadInt32());
                int under=r.ReadInt32();if(under<0||under>ng*40)throw new InvalidDataException("Bridge underpass edges");
                for(int i=0;i<under;i++){int a=r.ReadInt32(),c=r.ReadInt32();b.Add(b.UnderRoads,a,c);b.Add(b.UnderRoads,c,a);}
                if(header==0x32475242){
                    int count=r.ReadInt32();
                    if(count<0||count>ng*16)throw new InvalidDataException("Lower road sample count");
                    for(int i=0;i<count;i++){
                        int pixel=r.ReadInt32();
                        if(pixel<0||pixel>=b.Scale*b.Scale)throw new InvalidDataException("Lower road pixel");
                        b.LowerRoadPixels.Add(pixel);
                    }
                }
                bridges=b;
            }
        }
        internal int SurfaceGroup(PointF p,double? z){var b=bridges;return b==null?0:b.Group(b.Endpoint(p,z));}
        internal double[] BridgeHeights(PointF point) {
            var b=bridges;if(b==null)return new double[0];int cell=b.Cell(point);
            BridgeGround g;if(!b.Ground.TryGetValue(cell,out g))return new double[0];
            int id=b.Endpoint(point,null,true);if(id==cell||b.Height(id)-g.Height<180||g.Cost>=16)return new double[0];
            return new double[]{g.Height,b.Height(id)};
        }
        sealed class BridgePath {
            internal readonly List<PointF> Points=new List<PointF>();
            internal readonly List<int> Layers=new List<int>();
            internal void Add(PointF p,int layer){int n=Points.Count;if(n>0&&Points[n-1]==p&&Layers[n-1]==layer)return;Points.Add(p);Layers.Add(layer);}
        }
        const double BacktrackStubMeters=8.0,HookStubMeters=2.5;
        static readonly double BacktrackCosine=Math.Cos(150.0*Math.PI/180.0),HookCosine=Math.Cos(120.0*Math.PI/180.0);
        // Splicing a road route onto bridge topology joins sampled road points to deck and ground
        // cell centres, which can sit a few metres behind the route. The result is a short
        // out-and-back step. Drawn with a wide pen it shows as a long spike beside the bridge, and
        // voice guidance could read it as a turn, so such reversals are removed. A vertex whose
        // layer differs from both neighbours is a surface change and is always kept.
        internal static void RemoveBridgeBacktracks(List<PointF> points,List<int> layers) {
            if(points==null||layers==null||points.Count!=layers.Count)return;
            for(int i=1;i+1<points.Count;) {
                if(layers[i]!=layers[i-1]&&layers[i]!=layers[i+1]){i++;continue;}
                PointF a=points[i-1],b=points[i],c=points[i+1];
                double ux=(b.X-a.X)*MapScaleX,uy=(b.Y-a.Y)*MapScaleY,vx=(c.X-b.X)*MapScaleX,vy=(c.Y-b.Y)*MapScaleY;
                double lu=Math.Sqrt(ux*ux+uy*uy),lv=Math.Sqrt(vx*vx+vy*vy);
                double stub=Math.Min(lu,lv),cosine=lu<1e-6||lv<1e-6?1:(ux*vx+uy*vy)/(lu*lv);
                // Long reversals up to 8 m, or sharp hooks around a step of a couple of metres.
                bool reversal=stub<=BacktrackStubMeters&&cosine<=BacktrackCosine,hook=stub<=HookStubMeters&&cosine<=HookCosine;
                if(lu<1e-6||lv<1e-6||!(reversal||hook)){i++;continue;}
                points.RemoveAt(i);layers.RemoveAt(i);
                // The joined segment may form a new reversal with the previous vertex.
                if(i>1)i--;
            }
        }
        RoadRoute BridgeFailure(PointF a,PointF b,RoadRouteMode mode){return new RoadRoute{BridgeBlocked=true,Mode=mode,PlayerPoint=a,TargetPoint=b,Polyline=new PointF[0],JunctionIndices=new int[0]};}
        bool TouchesBridge(PointF a,PointF z) {
            var b=bridges;int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(z.X-a.X),Math.Abs(z.Y-a.Y))*b.Width*2));
            for(int i=0;i<=steps;i++)if(b.Ground.ContainsKey(b.Cell(Along(a,z,(double)i/steps))))return true;
            return false;
        }
        static PointF Along(PointF a,PointF z,double t){return new PointF((float)(a.X+(z.X-a.X)*t),(float)(a.Y+(z.Y-a.Y)*t));}
        RoadRoute ApplyBridgeLayers(RoadRoute original,PointF start,PointF end,RoadRouteMode mode,Func<bool> current,double? fromHeight,double? toHeight) {
            var b=bridges;if(b==null)return original;
            bool guided=original.Success&&original.Polyline!=null&&original.Polyline.Length>0;
            var path=guided?original.Polyline:new[]{start,end};
            if(guided && path[0]!=start)path=new[]{start}.Concat(path).ToArray();
            if(guided && path[path.Length-1]!=end)path=path.Concat(new[]{end}).ToArray();
            if(!guided && !(b.Ground.ContainsKey(b.Cell(start))||b.Ground.ContainsKey(b.Cell(end))) && RoadRouter.DistanceMeters(start,end)>1500)return original;
            if(!b.Ground.ContainsKey(b.Cell(start))&&!b.Ground.ContainsKey(b.Cell(end))&&!Enumerable.Range(1,path.Length-1).Any(i=>TouchesBridge(path[i-1],path[i])))return original;
            var result=new BridgePath();var cues=new HashSet<PointF>();
            if(guided&&original.JunctionIndices!=null)foreach(int i in original.JunctionIndices)if(i>=0&&i<original.Polyline.Length&&!b.Ground.ContainsKey(b.Cell(original.Polyline[i])))cues.Add(original.Polyline[i]);
            var fail=BridgeFailure(start,end,mode);
            if(!guided){
                if(!BridgeSection(start,end,mode,current,fromHeight,toHeight,result))return fail;
            }else{
                bool inside=b.Ground.ContainsKey(b.Cell(start));PointF entry=start,last=start;
                if(!inside)result.Add(start,0);
                for(int i=1;i<path.Length;i++){
                    var a=path[i-1];var z=path[i];int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(z.X-a.X),Math.Abs(z.Y-a.Y))*b.Width*2));
                    for(int j=1;j<=steps;j++){
                        var q=Along(a,z,(double)j/steps);bool hit=b.Ground.ContainsKey(b.Cell(q));
                        if(hit&&!inside){entry=last;result.Add(last,0);}
                        if(!hit&&inside&&!BridgeSection(entry,q,mode,current,entry==start?fromHeight:null,q==end?toHeight:null,result))return fail;
                        inside=hit;last=q;
                    }
                    if(!inside)result.Add(z,0);
                }
                if(inside&&!BridgeSection(entry,end,mode,current,entry==start?fromHeight:null,toHeight,result))return fail;
            }
            if(result.Points.Count==0||(!guided&&!result.Layers.Any(v=>v>0)&&!b.Ground.ContainsKey(b.Cell(start))&&!b.Ground.ContainsKey(b.Cell(end))))return fail;
            lock(searchLock){
                var route=FineResult(result.Points,mode,cues,result.Layers);route.BridgeLayers=result.Layers.ToArray();route.PlayerPoint=start;route.TargetPoint=end;return route;
            }
        }
        bool BridgeSection(PointF from,PointF to,RoadRouteMode mode,Func<bool> current,double? hz1,double? hz2,BridgePath output,bool sameLevel=false){
            var b=bridges;int s=b.Endpoint(from,hz1),t=b.Endpoint(to,hz2);
            int sg=b.Group(s),tg=b.Group(t);
            if(sg!=tg&&((sg!=0&&!b.OpenGroups.Contains(sg))||(tg!=0&&!b.OpenGroups.Contains(tg))))return false;
            List<int> path=null;
            foreach(int pad in new[]{24,96,384}){
                if(current!=null&&!current())return false;
                path=BridgeSearch(s,t,mode,pad,current,sameLevel);if(path!=null)break;
            }
            if(path==null)return false;
            if(sg==0||RoadRouter.DistanceMeters(from,b.Point(s))<=1.2)output.Add(from,sg);
            for(int i=0;i<path.Count;i++){
                int id=path[i];if(i>0&&i+1<path.Count&&b.Group(id)==b.Group(path[i-1])&&b.Group(id)==b.Group(path[i+1])){
                    var p=b.Point(path[i-1]);var q=b.Point(id);var r=b.Point(path[i+1]);
                    double cross=(q.X-p.X)*(r.Y-q.Y)-(q.Y-p.Y)*(r.X-q.X);
                    if(Math.Abs(cross)<1e-11&&(q.X-p.X)*(r.X-q.X)+(q.Y-p.Y)*(r.Y-q.Y)>0)continue;
                }
                output.Add(b.Point(id),b.Group(id));
            }
            if(tg==0||RoadRouter.DistanceMeters(to,b.Point(t))<=1.2)output.Add(to,tg);
            return true;
        }
        byte GroundCost(int cell,out byte terrain){
            BridgeGround g;if(bridges.Ground.TryGetValue(cell,out g)){terrain=g.Class;return g.Cost;}
            terrain=terrainFine[cell];return levels[LevelFine].Data[cell];
        }
        List<int> BridgeSearch(int start,int end,RoadRouteMode mode,int pad,Func<bool> current,bool sameLevel){
            var b=bridges;PointF sp=b.Point(start),ep=b.Point(end);int w=b.Width;
            int x0=Math.Max(0,(int)(Math.Min(sp.X,ep.X)*w)-pad),x1=Math.Min(w-1,(int)(Math.Max(sp.X,ep.X)*w)+pad);
            int y0=Math.Max(0,(int)(Math.Min(sp.Y,ep.Y)*w)-pad),y1=Math.Min(w-1,(int)(Math.Max(sp.Y,ep.Y)*w)+pad);
            byte tr;bool startAccess=start<b.GroundCount&&GroundCost(start,out tr)>2,endAccess=end<b.GroundCount&&GroundCost(end,out tr)>2;
            Func<int,byte,byte,bool> allowed=(node,c,cl)=>{
                if(c==0||c>=16||(mode!=RoadRouteMode.Walking&&cl>=4))return false;
                if(mode!=RoadRouteMode.Quickest||c<=2)return true;
                var p=b.Point(node);return (startAccess&&RoadRouter.DistanceMeters(p,sp)<=48)||(endAccess&&RoadRouter.DistanceMeters(p,ep)<=48);
            };
            byte costStart=start<b.GroundCount?GroundCost(start,out tr):(byte)1;
            if(start<b.GroundCount&&!allowed(start,costStart,terrainAt(start)))return null;
            var parent=new Dictionary<int,int>();var score=new Dictionary<int,double>();var closed=new HashSet<int>();var heap=new MinHeap();
            heap.Push(0,0,start);score[start]=0;int loops=0;
            double minFactor=mode==RoadRouteMode.Quickest?1/MaxSpeedFor(mode):mode==RoadRouteMode.Walking?WalkingFactor:1;
            while(heap.Count>0){
                float priority;int id=heap.Pop(out priority);if(!closed.Add(id))continue;
                if(id==end){var list=new List<int>{id};while(id!=start){id=parent[id];list.Add(id);}list.Reverse();return list;}
                if((++loops&1023)==0 && ((current!=null&&!current())||loops>=300000))return null;
                var p=b.Point(id);double curr=score[id];
                Action<int> visit=next=>{
                    if((sameLevel && b.Group(next)!=b.Group(start))||closed.Contains(next))return;
                    var q=b.Point(next);int xx=(int)(q.X*w),yy=(int)(q.Y*w);if(xx<x0||xx>x1||yy<y0||yy>y1)return;
                    byte cls=0;byte cost=next<b.GroundCount?GroundCost(next,out cls):CostPaved;
                    if(!allowed(next,cost,cls))return;
                    double length=RoadRouter.DistanceMeters(p,q);
                    double factor=StepFactor(cost,mode)*TerrainCost(mode,cls);
                    if(mode==RoadRouteMode.Quickest&&cost>2)factor=4*minFactor;
                    double trial=curr+length*factor;double old;if(score.TryGetValue(next,out old)&&trial>=old)return;
                    score[next]=trial;parent[next]=id;float h=(float)(RoadRouter.DistanceMeters(q,ep)*minFactor);heap.Push((float)trial+h,h,next);
                };
                if(id>=b.GroundCount){foreach(var next in b.Deck[id-b.GroundCount].Neighbours)visit(next);continue;}
                int x=id%w,y=id/w;
                for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++){
                    if((i==0&&j==0)||x+i<x0||x+i>x1||y+j<y0||y+j>y1)continue;
                    if(i!=0&&j!=0){byte ca,cb;int na=id+i,nb=id+j*w;byte va=GroundCost(na,out ca),vb=GroundCost(nb,out cb);if(!allowed(na,va,ca)||!allowed(nb,vb,cb))continue;}
                    visit(id+i+j*w);
                }
                List<int> fineRoads;if(b.UnderRoads.TryGetValue(id,out fineRoads))foreach(int next in fineRoads)visit(next);
                List<int> gates;if(b.Entrances.TryGetValue(id,out gates))foreach(int next in gates)visit(next);
            }
            return null;
        }

        // Traverse actual routing edges so a road above the player cannot add a junction arm.
        bool IsBridgeRoadCell(int cell) {
            byte terrain;
            byte cost = GroundCost(cell, out terrain);
            return cost > CostUndefined && cost <= CostDirt && terrain < 4;
        }

        IEnumerable<int> BridgeRoadNeighbours(int node) {
            var map = bridges;
            if (node >= map.GroundCount) {
                foreach (int next in map.Deck[node - map.GroundCount].Neighbours) {
                    if (next >= map.GroundCount || IsBridgeRoadCell(next))
                        yield return next;
                }
                yield break;
            }
            int x = node % map.Width, y = node / map.Width;
            for (int dy = -1; dy <= 1; dy++) {
                for (int dx = -1; dx <= 1; dx++) {
                    if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= map.Width ||
                        y + dy < 0 || y + dy >= map.Width) continue;
                    int next = node + dx + dy * map.Width;
                    if (!IsBridgeRoadCell(next)) continue;
                    if (dx != 0 && dy != 0 &&
                        (!IsBridgeRoadCell(node + dx) || !IsBridgeRoadCell(node + dy * map.Width))) continue;
                    yield return next;
                }
            }
            List<int> links;
            if (map.UnderRoads.TryGetValue(node, out links))
                foreach (int next in links) if (IsBridgeRoadCell(next)) yield return next;
            if (map.Entrances.TryGetValue(node, out links))
                foreach (int next in links) yield return next;
        }

        bool UsesBridgeTopology(PointF point, int layer) {
            return bridges != null && (layer > 0 || bridges.Ground.ContainsKey(bridges.Cell(point)));
        }

        int BridgeJunctionArms(PointF point, int layer, bool exact, Dictionary<int, int> cache) {
            var map = bridges;
            int cell = map.Cell(point), center = -1;
            int radius = exact ? 0 : 2;
            double nearest = double.MaxValue;
            for (int dy = -radius; dy <= radius; dy++) {
                for (int dx = -radius; dx <= radius; dx++) {
                    int x = cell % map.Width + dx, y = cell / map.Width + dy;
                    if (x < 0 || y < 0 || x >= map.Width || y >= map.Width) continue;
                    int candidate = x + y * map.Width;
                    if (layer == 0) {
                        if (!IsBridgeRoadCell(candidate)) continue;
                        double distance = RoadRouter.DistanceMeters(point, map.Point(candidate));
                        if (distance < nearest) { center = candidate; nearest = distance; }
                    } else {
                        List<int> nodes;
                        if (!map.ByCell.TryGetValue(candidate, out nodes)) continue;
                        foreach (int id in nodes) {
                            if (map.Group(id) != layer) continue;
                            double distance = RoadRouter.DistanceMeters(point, map.Point(id));
                            if (distance < nearest) { center = id; nearest = distance; }
                        }
                    }
                }
            }
            if (center < 0 || (exact && layer > 0 && nearest > 1.5)) return -1;
            int answer;
            if (cache.TryGetValue(center, out answer)) return answer;
            PointF origin = map.Point(center);
            var adjacency = new Dictionary<int, List<int>>();
            var squaredDistance = new Dictionary<int, double>();
            var queue = new Queue<int>();
            adjacency.Add(center, new List<int>());
            queue.Enqueue(center);
            while (queue.Count > 0) {
                int node = queue.Dequeue();
                var current = map.Point(node);
                double ox = (current.X - origin.X) * 15216.18;
                double oy = (current.Y - origin.Y) * 15236.18;
                squaredDistance[node] = ox * ox + oy * oy;
                foreach (int next in BridgeRoadNeighbours(node)) {
                    var position = map.Point(next);
                    double x = (position.X - origin.X) * 15216.18;
                    double y = (position.Y - origin.Y) * 15236.18;
                    if (Math.Abs(x) > 56 || Math.Abs(y) > 56) continue;
                    adjacency[node].Add(next);
                    if (adjacency.ContainsKey(next)) continue;
                    adjacency.Add(next, new List<int>());
                    queue.Enqueue(next);
                }
            }
            var seen = new HashSet<int>();
            int arms = 0;
            foreach (int start in adjacency.Keys) {
                if (squaredDistance[start] <= 16 * 16 || !seen.Add(start)) continue;
                int count = 0;
                bool touchesRing = false;
                queue.Enqueue(start);
                while (queue.Count > 0) {
                    int node = queue.Dequeue();
                    count++;
                    if (squaredDistance[node] <= 24 * 24) touchesRing = true;
                    foreach (int next in adjacency[node]) {
                        if (squaredDistance[next] > 16 * 16 && seen.Add(next)) queue.Enqueue(next);
                    }
                }
                if (count >= 4 && touchesRing && ++arms >= 3) break;
            }
            cache[center] = arms;
            return arms;
        }

        static int RouteLayerAtArc(double[] distances, List<int> layers, double arc) {
            if (layers == null) return 0;
            int index = Array.BinarySearch(distances, arc);
            if (index >= 0) return layers[index];
            index = ~index;
            if (index == 0) return layers[0];
            if (index >= layers.Count) return layers[layers.Count - 1];
            return arc - distances[index - 1] <= distances[index] - arc ? layers[index - 1] : layers[index];
        }
        byte terrainAt(int node){byte c;GroundCost(node,out c);return c;}
    }
}
