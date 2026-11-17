from pathlib import Path
def edit(file,a,b):
 p=Path(file);data=p.read_bytes();s=data.decode('utf-8-sig').replace('\r\n','\n');assert a in s,(file,a);s=s.replace(a,b)
 if b'\r\n' in data:s=s.replace('\n','\r\n')
 p.write_bytes((b'\xef\xbb\xbf' if data.startswith(b'\xef\xbb\xbf') else b'')+s.encode())
src='source/src/'
edit(src+'GridRouter.Bridges.cs','100.,Math.Max(0.,','100.0,Math.Max(0.0,')
edit(src+'GridRouter.World.cs','terrainFine = classes[LevelFine];','LoadBridgeMap();\n            terrainFine = classes[LevelFine];')
edit(src+'GridRouter.Local.cs','RoadRoute first=FindBaseRoute(a,b,mode);','''return FindAtHeight(a,b,mode,current,null,null);
        }
        internal RoadRoute FindAtHeight(PointF a,PointF b,RoadRouteMode mode,Func<bool> current,double? startHeight,double? endHeight) {
            var first=FindUnlayered(a,b,mode,current);
            if(bridges==null||!Finite(a.X)||!Finite(a.Y)||!Finite(b.X)||!Finite(b.Y)||!Enum.IsDefined(typeof(RoadRouteMode),mode))return first;
            return ApplyBridgeLayers(first,a,b,mode,current,startHeight,endHeight);
        }
        RoadRoute FindUnlayered(PointF a,PointF b,RoadRouteMode mode,Func<bool> current) {
            RoadRoute first=FindBaseRoute(a,b,mode);''')
edit(src+'RoadRouter.cs','public class RoadRoute {','public class RoadRoute {\n        internal int[] BridgeLayers;')
edit(src+'NavigationRouter.cs','        internal static RoadRoute FindRoute(PointF playerPt, PointF targetPt) {','''        internal static double[] BridgeHeights(PointF p) { return Ready?gridInstance.BridgeHeights(p):new double[0]; }
        internal static int SurfaceGroup(PointF p,double? z) { return Ready?gridInstance.SurfaceGroup(p,z):0; }
        internal static bool SameSurface(PointF p,double? z,PointF q,double? targetZ) {
            int a=SurfaceGroup(p,z),b=SurfaceGroup(q,targetZ);
            return a==b;
        }
        internal static RoadRoute FindAtHeight(PointF a,PointF b,RoadRouteMode mode,Func<bool> current,double? z,double? targetZ) {
            return Ready?gridInstance.FindAtHeight(a,b,mode,current,z,targetZ):FindCurrentRoute(a,b,mode,current);
        }
        internal static RoadRoute FindRoute(PointF playerPt, PointF targetPt) {''')
edit(src+'GridRouter.Bridges.cs','        internal double[] BridgeHeights','        internal int SurfaceGroup(PointF p,double? z){var b=bridges;return b==null?0:b.Group(b.Endpoint(p,z));}\n        internal double[] BridgeHeights')
edit(src+'Zones.cs','public sealed class MapZone {','public sealed class MapZone {\n        internal double? RouteHeightCm;')
edit(src+'MiniMap.cs','if(!object.ReferenceEquals(searchTarget,wpfUnsharedTarget)&&DestinationReached(searchTarget,currentPt))','if(!object.ReferenceEquals(searchTarget,wpfUnsharedTarget)&&DestinationReached(searchTarget,currentPt)&&NavigationRouter.SameSurface(currentPt,p.Z,searchTarget.Centroid,searchTarget.RouteHeightCm))')
edit(src+'MiniMap.cs','bool RequestRouteAsync(PointF playerPt','int lastRouteSurfaceGroup;\n        bool RequestRouteAsync(PointF playerPt')
edit(src+'MiniMap.cs','if(!force && lastRouteTarget==target','double? gpsHeight=position==null?(double?)null:position.Z;\n            int startGroup=NavigationRouter.SurfaceGroup(playerPt,gpsHeight);\n            if(!force && lastRouteSurfaceGroup==startGroup && lastRouteTarget==target')
edit(src+'MiniMap.cs','NavigationRouter.FindCurrentRoute(playerPt,targetPoint,requestedMode,()=>!closing && Interlocked.Read(ref routeGeneration)==request)','NavigationRouter.FindAtHeight(playerPt,targetPoint,requestedMode,()=>!closing && Interlocked.Read(ref routeGeneration)==request,gpsHeight,target.RouteHeightCm)')
edit(src+'MiniMap.cs','activeRoute=result; lastRouteTarget=target;', 'activeRoute=result;lastRouteSurfaceGroup=startGroup; lastRouteTarget=target;')
edit(src+'SquadTools.cs','<ArrivalRadius())','<ArrivalRadius()&&NavigationRouter.SameSurface(MapWindow.ToMap(state.Position),state.Position.Z,PointOf(activeStops[activeStop]),null))')
edit(src+'WpfFieldConsole.cs','            var save=new MenuItem { Header=Localization.Get("MarkerAdd")','''            if(State!=null&&State.Bunker==null&&NavigateShared!=null) {
                var heights=NavigationRouter.BridgeHeights(target);
                for(int i=0;i<heights.Length;i++) {
                    double height=heights[i];string label=Localization.Get(i==0?"BridgeLower":"BridgeUpper");
                    var pick=new MenuItem { Header=label };pick.Click+=(s,e)=>NavigateShared(new MapZone {Name=label,Points=new[]{target},RouteHeightCm=height});menu.Items.Add(pick);
                }
            }
            var save=new MenuItem { Header=Localization.Get("MarkerAdd")''')
edit(src+'Localization.cs','new[]{"LxLibrary",','new[]{"BridgeLower","Navigate below bridge","导航到桥下","導航至橋下"},\n                new[]{"BridgeUpper","Navigate onto bridge","导航到桥面","導航至橋面"},\n                new[]{"LxLibrary",')
edit('source/scripts/Get-MiniMapSources.ps1',"'GridRouter.World.cs',","'GridRouter.World.cs','GridRouter.Bridges.cs',")
for f in ['source/scripts/Invoke-MiniMapBuild.ps1','Build-LocalCandidate.ps1']:edit(f,"'nav-world-grade.bin',","'nav-world-grade.bin','nav-bridges.bin',")
for f in ['Build-LocalCandidate.ps1','tools/package-ui.py',src+'MiniMap.cs','package/SCUM-MiniMap/使用说明.md']:edit(f,'2.4.9','2.4.10')
