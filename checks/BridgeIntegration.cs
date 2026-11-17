using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Drawing;
class BridgeIntegration {
    static int failures,checks;
    static void Check(bool condition,string name) { checks++;Console.WriteLine((condition?"PASS ":"FAIL ")+name);if(!condition)failures++; }
    static int Main(string[] args) {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var router=asm.GetType("ScumMiniMap.NavigationRouter");
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        router.GetMethod("Initialize",flags).Invoke(null,null);
        var group=router.GetMethod("SurfaceGroup",flags);
        var same=router.GetMethod("SameSurface",flags);
        var choices=router.GetMethod("BridgeHeights",flags);
        var route=router.GetMethod("FindAtHeight",flags);
        var mode=Enum.Parse(asm.GetType("ScumMiniMap.RoadRouteMode"),"Quickest");
        foreach(string row in File.ReadLines(args[1])) {
            var c=row.Split('\t');
            if(!c[0].StartsWith("X")||!c[1].EndsWith("去")||c[2]!="Quickest")continue;
            var p=new PointF(float.Parse(c[3]),float.Parse(c[4]));
            double lower=double.Parse(c[7]),upper=double.Parse(c[8]);
            int a=(int)group.Invoke(null,new object[]{p,lower});
            int b=(int)group.Invoke(null,new object[]{p,upper});
            Check(a==0&&b>0,c[0]+" coordinates distinguish levels");
            Check(!(bool)same.Invoke(null,new object[]{p,lower,p,upper}),c[0]+" lower GPS cannot finish upper target");
            Check(!(bool)same.Invoke(null,new object[]{p,lower+100,p,upper}),c[0]+" lower GPS character offset");
            Check((bool)same.Invoke(null,new object[]{p,upper+100,p,upper}),c[0]+" upper GPS character offset");
            double[] heights=(double[])choices.Invoke(null,new object[]{p});
            Check(heights.Length==2&&heights[1]>heights[0]+180,c[0]+" context menu has distinct surfaces");
        }
        foreach(var p in new[]{new PointF(float.NaN,.5f),new PointF(-.1f,.5f),new PointF(2,.5f)}) {
            var result=route.Invoke(null,new object[]{p,new PointF(.5f,.5f),mode,null,null,null});
            Check(!(bool)result.GetType().GetField("Success").GetValue(result),"invalid map position rejected");
        }
        var example=File.ReadLines(args[1]).First(s=>s.StartsWith("E"));
        var fields=example.Split('\t');
        var start=new PointF(float.Parse(fields[3]),float.Parse(fields[4]));
        var finish=new PointF(float.Parse(fields[5]),float.Parse(fields[6]));
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var cancelled=route.Invoke(null,new object[]{start,finish,mode,new Func<bool>(()=>false),double.Parse(fields[7]),double.Parse(fields[8])});
        Check(!(bool)cancelled.GetType().GetField("Success").GetValue(cancelled),"cancelled bridge search cannot publish a route");
        Check(watch.ElapsedMilliseconds<1000,"cancelled bridge search returns promptly");
        Console.WriteLine(checks+" checks, "+failures+" failures");
        return failures==0?0:1;
    }
}