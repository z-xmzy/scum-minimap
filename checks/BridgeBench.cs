using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Globalization;
using System.Diagnostics;
class BridgeBench {
    static void Main(string[] args) {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var t=asm.GetType("ScumMiniMap.NavigationRouter");
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var init=Stopwatch.StartNew();
        t.GetMethod("Initialize",flags).Invoke(null,null);
        init.Stop();
        if(!(bool)t.GetProperty("Ready",flags).GetValue(null,null))throw new Exception("Not initialized");
        var mode=asm.GetType("ScumMiniMap.RoadRouteMode");
        var call=t.GetMethod("FindCurrentRoute",flags);
        using(var writer=new StreamWriter(args[2],false,new System.Text.UTF8Encoding(false))) {
            writer.WriteLine("initialization_ms\t"+init.Elapsed.TotalMilliseconds);
            foreach(string row in File.ReadAllLines(args[1])) {
                string[] f=row.Split('\t');
                var a=new PointF(float.Parse(f[3]),float.Parse(f[4]));
                var b=new PointF(float.Parse(f[5]),float.Parse(f[6]));
                var watch=Stopwatch.StartNew();
                var route=call.Invoke(null,new object[]{a,b,Enum.Parse(mode,f[2]),null});
                watch.Stop();
                writer.WriteLine(f[0]+"\t"+f[1]+"\t"+f[2]+"\t"+route.GetType().GetField("Success").GetValue(route)+"\t"+watch.Elapsed.TotalMilliseconds);
                writer.Flush();
            }
        }
    }
}