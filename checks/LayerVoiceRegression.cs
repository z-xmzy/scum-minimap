using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
class LayerVoiceRegression {
    static BindingFlags fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static BindingFlags statics=BindingFlags.Static|BindingFlags.NonPublic;
    static int failed,passed;
    static FieldInfo Field(object item,string name){return item.GetType().GetField(name,fields);}
    static void Set(object item,string name,object value){Field(item,name).SetValue(item,value);}
    static void Check(bool good,string name){Console.WriteLine((good?"PASS ":"FAIL ")+name);if(good)passed++;else failed++;}
    static int Main(string[] args) {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var navigation=asm.GetType("ScumMiniMap.NavigationRouter");
        navigation.GetMethod("Initialize",statics).Invoke(null,null);
        var grid=navigation.GetField("gridInstance",statics).GetValue(null);
        var levels=(Array)Field(grid,"levels").GetValue(grid);
        object fine=levels.Cast<object>().Where(v=>v!=null).OrderByDescending(v=>(int)Field(v,"Width").GetValue(v)).First();
        int width=(int)Field(fine,"Width").GetValue(fine);
        var data=(byte[])Field(fine,"Data").GetValue(fine);
        var terrain=(byte[])Field(grid,"terrainFine").GetValue(grid);
        var bridge=Field(grid,"bridges").GetValue(grid);
        var ground=(IDictionary)Field(bridge,"Ground").GetValue(bridge);
        var groundType=ground.GetType().GetGenericArguments()[1];
        foreach(string name in new[]{"ByCell","Entrances","UnderRoads","MarkedDeck"})((IDictionary)Field(bridge,name).GetValue(bridge)).Clear();
        ground.Clear();
        int cx=2000,cy=2000;
        var mode=Enum.Parse(asm.GetType("ScumMiniMap.RoadRouteMode"),"Quickest");
        var find=navigation.GetMethod("FindAtHeight",statics);
        var build=grid.GetType().GetMethod("BuildJunctionIndices",fields,null,new[]{typeof(List<PointF>),typeof(bool),typeof(bool)},null);
        for(int fixture=0;fixture<3;fixture++) {
            for(int y=-60;y<=60;y++)for(int x=-60;x<=60;x++) {
                int cell=(cx+x)+(cy+y)*width;
                bool horizontal=Math.Abs(y)<=1&&x>=-26&&x<=26;
                bool vertical=Math.Abs(x)<=1&&y>=0&&y<=26;
                bool lower=fixture==1?(horizontal&&x<=1)||vertical:horizontal||vertical;
                byte cost=lower?(byte)1:(byte)255;
                data[cell]=(horizontal||vertical)?(byte)1:(byte)255;
                terrain[cell]=0;
                object value=Activator.CreateInstance(groundType);
                Set(value,"Cost",cost);Set(value,"Class",(byte)0);Set(value,"Height",10000f);Set(value,"Road",lower);
                ground[cell]=value;
            }
            var from=new PointF((cx-23+.5f)/width,(cy+.5f)/width);
            var to=fixture==2?new PointF((cx+23+.5f)/width,(cy+.5f)/width):new PointF((cx+.5f)/width,(cy+23+.5f)/width);
            var route=find.Invoke(null,new object[]{from,to,mode,null,10090d,10000d});
            var pts=(PointF[])Field(route,"Polyline").GetValue(route);
            var cues=(int[])Field(route,"JunctionIndices").GetValue(route);
            string label=new[]{"same-level T turn","bend with other-level projected road","straight through T"}[fixture];
            Check((bool)Field(route,"Success").GetValue(route),label+" route");
            if(build!=null){
                var copy=new List<PointF>(pts);
                var projected=(int[])build.Invoke(grid,new object[]{copy,false,false});
                Console.WriteLine("projected detector "+label+" "+projected.Length);
            }
            Check(fixture==0?cues.Length>0:cues.Length==0,label+" cues");
            var voice=Activator.CreateInstance(asm.GetType("ScumMiniMap.VoiceNavigator"),true);
            var update=voice.GetType().GetMethod("Update",fields);
            int turns=0,step=0;var actions=new List<string>();
            for(int i=1;i<pts.Length;i++){
                double length=Math.Sqrt(Math.Pow((pts[i].X-pts[i-1].X)*15216.18,2)+Math.Pow((pts[i].Y-pts[i-1].Y)*15236.18,2));
                int samples=Math.Max(1,(int)Math.Ceiling(length/2));
                for(int n=0;n<samples;n++){
                    float t=(float)n/samples;
                    var at=new PointF(pts[i-1].X+(pts[i].X-pts[i-1].X)*t,pts[i-1].Y+(pts[i].Y-pts[i-1].Y)*t);
                    var clips=(string[])update.Invoke(voice,new object[]{route,at,new DateTime(2026,11,19).AddSeconds(++step),false});
                    if(clips!=null)actions.AddRange(clips);
                }
            }
            var clipList=(string[])asm.GetType("ScumMiniMap.VoicePacks").GetField("Clips",statics|BindingFlags.Public).GetValue(null);
            turns=actions.Count(s=>s==clipList[5]||s==clipList[6]||s==clipList[9]);
            Check(fixture==0?turns>0:turns==0,label+" replay");
        }
        Console.WriteLine(passed+" passed; "+failed+" failed");
        return failed==0?0:1;
    }
}