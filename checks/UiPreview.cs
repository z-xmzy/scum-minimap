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
    [STAThread]static void Main(string[] args) {
        string output=Path.GetFullPath(args[1]),profile=Path.Combine(output,"isolated-profile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"settings.ini"),"Welcomed=True\nLanguage="+args[2]+"\nWpfDefaultsRevision=1\n");
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var runtimeType=assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime");object runtime=Activator.CreateInstance(runtimeType,Flags,null,new object[]{profile,true},null);var peers=new List<IDisposable>();
        try {
            var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=135 R=0}");Set(engine,"fullMapOpacity",100);Set(engine,"fullMapZoom",2f);Set(engine,"gridOpacity",20);Call(engine,"WpfRotate",true);Call(engine,"WpfAppearance","Circle",90);
            var position=Get(engine,"position");var point=(System.Drawing.PointF)engine.GetType().GetMethod("ToMap",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new[]{position});
            Call(engine,"WpfRotate",false);Set(engine,"fullMapZoom",1.45f);
            Call(runtime,"Refresh");var tactical=(Window)Get(runtime,"tactical");tactical.Width=1280;tactical.Height=820;
            Shot(tactical,Path.Combine(output,"地图.png"));
            var settings=(Window)Call(runtime,"BuildSettings",0);Shot(settings,Path.Combine(output,"设置.png"));settings.Close();
            var hud=(Window)Get(runtime,"hud");Shot(hud,Path.Combine(output,"小地图.png"),1.5);
        }finally{((IDisposable)runtime).Dispose();}
    }
}
