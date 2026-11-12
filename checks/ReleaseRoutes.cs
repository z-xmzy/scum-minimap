using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
class ReleaseRoutes {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
 static int Main(string[] args){
    CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
    var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));
    var nav=asm.GetType("ScumMiniMap.NavigationRouter");var mode=asm.GetType("ScumMiniMap.RoadRouteMode");
    asm.GetType("ScumMiniMap.NavTrails").GetField("LearningEnabled",F).SetValue(null,false);
    nav.GetMethod("Initialize",F).Invoke(null,null);
    var grid=nav.GetField("gridInstance",F).GetValue(null);
    var worker=grid.GetType().GetField("fineSession",F);
    var find=nav.GetMethod("FindRoute",F,null,new[]{typeof(PointF),typeof(PointF),mode},null);
    using(var output=new StreamWriter(args[2])){
      foreach(var ln in File.ReadLines(args[1])){
        var c=ln.Split(',');var a=new PointF(float.Parse(c[2]),float.Parse(c[3]));var b=new PointF(float.Parse(c[4]),float.Parse(c[5]));
        var watch=Stopwatch.StartNew();var route=find.Invoke(null,new object[]{a,b,Enum.Parse(mode,c[1])});watch.Stop();
        var type=route.GetType();Func<string,object> field=n=>type.GetField(n,F).GetValue(route);
        var path=(PointF[])field("Polyline");var cues=(int[])field("JunctionIndices");bool ok=(bool)field("Success");
        if(ok&&(path.Length==0||cues.Any(i=>i<=0||i>=path.Length-1)||
          !((double)field("EstimatedSeconds")>=0)||Math.Abs((double)field("TotalDistanceMeters")-(double)field("RoadDistanceMeters")-(double)field("UnknownDistanceMeters"))>.01))throw new Exception("route metadata "+c[0]);
        output.WriteLine(string.Join("\t",c[0],c[1],ok,watch.Elapsed.TotalMilliseconds.ToString("F2"),worker.GetValue(grid)!=null,cues.Length,
          string.Join(";",path.Select(p=>((double)p.X).ToString("R")+","+((double)p.Y).ToString("R")))));output.Flush();
      }
    }
    return 0;
 }
}

