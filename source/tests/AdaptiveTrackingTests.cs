using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

static class AdaptiveTrackingTests {
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Assembly assembly;
    static Type windowType;
    static object NewWindow() {
        object window=FormatterServices.GetUninitializedObject(windowType);GC.SuppressFinalize(window);
        Set(window,"copyIntervalMs",250);Set(window,"scumCopyModifierKey",0);
        var cadence=windowType.GetField("trackingCadence",Hidden);
        if(cadence!=null)cadence.SetValue(window,Activator.CreateInstance(cadence.FieldType,true));
        Set(window,"motion",Activator.CreateInstance(assembly.GetType("ScumMiniMap.MapMotion")));
        return window;
    }
    static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Hidden).SetValue(obj,value); }
    static object Get(object obj,string name) { return obj.GetType().GetField(name,Hidden).GetValue(obj); }
    static void Observe(object window,double now,double speed,double filtered,double turn=0,double elapsed=.5) {
        Set(window,"filteredSpeed",filtered);
        var method=windowType.GetMethod("ObserveTrackingCadence",Hidden);
        if(method!=null)method.Invoke(window,new object[]{speed,turn,elapsed,now});
    }
    static int Rate(object window,double now) {
        var method=windowType.GetMethod("CurrentTrackingIntervalMs",Hidden);
        if(method!=null)return (int)method.Invoke(window,new object[]{now});
        // The old release always used this fixed interval regardless of motion.
        return (int)assembly.GetType("ScumMiniMap.Program").GetMethod("TrackingIntervalMs",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{Get(window,"copyIntervalMs"),Get(window,"scumCopyModifierKey")});
    }
    static void Check(bool ok,string label) { if(!ok)throw new Exception(label);Console.WriteLine("PASS: "+label); }
    static void Walk(object window,double start=0) { for(int i=0;i<=8;i++)Observe(window,start+i*.5,4,4); }
    static void Idle(object window,double start=0) { for(int i=0;i<=12;i++)Observe(window,start+i*.5,0,0); }
    [STAThread] static int Main(string[] args) {
        try {
            assembly=Assembly.LoadFrom(args[0]);windowType=assembly.GetType("ScumMiniMap.MapWindow",true);
            object window=NewWindow();Check(Rate(window,0)==250,"Start with fast acquisition");
            Walk(window);Check(Rate(window,4)==500,"Sustained walking uses 500 ms rather than fixed 250 ms");
            Observe(window,4.5,15,4.5);Check(Rate(window,4.5)==250,"Acceleration raises frequency before the smoothed speed catches up");
            Observe(window,5,0,0);Check(Rate(window,5)==250,"A single slow sample does not lower frequency");
            for(int i=1;i<=8;i++)Observe(window,5+i*.5,i%2==0?6:5,5);
            Check(Rate(window,9)==250,"Speed near the transition does not oscillate between rates");
            Walk(window,10);Check(Rate(window,14)==500,"Continuous low speed eventually returns to 500 ms");
            Idle(window,15);Check(Rate(window,21)==1000,"Sustained stationary position and heading uses 1000 ms");
            Observe(window,22,4,1,0,1);Check(Rate(window,22)==500,"Walking immediately leaves idle cadence");
            Observe(window,22.5,0,0,30);Check(Rate(window,22.5)==250,"Turning in place temporarily uses fast sampling");
            window=NewWindow();for(int i=0;i<=14;i++)Observe(window,i*.5,0,0,1);
            Check(Rate(window,7)==1000,"Small heading noise does not keep stationary sampling fast");
            Check(Rate(window,11)==250,"Stale measurements return to acquisition cadence");
            Observe(window,12,0,0,0,5);Check(Rate(window,12)==250,"Long interruptions reset slow-state history");
            window=NewWindow();Idle(window);Observe(window,6.5,double.NaN,0);
            Check(Rate(window,6.5)==250,"Teleport or invalid velocity resets the decision");
            Observe(window,7,0,0,0,0);Check(Rate(window,7)==250,"Zero elapsed time cannot establish stationary state");
            Walk(window,8);Check(Rate(window,12)==500,"Valid samples recover after invalid measurements");
            Set(window,"copyIntervalMs",750);Check(Rate(window,12)==750,"A slower configured interval is respected");
            Set(window,"copyIntervalMs",250);Set(window,"scumCopyModifierKey",17);
            Observe(window,12.5,20,20);Check(Rate(window,12.5)==1000,"Modifier bindings retain their existing safe minimum");
            window=NewWindow();Idle(window);
            DateTime sent=new DateTime(2026,10,31,1,0,0,DateTimeKind.Utc);
            Set(window,"pending",true);Set(window,"sent",sent);Set(window,"next",sent.AddSeconds(1));
            Observe(window,6.5,20,1);
            Check((DateTime)Get(window,"next")==sent.AddMilliseconds(250),"A fast reply reschedules the next request from its send time");
            Set(window,"pending",false);Set(window,"next",sent.AddSeconds(5));Walk(window,7);
            Check((DateTime)Get(window,"next")==sent.AddSeconds(5),"Unsolicited clipboard samples do not erase retry scheduling");
            Check((bool)Get(Get(window,"motion"),"SlowSampling"),"Low-frequency observations select smooth animation");
            Observe(window,11.5,20,20);
            Check(!(bool)Get(Get(window,"motion"),"SlowSampling"),"High-frequency observations restore responsive animation");
            object motion=Get(window,"motion");Set(motion,"SlowSampling",true);
            var sample=motion.GetType().GetMethod("Sample");var advance=motion.GetType().GetMethod("Advance");
            sample.Invoke(motion,new object[]{new PointF(.5f,.5f),359.0,0.0,false});
            sample.Invoke(motion,new object[]{new PointF(.5001f,.5f),1.0,.5,false});
            advance.Invoke(motion,new object[]{.75});
            Check(Math.Abs(((PointF)Get(motion,"Point")).X-.50005)<.000001,"Slow samples interpolate across the sampling interval");
            Check(Math.Abs((double)Get(motion,"Yaw")-361)<.001,"Heading wraps immediately without a long rotation");
            advance.Invoke(motion,new object[]{1.0});
            Check(Math.Abs(((PointF)Get(motion,"Point")).X-.5001)<.000001 && !(bool)advance.Invoke(motion,new object[]{2.0}),"Animation stops at the observed position when samples stop");
            // Simulate one minute of steady movement without any native input.
            foreach(double speed in new[]{0.0,4.0,20.0}) {
                window=NewWindow();int requests=0;double time=0;
                while(time<60) { double dt=Rate(window,time)/1000.0;time+=dt;Observe(window,time,speed,speed,0,dt);requests++; }
                Console.WriteLine("SIMULATION: speed="+speed+" m/s requests/min="+requests);
                Check(speed==20?requests==240:requests<150,"Adaptive request count matches motion without exceeding fixed-rate load");
            }
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
