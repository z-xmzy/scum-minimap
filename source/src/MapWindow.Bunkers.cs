using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        Task<BunkerAtlas> bunkerLoad;
        BunkerTracker bunkerTracker;
        string bunkerError;
        BunkerFloor displayedBunker;
        string bunkerManualView;
        float bunkerMiniZoom=1,bunkerFullZoom=1;
        PointF bunkerPan;
        bool bunkerFollow=true;
        readonly Dictionary<string,Task<BitmapSource>> bunkerImages=new Dictionary<string,Task<BitmapSource>>();
        readonly Queue<string> bunkerImageOrder=new Queue<string>();
        internal bool InBunker { get { return bunkerTracker!=null&&bunkerTracker.Current!=null&&mapSidecarPaths.MapId=="default"; } }
        void BeginBunkers() {
            // In-memory hosts (the Add-Type based canonical stability check) have no
            // assembly location, so there are no bunker assets to resolve; leave the
            // atlas unloaded instead of failing window construction on an empty path.
            string location=typeof(MapWindow).Assembly.Location;
            if(string.IsNullOrEmpty(location))return;
            string folder=Path.Combine(Path.GetDirectoryName(location),"assets","bunkers");
            bunkerLoad=Task.Run(()=>BunkerAtlas.Load(folder));
        }
        void ObserveBunker() {
            if(bunkerLoad==null||!bunkerLoad.IsCompleted||position==null)return;
            if(bunkerTracker==null) {
                if(bunkerLoad.IsFaulted) {
                    // Log the load failure once: a missing assets\bunkers folder otherwise only shows as a status suffix.
                    if(bunkerError==null)Program.LogException("BunkerAtlas.Load",bunkerLoad.Exception.GetBaseException());
                    bunkerError=Localization.Get("BunkerUnavailable");return;
                }
                if(bunkerLoad.IsCanceled)return;
                bunkerTracker=new BunkerTracker(bunkerLoad.Result.Floors);
            }
            bunkerTracker.Observe(position.X,position.Y,position.Z,updated);
        }
        BitmapSource BunkerImage(BunkerFloor floor) {
            Task<BitmapSource> task;
            if(!bunkerImages.TryGetValue(floor.Id,out task)) {
                // Only four decoded floors are retained. Loading and hashing never run on the UI thread.
                while(bunkerImageOrder.Count>=4)bunkerImages.Remove(bunkerImageOrder.Dequeue());
                task=Task.Run<BitmapSource>(()=> {
                    byte[] bytes=File.ReadAllBytes(floor.ImagePath);
                    using(var sha=SHA256.Create())if(!sha.ComputeHash(bytes).SequenceEqual(floor.ImageHash))throw new InvalidDataException("Bunker image checksum mismatch.");
                    using(var stream=new MemoryStream(bytes,false)) {
                        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();
                        if(image.PixelWidth!=floor.Width||image.PixelHeight!=floor.Height)throw new InvalidDataException("Bunker image size mismatch.");
                        image.Freeze();return image;
                    }
                });
                // Observe failures even if a rapidly changing floor has evicted the task.
                task.ContinueWith(t=> { var ignored=t.Exception; },TaskContinuationOptions.OnlyOnFaulted);
                bunkerImages.Add(floor.Id,task);bunkerImageOrder.Enqueue(floor.Id);
            }
            if(task.IsFaulted) { var observed=task.Exception;bunkerError=Localization.Get("BunkerUnavailable");return null; }
            return task.Status==TaskStatus.RanToCompletion?task.Result:null;
        }
        void ApplyBunker(WpfMapState state,bool tactical) {
            ObserveBunker();
            state.Bunker=null;state.BunkerImage=null;state.BunkerScale=1;state.BunkerCaption=null;
            BunkerFloor floor=InBunker&&DateTime.UtcNow-updated<TimeSpan.FromSeconds(15)?bunkerTracker.Current:null;
            if(floor==null) { displayedBunker=null;bunkerManualView=null;if(bunkerError!=null)state.TrackingStatus+=" — "+bunkerError;return; }
            if(displayedBunker==null||displayedBunker.BunkerId!=floor.BunkerId) { bunkerMiniZoom=bunkerFullZoom=1;bunkerFollow=true;bunkerManualView=null; }
            floor=bunkerLoad.Result.ViewFor(floor,position.X,position.Y,position.Z);
            state.BunkerCurrentView=floor.Id;
            if(tactical&&bunkerManualView!=null)floor=bunkerLoad.Result.Views.FirstOrDefault(v=>v.Id==bunkerManualView&&v.BunkerId==floor.BunkerId)??floor;
            displayedBunker=floor;
            var image=BunkerImage(floor);
            // Keep the world map until a complete, validated floor image is available.
            if(image==null) { state.TrackingStatus+=" — "+(bunkerError??Localization.Get("BunkerLoading"));return; }
            state.Bunker=floor;state.BunkerImage=image;
            state.BunkerScale=tactical?1:1521618.0/3000.0;
            state.Zoom=tactical?bunkerFullZoom:bunkerMiniZoom;state.Opacity=100;
            if(bunkerFollow&&position!=null)bunkerPan=ToMap(position);
            state.Pan=bunkerPan;
            if(tactical&&bunkerFollow&&(bunkerFullZoom==1||bunkerManualView!=null))state.Pan=ToMap(new Position { X=(floor.MinX+floor.MaxX)/2,Y=(floor.MinY+floor.MaxY)/2 });
            state.BunkerCaption=floor.BunkerId.Replace('_',' ')+" · "+(floor.DisplayName??Localization.T("BunkerHeightLayer",floor.Rank,floor.Count));
            state.Location=state.BunkerCaption;
            state.Grid=state.GridLabels=false;state.Store=null;state.Route=null;
            BunkerRouteState(state);
            // Existing point markers remain in world coordinates; surface road routes do not describe indoor paths.
        }
        internal BunkerFloor[] WpfBunkerViews() {return InBunker?bunkerLoad.Result.Views.Where(v=>v.BunkerId==bunkerTracker.Current.BunkerId).OrderBy(v=>v.Rank).ToArray():new BunkerFloor[0];}
        internal string WpfBunkerViewSelection {get{return bunkerManualView;}}
        internal void WpfSelectBunkerView(string id) {WpfVerifyAccess();bunkerManualView=id!=null&&WpfBunkerViews().Any(v=>v.Id==id)?id:null;bunkerFullZoom=1;bunkerFollow=true;}
        string HistoryContext(LocationHistoryPoint sample) {
            if(sample.Context!=null)return sample.Context;
            if(sample.MapId!="default"||sample.WorldPosition==null)return sample.Context=sample.MapId+"/surface";
            if(bunkerLoad!=null&&(bunkerLoad.IsFaulted||bunkerLoad.IsCanceled))return sample.Context=sample.MapId+"/surface";
            if(bunkerLoad==null||bunkerLoad.Status!=TaskStatus.RanToCompletion)return null;
            var p=sample.WorldPosition;var atlas=bunkerLoad.Result;var floor=atlas.FindFloor(p.X,p.Y,p.Z);var view=atlas.ViewFor(floor,p.X,p.Y,p.Z);
            return sample.Context=view==null?"default/surface":"default/"+view.Id;
        }
        sealed class HistoryProjection {internal PointF[] Points;internal PointF[][] Segments;}
        readonly Dictionary<string,HistoryProjection> historyProjections=new Dictionary<string,HistoryProjection>();
        bool cachedHistoryReady;
        internal void WpfHistoryForState(WpfMapState state) {
            string context=mapSidecarPaths.MapId+"/"+(state.Bunker==null?"surface":state.Bunker.Id);
            bool ready=bunkerLoad!=null&&bunkerLoad.Status==TaskStatus.RanToCompletion;
            if(cachedTrailRevision!=locationHistoryRevision||cachedHistoryReady!=ready) {historyProjections.Clear();cachedTrailRevision=locationHistoryRevision;cachedHistoryReady=ready;}
            HistoryProjection projection;
            if(!historyProjections.TryGetValue(context,out projection)) {
                var segments=new List<PointF[]>();var segment=new List<PointF>();LocationHistoryPoint previous=null;
                foreach(var sample in locationHistoryPoints) {
                    bool matches=HistoryContext(sample)==context;
                    bool gap=previous!=null&&((sample.SampledAtUtc-previous.SampledAtUtc).TotalSeconds>30||DestinationSearch.Metres(previous.MapPoint,sample.MapPoint)>200);
                    if(!matches||gap) {if(segment.Count>0)segments.Add(segment.ToArray());segment.Clear();}
                    if(matches)segment.Add(sample.MapPoint);previous=sample;
                }
                if(segment.Count>0)segments.Add(segment.ToArray());projection=new HistoryProjection {Segments=segments.ToArray(),Points=segments.SelectMany(s=>s).ToArray()};historyProjections.Add(context,projection);
            }
            state.Trail=projection.Points;state.TrailSegments=projection.Segments;
        }
        internal static void BunkerRouteState(WpfMapState state) {
            if(state.Bunker==null)return;
            state.Route=null;state.Calculating=state.SquadPlanning=false;
            state.RouteSummary=state.Target==null||state.Position==null?"":DestinationSearch.Distance(DestinationSearch.Metres(ToMap(state.Position),state.Target.Centroid))+" ("+Localization.Get("FieldDirect")+")";
            state.OwnRouteSummary=Localization.Get("FieldDirect");
            state.TravelRoad=state.TravelCalculating=false;state.TravelNote=null;
            state.TravelMeters=state.Target==null||state.Position==null?-1:DestinationSearch.Metres(ToMap(state.Position),state.Target.Centroid);
            state.TravelMinutes=0;
        }
        bool ZoomBunker(bool tactical,int direction) {
            if(displayedBunker==null)return false;
            float factor=(float)Math.Pow(1f+zoomStepPercent/100f,direction);
            if(tactical)bunkerFullZoom=Math.Max(1,Math.Min(32,bunkerFullZoom*factor));
            else bunkerMiniZoom=Math.Max(1,Math.Min(32,bunkerMiniZoom*factor));
            return true;
        }
        bool PanBunker(PointF point) {
            if(displayedBunker==null)return false;bunkerFollow=false;bunkerPan=point;return true;
        }
    }
}
