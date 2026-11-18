using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Linq;
using System.Globalization;
class VoiceProbe {
    static string Points(object route, string field) {
        var type=route.GetType();
        var points=(PointF[])type.GetField("Polyline").GetValue(route);
        var indices=(int[])type.GetField(field).GetValue(route);
        return string.Join(";",indices.Select(i=>points[i].X.ToString("R")+","+points[i].Y.ToString("R")));
    }
    static void Main(string[] args) {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var router=assembly.GetType("ScumMiniMap.NavigationRouter");
        router.GetMethod("Initialize",flags).Invoke(null,null);
        var grid=router.GetField("gridInstance",flags).GetValue(null);
        var raw=grid.GetType().GetMethod("FindUnlayered",BindingFlags.Instance|BindingFlags.NonPublic);
        var find=router.GetMethod("FindAtHeight",flags);
        var mode=Enum.Parse(assembly.GetType("ScumMiniMap.RoadRouteMode"),"Quickest");
        using(var output=new StreamWriter(args[2],false,new System.Text.UTF8Encoding(false))) {
            output.AutoFlush=true;
            foreach(string row in File.ReadLines(args[1])) {
                var c=row.Split('\t');
                var a=new PointF(float.Parse(c[3]),float.Parse(c[4]));
                var b=new PointF(float.Parse(c[5]),float.Parse(c[6]));
                var before=raw.Invoke(grid,new object[]{a,b,mode,null});
                var route=find.Invoke(null,new object[]{a,b,mode,null,c.Length>7?(object)double.Parse(c[7]):null,c.Length>8?(object)double.Parse(c[8]):null});
                var t=route.GetType();
                var points=(PointF[])t.GetField("Polyline").GetValue(route);
                var layers=(int[])t.GetField("BridgeLayers",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(route);
                output.WriteLine(row+"\t"+t.GetField("Success").GetValue(route)+"\t"+t.GetField("TotalDistanceMeters").GetValue(route)+"\t"+Points(before,"JunctionIndices")+"\t"+Points(route,"JunctionIndices")+"\t"+string.Join(";",points.Select((p,i)=>p.X.ToString("R")+","+p.Y.ToString("R")+","+(layers==null?0:layers[i]))));
            }
        }
    }
}