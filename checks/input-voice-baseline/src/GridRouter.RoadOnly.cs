using System;
using System.Collections.Generic;
using System.Drawing;

namespace ScumMiniMap {
    internal sealed partial class GridRouter {
        // ------------------------------------------------------------------
        // 驾车走线 (road-only) router.
        //
        // The generic hierarchical router can fail on long road-only routes because
        // its 16 m / 64 m corridors follow coarse road blocks and sometimes miss the
        // real 4 m road detours.  Road-only mode is simple enough to solve exactly:
        // build a compact graph of the 4 m road/trail cells once and run A* directly
        // on it.  ~640k nodes, a few million edges, few ms per route.
        // ------------------------------------------------------------------
        sealed class RoadGraph {
            internal int[] CellOfNode;      // node -> fine cell index
            internal int[] AdjStart;        // CSR offsets
            internal int[] Adj;             // neighbour node ids
            internal float[] G;
            internal int[] Parent;
            internal uint[] Seen;
            internal uint[] Closed;
            internal uint Epoch;
            internal int NodeCount;
            internal MinHeap Heap = new MinHeap();
        }

        RoadGraph roadGraph;
        internal double DebugRoadBuildMs;
        internal double DebugRoadSnapMs;
        internal double DebugRoadAStarMs;
        internal double DebugRoadPathCost;
        internal int DebugRoadStartCell;
        internal int DebugRoadTargetCell;
        readonly object roadGraphLock = new object();
        // navA27 (P0-2): 1 = water/blocked/undefined or vehicle-impassable terrain class 4,
        // built together with the road graph and used by the connector legality DDA.
        byte[] roadIllegalMask;

        void EnsureRoadOnlyGraph() {
            lock (roadGraphLock) {
                if (roadGraph != null) return;
                GridLevel fine = levels[LevelFine];
                int n = fine.CellCount;
                byte[] data = fine.Data;
                byte[] terrain = terrainFine;
                int terrainCells = terrain == null ? 0 : terrain.Length;
                int[] cellToNode = new int[n];
                for (int i = 0; i < n; i++) cellToNode[i] = -1;
                byte[] illegal = new byte[n];
                List<int> cells = new List<int>(700000);
                for (int i = 0; i < n; i++) {
                    byte cost = data[i];
                    if (cost == CostUndefined || cost == CostWater || cost == CostBlocked ||
                        (terrainCells > 0 && i < terrainCells && terrain[i] >= 4)) illegal[i] = 1;
                    if (cost <= CostDirt) {
                        cellToNode[i] = cells.Count;
                        cells.Add(i);
                    }
                }
                int count = cells.Count;
                int[] cellOfNode = cells.ToArray();
                int[] degree = new int[count];
                int width = fine.Width, height = fine.Height;
                for (int c = 0; c < count; c++) {
                    int cell = cellOfNode[c];
                    int x = cell % width, y = cell / width;
                    for (int k = 0; k < 8; k++) {
                        int nx = x + NeighborX[k], ny = y + NeighborY[k];
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        if (!RoadDiagonalClear(fine, terrain, x, y, nx, ny)) continue;
                        if (cellToNode[ny * width + nx] >= 0) degree[c]++;
                    }
                }
                int[] adjStart = new int[count + 1];
                for (int c = 0; c < count; c++) adjStart[c + 1] = adjStart[c] + degree[c];
                int[] adj = new int[adjStart[count]];
                int[] fill = new int[count];
                for (int c = 0; c < count; c++) {
                    int cell = cellOfNode[c];
                    int x = cell % width, y = cell / width;
                    for (int k = 0; k < 8; k++) {
                        int nx = x + NeighborX[k], ny = y + NeighborY[k];
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        if (!RoadDiagonalClear(fine, terrain, x, y, nx, ny)) continue;
                        int to = cellToNode[ny * width + nx];
                        if (to >= 0) adj[adjStart[c] + fill[c]++] = to;
                    }
                }
                RoadGraph graph = new RoadGraph();
                graph.CellOfNode = cellOfNode;
                graph.AdjStart = adjStart;
                graph.Adj = adj;
                graph.NodeCount = count;
                graph.G = new float[count];
                graph.Parent = new int[count];
                graph.Seen = new uint[count];
                graph.Closed = new uint[count];
                graph.Epoch = 1;
                roadGraph = graph;
                roadIllegalMask = illegal;
                // Let the transient cell -> node map be collected.
                cellToNode = null;
                fill = null;
                degree = null;
                cells = null;
            }
        }

        // navA27 (P0-2): nearest LEGAL road node in the given road component.
        //
        // The old ring scan returned the first road cell it touched: that is neither the
        // Euclidean nearest (Chebyshev ring + in-ring scan order bias) nor legal (the
        // auto-added straight connector was never checked, so routes could drive across
        // water/blocked cells to reach the road).
        //
        // Work order (cheap first, incremental):
        //   1) collect the component's cost<=2 cells in growing radius batches and their
        //      exact squared distances only (no legality sampling);
        //   2) sort nearest-first and run the legality DDA from the nearest candidate;
        //      the first legal one is the true nearest legal point of the batch;
        //   3) stop as soon as the best legal distance is below the minimum possible
        //      distance of all not-yet-collected rings ((r-1)*minCell lower bound), so a
        //      normal 148 m roadside access only scans a few rings instead of the full
        //      1.6 km disc.
        struct SnapCandidate {
            internal int Cell;
            internal double DistanceSq;
        }

