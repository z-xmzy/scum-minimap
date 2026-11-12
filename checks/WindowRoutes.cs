using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
class WindowRoutes {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
 static Assembly asm;
 static object Position(double u,double v){var p=Activator.CreateInstance(asm.GetType("ScumMiniMap.Position"));Set(p,"X",617718-u*1521618);Set(p,"Y",618618-v*1523618);return p;}
 static object Zone(string name,float u,float v){var z=Activator.CreateInstance(asm.GetType("ScumMiniMap.MapZone"));Set(z,"Name",name);Set(z,"Points",new[]{new PointF(u,v)});return z;}
 static void Pump(Func<bool> until,Action validate) {
    var t=Stopwatch.StartNew();while(!until()){
        Application.DoEvents();validate();if(t.ElapsedMilliseconds>30000)throw new Exception("Window test timed out");Thread.Sleep(2);
    }
 }
 [STAThread]static int Main(string[] args){
    try{return Run(args);}catch(Exception e){while(e.InnerException!=null)e=e.InnerException;Console.WriteLine(e.GetType().Name+" "+e.Message+" "+e.StackTrace);return 1;}
 }
 static int Run(string[] args){
    asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));Directory.CreateDirectory(args[1]);
    var nav=asm.GetType("ScumMiniMap.NavigationRouter");nav.GetMethod("Initialize",F).Invoke(null,null);
    var grid=nav.GetField("gridInstance",F).GetValue(null);
    using(var form=(Form)Activator.CreateInstance(asm.GetType("ScumMiniMap.MapWindow"),new object[]{args[1],true}))
    using(var timer=new System.Windows.Forms.Timer()){
        var handle=form.Handle;int ticks=0;long max=0;var elapsed=Stopwatch.StartNew();long last=0;
        timer.Interval=16;timer.Tick+=(s,e)=>{long now=elapsed.ElapsedMilliseconds;if(ticks>0)max=Math.Max(max,now-last);last=now;ticks++;};timer.Start();
        var roadmode=asm.GetType("ScumMiniMap.RoadRouteMode");
        Set(form,"wpfRouteMode",Enum.Parse(roadmode,"PavedOnly"));
        Set(form,"position",Position(.9225814931650894,.7343585699263933));
        var good=Zone("narrow",.9060199789695058f,.7338328075709779f);
        var watch=Stopwatch.StartNew();Call(form,"WpfTarget",good);long returned=watch.ElapsedMilliseconds;
        Pump(()=>!(bool)Get(form,"routeCalculating"),()=>{});
        var route=Get(form,"activeRoute");
        if(route==null||!(bool)Get(route,"Success")||Math.Abs((double)Get(route,"TotalDistanceMeters")-332.69)>1||
            ticks<5||Get(grid,"fineSession")==null)throw new Exception("Fine route or message pump");
        Console.WriteLine("Fine success via real MapWindow, route="+Get(route,"TotalDistanceMeters")+" m, target handler="+returned+
            " ms, completion="+watch.ElapsedMilliseconds+" ms, timer callbacks="+ticks+", largest tick gap="+max+" ms");
        Set(form,"wpfRouteMode",Enum.Parse(roadmode,"Quickest"));Set(form,"position",Position(.8877,.2751));
        var slow=Zone("factory",.7532f,.5387f);Call(form,"WpfTarget",slow);
        var delay=Stopwatch.StartNew();Pump(()=>delay.ElapsedMilliseconds>200,()=>{});
        if(!(bool)Get(form,"routeCalculating"))throw new Exception("Cancellation fixture already finished");
        Set(form,"position",Position(.2507,.6064));var latest=Zone("latest",.2233f,.6689f);
        Call(form,"WpfTarget",latest);watch.Restart();
        Pump(()=>!(bool)Get(form,"routeCalculating"),()=>{
            var active=Get(form,"activeRoute");if(active!=null&&(PointF)Get(active,"TargetPoint")!=new PointF(.2233f,.6689f))throw new Exception("Stale target published");
        });
        route=Get(form,"activeRoute");
        if(route==null||!(bool)Get(route,"Success")||(PointF)Get(route,"TargetPoint")!=new PointF(.2233f,.6689f)||
            !object.ReferenceEquals(Get(form,"lastRouteTarget"),latest))throw new Exception("Latest target did not publish");
        Console.WriteLine("Retarget during factory calculation passed, latest success after "+watch.ElapsedMilliseconds+" ms");
        timer.Stop();
    }
    return 0;
 }
}

