using System;
using System.IO;
using System.Linq;
using System.Globalization;
using ScumStudio.Assets.Catalog;
using ScumStudio.Assets.Meshes;

public static class DetailedStructures {
    public static void Export(AssetCatalog catalog, string bake) {
        string dir=Path.Combine(bake,"detailed");
        Directory.CreateDirectory(dir);
        var models=File.ReadLines(Path.Combine(bake,"models.tsv")).Select(l=>l.Split('\t')).GroupBy(r=>r[0]);
        using var log=new StreamWriter(Path.Combine(bake,"detailed-models.tsv"));
        foreach(var rows in models) {
            var r=rows.First();if(r[1]!="building" && r[1]!="barrier")continue;
            float Size(string[] row,int i)=>float.Parse(row[i],CultureInfo.InvariantCulture);
            float largest=rows.Max(t=>Math.Max(Size(t,8),Math.Max(Size(t,9),Size(t,10))));
            bool complex=r[4]=="complex-bounds";
            if(complex ? Math.Max(Size(r,8),Size(r,9))<5000 : largest<50000)continue;
            var obj=catalog.LoadObject(r[2]);
            var bounds=MeshExtractor.Describe(obj).Bounds;
            var size=bounds.Max-bounds.Min;
            float visual=Math.Max(size.X,Math.Max(size.Y,size.Z));
            if(!complex && largest<=2*visual)continue;
            var mesh=MeshExtractor.Extract(obj,0,false);
            if(mesh.Indices.Length==0 || mesh.Indices.Length%3!=0)throw new InvalidDataException(r[2]);
            using var w=new BinaryWriter(File.Create(Path.Combine(dir,r[0]+".bin")));
            foreach(int idx in mesh.Indices) {
                w.Write(mesh.Positions[3*idx]);w.Write(mesh.Positions[3*idx+1]);w.Write(mesh.Positions[3*idx+2]);
            }
            log.WriteLine(string.Join("\t",r[0],r[2],largest,visual,mesh.Indices.Length/3));
        }
    }
}
