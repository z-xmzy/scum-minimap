using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

class AuditRaster {
    struct V {
        public double x,y,z;
        public V(double x,double y,double z){this.x=x;this.y=y;this.z=z;}
        public static V operator +(V a,V b){return new V(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static V operator -(V a,V b){return new V(a.x-b.x,a.y-b.y,a.z-b.z);}
        public static V operator *(V a,double t){return new V(a.x*t,a.y*t,a.z*t);}
        public double Dot(V b){return x*b.x+y*b.y+z*b.z;}
        public V Cross(V b){return new V(y*b.z-z*b.y,z*b.x-x*b.z,x*b.y-y*b.x);}
    }
    class Shape {public int kind; public V[] v;}
    class Instance {
        public V p,s,q;public double w;
        public V Transform(V v){V t=new V(v.x*s.x,v.y*s.y,v.z*s.z);return p+t+q.Cross(q.Cross(t)+t*w)*2;}
    }
    struct Bounds {public double a,b;public V axis;}
    const double DX=1521618.0/3804,DY=1523618.0/3804;
    static int N,S,id;static double X,Y,ex,ey,ez;static float[] lo,hi;
    static byte[] flags;static int[] owner;static bool[] inside;
    static readonly HashSet<int> endpointHits=new HashSet<int>();
    static Dictionary<int,List<Shape>> models=new Dictionary<int,List<Shape>>();
    static Dictionary<int,string> types=new Dictionary<int,string>();
    static V Read(BinaryReader b){return new V(b.ReadSingle(),b.ReadSingle(),b.ReadSingle());}
    static float[] Floats(string file){var a=new float[N*N];Buffer.BlockCopy(File.ReadAllBytes(file),0,a,0,4*a.Length);return a;}
    static void Mark(int i){if(i==N*N)endpointHits.Add(id);else {flags[i]=1;owner[i]=id;}}
    delegate void Visit(int i,V pos,double radius,double hz);
    static void Scan(double x0,double x1,double y0,double y1,double z0,double z1,Visit visit){
        double rx=100+Math.Max(DX,DY)/(2*S);
        int a=Math.Max(0,(int)Math.Ceiling((X-x1-rx)*S/DX-.5)),b=Math.Min(N-1,(int)Math.Floor((X-x0+rx)*S/DX-.5));
        int c=Math.Max(0,(int)Math.Ceiling((Y-y1-rx)*S/DY-.5)),d=Math.Min(N-1,(int)Math.Floor((Y-y0+rx)*S/DY-.5));
        for(int y=c;y<=d;y++)for(int x=a;x<=b;x++){
            int i=y*N+x;double zlow=lo[i]+18,zhigh=hi[i]+220;
            if(z0>zhigh || z1<zlow)continue;
            visit(i,new V(X-(x+.5)*DX/S,Y-(y+.5)*DY/S,(zlow+zhigh)/2),rx,(zhigh-zlow)/2);
        }
        if(ex>=x0-100 && ex<=x1+100 && ey>=y0-100 && ey<=y1+100 && ez+220>=z0 && ez+18<=z1)
            visit(N*N,new V(ex,ey,ez+119),100,101);
    }
    static double Interval(double v,double e){return Math.Max(0,Math.Abs(v)-e);}
    static double Dist2(V a,V d,double t,double radius,double height){
        V v=a+d*t;double x=Interval(v.x,radius),y=Interval(v.y,radius),z=Interval(v.z,height);
        return x*x+y*y+z*z;
    }
    static void Capsule(V a,V b,double r,double innerRadius){
        V d=b-a;
        Scan(Math.Min(a.x,b.x)-r,Math.Max(a.x,b.x)+r,Math.Min(a.y,b.y)-r,Math.Max(a.y,b.y)+r,
             Math.Min(a.z,b.z)-r,Math.Max(a.z,b.z)+r,(i,q,rx,h)=>{
                 V offset=a-q;double l=0,u=1;
                 // Squared distance from the capsule axis to a box is convex on [0,1].
                 for(int j=0;j<32;j++){double t1=(2*l+u)/3,t2=(l+2*u)/3;if(Dist2(offset,d,t1,rx,h)<Dist2(offset,d,t2,rx,h))u=t2;else l=t1;}
                 double limit=i==N*N?innerRadius:r;
                 if(Dist2(offset,d,(l+u)/2,rx,h)<=limit*limit+1e-5)Mark(i);
             });
    }
    static double Min(double a,double b,double c){return Math.Min(a,Math.Min(b,c));}
    static double Max(double a,double b,double c){return Math.Max(a,Math.Max(b,c));}
    static void Triangle(V a,V b,V c,bool fill){
        V ab=b-a,ac=c-a,bc=c-b,normal=ab.Cross(ac);
        double x0=Min(a.x,b.x,c.x),x1=Max(a.x,b.x,c.x),y0=Min(a.y,b.y,c.y),y1=Max(a.y,b.y,c.y);
        if(x1<X-N*DX/S-130 || x0>X+130 || y1<Y-N*DY/S-130 || y0>Y+130)return;
        var axes=new Bounds[10];int n=0;
        foreach(V ax in new[]{normal,new V(0,ab.z,-ab.y),new V(-ab.z,0,ab.x),new V(ab.y,-ab.x,0),
            new V(0,bc.z,-bc.y),new V(-bc.z,0,bc.x),new V(bc.y,-bc.x,0),
            new V(0,ac.z,-ac.y),new V(-ac.z,0,ac.x),new V(ac.y,-ac.x,0)}){
            double v=ab.Dot(ax),w=ac.Dot(ax);axes[n++]=new Bounds{axis=ax,a=Min(0,v,w),b=Max(0,v,w)};
        }
        double z0=Min(a.z,b.z,c.z),z1=Max(a.z,b.z,c.z);
        Scan(x0,x1,y0,y1,fill?double.NegativeInfinity:z0,z1,(i,q,rx,h)=>{
            V k=q-a;
            if(fill && Math.Abs(normal.z)>1e-9){
                double u=(k.x*ac.y-k.y*ac.x)/normal.z,v=(ab.x*k.y-ab.y*k.x)/normal.z;
                if(u>=0 && v>=0 && u+v<1 && a.z+u*ab.z+v*ac.z>q.z)inside[i]=!inside[i];
            }
            if(q.z+h<z0 || q.z-h>z1 || q.x+rx<x0 || q.x-rx>x1 || q.y+rx<y0 || q.y-rx>y1)return;
            foreach(var plane in axes){double t=k.Dot(plane.axis),s=rx*(Math.Abs(plane.axis.x)+Math.Abs(plane.axis.y))+h*Math.Abs(plane.axis.z);
                if(t+s<plane.a || t-s>plane.b)return;}
            Mark(i);
        });
    }
    static void Draw(Instance tr,Shape shape){
        if(shape.kind==3){
            // Non-uniform scaling is bounded conservatively by the largest scale.
            double scale=Math.Max(Math.Abs(tr.s.x),Math.Max(Math.Abs(tr.s.y),Math.Abs(tr.s.z)));
            Capsule(tr.Transform(shape.v[0]),tr.Transform(shape.v[1]),scale*shape.v[2].x,Math.Min(Math.Abs(tr.s.x),Math.Min(Math.Abs(tr.s.y),Math.Abs(tr.s.z)))*shape.v[2].x);
            return;
        }
        bool fill=shape.kind==1 || types[id]=="rock";
        int xmin=N,xmax=-1,ymin=N,ymax=-1;
        for(int j=0;j<shape.v.Length;j+=3){
            V a=tr.Transform(shape.v[j]),b=tr.Transform(shape.v[j+1]),c=tr.Transform(shape.v[j+2]);Triangle(a,b,c,fill);
            if(fill){xmin=Math.Min(xmin,(int)((X-Max(a.x,b.x,c.x))*S/DX)-1);xmax=Math.Max(xmax,(int)((X-Min(a.x,b.x,c.x))*S/DX)+1);
                ymin=Math.Min(ymin,(int)((Y-Max(a.y,b.y,c.y))*S/DY)-1);ymax=Math.Max(ymax,(int)((Y-Min(a.y,b.y,c.y))*S/DY)+1);}
        }
        if(fill){
            for(int y=Math.Max(0,ymin);y<=Math.Min(N-1,ymax);y++)for(int x=Math.Max(0,xmin);x<=Math.Min(N-1,xmax);x++){
                int i=y*N+x;if(inside[i]){Mark(i);inside[i]=false;}
            }
            if(inside[N*N]){Mark(N*N);inside[N*N]=false;}
        }
    }
    static void Main(){
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        foreach(string line in File.ReadLines("../models.tsv").Concat(File.ReadLines("props.tsv"))){var p=line.Split('\t');types[int.Parse(p[0])]=p[1];}
        foreach(string file in Directory.GetFiles(".","*.geom"))using(var b=new BinaryReader(File.OpenRead(file))){
            var list=new List<Shape>();models[int.Parse(Path.GetFileNameWithoutExtension(file))]=list;
            while(b.BaseStream.Position<b.BaseStream.Length){int kind=b.ReadInt32();var v=new V[b.ReadInt32()];for(int i=0;i<v.Length;i++)v[i]=Read(b);list.Add(new Shape{kind=kind,v=v});}
        }
        using(var report=new StreamWriter("hits.tsv"))foreach(string line in File.ReadLines("regions.tsv")){
            var r=line.Split('\t');string key=r[0];N=int.Parse(r[3])+2;S=int.Parse(r[4]);
            X=617718-int.Parse(r[1])*DX+DX/S;Y=618618-int.Parse(r[2])*DY+DY/S;ex=617718-double.Parse(r[5])*1521618;ey=618618-double.Parse(r[6])*1523618;ez=double.Parse(r[7]);
            lo=Floats(key+".low");hi=Floats(key+".high");flags=File.ReadAllBytes(key+".base");inside=new bool[N*N+1];owner=new int[N*N];endpointHits.Clear();
            foreach(var file in new[]{key+".instances",key+".props"})using(var b=new BinaryReader(File.OpenRead(file)))while(b.BaseStream.Position<b.BaseStream.Length){
                id=b.ReadInt32();var t=new Instance{p=Read(b),q=Read(b),w=b.ReadSingle(),s=Read(b)};
                foreach(var shape in models[id])Draw(t,shape);
            }
            int width=N-2;var mask=new byte[width*width];var edges=new byte[width*4];
            for(int j=0;j<width;j++){
                Array.Copy(flags,(j+1)*N+1,mask,j*width,width);
                edges[j]=flags[(j+1)*N];edges[width+j]=flags[(j+1)*N+N-1];
                edges[2*width+j]=flags[j+1];edges[3*width+j]=flags[(N-1)*N+j+1];
            }
            File.WriteAllBytes(key+".mask",mask);File.WriteAllBytes(key+".edges",edges);
            int cx=(int)((X-ex)*S/DX),cy=(int)((Y-ey)*S/DY);
            var nearby=new HashSet<int>();
            for(int y=(cy-1)/S*S+1;y<((cy-1)/S+1)*S+1;y++)for(int x=(cx-1)/S*S+1;x<((cx-1)/S+1)*S+1;x++)
                if(owner[y*N+x]>0)nearby.Add(owner[y*N+x]);
            File.WriteAllText(key+".near",string.Join(",",nearby));
            report.WriteLine(key+"\t"+string.Join(",",endpointHits.OrderBy(i=>i)));report.Flush();
        }
    }
}
