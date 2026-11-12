using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Drawing;
using System.Threading.Tasks;
class BunkerLayoutRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static int failed;
 static void Check(bool ok,string label){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(!ok)failed++;}
 [STAThread]static int Main(string[] args){object runtime=null;try{
  var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime");runtime=Activator.CreateInstance(type,F,null,new object[]{Path.GetFullPath(args[1]),true},null);var engine=Get(runtime,"engine");
  Check(engine.GetType().GetMethod("WpfHistoryForState",F)!=null,"Trail has indoor/outdoor context filtering");if(failed>0)return 1;
  var load=(Task)Get(engine,"bunkerLoad");load.Wait();var atlas=load.GetType().GetProperty("Result").GetValue(load,null);
  // Use real extracted detection surfaces for indoor samples.
  if(atlas==null)atlas=load.GetType().GetProperty("Result").GetValue(load,null);
  var floors=((IEnumerable)Get(atlas,"Floors")).Cast<object>().ToArray();var d1=floors.Where(f=>(string)Get(f,"BunkerId")=="D_1_Abandoned").ToArray();Check(d1.Length>0,"D1 detection data present");
  var floor=d1.OrderBy(f=>Math.Abs((double)Get(f,"SurfaceZ")-80110)).First();var tiles=(float[])Get(floor,"Tiles");var offsets=(int[])Get(floor,"TileOffsets");int ti=Array.FindIndex(offsets,value=>value>=0),cell=0;while(float.IsNaN(tiles[offsets[ti]+cell]))cell++;int cols=((int)Get(floor,"Width")+7)/8;
  double step=(double)Get(floor,"CellSize"),x=(double)Get(floor,"MaxX")-((ti%cols)*8+cell%8+.5)*step,y=(double)Get(floor,"MaxY")-((ti/cols)*8+cell/8+.5)*step,z=tiles[offsets[ti]+cell]+92;
  var now=DateTime.UtcNow.AddSeconds(-30);var positionType=asm.GetType("ScumMiniMap.Position");
  Action<double,double,double,int> sample=(sx,sy,sz,seconds)=>{var p=Activator.CreateInstance(positionType);Set(p,"X",sx);Set(p,"Y",sy);Set(p,"Z",sz);Set(engine,"position",p);var pt=(PointF)engine.GetType().GetMethod("ToMap",F).Invoke(null,new[]{p});Call(engine,"RecordLocationHistory",pt,now.AddSeconds(seconds));};
  Call(engine,"ClearLocationHistory");sample(0,0,100000,0);sample(1000,0,100000,2);sample(x,y,z,4);sample(x,y,z,20);sample(5000,0,100000,22);sample(6000,0,100000,24);
  var state=Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfMapState"),true);Call(engine,"WpfHistoryForState",state);var surface=(PointF[])Get(state,"Trail");var segments=(PointF[][])Get(state,"TrailSegments");
  Check(surface.Length==4,"World history excludes indoor points");Check(segments.Length==2&&segments.All(s=>s.Length==2),"Outdoor trail breaks across a bunker visit");
  var find=atlas.GetType().GetMethod("FindFloor",F);var actual=find.Invoke(atlas,new object[]{x,y,z});var view=atlas.GetType().GetMethod("ViewFor",F).Invoke(atlas,new[]{actual,(object)x,(object)y,(object)z});Set(state,"Bunker",view);Call(engine,"WpfHistoryForState",state);
  Check(((PointF[])Get(state,"Trail")).Length==2,"Indoor history contains only the matching bunker view");
  Set(state,"Bunker",null);Call(engine,"WpfHistoryForState",state);Check(ReferenceEquals(surface,Get(state,"Trail")),"Alternating indoor and outdoor views reuse their cached trails");
  var views=((IEnumerable)Get(atlas,"Views")).Cast<object>().ToArray();Check(views.Length>0,"Composite views loaded separately from detection surfaces");
  var d1Views=views.Where(v=>(string)Get(v,"BunkerId")=="D_1_Abandoned").ToArray();Check(d1Views.Length>0&&d1Views.Length<d1.Length,"D1 display is no longer eleven raw height slices");Check(d1Views.Any(v=>((string)Get(v,"DisplayName")).Contains("1"))&&d1Views.Any(v=>((string)Get(v,"DisplayName")).Contains("2")),"D1 actual levels have named views");
  var labels=d1Views.SelectMany(v=>((IEnumerable)Get(v,"Labels")).Cast<object>()).Select(l=>(string)Get(l,"Text")).ToArray();Check(labels.Any(t=>t.Contains("配电"))&&labels.Any(t=>t.Contains("杀戮"))&&labels.Any(t=>t.Contains("A区"))&&labels.Any(t=>t.Contains("B区")),"D1 has important-room and area labels");
  Check(floors.Select(f=>(string)Get(f,"BunkerId")).Distinct().All(id=>views.Any(v=>(string)Get(v,"BunkerId")==id)),"Every existing location has a composite display view");
  Func<object,IEnumerable> entryLabels=v=>(IEnumerable)Get(v,"Labels");
  var entrances=views.SelectMany(v=>entryLabels(v).Cast<object>()).Where(l=>(string)Get(l,"Kind")=="entrance").ToArray();
  Check(entrances.Length==115&&entrances.All(l=>!(bool)Get(l,"Bounded")),"115 distinct entrance pins load independently of bounded room names");
  var a2=views.Where(v=>(string)Get(v,"BunkerId")=="A_2").SelectMany(v=>entryLabels(v).Cast<object>()).Where(l=>(string)Get(l,"Kind")=="entrance").Select(l=>(string)Get(l,"Text")).ToArray();
  Check(a2.Length==2&&a2.Any(t=>t.Contains("正门"))&&a2.Any(t=>t.Contains("竖井")),"Ordinary bunker distinguishes main door and rear shaft");
  bool entryBounds=views.All(v=>entryLabels(v).Cast<object>().Where(l=>(string)Get(l,"Kind")=="entrance").All(l=>(double)Get(l,"X")>=(double)Get(v,"MinX")&&(double)Get(l,"X")<(double)Get(v,"MaxX")&&(double)Get(l,"Y")>=(double)Get(v,"MinY")&&(double)Get(l,"Y")<(double)Get(v,"MaxY")));
  Check(entryBounds,"Entrances remain within their own view viewport bounds");
  var mapType=asm.GetType("ScumMiniMap.WpfMapSurface");int boundedCount=0;bool contained=true;
  Set(state,"Labels",true);Set(state,"LabelSize",12);Set(state,"Zoom",1f);Set(state,"Saved",Array.CreateInstance(asm.GetType("ScumMiniMap.MapZone"),0));
  foreach(var v in d1Views)foreach(double width in new[]{1000.0,1800.0})foreach(double angle in new[]{0.0,45.0,90.0}) {
   double height=width*.7;Set(state,"Bunker",v);
   var centre=new PointF((float)((617718-((double)Get(v,"MinX")+(double)Get(v,"MaxX"))/2)/1521618.0),(float)((618618-((double)Get(v,"MinY")+(double)Get(v,"MaxY"))/2)/1523618.0));Set(state,"Pan",centre);
   double side=(double)mapType.GetMethod("MapSide",F).Invoke(null,new object[]{state,width,height,false});
   bool compact=angle!=0;Set(state,"BunkerScale",side/width);Set(state,"RotateMap",compact);var orientation=Activator.CreateInstance(positionType);Set(orientation,"Yaw",90-angle);Set(state,"Position",orientation);
   var placed=((IEnumerable)mapType.GetMethod("SelectLabels",F).Invoke(null,new object[]{state,width,height,compact})).Cast<object>();
   foreach(var l in placed.Where(l=>(bool)Get(l,"Bounded"))) {
    boundedCount++;var source=Get(l,"Source");var at=(System.Windows.Point)Get(l,"At");var text=(System.Windows.Media.FormattedText)mapType.GetMethod("LabelText",F).Invoke(null,new[]{Get(l,"Text"),Get(l,"FontSize"),Get(l,"Ink")});
    foreach(double px in new[]{at.X-4,at.X+Math.Min(220,text.Width)+4})foreach(double py in new[]{at.Y-1,at.Y+text.Height+1}) {
     double cosine=Math.Cos(angle*Math.PI/180),sine=Math.Sin(angle*Math.PI/180),dx=px-width/2,dy=py-height/2;
     double wx=617718-(centre.X+(dx*cosine+dy*sine)/side)*1521618.0,wy=618618-(centre.Y+(-dx*sine+dy*cosine)/side)*1523618.0;
     contained&=wx>=(double)Get(source,"MinX")-.1&&wx<=(double)Get(source,"MaxX")+.1&&wy>=(double)Get(source,"MinY")-.1&&wy<=(double)Get(source,"MaxY")+.1;
    }
   }
  }
  Check(boundedCount>=10,"Room and sector names render at usable viewport sizes");Check(contained,"Entire text stays inside room bounds at 0, 45 and 90 degree rotation");
  Console.WriteLine("Failures="+failed);return failed==0?0:1;
 }catch(Exception e){while(e.InnerException!=null)e=e.InnerException;Console.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 2;}finally{if(runtime!=null)((IDisposable)runtime).Dispose();}}
}
