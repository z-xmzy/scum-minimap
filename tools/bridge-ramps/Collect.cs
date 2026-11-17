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
public class Collect {
 const int W=15216;byte[] near;
 AssetCatalog cat;Cue4ParseLevelReader reader;Dictionary<string,MeshData> cache=new();
 StreamWriter list,issues,tips;BinaryWriter cells;
 public Collect(AssetCatalog c,string folder) {
 near=File.ReadAllBytes(Path.Combine(folder,"near.bin"));cat=c;reader=new(c,new Cue4ParseLevelReaderOptions{ReadInstances=true,ExpandChildActors=true,ExpandBlueprintComponents=true});
 tips=File.CreateText(Path.Combine(folder,"geometry.tsv"));list=File.CreateText(Path.Combine(folder,"placements.tsv"));cells=new(File.Create(Path.Combine(folder,"cells.bin")));issues=File.CreateText(Path.Combine(folder,"reader-issues.txt"));
 }
 bool Relevant(string path){if(path==null)return false;var p=path.ToLowerInvariant();return !p.Contains("_wm.") && !p.Contains("wall") && !p.Contains("rvt") && (p.Contains("ramp") || p.EndsWith(".sm_track") || (p.Contains("motocross_track/") && (p.EndsWith(".platform_01")||p.EndsWith(".platform_01_end")||p.EndsWith(".platform_02"))));}
 void Place(string scene,string actor,string name,string path,FTransform t,MeshData mesh) {
 var n=path.Substring(path.LastIndexOf('/')+1).ToLowerInvariant();
 var touched=new Dictionary<(int cell,int band),float>();var vertices=new HashSet<(double x,double y)>();
 bool keep=!(new[]{"fence","cable","pillar","railing","support","traffic_light","cement_curb","wire","barrier","_sign","_fn.","lamp","_rvt."}.Any(n.Contains));
 if(keep&&mesh!=null){
 var v=mesh.Positions;var pts=new FVector[v.Length/3];for(int i=0;i<pts.Length;i++)pts[i]=t.TransformPosition(new(v[i*3],v[i*3+1],v[i*3+2]));
 foreach(var sec in mesh.Sections){
 var mat=(sec.MaterialName??"").ToLowerInvariant();if(new[]{"wire","fence","glass"}.Any(mat.Contains))continue;
 for(int j=sec.FirstIndex;j<sec.FirstIndex+sec.IndexCount;j+=3){
 var a=pts[mesh.Indices[j]];var b=pts[mesh.Indices[j+1]];var c=pts[mesh.Indices[j+2]];
 if(n.Contains("sm_dam_main_01")&&(Math.Min(a.Z,Math.Min(b.Z,c.Z))<71050||Math.Max(a.Z,Math.Max(b.Z,c.Z))>71200))continue;
 double x=(617718-a.X)*W/1521618.0-.5,y=(618618-a.Y)*W/1523618.0-.5;
 double u=(a.X-b.X)*W/1521618.0,vv=(a.Y-b.Y)*W/1523618.0,e=(a.X-c.X)*W/1521618.0,f=(a.Y-c.Y)*W/1523618.0,det=u*f-vv*e;if(det>=-1e-8)continue;
 double gx=((b.Z-a.Z)*f-(c.Z-a.Z)*vv)/det/100,gy=(u*(c.Z-a.Z)-e*(b.Z-a.Z))/det/100;if(gx*gx+gy*gy>.20)continue;
 vertices.Add((x,y));vertices.Add((x+u,y+vv));vertices.Add((x+e,y+f));
 for(int yy=Math.Max(0,(int)Math.Ceiling(y+Math.Min(0,Math.Min(vv,f))));yy<=Math.Min(W-1,(int)Math.Floor(y+Math.Max(0,Math.Max(vv,f))));yy++)
 for(int xx=Math.Max(0,(int)Math.Ceiling(x+Math.Min(0,Math.Min(u,e))));xx<=Math.Min(W-1,(int)Math.Floor(x+Math.Max(0,Math.Max(u,e))));xx++){
 if(near[xx/4+yy/4*(W/4)]==0)continue;double s=((xx-x)*f-(yy-y)*e)/det,h=(u*(yy-y)-vv*(xx-x))/det;if(s<0||h<0||s+h>1)continue;int key=yy*W+xx;float z=(float)(a.Z+s*(b.Z-a.Z)+h*(c.Z-a.Z));var surface=(key,(int)Math.Floor(z/15));if(!touched.TryGetValue(surface,out var old)||old<z)touched[surface]=z;
 }
 }}}
 if(touched.Count==0)return; if(vertices.Count<2)tips.WriteLine("-");else{
 double x=vertices.Average(v=>v.x),y=vertices.Average(v=>v.y);
 double xx=vertices.Sum(v=>(v.x-x)*(v.x-x)),yy=vertices.Sum(v=>(v.y-y)*(v.y-y)),xy=vertices.Sum(v=>(v.x-x)*(v.y-y));
 double angle=Math.Atan2(2*xy,xx-yy)/2,dx=Math.Cos(angle),dy=Math.Sin(angle);
 var ordered=vertices.OrderBy(v=>v.x*dx+v.y*dy).ToArray();
 var lo=ordered[0];var hi=ordered[^1];double span=(hi.x-lo.x)*dx+(hi.y-lo.y)*dy;
 var l=ordered.Where(v=>(v.x-lo.x)*dx+(v.y-lo.y)*dy<=.08*span).ToArray();
 var h=ordered.Where(v=>(hi.x-v.x)*dx+(hi.y-v.y)*dy<=.08*span).ToArray();
 tips.WriteLine(string.Join("\t",l.Average(v=>v.x),l.Average(v=>v.y),h.Average(v=>v.x),h.Average(v=>v.y)));
 }
 var p=t.TransformPosition(new(0,0,0));list.WriteLine(string.Join("\t",scene,actor,name,path,p.X,p.Y,p.Z,touched.Count));
 foreach(var item in touched){cells.Write(item.Key.cell);cells.Write(item.Value);}
 }
 MeshData Mesh(string path){if(!cache.TryGetValue(path,out var m)){m=MeshExtractor.Extract(cat.LoadObject(path),0,false);cache[path]=m;}return m;}
 public void Read(string scene){
 var doc=LevelDocument.Load(reader,scene);
 foreach(var a in doc.Actors){
 foreach(var c in a.Components){var p=c.StaticMeshPath;if(!Relevant(p)||c.IsInstanced)continue;try{var mesh=Mesh(p);if(c.SplineMesh!=null)mesh=SplineMeshDeformer.Deform(mesh,c.SplineMesh);Place(scene,a.Name,c.Name,p,c.WorldTransform,mesh);}catch(Exception ex){issues.WriteLine(scene+"\t"+p+"\t"+ex.Message);Place(scene,a.Name,c.Name,p,c.WorldTransform,null);}}
 foreach(var i in a.InstanceTransforms){var p=i.StaticMeshPath;if(!Relevant(p))continue;try{Place(scene,a.Name,"instance",p,i.WorldTransform,Mesh(p));}catch(Exception ex){issues.WriteLine(scene+"\t"+p+"\t"+ex.Message);Place(scene,a.Name,"instance",p,i.WorldTransform,null);}}
 }
 foreach(var warning in doc.Warnings)issues.WriteLine(scene+"\t"+warning);
 list.Flush();cells.Flush();issues.Flush();
 }
 public void Close(){tips.Dispose();list.Dispose();cells.Dispose();issues.Dispose();}
}