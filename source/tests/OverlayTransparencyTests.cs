using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
class OverlayTransparencyTests {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static IEnumerable<DependencyObject> Children(DependencyObject p){foreach(object o in LogicalTreeHelper.GetChildren(p)){var c=o as DependencyObject;if(c==null)continue;yield return c;foreach(var n in Children(c))yield return n;}}
 static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);System.Threading.Thread.Sleep(10);}
 static int failures;
 static void Check(bool ok,string label){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(!ok)failures++;}
 static BitmapSource Draw(object runtime,FrameworkElement root,FrameworkElement map,float zoom){var engine=Get(runtime,"engine");Set(engine,"fullMapZoom",zoom);Call(runtime,"EndCameraInteraction");Call(runtime,"Refresh");var state=Get(map,"State");foreach(string n in new[]{"Grid","GridLabels","History","Zones","Waypoints","Labels","Compass","Heading"})Set(state,n,false);Set(state,"Position",null);Call(Get(state,"Store"),"SetAllEnabled",false);root.Measure(new Size(1280,850));root.Arrange(new Rect(0,0,1280,850));for(int i=0;i<30;i++){map.InvalidateVisual();root.UpdateLayout();Pump();}root.UpdateLayout();var b=new RenderTargetBitmap(1280,850,96,96,PixelFormats.Pbgra32);b.Render(root);return b;}
 static int Alpha(BitmapSource b,int x,int y){var p=new byte[4];b.CopyPixels(new Int32Rect(x,y,1,1),p,4,0);return p[3];}
 static void Save(BitmapSource b,string file){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using(var s=File.Create(file))encoder.Save(s);}
 [STAThread]static int Main(string[] args){object runtime=null;try{Directory.CreateDirectory(args[1]);var asm=Assembly.LoadFrom(args[0]);runtime=Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfFieldConsoleRuntime"),F,null,new object[]{Path.Combine(args[1],"profile"),true},null);var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=90 R=0}");var window=(Window)Get(runtime,"tactical");var root=(FrameworkElement)window.Content;var map=(FrameworkElement)Get(runtime,"tacticalMap");
  var one=Draw(runtime,root,map,1);Save(one,Path.Combine(args[1],"overlay-1x.png"));
  Check((int)Get(Get(map,"State"),"Opacity")==0,"1x uses transparent software base map");Check(Alpha(one,80,24)<=2&&Alpha(one,640,24)<=2,"Game map top edge remains transparent at 1x");Check(Alpha(one,640,842)<=2,"Game map bottom edge remains transparent at 1x");
  var settings=Children(root).OfType<Button>().First(b=>(b.Tag as string)=="text:Settings");var p=settings.TranslatePoint(new Point(0,0),root);Check(p.X>=940&&p.X+settings.ActualWidth<=1280,"Settings action occupies sidebar only");
  var two=Draw(runtime,root,map,2);Save(two,Path.Combine(args[1],"overlay-2x.png"));Check((int)Get(Get(map,"State"),"Opacity")==100&&Alpha(two,640,24)==255,"Zoomed software base map renders through the top edge");Check(map.ActualWidth==1280&&map.ActualHeight==850,"Overlay controls do not resize map viewport");
  var back=Draw(runtime,root,map,1);Check(Alpha(back,640,24)<=2&&Alpha(back,640,842)<=2,"Returning to 1x restores transparent top and bottom");
  Children(root).OfType<Button>().First(b=>(b.Tag as string)=="tooltip:WpfCollapseSidebar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));root.UpdateLayout();var closed=Draw(runtime,root,map,1);Save(closed,Path.Combine(args[1],"overlay-1x-collapsed.png"));Check(Alpha(closed,1200,24)<=2&&Alpha(closed,1200,842)<=2,"Collapsing sidebar also removes its header and footer");Check(map.ActualWidth==1280&&map.ActualHeight==850,"Collapsed overlay retains full game-map coordinates");Console.WriteLine("Failures="+failures);return failures==0?0:1;
 }catch(Exception e){while(e.InnerException!=null)e=e.InnerException;Console.WriteLine(e.GetType().Name+": "+e.Message);Console.WriteLine(e.StackTrace);return 2;}finally{if(runtime!=null)((IDisposable)runtime).Dispose();}}
}
