using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
class SavedMarkerTests {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static object[] Markers(object engine){return ((IEnumerable)Call(engine,"WpfSavedMarkers")).Cast<object>().ToArray();}
 static PointF Point(object marker){return ((PointF[])Get(marker,"Points"))[0];}
 static IEnumerable<DependencyObject> Children(DependencyObject root){foreach(object o in LogicalTreeHelper.GetChildren(root)){var c=o as DependencyObject;if(c==null)continue;yield return c;foreach(var n in Children(c))yield return n;}}
 static T Tagged<T>(DependencyObject root,string tag) where T:FrameworkElement{return Children(root).OfType<T>().First(c=>(c.Tag as string)==tag);}
 static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static int failures;
 static void Check(bool ok,string message){Console.WriteLine((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
 [STAThread]static int Main(string[] args){object runtime=null;try{
  var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));string profile=Path.GetFullPath(args[1]);Directory.CreateDirectory(profile);
  Func<object> create=()=>Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{profile,true},null);
  runtime=create();var engine=Get(runtime,"engine");
  var target=Activator.CreateInstance(asm.GetType("ScumMiniMap.MapZone"));Set(target,"Name","Existing route");Set(target,"Points",new[]{new PointF(.2f,.2f)});Set(engine,"searchTarget",target);
  Check((bool)Call(engine,"WpfWaypoint","Test named marker",(PointF?)new PointF(.43f,.47f)),"Named point saves");
  Check(ReferenceEquals(Get(engine,"searchTarget"),target),"Saving a point does not change navigation");
  Call(engine,"WpfLayer","waypoints",true);
  var clicked=Call(engine,"WpfClickedTarget",new PointF(.431f,.471f),.01f);
  Check(Point(clicked)==new PointF(.43f,.47f),"Click near a saved marker uses exact marker coordinates");
  Call(engine,"WpfLayer","waypoints",false);clicked=Call(engine,"WpfClickedTarget",new PointF(.431f,.471f),.01f);
  Check(Point(clicked)==new PointF(.431f,.471f),"Hidden personal markers do not intercept map navigation");
  bool available=engine.GetType().GetMethod("WpfMarkCurrent",F)!=null&&engine.GetType().GetMethod("WpfSavedMarkers",F)!=null;
  Check(available,"One-click marker and saved-marker list APIs exist");if(!available)return 1;
  int count=Markers(engine).Length;Set(engine,"position",null);
  Check(!(bool)Call(engine,"WpfMarkCurrent")&&Markers(engine).Length==count,"No position cannot create a marker");
  Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=90 R=0}");
  var expected=(PointF)engine.GetType().GetMethod("ToMap",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public,null,new[]{Get(engine,"position").GetType()},null).Invoke(null,new[]{Get(engine,"position")});
  Set(engine,"searchTarget",target);Call(runtime,"Refresh");
  var root=(DependencyObject)((Window)Get(runtime,"tactical")).Content;
  Click(Tagged<Button>(root,"text:MarkerCurrent"));Click(Tagged<Button>(root,"text:MarkerCurrent"));
  var markers=Markers(engine);Check(markers.Length==count+2,"Toolbar directly saves once per click without a name dialog");
  var quick=markers.Where(m=>Point(m)==expected).ToArray();Check(quick.Length==2&&quick.Select(m=>(string)Get(m,"Name")).Distinct().Count()==2,"Current markers use exact position and unique default names");
  Check(ReferenceEquals(Get(engine,"searchTarget"),target),"Quick marking preserves the active route");
  var first=quick[0];Set(engine,"searchTarget",first);Set(engine,"wpfQueuedTarget",first);
  Check(!(bool)Call(engine,"WpfRenameMarker",first,"  "),"Blank rename is rejected");
  Check(!(bool)Call(engine,"WpfRenameMarker",first,new string('x',121)),"Overlong rename is rejected");
  Set(engine,"zonesLoadFailed",true);Check(!(bool)Call(engine,"WpfRenameMarker",first,"Rejected")&&(string)Get(first,"Name")!="Rejected","Failed save leaves marker unchanged");Set(engine,"zonesLoadFailed",false);
  Check((bool)Call(engine,"WpfRenameMarker",first,"  Renamed camp  "),"Marker can be renamed");
  Check(Point(first)==expected&&(string)Get(first,"Name")=="Renamed camp"&&ReferenceEquals(Get(engine,"searchTarget"),first)&&ReferenceEquals(Get(engine,"wpfQueuedTarget"),first),"Rename preserves position and in-flight navigation identity");
  var localization=asm.GetType("ScumMiniMap.Localization");localization.GetProperty("Current").SetValue(null,Enum.ToObject(asm.GetType("ScumMiniMap.AppLanguage"),6),null);
  var dialog=(Window)Call(runtime,"BuildSavedMarkers");var list=Tagged<ListBox>(dialog,"marker-list");Check(list.Items.Count==markers.Length,"My markers lists all saved points");
  Check(!Tagged<Button>(dialog,"marker-navigate").IsEnabled&&!Tagged<Button>(dialog,"marker-rename").IsEnabled,"List requires selection before navigate or rename");
  list.SelectedIndex=0;var selected=((ListBoxItem)list.SelectedItem).Tag;var name=Tagged<TextBox>(dialog,"marker-edit-name");name.Text="From list";Click(Tagged<Button>(dialog,"marker-rename"));Check((string)Get(selected,"Name")=="From list","List rename saves the selected point");
  runtime.GetType().GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{dialog,Path.Combine(profile,"my-markers.png")});
  Set(engine,"position",null);Click(Tagged<Button>(dialog,"marker-navigate"));Check(ReferenceEquals(Get(engine,"searchTarget"),selected),"List navigation selects the exact saved marker");
  ((IDisposable)runtime).Dispose();runtime=null;runtime=create();engine=Get(runtime,"engine");
  markers=Markers(engine);Check(markers.Length==count+2&&markers.Any(m=>(string)Get(m,"Name")=="From list"),"Markers and names survive restart");
  Check(markers.Count(m=>Point(m)==expected)==2,"Saved coordinates survive restart");
  Console.WriteLine("Failures="+failures);return failures==0?0:1;
 }catch(Exception e){while(e.InnerException!=null)e=e.InnerException;Console.WriteLine("ERROR "+e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 2;}finally{if(runtime!=null)((IDisposable)runtime).Dispose();}}
}
