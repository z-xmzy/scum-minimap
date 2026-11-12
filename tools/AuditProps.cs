using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ScumStudio.Assets.Catalog;
using ScumStudio.Assets.Meshes;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using ScumStudio.Core.Mathematics;
using ScumStudio.Level.Model;
using ScumStudio.Level.Reading;

public class AuditProps {
    class Area {public double x,y,h;public BinaryWriter writer;}
    record Mesh(int Id,double Radius);
    readonly List<Area> areas=new();
    readonly Dictionary<(int,int),List<Area>> bins=new();
    readonly Dictionary<string,Mesh> cache=new(StringComparer.OrdinalIgnoreCase);
    readonly AssetCatalog cat;readonly Cue4ParseLevelReader reader;readonly StreamWriter log;
    readonly HashSet<string> known;
    int id=1000000;
    public AuditProps(AssetCatalog c,string bake){
        cat=c;reader=new Cue4ParseLevelReader(cat,new Cue4ParseLevelReaderOptions{ExpandChildActors=false,ExpandBlueprintComponents=false});
        var dir=Path.Combine(bake,"audit");log=new StreamWriter(Path.Combine(dir,"props.tsv"));
        known=File.ReadLines(Path.Combine(bake,"models.tsv")).Select(l=>l.Split('\t')[2]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(string line in File.ReadLines(Path.Combine(dir,"areas.tsv"))){
            var r=line.Split('\t');double n=double.Parse(r[3])/double.Parse(r[4]);
            var a=new Area{x=617718-(double.Parse(r[1])+n/2)*1521618/3804,y=618618-(double.Parse(r[2])+n/2)*1523618/3804,
                h=n/2*1523618/3804+250,writer=new BinaryWriter(File.Create(Path.Combine(dir,r[0]+".props")))};
            areas.Add(a);
            for(int x=(int)Math.Floor((a.x-35000)/25000);x<=(int)Math.Floor((a.x+35000)/25000);x++)
            for(int y=(int)Math.Floor((a.y-35000)/25000);y<=(int)Math.Floor((a.y+35000)/25000);y++){
                if(!bins.TryGetValue((x,y),out var list))bins[(x,y)]=list=new();list.Add(a);
            }
        }
    }
    static bool Ignore(string p)=>p!=null&&(p.Equals("NoCollision",StringComparison.OrdinalIgnoreCase)||p.Contains("Overlap",StringComparison.OrdinalIgnoreCase)||p.Contains("Trigger",StringComparison.OrdinalIgnoreCase)||p.Contains("Foliage",StringComparison.OrdinalIgnoreCase));
    void Place(string path,string profile,FTransform t){
        if(path==null||known.Contains(path)||Ignore(profile)||Math.Abs(t.Scale3D.X*t.Scale3D.Y*t.Scale3D.Z)<1e-9)return;
        var pos=t.Translation;
        if(!bins.TryGetValue(((int)Math.Floor(pos.X/25000),(int)Math.Floor(pos.Y/25000)),out var near))return;
        // Decks, landscape roads and water surfaces are handled by the world surface layers.
        var p=path.ToLowerInvariant();if(p.Contains("/road/")||p.Contains("/roads/")||p.Contains("/rivers/")||p.Contains("bridge")||p.Contains("waterplane")||p.Contains("watersurface")||p.Contains("decal"))return;
        string key=path+"|"+profile;
        if(!cache.TryGetValue(key,out Mesh m)){
            m=null;
            if(cat.LoadObject(path) is UStaticMesh mesh){
                var col=MeshCollision.Read(mesh);
                if(col!=null&&!Ignore(string.IsNullOrEmpty(profile)?col.DefaultProfile:profile)&&(col.Boxes.Count>0||col.TraceFlag.Contains("ComplexAsSimple"))){
                    var b=MeshExtractor.Describe(mesh).Bounds;
                    double Max(float a,float b)=>Math.Max(Math.Abs(a),Math.Abs(b));
                    m=new Mesh(id++,Math.Sqrt(Math.Pow(Max(b.Min.X,b.Max.X),2)+Math.Pow(Max(b.Min.Y,b.Max.Y),2)+Math.Pow(Max(b.Min.Z,b.Max.Z),2)));
                }
            }
            cache[key]=m;
            if(m!=null)log.WriteLine(m.Id+"\tprop\t"+path+"\t"+profile);
        }
        if(m==null)return;
        double radius=m.Radius*Math.Max(Math.Abs(t.Scale3D.X),Math.Max(Math.Abs(t.Scale3D.Y),Math.Abs(t.Scale3D.Z)));
        foreach(var a in near)if(Math.Abs(a.x-pos.X)<=radius+a.h && Math.Abs(a.y-pos.Y)<=radius+a.h){
            a.writer.Write(m.Id);foreach(float v in new[]{pos.X,pos.Y,pos.Z,t.Rotation.X,t.Rotation.Y,t.Rotation.Z,t.Rotation.W,t.Scale3D.X,t.Scale3D.Y,t.Scale3D.Z})a.writer.Write(v);
        }
    }
    public void Scene(string path){
        var d=LevelDocument.Load(reader,path);
        foreach(var a in d.Actors){var cc=a.Components.ToDictionary(v=>v.ExportIndex);
            foreach(var c in a.Components)if(!c.IsInstanced && c.SplineMesh==null)Place(c.StaticMeshPath,c.CollisionProfile,c.WorldTransform);
            foreach(var i in a.InstanceTransforms)if(cc.TryGetValue(i.ComponentExportIndex,out var c))Place(i.StaticMeshPath,c.CollisionProfile,i.WorldTransform);
        }
    }
    public void Close(){foreach(var a in areas)a.writer.Dispose();log.Dispose();}
}
