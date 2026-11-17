using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Drawing;
using System.Reflection;
class BridgeProbe {
 static void Main(string[] args){
 CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
 var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=asm.GetType("ScumMiniMap.NavigationRouter");var f=BindingFlags.NonPublic|BindingFlags.Static;
 type.GetMethod("Initialize",f).Invoke(null,null);if(!(bool)type.GetProperty("Ready",f).GetValue(null,null))throw new Exception("World routing unavailable");
 var m=asm.GetType("ScumMiniMap.RoadRouteMode");var call=type.GetMethod("FindAtHeight",f);
 using(var outp=new StreamWriter(args[2],false,new System.Text.UTF8Encoding(false))){outp.AutoFlush=true;
 foreach(var line in File.ReadLines(args[1])){
 var c=line.Split('\t');var p=new PointF(float.Parse(c[3]),float.Parse(c[4]));var q=new PointF(float.Parse(c[5]),float.Parse(c[6]));
 var route=call.Invoke(null,new object[]{p,q,Enum.Parse(m,c[2]),null,c.Length>7?(object)double.Parse(c[7]):null,c.Length>8?(object)double.Parse(c[8]):null});var t=route.GetType();var pts=(PointF[])t.GetField("Polyline").GetValue(route);
 var groups=(int[])t.GetField("BridgeLayers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(route);
 outp.WriteLine(line+"\t"+t.GetField("Success").GetValue(route)+"\t"+t.GetField("TotalDistanceMeters").GetValue(route)+"\t"+string.Join(";",pts.Select((v,i)=>v.X.ToString("R")+","+v.Y.ToString("R")+","+(groups==null?0:groups[i]))));
 }}}
}