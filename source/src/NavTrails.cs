using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace ScumMiniMap {
    // nav-C (player trail learning) persistent store.
    //
    // The minimap already records the live position history for the on-screen
    // trail (MiniMap.RecordLocationHistory).  This class turns the same samples
    // into a small persistent "personal preference" raster that GridRouter
    // overlays on top of the read-only nav-grid.bin cost surface.
    //
    // ---------------------------------------------------------------------
    // File format: data/nav-trails.bin  (always gzip, magic "NAVT")
    // ---------------------------------------------------------------------
    // Decompressed payload = fixed-size records, little endian:
    //
    //   offset 0   : magic "NAVT"                      (4 bytes)
    //   offset 4   : uint16 formatVersion = 1          (2 bytes)
    //   offset 6   : uint16 reserved = 0               (2 bytes)
    //   offset 8   : uint32 canonicalWidth  = 3804     (4 bytes)
    //   offset 12  : uint32 canonicalHeight = 3804     (4 bytes)
    //   offset 16  : uint32 recordCount                (4 bytes)
    //   offset 20  : recordCount * 13 bytes, unsorted:
    //                  uint32 cellIndex   // cy*3804 + cx, cx=floor(u*3804)
    //                  uint16 count       // times this 4 m cell was entered
    //                  uint8  dayCount    // distinct consecutive days seen (sat. 255)
    //                  uint16 lastDay     // days since 2020-01-01 (UTC)
    //                  uint32 lastSeen    // unix seconds of the last visit
    //
    // The canonical cell grid is the nav-grid fine layer (4 m, 3804x3804 for
    // the navA1 asset); the loader remaps cell centres through UV so a slightly
    // different fine resolution still works.  Changing the cost-surface asset
    // resolution invalidates the file: delete data/nav-trails.bin to reset.
    //
    // Caps: <= 200,000 cell records and <= 3 MB compressed.  When either cap is
    // exceeded the least recently seen cells are pruned.  Writes are throttled
    // to one per 20 s or 200 new cells, flushed on process exit.  Nothing here
    // leaves the machine: no network, only data/ next to the executable.
    // ---------------------------------------------------------------------
    internal sealed class NavTrails {
        internal const int FormatVersion = 1;
        internal const int CanonicalWidth = 3804;
        internal const int CanonicalHeight = 3804;
        internal const int MaxCells = 200000;
        internal const long MaxFileBytes = 3L * 1024L * 1024L;

        // Sampling contract: a sample is considered when the player moved at
        // least 3 m or at least 1 s passed.  Jump/teleport pairs are dropped.
        const double SampleMinMeters = 3.0;
        const double SampleMaxSeconds = 1.0;
        const double TeleportStepMeters = 30.0;
        const double TeleportSpeedMps = 30.0 / 3.6;
        const double InterpolationMeters = 2.0;

        // World spans used by nav-grid.bin (see GridRouter.MapScaleX/Y).
        const double SpanXMeters = 15216.18;
        const double SpanYMeters = 15236.18;

        const double SaveIntervalSeconds = 20.0;
        const int SaveNewCellThreshold = 200;
        const int PruneChunk = 2000;

        static readonly DateTime DayEpoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal sealed class TrailCell {
            internal ushort Count;
            internal byte Days;
            internal ushort LastDay;
            internal uint LastSeen;
        }

        readonly object gate = new object();
        Dictionary<int, TrailCell> cells;
        bool loaded;
        int version;
        int pendingNewCells;
        volatile bool dirty;
        bool saving;
        DateTime lastSaveUtc = DateTime.UtcNow;

        // Last accepted sample (UV) used for the >=3 m / >=1 s sampling rule and
        // for the jump filter.  Guarded by gate.
        bool hasLast;
        double lastU, lastV;
        DateTime lastSampleUtc;

        // Test hook: tests redirect persistence into a scratch folder so the
        // real data/nav-trails.bin of the user is never touched.
        internal static string StoragePathOverride = null;
        // Settings > Map & zones > "Trail learning" switch. When off the recorder
        // stops appending immediately and GridRouter ignores the learned overlay
        // (including already unsealed cells) so navigation behaves exactly like
        // navA1. Existing data/nav-trails.bin is kept, so re-enabling restores
        // everything instantly without re-learning.
        internal static volatile bool LearningEnabled = true;
        // Settings > Map & zones > "Trail learning" switch.  When off the recorder
        // stops appending immediately and GridRouter ignores the learned overlay
        // (including already unsealed cells) so navigation behaves exactly like
        // navA1.  Existing data/nav-trails.bin is kept, so re-enabling restores
        // everything instantly.

        static NavTrails instance;

        internal static NavTrails Instance {
            get {
                NavTrails current = instance;
                if (current != null) return current;
                lock (typeof(NavTrails)) {
                    if (instance == null) instance = new NavTrails();
                    return instance;
                }
            }
        }

        NavTrails() {
            try { AppDomain.CurrentDomain.ProcessExit += delegate { try { Flush(); } catch { } }; } catch { }
        }

        internal static string TrailPath {
            get {
                string overridden = StoragePathOverride;
                if (!string.IsNullOrEmpty(overridden)) return overridden;
                return Path.Combine(Program.UserDataDirectory, "nav-trails.bin");
            }
        }

        // Version bumps on load and whenever a cell record changes.  GridRouter
        // compares it to know when its in-memory trail overlay must be rebuilt.
        internal int Version { get { return version; } }

        internal int CellCount {
            get {
                EnsureLoaded();
                lock (gate) { return cells.Count; }
            }
        }

        // --------------------------------------------------------------
        // Persistence
        // --------------------------------------------------------------
        void EnsureLoaded() {
            if (loaded) return;
            lock (gate) {
                if (loaded) return;
                cells = new Dictionary<int, TrailCell>(1024);
                loaded = true;
                LoadFrom(TrailPath);
                version++;
            }
        }

        internal void Reload() {
            lock (gate) {
                cells = new Dictionary<int, TrailCell>(1024);
                loaded = true;
                hasLast = false;
                LoadFrom(TrailPath);
                version++;
                dirty = false;
                lastSaveUtc = DateTime.UtcNow;
            }
        }

        void LoadFrom(string path) {
            try {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                FileInfo info = new FileInfo(path);
                if (info.Length > MaxFileBytes * 2L) return; // corrupt/foreign file: ignore
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (GZipStream zip = new GZipStream(stream, CompressionMode.Decompress))
                using (BinaryReader reader = new BinaryReader(zip)) {
                    byte[] magic = reader.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != 'N' || magic[1] != 'A' || magic[2] != 'V' || magic[3] != 'T') return;
                    int format = reader.ReadUInt16();
                    reader.ReadUInt16(); // reserved
                    int width = (int)reader.ReadUInt32();
                    int height = (int)reader.ReadUInt32();
                    int count = (int)reader.ReadUInt32();
                    if (format != FormatVersion) return;
                    if (width != CanonicalWidth || height != CanonicalHeight || count < 0) return;
                    if (count > MaxCells * 4) return; // implausible, treat as corrupt
                    for (int i = 0; i < count; i++) {
                        int cellIndex = (int)reader.ReadUInt32();
                        ushort visits = reader.ReadUInt16();
                        byte days = reader.ReadByte();
                        ushort lastDay = reader.ReadUInt16();
                        uint lastSeen = reader.ReadUInt32();
                        if (cellIndex < 0 || cellIndex >= CanonicalWidth * CanonicalHeight) continue;
                        TrailCell cell;
                        if (!cells.TryGetValue(cellIndex, out cell)) {
                            cell = new TrailCell();
                            cells[cellIndex] = cell;
                        }
                        if (visits > cell.Count) cell.Count = visits;
                        if (days > cell.Days) cell.Days = days;
                        if (lastDay > cell.LastDay || cell.LastDay == 0) cell.LastDay = lastDay;
                        if (lastSeen > cell.LastSeen) cell.LastSeen = lastSeen;
                    }
                }
                PruneLocked(MaxCells);
            } catch { }
        }

        internal void Flush() {
            EnsureLoaded();
            NavTrailCell[] snapshot;
            int sizeHint;
            lock (gate) {
                if (!dirty) return;
                if (saving) { /* a background save is in flight, it will catch the snapshot */ }
                dirty = false;
                saving = true;
                snapshot = SnapshotLocked();
                sizeHint = cells.Count;
            }
            try {
                WriteSnapshot(snapshot, sizeHint);
            } catch {
                lock (gate) { dirty = true; }
            } finally {
                lock (gate) { saving = false; lastSaveUtc = DateTime.UtcNow; }
            }
        }

        void SaveIfDue(bool force) {
            EnsureLoaded();
            NavTrailCell[] snapshot;
            int sizeHint;
            lock (gate) {
                if (saving) return;
                if (!force) {
                    bool due = pendingNewCells >= SaveNewCellThreshold
                        || (DateTime.UtcNow - lastSaveUtc).TotalSeconds >= SaveIntervalSeconds;
                    if (!due) return;
                }
                if (!dirty) return;
                dirty = false;
                saving = true;
                snapshot = SnapshotLocked();
                sizeHint = cells.Count;
            }
            ThreadPool.QueueUserWorkItem(delegate {
                try {
                    WriteSnapshot(snapshot, sizeHint);
                } catch {
                    lock (gate) { dirty = true; }
                } finally {
                    lock (gate) { saving = false; lastSaveUtc = DateTime.UtcNow; }
                }
            });
        }

        static void WriteSnapshot(NavTrailCell[] records, int hint) {
            if (records == null) return;
            byte[] payload = Encode(records, records.Length);
            int keep = records.Length;
            // Enforce the compressed size cap by dropping the oldest records.
            while (payload.Length > MaxFileBytes && keep > 0) {
                int drop = Math.Min(keep, Math.Max(PruneChunk, keep / 10));
                keep -= drop;
                NavTrailCell[] oldestFirst = (NavTrailCell[])records.Clone();
                Array.Sort(oldestFirst, CompareByLastSeen);
                NavTrailCell[] kept = new NavTrailCell[keep];
                Array.Copy(oldestFirst, drop, kept, 0, keep);
                payload = Encode(kept, keep);
                records = kept;
            }
            string path = TrailPath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, payload);
            if (File.Exists(path)) {
                try { File.Replace(temp, path, null); }
                catch { File.Copy(temp, path, true); File.Delete(temp); }
            } else {
                File.Move(temp, path);
            }
        }

        static int CompareByLastSeen(NavTrailCell a, NavTrailCell b) {
            int seen = a.LastSeen.CompareTo(b.LastSeen);
            if (seen != 0) return seen;
            return a.Cell.CompareTo(b.Cell);
        }

        static byte[] Encode(NavTrailCell[] records, int count) {
            using (MemoryStream memory = new MemoryStream(Math.Max(64, 20 + count * 13))) {
                using (GZipStream zip = new GZipStream(memory, CompressionMode.Compress, true))
                using (BinaryWriter writer = new BinaryWriter(zip)) {
                    writer.Write((byte)'N'); writer.Write((byte)'A'); writer.Write((byte)'V'); writer.Write((byte)'T');
                    writer.Write((ushort)FormatVersion);
                    writer.Write((ushort)0);
                    writer.Write((uint)CanonicalWidth);
                    writer.Write((uint)CanonicalHeight);
                    writer.Write((uint)count);
                    for (int i = 0; i < count; i++) {
                        writer.Write((uint)records[i].Cell);
                        writer.Write(records[i].Count);
                        writer.Write(records[i].Days);
                        writer.Write(records[i].LastDay);
                        writer.Write(records[i].LastSeen);
                    }
                }
                return memory.ToArray();
            }
        }

        NavTrailCell[] SnapshotLocked() {
            NavTrailCell[] result = new NavTrailCell[cells.Count];
            int index = 0;
            foreach (KeyValuePair<int, TrailCell> pair in cells) {
                NavTrailCell cell;
                cell.Cell = pair.Key;
                cell.Count = pair.Value.Count;
                cell.Days = pair.Value.Days;
                cell.LastDay = pair.Value.LastDay;
                cell.LastSeen = pair.Value.LastSeen;
                result[index++] = cell;
            }
            return result;
        }

        void PruneLocked(int limit) {
            if (cells == null || cells.Count <= limit) return;
            List<KeyValuePair<int, TrailCell>> entries = new List<KeyValuePair<int, TrailCell>>(cells);
            entries.Sort(delegate(KeyValuePair<int, TrailCell> a, KeyValuePair<int, TrailCell> b) {
                return a.Value.LastSeen.CompareTo(b.Value.LastSeen);
            });
            int drop = Math.Min(entries.Count, Math.Max(PruneChunk, entries.Count - limit));
            for (int i = 0; i < drop; i++) cells.Remove(entries[i].Key);
        }

        // --------------------------------------------------------------
        // Live recording (called from MiniMap.RecordLocationHistory)
        // --------------------------------------------------------------
        internal void RecordSample(float u, float v, string mapId, bool inBunker, DateTime sampledAtUtc) {
            try {
                if (!LearningEnabled) {
                    // Learning is switched off: append nothing. Break the sample
                    // chain so re-enabling cannot bridge the disabled interval with
                    // a teleport-length edge.
                    lock (gate) { hasLast = false; }
                    return;
                }
                if (!LearningEnabled) {
                    // Learning switched off: do not append anything. Break the sample
                    // chain so re-enabling cannot bridge the disabled interval with a
                    // teleport-length edge.
                    lock (gate) { hasLast = false; }
                    return;
                }
                if (float.IsNaN(u) || float.IsNaN(v) || u < 0f || v < 0f || u >= 1f || v >= 1f) return;
                bool defaultMap = string.IsNullOrEmpty(mapId) || string.Equals(mapId, "default", StringComparison.OrdinalIgnoreCase);
                if (!defaultMap || inBunker) {
                    // Leaving the learnable surface: break the trail so the next
                    // sample cannot create a long teleport segment.
                    lock (gate) { hasLast = false; }
                    return;
                }
                EnsureLoaded();
                int cellX = (int)(u * CanonicalWidth);
                int cellY = (int)(v * CanonicalHeight);
                if (cellX < 0) cellX = 0; else if (cellX >= CanonicalWidth) cellX = CanonicalWidth - 1;
                if (cellY < 0) cellY = 0; else if (cellY >= CanonicalHeight) cellY = CanonicalHeight - 1;
                int cellIndex = cellY * CanonicalWidth + cellX;
                int day = (int)(sampledAtUtc.ToUniversalTime() - DayEpoch).TotalDays;
                if (day < 0) return;
                if (day > 65535) day = 65535;
                uint unixNow = (uint)Math.Min(uint.MaxValue, Math.Max(0, (long)(sampledAtUtc.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds));
                int addedCells = 0;
                lock (gate) {
                    if (hasLast) {
                        double dx = (u - lastU) * SpanXMeters;
                        double dy = (v - lastV) * SpanYMeters;
                        double meters = Math.Sqrt(dx * dx + dy * dy);
                        double seconds = (sampledAtUtc - lastSampleUtc).TotalSeconds;
                        bool moved = meters >= SampleMinMeters;
                        bool elapsed = seconds >= SampleMaxSeconds;
                        if (!moved && !elapsed) return;
                        if (meters > TeleportStepMeters || (seconds > 0 && meters / seconds > TeleportSpeedMps)) {
                            // Teleport/jump: drop the whole segment but restart from
                            // the new position so later honest walking is still kept.
                            hasLast = true;
                            lastU = u; lastV = v;
                            lastSampleUtc = sampledAtUtc;
                            lastCellX = -1; lastCellY = -1;
                            return;
                        }
                        if (moved) {
                            addedCells = RasterizeLocked(lastU, lastV, u, v, lastCellX, lastCellY, day, unixNow);
                        }
                        // How: standing still or moving inside the same 4 m cell does
                        // not bump the visit counter, otherwise an AFK player would
                        // fabricate "evidence" for unblocking a wall.
                    } else {
                        addedCells = AddCellLocked(cellIndex, day, unixNow);
                    }
                    hasLast = true;
                    lastU = u; lastV = v;
                    lastSampleUtc = sampledAtUtc;
                    lastCellX = cellX;
                    lastCellY = cellY;
                    if (addedCells > 0) {
                        pendingNewCells += addedCells;
                        if (cells.Count > MaxCells + MaxCells / 20) PruneLocked(MaxCells);
                    }
                }
                if (addedCells > 0) SaveIfDue(false);
            } catch { }
        }

        int lastCellX = -1, lastCellY = -1;

        // Samples the segment every 2 m and bumps each newly entered canonical
        // cell; cells already passed through in this segment are counted once.
        int RasterizeLocked(double fromU, double fromV, double toU, double toV, int fromCellX, int fromCellY, int day, uint unixNow) {
            double dx = (toU - fromU) * SpanXMeters;
            double dy = (toV - fromV) * SpanYMeters;
            double meters = Math.Sqrt(dx * dx + dy * dy);
            int steps = (int)Math.Ceiling(meters / InterpolationMeters);
            if (steps < 1) steps = 1;
            if (steps > 64) steps = 64;
            int added = 0;
            int previousCell = fromCellX >= 0 ? fromCellY * CanonicalWidth + fromCellX : -1;
            int prevX = fromCellX, prevY = fromCellY;
            for (int s = 1; s <= steps; s++) {
                double t = (double)s / steps;
                double u = fromU + (toU - fromU) * t;
                double v = fromV + (toV - fromV) * t;
                int cx = (int)(u * CanonicalWidth);
                int cy = (int)(v * CanonicalHeight);
                if (cx < 0) cx = 0; else if (cx >= CanonicalWidth) cx = CanonicalWidth - 1;
                if (cy < 0) cy = 0; else if (cy >= CanonicalHeight) cy = CanonicalHeight - 1;
                int index = cy * CanonicalWidth + cx;
                if (index == previousCell) continue;
                // A diagonal cell step gets an orthogonal bridge so the learned
                // cells stay 4-connected for the router's no-corner-cutting rule.
                if (previousCell >= 0 && Math.Abs(cx - prevX) == 1 && Math.Abs(cy - prevY) == 1)
                    added += AddCellLocked(prevY * CanonicalWidth + cx, day, unixNow);
                added += AddCellLocked(index, day, unixNow);
                previousCell = index;
                prevX = cx;
                prevY = cy;
            }
            return added;
        }

        int AddCellLocked(int cellIndex, int day, uint unixNow) {
            TrailCell cell;
            if (cells.TryGetValue(cellIndex, out cell)) {
                if (cell.Count < ushort.MaxValue) cell.Count++;
                if (cell.LastDay != (ushort)day) {
                    cell.LastDay = (ushort)day;
                    if (cell.Days < byte.MaxValue) cell.Days++;
                }
                cell.LastSeen = unixNow;
                version++;
                dirty = true;
                return 0; // existing cell: not a "new cell" for the save throttle
            }
            cell = new TrailCell();
            cell.Count = 1;
            cell.Days = 1;
            cell.LastDay = (ushort)day;
            cell.LastSeen = unixNow;
            cells[cellIndex] = cell;
            version++;
            dirty = true;
            return 1;
        }

        // --------------------------------------------------------------
        // Overlay snapshot for GridRouter (read-only)
        // --------------------------------------------------------------
        internal NavTrailCell[] Snapshot() {
            EnsureLoaded();
            lock (gate) { return SnapshotLocked(); }
        }

        // Test-only bulk seed used by checks/trail-learning-20261106: bypasses the
        // live sampling filter, then prunes/persists through the normal save path.
        internal void SeedForTests(int[] canonicalCells, int visits, int days) {
            EnsureLoaded();
            lock (gate) {
                for (int i = 0; i < canonicalCells.Length; i++) {
                    int index = canonicalCells[i];
                    if (index < 0 || index >= CanonicalWidth * CanonicalHeight) continue;
                    TrailCell cell;
                    if (!cells.TryGetValue(index, out cell)) {
                        cell = new TrailCell();
                        cells[index] = cell;
                    }
                    if (visits > cell.Count) cell.Count = (ushort)Math.Min(ushort.MaxValue, visits);
                    if (days > cell.Days) cell.Days = (byte)Math.Min(byte.MaxValue, days);
                    cell.LastDay = 20000;
                    cell.LastSeen = (uint)(1000000 + i);
                }
                PruneLocked(MaxCells);
                dirty = true;
                version++;
            }
            Flush();
        }
    }

    // Compact record handed to GridRouter.TrailOverlay builder.
    internal struct NavTrailCell {
        internal int Cell;      // canonical fine layer index (see NavTrails format)
        internal ushort Count;  // number of visits
        internal byte Days;     // number of distinct days seen
        internal ushort LastDay;
        internal uint LastSeen;
    }
}
