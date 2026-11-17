using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
class UiInteractionRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static IEnumerable<DependencyObject> Children(DependencyObject root){foreach(object o in LogicalTreeHelper.GetChildren(root)){var c=o as DependencyObject;if(c==null)continue;yield return c;foreach(var n in Children(c))yield return n;}}
 static void Draw(object map){using(var dc=new DrawingVisual().RenderOpen())Call(map,"OnRender",dc);}
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);System.Threading.Thread.Sleep(10);}
 static bool CurrentSnapshot(object map){var scene=Get(map,"idleCameraScene");if(scene==null)return false;var key=map.GetType().GetMethod("SceneContentKey",F);return key==null||Equals(Get(scene,"ContentKey"),key.Invoke(map,null));}
 static void Ready(object map){var end=DateTime.UtcNow.AddSeconds(8);while(!CurrentSnapshot(map)&&DateTime.UtcNow<end){Draw(map);Pump();}if(!CurrentSnapshot(map))throw new Exception("Current idle snapshot failed to complete");}
 static int failures;
 static void Check(bool ok,string message){Console.WriteLine((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
 [STAThread]static int Main(string[] args){object runtime=null;try{
  Directory.CreateDirectory(args[1]);var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));runtime=Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{Path.GetFullPath(args[1]),true},null);
  var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=90 R=0}");Set(engine,"fullMapZoom",2f);
  var map=(FrameworkElement)Get(runtime,"tacticalMap");map.Measure(new Size(1400,900));map.Arrange(new Rect(0,0,1400,900));Call(runtime,"Refresh");Draw(map);
  ((HashSet<string>)Get(runtime,"expanded")).Clear();Set(runtime,"filterIdentity",null);Call(runtime,"Refresh");
  var sidebar=(Panel)Get(runtime,"sidebar");var original=sidebar.Children[0];var expand=Children(sidebar).OfType<Button>().First(b=>(b.Tag as string??"").StartsWith("poi-expand:"));string tag=(string)expand.Tag;
  expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(ReferenceEquals(original,sidebar.Children[0]),"Expanding one section preserves unrelated sidebar controls");
  var category=Children(sidebar).OfType<CheckBox>().First(c=>(c.Tag as string??"").StartsWith("poi-category:"));string catTag=(string)category.Tag;bool initial=category.IsChecked==true;
  category.IsChecked=!initial;var store=Get(Get(map,"State"),"Store");int id=int.Parse(catTag.Substring("poi-category:".Length));Check((bool)Call(store,"IsCategoryEnabled",id)==!initial,"Expanded category still changes filter");
  Children(sidebar).OfType<Button>().First(b=>(b.Tag as string)==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Children(sidebar).OfType<Button>().First(b=>(b.Tag as string)==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(Children(sidebar).OfType<CheckBox>().First(c=>(c.Tag as string)==catTag).IsChecked==!initial,"Collapse/reopen retains category state");
  Draw(map);Ready(map);Call(runtime,"RefreshCamera");Draw(map);Call(runtime,"EndCameraInteraction");
  int terrain=(int)Get(map,"TerrainLayerBuilds"),annotations=(int)Get(map,"AnnotationLayerBuilds");Call(engine,"WpfZoom",true,1);Call(runtime,"RefreshCamera");Draw(map);
  Check((int)Get(map,"TerrainLayerBuilds")==terrain&&(int)Get(map,"AnnotationLayerBuilds")==annotations,"Immediate resume after settle reuses snapshot without rebuilding map layers");
  Call(runtime,"FilterAll",false);Draw(map);Call(engine,"WpfZoom",true,1);Call(runtime,"RefreshCamera");Draw(map);
  Check(Get(map,"cameraScene")==null,"Changed filters cannot show an old enabled-marker snapshot");
  Call(runtime,"EndCameraInteraction");Draw(map);Ready(map);
  Call(runtime,"RefreshCamera");Draw(map);Check(Get(map,"cameraScene")!=null,"Replacement filtered snapshot becomes reusable");
  Call(runtime,"EndCameraInteraction");
  var state=Get(map,"State");Set(state,"Opacity",0);Call(map,"BeginCameraFrames");Draw(map);
  Check(Get(map,"cameraScene")==null,"Changed opacity cannot reuse the previous opaque snapshot");
  Call(map,"EndCameraFrames");Draw(map);Ready(map);Set(state,"Labels",!(bool)Get(state,"Labels"));Call(map,"BeginCameraFrames");Draw(map);
  Check(Get(map,"cameraScene")==null,"Changed labels invalidate the cached drawing");
  map.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
  Check(Get(map,"cameraScene")==null&&Get(map,"idleCameraScene")==null,"Unloaded map releases both cached snapshots");
  var window=(Window)Get(runtime,"tactical");var root=(FrameworkElement)window.Content;
  foreach(int width in new[]{900,1280,1600}) {
   root.Measure(new Size(width,850));root.Arrange(new Rect(0,0,width,850));root.UpdateLayout();double before=map.ActualWidth;
   var toggle=Children(root).OfType<Button>().First(b=>(b.Tag as string)=="tooltip:WpfCollapseSidebar");toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));root.UpdateLayout();
   Check(Math.Abs(map.ActualWidth-before)<.01&&Math.Abs(map.ActualWidth-width)<.01,"Sidebar collapse preserves map viewport at width "+width);
   toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));root.UpdateLayout();
   var headerSettings=Children(root).OfType<Button>().First(b=>(b.Tag as string)=="text:Settings");var p=headerSettings.TranslatePoint(new Point(0,0),root);
   Check(p.X>=0&&p.Y>=0&&p.X+headerSettings.ActualWidth<=width&&p.Y+headerSettings.ActualHeight<=72,"Header actions remain inside window at width "+width);
   var move=Children(root).OfType<Thumb>().First(t=>(t.Tag as string)=="tooltip:WpfMoveControls");var chrome=(FrameworkElement)((FrameworkElement)move.Parent).Parent;
   move.RaiseEvent(new DragDeltaEventArgs(0,-10000){RoutedEvent=Thumb.DragDeltaEvent});root.UpdateLayout();var top=chrome.TranslatePoint(new Point(0,0),root);
   Check(top.Y>=8,"Dragged toolbar remains inside map area at width "+width);
   move.RaiseEvent(new DragDeltaEventArgs(10000,10000){RoutedEvent=Thumb.DragDeltaEvent});root.UpdateLayout();var bottom=chrome.TranslatePoint(new Point(chrome.ActualWidth,chrome.ActualHeight),root);
   Check(bottom.Y<=842&&bottom.X<=width-340,"Dragged toolbar avoids map edge and sidebar at width "+width);
   ((MenuItem)chrome.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));root.UpdateLayout();
  }
  Check(Children(root).OfType<Button>().Any(b=>(b.Tag as string)=="text:MarkerCurrent"),"Current-position marker action is available on the map toolbar");
  var tools=Children(sidebar).OfType<Expander>().First();tools.IsExpanded=true;root.UpdateLayout();
  Check(Children(tools).OfType<Button>().Any(b=>(b.Tag as string)=="map-placement")&&Children(tools).OfType<Button>().Any(b=>(b.Tag as string)=="sidebar-presets"),"Map placement and presets remain accessible in tools");
  Console.WriteLine("Failures="+failures);return failures==0?0:1;
 }catch(Exception e){while(e.InnerException!=null)e=e.InnerException;Console.WriteLine("ERROR "+e.GetType().Name+": "+e.Message);Console.WriteLine(e.StackTrace);return 2;}finally{if(runtime!=null)((IDisposable)runtime).Dispose();}}
}
