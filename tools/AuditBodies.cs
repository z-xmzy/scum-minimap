using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.PhysicsEngine;
using ScumStudio.Core.Mathematics;
using ScumStudio.Assets.Catalog;
using ScumStudio.Assets.Meshes;
public static class AuditBodies {
    static FVector V(CUE4Parse.UE4.Objects.Core.Math.FVector v)=>new(v.X,v.Y,v.Z);
    static FQuat Q(CUE4Parse.UE4.Objects.Core.Math.FRotator r)=>new FRotator(r.Pitch,r.Yaw,r.Roll).Quaternion();
    static void Point(BinaryWriter w,FVector v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
    static void Hull(BinaryWriter w,IEnumerable<FVector> points){var a=points.ToArray();w.Write(1);w.Write(a.Length);foreach(var v in a)Point(w,v);}
    static IEnumerable<FVector> Box(FVector c,FQuat q,float x,float y,float z){
        var t=new FTransform(q,c,FVector.One);
        foreach(int i in new[]{-1,1})foreach(int j in new[]{-1,1})foreach(int k in new[]{-1,1})yield return t.TransformPosition(new FVector(i*x/2,j*y/2,k*z/2));
    }
    public static void Export(AssetCatalog catalog,string bake){
        var dir=Path.Combine(bake,"audit");
        var ids=File.ReadAllLines(Path.Combine(dir,"model-ids.txt")).ToHashSet();
        foreach(var file in Directory.GetFiles(dir,"*.props"))using(var rd=new BinaryReader(File.OpenRead(file))){
            while(rd.BaseStream.Position<rd.BaseStream.Length){ids.Add(rd.ReadInt32().ToString());rd.BaseStream.Seek(40,SeekOrigin.Current);}
        }
        foreach(var group in File.ReadLines(Path.Combine(bake,"models.tsv")).Concat(File.ReadLines(Path.Combine(dir,"props.tsv"))).Select(x=>x.Split('\t')).Where(x=>ids.Contains(x[0])).GroupBy(x=>x[0])){
            var r=group.First();var mesh=(UStaticMesh)catalog.LoadObject(r[2]);using var w=new BinaryWriter(File.Create(Path.Combine(dir,r[0]+".raw")));
            if(!mesh.BodySetup.TryLoad<UBodySetup>(out var setup))throw new Exception(r[2]);
            var info=MeshCollision.Read(mesh);
            if(info.TraceFlag.Contains("ComplexAsSimple",StringComparison.OrdinalIgnoreCase)||File.Exists(Path.Combine(bake,"detailed",r[0]+".bin"))){
                var m=MeshExtractor.Extract(mesh,0,false);w.Write(2);w.Write(m.Indices.Length);
                foreach(int i in m.Indices)Point(w,new FVector(m.Positions[3*i],m.Positions[3*i+1],m.Positions[3*i+2]));continue;
            }
            var g=setup.AggGeom;
            foreach(var box in g.BoxElems??[])Hull(w,Box(V(box.Center),Q(box.Rotation),box.X,box.Y,box.Z));
            foreach(var c in g.ConvexElems??[]){
                if(c.VertexData==null || c.VertexData.Length<4)throw new Exception("Missing hull "+r[2]);
                var t=c.Transform;var s=V(t.Scale3D);
                var tr=new FTransform(new FQuat(t.Rotation.X,t.Rotation.Y,t.Rotation.Z,t.Rotation.W),V(t.Translation),s.X==0&&s.Y==0&&s.Z==0?FVector.One:s);
                Hull(w,c.VertexData.Select(v=>tr.TransformPosition(V(v))));
            }
            void Round(FVector a,FVector b,float radius){w.Write(3);w.Write(3);Point(w,a);Point(w,b);Point(w,new FVector(radius,0,0));}
            foreach(var c in g.SphylElems??[]){var t=new FTransform(Q(c.Rotation),V(c.Center),FVector.One);Round(t.TransformPosition(new FVector(0,0,-c.Length/2)),t.TransformPosition(new FVector(0,0,c.Length/2)),c.Radius);}
            foreach(var c in g.SphereElems??[])Round(V(c.Center),V(c.Center),c.Radius);
            if(w.BaseStream.Length==0)throw new Exception("Empty body: "+r[2]);
        }
    }
}
