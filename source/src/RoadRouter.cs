using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Reflection;

namespace ScumMiniMap {
    // Fixed coverage derived from the bundled map; independent of custom user textures.
    internal sealed class RoutingWaterMask {
        byte[] bits;
        int width,height;
        internal bool IsLoaded { get { return bits!=null; } }
        internal void Load() {
            Stream raw=Assembly.GetExecutingAssembly().GetManifestResourceStream("water-mask.bin");
            if(raw==null) {
                foreach(string path in new[]{Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"water-mask.bin"),Path.Combine(Environment.CurrentDirectory,"resources","water-mask.bin")})
                    if(File.Exists(path)) { raw=File.OpenRead(path); break; }
            }
            if(raw==null) return;
            using(raw) using(var zip=new GZipStream(raw,CompressionMode.Decompress)) using(var reader=new BinaryReader(zip)) {
                if(new string(reader.ReadChars(4))!="WATR") throw new InvalidDataException("Invalid water mask.");
                width=reader.ReadInt32(); height=reader.ReadInt32();
                if(width<1 || height<1 || width>8192 || height>8192) throw new InvalidDataException("Invalid water mask size.");
                int count=(width*height+7)/8;
                byte[] data=reader.ReadBytes(count);
                if(data.Length!=count) throw new InvalidDataException("Truncated water mask.");
                bits=data;
            }
        }
        internal bool IsWaterForPresentation(float x,float y) { return Water(x,y); }
        bool Water(float x,float y) {
            if(float.IsNaN(x)||float.IsNaN(y)||x<0||y<0||x>=1||y>=1) return true;
            int index=(int)(y*height)*width+(int)(x*width);
            return (bits[index>>3]&(1<<(index&7)))!=0;
        }
        internal bool AllowsConnector(PointF a,PointF b) {
            if(!IsLoaded) return false;
            double dx=b.X-a.X,dy=b.Y-a.Y;
            if(double.IsNaN(dx)||double.IsNaN(dy)||double.IsInfinity(dx)||double.IsInfinity(dy)) return false;
            // Half-cell spacing preserves small streams and narrow lake inlets.
            int samples=(int)Math.Ceiling(Math.Max(Math.Abs(dx)*width,Math.Abs(dy)*height)*2);
            if(samples>32768) return false;
            samples=Math.Max(1,samples);
            for(int i=0;i<=samples;i++) if(Water(a.X+(float)(dx*i/samples),a.Y+(float)(dy*i/samples))) return false;
            return true;
        }
    }
    public enum RoadRouteMode { Quickest, Shortest, PavedOnly, Walking }
    public enum RoadSurface { Unknown, Paved, Dirt }

    public class RoadRoute {
        internal int[] BridgeLayers;
        internal bool BridgeBlocked;
        public RoadRouteMode Mode;
        public double EstimatedSeconds;
        public double PavedDistanceMeters;
        public double DirtDistanceMeters;
        public double UnknownDistanceMeters;
        public bool Success;
        public PointF PlayerPoint;
        public PointF RoadEntryPoint;
        public PointF RoadExitPoint;
        public PointF TargetPoint;
        public PointF[] Polyline; // Full route including entry/exit or just road path
        public int[] JunctionIndices=new int[0]; // Actual branch nodes along Polyline, not geometry bends.
        public double TotalDistanceMeters;
        public double RoadDistanceMeters;
        public double EntryDistanceMeters;
        public double ExitDistanceMeters;
    }

    public class RoadRouter {
        private static readonly RoadRouter instance = new RoadRouter();
        public static RoadRouter Instance {
            get {
                return instance;
            }
        }

        private volatile bool isLoaded;
        private bool mapRoadsLoaded;
        private int mapRoadsFirstNode;
        private readonly object loadLock = new object();
        public bool IsLoaded { get { return isLoaded; } private set { isLoaded=value; } }

        public struct RoadNode {
            public float U;
            public float V;
        }

        public struct RoadEdge {
            public int U;
            public int V;
            public float LengthMeters;
            public PointF[] Points;
            public bool IsBridge;
            public RoadSurface Surface;
            // 1 for captured geometry. Inferred land connectors use a penalty so
            // ordinary roads stay preferred unless the detour is far longer.
            public float CostFactor;
        }

        public struct HalfEdge {
            public int TargetNode;
            public float Weight;
            public int EdgeIndex;
            public bool Forward;
        }

        private List<RoadNode> nodesList;
        private List<RoadEdge> edgesList;
        private List<HalfEdge>[] adj;
        private int gridRes;
        private double heuristicDistanceFactor=1;
        private List<ushort>[] spatialGridList;

        // Fast lookup arrays
        private RoadNode[] nodes;
        private RoadEdge[] edges;
        private ushort[][] spatialGrid;

        // Connected component cache
        private int[] nodeComponent;
        private int componentCount;
        private List<int>[] componentNodes;
        private int mainComponentId;
        private readonly RoutingWaterMask waterMask=new RoutingWaterMask();

        // Reusable zero-allocation A* search state
        private readonly object searchLock = new object();
        private double[] gScores;
        private HalfEdge[] cameFrom;
        private uint[] visitedEpoch;
        private uint currentEpoch;
        private MinHeap pq;

        private const double MapScaleX = 1521618.0 / 100.0; // 15216.18 meters across map
        private const double MapScaleY = 1523618.0 / 100.0; // 15236.18 meters across map

        public static double DistanceMeters(PointF a, PointF b) {
            double dx = (b.X - a.X) * MapScaleX;
            double dy = (b.Y - a.Y) * MapScaleY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double DistanceMeters(float u1, float v1, float u2, float v2) {
            double dx = (u2 - u1) * MapScaleX;
            double dy = (v2 - v1) * MapScaleY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal PointF[][] PresentationGeometry() { lock(loadLock) { return edgesList==null?new PointF[0][]:edgesList.Select(edge=>(PointF[])edge.Points.Clone()).ToArray(); } }
        public void InitializeFromResource() {
            lock(loadLock) { InitializeCore(); }
        }
        private void InitializeCore() {
            if (IsLoaded) return;
            try {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream raw = InstallationAssets.Open("roads.bin")) {
                    if (raw != null) {
                        using (GZipStream gz = new GZipStream(raw, CompressionMode.Decompress))
                        using (BinaryReader br = new BinaryReader(gz)) {
                            LoadBinary(br);
                        }
                        CompleteNetworkLoad();
                        return;
                    }
                }
                // Fallback: check file on disk if running unbundled
                string[] candidates = new string[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roads.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "roads.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScumMiniMap", "resources", "roads.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScumMiniMap", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "resources", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "ScumMiniMap", "resources", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "ScumMiniMap", "roads.bin"),
                    Path.Combine(Environment.CurrentDirectory, "roads.bin")
                };
                foreach (string path in candidates) {
                    if (File.Exists(path)) {
                        using (FileStream fs = File.OpenRead(path))
                        using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                        using (BinaryReader br = new BinaryReader(gz)) {
                            LoadBinary(br);
                        }
                        CompleteNetworkLoad();
                        return;
                    }
                }
            } catch {
                IsLoaded = false;
            }
        }

        static float ReadCoordinate(BinaryReader reader) {
            float value=reader.ReadSingle();
            if(float.IsNaN(value)||float.IsInfinity(value)||value<-.1f||value>1.1f) throw new InvalidDataException("Invalid road coordinate.");
            return value;
        }
        public void LoadBinary(BinaryReader br) {
            byte[] magic = br.ReadBytes(4);
            IsLoaded=false;
            if (magic.Length!=4) throw new InvalidDataException("Truncated road header.");
            if (magic[0] != 'R' || magic[1] != 'O' || magic[2] != 'A' || magic[3] != 'D') throw new InvalidDataException("Invalid road header.");
            uint version = br.ReadUInt32();
            if (version != 2 && version != 3 && version != 4) throw new InvalidDataException("Unsupported road version.");

            int nodeCount = br.ReadInt32();
            int edgeCount = br.ReadInt32();
            gridRes = br.ReadInt32();
            if(nodeCount<1 || nodeCount>100000 || edgeCount<1 || edgeCount>60000 || gridRes<1 || gridRes>512)
                throw new InvalidDataException("Road counts exceed supported limits.");
            long totalPoints=0, totalReferences=0;
            heuristicDistanceFactor=1;

            nodesList = new List<RoadNode>(nodeCount + 64);
            for (int i = 0; i < nodeCount; i++) {
                nodesList.Add(new RoadNode { U = ReadCoordinate(br), V = ReadCoordinate(br) });
            }

            edgesList = new List<RoadEdge>(edgeCount + 2048);
            adj = new List<HalfEdge>[nodeCount + 2048];
            for (int i = 0; i < adj.Length; i++) adj[i] = new List<HalfEdge>(4);

            for (int i = 0; i < edgeCount; i++) {
                int u = br.ReadInt32();
                int v = br.ReadInt32();
                float lengthM = br.ReadSingle();
                if(u<0 || v<0 || u>=nodeCount || v>=nodeCount || float.IsNaN(lengthM) || float.IsInfinity(lengthM) || lengthM<=0 || lengthM>100000)
                    throw new InvalidDataException("Invalid road edge.");
                byte flags = version >= 3 ? br.ReadByte() : (byte)0;
                if((version < 4 && flags > 1) || (version == 4 && flags > 7)) throw new InvalidDataException("Invalid road surface flags.");
                ushort ptCount = br.ReadUInt16();
                totalPoints+=ptCount;
                if(ptCount<2 || totalPoints>4000000) throw new InvalidDataException("Road geometry exceeds supported limits.");
                PointF[] pts = new PointF[ptCount];
                for (int p = 0; p < ptCount; p++) {
                    pts[p] = new PointF(ReadCoordinate(br), ReadCoordinate(br));
                }
                double geometryLength=0;
                for(int p=1;p<pts.Length;p++)geometryLength+=DistanceMeters(pts[p-1],pts[p]);
                if(geometryLength>0)heuristicDistanceFactor=Math.Min(heuristicDistanceFactor,lengthM/geometryLength);
                edgesList.Add(new RoadEdge { U = u, V = v, LengthMeters = lengthM, Points = pts, IsBridge = (flags & 1) != 0, Surface = version < 4 || (flags & 4) != 0 ? RoadSurface.Unknown : (flags & 2) != 0 ? RoadSurface.Dirt : RoadSurface.Paved, CostFactor = 1f });
                adj[u].Add(new HalfEdge { TargetNode = v, Weight = lengthM, EdgeIndex = i, Forward = true });
                adj[v].Add(new HalfEdge { TargetNode = u, Weight = lengthM, EdgeIndex = i, Forward = false });
            }

            int totalCells = gridRes * gridRes;
            spatialGridList = new List<ushort>[totalCells];
            for (int i = 0; i < totalCells; i++) {
                ushort count = br.ReadUInt16();
                totalReferences+=count;
                if(totalReferences>4000000) throw new InvalidDataException("Road index exceeds supported limits.");
                List<ushort> list = new List<ushort>(count + 8);
                for (int c = 0; c < count; c++) { ushort edge=br.ReadUInt16(); if(edge>=edgeCount) throw new InvalidDataException("Invalid road index."); list.Add(edge); }
                spatialGridList[i] = list;
            }

            // Topology is compiled from classified roads, audited dry/3D seams and
            // named surveyed highway bridges. Runtime land connectors are added only
            // after a successful asset load (see CompleteNetworkLoad).
            waterMask.Load();

            // Commit final arrays for high-performance direct indexing
            nodes = nodesList.ToArray();
            edges = edgesList.ToArray();
            spatialGrid = new ushort[totalCells][];
            for (int i = 0; i < totalCells; i++) {
                spatialGrid[i] = spatialGridList[i].ToArray();
            }

            // Analyze and index connected components
            ComputeConnectedComponents();

            // Allocate reusable zero-allocation A* search buffers
            int totalNodes = nodes.Length;
            gScores = new double[totalNodes];
            cameFrom = new HalfEdge[totalNodes];
            visitedEpoch = new uint[totalNodes];
            currentEpoch = 1;
            pq = new MinHeap(1024);

            IsLoaded = true;
        }

        private void ComputeConnectedComponents() {
            int count = nodes.Length;
            nodeComponent = new int[count];
            for (int i = 0; i < count; i++) nodeComponent[i] = -1;

            List<List<int>> comps = new List<List<int>>();
            Queue<int> q = new Queue<int>();

            for (int i = 0; i < count; i++) {
                if (nodeComponent[i] >= 0) continue;
                int cIdx = comps.Count;
                List<int> comp = new List<int>();
                q.Enqueue(i);
                nodeComponent[i] = cIdx;

                while (q.Count > 0) {
                    int curr = q.Dequeue();
                    comp.Add(curr);
                    List<HalfEdge> outgoing = adj[curr];
                    for (int k = 0; k < outgoing.Count; k++) {
                        int nbr = outgoing[k].TargetNode;
                        if (nodeComponent[nbr] < 0) {
                            nodeComponent[nbr] = cIdx;
                            q.Enqueue(nbr);
                        }
                    }
                }
                comps.Add(comp);
            }

            componentCount = comps.Count;
            componentNodes = comps.ToArray();

            int maxCount = -1;
            mainComponentId = 0;
            for (int c = 0; c < comps.Count; c++) {
                if (comps[c].Count > maxCount) {
                    maxCount = comps[c].Count;
                    mainComponentId = c;
                }
            }
        }

        // The bundled map.png draws every paved road, including town streets that the
        // UE4 landscape-spline capture does not contain. tools/extract-map-roads.py
        // compiles those visible streets into a compact supplementary graph that is
        // loaded from assets/map-roads.bin next to the executable. Missing or invalid
        // files are skipped: the packaged roads.bin asset remains authoritative.
        private const int MapRoadsMaxNodes = 400000;
        private const int MapRoadsMaxEdges = 1200000;
        private const double MapRoadsAnchorMeters = 12.0;
        private const int MapRoadsAnchorCellMeters = 128;
        // Purification: drop supplementary edges that duplicate captured geometry
        // (point-to-line <= 6 m, direction within 20 degrees), avoiding two parallel
        // road geometries that make A* switch back and forth.
        private const double MapRoadsOverlapMeters = 6.0;
        private const double MapRoadsOverlapCellMeters = 32.0;
        private const double MapRoadsOverlapCos = 0.93969262;   // cos(20 deg)
        private const float MapRoadsCostFactor = 1f;
        private const int MapRoadsVirtualBase = 1000000;
        private int mapRoadsSuppressed;

        private void CompleteNetworkLoad() {
            bool supplemented = false;
            try { supplemented = TryLoadMapRoads(); } catch { supplemented = false; }
            int added = 0;
            if (!supplemented) added = AddLandConnectors();
            if (!supplemented && added == 0) return;
            nodes = nodesList.ToArray();
            edges = edgesList.ToArray();
            ComputeConnectedComponents();
            if (supplemented) {
                int bridges = BridgeIsolatedMapComponents();
                if (bridges > 0) {
                    edges = edgesList.ToArray();
                    ComputeConnectedComponents();
                }
                RebuildSpatialGridWithMapRoads();
            }
            int totalNodes = nodes.Length;
            gScores = new double[totalNodes];
            cameFrom = new HalfEdge[totalNodes];
            visitedEpoch = new uint[totalNodes];
            currentEpoch = 1;
            pq = new MinHeap(1024);
        }

        private static string[] MapRoadCandidates() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string current = Environment.CurrentDirectory;
            return new string[] {
                Path.Combine(baseDir, "assets", "map-roads.bin"),
                Path.Combine(baseDir, "map-roads.bin"),
                Path.Combine(current, "assets", "map-roads.bin"),
                Path.Combine(current, "resources", "map-roads.bin"),
                Path.Combine(current, "map-roads.bin")
            };
        }

        private static long AnchorCellKey(PointF p) {
            long x = (long)Math.Floor(p.X * MapScaleX / MapRoadsAnchorCellMeters);
            long y = (long)Math.Floor(p.Y * MapScaleY / MapRoadsAnchorCellMeters);
            return x * 100000L + y;
        }

        private bool TryLoadMapRoads() {
            mapRoadsFirstNode = nodes == null ? 0 : nodes.Length;
            if (nodes == null || nodesList == null || edgesList == null || adj == null) return false;
            string path = null;
            string[] candidates = MapRoadCandidates();
            for (int i = 0; i < candidates.Length; i++) {
                if (File.Exists(candidates[i])) { path = candidates[i]; break; }
            }
            if (path == null) return false;
            PointF[] points;
            int[] edgeU, edgeV;
            PointF[][] edgePoints;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(stream)) {
                if (stream.Length < 16 || stream.Length > 64L * 1024 * 1024) return false;
                byte[] magic = br.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != 'M' || magic[1] != 'R' || magic[2] != 'O' || magic[3] != 'D') return false;
                ushort version = br.ReadUInt16();
                br.ReadUInt16();
                if (version != 1 && version != 2) return false;
                int nodeCount = br.ReadInt32();
                int edgeCount = br.ReadInt32();
                if (nodeCount < 1 || nodeCount > MapRoadsMaxNodes || edgeCount < 1 || edgeCount > MapRoadsMaxEdges) return false;
                points = new PointF[nodeCount];
                for (int i = 0; i < nodeCount; i++) {
                    float u = br.ReadSingle();
                    float v = br.ReadSingle();
                    if (float.IsNaN(u) || float.IsNaN(v) || u < 0 || u > 1 || v < 0 || v > 1) return false;
                    points[i] = new PointF(u, v);
                }
                edgeU = new int[edgeCount];
                edgeV = new int[edgeCount];
                edgePoints = new PointF[edgeCount][];
                for (int i = 0; i < edgeCount; i++) {
                    int u = br.ReadInt32();
                    int v = br.ReadInt32();
                    float length = br.ReadSingle();
                    if (u < 0 || v < 0 || u >= nodeCount || v >= nodeCount || u == v) return false;
                    if (float.IsNaN(length) || float.IsInfinity(length) || length <= 0 || length > 100000) return false;
                    edgeU[i] = u;
                    edgeV[i] = v;
                    if (version >= 2) {
                        ushort pointCount = br.ReadUInt16();
                        if (pointCount < 2 || pointCount > 4096) return false;
                        PointF[] polyline = new PointF[pointCount];
                        for (int p = 0; p < pointCount; p++) {
                            float pu = br.ReadSingle();
                            float pv = br.ReadSingle();
                            if (float.IsNaN(pu) || float.IsNaN(pv) || pu < 0 || pu > 1 || pv < 0 || pv > 1) return false;
                            polyline[p] = new PointF(pu, pv);
                        }
                        edgePoints[i] = polyline;
                    } else {
                        edgePoints[i] = new PointF[] { points[u], points[v] };
                    }
                }
            }
            // Anchor extracted nodes to the captured network: prefer a captured node,
            // otherwise split the nearest captured segment at the projection so the
            // junction does not depend on existing captured node density.
            int originalNodes = nodes.Length;
            int originalEdges = edgesList.Count;
            Dictionary<long, List<int>> anchorCells = new Dictionary<long, List<int>>(originalNodes / 2);
            for (int i = 0; i < originalNodes; i++) {
                long key = AnchorCellKey(new PointF(nodes[i].U, nodes[i].V));
                List<int> list;
                if (!anchorCells.TryGetValue(key, out list)) { list = new List<int>(4); anchorCells[key] = list; }
                list.Add(i);
            }
            int[] mapNodeId = new int[points.Length];
            for (int i = 0; i < points.Length; i++) {
                long cx = (long)Math.Floor(points[i].X * MapScaleX / MapRoadsAnchorCellMeters);
                long cy = (long)Math.Floor(points[i].Y * MapScaleY / MapRoadsAnchorCellMeters);
                int nearest = -1;
                double best = MapRoadsAnchorMeters;
                for (long dx = -1; dx <= 1; dx++) {
                    for (long dy = -1; dy <= 1; dy++) {
                        List<int> list;
                        if (!anchorCells.TryGetValue((cx + dx) * 100000L + (cy + dy), out list)) continue;
                        for (int k = 0; k < list.Count; k++) {
                            int candidate = list[k];
                            double distance = DistanceMeters(points[i], new PointF(nodes[candidate].U, nodes[candidate].V));
                            if (distance < best) { best = distance; nearest = candidate; }
                        }
                    }
                }
                mapNodeId[i] = nearest;   // -1: node has to be materialised
            }
            // Purified temporary graph: virtual ids are captured ids or originalNodes+extractedIndex.
            List<MapRoadDraft> drafts = new List<MapRoadDraft>(edgeU.Length);
            HashSet<long> pairs = new HashSet<long>();
            for (int i = 0; i < edgeU.Length; i++) {
                int a = mapNodeId[edgeU[i]];
                int b = mapNodeId[edgeV[i]];
                int u = a >= 0 ? a : MapRoadsVirtualBase + edgeU[i];
                int v = b >= 0 ? b : MapRoadsVirtualBase + edgeV[i];
                if (u == v) continue;
                long key = u < v ? ((long)u << 32) | (uint)v : ((long)v << 32) | (uint)u;
                if (!pairs.Add(key)) continue;
                drafts.Add(new MapRoadDraft { U = u, V = v, Points = edgePoints[i], Protected = (u < MapRoadsVirtualBase) != (v < MapRoadsVirtualBase) });
            }
            // Purify: prune short dead-end branches of the supplementary graph.
            for (int pass = 0; pass < 6; pass++) {
                Dictionary<int, int> degree = new Dictionary<int, int>(drafts.Count * 2);
                for (int i = 0; i < drafts.Count; i++) {
                    MapRoadDraft d = drafts[i];
                    int n;
                    degree.TryGetValue(d.U, out n); degree[d.U] = n + 1;
                    degree.TryGetValue(d.V, out n); degree[d.V] = n + 1;
                }
                int before = drafts.Count;
                List<MapRoadDraft> keep = new List<MapRoadDraft>(drafts.Count);
                for (int i = 0; i < drafts.Count; i++) {
                    MapRoadDraft d = drafts[i];
                    if ((degree[d.U] == 1 && PolylineLength(d.Points) <= 25.0) ||
                        (degree[d.V] == 1 && PolylineLength(d.Points) <= 25.0)) continue;
                    keep.Add(d);
                }
                drafts = keep;
                if (drafts.Count == before) break;
            }
            // Purify: drop edges that merely duplicate captured geometry (<=6 m, <=20 deg).
            int removedOverlap = DropCapturedParallels(drafts, edgesList.Count);
            // Materialise surviving edges, allocating extracted nodes only when used.
            int added = 0;
            for (int i = 0; i < drafts.Count; i++) {
                MapRoadDraft d = drafts[i];
                int u = ResolveMapNode(d.U, points, mapNodeId);
                int v = ResolveMapNode(d.V, points, mapNodeId);
                if (u == v) continue;
                PointF[] polyline = new PointF[d.Points.Length];
                for (int p = 0; p < polyline.Length; p++) polyline[p] = d.Points[p];
                polyline[0] = new PointF(nodesList[u].U, nodesList[u].V);
                polyline[polyline.Length - 1] = new PointF(nodesList[v].U, nodesList[v].V);
                double length = 0;
                for (int p = 1; p < polyline.Length; p++) length += DistanceMeters(polyline[p - 1], polyline[p]);
                if (length <= 0.01) continue;
                EnsureAdjacency(nodesList.Count);
                int index = edgesList.Count;
                edgesList.Add(new RoadEdge {
                    U = u, V = v, LengthMeters = (float)length, Points = polyline,
                    IsBridge = false, Surface = RoadSurface.Paved, CostFactor = MapRoadsCostFactor
                });
                adj[u].Add(new HalfEdge { TargetNode = v, Weight = (float)length, EdgeIndex = index, Forward = true });
                adj[v].Add(new HalfEdge { TargetNode = u, Weight = (float)length, EdgeIndex = index, Forward = false });
                added++;
            }
            mapRoadsSuppressed = removedOverlap;
            mapRoadsLoaded = added > 0;
            return mapRoadsLoaded;
        }

        private static double SegmentProject(PointF p, PointF a, PointF b, out double t) {
            double ax = a.X * MapScaleX, ay = a.Y * MapScaleY;
            double ex = b.X * MapScaleX - ax, ey = b.Y * MapScaleY - ay;
            double px = p.X * MapScaleX, py = p.Y * MapScaleY;
            double l2 = ex * ex + ey * ey;
            t = l2 > 1e-12 ? Math.Max(0.0, Math.Min(1.0, ((px - ax) * ex + (py - ay) * ey) / l2)) : 0.0;
            double qx = ax + t * ex, qy = ay + t * ey;
            return Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
        }

        private int SplitCapturedEdge(int edgeIndex, int segment, double t) {
            RoadEdge edge = edgesList[edgeIndex];
            PointF[] pts = edge.Points;
            PointF a = pts[segment], b = pts[segment + 1];
            PointF cut = new PointF((float)(a.X + (b.X - a.X) * t), (float)(a.Y + (b.Y - a.Y) * t));
            int cutNode = nodesList.Count;
            nodesList.Add(new RoadNode { U = cut.X, V = cut.Y });
            PointF[] left = new PointF[segment + 2];
            Array.Copy(pts, left, segment + 1);
            left[segment + 1] = cut;
            PointF[] right = new PointF[pts.Length - segment];
            right[0] = cut;
            Array.Copy(pts, segment + 1, right, 1, pts.Length - segment - 1);
            double leftLength = 0, rightLength = 0;
            for (int p = 1; p < left.Length; p++) leftLength += DistanceMeters(left[p - 1], left[p]);
            for (int p = 1; p < right.Length; p++) rightLength += DistanceMeters(right[p - 1], right[p]);
            edgesList[edgeIndex] = new RoadEdge { U = edge.U, V = cutNode, LengthMeters = (float)leftLength,
                Points = left, IsBridge = edge.IsBridge, Surface = edge.Surface, CostFactor = edge.CostFactor };
            edgesList.Add(new RoadEdge { U = cutNode, V = edge.V, LengthMeters = (float)rightLength,
                Points = right, IsBridge = edge.IsBridge, Surface = edge.Surface, CostFactor = edge.CostFactor });
            return cutNode;
        }

        private void RebuildAdjacency() {
            EnsureAdjacency(nodesList.Count);
            for (int i = 0; i < adj.Length; i++) adj[i].Clear();
            for (int e = 0; e < edgesList.Count; e++) {
                RoadEdge edge = edgesList[e];
                adj[edge.U].Add(new HalfEdge { TargetNode = edge.V, Weight = edge.LengthMeters, EdgeIndex = e, Forward = true });
                adj[edge.V].Add(new HalfEdge { TargetNode = edge.U, Weight = edge.LengthMeters, EdgeIndex = e, Forward = false });
            }
        }
        // Town grids extracted from the map can end up as components with no
        // captured node. Bridge such components to the main network with the same
        // bounded, dry, penalised connector policy so the destination is no longer
        // snapped to a road hundreds of metres away.
        private int BridgeIsolatedMapComponents() {
            if (!mapRoadsLoaded || componentNodes == null || nodes == null) return 0;
            int first = mapRoadsFirstNode;
            if (first <= 0 || first >= nodes.Length || componentNodes.Length < 2) return 0;
            const double maxMeters = 800.0;
            const float factor = 1.5f;
            List<int>[] cells = new List<int>[gridRes * gridRes];
            List<int> main = componentNodes[mainComponentId];
            for (int k = 0; k < main.Count; k++) {
                int n = main[k];
                int cell = GridCell(nodes[n]);
                List<int> list = cells[cell];
                if (list == null) { list = new List<int>(8); cells[cell] = list; }
                list.Add(n);
            }
            int radius = Math.Max(1, (int)Math.Ceiling(maxMeters / (15220.0 / gridRes)));
            int bridges = 0;
            int inspectedComponents = 0;
            for (int c = 0; c < componentNodes.Length; c++) {
                if (++inspectedComponents > 200) break;
                if (c == mainComponentId) continue;
                List<int> comp = componentNodes[c];
                bool hasMap = false, hasCaptured = false;
                for (int k = 0; k < comp.Count; k++) {
                    if (comp[k] >= first) hasMap = true; else hasCaptured = true;
                    if (hasMap && hasCaptured) break;
                }
                if (!hasMap || comp.Count < 60 || bridges >= 60) continue;
                int added = 0;
                int step = Math.Max(1, comp.Count / 4);
                for (int k = 0; k < comp.Count && added < 1; k += step) {
                    int source = comp[k];
                    int gu = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(nodes[source].U * gridRes)));
                    int gv = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(nodes[source].V * gridRes)));
                    int target = -1;
                    double best = maxMeters;
                    for (int r = 0; r <= radius; r++) {
                        for (int dx = -r; dx <= r; dx++) {
                            for (int dy = -r; dy <= r; dy++) {
                                if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                                int cx = gu + dx, cy = gv + dy;
                                if (cx < 0 || cx >= gridRes || cy < 0 || cy >= gridRes) continue;
                                List<int> list = cells[cy * gridRes + cx];
                                if (list == null) continue;
                                for (int q = 0; q < list.Count; q++) {
                                    int n = list[q];
                                    double d = DistanceMeters(nodes[source].U, nodes[source].V, nodes[n].U, nodes[n].V);
                                    if (d >= 1.0 && d < best) { best = d; target = n; }
                                }
                            }
                        }
                        if (target >= 0 && best < (r - 1) * (15220.0 / gridRes)) break;
                    }
                    if (target < 0) continue;
                    PointF a = new PointF(nodes[source].U, nodes[source].V);
                    PointF b = new PointF(nodes[target].U, nodes[target].V);
                    if (!waterMask.AllowsConnector(a, b)) continue;
                    int index = edgesList.Count;
                    float length = (float)best;
                    edgesList.Add(new RoadEdge { U = source, V = target, LengthMeters = length,
                        Points = new PointF[] { a, b }, IsBridge = false,
                        Surface = RoadSurface.Unknown, CostFactor = factor });
                    adj[source].Add(new HalfEdge { TargetNode = target, Weight = length, EdgeIndex = index, Forward = true });
                    adj[target].Add(new HalfEdge { TargetNode = source, Weight = length, EdgeIndex = index, Forward = false });
                    added++;
                    bridges++;
                }
            }
            return bridges;
        }
        // The packaged grid only indexes captured edges; extracted town streets must
        // be snappable too or a destination inside a town projects onto a captured
        // road hundreds of metres away (the last straight approach the player sees).
        private void RebuildSpatialGridWithMapRoads() {
            if (edges == null || gridRes < 1) return;
            if (edges.Length > 65535) return;   // grid stores ushort edge ids
            int cells = gridRes * gridRes;
            List<ushort>[] grid = new List<ushort>[cells];
            for (int i = 0; i < cells; i++) grid[i] = new List<ushort>(4);
            for (int e = 0; e < edges.Length; e++) {
                PointF[] pts = edges[e].Points;
                if (pts == null || pts.Length < 2) continue;
                for (int p = 1; p < pts.Length; p++) {
                    float a = pts[p - 1].X, b = pts[p - 1].Y, cc = pts[p].X, d = pts[p].Y;
                    int sx0 = Math.Max(0, Math.Min(gridRes - 1, (int)(Math.Min(a, cc) * gridRes)));
                    int sx1 = Math.Max(0, Math.Min(gridRes - 1, (int)(Math.Max(a, cc) * gridRes)));
                    int sy0 = Math.Max(0, Math.Min(gridRes - 1, (int)(Math.Min(b, d) * gridRes)));
                    int sy1 = Math.Max(0, Math.Min(gridRes - 1, (int)(Math.Max(b, d) * gridRes)));
                    for (int y = sy0; y <= sy1; y++) {
                        for (int x = sx0; x <= sx1; x++) grid[y * gridRes + x].Add((ushort)e);
                    }
                }
            }
            ushort[][] rebuilt = new ushort[cells][];
            for (int i = 0; i < cells; i++) rebuilt[i] = grid[i].ToArray();
            spatialGrid = rebuilt;
        }
        private struct MapRoadDraft {
            public int U;
            public int V;
            public PointF[] Points;
            public bool Protected;
        }

        private static double PolylineLength(PointF[] points) {
            double length = 0;
            for (int i = 1; i < points.Length; i++) length += DistanceMeters(points[i - 1], points[i]);
            return length;
        }

        private int ResolveMapNode(int id, PointF[] points, int[] mapNodeId) {
            if (id < MapRoadsVirtualBase) return id;
            int index = id - MapRoadsVirtualBase;
            if (index < 0 || index >= points.Length) return -1;
            if (mapNodeId[index] >= 0) return mapNodeId[index];
            int node = nodesList.Count;
            nodesList.Add(new RoadNode { U = points[index].X, V = points[index].Y });
            mapNodeId[index] = node;
            return node;
        }

        private void EnsureAdjacency(int count) {
            if (adj.Length >= count) return;
            int grownCount = Math.Max(count, adj.Length + adj.Length / 2 + 64);
            List<HalfEdge>[] grown = new List<HalfEdge>[grownCount];
            Array.Copy(adj, grown, adj.Length);
            for (int i = adj.Length; i < grown.Length; i++) grown[i] = new List<HalfEdge>(4);
            adj = grown;
        }

        // Fast overlap mask: 8 m raster of captured road pixels plus a 4-bin
        // direction mask, so duplicate supplementary edges are removed in O(1)
        // per sample instead of scanning nearby captured segments.
        private int DropCapturedParallels(List<MapRoadDraft> drafts, int originalEdges) {
            const double cellMeters = 8.0;
            int cellsX = (int)Math.Ceiling(MapScaleX / cellMeters) + 2;
            int cellsY = (int)Math.Ceiling(MapScaleY / cellMeters) + 2;
            byte[] directionMask = new byte[cellsX * cellsY];
            for (int e = 0; e < originalEdges; e++) {
                PointF[] pts = edgesList[e].Points;
                for (int p = 1; p < pts.Length; p++) {
                    double dx = (pts[p].X - pts[p - 1].X) * MapScaleX;
                    double dy = (pts[p].Y - pts[p - 1].Y) * MapScaleY;
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    if (length < 1e-9) continue;
                    double angle = Math.Atan2(dy, dx);
                    int bin = (int)Math.Round(angle / (Math.PI / 4.0));
                    bin = ((bin % 4) + 4) % 4;
                    byte bit = (byte)(1 << bin);
                    long x0 = (long)Math.Floor(Math.Min(pts[p - 1].X, pts[p].X) * MapScaleX / cellMeters);
                    long x1 = (long)Math.Floor(Math.Max(pts[p - 1].X, pts[p].X) * MapScaleX / cellMeters);
                    long y0 = (long)Math.Floor(Math.Min(pts[p - 1].Y, pts[p].Y) * MapScaleY / cellMeters);
                    long y1 = (long)Math.Floor(Math.Max(pts[p - 1].Y, pts[p].Y) * MapScaleY / cellMeters);
                    for (long x = x0; x <= x1; x++) {
                        for (long y = y0; y <= y1; y++) {
                            int index = (int)(y * cellsX + x);
                            if (index >= 0 && index < directionMask.Length) directionMask[index] |= bit;
                        }
                    }
                }
            }
            List<MapRoadDraft> keep = new List<MapRoadDraft>(drafts.Count);
            int removed = 0;
            for (int i = 0; i < drafts.Count; i++) {
                MapRoadDraft d = drafts[i];
                PointF[] pts = d.Points;
                int samples = 0, covered = 0;
                for (int p = 0; p < pts.Length; p++) {
                    int q0 = p > 0 ? p - 1 : p;
                    int q1 = p < pts.Length - 1 ? p + 1 : p;
                    double dx = (pts[q1].X - pts[q0].X) * MapScaleX;
                    double dy = (pts[q1].Y - pts[q0].Y) * MapScaleY;
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    if (length < 1e-9) continue;
                    samples++;
                    int cx = (int)Math.Floor(pts[p].X * MapScaleX / cellMeters);
                    int cy = (int)Math.Floor(pts[p].Y * MapScaleY / cellMeters);
                    int wantBin = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4.0));
                    wantBin = ((wantBin % 4) + 4) % 4;
                    byte want = (byte)(1 << wantBin);
                    byte adjacent = (byte)(want | (want << 1) | (want >> 1) | ((want & 1) << 3) | ((want & 8) >> 3));
                    for (int ox = -1; ox <= 1 && covered == samples - 1; ox++) {
                        for (int oy = -1; oy <= 1; oy++) {
                            int x = cx + ox, y = cy + oy;
                            if (x < 0 || y < 0 || x >= cellsX || y >= cellsY) continue;
                            if ((directionMask[y * cellsX + x] & adjacent) != 0) { covered++; ox = 2; break; }
                        }
                    }
                }
                if (d.Protected) { keep.Add(d); continue; }   if (samples > 0 && covered * 2 >= samples) { removed++; continue; }
                keep.Add(d);
            }
            drafts.Clear();
            drafts.AddRange(keep);
            return removed;
        }

        // Captured road splines stop at town gates and disconnected spline ends.
        // A dead end is bridged to a nearby dry dead end so the router can cross
        // the gap, but the connector is penalised: ordinary roads win unless the
        // detour costs more than twice the connector distance.
        private const double ConnectorMaxMeters = 600.0;
        private const int ConnectorMaxPerNode = 4;
        private const int ConnectorMaxTotal = 600;
        private const float ConnectorCostFactor = 2.5f;

        private int AddLandConnectors() {
            if (edgesList == null || adj == null || nodes == null || gridRes < 1 || !waterMask.IsLoaded) return 0;
            int count = nodes.Length;
            bool[] deadEnd = new bool[count];
            int deadCount = 0;
            for (int i = 0; i < count; i++) {
                if (adj[i].Count <= 1) { deadEnd[i] = true; deadCount++; }
            }
            if (deadCount < 2) return 0;
            int cells = gridRes * gridRes;
            List<int>[] cellNodes = new List<int>[cells];
            for (int i = 0; i < count; i++) {
                if (!deadEnd[i]) continue;
                int cell = GridCell(nodes[i]);
                List<int> list = cellNodes[cell];
                if (list == null) { list = new List<int>(2); cellNodes[cell] = list; }
                list.Add(i);
            }
            int radius = Math.Max(1, (int)Math.Ceiling(ConnectorMaxMeters / (15220.0 / gridRes)));
            int[] used = new int[count];
            List<ConnectorCandidate> candidates = new List<ConnectorCandidate>(32);
            int total = 0;
            for (int i = 0; i < count && total < ConnectorMaxTotal; i++) {
                if (!deadEnd[i]) continue;
                int room = ConnectorMaxPerNode - used[i];
                if (room <= 0) continue;
                candidates.Clear();
                int gu = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(nodes[i].U * gridRes)));
                int gv = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(nodes[i].V * gridRes)));
                for (int r = 1; r <= radius; r++) {
                    for (int dx = -r; dx <= r; dx++) {
                        for (int dy = -r; dy <= r; dy++) {
                            if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                            int cx = gu + dx;
                            int cy = gv + dy;
                            if (cx < 0 || cx >= gridRes || cy < 0 || cy >= gridRes) continue;
                            List<int> list = cellNodes[cy * gridRes + cx];
                            if (list == null) continue;
                            for (int k = 0; k < list.Count; k++) {
                                int j = list[k];
                                if (j <= i || used[j] >= ConnectorMaxPerNode) continue;
                                double d = DistanceMeters(nodes[i].U, nodes[i].V, nodes[j].U, nodes[j].V);
                                if (d < 1.0 || d > ConnectorMaxMeters) continue;
                                candidates.Add(new ConnectorCandidate { Distance = d, Node = j });
                            }
                        }
                    }
                    if (candidates.Count > 0 && r >= 2) {
                        candidates.Sort(CompareConnectorCandidates);
                        if (candidates[0].Distance < (r - 1) * (MapScaleX / gridRes)) break;
                    }
                }
                candidates.Sort(CompareConnectorCandidates);
                for (int k = 0; k < candidates.Count; k++) {
                    if (room <= 0 || total >= ConnectorMaxTotal) break;
                    int j = candidates[k].Node;
                    if (used[j] >= ConnectorMaxPerNode) continue;
                    PointF a = new PointF(nodes[i].U, nodes[i].V);
                    PointF b = new PointF(nodes[j].U, nodes[j].V);
                    if (!waterMask.AllowsConnector(a, b)) continue;
                    float length = (float)candidates[k].Distance;
                    PointF[] points = new PointF[] { a, b };
                    int index = edgesList.Count;
                    edgesList.Add(new RoadEdge { U = i, V = j, LengthMeters = length, Points = points,
                        IsBridge = false, Surface = RoadSurface.Unknown, CostFactor = ConnectorCostFactor });
                    adj[i].Add(new HalfEdge { TargetNode = j, Weight = length, EdgeIndex = index, Forward = true });
                    adj[j].Add(new HalfEdge { TargetNode = i, Weight = length, EdgeIndex = index, Forward = false });
                    used[i]++;
                    used[j]++;
                    room--;
                    total++;
                }
            }
            return total;
        }

        private int GridCell(RoadNode node) {
            int gu = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(node.U * gridRes)));
            int gv = Math.Max(0, Math.Min(gridRes - 1, (int)Math.Floor(node.V * gridRes)));
            return gv * gridRes + gu;
        }

        private struct ConnectorCandidate {
            public double Distance;
            public int Node;
        }

        private static int CompareConnectorCandidates(ConnectorCandidate a, ConnectorCandidate b) {
            return a.Distance.CompareTo(b.Distance);
        }

        public struct SnapResult {
            public int EdgeIndex;
            public PointF Projected;
            public double DistanceMeters;
            public int SegmentIndex;
            public double SegmentT;
        }

        private bool AllowsRoadApproach(PointF point, PointF projected, int edgeIndex, double distance) {
            // A map water pixel beneath a known bridge must not reject a player
            // already on its deck. The exemption is confined to the deck width.
            return waterMask.IsLoaded && (waterMask.AllowsConnector(point,projected) ||
                (edges[edgeIndex].IsBridge && distance <= 8.0));
        }

        public SnapResult SnapToComponent(PointF pt, int targetComponent, double maxSearchMeters = 3500.0, RoadRouteMode mode = RoadRouteMode.Shortest) {
            if (!IsLoaded || edges == null || edges.Length == 0)
                return new SnapResult { EdgeIndex = -1, DistanceMeters = double.MaxValue };

            int gu = (int)Math.Floor(pt.X * gridRes);
            int gv = (int)Math.Floor(pt.Y * gridRes);
            gu = Math.Max(0, Math.Min(gridRes - 1, gu));
            gv = Math.Max(0, Math.Min(gridRes - 1, gv));

            HashSet<int> inspected = new HashSet<int>();
            int maxRadius = Math.Max(1, (int)Math.Ceiling(maxSearchMeters / (15220.0 / gridRes)));

            double bestDist = double.MaxValue;
            SnapResult bestSnap = new SnapResult { EdgeIndex = -1, DistanceMeters = double.MaxValue };

            for (int r = 0; r <= maxRadius; r++) {
                for (int dx = -r; dx <= r; dx++) {
                    for (int dy = -r; dy <= r; dy++) {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int cx = gu + dx;
                        int cy = gv + dy;
                        if (cx < 0 || cx >= gridRes || cy < 0 || cy >= gridRes) continue;

                        ushort[] cellEdges = spatialGrid[cy * gridRes + cx];
                        if (cellEdges == null) continue;

                        for (int e = 0; e < cellEdges.Length; e++) {
                            int eid = cellEdges[e];
                            if (inspected.Contains(eid)) continue;
                            inspected.Add(eid);

                            if (nodeComponent[edges[eid].U] != targetComponent || !AllowsSurface(edges[eid],mode)) continue;

                            PointF[] pts = edges[eid].Points;
                            for (int i = 0; i < pts.Length - 1; i++) {
                                PointF a = pts[i];
                                PointF b = pts[i + 1];
                                double ldx = b.X - a.X;
                                double ldy = b.Y - a.Y;
                                double l2 = ldx * ldx + ldy * ldy;
                                double t = 0;
                                if (l2 > 1e-12) {
                                    t = Math.Max(0.0, Math.Min(1.0, ((pt.X - a.X) * ldx + (pt.Y - a.Y) * ldy) / l2));
                                }
                                PointF proj = new PointF((float)(a.X + t * ldx), (float)(a.Y + t * ldy));
                                double d = DistanceMeters(pt, proj);
                                if (d <= maxSearchMeters && d < bestDist && AllowsRoadApproach(pt,proj,eid,d)) {
                                    bestDist = d;
                                    bestSnap = new SnapResult {
                                        EdgeIndex = eid,
                                        Projected = proj,
                                        DistanceMeters = d,
                                        SegmentIndex = i,
                                        SegmentT = t
                                    };
                                }
                            }
                        }
                    }
                }
                double ringMinMeters = Math.Max(0, (r - 1) * (15220.0 / gridRes));
                if (bestDist < ringMinMeters) break;
            }

            return bestSnap;
        }

        SnapResult ProjectToSegment(PointF pt,int edgeIndex,int segment) {
            PointF a=edges[edgeIndex].Points[segment],b=edges[edgeIndex].Points[segment+1];
            double dx=b.X-a.X,dy=b.Y-a.Y;
            double length2=dx*dx+dy*dy;
            double t=length2>1e-12?Math.Max(0,Math.Min(1,((pt.X-a.X)*dx+(pt.Y-a.Y)*dy)/length2)):0;
            var projected=new PointF((float)(a.X+t*(b.X-a.X)),(float)(a.Y+t*(b.Y-a.Y)));
            return new SnapResult { EdgeIndex=edgeIndex,SegmentIndex=segment,SegmentT=t,Projected=projected,DistanceMeters=DistanceMeters(pt,projected) };
        }
        static double SnapTolerance(double nearest) { return nearest<=5?2:nearest<=50?5:100; }
        static void InsertSnap(List<SnapResult> results,SnapResult snap,int limit) {
            int index=0;while(index<results.Count && results[index].DistanceMeters<=snap.DistanceMeters)index++;
            if(index>=limit)return;
            results.Insert(index,snap);if(results.Count>limit)results.RemoveAt(results.Count-1);
        }

        public List<SnapResult> SnapToRoadCandidates(PointF pt, double maxSearchMeters = 3000.0, int maxCandidates = 8, RoadRouteMode mode = RoadRouteMode.Shortest) {
            List<SnapResult> results = new List<SnapResult>(maxCandidates);
            if (!IsLoaded || edges == null || edges.Length == 0) return results;

            int gu = (int)Math.Floor(pt.X * gridRes);
            int gv = (int)Math.Floor(pt.Y * gridRes);
            gu = Math.Max(0, Math.Min(gridRes - 1, gu));
            gv = Math.Max(0, Math.Min(gridRes - 1, gv));

            HashSet<int> inspected = new HashSet<int>();
            int maxRadius = Math.Max(1, (int)Math.Ceiling(maxSearchMeters / (15220.0 / gridRes)));

            for (int r = 0; r <= maxRadius; r++) {
                for (int dx = -r; dx <= r; dx++) {
                    for (int dy = -r; dy <= r; dy++) {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int cx = gu + dx;
                        int cy = gv + dy;
                        if (cx < 0 || cx >= gridRes || cy < 0 || cy >= gridRes) continue;

                        ushort[] cellEdges = spatialGrid[cy * gridRes + cx];
                        if (cellEdges == null) continue;

                        for (int e = 0; e < cellEdges.Length; e++) {
                            int eid = cellEdges[e];
                            if (inspected.Contains(eid)) continue;
                            inspected.Add(eid);

                            if(!AllowsSurface(edges[eid],mode)) continue;
                            PointF[] pts = edges[eid].Points;
                            var best=new SnapResult { EdgeIndex=-1,DistanceMeters=double.MaxValue };
                            for (int i = 0; i < pts.Length - 1; i++) {
                                var snap=ProjectToSegment(pt,eid,i);double distance=snap.DistanceMeters;
                                if(distance>maxSearchMeters || distance>=best.DistanceMeters || (results.Count==maxCandidates && distance>=results[results.Count-1].DistanceMeters))continue;
                                if(!AllowsRoadApproach(pt,snap.Projected,eid,distance))continue;
                                best=snap;
                            }
                            if(best.EdgeIndex>=0)InsertSnap(results,best,maxCandidates);
                        }
                    }
                }
                // Unvisited cells cannot contribute within the retained tolerance.
                // This also works when fewer than eight roads exist nearby.
                if(results.Count>0 && r>=2) {
                    double limit=Math.Min(maxSearchMeters,results[0].DistanceMeters+SnapTolerance(results[0].DistanceMeters));
                    if(results.Count==maxCandidates)limit=Math.Min(limit,results[results.Count-1].DistanceMeters);
                    if(limit<(r-1)*(MapScaleX/gridRes))break;
                }
            }

            // Stay on the nearest road when already on it. From open ground,
            // allow nearby alternatives on the same bank to reach a connected road.
            if (results.Count > 0) {
                double nearest = results[0].DistanceMeters;
                double tolerance = SnapTolerance(nearest);
                results.RemoveAll(candidate => candidate.DistanceMeters > nearest + tolerance);
                // Extra projections must never evict a different road's best
                // candidate at dense junctions. Keep the existing eight first.
                if(maxCandidates>1) {
                    var alternatives=new List<SnapResult>();
                    foreach(var primary in results) {
                        var edge=edges[primary.EdgeIndex];
                        // No shortcut across different parts of a surveyed bridge.
                        if(edge.IsBridge)continue;
                        double primaryAlong=Along(edge,primary);
                        for(int i=0;i<edge.Points.Length-1;i++) {
                            var snap=ProjectToSegment(pt,primary.EdgeIndex,i);double d=snap.DistanceMeters;
                            if(d>nearest+tolerance || d>maxSearchMeters || DistanceMeters(primary.Projected,snap.Projected)<.01)continue;
                            if(snap.SegmentT==0 && i>0 && ProjectToSegment(pt,snap.EdgeIndex,i-1).DistanceMeters<=d+.0001)continue;
                            if(snap.SegmentT==1 && i+2<edge.Points.Length && ProjectToSegment(pt,snap.EdgeIndex,i+1).DistanceMeters<d-.0001)continue;
                            if(Math.Abs(Along(edge,snap)-primaryAlong)<35)continue;
                            if(AllowsRoadApproach(pt,snap.Projected,snap.EdgeIndex,d))InsertSnap(alternatives,snap,maxCandidates);
                        }
                    }
                    results.AddRange(alternatives);
                }
                // Dense isolated service roads can fill every candidate slot.
                // From open ground, retain a dry approach to the main road too.
                if(maxCandidates > 1 && nearest > 15.0 && !results.Any(candidate => nodeComponent[edges[candidate.EdgeIndex].U] == mainComponentId)) {
                    SnapResult main = SnapToComponent(pt,mainComponentId,maxSearchMeters,mode);
                    if(main.EdgeIndex >= 0) results.Add(main);
                }
            }

            return results;
        }

        public SnapResult SnapToRoad(PointF pt, double maxSearchMeters = 2000.0) {
            List<SnapResult> cands = SnapToRoadCandidates(pt, maxSearchMeters, 1);
            if (cands.Count > 0) return cands[0];
            return new SnapResult { EdgeIndex = -1, DistanceMeters = double.MaxValue };
        }

        private class MinHeap {
            private struct Entry {
                public double Priority;
                public int Node;
            }
            private Entry[] heap;
            public int Count { get; private set; }

            public MinHeap(int initialCapacity = 512) {
                heap = new Entry[initialCapacity];
            }

            public void Clear() {
                Count = 0;
            }

            public void Push(double priority, int node) {
                if (Count == heap.Length) {
                    Array.Resize(ref heap, heap.Length * 2);
                }
                int i = Count++;
                heap[i] = new Entry { Priority = priority, Node = node };
                while (i > 0) {
                    int p = (i - 1) >> 1;
                    if (heap[p].Priority <= heap[i].Priority) break;
                    Entry tmp = heap[p]; heap[p] = heap[i]; heap[i] = tmp;
                    i = p;
                }
            }

            public double Minimum { get { return Count==0?double.PositiveInfinity:heap[0].Priority; } }
            public int Pop() {
                int res = heap[0].Node;
                Count--;
                if (Count > 0) {
                    heap[0] = heap[Count];
                    int i = 0;
                    while (true) {
                        int left = (i << 1) + 1;
                        if (left >= Count) break;
                        int right = left + 1;
                        int best = (right < Count && heap[right].Priority < heap[left].Priority) ? right : left;
                        if (heap[i].Priority <= heap[best].Priority) break;
                        Entry tmp = heap[i]; heap[i] = heap[best]; heap[best] = tmp;
                        i = best;
                    }
                }
                return res;
            }
        }

        public RoadRoute FindRoute(PointF playerPt, PointF targetPt) { return FindRoute(playerPt,targetPt,RoadRouteMode.Shortest); }
        public RoadRoute FindRoute(PointF playerPt, PointF targetPt, RoadRouteMode mode) {
            if(!Enum.IsDefined(typeof(RoadRouteMode),mode)) throw new ArgumentOutOfRangeException("mode");
            RoadRoute result = new RoadRoute {
                Success = false, Mode = mode,
                PlayerPoint = playerPt,
                TargetPoint = targetPt,
                TotalDistanceMeters = DistanceMeters(playerPt, targetPt)
            };

            if (!IsLoaded || !waterMask.IsLoaded) return result;

            // The map.png-derived network roughly doubles node count; searching the
            // same eight candidates per side would quadruple A* work for little gain.
            int candidateLimit = mapRoadsLoaded ? 4 : 8;
            List<SnapResult> startCands = SnapToRoadCandidates(playerPt, 1000.0, candidateLimit, mode);
            List<SnapResult> endCands = SnapToRoadCandidates(targetPt, 1000.0, candidateLimit, mode);

            if (startCands.Count == 0 || endCands.Count == 0) {
                return result;
            }

            // 1. Try all candidate combinations where compStart == compEnd (pure land/bridge route)
            RoadRoute bestConnectedRoute = null;
            double bestTotalDist = double.MaxValue;

            for (int s = 0; s < startCands.Count; s++) {
                SnapResult snapStart = startCands[s];
                int compStart = nodeComponent[edges[snapStart.EdgeIndex].U];

                for (int e = 0; e < endCands.Count; e++) {
                    SnapResult snapEnd = endCands[e];
                    int compEnd = nodeComponent[edges[snapEnd.EdgeIndex].U];

                    if (compStart == compEnd) {
                        RoadRoute candidateRoute = ComputeConnectedRoute(playerPt, targetPt, snapStart, snapEnd, mode);
                        if (candidateRoute != null && candidateRoute.Success) {
                            double cost=mode==RoadRouteMode.Quickest?candidateRoute.EstimatedSeconds:candidateRoute.TotalDistanceMeters;
                            if (cost < bestTotalDist) {
                                bestTotalDist = cost;
                                bestConnectedRoute = candidateRoute;
                            }
                        }
                    }
                }
            }

            if (bestConnectedRoute != null) {
                return bestConnectedRoute;
            }

            // Road navigation must never invent a swimming/boat crossing.
            return result;
        }

        static bool AllowsSurface(RoadEdge edge,RoadRouteMode mode) { return mode!=RoadRouteMode.PavedOnly || edge.Surface==RoadSurface.Paved; }
        // Planning assumptions in metres/second: 60 km/h paved, 30 dirt, 20 unknown.
        // Dry entry/exit approaches use 5 km/h. These are estimates, not live traffic.
        static double Speed(RoadEdge edge) { return edge.Surface==RoadSurface.Paved?60/3.6:edge.Surface==RoadSurface.Dirt?30/3.6:20/3.6; }
        static double Cost(RoadEdge edge,double length,RoadRouteMode mode) { return mode==RoadRouteMode.Quickest?length*edge.CostFactor/Speed(edge):length*edge.CostFactor; }
        static double Along(RoadEdge edge,SnapResult snap) {
            double total=0,before=0;
            for(int i=0;i<edge.Points.Length-1;i++) {
                double length=DistanceMeters(edge.Points[i],edge.Points[i+1]);total+=length;
                if(i<snap.SegmentIndex)before+=length;
                else if(i==snap.SegmentIndex)before+=length*snap.SegmentT;
            }
            return total>0?edge.LengthMeters*Math.Max(0,Math.Min(1,before/total)):0;
        }
        static void Measure(RoadRoute result,RoadEdge edge,double length) {
            result.RoadDistanceMeters+=length;result.EstimatedSeconds+=length/(result.Mode==RoadRouteMode.Walking?5/3.6:Speed(edge));
            if(edge.Surface==RoadSurface.Paved)result.PavedDistanceMeters+=length;
            else if(edge.Surface==RoadSurface.Dirt)result.DirtDistanceMeters+=length;
            else result.UnknownDistanceMeters+=length;
        }
        static void AppendSection(List<PointF> line,RoadEdge edge,SnapResult snap,bool toNode,bool atU) {
            if(toNode) {
                line.Add(snap.Projected);
                if(atU)for(int i=snap.SegmentIndex;i>=0;i--)line.Add(edge.Points[i]);
                else for(int i=snap.SegmentIndex+1;i<edge.Points.Length;i++)line.Add(edge.Points[i]);
            } else {
                if(atU)for(int i=1;i<=snap.SegmentIndex;i++)line.Add(edge.Points[i]);
                else for(int i=edge.Points.Length-2;i>snap.SegmentIndex;i--)line.Add(edge.Points[i]);
                line.Add(snap.Projected);
            }
        }
        private RoadRoute ComputeConnectedRoute(PointF playerPt, PointF targetPt, SnapResult snapStart, SnapResult snapEnd,RoadRouteMode mode) {
            var result=new RoadRoute { Mode=mode,PlayerPoint=playerPt,TargetPoint=targetPt,
                RoadEntryPoint=snapStart.Projected,RoadExitPoint=snapEnd.Projected,
                EntryDistanceMeters=snapStart.DistanceMeters,ExitDistanceMeters=snapEnd.DistanceMeters };
            RoadEdge first=edges[snapStart.EdgeIndex],last=edges[snapEnd.EdgeIndex];
            if(!AllowsSurface(first,mode)||!AllowsSurface(last,mode))return result;
            double startU=Along(first,snapStart),startV=first.LengthMeters-startU;
            double endU=Along(last,snapEnd),endV=last.LengthMeters-endU;
            bool direct=snapStart.EdgeIndex==snapEnd.EdgeIndex;
            double best=direct?Cost(first,Math.Abs(endU-startU),mode):double.PositiveInfinity;
            int reached=-1;bool endAtU=true;
            var line=new List<PointF>();var junctions=new List<int>();
            lock(searchLock) {
                currentEpoch++;if(currentEpoch==0) { Array.Clear(visitedEpoch,0,visitedEpoch.Length);currentEpoch=1; }
                pq.Clear();
                int[] roots={first.U,first.V};double[] rootLengths={startU,startV};
                for(int i=0;i<2;i++) {
                    int n=roots[i];double cost=Cost(first,rootLengths[i],mode);
                    if(visitedEpoch[n]==currentEpoch&&gScores[n]<=cost)continue;
                    visitedEpoch[n]=currentEpoch;gScores[n]=cost;cameFrom[n]=new HalfEdge { EdgeIndex=-1,Forward=i==0 };
                    pq.Push(cost+Heuristic(n,snapEnd.Projected,mode),n);
                }
                // The destination is a virtual point inside its edge. Include its
                // remaining edge cost before choosing which endpoint wins.
                while(pq.Count>0&&pq.Minimum<best) {
                    int current=pq.Pop();double currentCost=gScores[current];
                    if(current==last.U||current==last.V) {
                        bool atU=current==last.U&&(last.U!=last.V||endU<=endV);
                        double cost=currentCost+Cost(last,atU?endU:endV,mode);
                        if(cost<best) { best=cost;reached=current;endAtU=atU;direct=false; }
                    }
                    foreach(var half in adj[current]) {
                        var edge=edges[half.EdgeIndex];if(!AllowsSurface(edge,mode))continue;
                        int next=half.TargetNode;double cost=currentCost+Cost(edge,edge.LengthMeters,mode);
                        if(visitedEpoch[next]==currentEpoch&&gScores[next]<=cost)continue;
                        visitedEpoch[next]=currentEpoch;gScores[next]=cost;cameFrom[next]=half;
                        pq.Push(cost+Heuristic(next,snapEnd.Projected,mode),next);
                    }
                }
                if(direct) {
                    line.Add(snapStart.Projected);
                    if(startU<=endU)for(int i=snapStart.SegmentIndex+1;i<=snapEnd.SegmentIndex;i++)line.Add(first.Points[i]);
                    else for(int i=snapStart.SegmentIndex;i>snapEnd.SegmentIndex;i--)line.Add(first.Points[i]);
                    line.Add(snapEnd.Projected);Measure(result,first,Math.Abs(endU-startU));
                } else {
                    if(reached<0)return result;
                    var path=new List<HalfEdge>();int trace=reached;
                    while(cameFrom[trace].EdgeIndex>=0) {
                        var half=cameFrom[trace];path.Add(half);trace=half.Forward?edges[half.EdgeIndex].U:edges[half.EdgeIndex].V;
                        if(path.Count>nodes.Length)throw new InvalidDataException("Road predecessor cycle.");
                    }
                    path.Reverse();AppendSection(line,first,snapStart,true,cameFrom[trace].Forward);
                    Measure(result,first,cameFrom[trace].Forward?startU:startV);
                    if(adj[trace].Count>=3)junctions.Add(line.Count-1);
                    foreach(var half in path) {
                        var edge=edges[half.EdgeIndex];var points=edge.Points;
                        if(half.Forward)for(int i=1;i<points.Length;i++)line.Add(points[i]);
                        else for(int i=points.Length-2;i>=0;i--)line.Add(points[i]);
                        Measure(result,edge,edge.LengthMeters);
                        if(adj[half.TargetNode].Count>=3)junctions.Add(line.Count-1);
                    }
                    AppendSection(line,last,snapEnd,false,endAtU);Measure(result,last,endAtU?endU:endV);
                }
            }
            if(result.RoadDistanceMeters<1&&DistanceMeters(playerPt,targetPt)>35)return result;
            result.TotalDistanceMeters=result.EntryDistanceMeters+result.RoadDistanceMeters+result.ExitDistanceMeters;
            result.EstimatedSeconds+=(result.EntryDistanceMeters+result.ExitDistanceMeters)/(5/3.6);
            result.Polyline=line.ToArray();result.JunctionIndices=junctions.ToArray();result.Success=true;return result;
        }
        double Heuristic(int node,PointF target,RoadRouteMode mode) {
            double distance=DistanceMeters(nodes[node].U,nodes[node].V,target.X,target.Y)*heuristicDistanceFactor;
            return mode==RoadRouteMode.Quickest?distance/(60/3.6):distance;
        }
    }
}


















