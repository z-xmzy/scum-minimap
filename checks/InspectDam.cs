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
    static bool OverlapsAxis(double ax,double ay,V u,V v,V w,double x,double y,double radius) {
        return Math.Abs(ax*x+ay*y)<=Math.Abs(ax*u.x+ay*u.y)+Math.Abs(ax*v.x+ay*v.y)+Math.Abs(ax*w.x+ay*w.y)+radius*(Math.Abs(ax)+Math.Abs(ay));
    }
    static string current;
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
            if(containsSmallObject || (Math.Abs(ox)<=ex+inner && Math.Abs(oy)<=ey+inner && OverlapsAxis(-u.y,u.x,u,v,w,ox,oy,inner)&&OverlapsAxis(-v.y,v.x,u,v,w,ox,oy,inner)&&OverlapsAxis(-w.y,w.x,u,v,w,ox,oy,inner))){mask[i]|=128;if(x==(int)(.4006*N) && y==(int)(.235*N)) Console.WriteLine(current);}
        }
    }
    static void Main(string[] args){
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;ground=ReadFloats("ground.f32");deck=ReadFloats("deck-used.f32");
        var shapes=new Dictionary<int,List<Box>>();
        foreach(string line in File.ReadLines("models.tsv")){
            var p=line.Split('\t');int id=int.Parse(p[0]);var d=new double[10];for(int j=0;j<10;j++)d[j]=double.Parse(p[5+j]);
            Q q=new Q(d[6],d[7],d[8],d[9]);if(!shapes.ContainsKey(id))shapes[id]=new List<Box>();
            shapes[id].Add(new Box{c=new V(d[0],d[1],d[2]),u=q.Rot(new V(d[3]/2,0,0)),v=q.Rot(new V(0,d[4]/2,0)),w=q.Rot(new V(0,0,d[5]/2)),flag=(byte)(p[1]=="tree"?1:p[1]=="wood"?2:p[1]=="rock"?4:8)});
        }
        var names=new Dictionary<int,string>();foreach(string line in File.ReadLines("models.tsv")){var p=line.Split('\t');names[int.Parse(p[0])]=p[2]+" "+p[4];} var counts=new long[16];
        foreach(string file in Directory.GetFiles("tiles","*.bin"))using(var b=new BinaryReader(File.OpenRead(file))){
            while(b.BaseStream.Position<b.BaseStream.Length) {
                int id=b.ReadInt32();current=names[id];var boxes=shapes[id];V pos=new V(b.ReadSingle(),b.ReadSingle(),b.ReadSingle());
                var q=new Q(b.ReadSingle(),b.ReadSingle(),b.ReadSingle(),b.ReadSingle());V s=new V(b.ReadSingle(),b.ReadSingle(),b.ReadSingle());
                counts[boxes[0].flag]++;
                foreach(var box in boxes) Draw(pos+q.Rot(V.Product(s,box.c)),q.Rot(V.Product(s,box.u)),q.Rot(V.Product(s,box.v)),q.Rot(V.Product(s,box.w)),box.flag);
            }
        }
        Console.WriteLine("tree {0}, stump/wood {1}, rock {2}, structures {3}",counts[1],counts[2],counts[4],counts[8]);
    }
}
