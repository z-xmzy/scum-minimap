using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace ScumMiniMap {
    // nav-A: multi-resolution grid cost-surface router.
    //
    // Embedded world navigation grid is a gzip stream that decompresses to:
    //   "NAVG" (4) | uint16 version=1 | uint16 levelCount=3
    //   then three layers fine/mid/coarse, each:
    //     float32 cellMeters | uint32 width | uint32 height | width*height uint8 costs
    //
    // Cost codes: 0 undefined, 1 paved, 2 dirt, 4 open, 8 rough, 16 water, 255 building.
    // Blocks 0/16/255 are never walkable.
    //
    // Routing is hierarchical: a whole-map coarse (64 m) A* yields the global corridor;
    // a mid (16 m) A* then searches a corridor built from that coarse path with a
    // +-2 coarse-cell buffer; finally a fine (4 m) A* searches a corridor built from
    // the mid path with a +-2 mid-cell buffer (the fine corridor is widened and falls
    // back to the coarse corridor if the guide resolution cannot be followed).
    // Cost is distance for Shortest/PavedOnly and distance/speed for Quickest/Walking.
    // The heuristic is the admissible 8-connected octile distance divided by the
    // fastest allowed speed; Quickest applies a small bounded weight because the
    // hierarchical corridor already bounds the search region (see note below).
    // Diagonal steps need at least one orthogonal neighbour walkable (no corner
    // cutting).  The final polyline is string-pulled with a line-of-sight walk that
    // visits every grid cell the chord touches; Quickest only pulls a chord when it
    // does not make the leg slower.
    //
    // Missing or malformed world layers disable route calculation:
    // IsLoaded stays false / Success stays false, no exception escapes.
    internal sealed partial class GridRouter {
        internal const double MapScaleX = 1521618.0 / 100.0;
        internal const double MapScaleY = 1523618.0 / 100.0;

        const int LevelFine = 0;
        const int LevelMid = 1;
        const int LevelCoarse = 2;

        const byte CostUndefined = 0;
        const byte CostPaved = 1;
        const byte CostDirt = 2;
        const byte CostOpen = 4;
        const byte CostRough = 8;
        const byte CostWater = 16;
        const byte CostBlocked = 255;

        const int MaxDimension = 8192;
        const long MaxTotalCells = 64L * 1024L * 1024L;
        const int FineBlockShift = 4;
        const int FineBlockSize = 1 << FineBlockShift;
        const int FineBlockMask = FineBlockSize - 1;
        const int MaxFineBlocks = 12000;
        const int MaxFineBandCells = 1500000;

        const double QuickestMaxSpeed = 60.0 / 3.6;
        const double WalkingSpeed = 5.0 / 3.6;
        // The hierarchically refined corridor search keeps Quickest inside the same
        // corridor; a small bounded inflation of its time heuristic is what keeps the
        // route latency budget (p50 <= 40 ms) achievable on 15+ km routes.
        const double QuickestHeuristicWeight = 2.4;
        const double ShortestHeuristicWeight = 1.0;

        static readonly int[] MidBuffers = new int[] { 16, 64, 256 };
        static readonly int[] FineBuffers = new int[] { 16, 64, 256 };
        static readonly int[] CoarseGuideBuffers = new int[] { 2, 3, 4, 6, 8 };

        static readonly int[] NeighborX = new int[] { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] NeighborY = new int[] { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly double WalkingFactor = 1.0 / (5.0 / 3.6);
        static readonly double[] QuickestFactor = BuildQuickestFactors();
        const double OctileStraight = 0.4142135623730951;

        static double[] BuildQuickestFactors() {
            double[] factors = new double[256];
            for (int i = 0; i < factors.Length; i++) factors[i] = WalkingFactor;
            factors[1] = 3.6 / 60.0;
            factors[2] = 3.6 / 40.0;
            factors[4] = 3.6 / 20.0;
            factors[8] = 3.6 / 10.0;
            return factors;
        }

        sealed class GridLevel {
            internal byte[] Data;
            internal int Width;
            internal int Height;
            internal float CellMeters;
            internal double MetersX;
            internal double MetersY;
            internal int CellCount { get { return Width * Height; } }
        }

        // nav-C learned-trail overlay: one byte per cell per level, 0 = untouched
        // (base asset array is never modified).  Low 6 bits are a cost multiplier
        // in percent, 0x80 means "was blocked, learn-unsealed as open", 0x40 picks
        // paved (1) instead of open ground (4) for that unseal.
        sealed class TrailOverlay {
            internal byte[] Fine;
            internal byte[] Mid;
            internal byte[] Coarse;
            internal int TrailCells;
        }

        const byte TrailFlagUnblock = 0x80;
        const byte TrailFlagPaved = 0x40;
        const byte TrailCodeMask = 0x3F;
        const byte TrailFactorOn = 55;   // learned 4 m cell: cost x 0.55
        const byte TrailFactorNear = 80; // immediate 4 m ring: cost x 0.8
        const int TrailUnblockMinVisits = 3;
        const int TrailUnblockMinDays = 2;

        sealed class CellList {
            internal int[] Items = new int[1024];
            internal int Count;
            internal void Clear() { Count = 0; }
            internal void Add(int value) {
                if (Count == Items.Length) {
                    int[] grown = new int[Items.Length * 2];
                    Array.Copy(Items, grown, Count);
                    Items = grown;
                }
                Items[Count++] = value;
            }
        }

        sealed class MinHeap {
            float[] priority = new float[1024];
            float[] tie = new float[1024];
            int[] value = new int[1024];
            int count;
            internal int Count { get { return count; } }
            internal void Clear() { count = 0; }
            internal void Push(float p, float t, int v) {
                if (count == priority.Length) {
                    int grown = priority.Length * 2;
                    float[] np = new float[grown];
                    float[] nt = new float[grown];
                    int[] nv = new int[grown];
                    Array.Copy(priority, np, count);
                    Array.Copy(tie, nt, count);
                    Array.Copy(value, nv, count);
                    priority = np;
                    tie = nt;
                    value = nv;
                }
                int i = count++;
                priority[i] = p;
                tie[i] = t;
                value[i] = v;
                while (i > 0) {
                    int parent = (i - 1) >> 1;
                    // Same f: prefer the node with the smaller heuristic (i.e. the
                    // deeper node).  This removes most plateau expansions in A*.
                    if (priority[parent] < priority[i]) break;
                    if (priority[parent] == priority[i] && tie[parent] <= tie[i]) break;
                    Swap(parent, i);
                    i = parent;
                }
            }
            internal int Pop(out float p) {
                p = priority[0];
                int result = value[0];
                count--;
                if (count > 0) {
                    priority[0] = priority[count];
                    tie[0] = tie[count];
                    value[0] = value[count];
                    int i = 0;
                    while (true) {
                        int left = (i << 1) + 1;
                        if (left >= count) break;
                        int best = left;
                        int right = left + 1;
                        if (right < count) {
                            if (priority[right] < priority[left]) best = right;
                            else if (priority[right] == priority[left] && tie[right] < tie[left]) best = right;
                        }
                        if (priority[i] < priority[best]) break;
                        if (priority[i] == priority[best] && tie[i] <= tie[best]) break;
                        Swap(i, best);
                        i = best;
                    }
                }
                return result;
            }
            void Swap(int a, int b) {
                float tp = priority[a];
                priority[a] = priority[b];
                priority[b] = tp;
                float tt = tie[a];
                tie[a] = tie[b];
                tie[b] = tt;
                int tv = value[a];
                value[a] = value[b];
                value[b] = tv;
            }
        }

        sealed class FullSearch {
            internal float[] G = new float[0];
            internal int[] Parent = new int[0];
            internal uint[] Seen = new uint[0];
            internal uint[] Closed = new uint[0];
            internal MinHeap Heap = new MinHeap();
            internal uint Epoch = 1;
            internal void Prepare(int cells) {
                if (G.Length != cells) {
                    G = new float[cells];
                    Parent = new int[cells];
                    Seen = new uint[cells];
                    Closed = new uint[cells];
                    Epoch = 1;
                }
            }
            internal void NextEpoch() {
                Epoch++;
                if (Epoch == 0) {
                    Array.Clear(Seen, 0, Seen.Length);
                    Array.Clear(Closed, 0, Closed.Length);
                    Epoch = 1;
                }
            }
        }

        readonly object loadLock = new object();
        readonly object searchLock = new object();

        // nav-C trail overlay (null = no learned data, behaviour identical to navA1).
        TrailOverlay trailOverlay;
        int trailSyncedVersion = -1;
        bool trailEnabledSynced;
        internal int DebugTrailCells;
        internal int DebugTrailUnblocked;
        internal bool DebugTrailActive { get { return trailOverlay != null; } }
        volatile bool isLoaded;
        byte[] terrainFine, terrainMid, terrainCoarse;
        GridLevel[] levels;

        readonly FullSearch coarseSearch = new FullSearch();
        readonly FullSearch midSearch = new FullSearch();

        byte[] midBand;
        readonly CellList midBandCells = new CellList();

        byte[] fineBand;
        readonly CellList fineBandCells = new CellList();
        int[] fineBlockBase;
        int fineBlockCols;
        int fineBlockRows;
        readonly CellList fineAllocBlocks = new CellList();
        float[] fineG = new float[0];
        int[] fineParent = new int[0];
        uint[] fineSeen = new uint[0];
        uint[] fineClosed = new uint[0];
        uint fineEpoch = 1;
        readonly MinHeap fineHeap = new MinHeap();

        internal bool IsLoaded { get { return isLoaded; } }

        // Diagnostics for the probe harness (cheap counters, no behaviour change).
        internal int DebugCoarseExpansions;
        internal int DebugMidExpansions;
        internal int DebugFineExpansions;
        internal double DebugCoarseMs;
        internal double DebugMidMs;
        internal double DebugFineMs;
        internal double DebugPullMs;
        internal double DebugMetricsMs;
        internal int DebugMidBandCells;
        internal int DebugMidBandAttempt;
        internal int DebugFineBandCells;
        internal int DebugFineBandAttempt;
        internal bool DebugFineCoarseFallback;
        internal double DebugFinePathCost;
        internal int DebugFineStartCell;
        internal int DebugFineTargetCell;
        internal void ResetDebugCounters() {
            DebugCoarseExpansions = 0;
            DebugMidExpansions = 0;
            DebugFineExpansions = 0;
            DebugCoarseMs = 0;
            DebugMidMs = 0;
            DebugFineMs = 0;
            DebugPullMs = 0;
            DebugMetricsMs = 0;
            DebugMidBandCells = 0;
            DebugMidBandAttempt = -1;
            DebugFineBandCells = 0;
            DebugFineBandAttempt = -1;
            DebugFineCoarseFallback = false;
        }

        // ------------------------------------------------------------------
        // Embedded world navigation layers
        // ------------------------------------------------------------------
        internal void InitializeFromResource() {
            lock (loadLock) {
                if (isLoaded) return;
                try {
                    LoadWorldLayers();
                    ReleaseSearchState();
                    isLoaded = true;
                    SyncTrailOverlay();
                } catch {
                    levels = null;
                    isLoaded = false;
                }
            }
        }

        // ------------------------------------------------------------------
        // nav-C learned trail overlay
        // ------------------------------------------------------------------
        // Rebuilds the per-level trail factors from NavTrails.  Called once after
        // the grid asset loads and again only when the live recorder bumps the
        // trail version (so recording never needs an app restart).
        void SyncTrailOverlay() {
            if (!isLoaded || levels == null) return;
            if (!NavTrails.LearningEnabled) {
                // Switch off: drop the overlay (no rebuild, no trail-file access) so
                // every cost query is identical to navA1 again. The file is untouched.
                if (trailOverlay != null) {
                    trailOverlay = null;
                    DebugTrailCells = 0;
                    DebugTrailUnblocked = 0;
                }
                trailSyncedVersion = NavTrails.Instance.Version;
                trailEnabledSynced = false;
                return;
            }
            if (!trailEnabledSynced) {
                // Just re-enabled: force one rebuild from the persisted file.
                trailEnabledSynced = true;
                trailSyncedVersion = -1;
            }
            NavTrails trails = NavTrails.Instance;
            int version = trails.Version;
            if (trailSyncedVersion == version) return;
            NavTrailCell[] records = trails.Snapshot();
            trailOverlay = BuildTrailOverlay(levels, records);
            trailSyncedVersion = version;
            DebugTrailCells = trailOverlay == null ? 0 : trailOverlay.TrailCells;
            DebugTrailUnblocked = 0;
        }
        static TrailOverlay BuildTrailOverlay(GridLevel[] levelSet, NavTrailCell[] records) {
            if (levelSet == null || records == null || records.Length == 0) return null;
            GridLevel fine = levelSet[LevelFine];
            byte[] fineOverlay = new byte[fine.CellCount];
            List<int> touched = new List<int>(records.Length * 4);
            TrailOverlay overlay = new TrailOverlay();
            // Pass 1: learned cells themselves.
            for (int i = 0; i < records.Length; i++) {
                int cell = CanonicalToFine(records[i].Cell, fine.Width, fine.Height);
                byte baseCost = fine.Data[cell];
                // Learned paths can lower the cost of open ground, never erase a physical obstacle.
                if (baseCost == CostUndefined || baseCost == CostWater || baseCost == CostBlocked) continue;
                byte code = TrailFactorOn;
                if (fineOverlay[cell] == 0) touched.Add(cell);
                fineOverlay[cell] = code;
                overlay.TrailCells++;
                overlay.Fine = fineOverlay;
            }
            if (overlay.Fine == null) return null;
            // Pass 2: immediate 8-neighbour ring gets the milder x0.8 factor only
            // on already walkable cells (the ring never unseals anything).
            int trailTouched = touched.Count;
            for (int i = 0; i < trailTouched; i++) {
                int cell = touched[i];
                int x = cell % fine.Width, y = cell / fine.Width;
                for (int k = 0; k < 8; k++) {
                    int nx = x + NeighborX[k], ny = y + NeighborY[k];
                    if (nx < 0 || ny < 0 || nx >= fine.Width || ny >= fine.Height) continue;
                    int next = ny * fine.Width + nx;
                    if (fineOverlay[next] != 0) continue;
                    byte baseCost = fine.Data[next];
                    if (baseCost == CostUndefined || baseCost == CostWater || baseCost == CostBlocked) continue;
                    fineOverlay[next] = TrailFactorNear;
                    touched.Add(next);
                }
            }
            // Pass 3: fold the fine overlay into the mid / coarse layers so the
            // global A* also prefers the player's habitual corridors.
            overlay.Mid = BuildCoarseOverlay(levelSet[LevelMid], fine, fineOverlay, touched);
            overlay.Coarse = BuildCoarseOverlay(levelSet[LevelCoarse], fine, fineOverlay, touched);
            return overlay;
        }

        static byte[] BuildCoarseOverlay(GridLevel target, GridLevel fine, byte[] fineOverlay, List<int> touched) {
            byte[] result = new byte[target.CellCount];
            for (int i = 0; i < touched.Count; i++) {
                int cell = touched[i];
                byte fineCode = fineOverlay[cell];
                if (fineCode == 0) continue;
                int x = cell % fine.Width, y = cell / fine.Width;
                double u = (x + 0.5) / fine.Width, v = (y + 0.5) / fine.Height;
                int tx = ClampCell((int)(u * target.Width), target.Width);
                int ty = ClampCell((int)(v * target.Height), target.Height);
                int next = ty * target.Width + tx;
                byte baseCost = target.Data[next];
                if (baseCost == CostUndefined || baseCost == CostWater) continue; // water never unsealed
                byte code;
                if (baseCost == CostBlocked) {
                    if ((fineCode & TrailFlagUnblock) == 0) continue;
                    code = (byte)(TrailFlagUnblock | TrailFactorOn);
                    if ((fineCode & TrailFlagPaved) != 0) code |= TrailFlagPaved;
                } else {
                    int factor = fineCode & TrailCodeMask;
                    code = (byte)(factor == 0 ? TrailFactorOn : factor);
                }
                MergeTrailCode(result, next, code);
            }
            // Guarantee at least one 0.8 ring at the coarse layers too.
            DilateTrailRing(target, result);
            return result;
        }

        static void MergeTrailCode(byte[] overlay, int cell, byte code) {
            byte current = overlay[cell];
            if (current == 0 || TrailCodeRank(code) > TrailCodeRank(current)) overlay[cell] = code;
        }

        static int TrailCodeRank(byte code) {
            int rank = (code & TrailFlagUnblock) != 0 ? 4 : 0;
            int factor = code & TrailCodeMask;
            if (factor == TrailFactorOn) rank += 2;
            else if (factor == TrailFactorNear) rank += 1;
            return rank;
        }

        static void DilateTrailRing(GridLevel level, byte[] overlay) {
            List<int> learned = new List<int>(64);
            for (int i = 0; i < overlay.Length; i++) {
                if ((overlay[i] & TrailCodeMask) == TrailFactorOn) learned.Add(i);
            }
            for (int i = 0; i < learned.Count; i++) {
                int cell = learned[i];
                int x = cell % level.Width, y = cell / level.Width;
                for (int k = 0; k < 8; k++) {
                    int nx = x + NeighborX[k], ny = y + NeighborY[k];
                    if (nx < 0 || ny < 0 || nx >= level.Width || ny >= level.Height) continue;
                    int next = ny * level.Width + nx;
                    if (overlay[next] != 0) continue;
                    byte baseCost = level.Data[next];
                    if (baseCost == CostUndefined || baseCost == CostWater || baseCost == CostBlocked) continue;
                    overlay[next] = TrailFactorNear;
                }
            }
        }

        // Canonical (4 m, 3804x3804) trail cell -> this asset's fine cell index.
        static int CanonicalToFine(int canonicalCell, int fineWidth, int fineHeight) {
            int cx = canonicalCell % NavTrails.CanonicalWidth;
            int cy = canonicalCell / NavTrails.CanonicalWidth;
            double u = (cx + 0.5) / NavTrails.CanonicalWidth;
            double v = (cy + 0.5) / NavTrails.CanonicalHeight;
            int x = ClampCell((int)(u * fineWidth), fineWidth);
            int y = ClampCell((int)(v * fineHeight), fineHeight);
            return y * fineWidth + x;
        }

        // "Road evidence" for unsealing a blocked trail cell: a paved neighbour
        // means the gap is really a street crossing, so the cell opens as paved.
        static bool HasPavedEvidence(GridLevel fine, int cell) {
            int x = cell % fine.Width, y = cell / fine.Width;
            for (int k = 0; k < 8; k++) {
                int nx = x + NeighborX[k], ny = y + NeighborY[k];
                if (nx < 0 || ny < 0 || nx >= fine.Width || ny >= fine.Height) continue;
                if (fine.Data[ny * fine.Width + nx] == CostPaved) return true;
            }
            return false;
        }

        // Effective base cost after a trail unseal (base asset arrays stay intact).
        static byte TrailCost(byte[] overlay, int cell, byte baseCost) {
            if (overlay == null) return baseCost;
            byte code = overlay[cell];
            if (code == 0) return baseCost;
            if ((code & TrailFlagUnblock) != 0 && baseCost == CostBlocked)
                return (code & TrailFlagPaved) != 0 ? CostPaved : CostOpen;
            return baseCost;
        }

        static double TrailMultiplier(byte[] overlay, int cell) {
            if (overlay == null) return 1.0;
            byte code = overlay[cell];
            if (code == 0) return 1.0;
            int factor = code & TrailCodeMask;
            return factor == 0 ? 1.0 : factor * 0.01;
        }

        // Speed used by ETA / string-pull travel checks.  Quickest/Walking share
        // their cost model with A*, so the trail factor shortens their time too.
        static double TravelSpeedFor(RoadRouteMode mode, byte effectiveCost, double multiplier) {
            double speed;
            switch (mode) {
                case RoadRouteMode.Walking:
                    speed = WalkingSpeed;
                    break;
                case RoadRouteMode.Quickest:
                    speed = SpeedFor(effectiveCost); // roads/trails only in this mode
                    break;
                case RoadRouteMode.Shortest: // roads first, semi off-road
                    if (effectiveCost == CostPaved) speed = 60.0 / 3.6;
                    else if (effectiveCost == CostDirt) speed = 40.0 / 3.6;
                    else if (effectiveCost == CostOpen) speed = 35.0 / 3.6;
                    else speed = 15.0 / 3.6;
                    break;
                default: // full off-road
                    if (effectiveCost == CostPaved || effectiveCost == CostDirt) speed = 50.0 / 3.6;
                    else if (effectiveCost == CostOpen) speed = 45.0 / 3.6;
                    else speed = 18.0 / 3.6;
                    break;
            }
            if (multiplier > 0.0 && multiplier < 1.0 && (mode == RoadRouteMode.Quickest || mode == RoadRouteMode.Walking))
                speed /= multiplier;
            return speed;
        }

        // navA22 terrain-aware off-road cost: terrain classes are extracted from the
        // official topographic (contour) map of the island (scum-map.com tactical layer).
        // 0 = flat/lowland, 1 = gentle slopes, 2 = steep, 3 = very steep / mountain side.
        byte[] TerrainForLevel(GridLevel level) {
            if (terrainFine == null || level == null) return null;
            if (level == levels[LevelFine]) return terrainFine;
            if (level == levels[LevelMid]) return terrainMid;
            if (level == levels[LevelCoarse]) return terrainCoarse;
            return null;
        }

        static double TerrainCost(RoadRouteMode mode, byte t) {
            if (t >= 4) t = 3;
            if (t == 0) return 1.0;
            if (mode == RoadRouteMode.Walking) {
                if (t == 1) return 1.10;
                if (t == 2) return 1.25;
                return 1.45;
            }
            if (mode == RoadRouteMode.Shortest) { // 半越野: avoid steep mountain sides
                if (t == 1) return 1.5;
                if (t == 2) return 3.0;
                return 12.0;
            }
            if (mode == RoadRouteMode.PavedOnly) { // 全越野: still prefers gentle ground
                if (t == 1) return 1.25;
                if (t == 2) return 2.0;
                return 8.0;
            }
            return 1.0; // road-only is driven on graded roads
        }

        static GridLevel[] ParseAsset(BinaryReader reader) {
            byte[] magic = reader.ReadBytes(4);
            if (magic.Length != 4 || magic[0] != 'N' || magic[1] != 'A' || magic[2] != 'V' || magic[3] != 'G') return null;
            ushort version = reader.ReadUInt16();
            if (version != 1) return null;
            ushort levelCount = reader.ReadUInt16();
            if (levelCount != 3) return null;
            GridLevel[] parsed = new GridLevel[3];
            long totalCells = 0;
            for (int i = 0; i < 3; i++) {
                float cellMeters = reader.ReadSingle();
                uint width = reader.ReadUInt32();
                uint height = reader.ReadUInt32();
                if (!Finite(cellMeters) || cellMeters < 0.5f || cellMeters > 256f) return null;
                if (width < 32 || height < 32 || width > MaxDimension || height > MaxDimension) return null;
                long cells = (long)width * (long)height;
                totalCells += cells;
                if (totalCells > MaxTotalCells) return null;
                byte[] data = reader.ReadBytes((int)cells);
                if (data.Length != (int)cells) return null;
                GridLevel level = new GridLevel();
                level.Data = data;
                level.Width = (int)width;
                level.Height = (int)height;
                level.CellMeters = cellMeters;
                level.MetersX = MapScaleX / level.Width;
                level.MetersY = MapScaleY / level.Height;
                parsed[i] = level;
            }
            // Layer contract: fine < mid < coarse resolution, all spanning the map once.
            for (int i = 0; i < 3; i++) {
                GridLevel level = parsed[i];
                double spanX = level.Width * (double)level.CellMeters;
                double spanY = level.Height * (double)level.CellMeters;
                if (spanX < MapScaleX * 0.75 || spanX > MapScaleX * 1.5) return null;
                if (spanY < MapScaleY * 0.75 || spanY > MapScaleY * 1.5) return null;
                if (level.Width < level.Height * 0.8 || level.Width > level.Height * 1.25) return null;
                if (i > 0) {
                    GridLevel previous = parsed[i - 1];
                    if (level.CellMeters < previous.CellMeters * 1.5f) return null;
                    if (level.Width > previous.Width || level.Height > previous.Height) return null;
                }
            }
            return parsed;
        }

        void ReleaseSearchState() {
            midBand = null;
            midBandCells.Clear();
            fineBand = null;
            fineBandCells.Clear();
            fineBlockBase = null;
            fineAllocBlocks.Clear();
            fineG = new float[0];
            fineParent = new int[0];
            fineSeen = new uint[0];
            fineClosed = new uint[0];
            fineEpoch = 1;
        }

        static bool Finite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool Finite(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        // ------------------------------------------------------------------
        // Cost / passability model
        // ------------------------------------------------------------------
        // Mode semantics (UI labels):
        //   Quickest  = 驾车走线     : only roads / trails (cost 1/2) are drivable.
        //   Shortest  = 驾车半越野   : roads preferred, crossing open/rough ground allowed.
        //   PavedOnly = 驾车全越野   : cross open ground by default, roads only where terrain blocks.
        //   Walking   = 步行         : straight-ish, bridges/roads only across water.
        static bool Passable(RoadRouteMode mode, byte cost) {
            if (cost == CostUndefined || cost == CostWater || cost == CostBlocked) return false;
            if (mode == RoadRouteMode.Quickest) return cost == CostPaved || cost == CostDirt;
            return true;
        }

        static double SpeedFor(byte cost) {
            switch (cost) {
                case CostPaved: return 60.0 / 3.6;
                case CostDirt: return 40.0 / 3.6;
                case CostOpen: return 20.0 / 3.6;
                case CostRough: return 10.0 / 3.6;
                default: return WalkingSpeed;
            }
        }

        static bool DistanceMode(RoadRouteMode mode) {
            return mode == RoadRouteMode.Shortest || mode == RoadRouteMode.PavedOnly;
        }

        // Shortest is distance based, but plain metres make cross-country as cheap as
        // the road network.  This multiplier keeps it a shortest-path mode while
        // preferring roads/trails: paved/trail (1/2) x1.0, open ground (4) x2.0,
        // rough/forest (8) x4.0.  Blocked cells stay impassable, so the penalty can
        // only ever push the route back towards roads, never through obstacles.
        static readonly double[] ShortestTerrainPenalty = BuildShortestTerrainPenalties();

        static double[] BuildShortestTerrainPenalties() {
            double[] penalties = new double[256];
            for (int i = 0; i < penalties.Length; i++) penalties[i] = 1.7;
            penalties[CostPaved] = 1.0;
            penalties[CostDirt] = 1.0;   // white-line trails / dirt roads added by the asset
            penalties[CostOpen] = 2.0;   // 半越野: roads first, flat open ground is the only shortcut
            penalties[CostRough] = 4.0;  // passable ground near obstacles; solid footprints are blocked separately
            return penalties;
        }

        // Full off-road ("PavedOnly" slot): open ground is cheapest, roads are a
        // slightly worse straight line, rough terrain is avoided when possible.
        static readonly double[] FullOffroadPenalty = BuildFullOffroadPenalties();

        static double[] BuildFullOffroadPenalties() {
            double[] penalties = new double[256];
            for (int i = 0; i < penalties.Length; i++) penalties[i] = 1.35;
            penalties[CostPaved] = 1.1;
            penalties[CostDirt] = 1.1;
            penalties[CostOpen] = 1.0;
            penalties[CostRough] = 2.0;  // passable brush, with separate slope and collision costs
            return penalties;
        }

        // Cost multiplier per metre for each mode.  Quickest is road-only time,
        // Walking is constant walk time, Shortest carries the roads-first penalty,
        // and the full-off-road slot carries the open-ground-first penalty.
        static double StepFactor(byte cost, RoadRouteMode mode) {
            if (mode == RoadRouteMode.Quickest) return QuickestFactor[cost];
            if (mode == RoadRouteMode.Walking) return WalkingFactor;
            if (mode == RoadRouteMode.Shortest) return ShortestTerrainPenalty[cost];
            if (mode == RoadRouteMode.PavedOnly) return FullOffroadPenalty[cost];
            return 1.0;
        }

        static double MaxSpeedFor(RoadRouteMode mode) {
            if (mode == RoadRouteMode.Quickest) return QuickestMaxSpeed;
            if (mode == RoadRouteMode.Walking) return WalkingSpeed;
            return 1.0;
        }

        // ------------------------------------------------------------------
        // Public routing entry point
        // ------------------------------------------------------------------
        internal RoadRoute FindRoute(PointF playerPt, PointF targetPt, RoadRouteMode mode) {
            RoadRoute result = new RoadRoute();
            result.Success = false;
            result.Mode = mode;
            result.PlayerPoint = playerPt;
            result.TargetPoint = targetPt;
            result.Polyline = new PointF[0];
            result.JunctionIndices = new int[0];
            if (!isLoaded) return result;
            if (!Finite(playerPt.X) || !Finite(playerPt.Y) || !Finite(targetPt.X) || !Finite(targetPt.Y)) return result;
            if (!Enum.IsDefined(typeof(RoadRouteMode), mode)) return result;
            PointF start = ClampPoint(playerPt);
            PointF finish = ClampPoint(targetPt);
            lock (searchLock) {
                try {
                    ResetDebugCounters();
                    SyncTrailOverlay();
                    RoadRouteMode accessMode = mode == RoadRouteMode.Quickest ? RoadRouteMode.Shortest : mode;
                    if (!LineClear(start, start, accessMode, levels[LevelFine], null) ||
                        !LineClear(finish, finish, accessMode, levels[LevelFine], null)) return result;
                    if (mode == RoadRouteMode.Quickest) {
                        // Exact 4 m road-network A*: reliable on long routes.
                        ComputeRoadOnlyRoute(start, finish, result);
                        return result;
                    }
                    if (mode == RoadRouteMode.Quickest) {
                        EnsureRoadComponents();
                        GridLevel fineLevel = levels[LevelFine];
                        int startCell, targetCell; ushort startComp, targetComp;
                        bool haveStart = NearestRoadComponent(fineLevel, fineRoadLabel, start.X, start.Y, 320, out startCell, out startComp);
                        bool haveTarget = NearestRoadComponent(fineLevel, fineRoadLabel, finish.X, finish.Y, 320, out targetCell, out targetComp);
                        if (haveStart && haveTarget && startComp != targetComp) {
                            // Both ends have roads but they are different components (e.g. an
                            // island road and the mainland): water cannot be crossed, fail.
                            result.Success = false;
                            result.Polyline = new PointF[0];
                            return result;
                        }
                        roadOnlyForceComp = haveStart && haveTarget ? startComp : (haveStart ? startComp : (haveTarget ? targetComp : fineRoadMain));
                        // Guide levels can fragment differently from the 4 m layer; anchor
                        // mid/coarse snapping to the component near the start so the coarse
                        // A* actually reaches the fine-layer road network.
                        // Map the fine component anchor (a real 4 m road cell) into the mid /
                        // coarse layers so every level snaps to the matching road component.
                        int anchorFine = haveStart ? startCell : (haveTarget ? targetCell : -1);
                        roadOnlyMidComp = 0;
                        roadOnlyCoarseComp = 0;
                        if (anchorFine >= 0) {
                            GridLevel midLevel = levels[LevelMid];
                            GridLevel coarseLevel = levels[LevelCoarse];
                            PointF anchor = FineCenter(fineLevel, anchorFine % fineLevel.Width, anchorFine / fineLevel.Width);
                            int mx = ClampCell((int)Math.Floor(anchor.X * midLevel.Width), midLevel.Width);
                            int my = ClampCell((int)Math.Floor(anchor.Y * midLevel.Height), midLevel.Height);
                            int midCell = my * midLevel.Width + mx;
                            if (midCell >= 0 && midCell < midRoadLabel.Length && midLevel.Data[midCell] <= CostDirt) roadOnlyMidComp = midRoadLabel[midCell];
                            int cxx = ClampCell((int)Math.Floor(anchor.X * coarseLevel.Width), coarseLevel.Width);
                            int cyy = ClampCell((int)Math.Floor(anchor.Y * coarseLevel.Height), coarseLevel.Height);
                            int coarseCell = cyy * coarseLevel.Width + cxx;
                            if (coarseCell >= 0 && coarseCell < coarseRoadLabel.Length && coarseLevel.Data[coarseCell] <= CostDirt) roadOnlyCoarseComp = coarseRoadLabel[coarseCell];
                        }
                        if (roadOnlyMidComp == 0) roadOnlyMidComp = NearestComponent(levels[LevelMid], midRoadLabel, start.X, start.Y, 24);
                        if (roadOnlyMidComp == 0) roadOnlyMidComp = midRoadMain;
                        if (roadOnlyCoarseComp == 0) roadOnlyCoarseComp = NearestComponent(levels[LevelCoarse], coarseRoadLabel, start.X, start.Y, 6);
                        if (roadOnlyCoarseComp == 0) roadOnlyCoarseComp = coarseRoadMain;
                    }
                    ComputeRoute(start, finish, mode, result);
                    if (!result.Success && mode == RoadRouteMode.Quickest) {
                        // The nearest road cell may sit in a small isolated component; retry
                        // with a much wider snap so the nearest connected road network is used.
                        wideSnapRetry = true;
                        try { ComputeRoute(start, finish, mode, result); }
                        finally { wideSnapRetry = false; }
                    }
                } catch {
                    result.Success = false;
                    result.Polyline = new PointF[0];
                }
            }
            return result;
        }

        double FinePathCost(int[] path, GridLevel fine, byte[] overlay, RoadRouteMode mode, out double length) {
            double sum = 0;
            length = 0;
            for (int i = 1; i < path.Length; i++) {
                int a = path[i-1], b = path[i];
                double dx = ((b % fine.Width) - (a % fine.Width)) * fine.MetersX;
                double dy = ((b / fine.Width) - (a / fine.Width)) * fine.MetersY;
                double metres = Math.Sqrt(dx*dx + dy*dy);
                if (metres == 0) continue;
                length += metres;
                sum += metres * StepFactor(TrailCost(overlay, b, fine.Data[b]), mode) * TerrainCost(mode, terrainFine[b])
                    * GradeFactor(b, dx/metres, dy/metres, mode) * TrailMultiplier(overlay, b);
            }
            return sum;
        }

        bool wideSnapRetry;
        ushort[] fineRoadLabel, midRoadLabel, coarseRoadLabel;
        ushort fineRoadMain, midRoadMain, coarseRoadMain;
        ushort roadOnlyForceComp;
        ushort roadOnlyMidComp;
        ushort roadOnlyCoarseComp;
        bool roadComponentsReady;

        void ComputeRoute(PointF playerPt, PointF targetPt, RoadRouteMode mode, RoadRoute result) {
            GridLevel fine = levels[LevelFine];
            GridLevel mid = levels[LevelMid];
            GridLevel coarse = levels[LevelCoarse];
            TrailOverlay overlay = trailOverlay;
            byte[] fineOverlay = overlay == null ? null : overlay.Fine;
            byte[] midOverlay = overlay == null ? null : overlay.Mid;
            byte[] coarseOverlay = overlay == null ? null : overlay.Coarse;

            double straight = RoadRouter.DistanceMeters(playerPt, targetPt);
            if (straight < 0.5 && LineClear(playerPt, targetPt, mode, fine, fineOverlay)) {
                result.Polyline = new PointF[] { playerPt, targetPt };
                result.TotalDistanceMeters = straight;
                result.EstimatedSeconds = 0.0;
                result.RoadDistanceMeters = 0.0;
                result.RoadEntryPoint = playerPt;
                result.RoadExitPoint = targetPt;
                result.Success = true;
                return;
            }

            // 1. Snap both ends to walkable cells of each resolution.  The fine snap
            //    defines the exact polyline ends, so a destination marker inside a
            //    building still yields a legal walkable final point (Entry/Exit = 0).
            PointF startPoint = playerPt;
            PointF targetPoint = targetPt;
            int fineStartCell, fineTargetCell;
            int fineSnapRadius = mode == RoadRouteMode.Quickest ? (wideSnapRetry ? 2400 : 640) : 96;
            int coarseSnapRadius = mode == RoadRouteMode.Quickest ? (wideSnapRetry ? 600 : 160) : 24;
            if (!SnapCell(fine, startPoint.X, startPoint.Y, mode, fineOverlay, fineSnapRadius, out fineStartCell)) return;
            if (!SnapCell(fine, targetPoint.X, targetPoint.Y, mode, fineOverlay, fineSnapRadius, out fineTargetCell)) return;
            DebugFineStartCell = fineStartCell;
            DebugFineTargetCell = fineTargetCell;
            int fineStartX = fineStartCell % fine.Width;
            int fineStartY = fineStartCell / fine.Width;
            int fineTargetX = fineTargetCell % fine.Width;
            int fineTargetY = fineTargetCell / fine.Width;
            int rawStartX = ClampCell((int)Math.Floor(startPoint.X * fine.Width), fine.Width);
            int rawStartY = ClampCell((int)Math.Floor(startPoint.Y * fine.Height), fine.Height);
            int rawTargetX = ClampCell((int)Math.Floor(targetPoint.X * fine.Width), fine.Width);
            int rawTargetY = ClampCell((int)Math.Floor(targetPoint.Y * fine.Height), fine.Height);
            int rawStartCell = rawStartY * fine.Width + rawStartX;
            int rawTargetCell = rawTargetY * fine.Width + rawTargetX;
            bool playerCellWalkable = Passable(mode, TrailCost(fineOverlay, rawStartCell, fine.Data[rawStartCell]));
            bool targetCellWalkable = Passable(mode, TrailCost(fineOverlay, rawTargetCell, fine.Data[rawTargetCell]));
            PointF routeStart = playerCellWalkable ? startPoint : FineCenter(fine, fineStartX, fineStartY);
            PointF routeTarget = targetCellWalkable ? targetPoint : FineCenter(fine, fineTargetX, fineTargetY);
            startPoint = routeStart;
            targetPoint = routeTarget;

            // 2. Whole-map coarse A*.
            int coarseStart, coarseTarget;
            if (!SnapCell(coarse, startPoint.X, startPoint.Y, mode, coarseOverlay, coarseSnapRadius, out coarseStart)) return;
            if (!SnapCell(coarse, targetPoint.X, targetPoint.Y, mode, coarseOverlay, coarseSnapRadius, out coarseTarget)) return;
            System.Diagnostics.Stopwatch stageWatch = System.Diagnostics.Stopwatch.StartNew();
            int[] coarsePath = FindPathFull(coarse, coarseSearch, coarseStart, coarseTarget, mode, null, 2, coarseOverlay);
            DebugCoarseMs = stageWatch.Elapsed.TotalMilliseconds;
            if (coarsePath == null || coarsePath.Length == 0) return;

            // 3. Mid corridor refinement.
            stageWatch.Restart();
            int[] midPath = RefineMid(coarsePath, startPoint, targetPoint, mode, overlay);
            DebugMidMs = stageWatch.Elapsed.TotalMilliseconds;

            // 4. Fine corridor refinement (with progressive corridor widening and a
            //    coarse-corridor fallback if the mid path cannot be followed).
            stageWatch.Restart();
            int[] finePath = RefineFine(midPath, coarsePath, startPoint, targetPoint, mode, overlay);
            DebugFineMs = stageWatch.Elapsed.TotalMilliseconds;
            if (finePath == null || finePath.Length == 0) {
                // Corridor fallback for every mode: the narrow band around the coarse
                // guide can miss real detours (or whole land corridors around water).
                // Solve the whole 16 m network directly and refine the 4 m band around
                // that path instead.
                int midStartRoad, midTargetRoad;
                if (SnapCell(mid, startPoint.X, startPoint.Y, mode, midOverlay, 200, out midStartRoad) &&
                    SnapCell(mid, targetPoint.X, targetPoint.Y, mode, midOverlay, 200, out midTargetRoad)) {
                    int[] directMid = FindPathFull(mid, midSearch, midStartRoad, midTargetRoad, mode, null, 1, midOverlay);
                    if (directMid != null && directMid.Length > 1) {
                        int[] retry = RefineFine(directMid, directMid, startPoint, targetPoint, mode, overlay);
                        if (retry != null && retry.Length > 0) {
                            finePath = retry;
                            DebugFineCoarseFallback = true;
                        }
                    }
                }
            }
            if ((finePath == null || finePath.Length == 0) && mode != RoadRouteMode.Quickest) {
                // Last resort: walking/driving cross-country straight corridor.  The
                // corridor is widened until the 4 m grid connects, which handles narrow
                // land bridges / lake shores that the mid-layer corridor misses.
                int[] direct = TryDirectFineCorridor(startPoint, targetPoint, mode, fineOverlay);
                if (direct != null && direct.Length > 0) {
                    finePath = direct;
                    DebugFineCoarseFallback = true;
                }
            }
            if (finePath == null || finePath.Length == 0) return;
            double rawLength;
            DebugFinePathCost = FinePathCost(finePath, fine, fineOverlay, mode, out rawLength);
            if ((mode == RoadRouteMode.Shortest || mode == RoadRouteMode.PavedOnly) && rawLength > straight * 3) {
                // Dense obstacles can force a long detour inside a coarse guide. Compare another
                // fine-grid corridor using exactly the same terrain and directional grade costs.
                int[] direct = TryDirectFineCorridor(startPoint, targetPoint, mode, fineOverlay);
                if (direct != null && direct.Length > 0) {
                    double directLength;
                    double other = FinePathCost(direct, fine, fineOverlay, mode, out directLength);
                    if (directLength < rawLength && other < DebugFinePathCost) { finePath = direct; DebugFinePathCost = other; }
                }
            }

            // 5. Assemble polyline: exact ends + one point per fine cell.
            List<PointF> points = new List<PointF>(finePath.Length + 2);
            points.Add(startPoint);
            for (int i = 0; i < finePath.Length; i++) {
                int cell = finePath[i];
                points.Add(FineCenter(fine, cell % fine.Width, cell / fine.Width));
            }
            if (finePath.Length == 1) {
                // Same cell: straight line inside a single walkable cell.
                points.Add(targetPoint);
            } else {
                points.Add(targetPoint);
            }            RemoveDuplicatePoints(points);

            // 6. String pulling (line of sight through fine cells).
            stageWatch.Restart();
            List<PointF> simplified = StringPull(points, mode, fine, fineOverlay);
            DebugPullMs = stageWatch.Elapsed.TotalMilliseconds;
            if (simplified.Count < 2) return;
            // Off-road start/target: drive/walk straight to the first road and from the
            // last road point to the destination (same rule for both ends).
            if (RoadRouter.DistanceMeters(playerPt, simplified[0]) > 1.0) simplified.Insert(0, playerPt);
            if (RoadRouter.DistanceMeters(simplified[simplified.Count - 1], targetPt) > 1.0) simplified.Add(targetPt);

            // 7. Metrics from the actual polyline and the fine cost surface.
            stageWatch.Restart();
            PointF[] polyline = simplified.ToArray();
            for (int j = 1; j < polyline.Length; j++)
                if (!LineClear(polyline[j-1], polyline[j], mode, fine, fineOverlay)) return;
            double total = 0;
            double seconds = 0;
            double paved = 0;
            double dirt = 0;
            double unknown = 0;
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
                    int x = (int)Math.Floor(u * fine.Width);
                    int y = (int)Math.Floor(v * fine.Height);
                    if (x < 0) x = 0; else if (x >= fine.Width) x = fine.Width - 1;
                    if (y < 0) y = 0; else if (y >= fine.Height) y = fine.Height - 1;
                    int cell = y * fine.Width + x;
                    byte cost = TrailCost(fineOverlay, cell, fine.Data[cell]);
                    double multiplier = TrailMultiplier(fineOverlay, cell);
                    double speed = TravelSpeedFor(mode, cost, multiplier);
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
            result.RoadEntryPoint = polyline[0];
            result.RoadExitPoint = polyline[polyline.Length - 1];
            bool startIsPlayer = polyline.Length >= 2 && RoadRouter.DistanceMeters(playerPt, polyline[0]) <= 1.0;
            bool endIsTarget = polyline.Length >= 2 && RoadRouter.DistanceMeters(targetPt, polyline[polyline.Length - 1]) <= 1.0;
            result.EntryDistanceMeters = startIsPlayer && polyline.Length >= 2 ? RoadRouter.DistanceMeters(playerPt, polyline[1]) : 0;
            result.ExitDistanceMeters = endIsTarget && polyline.Length >= 2 ? RoadRouter.DistanceMeters(targetPt, polyline[polyline.Length - 2]) : 0;
            DebugMetricsMs = stageWatch.Elapsed.TotalMilliseconds;
            result.Success = true;
        }

        static PointF FineCenter(GridLevel level, int x, int y) {
            return new PointF((float)((x + 0.5) / level.Width), (float)((y + 0.5) / level.Height));
        }

        static int ClampCell(int value, int size) {
            if (value < 0) return 0;
            if (value >= size) return size - 1;
            return value;
        }

        static PointF ClampPoint(PointF point) {
            const float epsilon = 1e-7f;
            float x = point.X, y = point.Y;
            if (x < epsilon) x = epsilon;
            if (y < epsilon) y = epsilon;
            if (x > 1f - epsilon) x = 1f - epsilon;
            if (y > 1f - epsilon) y = 1f - epsilon;
            return new PointF(x, y);
        }

        // Grounds a coarse/mid guide point onto the nearest fine cell walkable in the
        // requested mode.  Mid cells can be road-priority blocks whose centre is a
        // building; using the centre directly as a fine corridor guide is what used to
        // force the fine band to widen unnecessarily.  When preferRoad is set (all
        // driving/riding modes), choose the nearest paved road or cost-2 trail pixel so
        // the narrow fine corridor follows the actual white/yellow line.
        PointF GroundGuidePoint(GridLevel fine, RoadRouteMode mode, PointF point, int radius, byte[] overlay, bool preferRoad) {
            int width = fine.Width, height = fine.Height;
            byte[] data = fine.Data;
            int cx = ClampCell((int)Math.Floor(point.X * width), width);
            int cy = ClampCell((int)Math.Floor(point.Y * height), height);
            int center = cy * width + cx;
            byte centerCost = TrailCost(overlay, center, data[center]);
            if (Passable(mode, centerCost) && (!preferRoad || centerCost <= CostDirt)) return FineCenter(fine, cx, cy);
            int bestCell = -1;
            long bestDistance = long.MaxValue;
            for (int r = 1; r <= radius; r++) {
                for (int dy = -r; dy <= r; dy++) {
                    for (int dx = -r; dx <= r; dx++) {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        int candidate = y * width + x;
                        byte cost = TrailCost(overlay, candidate, data[candidate]);
                        if (!Passable(mode, cost)) continue;
                        if (preferRoad && cost > CostDirt) continue;
                        long distance = (long)dx * dx + (long)dy * dy;
                        if (distance < bestDistance) { bestDistance = distance; bestCell = candidate; }
                    }
                }
                if (bestCell >= 0) break;
            }
            if (bestCell >= 0) return FineCenter(fine, bestCell % width, bestCell / width);
            if (preferRoad) {
                // No road/trail pixel near this guide point: fall back to any walkable
                // fine cell so the corridor still covers this mid leg.
                return GroundGuidePoint(fine, mode, point, radius, overlay, false);
            }
            return point;
        }
        static void RemoveDuplicatePoints(List<PointF> points) {
            for (int i = points.Count - 1; i > 0; i--) {
                if (Math.Abs(points[i].X - points[i - 1].X) < 1e-7f && Math.Abs(points[i].Y - points[i - 1].Y) < 1e-7f)
                    points.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------
        // Cell snapping
        // ------------------------------------------------------------------
        // Road-only mode may snap to a tiny isolated road fragment next to the player
        // (or target) and then fail to reach the main network.  Label the road cells of
        // every resolution once and only snap to the largest road component.
        void EnsureRoadComponents() {
            if (roadComponentsReady) return;
            roadComponentsReady = true;
            fineRoadLabel = BuildRoadLabels(levels[LevelFine], out fineRoadMain);
            midRoadLabel = BuildRoadLabels(levels[LevelMid], out midRoadMain);
            coarseRoadLabel = BuildRoadLabels(levels[LevelCoarse], out coarseRoadMain);
        }

        static bool RoadDiagonalClear(GridLevel level, byte[] terrain, int x, int y, int nx, int ny) {
            if (x == nx || y == ny) return true;
            int sideA = y * level.Width + nx, sideB = ny * level.Width + x;
            byte a = level.Data[sideA], b = level.Data[sideB];
            return a != CostUndefined && a < CostWater && b != CostUndefined && b < CostWater &&
                (terrain == null || (terrain[sideA] < 4 && terrain[sideB] < 4));
        }

        ushort[] BuildRoadLabels(GridLevel level, out ushort mainLabel) {
            int w = level.Width, h = level.Height, n = w * h;
            byte[] terrain = level == levels[LevelFine] ? terrainFine : (level == levels[LevelMid] ? terrainMid : terrainCoarse);
            ushort[] labels = new ushort[n];
            int[] queue = new int[n];
            byte[] data = level.Data;
            ushort next = 1;
            int bestSize = 0;
            mainLabel = 0;
            for (int start = 0; start < n; start++) {
                if (labels[start] != 0 || data[start] > CostDirt) continue;
                int head = 0, tail = 0, size = 0;
                queue[tail++] = start;
                labels[start] = next;
                while (head < tail) {
                    int cell = queue[head++];
                    size++;
                    int x = cell % w, y = cell / w;
                    for (int k = 0; k < 8; k++) {
                        int nx = x + NeighborX[k], ny = y + NeighborY[k];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int nc = ny * w + nx;
                        if (labels[nc] != 0 || data[nc] > CostDirt || !RoadDiagonalClear(level, terrain, x, y, nx, ny)) continue;
                        labels[nc] = next;
                        queue[tail++] = nc;
                    }
                }
                if (size > bestSize) { bestSize = size; mainLabel = next; }
                next++;
            }
            return labels;
        }

        ushort[] RoadLabelsFor(GridLevel level) {
            if (level == levels[LevelFine]) return fineRoadLabel;
            if (level == levels[LevelMid]) return midRoadLabel;
            if (level == levels[LevelCoarse]) return coarseRoadLabel;
            return null;
        }

        ushort RoadMainFor(GridLevel level) {
            if (level == levels[LevelFine]) return fineRoadMain;
            if (level == levels[LevelMid]) return midRoadMain;
            return coarseRoadMain;
        }

        bool NearestRoadComponent(GridLevel level, ushort[] labels, float u, float v, int maxRadius, out int cell, out ushort comp) {
            cell = -1; comp = 0;
            int width = level.Width, height = level.Height;
            int cx = (int)Math.Floor(u * width), cy = (int)Math.Floor(v * height);
            if (cx < 0) cx = 0; else if (cx >= width) cx = width - 1;
            if (cy < 0) cy = 0; else if (cy >= height) cy = height - 1;
            long best = long.MaxValue;
            for (int r = 0; r <= maxRadius; r++) {
                for (int dy = -r; dy <= r; dy++) {
                    for (int dx = -r; dx <= r; dx++) {
                        if (r > 0 && Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        int index = y * width + x;
                        if (level.Data[index] > CostDirt) continue;
                        long d = (long)dx * dx + (long)dy * dy;
                        if (d < best) { best = d; cell = index; comp = labels == null ? (ushort)0 : labels[index]; }
                    }
                }
                if (cell >= 0) break;
            }
            return cell >= 0;
        }

        ushort NearestComponent(GridLevel level, ushort[] labels, float u, float v, int maxRadius) {
            int cell; ushort comp;
            return NearestRoadComponent(level, labels, u, v, maxRadius, out cell, out comp) ? comp : (ushort)0;
        }

        ushort LevelForcedComponent(GridLevel level) {
            if (level == levels[LevelFine]) return roadOnlyForceComp;
            if (level == levels[LevelMid]) return roadOnlyMidComp;
            if (level == levels[LevelCoarse]) return roadOnlyCoarseComp;
            return 0;
        }

        bool SnapCell(GridLevel level, float u, float v, RoadRouteMode mode, byte[] overlay, int maxRadius, out int cell) {
            cell = -1;
            int width = level.Width, height = level.Height;
            int cx = (int)Math.Floor(u * width);
            int cy = (int)Math.Floor(v * height);
            if (cx < 0) cx = 0; else if (cx >= width) cx = width - 1;
            if (cy < 0) cy = 0; else if (cy >= height) cy = height - 1;
            int center = cy * width + cx;
            ushort[] labels = (mode == RoadRouteMode.Quickest && roadComponentsReady) ? RoadLabelsFor(level) : null;
            ushort mainLabel = labels == null ? (ushort)0 : LevelForcedComponent(level);
            bool centerOk = Passable(mode, TrailCost(overlay, center, level.Data[center])) && (labels == null || labels[center] == mainLabel);
            if (centerOk) {
                cell = center;
                return true;
            }
            int bestCell = -1;
            long bestDistance = long.MaxValue;
            int firstRing = -1;
            for (int r = 1; r <= maxRadius; r++) {
                for (int dy = -r; dy <= r; dy++) {
                    for (int dx = -r; dx <= r; dx++) {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        int candidate = y * width + x;
                        if (!Passable(mode, TrailCost(overlay, candidate, level.Data[candidate]))) continue;
                        if (labels != null && labels[candidate] != mainLabel) continue;
                        long distance = (long)dx * dx + (long)dy * dy;
                        if (distance < bestDistance) {
                            bestDistance = distance;
                            bestCell = candidate;
                        }
                    }
                }
                if (bestCell >= 0) {
                    if (firstRing < 0) firstRing = r;
                    if (r >= firstRing + 1) break;
                }
            }
            if (bestCell < 0) return false;
            cell = bestCell;
            return true;
        }

        // Admissible 8-connected (octile) grid heuristic in metric units.  The
        // smallest per-axis cell size is used so slightly anisotropic layers never
        // overestimate the remaining travel cost.  For Shortest this remains
        // admissible because every terrain penalty in ShortestTerrainPenalty is >= 1.
        // The learned-trail factor is not folded in here on purpose: keeping the
        // navA1 heuristic keeps Quickest at ~36 ms p50; with a 0.55 trail lower bound
        // A* expands ~2x more nodes (documented suboptimality bound when learned
        // trails make an edge cheaper).
        static float Heuristic(int x, int y, int targetX, int targetY, double minCellMeters,
            bool distanceMode, double maxSpeed, double weight) {
            double dx = Math.Abs(targetX - x);
            double dy = Math.Abs(targetY - y);
            double octile;
            if (dx > dy) octile = dx + OctileStraight * dy;
            else octile = dy + OctileStraight * dx;
            double meters = octile * minCellMeters;
            if (!distanceMode) meters = meters / maxSpeed;
            return (float)(meters * weight);
        }

        // ------------------------------------------------------------------
        // Full-grid A* (coarse layer and masked mid layer)
        // ------------------------------------------------------------------
        int[] FindPathFull(GridLevel level, FullSearch state, int start, int target, RoadRouteMode mode, byte[] bandMask, int phase, byte[] overlay) {
            int width = level.Width, height = level.Height;
            state.Prepare(level.CellCount);
            state.NextEpoch();
            uint epoch = state.Epoch;
            state.Heap.Clear();
            float[] g = state.G;
            int[] parent = state.Parent;
            uint[] seen = state.Seen;
            uint[] closed = state.Closed;
            MinHeap heap = state.Heap;
            byte[] data = level.Data;
            bool hasOverlay = overlay != null;
            double metersX = level.MetersX, metersY = level.MetersY;
            double minCell = Math.Min(metersX, metersY);
            double diagonal = Math.Sqrt(metersX * metersX + metersY * metersY);
            bool distanceMode = DistanceMode(mode);
            bool quickestMode = mode == RoadRouteMode.Quickest;
            double maxSpeed = MaxSpeedFor(mode);
            double weight = quickestMode ? QuickestHeuristicWeight : mode == RoadRouteMode.Shortest ? ShortestHeuristicWeight : 1.0;
            int targetX = target % width, targetY = target / width;
            g[start] = 0;
            parent[start] = -1;
            seen[start] = epoch;
            heap.Push(0f, 0f, start);
            while (heap.Count > 0) {
                float priority;
                int current = heap.Pop(out priority);
                if (closed[current] == epoch) continue;
                closed[current] = epoch;
                if (phase == 2) DebugCoarseExpansions++;
                else if (phase == 1) DebugMidExpansions++;
                if (current == target) return Reconstruct(state, start, target);
                int cx = current % width, cy = current / width;
                double baseCost = g[current];
                for (int k = 0; k < 8; k++) {
                    int dx = NeighborX[k], dy = NeighborY[k];
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int next = ny * width + nx;
                    if (bandMask != null && bandMask[next] == 0) continue;
                    byte nextCost = data[next];
                    double multiplier = 1.0;
                    if (hasOverlay) {
                        byte code = overlay[next];
                        if (code != 0) {
                            if ((code & TrailFlagUnblock) != 0 && nextCost == CostBlocked)
                                nextCost = (code & TrailFlagPaved) != 0 ? CostPaved : CostOpen;
                            int factor = code & TrailCodeMask;
                            if (factor != 0) multiplier = factor * 0.01;
                        }
                    }
                    if (!Passable(mode, nextCost)) continue;
                    if (dx != 0 && dy != 0 && mode == RoadRouteMode.Quickest) { // legacy path is never used by Quickest (exact router)
                        byte sideA = data[cy * width + nx];
                        byte sideB = data[ny * width + cx];
                        if (hasOverlay) {
                            sideA = TrailCost(overlay, cy * width + nx, sideA);
                            sideB = TrailCost(overlay, ny * width + cx, sideB);
                        }
                        if (!Passable(mode, sideA) && !Passable(mode, sideB)) continue;
                    }
                    double meters;
                    if (dx == 0) meters = Math.Abs(dy) * metersY;
                    else if (dy == 0) meters = Math.Abs(dx) * metersX;
                    else meters = diagonal;
                    double factor2 = StepFactor(nextCost, mode);
                    if (mode != RoadRouteMode.Quickest) {
                        byte[] terrain = TerrainForLevel(level);
                        if (terrain != null && next < terrain.Length) factor2 *= TerrainCost(mode, terrain[next]);
                    }
                    double candidate = baseCost + meters * factor2 * multiplier;
                    if (seen[next] == epoch && g[next] <= candidate) continue;
                    seen[next] = epoch;
                    g[next] = (float)candidate;
                    parent[next] = current;
                    float heuristic = Heuristic(nx, ny, targetX, targetY, minCell, distanceMode, maxSpeed, weight);
                    heap.Push((float)candidate + heuristic, heuristic, next);
                }
            }
            return null;
        }

        int[] Reconstruct(FullSearch state, int start, int target) {
            List<int> reversed = new List<int>(256);
            int current = target;
            int guard = 0;
            while (true) {
                reversed.Add(current);
                if (current == start) break;
                current = state.Parent[current];
                if (++guard > state.Parent.Length + 4) return null;
            }
            reversed.Reverse();
            return reversed.ToArray();
        }

        // ------------------------------------------------------------------
        // Mid (16 m) refinement inside a corridor around the coarse path
        // ------------------------------------------------------------------
        int[] RefineMid(int[] coarsePath, PointF startPoint, PointF targetPoint, RoadRouteMode mode, TrailOverlay overlay) {
            GridLevel coarse = levels[LevelCoarse];
            GridLevel mid = levels[LevelMid];
            byte[] midOverlay = overlay == null ? null : overlay.Mid;
            if (midBand == null) midBand = new byte[mid.CellCount];
            List<PointF> guide = new List<PointF>(coarsePath.Length + 2);
            guide.Add(startPoint);
            for (int i = 0; i < coarsePath.Length; i++)
                guide.Add(new PointF((float)((coarsePath[i] % coarse.Width + 0.5) / coarse.Width), (float)((coarsePath[i] / coarse.Width + 0.5) / coarse.Height)));
            guide.Add(targetPoint);
            int bufferMid = (int)Math.Ceiling(2.0 * coarse.MetersX / mid.MetersX);
            if (bufferMid < 1) bufferMid = 1;
            int startMid, targetMid;
            int midSnapRadius = mode == RoadRouteMode.Quickest ? (wideSnapRetry ? 800 : 200) : 32;
            if (!SnapCell(mid, startPoint.X, startPoint.Y, mode, midOverlay, midSnapRadius, out startMid)) return null;
            if (!SnapCell(mid, targetPoint.X, targetPoint.Y, mode, midOverlay, midSnapRadius, out targetMid)) return null;
            for (int attempt = 0; attempt < MidBuffers.Length; attempt++) {
                int buffer = bufferMid * MidBuffers[attempt] / 2;
                ClearBand(midBand, midBandCells);
                BuildMidBand(guide, buffer, mid);
                MarkMaskBox(midBand, midBandCells, mid.Width, mid.Height, startMid % mid.Width, startMid / mid.Width, buffer + 2);
                MarkMaskBox(midBand, midBandCells, mid.Width, mid.Height, targetMid % mid.Width, targetMid / mid.Width, buffer + 2);
                DebugMidBandCells = midBandCells.Count;
                int[] path = FindPathFull(mid, midSearch, startMid, targetMid, mode, midBand, 1, midOverlay);
                if (path != null && path.Length > 0) { DebugMidBandAttempt = attempt; return path; }
                ClearBand(midBand, midBandCells);
            }
            return null;
        }

        void BuildMidBand(List<PointF> guide, int bufferCells, GridLevel mid) {
            int count = guide.Count;
            for (int i = 0; i < count - 1; i++) {
                MarkBandSegment(midBand, midBandCells, mid.Width, mid.Height, guide[i], guide[i + 1], bufferCells);
            }
            if (count == 1) MarkMaskBox(midBand, midBandCells, mid.Width, mid.Height,
                (int)Math.Floor(guide[0].X * mid.Width), (int)Math.Floor(guide[0].Y * mid.Height), bufferCells);
        }

        // ------------------------------------------------------------------
        // Fine (4 m) refinement inside a corridor around the mid / coarse path
        // ------------------------------------------------------------------
        int[] RefineFine(int[] midPath, int[] coarsePath, PointF startPoint, PointF targetPoint, RoadRouteMode mode, TrailOverlay overlay) {
            GridLevel fine = levels[LevelFine];
            GridLevel mid = levels[LevelMid];
            GridLevel coarse = levels[LevelCoarse];
            byte[] fineOverlay = overlay == null ? null : overlay.Fine;
            // Ground the corridor guide on the road/trail pixels themselves: mid cells
            // are road-priority blocks, so their centres can sit on a building or grass
            // far from the actual 4 m trail.  A road-preferring guide keeps the narrow
            // (+-2 mid cell) fine corridor valid and avoids the wasted widen attempt.
            bool preferFineRoad = mode == RoadRouteMode.Quickest || mode == RoadRouteMode.Shortest || mode == RoadRouteMode.PavedOnly;
            int startFine, targetFine;
            int fineRefineSnap = mode == RoadRouteMode.Quickest ? (wideSnapRetry ? 2400 : 640) : 96;
            if (!SnapCell(fine, startPoint.X, startPoint.Y, mode, fineOverlay, fineRefineSnap, out startFine)) return null;
            if (!SnapCell(fine, targetPoint.X, targetPoint.Y, mode, fineOverlay, fineRefineSnap, out targetFine)) return null;
            int startX = startFine % fine.Width, startY = startFine / fine.Width;
            int targetX = targetFine % fine.Width, targetY = targetFine / fine.Width;
            EnsureFineSearchState(fine);

            if (midPath != null && midPath.Length > 0) {
                List<PointF> guide = new List<PointF>(midPath.Length + 2);
                guide.Add(startPoint);
                for (int i = 0; i < midPath.Length; i++)
                    guide.Add(GroundGuidePoint(fine, mode, new PointF((float)((midPath[i] % mid.Width + 0.5) / mid.Width), (float)((midPath[i] / mid.Width + 0.5) / mid.Height)), 6, fineOverlay, preferFineRoad));
                guide.Add(targetPoint);
                int bufferFine = (int)Math.Ceiling(2.0 * mid.MetersX / fine.MetersX);
                if (bufferFine < 1) bufferFine = 1;
                for (int attempt = 0; attempt < FineBuffers.Length; attempt++) {
                    int buffer = bufferFine * FineBuffers[attempt] / 2;
                    int[] path = TryFineBand(guide, buffer, mode, startX, startY, targetX, targetY, fineOverlay);
                    if (path != null) { DebugFineBandAttempt = attempt; return path; }
                }
            } else {
                // Mid refinement failed: use the coarse path itself as the fine corridor.
                List<PointF> guide = new List<PointF>(coarsePath.Length + 2);
                guide.Add(startPoint);
                for (int i = 0; i < coarsePath.Length; i++)
                    guide.Add(GroundGuidePoint(fine, mode, new PointF((float)((coarsePath[i] % coarse.Width + 0.5) / coarse.Width), (float)((coarsePath[i] / coarse.Width + 0.5) / coarse.Height)), 16, fineOverlay, preferFineRoad));
                guide.Add(targetPoint);
                int bufferFine = (int)Math.Ceiling(2.0 * coarse.MetersX / fine.MetersX);
                if (bufferFine < 1) bufferFine = 1;
                for (int attempt = 0; attempt < CoarseGuideBuffers.Length; attempt++) {
                    int buffer = bufferFine * CoarseGuideBuffers[attempt] / 2;
                    int[] path = TryFineBand(guide, buffer, mode, startX, startY, targetX, targetY, fineOverlay);
                    if (path != null) { DebugFineBandAttempt = attempt; DebugFineCoarseFallback = true; return path; }
                }
            }
            return null;
        }

        int[] TryDirectFineCorridor(PointF startPoint, PointF targetPoint, RoadRouteMode mode, byte[] fineOverlay) {
            GridLevel fine = levels[LevelFine];
            int startFine, targetFine;
            if (!SnapCell(fine, startPoint.X, startPoint.Y, mode, fineOverlay, 96, out startFine)) return null;
            if (!SnapCell(fine, targetPoint.X, targetPoint.Y, mode, fineOverlay, 96, out targetFine)) return null;
            int startX = startFine % fine.Width, startY = startFine / fine.Width;
            int targetX = targetFine % fine.Width, targetY = targetFine / fine.Width;
            EnsureFineSearchState(fine);
            List<PointF> guide = new List<PointF>(2);
            guide.Add(startPoint);
            guide.Add(targetPoint);
            int[] widths = new int[] { 8, 32, 128, 512 }; // 32 m .. 2 km half-width
            for (int i = 0; i < widths.Length; i++) {
                int[] path = TryFineBand(guide, widths[i], mode, startX, startY, targetX, targetY, fineOverlay);
                if (path != null && path.Length > 0) return path;
            }
            return null;
        }

        int[] TryFineBand(List<PointF> guide, int bufferCells, RoadRouteMode mode,
            int startX, int startY, int targetX, int targetY, byte[] overlay) {
            GridLevel fine = levels[LevelFine];
            ClearBand(fineBand, fineBandCells);
            if (fineAllocBlocks.Count > 0) {
                for (int i = 0; i < fineAllocBlocks.Count; i++) fineBlockBase[fineAllocBlocks.Items[i]] = -1;
                fineAllocBlocks.Clear();
            }
            for (int i = 0; i < guide.Count - 1; i++)
                MarkBandSegment(fineBand, fineBandCells, fine.Width, fine.Height, guide[i], guide[i + 1], bufferCells);
            if (guide.Count == 1)
                MarkMaskBox(fineBand, fineBandCells, fine.Width, fine.Height, startX, startY, bufferCells);
            MarkMaskBox(fineBand, fineBandCells, fine.Width, fine.Height, startX, startY, bufferCells + 2);
            MarkMaskBox(fineBand, fineBandCells, fine.Width, fine.Height, targetX, targetY, bufferCells + 2);
            DebugFineBandCells = fineBandCells.Count;
            if (fineBandCells.Count > MaxFineBandCells) return null;
            // Allocate the small local arrays used by the banded A*: one block of
            // 16x16 cells per 64 m square touched by the corridor.
            for (int i = 0; i < fineBandCells.Count; i++) {
                int cell = fineBandCells.Items[i];
                int block = (cell / fine.Width >> FineBlockShift) * fineBlockCols + ((cell % fine.Width) >> FineBlockShift);
                if (fineBlockBase[block] < 0) {
                    fineBlockBase[block] = fineAllocBlocks.Count << (FineBlockShift * 2);
                    fineAllocBlocks.Add(block);
                    if (fineAllocBlocks.Count > MaxFineBlocks) return null;
                }
            }
            int localCells = fineAllocBlocks.Count << (FineBlockShift * 2);
            if (localCells == 0) return null;
            EnsureFineCapacity(localCells);
            int startLocal = FineLocal(startX, startY);
            int targetLocal = FineLocal(targetX, targetY);
            if (startLocal < 0 || targetLocal < 0) return null;
            return FindPathFine(mode, startLocal, targetLocal, startX, startY, targetX, targetY, fine, overlay);
        }

        void EnsureFineSearchState(GridLevel fine) {
            int blocksPerRow = (fine.Width + FineBlockSize - 1) / FineBlockSize;
            int blockRows = (fine.Height + FineBlockSize - 1) / FineBlockSize;
            if (fineBand == null) fineBand = new byte[fine.CellCount];
            if (fineBlockBase == null || fineBlockCols != blocksPerRow || fineBlockRows != blockRows) {
                fineBlockCols = blocksPerRow;
                fineBlockRows = blockRows;
                fineBlockBase = new int[fineBlockCols * fineBlockRows];
                for (int i = 0; i < fineBlockBase.Length; i++) fineBlockBase[i] = -1;
                fineAllocBlocks.Clear();
            }
            if (fineG.Length == 0) EnsureFineCapacity(1024);
        }

        void EnsureFineCapacity(int cells) {
            if (fineG.Length >= cells) return;
            int capacity = 1024;
            while (capacity < cells) capacity *= 2;
            fineG = new float[capacity];
            fineParent = new int[capacity];
            fineSeen = new uint[capacity];
            fineClosed = new uint[capacity];
            fineEpoch = 1;
        }

        int FineBlockIndex(int x, int y) {
            return (y >> FineBlockShift) * fineBlockCols + (x >> FineBlockShift);
        }

        int FineLocal(int x, int y) {
            int baseIndex = fineBlockBase[FineBlockIndex(x, y)];
            if (baseIndex < 0) return -1;
            return baseIndex + ((y & FineBlockMask) << FineBlockShift) + (x & FineBlockMask);
        }

        void FineCell(int local, out int x, out int y) {
            int slot = local >> (FineBlockShift * 2);
            int block = fineAllocBlocks.Items[slot];
            int blockX = block % fineBlockCols;
            int blockY = block / fineBlockCols;
            x = (blockX << FineBlockShift) + (local & FineBlockMask);
            y = (blockY << FineBlockShift) + ((local >> FineBlockShift) & FineBlockMask);
        }

        int[] FindPathFine(RoadRouteMode mode, int startLocal, int targetLocal,
            int startX, int startY, int targetX, int targetY, GridLevel fine, byte[] overlay) {
            fineEpoch++;
            if (fineEpoch == 0) {
                Array.Clear(fineSeen, 0, fineSeen.Length);
                Array.Clear(fineClosed, 0, fineClosed.Length);
                fineEpoch = 1;
            }
            uint epoch = fineEpoch;
            fineHeap.Clear();
            byte[] data = fine.Data;
            byte[] band = fineBand;
            bool hasOverlay = overlay != null;
            int width = fine.Width, height = fine.Height;
            double metersX = fine.MetersX, metersY = fine.MetersY;
            double minCell = Math.Min(metersX, metersY);
            double diagonal = Math.Sqrt(metersX * metersX + metersY * metersY);
            bool distanceMode = DistanceMode(mode);
            bool quickestMode = mode == RoadRouteMode.Quickest;
            double maxSpeed = MaxSpeedFor(mode);
            double weight = quickestMode ? QuickestHeuristicWeight : mode == RoadRouteMode.Shortest ? ShortestHeuristicWeight : 1.0;
            fineG[startLocal] = 0;
            fineParent[startLocal] = -1;
            fineSeen[startLocal] = epoch;
            float startHeuristic = Heuristic(startX, startY, targetX, targetY, minCell, distanceMode, maxSpeed, weight);
            fineHeap.Push(startHeuristic, startHeuristic, startLocal);
            while (fineHeap.Count > 0) {
                float priority;
                int current = fineHeap.Pop(out priority);
                if (fineClosed[current] == epoch) continue;
                fineClosed[current] = epoch;
                DebugFineExpansions++;
                if (current == targetLocal) return ReconstructFine(startLocal, targetLocal);
                int cx, cy;
                FineCell(current, out cx, out cy);
                double baseCost = fineG[current];
                for (int k = 0; k < 8; k++) {
                    int dx = NeighborX[k], dy = NeighborY[k];
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int nextCell = ny * width + nx;
                    if (band[nextCell] == 0) continue;
                    byte nextCost = data[nextCell];
                    double multiplier = 1.0;
                    if (hasOverlay) {
                        byte code = overlay[nextCell];
                        if (code != 0) {
                            if ((code & TrailFlagUnblock) != 0 && nextCost == CostBlocked)
                                nextCost = (code & TrailFlagPaved) != 0 ? CostPaved : CostOpen;
                            int factor = code & TrailCodeMask;
                            if (factor != 0) multiplier = factor * 0.01;
                        }
                    }
                    if (!Passable(mode, nextCost)) continue;
                    if (dx != 0 && dy != 0) {
                        byte sideA = data[cy * width + nx];
                        byte sideB = data[ny * width + cx];
                        if (hasOverlay) {
                            sideA = TrailCost(overlay, cy * width + nx, sideA);
                            sideB = TrailCost(overlay, ny * width + cx, sideB);
                        }
                        bool clearA = Passable(mode, sideA) && (!distanceMode || terrainFine[cy * width + nx] < 4);
                        bool clearB = Passable(mode, sideB) && (!distanceMode || terrainFine[ny * width + cx] < 4);
                        if (!clearA || !clearB) continue;
                    }
                    int nextLocal = FineLocal(nx, ny);
                    if (nextLocal < 0) continue;
                    double meters = dx == 0 ? Math.Abs(dy) * metersY : dy == 0 ? Math.Abs(dx) * metersX : diagonal;
                    // Vehicles cannot climb near-vertical ground: class-3 terrain
                    // (cliffs / dam walls / very steep mountain sides) is impassable in
                    // the fine layer for the two off-road driving modes.
                    if ((mode == RoadRouteMode.Shortest || mode == RoadRouteMode.PavedOnly) &&
                        terrainFine != null && nextCell < terrainFine.Length && terrainFine[nextCell] >= 4) continue;
                    double factor2 = StepFactor(nextCost, mode);
                    if (mode != RoadRouteMode.Quickest && terrainFine != null && nextCell < terrainFine.Length)
                        factor2 *= TerrainCost(mode, terrainFine[nextCell]);
                    factor2 *= GradeFactor(nextCell, dx * metersX / meters, dy * metersY / meters, mode);
                    double candidate = baseCost + meters * factor2 * multiplier;
                    if (fineSeen[nextLocal] == epoch && fineG[nextLocal] <= candidate) continue;
                    fineSeen[nextLocal] = epoch;
                    fineG[nextLocal] = (float)candidate;
                    fineParent[nextLocal] = current;
                    float heuristic = Heuristic(nx, ny, targetX, targetY, minCell, distanceMode, maxSpeed, weight);
                    fineHeap.Push((float)candidate + heuristic, heuristic, nextLocal);
                }
            }
            return null;
        }

        int[] ReconstructFine(int startLocal, int targetLocal) {
            List<int> reversed = new List<int>(128);
            int current = targetLocal;
            int guard = 0;
            List<int> cells = new List<int>(128);
            while (true) {
                cells.Add(current);
                if (current == startLocal) break;
                current = fineParent[current];
                if (++guard > fineParent.Length + 4) return null;
            }
            cells.Reverse();
            // Convert local indices to global fine cell indices.
            int[] result = new int[cells.Count];
            for (int i = 0; i < cells.Count; i++) {
                int x, y;
                FineCell(cells[i], out x, out y);
                result[i] = y * levels[LevelFine].Width + x;
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Corridor bookkeeping
        // ------------------------------------------------------------------
        static void ClearBand(byte[] mask, CellList cells) {
            if (mask == null) return;
            for (int i = 0; i < cells.Count; i++) mask[cells.Items[i]] = 0;
            cells.Clear();
        }

        static void MarkMaskBox(byte[] mask, CellList cells, int width, int height, int centerX, int centerY, int radius) {
            int x0 = centerX - radius, x1 = centerX + radius, y0 = centerY - radius, y1 = centerY + radius;
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 >= width) x1 = width - 1;
            if (y1 >= height) y1 = height - 1;
            for (int y = y0; y <= y1; y++) {
                int row = y * width;
                for (int x = x0; x <= x1; x++) {
                    int cell = row + x;
                    if (mask[cell] == 0) {
                        mask[cell] = 1;
                        cells.Add(cell);
                    }
                }
            }
        }

        static void MarkBandSegment(byte[] mask, CellList cells, int width, int height,
            PointF a, PointF b, int bufferCells) {
            double ax = a.X * width, ay = a.Y * height;
            double bx = b.X * width, by = b.Y * height;
            double dx = bx - ax, dy = by - ay;
            double longest = Math.Max(Math.Abs(dx), Math.Abs(dy));
            int steps = 0;
            if (bufferCells > 0 && longest > bufferCells)
                steps = (int)Math.Ceiling(longest / bufferCells);
            if (steps > 8192) steps = 8192;
            double stepX = steps == 0 ? 0 : dx / steps;
            double stepY = steps == 0 ? 0 : dy / steps;
            double x = ax, y = ay;
            for (int s = 0; s <= steps; s++) {
                int cx = (int)Math.Floor(x);
                int cy = (int)Math.Floor(y);
                MarkMaskBox(mask, cells, width, height, cx, cy, bufferCells);
                x += stepX;
                y += stepY;
            }
        }

        // ------------------------------------------------------------------
        // navA28 (C1): topological junction arm test (road-only turn cues)
        // ------------------------------------------------------------------
        // A real T/X junction is a point where removing a 16 m disc from the cost<=2
        // road mask leaves >=3 arms of the point's own road component reaching the
        // 16..18 m ring.  Plain road-width artifacts (a 2-3 cell wide road has >=3
        // eight-connected neighbours almost everywhere), ordinary bends and
        // straight-through splits produce <3 arms and are excluded.  This mirrors
        // checks/analysis/item34/probes/JunctionProbe.cs ArmsAt() with reusable scratch
        // buffers and an early exit at 3 arms (the caller only needs the >=3 decision).
        const int JunctionWindowRadius = 14; // 56 m window (matches the independent ground-truth detector)
        const int JunctionDiscRadius = 4;    // 16 m removed disc
        const int JunctionWindowSide = JunctionWindowRadius * 2 + 1;
        const int JunctionWindowCells = JunctionWindowSide * JunctionWindowSide;
        // Scratch buffers are allocated inside TopologicalArmsAt on purpose: a single
        // GridRouter instance can be used by two concurrent squad route calculations
        // (SquadRoutePlanner allows two parallel computations), so instance/static scratch
        // arrays would race.  841-element locals are cheap and GC-friendly.

        // Public entry points.  Scratch buffers are allocated per call (thread-safe: a
        // GridRouter instance can serve two concurrent squad routes).  The road-only
        // junction stage uses the overloads that take reusable per-route scratch arrays,
        // which removes thousands of allocations per route (navA28 fix: p50 cost).
        internal int TopologicalArmsAt(float u, float v) {
            return TopologicalArmsAt(u, v, new byte[JunctionWindowCells], new int[JunctionWindowCells], new int[JunctionWindowCells], new int[JunctionWindowCells]);
        }

        internal int TopologicalArmsAtExact(float u, float v) {
            return TopologicalArmsAtExact(u, v, new byte[JunctionWindowCells], new int[JunctionWindowCells], new int[JunctionWindowCells], new int[JunctionWindowCells]);
        }

        // Snap variant: centre = nearest road cell within +-2 cells of (u,v).
        internal int TopologicalArmsAt(float u, float v, byte[] road, int[] full, int[] lab, int[] stack) {
            GridLevel fine = levels[LevelFine];
            if (fine == null) return -1;
            int W = fine.Width;
            int cell = NearestRoadCell(fine, u, v);
            if (cell < 0) return -1;
            return ArmsAtWindow(fine, cell % W, cell / W, road, full, lab, stack);
        }

        // Exact variant: centre = the cell that contains (u,v), NO snap.  Used as the
        // mandatory re-check of a cue point: a point that is not itself on a >=3-arm
        // junction must not be announced (navA28 fix: a sharp bend with arms=1 was cued
        // RIGHT because the +-2 cell snap could borrow a nearby junction's arms).
        internal int TopologicalArmsAtExact(float u, float v, byte[] road, int[] full, int[] lab, int[] stack) {
            GridLevel fine = levels[LevelFine];
            if (fine == null) return -1;
            int W = fine.Width, H = fine.Height;
            int bx = (int)(u * W), by = (int)(v * H);
            if (bx < 0 || by < 0 || bx >= W || by >= H) return -1;
            if (fine.Data[by * W + bx] > CostDirt) return -1;
            return ArmsAtWindow(fine, bx, by, road, full, lab, stack);
        }

        int NearestRoadCell(GridLevel fine, float u, float v) {
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
            if (bx < 0) return -1;
            return by * W + bx;
        }

        int ArmsAtWindow(GridLevel fine, int bx, int by, byte[] road, int[] full, int[] lab, int[] stack) {
            int W = fine.Width, H = fine.Height;
            byte[] data = fine.Data;
            int radius = JunctionWindowRadius, disc = JunctionDiscRadius;
            int side = JunctionWindowSide, n = JunctionWindowCells;
            int x0 = bx - radius, y0 = by - radius;
            if (x0 < 0 || y0 < 0 || x0 + 2 * radius >= W || y0 + 2 * radius >= H) return -1;
            for (int y = 0; y < side; y++) {
                int row = (y0 + y) * W + x0;
                int lrow = y * side;
                for (int x = 0; x < side; x++) road[lrow + x] = (byte)(data[row + x] <= CostDirt ? 1 : 0);
            }
            for (int i = 0; i < n; i++) { full[i] = 0; lab[i] = 0; }
            int labels = 0;
            for (int i = 0; i < n; i++) {
                if (road[i] == 0 || full[i] != 0) continue;
                int cid = ++labels;
                int top = 0;
                stack[top++] = i;
                full[i] = cid;
                while (top > 0) {
                    int cur = stack[--top];
                    int x = cur % side, y = cur / side;
                    for (int k = 0; k < 8; k++) {
                        int nx = x + NeighborX[k], ny = y + NeighborY[k];
                        if (nx < 0 || ny < 0 || nx >= side || ny >= side) continue;
                        int ni = ny * side + nx;
                        if (road[ni] == 0 || full[ni] != 0) continue;
                        full[ni] = cid;
                        stack[top++] = ni;
                    }
                }
            }
            int center = radius * side + radius;
            int targetComponent = full[center];
            if (targetComponent == 0) return -1;
            int arms = 0;
            for (int i = 0; i < n; i++) {
                if (road[i] == 0 || lab[i] != 0) continue;
                int x = i % side, y = i / side;
                int ddx = x - radius, ddy = y - radius;
                if (ddx * ddx + ddy * ddy <= disc * disc) continue;
                int cid = ++labels;
                int top = 0;
                stack[top++] = i;
                lab[i] = cid;
                int size = 0;
                bool touchesRing = false, sameComponent = false;
                while (top > 0) {
                    int cur = stack[--top];
                    size++;
                    int x2 = cur % side, y2 = cur / side;
                    int rx = x2 - radius, ry = y2 - radius;
                    int d2 = rx * rx + ry * ry;
                    if (d2 > disc * disc && d2 <= (disc + 2) * (disc + 2)) touchesRing = true;
                    if (full[cur] == targetComponent) sameComponent = true;
                    for (int k = 0; k < 8; k++) {
                        int nx = x2 + NeighborX[k], ny = y2 + NeighborY[k];
                        if (nx < 0 || ny < 0 || nx >= side || ny >= side) continue;
                        int ni = ny * side + nx;
                        if (road[ni] == 0 || lab[ni] != 0) continue;
                        if ((nx - radius) * (nx - radius) + (ny - radius) * (ny - radius) <= disc * disc) continue;
                        lab[ni] = cid;
                        stack[top++] = ni;
                    }
                }
                if (size >= 4 && touchesRing && sameComponent) {
                    arms++;
                    if (arms >= 3) return 3;
                }
            }
            return arms;
        }


        // ------------------------------------------------------------------
        // Line of sight / string pulling
        // ------------------------------------------------------------------
        bool LineClear(PointF a, PointF b, RoadRouteMode mode, GridLevel fine, byte[] overlay) {
            int width = fine.Width, height = fine.Height;
            byte[] data = fine.Data;
            // navA26 defect B: the fine A* hard-blocks terrain class 4 (very steep
            // cliffs / dam walls / vehicle-impassable ground) for the two vehicle
            // off-road modes, but this line-of-sight pull used to ignore nav-terrain and
            // could straighten the assembled polyline back across the wall (rendered
            // route climbed the dam although the A* cost detoured around it).  Mirror
            // the A* hard block here: a pulled chord may not sample a class-4 cell.
            // Walking keeps the historical behaviour (people can climb walls).
            bool blockClass4 = (mode == RoadRouteMode.Shortest || mode == RoadRouteMode.PavedOnly) && terrainFine != null;
            int terrainCells = blockClass4 ? terrainFine.Length : 0;
            double ax = (double)a.X * width, ay = (double)a.Y * height;
            double bx = (double)b.X * width, by = (double)b.Y * height;
            if (ax < 0 || ay < 0 || ax > width - 1e-9 || ay > height - 1e-9) return false;
            if (bx < 0 || by < 0 || bx > width - 1e-9 || by > height - 1e-9) return false;
            int x = (int)Math.Floor(ax), y = (int)Math.Floor(ay);
            int targetX = (int)Math.Floor(bx), targetY = (int)Math.Floor(by);
            if (x == targetX && y == targetY) {
                int single = y * width + x;
                if (blockClass4 && single < terrainCells && terrainFine[single] >= 4) return false;
                return Passable(mode, TrailCost(overlay, single, data[single]));
            }
            double dx = bx - ax, dy = by - ay;
            int stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
            int stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            double tDeltaX = stepX == 0 ? double.MaxValue : 1.0 / Math.Abs(dx);
            double tDeltaY = stepY == 0 ? double.MaxValue : 1.0 / Math.Abs(dy);
            double tMaxX = stepX == 0 ? double.MaxValue : (stepX > 0 ? (x + 1 - ax) : (ax - x)) / Math.Abs(dx);
            double tMaxY = stepY == 0 ? double.MaxValue : (stepY > 0 ? (y + 1 - ay) : (ay - y)) / Math.Abs(dy);
            int guard = 0;
            int maxSteps = Math.Abs(targetX - x) + Math.Abs(targetY - y) + 4;
            while (true) {
                if (x < 0 || y < 0 || x >= width || y >= height) return false;
                int cell = y * width + x;
                if (!Passable(mode, TrailCost(overlay, cell, data[cell]))) return false;
                if (blockClass4 && cell < terrainCells && terrainFine[cell] >= 4) return false;
                if (x == targetX && y == targetY) return true;
                if (++guard > maxSteps) return false;
                if (tMaxX < tMaxY) {
                    x += stepX;
                    tMaxX += tDeltaX;
                } else if (tMaxY < tMaxX) {
                    y += stepY;
                    tMaxY += tDeltaY;
                } else {
                    // Exact corner crossing: same rule as diagonal A* moves (reject
                    // when either orthogonal neighbour is undrivable). For the vehicle
                    // modes class 4 counts as undrivable, mirroring the fine-layer hard
                    // block used by the A* itself.
                    if (stepX != 0 && stepY != 0) {
                        int sideA = y * width + (x + stepX);
                        int sideB = (y + stepY) * width + x;
                        bool okA = Passable(mode, TrailCost(overlay, sideA, data[sideA]));
                        bool okB = Passable(mode, TrailCost(overlay, sideB, data[sideB]));
                        if (blockClass4) {
                            if (sideA < terrainCells && terrainFine[sideA] >= 4) okA = false;
                            if (sideB < terrainCells && terrainFine[sideB] >= 4) okB = false;
                        }
                        if (!okA || !okB) return false;
                    }
                    x += stepX;
                    y += stepY;
                    tMaxX += tDeltaX;
                    tMaxY += tDeltaY;
                }
            }
        }

        List<PointF> StringPull(List<PointF> points, RoadRouteMode mode, GridLevel fine, byte[] overlay) {
            if (points.Count < 3) return new List<PointF>(points);
            double[] cumulative = null;
            bool costGuard = mode == RoadRouteMode.Shortest || mode == RoadRouteMode.PavedOnly;
            if (mode == RoadRouteMode.Quickest) {
                cumulative = new double[points.Count];
                for (int i = 1; i < points.Count; i++)
                    cumulative[i] = cumulative[i - 1] + LineTravelSeconds(points[i - 1], points[i], mode, fine, overlay);
            } else if (costGuard) {
                // navA27 (P0-1): weighted cost of the raw A* polyline, on the exact A*
                // cost model.  A pulled chord may shorten the *distance* but must not
                // silently buy that distance with terrain penalty (rough x24 for
                // Shortest, class-4 clamp, trail factors).
                cumulative = new double[points.Count];
                for (int i = 1; i < points.Count; i++)
                    cumulative[i] = cumulative[i - 1] + WeightedSegmentCost(points[i - 1], points[i], mode, fine, overlay);
            }
            List<PointF> result = new List<PointF>();
            int index = 0;
            int count = points.Count;
            while (index < count - 1) {
                int chosen = index + 1;
                int limit = Math.Min(count - 1, index + 256);
                int low = index + 1;
                int bestClear = low;
                if (LineClear(points[index], points[limit], mode, fine, overlay)) {
                    bestClear = limit;
                } else if (limit > low + 1) {
                    // Screened straight runs are rare; binary search the longest clear
                    // prefix.  This keeps long road straights at ~log(n) LOS checks.
                    int high = limit;
                    while (high - low > 1) {
                        int middle = (low + high) >> 1;
                        if (LineClear(points[index], points[middle], mode, fine, overlay)) low = middle;
                        else high = middle;
                    }
                    bestClear = low;
                }
                if (bestClear > index + 1) {
                    if (cumulative == null) {
                        chosen = bestClear;
                    } else if (costGuard) {
                        // navA27 (P0-1): same idea as the Quickest check below, using the
                        // unified weighted model.  Instead of a linear downward scan (which
                        // can be long when a long chord is pricey) probe geometrically:
                        // span/2/4/... to find a passing length, then binary-refine the
                        // boundary.  This keeps the farthest cost-consistent chord (v2
                        // simplification quality) with a bounded number of probes.
                        int span = bestClear - (index + 1);
                        int probes = 0;
                        int passOffset = -1;
                        int step = span;
                        while (step >= 1 && probes < 24) {
                            probes++;
                            if (PullCostAccept(points, index, index + 1 + step, cumulative, mode, fine, overlay)) {
                                passOffset = step;
                                break;
                            }
                            step >>= 1;
                        }
                        if (passOffset >= 0) {
                            int lo = passOffset;
                            int hi = passOffset * 2;
                            if (hi > span) hi = span + 1; // beyond tested span: treat as non-passing bound
                            while (hi - lo > 1 && probes < 30) {
                                int middle = (lo + hi) >> 1;
                                probes++;
                                if (PullCostAccept(points, index, index + 1 + middle, cumulative, mode, fine, overlay)) lo = middle;
                                else hi = middle;
                            }
                            chosen = index + 1 + lo;
                        }
                    } else {
                        // Quickest must not "straighten" onto slower terrain: only pull
                        // when the pulled line is no worse than the original A* leg.
                        for (int candidate = bestClear; candidate > index + 1; candidate--) {
                            if (!LineClear(points[index], points[candidate], mode, fine, overlay)) continue;
                            if (TravelAcceptable(points, index, candidate, cumulative, mode, fine, overlay)) {
                                chosen = candidate;
                                break;
                            }
                        }
                    }
                }
                result.Add(points[index]);
                index = chosen;
            }
            result.Add(points[count - 1]);
            return result;
        }

        // Allowed small tolerance for a pulled chord (see StringPull): 2% + 0.5 cost
        // units, i.e. roughly half a metre of plain driving for Shortest/PavedOnly.
        const double PullCostTolerance = 1.02;
        const double PullCostToleranceMeters = 0.5;

        // Weighted cost of one straight segment, sampled at <=2 m exactly like the A*
        // step cost (metres x StepFactor x TerrainCost x trail multiplier).  This is
        // the same model the A* and the final metrics use, so comparing a chord with
        // the raw segment it replaces is apples-to-apples.
        double WeightedSegmentCost(PointF a, PointF b, RoadRouteMode mode, GridLevel fine, byte[] overlay) {
            double length = RoadRouter.DistanceMeters(a, b);
            if (length <= 0) return 0;
            int samples = (int)Math.Ceiling(length / 2.0);
            if (samples < 1) samples = 1;
            double slice = length / samples;
            double nx = (b.X - a.X) * MapScaleX / length, ny = (b.Y - a.Y) * MapScaleY / length;
            double total = 0;
            byte[] terrain = terrainFine;
            int terrainCells = terrain == null ? 0 : terrain.Length;
            bool useTerrain = mode != RoadRouteMode.Quickest;
            for (int s = 0; s < samples; s++) {
                double t = (s + 0.5) / samples;
                double u = a.X + (b.X - a.X) * t;
                double v = a.Y + (b.Y - a.Y) * t;
                int x = (int)Math.Floor(u * fine.Width);
                int y = (int)Math.Floor(v * fine.Height);
                if (x < 0) x = 0; else if (x >= fine.Width) x = fine.Width - 1;
                if (y < 0) y = 0; else if (y >= fine.Height) y = fine.Height - 1;
                int cell = y * fine.Width + x;
                byte cost = TrailCost(overlay, cell, fine.Data[cell]);
                double terrainFactor = 1.0;
                if (useTerrain && cell < terrainCells) terrainFactor = TerrainCost(mode, terrain[cell]);
                total += slice * StepFactor(cost, mode) * terrainFactor * GradeFactor(cell, nx, ny, mode) * TrailMultiplier(overlay, cell);
            }
            return total;
        }

        // True when the chord weighted cost does not exceed 'limit'.  Two stages:
        //   1) 8 m coarse sampling is used ONLY as a fast reject, with a 2x safety
        //      margin and only for long chords.  A coarse sample can overestimate a
        //      narrow (4 m) high-cost feature by at most a bounded factor, so a clearly
        //      over-budget coarse sum is safe to reject; it is never used to accept.
        //   2) every surviving candidate is re-sampled at <=2 m (the exact model) with
        //      an early exit: costs are non-negative, so once the running sum exceeds
        //      the limit the final cost necessarily does too.
        bool WeightedChordWithin(PointF a, PointF b, RoadRouteMode mode, GridLevel fine, byte[] overlay, double limit) {
            double length = RoadRouter.DistanceMeters(a, b);
            if (length <= 0) return true;
            // Coarse 8 m screening is only a fast reject for chords long enough that the
            // "narrow feature sampled over one 8 m slice" overestimate is < 2x (>= 8
            // samples), and only with a 2x safety margin.  It never accepts a chord.
            if (length >= 64.0 && CoarseChordTooExpensive(a, b, mode, fine, overlay, limit)) return false;
            int samples = (int)Math.Ceiling(length / 2.0);
            if (samples < 1) samples = 1;
            double slice = length / samples;
            double nx = (b.X - a.X) * MapScaleX / length, ny = (b.Y - a.Y) * MapScaleY / length;
            double total = 0;
            byte[] terrain = terrainFine;
            int terrainCells = terrain == null ? 0 : terrain.Length;
            bool useTerrain = mode != RoadRouteMode.Quickest;
            for (int s = 0; s < samples; s++) {
                double t = (s + 0.5) / samples;
                double u = a.X + (b.X - a.X) * t;
                double v = a.Y + (b.Y - a.Y) * t;
                int x = (int)Math.Floor(u * fine.Width);
                int y = (int)Math.Floor(v * fine.Height);
                if (x < 0) x = 0; else if (x >= fine.Width) x = fine.Width - 1;
                if (y < 0) y = 0; else if (y >= fine.Height) y = fine.Height - 1;
                int cell = y * fine.Width + x;
                byte cost = TrailCost(overlay, cell, fine.Data[cell]);
                double terrainFactor = 1.0;
                if (useTerrain && cell < terrainCells) terrainFactor = TerrainCost(mode, terrain[cell]);
                total += slice * StepFactor(cost, mode) * terrainFactor * GradeFactor(cell, nx, ny, mode) * TrailMultiplier(overlay, cell);
                if (total > limit) return false;
            }
            return true;
        }

        // navA27 (P0-1): a candidate chord is usable when its weighted cost stays within
        // the replaced raw segment cost (small tolerance) AND it is line-of-sight clear.
        bool PullCostAccept(List<PointF> points, int from, int to, double[] cumulative,
            RoadRouteMode mode, GridLevel fine, byte[] overlay) {
            double original = cumulative[to] - cumulative[from];
            double allowed = original * PullCostTolerance + PullCostToleranceMeters;
            if (!WeightedChordWithin(points[from], points[to], mode, fine, overlay, allowed)) return false;
            return LineClear(points[from], points[to], mode, fine, overlay);
        }
        // 8 m coarse screening used by WeightedChordWithin as a fast reject only.
        bool CoarseChordTooExpensive(PointF a, PointF b, RoadRouteMode mode, GridLevel fine, byte[] overlay, double limit) {
            double length = RoadRouter.DistanceMeters(a, b);
            int samples = (int)Math.Ceiling(length / 8.0);
            if (samples < 2) return false;
            double slice = length / samples;
            double nx = (b.X - a.X) * MapScaleX / length, ny = (b.Y - a.Y) * MapScaleY / length;
            double coarse = 0;
            byte[] terrain = terrainFine;
            int terrainCells = terrain == null ? 0 : terrain.Length;
            bool useTerrain = mode != RoadRouteMode.Quickest;
            for (int s = 0; s < samples; s++) {
                double t = (s + 0.5) / samples;
                double u = a.X + (b.X - a.X) * t;
                double v = a.Y + (b.Y - a.Y) * t;
                int x = (int)Math.Floor(u * fine.Width);
                int y = (int)Math.Floor(v * fine.Height);
                if (x < 0) x = 0; else if (x >= fine.Width) x = fine.Width - 1;
                if (y < 0) y = 0; else if (y >= fine.Height) y = fine.Height - 1;
                int cell = y * fine.Width + x;
                byte cost = TrailCost(overlay, cell, fine.Data[cell]);
                double terrainFactor = 1.0;
                if (useTerrain && cell < terrainCells) terrainFactor = TerrainCost(mode, terrain[cell]);
                coarse += slice * StepFactor(cost, mode) * terrainFactor * GradeFactor(cell, nx, ny, mode) * TrailMultiplier(overlay, cell);
                if (coarse > limit * 2.0 + 4.0) return true;
            }
            return coarse > limit * 2.0 + 4.0;
        }

        bool TravelAcceptable(List<PointF> points, int from, int to, double[] cumulative,
            RoadRouteMode mode, GridLevel fine, byte[] overlay) {
            double original = cumulative[to] - cumulative[from];
            if (original <= 0.0001) return true;
            double straight = LineTravelSeconds(points[from], points[to], mode, fine, overlay);
            return straight <= original * 1.12 + 0.25;
        }

        double LineTravelSeconds(PointF a, PointF b, RoadRouteMode mode, GridLevel fine, byte[] overlay) {
            double length = RoadRouter.DistanceMeters(a, b);
            if (length <= 0) return 0;
            int samples = (int)Math.Ceiling(length / 2.0);
            if (samples < 1) samples = 1;
            double slice = length / samples;
            double seconds = 0;
            for (int i = 0; i < samples; i++) {
                double t = (i + 0.5) / samples;
                double u = a.X + (b.X - a.X) * t;
                double v = a.Y + (b.Y - a.Y) * t;
                int x = (int)Math.Floor(u * fine.Width);
                int y = (int)Math.Floor(v * fine.Height);
                if (x < 0) x = 0; else if (x >= fine.Width) x = fine.Width - 1;
                if (y < 0) y = 0; else if (y >= fine.Height) y = fine.Height - 1;
                byte cost = fine.Data[y * fine.Width + x];
                double speed = mode == RoadRouteMode.Walking ? WalkingSpeed : SpeedFor(cost);
                seconds += slice / speed;
            }
            return seconds;
        }
    }
}
