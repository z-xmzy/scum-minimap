using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace ScumMiniMap {
    internal sealed partial class GridRouter {
        byte[] worldGrade;

        static BinaryReader OpenWorldLayer(string resource) {
            Stream stream = typeof(GridRouter).Assembly.GetManifestResourceStream(resource);
            if (stream == null) throw new InvalidDataException("Missing world navigation layer: " + resource);
            return new BinaryReader(new GZipStream(stream, CompressionMode.Decompress));
        }

        void LoadWorldLayers() {
            using (BinaryReader grid = OpenWorldLayer("nav-world-grid.bin"))
                levels = ParseAsset(grid);
            if (levels == null) throw new InvalidDataException("Invalid world navigation grid.");
            byte[][] classes = new byte[levels.Length][];
            using (BinaryReader r = OpenWorldLayer("nav-world-terrain.bin")) {
                if (r.ReadUInt32() != 0x3152544E || r.ReadUInt16() != 1 || r.ReadUInt16() != levels.Length)
                    throw new InvalidDataException("Invalid world terrain header.");
                for (int i = 0; i < levels.Length; i++) {
                    GridLevel l = levels[i];
                    if (r.ReadSingle() != l.CellMeters || r.ReadInt32() != l.Width || r.ReadInt32() != l.Height)
                        throw new InvalidDataException("World terrain resolution mismatch.");
                    classes[i] = r.ReadBytes(l.CellCount);
                    if (classes[i].Length != l.CellCount) throw new EndOfStreamException();
                }
            }
            GridLevel fine = levels[LevelFine];
            using (BinaryReader r = OpenWorldLayer("nav-world-grade.bin")) {
                if (r.ReadUInt32() != 0x31445247 || r.ReadInt32() != fine.Width || r.ReadInt32() != fine.Height || r.ReadInt32() != 26593778)
                    throw new InvalidDataException("Invalid SCUM world grade layer.");
                worldGrade = r.ReadBytes(checked(fine.CellCount * 2));
                if (worldGrade.Length != fine.CellCount * 2) throw new EndOfStreamException();
            }
            terrainFine = classes[LevelFine]; terrainMid = classes[LevelMid]; terrainCoarse = classes[LevelCoarse];
        }

        // Penalize longitudinal grade as well as the existing surface-inclination classes.
        // nx, ny are the direction of travel in normalized horizontal metres.
        double GradeFactor(int cell, double nx, double ny, RoadRouteMode mode) {
            if (mode != RoadRouteMode.Shortest && mode != RoadRouteMode.PavedOnly) return 1.0;
            int i = cell * 2;
            double risePercent = Math.Abs(unchecked((sbyte)worldGrade[i]) * nx + unchecked((sbyte)worldGrade[i+1]) * ny);
            double excess = Math.Max(0.0, (risePercent-25.0)/25.0);
            return 1.0 + excess*excess;
        }
    }
}
