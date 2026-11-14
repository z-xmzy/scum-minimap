using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
class ZoomBench {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string k){return o.GetType().GetField(k,F).GetValue(o);}
 static object Call(object o,string k,params object[] a){return o.GetType().GetMethod(k,F).Invoke(o,a);}
 static object map,rt;
 static void Sample(string name,Action start,int span) {
  var w=Stopwatch.StartNew();long prior=0,max=0;int frames=0;
  Action<long> beat=t=>{max=Math.Max(max,t-prior);prior=t;};
  EventHandler render=(s,e)=>{frames++;};
  System.Windows.Media.CompositionTarget.Rendering+=render;
  int terrain=(int)Get(map,"TerrainLayerBuilds"),labels=(int)Get(map,"AnnotationLayerBuilds");
  start();long sync=w.ElapsedMilliseconds;beat(sync);
  while(w.ElapsedMilliseconds<span){System.Windows.Forms.Application.DoEvents();beat(w.ElapsedMilliseconds);System.Threading.Thread.Sleep(1);}
  System.Windows.Media.CompositionTarget.Rendering-=render;
  Console.WriteLine("{0} sync={1} maxGap={2} renderEvents={3} terrain={4} annotations={5}",name,sync,max,frames,(int)Get(map,"TerrainLayerBuilds")-terrain,(int)Get(map,"AnnotationLayerBuilds")-labels);
 }
 [STAThread]static int Main(string[] a){try{
 string profile=Path.GetFullPath(a[1]);Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"settings.ini"),"Welcomed=True\nLanguage=zh-CN\nWpfDefaultsRevision=1\n");
 var asm=Assembly.LoadFrom(Path.GetFullPath(a[0]));var t=asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime");
 rt=Activator.CreateInstance(t,F,null,new object[]{profile,true},null);map=Get(rt,"tacticalMap");
 var engine=Get(rt,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=135 R=0}");engine.GetType().GetField("fullMapZoom",F).SetValue(engine,1f);
 if(a.Length>2){Call(rt,"StartViews");Sample("startupIdle",()=>{},400);}
 Sample("open",()=>Call(rt,"ToggleTactical",false),700);
 Sample("firstWheel",()=>Call(rt,"QueueZoom",1,true),700);
 Sample("nextWheel",()=>Call(rt,"QueueZoom",1,true),700);
 Sample("zoomOut",()=>{Call(rt,"QueueZoom",-1,true);Call(rt,"QueueZoom",-1,true);},700);
 ((IDisposable)rt).Dispose();return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}