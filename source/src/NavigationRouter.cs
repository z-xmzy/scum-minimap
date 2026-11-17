using System;
using System.Drawing;

namespace ScumMiniMap {
    // All route modes share the embedded SCUM world navigation layers.
    internal static class NavigationRouter {
        static readonly object gate = new object();
        static volatile bool ready;

        internal static bool Ready { get { return ready; } }

        internal static void Initialize() {
            lock (gate) {
                GridRouter grid = gridInstance;
                if (grid == null) {
                    grid = new GridRouter();
                    gridInstance = grid;
                }
                try { RoadRouter.Instance.InitializeFromResource(); } catch { }
                if (!grid.IsLoaded) grid.InitializeFromResource();
                ready = grid.IsLoaded;
            }
        }

        static GridRouter gridInstance;

        internal static RoadRoute FindRoute(PointF playerPt, PointF targetPt, RoadRouteMode mode) {
            return FindCurrentRoute(playerPt,targetPt,mode,null);
        }

        internal static RoadRoute FindCurrentRoute(PointF playerPt,PointF targetPt,RoadRouteMode mode,Func<bool> current) {
            GridRouter grid = gridInstance;
            if (ready && grid != null && grid.IsLoaded) {
                // Grid loaded: it is the authoritative router for all four modes.  No
                // legacy fallback here - a failed terrain-aware route must stay a failure
                // (or be rescued inside GridRouter) instead of silently switching to a
                // road-graph route that ignores terrain, cliffs and the dam wall.
                return grid.FindCurrentRoute(playerPt,targetPt,mode,current);
            }
            // No valid world layers are available.
            return new RoadRoute { Mode=mode, PlayerPoint=playerPt, TargetPoint=targetPt, Polyline=new PointF[0] };
        }

        internal static double[] BridgeHeights(PointF p) { return Ready?gridInstance.BridgeHeights(p):new double[0]; }
        internal static int SurfaceGroup(PointF p,double? z) { return Ready?gridInstance.SurfaceGroup(p,z):0; }
        internal static bool SameSurface(PointF p,double? z,PointF q,double? targetZ) {
            int a=SurfaceGroup(p,z),b=SurfaceGroup(q,targetZ);
            return a==b;
        }
        internal static RoadRoute FindAtHeight(PointF a,PointF b,RoadRouteMode mode,Func<bool> current,double? z,double? targetZ) {
            return Ready?gridInstance.FindAtHeight(a,b,mode,current,z,targetZ):FindCurrentRoute(a,b,mode,current);
        }
        internal static RoadRoute FindRoute(PointF playerPt, PointF targetPt) {
            return FindRoute(playerPt, targetPt, RoadRouteMode.Shortest);
        }
    }
}
