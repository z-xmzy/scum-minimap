using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
class MarkerIconTests {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static object[] Markers(object e){return ((IEnumerable)Call(e,"WpfSavedMarkers")).Cast<object>().ToArray();}
 static IEnumerable<DependencyObject> Children(DependencyObject r){foreach(object o in LogicalTreeHelper.GetChildren(r)){var c=o as DependencyObject;if(c==null)continue;yield return c;foreach(var n in Children(c))yield return n;}}
 static T Tagged<T>(DependencyObject r,string tag) where T:FrameworkElement{return Children(r).OfType<T>().First(c=>(c.Tag as string)==tag);}
 static void Click(Button b){b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void Choose(ListBox l,string id){l.SelectedItem=l.Items.Cast<ListBoxItem>().First(i=>(string)i.Tag==id);}
 static int failures;
 static void Check(bool ok,string m){Console.WriteLine((ok?"PASS ":"FAIL ")+m);if(!ok)failures++;}
 [STAThread]static int Main(string[] args){object runtime=null;try{
  var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));var model=asm.GetType("ScumMiniMap.MapZone");
  Check(model.GetField("Icon")!=null,"Marker model supports icon identity");if(failures>0)return 1;
  string folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);var zoneStore=asm.GetType("ScumMiniMap.ZoneStore");
  Func<string,object> load=text=>zoneStore.GetMethod("Load",new[]{typeof(Stream)}).Invoke(null,new object[]{new MemoryStream(Encoding.UTF8.GetBytes(text))});
  var legacy=((IEnumerable)load("Old marker\t-1\t0.4,0.5\tCustom\tCustom%20Waypoints\n")).Cast<object>().Single();
  Check((string)Get(legacy,"Icon")=="pin","Legacy marker without icon defaults safely");
  var unknown=((IEnumerable)load("Future marker\t-1\t0.4,0.5\tCustom\tCustom%20Waypoints\t\t\tfuture-icon\n")).Cast<object>().Single();
  Check((string)Get(unknown,"Icon")=="pin","Unknown icon falls back without losing marker");
  Func<object> create=()=>Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{folder,true},null);runtime=create();var engine=Get(runtime,"engine");
  asm.GetType("ScumMiniMap.Localization").GetProperty("Current").SetValue(null,Enum.ToObject(asm.GetType("ScumMiniMap.AppLanguage"),6),null);
  var target=Activator.CreateInstance(model);Set(target,"Name","Existing navigation");Set(target,"Points",new[]{new PointF(.2f,.2f)});Set(engine,"searchTarget",target);
  string[] ids={"pin","house","skull","car","box","tent","fuel","flag"};
  foreach(string id in ids)Check((bool)Call(engine,"WpfWaypointWithIcon",id,(PointF?)new PointF(.4f,.5f),id),"Save "+id+" marker");
  var skull=Markers(engine).First(m=>(string)Get(m,"Icon")=="skull");Check(!(bool)skull.GetType().GetProperty("IsDeathMarker").GetValue(skull,null),"Skull icon remains a normal marker, not a death record");
  Check(ReferenceEquals(Get(engine,"searchTarget"),target),"Saving icon markers preserves navigation");
  var car=Markers(engine).First(m=>(string)Get(m,"Icon")=="car");Check((string)Get(Call(car,"Clone"),"Icon")=="car","Clone retains selected icon");
  Set(engine,"searchTarget",car);Set(engine,"zonesLoadFailed",true);
  Check(!(bool)Call(engine,"WpfUpdateMarker",car,"Do not save","box")&&(string)Get(car,"Icon")=="car"&&(string)Get(car,"Name")=="car","Failed edit retains name and icon together");Set(engine,"zonesLoadFailed",false);
  var window=(Window)Call(runtime,"BuildAddWaypoint",(PointF?)new PointF(.45f,.55f));var picker=Tagged<ListBox>(window,"marker-icon");Check(picker.Items.Count==8,"Add dialog offers eight icon previews");
  Tagged<TextBox>(window,"waypoint-name").Text="林间藏箱";Choose(picker,"box");Click(Tagged<Button>(window,"waypoint-save"));
  var box=Markers(engine).First(m=>(string)Get(m,"Name")=="林间藏箱");Check((string)Get(box,"Icon")=="box","Named add dialog commits selected icon");
  window=(Window)Call(runtime,"BuildSavedMarkers");var list=Tagged<ListBox>(window,"marker-list");list.SelectedItem=list.Items.Cast<ListBoxItem>().First(i=>ReferenceEquals(i.Tag,car));picker=Tagged<ListBox>(window,"marker-icon");
  Check((string)((ListBoxItem)picker.SelectedItem).Tag=="car","Edit dialog restores selected icon");Choose(picker,"house");Tagged<TextBox>(window,"marker-edit-name").Text="山边住处";Click(Tagged<Button>(window,"marker-rename"));
  Check((string)Get(car,"Icon")=="house"&&(string)Get(car,"Name")=="山边住处"&&ReferenceEquals(Get(engine,"searchTarget"),car),"UI edits name and icon without changing navigation identity");
  runtime.GetType().GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{window,Path.Combine(folder,"marker-icons.png")});window.Close();
  Check((bool)Call(engine,"WpfRenameMarker",car,"住处改名")&&(string)Get(car,"Icon")=="house","Name-only edits preserve icon");
  var renderer=asm.GetType("ScumMiniMap.WpfMarkerIcons");var iconMethod=renderer.GetMethod("Image",BindingFlags.Static|BindingFlags.NonPublic);
  var hashes=new HashSet<string>();foreach(string id in ids){var source=(ImageSource)iconMethod.Invoke(null,new object[]{id});Check(source.IsFrozen&&ReferenceEquals(source,iconMethod.Invoke(null,new object[]{id})),"Frozen cached vector for "+id);var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(source,new Rect(0,0,32,32));var bitmap=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);byte[] pixels=new byte[32*32*4];bitmap.CopyPixels(pixels,128,0);hashes.Add(Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(pixels)));}
  Check(hashes.Count==8,"All eight icons render distinctly");
  foreach(bool compact in new[]{false,true}) {
   var surface=(FrameworkElement)Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfMapSurface"),true);Set(surface,"Compact",compact);surface.Measure(new System.Windows.Size(320,320));surface.Arrange(new Rect(0,0,320,320));
   var state=Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfMapState"),true);Set(state,"Waypoints",true);Set(state,"Shape","Square");Set(state,"Zoom",1f);Set(state,"Pan",new PointF(.5f,.5f));Set(state,"Trail",new PointF[0]);hashes.Clear();
   foreach(string id in ids) {
    var marker=Activator.CreateInstance(model);Set(marker,"Name",id);Set(marker,"Icon",id);Set(marker,"Layer","Custom Waypoints");Set(marker,"Points",new[]{new PointF(.5f,.5f)});var saved=Array.CreateInstance(model,1);saved.SetValue(marker,0);Set(state,"Saved",saved);Call(surface,"PublishState",state);
    using(var dc=new DrawingVisual().RenderOpen())Call(surface,"OnRender",dc);surface.UpdateLayout();var bitmap=new RenderTargetBitmap(320,320,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var pixels=new byte[40*40*4];bitmap.CopyPixels(new Int32Rect(140,140,40,40),pixels,160,0);hashes.Add(Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(pixels)));
   }
   Check(hashes.Count==8,(compact?"Minimap":"Full map")+" renders the selected icon, not a generic dot");
   Check(Convert.ToDouble(Call(surface,"MarkerHitRadius"))*320>=(compact?11:13),"Hit target covers the visible icon");
   surface.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
  }
  var filled=Call(car,"Clone");Set(filled,"FillArgb",(int?)-1234);Set(filled,"Layer","Death markers");Set(filled,"DeathCreatedUtc",(DateTime?)new DateTime(2026,1,2,3,4,5,DateTimeKind.Utc));
  var collection=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(model));collection.Add(filled);string file=Path.Combine(folder,"column-check.tsv");zoneStore.GetMethod("Save").Invoke(null,new object[]{file,collection});var roundtrip=((IEnumerable)zoneStore.GetMethod("Load",new[]{typeof(string)}).Invoke(null,new object[]{file})).Cast<object>().Single();
  Check((string)Get(roundtrip,"Icon")=="house"&&Equals(Get(roundtrip,"FillArgb"),-1234)&&Equals(Get(roundtrip,"DeathCreatedUtc"),Get(filled,"DeathCreatedUtc")),"Icon field preserves fill colour and death timestamp columns");
  ((IDisposable)runtime).Dispose();runtime=null;runtime=create();engine=Get(runtime,"engine");
  Check(Markers(engine).Any(m=>(string)Get(m,"Name")=="林间藏箱"&&(string)Get(m,"Icon")=="box")&&Markers(engine).Any(m=>(string)Get(m,"Name")=="住处改名"&&(string)Get(m,"Icon")=="house"),"Icons and edited names survive restart");
  Console.WriteLine("Failures="+failures);return failures==0?0:1;
 }catch(Exception e){while(e.InnerException!=null)e=e.InnerException;Console.WriteLine("ERROR "+e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 2;}finally{if(runtime!=null)((IDisposable)runtime).Dispose();}}
}
