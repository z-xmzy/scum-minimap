using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using ScumStudio.Assets.Catalog;
using ScumStudio.Assets.Meshes;
using ScumStudio.Core.Mathematics;
using ScumStudio.Level.Model;
using ScumStudio.Level.Reading;
public sealed class WorldObjects {
    readonly AssetCatalog catalog;
    readonly Cue4ParseLevelReader reader;
    readonly Dictionary<string,int> ids = new(StringComparer.OrdinalIgnoreCase);
    readonly string folder;
    readonly StreamWriter models;
    readonly StreamWriter issues;
    public WorldObjects(AssetCatalog catalog,string folder) {
        this.folder=folder;
        this.catalog=catalog;
        reader=new Cue4ParseLevelReader(catalog,new Cue4ParseLevelReaderOptions {ExpandChildActors=false,ExpandBlueprintComponents=false});
        models=new StreamWriter(Path.Combine(folder,"models.tsv"));
        issues=new StreamWriter(Path.Combine(folder,"issues.tsv"));
    }
    static bool Ignore(string profile) {
        return profile!=null && (profile.Equals("NoCollision",StringComparison.OrdinalIgnoreCase) || profile.Contains("Overlap",StringComparison.OrdinalIgnoreCase) || profile.Contains("Trigger",StringComparison.OrdinalIgnoreCase));
    }
    static string Kind(string path) {
        if(path==null)return null;
        string n=path.Substring(path.LastIndexOf('/')+1).ToLowerInvariant();
        if(n.Contains("bridge")||n.Contains("dam_main")||n.Contains("water")||n.Contains("decal"))return null;
        if(n.Contains("rock")||n.Contains("boulder"))return "rock";
        if(n.Contains("stump")||n.Contains("fallen_tree")||n.Contains("debris_large")||n.Contains("log_pile"))return "wood";
        if(path.Contains("/Trees/",StringComparison.OrdinalIgnoreCase))return "tree";
        if(n.Contains("barrier")||n.Contains("electrical_pole")||n.Contains("wooden_post")||n.Contains("anti_tank_obstacle")||n.Contains("bollard")||n.Contains("fence"))return "barrier";
        if(path.Contains("/Buildings/",StringComparison.OrdinalIgnoreCase)
          && !n.Contains("floor")&&!n.Contains("foundation")&&!n.Contains("stairs")&&!n.Contains("roof")&&!n.Contains("tunnel")&&!n.Contains("ramp"))return "building";
        return null;
    }
    int Model(string path,string kind,string profile) {
        string key=path+"|"+profile;
        if(ids.TryGetValue(key,out int id))return id;
        id=-1;
        if(!Ignore(profile)&&catalog.LoadObject(path) is UStaticMesh mesh) {
            var col=MeshCollision.Read(mesh);
            var effective=string.IsNullOrEmpty(profile)?col?.DefaultProfile:profile;
            if(col!=null && !Ignore(effective) && (effective==null||!effective.Contains("Foliage",StringComparison.OrdinalIgnoreCase))) {
                var shapes=col.Boxes.ToList();
                string shapeSource="collision";
                if(shapes.Count==0 && col.TraceFlag.Contains("ComplexAsSimple",StringComparison.OrdinalIgnoreCase)) {
                    var b=MeshExtractor.Describe(mesh).Bounds;
                    var c=(b.Min+b.Max)*0.5f;var s=b.Max-b.Min;
                    shapes.Add(new CollisionBox(new FVector(c.X,c.Y,c.Z),FRotator.Zero,new FVector(s.X,s.Y,s.Z)));
                    shapeSource="complex-bounds";
                }
                if(shapes.Count>0) {
                    id=ids.Count;
                    foreach(var box in shapes) {
                        var q=box.Rotation.Quaternion();
                        models.WriteLine(string.Join("\t",new object[]{id,kind,path,effective,shapeSource,box.Center.X,box.Center.Y,box.Center.Z,box.Size.X,box.Size.Y,box.Size.Z,q.X,q.Y,q.Z,q.W}));
                    }
                }
            }
        }
        ids.Add(key,id);return id;
    }
    void Place(BinaryWriter w,string path,string profile,FTransform tr) {
        string kind=Kind(path);
        if(kind==null||Math.Abs(tr.Scale3D.X*tr.Scale3D.Y*tr.Scale3D.Z)<1e-8)return;
        int id=Model(path,kind,profile);
        if(id<0)return;
        w.Write(id);
        foreach(var v in new[]{tr.Translation.X,tr.Translation.Y,tr.Translation.Z,tr.Rotation.X,tr.Rotation.Y,tr.Rotation.Z,tr.Rotation.W,tr.Scale3D.X,tr.Scale3D.Y,tr.Scale3D.Z})w.Write(v);
    }
    public void Export(string path,int index) {
        string file=Path.Combine(folder,"tiles",index.ToString("D4")+".bin");
        var doc=LevelDocument.Load(reader,path);
        using(var w=new BinaryWriter(File.Create(file+".part"))) {
            foreach(var a in doc.Actors) {
                var components=a.Components.ToDictionary(c=>c.ExportIndex,c=>c);
                foreach(var c in a.Components)if(!c.IsInstanced && c.SplineMesh==null)Place(w,c.StaticMeshPath,c.CollisionProfile,c.WorldTransform);
                foreach(var i in a.InstanceTransforms)if(components.TryGetValue(i.ComponentExportIndex,out var c))Place(w,i.StaticMeshPath,c.CollisionProfile,i.WorldTransform);
            }
        }
        File.Move(file+".part",file,true);
        if(doc.Warnings.Count>0)issues.WriteLine(path+"\t"+doc.Warnings.Count+"\t"+doc.Warnings[0]);
        models.Flush();issues.Flush();
    }
    public void Close(){models.Dispose();issues.Dispose();}
}
