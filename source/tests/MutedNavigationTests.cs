using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

// Exercise the real window guidance entry point without constructing UI,
// installing voices, starting native hooks or writing a user profile.
static class MutedNavigationTests {
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Assembly assembly;
    static object Create(string name) { return Activator.CreateInstance(assembly.GetType("ScumMiniMap."+name),true); }
    static void Set(object value,string field,object data) { value.GetType().GetField(field,Fields).SetValue(value,data); }
    static object Get(object value,string field) { return value.GetType().GetField(field,Fields).GetValue(value); }
    static PointF P(double x,double y) { return new PointF((float)(.5+x/15216.18),(float)(.5+y/15236.18)); }
    static void Check(bool ok,string label) { if(!ok)throw new Exception(label);Console.WriteLine("PASS: "+label); }
    static object Fixture() {
        object window=FormatterServices.GetUninitializedObject(assembly.GetType("ScumMiniMap.MapWindow"));GC.SuppressFinalize(window);
        Set(window,"chat",Create("ChatState"));Set(window,"voiceNavigator",Create("VoiceNavigator"));
        // Do not run the asynchronous router in this isolated gate test.
        Set(window,"closing",true);
        object target=Create("MapZone");Set(target,"Points",new[]{P(500,0)});
        Set(window,"searchTarget",target);Set(window,"voiceTarget",target);
        object route=Create("RoadRoute");Set(route,"Success",true);Set(route,"Polyline",new[]{P(0,0),P(500,0)});
        Set(route,"TargetPoint",P(500,0));Set(window,"activeRoute",route);
        return window;
    }
    static void Sample(object window,double x,double y,DateTime now,bool focus,bool fresh) {
        object position=Create("Position");PointF p=P(x,y);
        Set(position,"X",617718-p.X*1521618.0);Set(position,"Y",618618-p.Y*1523618.0);
        Set(window,"position",position);Set(window,"updated",fresh?now:now.AddSeconds(-10));
        window.GetType().GetMethod("TickVoice",Fields).Invoke(window,new object[]{now,focus});
    }
    [STAThread] static int Main(string[] args) {
        try {
            assembly=Assembly.LoadFrom(args[0]);DateTime now=DateTime.UtcNow;
            foreach(string context in new[]{"muted","unfocused","stale"}) {
                object window=Fixture();
                for(int i=0;i<3;i++)Sample(window,100-i*8,0,now.AddSeconds(i),context!="unfocused",context!="stale");
                object navigator=Get(window,"voiceNavigator");
                bool reroute=(bool)navigator.GetType().GetProperty("NeedsReroute",Fields).GetValue(navigator,null);
                Check(reroute==(context=="muted"),context=="muted"?"Muted guidance detects reverse travel and requests rerouting":"Guidance ignores "+context+" positions");
                Check(Get(window,"voicePlayer")==null&&!(bool)Get(window,"voicesInstalled"),"Silent guidance does not initialize audio or install voice files");
            }
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
