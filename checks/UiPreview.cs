using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Captures the actual packaged WPF interface using synthetic, loopback-only peers.
class WpfShowcaseHarness {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object value,string name) { return value.GetType().GetField(name,Flags).GetValue(value); }
    static void Set(object value,string name,object data) { value.GetType().GetField(name,Flags).SetValue(value,data); }
    static object Call(object value,string name,params object[] args) { return value.GetType().GetMethod(name,Flags).Invoke(value,args); }
    static IEnumerable<DependencyObject> Children(DependencyObject parent) { foreach(object item in LogicalTreeHelper.GetChildren(parent)) { var child=item as DependencyObject;if(child==null)continue;yield return child;foreach(var nested in Children(child))yield return nested; } }
    static void Pump() { System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(20); }
    static string Json(string value) { return "\""+value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\""; }
    static void Shot(Window window,string file,double scale=1) {
        window.ShowActivated=false;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-32000;window.Top=-32000;window.Show();window.UpdateLayout();for(int i=0;i<5;i++)Pump();window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)(window.ActualWidth*scale),(int)(window.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(file))encoder.Save(stream);window.Hide();Console.WriteLine(Path.GetFileName(file));
    }
    [STAThread]static int Main(string[] args) {try {Run(args);return 0;}catch(Exception e){while(e.InnerException!=null)e=e.InnerException;Console.WriteLine(e.GetType().Name+" "+e.Message+" "+e.StackTrace);return 1;}} static void Run(string[] args){
        string output=Path.GetFullPath(args[1]),profile=Path.Combine(output,"isolated-profile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"settings.ini"),"Welcomed=True\nLanguage="+args[2]+"\nWpfDefaultsRevision=1\n");
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var runtimeType=assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime");object runtime=Activator.CreateInstance(runtimeType,Flags,null,new object[]{profile,true},null);var peers=new List<IDisposable>();
        try {
            var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=135 R=0}");Set(engine,"fullMapOpacity",100);Set(engine,"fullMapZoom",2f);Set(engine,"gridOpacity",20);Call(engine,"WpfRotate",true);Call(engine,"WpfAppearance","Circle",90);
            var position=Get(engine,"position");var point=(System.Drawing.PointF)engine.GetType().GetMethod("ToMap",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new[]{position});
            var tactical=(Window)Get(runtime,"tactical");tactical.Width=1280;tactical.Height=820;
            Set(engine,"fullMapZoom",1f);Call(runtime,"ToggleTactical",false);
            tactical.WindowState=WindowState.Normal;tactical.Width=1280;tactical.Height=820;
            var toggle=Children(tactical).OfType<Button>().Single(b=>(b.Tag as string??"").EndsWith("Sidebar"));
            var side=(FrameworkElement)Get(runtime,"sidebar");var zoom=(Button)Get(runtime,"zoomResetButton");
            var card=(FrameworkElement)Get(runtime,"targetCard");var cards=(FrameworkElement)card.Parent;
            var map=(FrameworkElement)Get(runtime,"tacticalMap");
            var grid=(Grid)tactical.Content;var shelf=(Grid)toggle.Parent;
            Action<int,int,bool,bool> check=(w,h,native,open)=>{
                tactical.MinWidth=0;tactical.MinHeight=0;tactical.MaxWidth=4000;tactical.MaxHeight=4000;
                grid.Width=w;grid.Height=h;tactical.Width=w;tactical.Height=h;Pump();tactical.UpdateLayout();Pump();tactical.UpdateLayout();
                grid.Measure(new Size(w,h));grid.Arrange(new Rect(0,0,w,h));grid.UpdateLayout();
                double vw=grid.ActualWidth,vh=grid.ActualHeight;if(Math.Abs(vw-w)>2||Math.Abs(vh-h)>2)throw new Exception("fixture dimensions "+vw+"x"+vh);
                bool hasSpace=w!=h||!native;
                bool shown=hasSpace&&open;
                if(side.IsVisible!=shown||zoom.IsVisible==native||cards.IsVisible==native||toggle.IsVisible!=hasSpace)throw new Exception("visibility "+w+"x"+h);
                var corner=shelf.TranslatePoint(new Point(),grid);
                var region=new Rect(corner,new Size(shelf.ActualWidth,shelf.ActualHeight));
                double size=Math.Min(vw,vh);
                var mapBox=native?new Rect((vw-size)/2,(vh-size)/2,size,size):new Rect(0,0,map.ActualWidth,map.ActualHeight);
                Rect overlap=Rect.Intersect(region,mapBox);
                if(hasSpace&&(!overlap.IsEmpty && overlap.Width*overlap.Height>.001))throw new Exception("sidebar overlap "+overlap);
                if(native&&(Math.Abs(map.ActualWidth-vw)>1 ||(int)Get(Get(map,"State"),"Opacity")!=0))throw new Exception("native camera changed");
                if(shown&&(region.Width<40||region.Height<40))throw new Exception("controls clipped");
                Console.WriteLine("PASS "+w+"x"+h+" native="+native+" open="+open+" sidebar="+region+" map="+mapBox);
            };
            Action click=()=>toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(1920,1080,true,true);check(1280,820,true,true);check(800,600,true,true);check(1080,1920,true,true);
            check(900,900,true,true);check(1440,900,true,true);click();check(1440,900,true,false);
            Call(runtime,"Refresh");check(1440,900,true,false);click();check(1440,900,true,true);
            Call(runtime,"ToggleTactical",false);Call(runtime,"ToggleTactical",false);check(1440,900,true,true);
            Call(engine,"WpfZoomTo",true,1.45f);Call(runtime,"RefreshCamera");check(1280,820,false,true);
            click();check(1280,820,false,false);click();Call(engine,"WpfZoomTo",true,1f);Call(runtime,"RefreshCamera");check(1280,820,true,true);
            Shot(tactical,Path.Combine(output,"原图侧栏.png"));
        }finally{((IDisposable)runtime).Dispose();}
    }
}

