using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
class Footprints {
    const int N=3804;
    const double dx=1521618.0/N,dy=1523618.0/N;
    struct V {
        public double x,y,z;
        public V(double a,double b,double c){x=a;y=b;z=c;}
        public static V operator +(V a,V b){return new V(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static V operator *(double k,V a){return new V(k*a.x,k*a.y,k*a.z);}
        public static V Product(V a,V b){return new V(a.x*b.x,a.y*b.y,a.z*b.z);}
        public static V Cross(V a,V b){return new V(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
    }
    struct Q {
        V v;double w;
        public Q(double x,double y,double z,double w){v=new V(x,y,z);this.w=w;}
        public V Rot(V a){return a+2*V.Cross(v,V.Cross(v,a)+w*a);}
    }
    class Box{public V c,u,v,w;public byte flag;}
    static float[] ReadFloats(string path){byte[] b=File.ReadAllBytes(path);float[] a=new float[N*N];if(b.Length!=a.Length*4)throw new InvalidDataException(path);Buffer.BlockCopy(b,0,a,0,b.Length);return a;}
    static float[] ground,deck;
    static byte[] mask=new byte[N*N];
    // Triangle against a voxel with half-extents (100,100,101) cm around vehicle height.
    static bool Separates(V axis,V a,V b,V c) {
        double r=100*(Math.Abs(axis.x)+Math.Abs(axis.y))+101*Math.Abs(axis.z);
        double x=axis.x*a.x+axis.y*a.y+axis.z*a.z,y=axis.x*b.x+axis.y*b.y+axis.z*b.z,z=axis.x*c.x+axis.y*c.y+axis.z*c.z;
        return Math.Min(x,Math.Min(y,z))>r || Math.Max(x,Math.Max(y,z)) < -r;
    }
    static bool EdgeSeparates(V edge,V a,V b,V c) {
        return Separates(new V(0,edge.z,-edge.y),a,b,c)||Separates(new V(-edge.z,0,edge.x),a,b,c)||Separates(new V(edge.y,-edge.x,0),a,b,c);
    }
    static void DrawTriangle(V a,V b,V c) {
        int x0=Math.Max(0,(int)Math.Ceiling((617718-Math.Max(a.x,Math.Max(b.x,c.x))-100)/dx-.5)),x1=Math.Min(N-1,(int)Math.Floor((617718-Math.Min(a.x,Math.Min(b.x,c.x))+100)/dx-.5));
        int y0=Math.Max(0,(int)Math.Ceiling((618618-Math.Max(a.y,Math.Max(b.y,c.y))-100)/dy-.5)),y1=Math.Min(N-1,(int)Math.Floor((618618-Math.Min(a.y,Math.Min(b.y,c.y))+100)/dy-.5));
        V ab=b+(-1)*a,bc=c+(-1)*b,ca=a+(-1)*c,normal=V.Cross(ab,bc);
        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
            int i=y*N+x;
            double floor=Math.Max(ground[i],deck[i]);
            if(Math.Max(a.z,Math.Max(b.z,c.z))<floor+18 || Math.Min(a.z,Math.Min(b.z,c.z))>floor+220)continue;
            V offset=new V((x+.5)*dx-617718,(y+.5)*dy-618618,-floor-119);
            V p=a+offset,q=b+offset,r=c+offset;
            if(!Separates(normal,p,q,r)&&!EdgeSeparates(ab,p,q,r)&&!EdgeSeparates(bc,p,q,r)&&!EdgeSeparates(ca,p,q,r))mask[i]|=136;
        }
    }
    static bool OverlapsAxis(double ax,double ay,V u,V v,V w,double x,double y,double radius) {
        return Math.Abs(ax*x+ay*y)<=Math.Abs(ax*u.x+ay*u.y)+Math.Abs(ax*v.x+ay*v.y)+Math.Abs(ax*w.x+ay*w.y)+radius*(Math.Abs(ax)+Math.Abs(ay));
    }
    static double Dot(V a,V b){return a.x*b.x+a.y*b.y+a.z*b.z;}
    static bool BoxAxis(V a,V u,V v,V w,V off,double radius){
        double extent=Math.Abs(Dot(a,u))+Math.Abs(Dot(a,v))+Math.Abs(Dot(a,w));
        return Math.Abs(Dot(a,off))<=extent+radius*(Math.Abs(a.x)+Math.Abs(a.y))+101*Math.Abs(a.z);
    }
    static bool BoxEdges(V a,V u,V v,V w,V off,double radius){
        return BoxAxis(new V(0,a.z,-a.y),u,v,w,off,radius) &&
            BoxAxis(new V(-a.z,0,a.x),u,v,w,off,radius) && BoxAxis(new V(a.y,-a.x,0),u,v,w,off,radius);
    }
    static bool VehicleIntersects(V u,V v,V w,V off,double radius){
        // World X/Y/Z already checked in the broad phase. A transformed mesh box also
        // needs its three face normals and the nine pairs of cross-product axes.
        return BoxAxis(V.Cross(u,v),u,v,w,off,radius)&&BoxAxis(V.Cross(u,w),u,v,w,off,radius)&&
            BoxAxis(V.Cross(v,w),u,v,w,off,radius)&&BoxEdges(u,u,v,w,off,radius)&&
            BoxEdges(v,u,v,w,off,radius)&&BoxEdges(w,u,v,w,off,radius);
    }
    static void Draw(V c,V u,V v,V w,byte flag) {
        // Project collision primitives onto the nav grid; allow a one-metre vehicle half-width.
        // Keep a separate tighter mask for known road centerlines (registration uncertainty).
        const double outer=300,inner=100;
        double ex=Math.Abs(u.x)+Math.Abs(v.x)+Math.Abs(w.x),ey=Math.Abs(u.y)+Math.Abs(v.y)+Math.Abs(w.y);
        double ez=Math.Abs(u.z)+Math.Abs(v.z)+Math.Abs(w.z);
        int x0=Math.Max(0,(int)Math.Ceiling((617718-c.x-ex-outer)/dx-.5)),x1=Math.Min(N-1,(int)Math.Floor((617718-c.x+ex+outer)/dx-.5));
        int y0=Math.Max(0,(int)Math.Ceiling((618618-c.y-ey-outer)/dy-.5)),y1=Math.Min(N-1,(int)Math.Floor((618618-c.y+ey+outer)/dy-.5));
        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
            int i=y*N+x;double floor=float.IsNegativeInfinity(deck[i])?ground[i]:Math.Max(ground[i],deck[i]);
            if(c.z+ez<floor+18 || c.z-ez>floor+220)continue;
            double ox=617718-(x+.5)*dx-c.x,oy=618618-(y+.5)*dy-c.y;
            if(!OverlapsAxis(-u.y,u.x,u,v,w,ox,oy,outer)||!OverlapsAxis(-v.y,v.x,u,v,w,ox,oy,outer)||!OverlapsAxis(-w.y,w.x,u,v,w,ox,oy,outer))continue;
            mask[i]|=flag;
            bool containsSmallObject=ex<dx && ey<dy && Math.Abs(ox)<=dx/2 && Math.Abs(oy)<=dy/2;
            double radius=containsSmallObject?Math.Max(dx,dy)/2:inner;
            if(Math.Abs(ox)<=ex+radius && Math.Abs(oy)<=ey+radius &&
                VehicleIntersects(u,v,w,new V(ox,oy,floor+119-c.z),radius))mask[i]|=128;
        }
    }
    static void Main(string[] args){
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;ground=ReadFloats("ground.f32");deck=ReadFloats("deck-used.f32");
        var shapes=new Dictionary<int,List<Box>>();
        var spillwayIds=new HashSet<int>();
        foreach(string line in File.ReadLines("models.tsv")){
            var p=line.Split('\t');int id=int.Parse(p[0]);var d=new double[10];for(int j=0;j<10;j++)d[j]=double.Parse(p[5+j]);
            if(p[2].EndsWith(".SM_Dam_Outflow",StringComparison.OrdinalIgnoreCase))spillwayIds.Add(id);
            Q q=new Q(d[6],d[7],d[8],d[9]);if(!shapes.ContainsKey(id))shapes[id]=new List<Box>();
            shapes[id].Add(new Box{c=new V(d[0],d[1],d[2]),u=q.Rot(new V(d[3]/2,0,0)),v=q.Rot(new V(0,d[4]/2,0)),w=q.Rot(new V(0,0,d[5]/2)),flag=(byte)(p[1]=="tree"?1:p[1]=="wood"?2:p[1]=="rock"?4:8)});
        }
        var detailed=new Dictionary<int,List<V>>();
        foreach(string row in File.ReadLines("detailed-models.tsv")) {
            int id=int.Parse(row.Split('\t')[0]);var verts=new List<V>();
            using(var f=new BinaryReader(File.OpenRead(Path.Combine("detailed",id+".bin"))))
                while(f.BaseStream.Position<f.BaseStream.Length)verts.Add(new V(f.ReadSingle(),f.ReadSingle(),f.ReadSingle()));
            detailed.Add(id,verts);
        }
        var counts=new long[16];
        var triangles=new List<V>();
        using(var b=new BinaryReader(File.OpenRead("outflow-triangles.f32")))
            while(b.BaseStream.Position<b.BaseStream.Length)triangles.Add(new V(b.ReadSingle(),b.ReadSingle(),b.ReadSingle()));
        foreach(string file in Directory.GetFiles("tiles","*.bin"))using(var b=new BinaryReader(File.OpenRead(file))){
            while(b.BaseStream.Position<b.BaseStream.Length) {
                int id=b.ReadInt32();var boxes=shapes[id];V pos=new V(b.ReadSingle(),b.ReadSingle(),b.ReadSingle());
                var q=new Q(b.ReadSingle(),b.ReadSingle(),b.ReadSingle(),b.ReadSingle());V s=new V(b.ReadSingle(),b.ReadSingle(),b.ReadSingle());
                counts[boxes[0].flag]++;
                List<V> verts;
                if(!detailed.TryGetValue(id,out verts) && spillwayIds.Contains(id))verts=triangles;
                if(verts!=null){
                    for(int i=0;i<verts.Count;i+=3)
                        DrawTriangle(pos+q.Rot(V.Product(s,verts[i])),pos+q.Rot(V.Product(s,verts[i+1])),pos+q.Rot(V.Product(s,verts[i+2])));
                    continue;
                }
                foreach(var box in boxes) Draw(pos+q.Rot(V.Product(s,box.c)),q.Rot(V.Product(s,box.u)),q.Rot(V.Product(s,box.v)),q.Rot(V.Product(s,box.w)),box.flag);
            }
        }
        File.WriteAllBytes("obstacles.u8",mask);Console.WriteLine("tree {0}, stump/wood {1}, rock {2}, structures {3}",counts[1],counts[2],counts[4],counts[8]);
    }
}
