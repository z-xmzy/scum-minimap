using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ScumStudio.Assets.Catalog;
using ScumStudio.Assets.Meshes;
using ScumStudio.Core.Geometry;
using ScumStudio.Core.Mathematics;
using ScumStudio.Level.Model;
using ScumStudio.Level.Reading;
public class WorldSurfaces {
    const int W=3804;
    readonly AssetCatalog catalog;
    readonly Cue4ParseLevelReader reader;
    readonly float[][] levels=new[]{new float[W*W],new float[W*W],new float[W*W]};
    readonly Dictionary<string,MeshData> cache=new();
    readonly Dictionary<string,int> used=new();
    public WorldSurfaces(AssetCatalog c) {
        catalog=c;reader=new Cue4ParseLevelReader(c,new Cue4ParseLevelReaderOptions{ReadInstances=false,ExpandChildActors=false,ExpandBlueprintComponents=false});
        foreach(var a in levels)Array.Fill(a,float.NegativeInfinity);
    }
    static int Kind(string path) {
        string n=path.ToLowerInvariant();
        if(n.Contains("_fn.")||n.Contains("riverbank")||n.Contains("_rvt.")||n.Contains("puddle")||n.Contains("fence")||n.Contains("cable")||n.Contains("pillar")||n.Contains("railing")||n.Contains("support"))return -1;
        if(n.Contains("lakewatersurface")||n.Contains("sm_waterplane_")||n.Contains("/rivers/")&&!n.Contains("_es.")||n.Contains("/lake/watersurface."))return 0;
        if(n.Contains("/road/")&&n.Contains("bridge")||n.EndsWith(".sm_dam_main_01"))return 1;
        if((n.Contains("/road/")||n.Contains("/roads/")) && (n.Contains("asphalt")||n.Contains("dirtroad")||n.Contains("gravel")||n.Contains("road_segment")))return 2;
        return -1;
    }
    void Tri(float[] dst,FVector a,FVector b,FVector c,int kind) {
        double ax=(617718-a.X)*W/1521618.0-.5,ay=(618618-a.Y)*W/1523618.0-.5;
        double bx=(617718-b.X)*W/1521618.0-.5,by=(618618-b.Y)*W/1523618.0-.5;
        double cx=(617718-c.X)*W/1521618.0-.5,cy=(618618-c.Y)*W/1523618.0-.5;
        double ux=bx-ax,uy=by-ay,vx=cx-ax,vy=cy-ay,det=ux*vy-uy*vx;
        if(Math.Abs(det)<1e-8)return;
        double sx=((b.Z-a.Z)*vy-(c.Z-a.Z)*uy)/det/400,sy=((c.Z-a.Z)*ux-(b.Z-a.Z)*vx)/det/400;
        if(kind>0 && (sx*sx+sy*sy>.20 || det>0))return;
        int x0=Math.Max(0,(int)Math.Ceiling(Math.Min(ax,Math.Min(bx,cx)))),x1=Math.Min(W-1,(int)Math.Floor(Math.Max(ax,Math.Max(bx,cx))));
        int y0=Math.Max(0,(int)Math.Ceiling(Math.Min(ay,Math.Min(by,cy)))),y1=Math.Min(W-1,(int)Math.Floor(Math.Max(ay,Math.Max(by,cy))));
        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++) {
            double u=((x-ax)*vy-(y-ay)*vx)/det,v=(ux*(y-ay)-uy*(x-ax))/det;
            if(u<0||v<0||u+v>1)continue;
            int i=y*W+x;float z=(float)(a.Z+u*(b.Z-a.Z)+v*(c.Z-a.Z));
            if(z>dst[i])dst[i]=z;
        }
    }
    public void Read(string scene) {
        var doc=LevelDocument.Load(reader,scene);
        foreach(var a in doc.Actors)foreach(var c in a.Components) {
            string p=c.StaticMeshPath;if(p==null||c.IsInstanced)continue;
            int kind=Kind(p);if(kind<0)continue;
            if(!cache.TryGetValue(p,out var mesh)) {
                mesh=MeshExtractor.Extract(catalog.LoadObject(p),0,false);cache.Add(p,mesh);
            }
            if(c.SplineMesh!=null)mesh=SplineMeshDeformer.Deform(mesh,c.SplineMesh);
            var src=mesh.Positions;var points=new FVector[src.Length/3];
            for(int i=0;i<points.Length;i++)points[i]=c.WorldTransform.TransformPosition(new FVector(src[3*i],src[3*i+1],src[3*i+2]));
            foreach(var sec in mesh.Sections) {
                string m=(sec.MaterialName??"").ToLowerInvariant();
                if(kind==1&&(m.Contains("wire")||m.Contains("metal")||m.Contains("fence")||m.Contains("glass")))continue;
                for(int j=sec.FirstIndex;j<sec.FirstIndex+sec.IndexCount;j+=3) {
                    var x=points[mesh.Indices[j]];var y=points[mesh.Indices[j+1]];var z=points[mesh.Indices[j+2]];
                    if(p.EndsWith(".SM_Dam_Main_01") && (Math.Min(x.Z,Math.Min(y.Z,z.Z))<71050||Math.Max(x.Z,Math.Max(y.Z,z.Z))>71200))continue;
                    Tri(levels[kind],x,y,z,kind);
                }
            }
            used.TryGetValue(p,out int n);used[p]=n+1;
        }
    }
    public void Save(string folder) {
        for(int i=0;i<3;i++) {
            var bytes=new byte[levels[i].Length*4];Buffer.BlockCopy(levels[i],0,bytes,0,bytes.Length);
            File.WriteAllBytes(Path.Combine(folder,new[]{"water.f32","deck.f32","roads.f32"}[i]),bytes);
        }
        File.WriteAllLines(Path.Combine(folder,"surface-models.tsv"),used.Select(p=>p.Value+"\t"+p.Key));
    }
}
