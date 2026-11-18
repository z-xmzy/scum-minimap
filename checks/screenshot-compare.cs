using System;using System.IO;using System.Linq;using System.Globalization;using System.Drawing;using System.Reflection;
class Compare{
 static void Main(string[] args){
 CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));
 var flags=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Public|BindingFlags.Static;
 var nav=asm.GetType("ScumMiniMap.NavigationRouter");nav.GetMethod("Initialize",flags).Invoke(null,null);var gr=nav.GetField("gridInstance",flags).GetValue(null);var gt=gr.GetType();
 var modes=asm.GetType("ScumMiniMap.RoadRouteMode");
 using(var f=new StreamWriter(args[2])) foreach(var line in File.ReadLines(args[1])){
 var c=line.Split('\t');var p=new PointF(float.Parse(c[3]),float.Parse(c[4]));var q=new PointF(float.Parse(c[5]),float.Parse(c[6]));var mode=Enum.Parse(modes,c[2]);
 foreach(string name in new[]{"FindBaseRoute","FindUnlayered","FindAtHeight"}){
 var a=name=="FindBaseRoute"?new object[]{p,q,mode}:name=="FindUnlayered"?new object[]{p,q,mode,null}:new object[]{p,q,mode,null,null,null};
 var r=gt.GetMethod(name,flags).Invoke(gr,a);var t=r.GetType();var pts=(PointF[])t.GetField("Polyline").GetValue(r);
 f.WriteLine(c[0]+"\t"+c[2]+"\t"+name+"\t"+t.GetField("Success").GetValue(r)+"\t"+t.GetField("TotalDistanceMeters").GetValue(r)+"\t"+string.Join(";",pts.Select(v=>v.X.ToString("R")+","+v.Y.ToString("R"))));
 }} }
}
