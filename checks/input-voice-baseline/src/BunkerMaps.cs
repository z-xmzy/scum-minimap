using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ScumMiniMap {
    // Immutable after loading. Coordinates and surface heights are game-world centimetres.
    internal sealed class BunkerFloor {
        internal string DisplayName;
        internal BunkerLabel[] Labels=new BunkerLabel[0];
        internal string Id,BunkerId,Kind,ImagePath;
        internal int Rank,Count,Width,Height;
        internal double MinX,MinY,MaxX,MaxY,SurfaceZ,CellSize;
        internal float[] Heights=null;
        internal int[] TileOffsets;
        internal float[] Tiles;
        internal float[] SurfaceTriangles=new float[0];
        internal byte[] ImageHash;
        float HeightAt(int row,int col) {
            if(Heights!=null)return Heights[row*Width+col];
            int offset=TileOffsets[(row/8)*((Width+7)/8)+col/8];
            return offset<0?float.NaN:Tiles[offset+(row%8)*8+col%8];
        }
        internal double Score(double x,double y,double z) {
            if(!BunkerAtlas.Finite(x)||!BunkerAtlas.Finite(y)||!BunkerAtlas.Finite(z)||x<MinX||y<MinY||x>=MaxX||y>=MaxY)return double.PositiveInfinity;
            int col=(int)((MaxX-x)/CellSize),row=(int)((MaxY-y)/CellSize);
            double best=double.PositiveInfinity;
            // A neighbouring cell is only a seam allowance for fine grids. Large complexes
            // use precise surfaces for gaps; a coarse cell must not pull exterior points indoors.
            int reach=CellSize<=30?1:0;
            for(int r=Math.Max(0,row-reach);r<=Math.Min(Height-1,row+reach);r++)for(int c=Math.Max(0,col-reach);c<=Math.Min(Width-1,col+reach);c++) {
                float floor=HeightAt(r,c);if(float.IsNaN(floor))continue;
                double offset=z-floor;
                // Standing/crouching calibration differs by 30 cm; never subtract a fixed posture.
                if(offset>=20&&offset<=145)best=Math.Min(best,Math.Abs(offset-77)+(r==row&&c==col?0:5));
            }
            if(!double.IsPositiveInfinity(best))return best;
            // Stacked floors and flights can share a height layer and XY cell. Preserve the lower
            // surface with an exact triangle instead of extending the posture tolerance.
            for(int i=0;i<SurfaceTriangles.Length;i+=9) {
                double ax=SurfaceTriangles[i],ay=SurfaceTriangles[i+1],az=SurfaceTriangles[i+2];
                double vx=SurfaceTriangles[i+3]-ax,vy=SurfaceTriangles[i+4]-ay,wx=SurfaceTriangles[i+6]-ax,wy=SurfaceTriangles[i+7]-ay;
                double determinant=vx*wy-vy*wx;if(Math.Abs(determinant)<.001)continue;
                double s=((x-ax)*wy-(y-ay)*wx)/determinant,t=(vx*(y-ay)-vy*(x-ax))/determinant;
                if(s<-.00001||t<-.00001||s+t>1.00001)continue;
                double offset=z-(az+s*(SurfaceTriangles[i+5]-az)+t*(SurfaceTriangles[i+8]-az));
                if(offset>=20&&offset<=145)best=Math.Min(best,Math.Abs(offset-77));
            }
            return best;
        }
    }
    internal sealed class BunkerLabel { internal double X,Y,MinX,MinY,MaxX,MaxY;internal string Text,Colour,Kind;internal bool Bounded; }
    internal sealed class BunkerAtlas {
        internal BunkerFloor[] Floors;
        internal BunkerFloor[] Views=new BunkerFloor[0];
        readonly Dictionary<string,BunkerFloor> defaultViews=new Dictionary<string,BunkerFloor>(StringComparer.Ordinal);
        internal int GameBuild;
        internal BunkerFloor FindFloor(double x,double y,double z) {
            BunkerFloor best=null;double score=double.PositiveInfinity;
            foreach(var floor in Floors) {double next=floor.Score(x,y,z);if(next<score) {score=next;best=floor;}}
            return best;
        }
        internal BunkerFloor ViewFor(BunkerFloor floor,double x,double y,double z) {
            if(floor==null)return null;
            var candidates=Views.Where(v=>v.BunkerId==floor.BunkerId).ToArray();if(candidates.Length==0)return floor;
            BunkerFloor best=null;double score=double.PositiveInfinity;
            foreach(var view in candidates) {double next=view.Score(x,y,z);if(next<score) {score=next;best=view;}}
            if(best!=null)return best;
            if(defaultViews.TryGetValue(floor.Id,out best))return best;
            return candidates.OrderBy(v=>Math.Abs(v.SurfaceZ-(z-77))).First();
        }
        internal static bool Finite(double value) { return !double.IsNaN(value)&&!double.IsInfinity(value); }
        static string Text(BinaryReader r) {
            int count=r.ReadInt32();if(count<1||count>1024)throw new InvalidDataException("Invalid bunker text length.");
            byte[] bytes=r.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);
        }
        internal static BunkerAtlas Load(string folder) {
            var atlas=Read(folder,"atlas.bin");
            if(!File.Exists(Path.Combine(folder,"views.bin")))return atlas;
            var views=Read(folder,"views.bin");if(views.GameBuild!=atlas.GameBuild)throw new InvalidDataException("Bunker display build differs.");atlas.Views=views.Floors;
            var byId=atlas.Views.ToDictionary(v=>v.Id,StringComparer.Ordinal);var labels=atlas.Views.ToDictionary(v=>v.Id,v=>new List<BunkerLabel>(),StringComparer.Ordinal);
            foreach(var parts in Rows(folder,"views.tsv",2)) {BunkerFloor view;if(!byId.TryGetValue(parts[0],out view))throw new InvalidDataException("Unknown bunker view.");view.DisplayName=parts[1];}
            foreach(var parts in Rows(folder,"labels.tsv",4)) {
                List<BunkerLabel> target;double x,y;if(!labels.TryGetValue(parts[0],out target)||!double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!double.TryParse(parts[2],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!Finite(x)||!Finite(y))throw new InvalidDataException("Invalid bunker label.");
                target.Add(new BunkerLabel {X=x,Y=y,Text=parts[3]});
            }
            foreach(var view in atlas.Views)view.Labels=labels[view.Id].ToArray();
            if(File.Exists(Path.Combine(folder,"labels-bounded.tsv"))) {
                foreach(var target in labels.Values)target.Clear();
                foreach(var parts in Rows(folder,"labels-bounded.tsv",10)) {
                    List<BunkerLabel> target;var values=new double[6];int[] columns={1,2,4,5,6,7};
                    if(!labels.TryGetValue(parts[0],out target))throw new InvalidDataException("Unknown bounded label view.");
                    for(int i=0;i<columns.Length;i++)if(!double.TryParse(parts[columns[i]],NumberStyles.Float,CultureInfo.InvariantCulture,out values[i])||!Finite(values[i]))throw new InvalidDataException("Invalid label bounds.");
                    uint colour;if(parts[8].Length!=6||!uint.TryParse(parts[8],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out colour)||values[2]>=values[4]||values[3]>=values[5]||values[0]<values[2]||values[0]>values[4]||values[1]<values[3]||values[1]>values[5])throw new InvalidDataException("Invalid label rectangle.");
                    target.Add(new BunkerLabel {X=values[0],Y=values[1],Text=parts[3],MinX=values[2],MinY=values[3],MaxX=values[4],MaxY=values[5],Colour=parts[8],Kind=parts[9],Bounded=true});
                }
                foreach(var view in atlas.Views)view.Labels=labels[view.Id].ToArray();
            }
            foreach(string filename in new[]{"entrances.tsv","connections.tsv"})foreach(var parts in Rows(folder,filename,4)) {
                BunkerFloor view;double x,y;
                if(!byId.TryGetValue(parts[0],out view)||!double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!double.TryParse(parts[2],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!Finite(x)||!Finite(y)||x<view.MinX||x>=view.MaxX||y<view.MinY||y>=view.MaxY||string.IsNullOrWhiteSpace(parts[3]))throw new InvalidDataException("Invalid bunker entrance.");
                bool entrance=filename=="entrances.tsv";labels[view.Id].Add(new BunkerLabel {X=x,Y=y,Text=parts[3],Kind=entrance?"entrance":"connection",Colour=entrance?"62D7AF":"E9B85D"});
            }
            foreach(var view in atlas.Views)view.Labels=labels[view.Id].ToArray();
            var raw=atlas.Floors.ToDictionary(f=>f.Id,StringComparer.Ordinal);
            foreach(var parts in Rows(folder,"mapping.tsv",2)) {BunkerFloor view,floor;if(!byId.TryGetValue(parts[1],out view)||!raw.TryGetValue(parts[0],out floor)||view.BunkerId!=floor.BunkerId)throw new InvalidDataException("Invalid bunker view mapping.");atlas.defaultViews.Add(parts[0],view);}
            return atlas;
        }
        static IEnumerable<string[]> Rows(string folder,string name,int columns) {
            string path=Path.Combine(folder,name);if(!File.Exists(path))yield break;
            if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("Bunker annotations too large.");
            foreach(string line in File.ReadLines(path,Encoding.UTF8)) {if(string.IsNullOrWhiteSpace(line))continue;var parts=line.Split('\t');if(parts.Length!=columns||parts.Any(p=>p.Length>1024))throw new InvalidDataException("Invalid bunker annotation row.");yield return parts;}
        }
        static BunkerAtlas Read(string folder,string name) {
            using(var file=File.OpenRead(Path.Combine(folder,name)))using(var zip=new GZipStream(file,CompressionMode.Decompress))using(var r=new BinaryReader(zip)) {
                if(r.ReadUInt32()!=0x414D4253)throw new InvalidDataException("Unsupported bunker atlas.");
                int format=r.ReadInt32();if(format!=2&&format!=3)throw new InvalidDataException("Unsupported bunker atlas.");
                int build=r.ReadInt32(),count=r.ReadInt32();if(count<1||count>1000)throw new InvalidDataException("Invalid layer count.");
                var floors=new BunkerFloor[count];long total=0;
                for(int i=0;i<count;i++) {
                    var f=new BunkerFloor { Id=Text(r),BunkerId=Text(r),Kind=Text(r) };string image=Text(r);
                    if(Path.GetFileName(image)!=image||!image.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||image.IndexOf(':')>=0)throw new InvalidDataException("Invalid layer image path.");
                    f.ImagePath=Path.Combine(folder,image);f.Rank=r.ReadInt32();f.Count=r.ReadInt32();
                    f.MinX=r.ReadDouble();f.MinY=r.ReadDouble();f.MaxX=r.ReadDouble();f.MaxY=r.ReadDouble();f.SurfaceZ=r.ReadDouble();f.CellSize=r.ReadDouble();
                    f.Width=r.ReadInt32();f.Height=r.ReadInt32();
                    if(!Finite(f.MinX)||!Finite(f.MinY)||!Finite(f.MaxX)||!Finite(f.MaxY)||!Finite(f.SurfaceZ)||!Finite(f.CellSize)||f.CellSize<1||f.CellSize>200||f.Width<1||f.Height<1||f.Width>2048||f.Height>2048||f.Rank<1||f.Rank>f.Count||f.Count>100||Math.Abs(f.MaxX-f.MinX-f.Width*f.CellSize)>.01||Math.Abs(f.MaxY-f.MinY-f.Height*f.CellSize)>.01)throw new InvalidDataException("Invalid bunker grid.");
                    f.ImageHash=r.ReadBytes(32);if(f.ImageHash.Length!=32)throw new EndOfStreamException();
                    int slots=((f.Width+7)/8)*((f.Height+7)/8),tiles=r.ReadInt32();
                    if(tiles<1||tiles>slots)throw new InvalidDataException("Invalid surface tiles.");
                    total+=slots+tiles*64;if(total>16000000)throw new InvalidDataException("Bunker atlas exceeds memory limit.");
                    f.TileOffsets=new int[slots];for(int j=0;j<slots;j++)f.TileOffsets[j]=-1;
                    f.Tiles=new float[tiles*64];
                    for(int tile=0;tile<tiles;tile++) {
                        int index=r.ReadInt32();if(index<0||index>=slots||f.TileOffsets[index]>=0)throw new InvalidDataException("Invalid surface tile index.");
                        f.TileOffsets[index]=tile*64;byte[] bytes=r.ReadBytes(256);if(bytes.Length!=256)throw new EndOfStreamException();
                        Buffer.BlockCopy(bytes,0,f.Tiles,tile*256,256);
                    }
                    foreach(float value in f.Tiles)if(float.IsInfinity(value))throw new InvalidDataException("Invalid surface height.");
                    if(format>=3) {
                        int triangles=r.ReadInt32();if(triangles<0||triangles>20000)throw new InvalidDataException("Invalid surface geometry count.");
                        total+=triangles*9;if(total>16000000)throw new InvalidDataException("Bunker atlas exceeds memory limit.");
                        byte[] data=r.ReadBytes(triangles*36);if(data.Length!=triangles*36)throw new EndOfStreamException();
                        f.SurfaceTriangles=new float[triangles*9];Buffer.BlockCopy(data,0,f.SurfaceTriangles,0,data.Length);
                        foreach(float value in f.SurfaceTriangles)if(!Finite(value))throw new InvalidDataException("Invalid surface coordinate.");
                    }
                    floors[i]=f;
                }
                if(zip.ReadByte()!=-1)throw new InvalidDataException("Unexpected atlas data.");
                return new BunkerAtlas { Floors=floors,GameBuild=build };
            }
        }
    }
    internal sealed class BunkerTracker {
        readonly BunkerFloor[] floors;
        internal BunkerFloor Current { get;private set; }
        BunkerFloor pending;
        DateTime last,pendingSince,missingSince;
        double lastX,lastY;
        internal BunkerTracker(BunkerFloor[] floors) { this.floors=floors; }
        internal void Observe(double x,double y,double z,DateTime timestamp) {
            if(timestamp<=last)return;
            if(!BunkerAtlas.Finite(x)||!BunkerAtlas.Finite(y)||!BunkerAtlas.Finite(z)) { Current=pending=null;last=timestamp;return; }
            bool teleport=last!=default(DateTime)&&((x-lastX)*(x-lastX)+(y-lastY)*(y-lastY)>20000.0*20000||timestamp-last>TimeSpan.FromSeconds(15));
            last=timestamp;lastX=x;lastY=y;
            if(teleport) { Current=pending=null;missingSince=default(DateTime); }
            if(Current!=null&&!double.IsPositiveInfinity(Current.Score(x,y,z))) { pending=null;missingSince=default(DateTime);return; }
            BunkerFloor best=null;double score=double.PositiveInfinity;
            foreach(var f in floors) { double s=f.Score(x,y,z);if(s<score) { best=f;score=s; } }
            if(best!=null) {
                missingSince=default(DateTime);
                if(pending!=best) { pending=best;pendingSince=timestamp; }
                else if(timestamp-pendingSince>=TimeSpan.FromMilliseconds(250)) { Current=best;pending=null; }
            } else {
                pending=null;
                if(missingSince==default(DateTime))missingSince=timestamp;
                if(timestamp-missingSince>=TimeSpan.FromSeconds(1))Current=null;
            }
        }
    }
}
