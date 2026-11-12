using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ScumMiniMap {
    internal sealed class SquadRoutePlan {
        internal int Revision;
        internal PointF[][] Legs;
        internal bool[] Verified;
        internal double Distance,EstimatedSeconds;
        // B2 (navA27): distance is reported in two explicit calibres so a failed
        // leg is never presented as a real 0 m road leg.  Distance stays the legacy
        // facing total (planned + estimated straight line) for existing UI/tests.
        internal double PlannedDistance,EstimatedDistance;
        internal int UnplannedLegs;
        internal bool AllFailed;
        internal RoadRouteMode Mode;
        internal bool HasOrigin;
        internal int MissingRoads { get { return Verified.Count(v=>!v); } }
    }
    internal sealed class SquadRouteDrawing { internal string Key,Name;internal bool Shared;internal SquadRoutePlan Plan; }
    // A single bounded worker coalesces edits and abandons obsolete work between
    // legs. All road searches run away from the WPF dispatcher.
    internal sealed class SquadRoutePlanner:IDisposable {
        sealed class Request { internal int Revision;internal RoadRouteMode Mode;internal PointF? Origin;internal PointF[] Stops; }
        readonly object gate=new object();readonly Func<PointF,PointF,RoadRouteMode,Func<bool>,RoadRoute> calculate;
        static readonly SemaphoreSlim calculations=new SemaphoreSlim(2);
        int revision;bool worker,disposed,busy;Request pending;SquadRoutePlan plan;
        internal SquadRoutePlanner(Func<PointF,PointF,RoadRoute> calculate):this((a,b,mode)=>calculate(a,b)) {}
        internal SquadRoutePlanner(Func<PointF,PointF,RoadRouteMode,RoadRoute> calculate) :this((a,b,m,current)=>calculate(a,b,m)) {}
        internal SquadRoutePlanner(Func<PointF,PointF,RoadRouteMode,Func<bool>,RoadRoute> calculate) { this.calculate=calculate; }
        internal bool Busy { get { lock(gate)return busy; } }
        internal SquadRoutePlan Plan { get { lock(gate)return plan; } }
        static bool Valid(PointF p) { return !float.IsNaN(p.X)&&!float.IsNaN(p.Y)&&p.X>=0&&p.X<=1&&p.Y>=0&&p.Y<=1; }
        internal void Update(PointF? origin,PointF[] stops,RoadRouteMode mode=RoadRouteMode.Shortest) {
            if(!Enum.IsDefined(typeof(RoadRouteMode),mode))throw new ArgumentOutOfRangeException("mode");
            if(stops==null||stops.Length<1||stops.Length>16||stops.Any(p=>!Valid(p))||origin.HasValue&&!Valid(origin.Value))throw new ArgumentException();
            lock(gate) {
                if(disposed)return;pending=new Request { Revision=++revision,Mode=mode,Origin=origin,Stops=(PointF[])stops.Clone() };plan=null;busy=true;
                if(!worker) { worker=true;Task.Run((Func<Task>)Work); }
            }
        }
        bool Current(Request request) { lock(gate)return !disposed&&request.Revision==revision; }
        RoadRoute CalculateBackground(PointF from,PointF to,Request request) {
            var thread=Thread.CurrentThread;var previous=thread.Priority;
            try { thread.Priority=ThreadPriority.BelowNormal;return calculate(from,to,request.Mode,()=>Current(request)); }
            finally { thread.Priority=previous; }
        }
        async Task Work() {
            while(true) {
                Request request;lock(gate) { if(disposed||pending==null) { worker=false;return; }request=pending;pending=null; }
                await Task.Delay(150);if(!Current(request))continue;
                var legs=new List<PointF[]>();var verified=new List<bool>();double planned=0,estimated=0,seconds=0;int unplanned=0;
                PointF from=request.Origin??request.Stops[0];int first=request.Origin.HasValue?0:1;
                for(int i=first;i<request.Stops.Length;i++) {
                    if(!Current(request))break;PointF to=request.Stops[i];RoadRoute route=null;
                    await calculations.WaitAsync();
                    try { if(Current(request))route=await Task.Run(()=>CalculateBackground(from,to,request)); }catch(Exception) { }finally { calculations.Release(); }
                    if(!Current(request))break;
                    bool good=route!=null&&route.Success&&route.Polyline!=null&&route.Polyline.Length>0&&route.Polyline.All(Valid);
                    double straight=RoadRouter.DistanceMeters(from,to);
                    legs.Add(good?new[]{from}.Concat(route.Polyline).Concat(new[]{to}).ToArray():new[]{from,to});verified.Add(good);
                    if(good) {
                        double length=route.TotalDistanceMeters;
                        planned+=!double.IsNaN(length)&&!double.IsInfinity(length)&&length>=0?length:straight;
                        seconds+=route.EstimatedSeconds;
                    } else { estimated+=straight;unplanned++; }
                    from=to;
                }
                lock(gate)if(!disposed&&request.Revision==revision) { plan=new SquadRoutePlan { Revision=request.Revision,Legs=legs.ToArray(),Verified=verified.ToArray(),Distance=planned+estimated,PlannedDistance=planned,EstimatedDistance=estimated,UnplannedLegs=unplanned,AllFailed=verified.Count>0&&unplanned==verified.Count,EstimatedSeconds=seconds,Mode=request.Mode,HasOrigin=request.Origin.HasValue };busy=false; }
            }
        }
        internal void Clear() { lock(gate) { revision++;pending=null;plan=null;busy=false; } }
        public void Dispose() { lock(gate) { disposed=true;revision++;pending=null;plan=null;busy=false; } }
    }
}