        static int CompareSnapCandidates(SnapCandidate a, SnapCandidate b) {
            if (a.DistanceSq < b.DistanceSq) return -1;
            if (a.DistanceSq > b.DistanceSq) return 1;
            return a.Cell.CompareTo(b.Cell);
        }

        void CollectRoadSnapCandidates(ushort[] labels, ushort component, double px, double py,
            int cx, int cy, GridLevel fine, byte[] data, int fromRing, int toRing, List<SnapCandidate> list) {
            int width = fine.Width, height = fine.Height;
            double mx = fine.MetersX, my = fine.MetersY;
            // Incremental square scan: the old nested ring loops re-iterated the whole
            // (2r+1)^2 square for every ring (O(r^3) skip iterations, ~85M at r=400).
            // We only need the newly added Chebyshev band, so scan the outer square and
            // skip cells already collected in a previous stage: O(r^2).
            int x0 = cx - toRing, x1 = cx + toRing;
            int y0 = cy - toRing, y1 = cy + toRing;
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 >= width) x1 = width - 1;
            if (y1 >= height) y1 = height - 1;
            int firstRing = fromRing;
            for (int y = y0; y <= y1; y++) {
                int dy = y > cy ? y - cy : cy - y;
                int row = y * width;
                for (int x = x0; x <= x1; x++) {
                    int dx = x > cx ? x - cx : cx - x;
                    int cheb = dx > dy ? dx : dy;
                    if (cheb < firstRing) continue;
                    int cell = row + x;
                    if (data[cell] > CostDirt) continue;
                    if (labels != null && labels[cell] != component) continue;
                    double ddx = (x + 0.5) - px;
                    double ddy = (y + 0.5) - py;
                    SnapCandidate candidate;
                    candidate.Cell = cell;
                    candidate.DistanceSq = ddx * ddx * mx * mx + ddy * ddy * my * my;
                    list.Add(candidate);
                }
            }
        }

        int RoadNodeForCell(int cell) {
            // map fine cell -> node id via binary search over CellOfNode (sorted ascending)
            int[] cells = roadGraph.CellOfNode;
            int lo = 0, hi = cells.Length - 1;
            while (lo <= hi) {
                int mid = (lo + hi) >> 1;
                if (cells[mid] == cell) return mid;
                if (cells[mid] < cell) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }

        // Find a drivable approach when no line of sight exists from the endpoint to this road component.
        int RoadAccess(PointF endpoint, ushort component, int radius, out List<PointF> approach, out double length) {
            approach = null;
            int direct = SnapLegalRoadNode(fineRoadLabel, component, endpoint.X, endpoint.Y, radius, out length);
            if (direct >= 0) return direct;
            GridLevel f = levels[LevelFine];
            int start = (int)((double)endpoint.Y*f.Height)*f.Width + (int)((double)endpoint.X*f.Width);
            Dictionary<int,int> parent = new Dictionary<int,int>();
            Dictionary<int,float> distance = new Dictionary<int,float>();
            MinHeap queue = new MinHeap();
            parent[start]=start; distance[start]=0; queue.Push(0,0,start);
            double limit=radius*Math.Min(f.MetersX,f.MetersY);
            while (queue.Count > 0) {
                float d;
                int cell=queue.Pop(out d);
                if (d!=distance[cell]) continue;
                if (fineRoadLabel[cell]==component) {
                    List<PointF> raw = new List<PointF>();
                    int c=cell;
                    while(true) {
                        raw.Add(FineCenter(f,c%f.Width,c/f.Width));
                        if(c==start) break;
                        c=parent[c];
                    }
                    raw.Add(endpoint);
                    raw.Reverse();
                    approach=new List<PointF>();
                    approach.Add(endpoint);
                    int anchor=0;
                    for(int j=2;j<raw.Count;j++) {
                        if(RoadChordLegalStrict(raw[anchor],raw[j]))continue;
                        anchor=j-1;
                        approach.Add(raw[anchor]);
                    }
                    approach.Add(raw[raw.Count-1]);
                    length=0;
                    for(int j=1;j<approach.Count;j++)length+=RoadRouter.DistanceMeters(approach[j-1],approach[j]);
                    if(length>limit) return -1;
                    return RoadNodeForCell(cell);
                }
                int x=cell%f.Width,y=cell/f.Width;
                for(int k=0;k<8;k++){
                    int nx=x+NeighborX[k],ny=y+NeighborY[k];
                    if(nx<0 || ny<0 || nx>=f.Width || ny>=f.Height)continue;
                    int next=ny*f.Width+nx;
                    if(roadIllegalMask[next]!=0 || !RoadDiagonalClear(f,terrainFine,x,y,nx,ny))continue;
                    float candidate=d+(float)(nx==x?f.MetersY:ny==y?f.MetersX:Math.Sqrt(f.MetersX*f.MetersX+f.MetersY*f.MetersY));
                    float old;
                    if(candidate>limit || (distance.TryGetValue(next,out old) && candidate>=old))continue;
                    parent[next]=cell;distance[next]=candidate;queue.Push(candidate,0,next);
                }
            }
            return -1;
        }

        // Batch radii (rings of 4 m cells) for the incremental outward scan.
        static readonly int[] SnapRingStages = new int[] { 8, 16, 24, 32, 48, 64, 96, 128, 192, 256, 320, 400 };

        int SnapLegalRoadNode(ushort[] labels, ushort component, float u, float v, int maxRadius, out double distanceMeters) {
            distanceMeters = double.MaxValue;
            GridLevel fine = levels[LevelFine];
            int width = fine.Width, height = fine.Height;
            byte[] data = fine.Data;
            double px = u * width, py = v * height;
            int cx = ClampCell((int)Math.Floor(px), width);
            int cy = ClampCell((int)Math.Floor(py), height);
            PointF player = new PointF(u, v);
            double minCell = Math.Min(fine.MetersX, fine.MetersY);
            List<SnapCandidate> candidates = new List<SnapCandidate>(512);
            HashSet<int> rejected = null;
            int collectedRing = -1;
            int bestCell = -1;
            double bestDistSq = double.MaxValue;
            int stage = 0;
            while (collectedRing < maxRadius) {
                int targetRing = stage < SnapRingStages.Length ? SnapRingStages[stage] : maxRadius;
                stage++;
                if (targetRing > maxRadius) targetRing = maxRadius;
                if (targetRing <= collectedRing) targetRing = maxRadius;
                int before = candidates.Count;
                CollectRoadSnapCandidates(labels, component, px, py, cx, cy, fine, data, collectedRing + 1, targetRing, candidates);
                collectedRing = targetRing;
                if (candidates.Count > before) candidates.Sort(CompareSnapCandidates);
                for (int i = 0; i < candidates.Count; i++) {
                    SnapCandidate candidate = candidates[i];
                    // Candidates are distance sorted: once one is at least as far as the
                    // best legal result it can only tie/ worsen the answer (cell id order
                    // keeps ties deterministic), so further scanning is pointless.
                    if (candidate.DistanceSq > bestDistSq) break;
                    if (candidate.DistanceSq == bestDistSq && bestCell >= 0) {
                        if (candidate.Cell > bestCell) break;
                        continue;
                    }
                    if (rejected != null && rejected.Contains(candidate.Cell)) continue;
                    int cell = candidate.Cell;
                    if (RoadConnectorLegal(player, FineCenter(fine, cell % width, cell / width))) {
                        bestCell = cell;
                        bestDistSq = candidate.DistanceSq;
                        break;
                    }
                    if (rejected == null) rejected = new HashSet<int>();
                    rejected.Add(cell);
                }
                if (bestCell >= 0) {
                    // Rings not collected yet are all at least (theirRing-1)*minCell away,
                    // so ring collectedRing+1 and beyond cannot beat this distance.
                    double ringLowerBound = collectedRing * minCell;
                    if (Math.Sqrt(bestDistSq) < ringLowerBound || collectedRing >= maxRadius) break;
                }
            }
            if (bestCell < 0) return -1;
            distanceMeters = Math.Sqrt(bestDistSq);
            return RoadNodeForCell(bestCell);
        }

        // navA27 (P0-2): the auto-added straight connector (player/target -> first/last
        // road cell centre) must be drivable over the baked nav-grid.  Illegal = water(16),
        // blocked(255), undefined(0) or vehicle-impassable terrain class 4.
        //
        // This is a full DDA cell traversal (supercover) of the connector: every 4 m cell
        // the straight line touches is checked, so a narrow obstacle can never fall
        // between samples (the external-AI caveat about coarse sampling only prefiltering
        // is respected - this is the final, finest check).  The illegal mask is built once
        // together with the road graph (see EnsureRoadOnlyGraph) and is a plain byte lookup.
        bool RoadConnectorLegal(PointF from, PointF to) {
            return RoadChordLegalStrict(from, to);
        }

        ushort NearestRoadComponentAt(float u, float v, int maxRadius) {
            GridLevel fine = levels[LevelFine];
            int width = fine.Width, height = fine.Height;
            byte[] data = fine.Data;
            int cx = ClampCell((int)Math.Floor(u * width), width);
            int cy = ClampCell((int)Math.Floor(v * height), height);
            for (int r = 0; r <= maxRadius; r++) {
                for (int dy = -r; dy <= r; dy++) {
                    for (int dx = -r; dx <= r; dx++) {
                        if (r > 0 && Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        int cell = y * width + x;
                        if (data[cell] <= CostDirt) return fineRoadLabel[cell];
                    }
                }
            }
            return 0;
        }

        void ComputeRoadOnlyRoute(PointF playerPt, PointF targetPt, RoadRoute result) {
            EnsureRoadComponents();
            System.Diagnostics.Stopwatch buildWatch = System.Diagnostics.Stopwatch.StartNew();
            EnsureRoadOnlyGraph();
            DebugRoadBuildMs = buildWatch.Elapsed.TotalMilliseconds;
            GridLevel fine = levels[LevelFine];
            byte[] fineOverlay = trailOverlay == null ? null : trailOverlay.Fine;
            double straight = RoadRouter.DistanceMeters(playerPt, targetPt);
            if (straight < 0.5 && RoadChordLegalStrict(playerPt, targetPt)) {
                result.Polyline = new PointF[] { playerPt, targetPt };
                result.TotalDistanceMeters = straight;
                result.Success = true;
                return;
            }
            ushort compA = NearestRoadComponentAt(playerPt.X, playerPt.Y, 300);
            ushort compB = NearestRoadComponentAt(targetPt.X, targetPt.Y, 300);
            ushort[] candidates = new ushort[3];
            int candidateCount = 0;
            if (compA != 0) candidates[candidateCount++] = compA;
            if (compB != 0 && compB != compA) candidates[candidateCount++] = compB;
            if (fineRoadMain != 0 && fineRoadMain != compA && fineRoadMain != compB) candidates[candidateCount++] = fineRoadMain;
            const int maxConnectorCells = 400; // refuse a >1.6 km straight connector
            int startNode = -1, targetNode = -1;
            double startConnector = 0, targetConnector = 0;
            int[] nodePath = null;
            List<PointF> startApproach=null, endApproach=null;
            System.Diagnostics.Stopwatch roadWatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < candidateCount && nodePath == null; i++) {
                ushort comp = candidates[i];
                double sd, td;
                // navA27 (P0-2): true nearest *legal* access point inside this component.
                // No legal access -> try the next candidate component, then fail: a road
                // route must never invent a straight line across water/blocked ground.
                int s = RoadAccess(playerPt, comp, maxConnectorCells, out startApproach, out sd);
                if(s<0)continue;
                int t = RoadAccess(targetPt, comp, maxConnectorCells, out endApproach, out td);
                if (s < 0 || t < 0) continue;
                startNode = s; targetNode = t; startConnector = sd; targetConnector = td;
                System.Diagnostics.Stopwatch astarWatch = System.Diagnostics.Stopwatch.StartNew();
                nodePath = RoadAStar(startNode, targetNode);
                astarWatch.Stop();
                DebugRoadAStarMs = astarWatch.Elapsed.TotalMilliseconds;
                if (nodePath != null && nodePath.Length > 0) {
                    DebugRoadStartCell = roadGraph.CellOfNode[nodePath[0]];
                    DebugRoadTargetCell = roadGraph.CellOfNode[nodePath[nodePath.Length - 1]];
                    double dbg = 0;
                    for (int ni = 1; ni < nodePath.Length; ni++) {
                        int pcell = roadGraph.CellOfNode[nodePath[ni - 1]];
                        int ccell = roadGraph.CellOfNode[nodePath[ni]];
                        int px = pcell % fine.Width, py = pcell / fine.Width;
                        int cxx = ccell % fine.Width, cyy = ccell / fine.Width;
                        int dx = cxx - px, dy = cyy - py;
                        double m = dx == 0 ? Math.Abs(dy) * fine.MetersY : (dy == 0 ? Math.Abs(dx) * fine.MetersX : Math.Sqrt(fine.MetersX * fine.MetersX + fine.MetersY * fine.MetersY));
                        dbg += m * (fine.Data[ccell] == CostDirt ? 3.6 / 40.0 : 3.6 / 60.0);
                    }
                    DebugRoadPathCost = dbg;
                }
            }
            DebugRoadSnapMs = roadWatch.Elapsed.TotalMilliseconds;
            if (startNode < 0 || targetNode < 0 || nodePath == null || nodePath.Length < 1) { result.Success = false; return; }
            List<PointF> points = new List<PointF>(nodePath.Length + 2);
            for (int i = 0; i < nodePath.Length; i++) {
                int cell = roadGraph.CellOfNode[nodePath[i]];
                points.Add(FineCenter(fine, cell % fine.Width, cell / fine.Width));
            }
            PointF entryPoint=points[0], exitPoint=points[points.Count-1];
            // Light RDP simplification only: StringPull is O(n^2) on long paths and a
            // 20 km road-only route produced thousands of nodes (20 s worst case), so
            // keep the raw road-following polyline and just thin it geometrically.
            // navA27 (P0-2): a thinned chord must still be legal on the baked nav-grid.
            // The old RDP could cut a corner over water at narrow bridges (displayed
            // polyline sampled water, navA26 B->A had 5.6 m).  Illegal chords are
            // re-expanded to the raw road points they replaced.
            List<int> kept = RdpIndices(points, points.Count > 3000 ? 5.0 : 2.5);
            if (kept.Count < 2) {
                kept = new List<int>(points.Count);
                for (int i = 0; i < points.Count; i++) kept.Add(i);
            }
            List<PointF> simplified = new List<PointF>(kept.Count + 8);
            for (int k = 0; k < kept.Count; k++) {
                simplified.Add(points[kept[k]]);
                if (k + 1 < kept.Count && !RoadChordLegalStrict(points[kept[k]], points[kept[k + 1]])) {
                    for (int q = kept[k] + 1; q < kept[k + 1]; q++) simplified.Add(points[q]);
                }
            }
            if (simplified.Count < 2) simplified = points;
            bool startConnectorInserted = false, endConnectorInserted = false;
            if (RoadRouter.DistanceMeters(playerPt, simplified[0]) > 1.0) {
                simplified.Insert(0, playerPt);
                startConnectorInserted = true;
            }
            if (RoadRouter.DistanceMeters(simplified[simplified.Count - 1], targetPt) > 1.0) {
                simplified.Add(targetPt);
                endConnectorInserted = true;
            }
            // navA28 (C1): detect real junctions on the road section, insert the ones
            // that require a turn and map them to final Polyline indices for the voice
            // navigator (negative turn -> turn_left clip, positive -> turn_right, same
            // atan2 formula as VoiceNavigation.NextCue).
            int[] junctionIndices = BuildJunctionIndices(simplified, startConnectorInserted, endConnectorInserted);
            // Add winding approach geometry after road-only cues have been computed.
            if(startApproach!=null && startConnectorInserted && startApproach.Count>2){
                simplified.InsertRange(1,startApproach.GetRange(1,startApproach.Count-2));
                for(int j=0;j<junctionIndices.Length;j++)junctionIndices[j]+=startApproach.Count-2;
            }
            if(endApproach!=null && endConnectorInserted && endApproach.Count>2){
                endApproach.Reverse();
                simplified.InsertRange(simplified.Count-1,endApproach.GetRange(1,endApproach.Count-2));
            }
            PointF[] polyline = simplified.ToArray();
            for (int i = 1; i < polyline.Length; i++)
                if (!RoadChordLegalStrict(polyline[i - 1], polyline[i])) return;
            double total = 0, seconds = 0, paved = 0, dirt = 0, unknown = 0;
            for (int i = 0; i < polyline.Length - 1; i++) {
                double length = RoadRouter.DistanceMeters(polyline[i], polyline[i + 1]);
                total += length;
                if (length <= 0) continue;
                int samples = (int)Math.Ceiling(length / 2.0);
                if (samples < 1) samples = 1;
                double slice = length / samples;
                for (int s = 0; s < samples; s++) {
                    double t = (s + 0.5) / samples;
                    double u = polyline[i].X + (polyline[i + 1].X - polyline[i].X) * t;
                    double v = polyline[i].Y + (polyline[i + 1].Y - polyline[i].Y) * t;
                    int x = ClampCell((int)Math.Floor(u * fine.Width), fine.Width);
                    int y = ClampCell((int)Math.Floor(v * fine.Height), fine.Height);
                    int cell = y * fine.Width + x;
                    byte cost = TrailCost(fineOverlay, cell, fine.Data[cell]);
                    double speed = TravelSpeedFor(RoadRouteMode.Quickest, cost, TrailMultiplier(fineOverlay, cell));
                    seconds += slice / speed;
                    if (cost == CostPaved) paved += slice;
                    else if (cost == CostDirt) dirt += slice;
                    else unknown += slice;
                }
            }
            result.Polyline = polyline;
            result.TotalDistanceMeters = total;
            result.EstimatedSeconds = seconds;
            result.RoadDistanceMeters = paved + dirt;
            result.PavedDistanceMeters = paved;
            result.DirtDistanceMeters = dirt;
            result.UnknownDistanceMeters = unknown;
            result.RoadEntryPoint = entryPoint;
            result.RoadExitPoint = exitPoint;
            result.EntryDistanceMeters = startConnector;
            result.ExitDistanceMeters = targetConnector;
            result.JunctionIndices = junctionIndices;
            result.Success = true;
        }

        // navA28 (C1): point at a given arc length along the polyline.  Binary search on
        // the monotonic cumulative distance array (the junction stage calls this several
        // thousand times per route, so a linear walk is too slow).
        static PointF PointAtArc(List<PointF> polyline, double[] dists, double arc) {
            int count = polyline.Count;
            if (arc <= 0) return polyline[0];
            if (arc >= dists[count - 1]) return polyline[count - 1];
            int lo = 1, hi = count - 1;
            while (lo < hi) {
                int mid = (lo + hi) / 2;
                if (dists[mid] < arc) lo = mid + 1;
                else hi = mid;
            }
            double length = dists[lo] - dists[lo - 1];
            double t = length < 0.0001 ? 0 : (arc - dists[lo - 1]) / length;
            return new PointF(
                (float)(polyline[lo - 1].X + (polyline[lo].X - polyline[lo - 1].X) * t),
                (float)(polyline[lo - 1].Y + (polyline[lo].Y - polyline[lo - 1].Y) * t));
        }

        // navA28 (C1): route turn angle at arc length, exactly the VoiceNavigation.NextCue
        // formula: before = at(-35 m), after = at(+35 m),
        // atan2(cross, dot); negative => left (clip 5), positive => right (clip 6).
        static double RouteTurnAtArc(List<PointF> polyline, double[] dists, double arc) {
            PointF at = PointAtArc(polyline, dists, arc);
            PointF before = PointAtArc(polyline, dists, arc - 35.0);
            PointF after = PointAtArc(polyline, dists, arc + 35.0);
            double ax = (at.X - before.X) * 15216.18, ay = (at.Y - before.Y) * 15236.18;
            double bx = (after.X - at.X) * 15216.18, by = (after.Y - at.Y) * 15236.18;
            return Math.Atan2(ax * by - ay * bx, ax * bx + ay * by) * 180.0 / Math.PI;
        }

        // navA28 (C1): JunctionIndices producer for the road-only route.
        //
        // Detection walks the ROAD section (auto-added player/target connector lines are
        // excluded) every 4 m (narrow arm-hit windows are only a few metres wide; a 16 m
        // grid misses junctions such as the acceptance-route E1).  Candidates are screened
        // by a cheap ring-cluster pre-filter, then verified by the 56 m / 16 m-disc
        // topological arm test, clustered at 60 m, and only kept when the final point
        // itself has >=3 arms AND the route really turns >=20 deg there.
        //
        // navA28 fix (C7): a cue representative is only accepted if the EXACT cell at its
        // own point passes the arm test (TopologicalArmsAtExact >= 3).  This removes the
        // residual sharp-bend false positive at uv=(0.905757,0.696767) where the point has
        // arms=1 and only the +-2 cell snap of the scan variant borrowed a nearby
        // junction.  The same exact check is repeated on the mutated polyline before the
        // index is returned.
        const double JunctionSampleStep = 4.0;
        const double JunctionMergeArc = 60.0; // merge nearby junction hits (over-segmentation guard)
        const double JunctionInsertMinMeters = 5.0;
        const double JunctionTurnMinDegrees = 20.0; // stricter than the 15 deg UI minimum: 15-20 deg junctions are ambiguous

        // diagnostics for probes (counters only; harmless under concurrent routes)
        internal int DebugJunctionCandidates;
        internal int DebugJunctionPrefilterPass;
        internal int DebugJunctionArmsCalls;

        // navA28 (C1): cheap ring-cluster pre-filter for the arm test.  Exact ring cells at
        // r=5..6 cells (20..24 m) around the nearest road cell: a real T/X junction leaves
        // >=3 eight-connected ring clusters, a straight road or a bend leaves 2.  Only a
        // negative screen - it can never reject a real >=3-arm junction.  Scratch arrays
        // are supplied by the caller (per-route, thread-safe, allocation-free per sample).
        bool RoadBranchCandidate(float u, float v, byte[] ring, int[] ringStack) {
            GridLevel fine = levels[LevelFine];
            if (fine == null) return false;
            int W = fine.Width, H = fine.Height;
            byte[] data = fine.Data;
            int cx = (int)(u * W), cy = (int)(v * H);
            int bx = -1, by = -1, best = int.MaxValue;
            for (int dy = -2; dy <= 2; dy++) {
                for (int dx = -2; dx <= 2; dx++) {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    if (data[y * W + x] > CostDirt) continue;
                    int d = dx * dx + dy * dy;
                    if (d < best) { best = d; bx = x; by = y; }
                }
            }
            if (bx < 0) return false;
            const int ringSide = 13;            // covers offsets -6..+6
            for (int i = 0; i < ring.Length; i++) ring[i] = 0;
            int ringCells = 0;
            for (int dy = -6; dy <= 6; dy++) {
                for (int dx = -6; dx <= 6; dx++) {
                    int adx = dx < 0 ? -dx : dx;
                    int ady = dy < 0 ? -dy : dy;
                    int cheb = adx > ady ? adx : ady;
                    if (cheb < 5) continue;
                    int x = bx + dx, y = by + dy;
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    if (data[y * W + x] > CostDirt) continue;
                    ring[(dy + 6) * ringSide + dx + 6] = 1;
                    ringCells++;
                }
            }
            if (ringCells < 3) return false;
            int clusters = 0;
            int ringLength = ringSide * ringSide;
            for (int i = 0; i < ringLength; i++) {
                if (ring[i] != 1) continue;
                clusters++;
                int top = 0;
                ringStack[top++] = i;
                ring[i] = 2;
                while (top > 0) {
                    int cur = ringStack[--top];
                    int cx2 = cur % ringSide, cy2 = cur / ringSide;
                    for (int ny = cy2 - 1; ny <= cy2 + 1; ny++) {
                        if (ny < 0 || ny >= ringSide) continue;
                        for (int nx = cx2 - 1; nx <= cx2 + 1; nx++) {
                            if (nx < 0 || nx >= ringSide) continue;
                            int ni = ny * ringSide + nx;
                            if (ring[ni] != 1) continue;
                            ring[ni] = 2;
                            ringStack[top++] = ni;
                        }
                    }
                }
            }
            return clusters >= 3;
        }

        internal int[] BuildJunctionIndices(List<PointF> polyline, bool startConnector, bool endConnector) {
            if (polyline == null || polyline.Count < 4) return new int[0];
            int count = polyline.Count;
            double[] dists = new double[count];
            for (int i = 1; i < count; i++) {
                dists[i] = dists[i - 1] + RoadRouter.DistanceMeters(polyline[i - 1], polyline[i]);
            }
            double total = dists[count - 1];
            double roadStart = startConnector ? dists[1] : 0.0;
            double roadEnd = endConnector ? dists[count - 2] : total;
            if (roadEnd - roadStart < 80.0) return new int[0];
            // Per-route scratch (locals => thread-safe; no per-candidate allocations).
            byte[] armsRoad = new byte[JunctionWindowCells];
            int[] armsFull = new int[JunctionWindowCells];
            int[] armsLab = new int[JunctionWindowCells];
            int[] armsStack = new int[JunctionWindowCells];
            byte[] ring = new byte[13 * 13];
            int[] ringStack = new int[13 * 13];
            // 1) 4 m candidate scan on the road section with pre-filter + arm test.
            List<double> hits = new List<double>(8);
            DebugJunctionCandidates = 0;
            DebugJunctionPrefilterPass = 0;
            DebugJunctionArmsCalls = 0;
            for (double arc = roadStart + 8.0; arc <= roadEnd - 8.0; arc += JunctionSampleStep) {
                DebugJunctionCandidates++;
                PointF point = PointAtArc(polyline, dists, arc);
                if (!RoadBranchCandidate(point.X, point.Y, ring, ringStack)) continue;
                DebugJunctionPrefilterPass++;
                DebugJunctionArmsCalls++;
                if (TopologicalArmsAt(point.X, point.Y, armsRoad, armsFull, armsLab, armsStack) >= 3) hits.Add(arc);
            }
            if (hits.Count == 0) return new int[0];
            // 2) cluster hits and accept one representative per cluster: strongest
            //    |routeTurn| among samples that pass the EXACT-cell arm re-check.
            List<double> cueArcs = new List<double>(hits.Count);
            int index = 0;
            while (index < hits.Count) {
                int last = index;
                while (last + 1 < hits.Count && hits[last + 1] - hits[index] <= JunctionMergeArc) last++;
                double bestArc = -1;
                double bestAbsTurn = 0;
                for (int k = index; k <= last; k++) {
                    double absTurn = Math.Abs(RouteTurnAtArc(polyline, dists, hits[k]));
                    if (absTurn < JunctionTurnMinDegrees || absTurn <= bestAbsTurn) continue;
                    PointF candidatePoint = PointAtArc(polyline, dists, hits[k]);
                    if (TopologicalArmsAtExact(candidatePoint.X, candidatePoint.Y, armsRoad, armsFull, armsLab, armsStack) < 3) continue;
                    bestAbsTurn = absTurn;
                    bestArc = hits[k];
                }
                if (bestArc >= 0) cueArcs.Add(bestArc);
                index = last + 1;
            }
            if (cueArcs.Count == 0) return new int[0];
            // 3) insert the cue junctions into the polyline (only when >5 m from the
            //    nearest vertex) and map them to final 0-based indices.
            List<PointF> work = new List<PointF>(polyline);
            List<double> workArc = new List<double>(dists);
            List<int> indices = new List<int>(cueArcs.Count);
            for (int i = 0; i < cueArcs.Count; i++) {
                double cueArc = cueArcs[i];
                int nearest = -1;
                double nearestArc = double.MaxValue;
                for (int v = 1; v < work.Count - 1; v++) {
                    double d = Math.Abs(workArc[v] - cueArc);
                    if (d < nearestArc) { nearestArc = d; nearest = v; }
                }
                if (nearest < 0 || nearestArc > JunctionInsertMinMeters) {
                    PointF point = PointAtArc(polyline, dists, cueArc);
                    int pos = 0;
                    while (pos < workArc.Count && workArc[pos] < cueArc) pos++;
                    if (pos < 1) pos = 1;
                    if (pos > work.Count - 1) pos = work.Count - 1;
                    work.Insert(pos, point);
                    workArc.Insert(pos, cueArc);
                    nearest = pos;
                }
                if (nearest > 0 && nearest < work.Count - 1 && !indices.Contains(nearest)) indices.Add(nearest);
            }
            if (indices.Count == 0) return new int[0];
            indices.Sort();
            // 4) final consistency check on the MUTATED polyline: exact VoiceNavigation
            //    turn formula (|turn| >= 20 deg) AND the exact-cell arm test at the final
            //    point.  Both must pass, otherwise the cue is dropped (no false turn on a
            //    plain sharp bend).
            double[] finalArcs = new double[work.Count];
            for (int i = 1; i < work.Count; i++) finalArcs[i] = finalArcs[i - 1] + RoadRouter.DistanceMeters(work[i - 1], work[i]);
            List<int> kept = new List<int>(indices.Count);
            for (int i = 0; i < indices.Count; i++) {
                int v = indices[i];
                if (v <= 0 || v >= work.Count - 1) continue;
                if (Math.Abs(RouteTurnAtArc(work, finalArcs, finalArcs[v])) < JunctionTurnMinDegrees) continue;
                if (TopologicalArmsAtExact(work[v].X, work[v].Y, armsRoad, armsFull, armsLab, armsStack) < 3) continue;
                kept.Add(v);
            }
            if (kept.Count == 0) return new int[0];
            polyline.Clear();
            polyline.AddRange(work);
            return kept.ToArray();
        }

        static double PointSegmentMeters(PointF p, PointF a, PointF b) {
            double ax = (a.X - p.X) * 15216.18, ay = (a.Y - p.Y) * 15236.18;
            double bx = (b.X - p.X) * 15216.18, by = (b.Y - p.Y) * 15236.18;
            double dx = bx - ax, dy = by - ay;
            double length2 = dx * dx + dy * dy;
            double t = length2 <= 1e-12 ? 0.0 : Math.Max(0.0, Math.Min(1.0, -(ax * dx + ay * dy) / length2));
            double qx = ax + t * dx, qy = ay + t * dy;
            return Math.Sqrt(qx * qx + qy * qy);
        }

        static List<int> RdpIndices(List<PointF> points, double tolerance) {
            List<int> result = new List<int>(points == null ? 0 : points.Count);
            if (points == null || points.Count == 0) return result;
            if (points.Count <= 2) {
                for (int i = 0; i < points.Count; i++) result.Add(i);
                return result;
            }
            bool[] keep = new bool[points.Count];
            keep[0] = true;
            keep[points.Count - 1] = true;
            Stack<int[]> stack = new Stack<int[]>();
            stack.Push(new int[] { 0, points.Count - 1 });
            while (stack.Count > 0) {
                int[] range = stack.Pop();
                int first = range[0], last = range[1];
                if (last <= first + 1) continue;
                double best = 0.0;
                int bestIndex = -1;
                for (int i = first + 1; i < last; i++) {
                    double d = PointSegmentMeters(points[i], points[first], points[last]);
                    if (d > best) { best = d; bestIndex = i; }
                }
                if (best > tolerance && bestIndex > 0) {
                    keep[bestIndex] = true;
                    stack.Push(new int[] { first, bestIndex });
                    stack.Push(new int[] { bestIndex, last });
                }
            }
            for (int i = 0; i < points.Count; i++) if (keep[i]) result.Add(i);
            return result;
        }

        // navA27 (P0-2): strict display-chord legality for the thinned road polyline.
        // Every 4 m cell the chord touches must be drivable, and at an exact corner
        // crossing BOTH orthogonal neighbours must be drivable.  This is stricter than
        // the diagonal A* squeeze rule on purpose: the rendered road-only polyline must
        // never sample water/blocked/vehicle-class4 (narrow bridges are the sensitive
        // case).  Illegal RDP chords are re-expanded to raw road points by the caller.
        bool RoadChordLegalStrict(PointF from, PointF to) {
            GridLevel fine = levels[LevelFine];
            int width = fine.Width, height = fine.Height;
            byte[] mask = roadIllegalMask;
            double ax = (double)from.X * width, ay = (double)from.Y * height;
            double bx = (double)to.X * width, by = (double)to.Y * height;
            if (ax < 0 || ay < 0 || ax > width - 1e-9 || ay > height - 1e-9) return false;
            if (bx < 0 || by < 0 || bx > width - 1e-9 || by > height - 1e-9) return false;
            int x = (int)Math.Floor(ax), y = (int)Math.Floor(ay);
            int targetX = (int)Math.Floor(bx), targetY = (int)Math.Floor(by);
            if (x == targetX && y == targetY) return mask[y * width + x] == 0;
            double dx = bx - ax, dy = by - ay;
            int stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
            int stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            double tDeltaX = stepX == 0 ? double.MaxValue : 1.0 / Math.Abs(dx);
            double tDeltaY = stepY == 0 ? double.MaxValue : 1.0 / Math.Abs(dy);
            double tMaxX = stepX == 0 ? double.MaxValue : (stepX > 0 ? (x + 1 - ax) : (ax - x)) / Math.Abs(dx);
            double tMaxY = stepY == 0 ? double.MaxValue : (stepY > 0 ? (y + 1 - ay) : (ay - y)) / Math.Abs(dy);
            int maxSteps = Math.Abs(targetX - x) + Math.Abs(targetY - y) + 4;
            int guard = 0;
            while (true) {
                if (mask[y * width + x] != 0) return false;
                if (x == targetX && y == targetY) return true;
                if (++guard > maxSteps) return false;
                if (tMaxX < tMaxY) {
                    x += stepX;
                    tMaxX += tDeltaX;
                } else if (tMaxY < tMaxX) {
                    y += stepY;
                    tMaxY += tDeltaY;
                } else {
                    if (stepX != 0 && stepY != 0) {
                        if (mask[y * width + (x + stepX)] != 0 || mask[(y + stepY) * width + x] != 0) return false;
                    }
                    x += stepX;
                    y += stepY;
                    tMaxX += tDeltaX;
                    tMaxY += tDeltaY;
                }
            }
        }

        int[] RoadAStar(int startNode, int targetNode) {
            RoadGraph g = roadGraph;
            GridLevel fine = levels[LevelFine];
            if (g.Epoch == uint.MaxValue) {
                Array.Clear(g.Seen, 0, g.Seen.Length);
                Array.Clear(g.Closed, 0, g.Closed.Length);
                g.Epoch = 1;
            }
            uint epoch = g.Epoch++;
            int[] cells = g.CellOfNode;
            int width = fine.Width;
            double metersX = fine.MetersX, metersY = fine.MetersY;
            double diagonal = Math.Sqrt(metersX * metersX + metersY * metersY);
            double minFactor = 3.6 / 60.0; // fastest possible road speed: admissible heuristic
            int tx = cells[targetNode] % width, ty = cells[targetNode] / width;
            g.Heap.Clear();
            g.G[startNode] = 0;
            g.Parent[startNode] = -1;
            g.Seen[startNode] = epoch;
            g.Heap.Push(0f, 0f, startNode);
            while (g.Heap.Count > 0) {
                float priority;
                int current = g.Heap.Pop(out priority);
                if (g.Closed[current] == epoch) continue;
                g.Closed[current] = epoch;
                if (current == targetNode) return ReconstructRoadPath(g, startNode, targetNode);
                int currentCell = cells[current];
                int cx = currentCell % width, cy = currentCell / width;
                double baseCost = g.G[current];
                for (int k = g.AdjStart[current]; k < g.AdjStart[current + 1]; k++) {
                    int next = g.Adj[k];
                    if (g.Closed[next] == epoch) continue;
                    int nextCell = cells[next];
                    int nx = nextCell % width, ny = nextCell / width;
                    int dx = nx - cx, dy = ny - cy;
                    double meters;
                    if (dx == 0) meters = Math.Abs(dy) * metersY;
                    else if (dy == 0) meters = Math.Abs(dx) * metersX;
                    else meters = diagonal;
                    byte cost = fine.Data[nextCell];
                    double factor = cost == CostDirt ? 3.6 / 40.0 : 3.6 / 60.0;
                    double candidate = baseCost + meters * factor;
                    if (g.Seen[next] == epoch && g.G[next] <= candidate) continue;
                    g.Seen[next] = epoch;
                    g.G[next] = (float)candidate;
                    g.Parent[next] = current;
                    double hx = Math.Abs(tx - nx), hy = Math.Abs(ty - ny);
                    double octile = hx > hy ? hx + 0.4142135623730951 * hy : hy + 0.4142135623730951 * hx;
                    float heuristic = (float)(octile * Math.Min(metersX, metersY) * minFactor * 1.15);
                    g.Heap.Push((float)candidate + heuristic, heuristic, next);
                }
            }
            return null;
        }

        int[] ReconstructRoadPath(RoadGraph g, int startNode, int targetNode) {
            List<int> reversed = new List<int>(256);
            int current = targetNode;
            int guard = 0;
            while (true) {
                reversed.Add(current);
                if (current == startNode) break;
                current = g.Parent[current];
                if (++guard > g.Parent.Length + 4) return null;
            }
            reversed.Reverse();
            return reversed.ToArray();
        }
    }
}
