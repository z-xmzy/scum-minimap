using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScumMiniMap;
class BunkerRenderingTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static object Get(object o,string name) { return o.GetType().GetField(name,F).GetValue(o); }
    static void Set(object o,string name,object value) { o.GetType().GetField(name,F).SetValue(o,value); }
    static object Call(object o,string name,params object[] args) { return o.GetType().GetMethod(name,F).Invoke(o,args); }
    static void Check(bool ok,string name) { if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name); }
    [STAThread] static int Main(string[] args) {
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>new AssemblyName(e.Name).Name=="SkynettMiniMap"?Assembly.LoadFrom(args[1]):null;
        try { Run(args);return 0; }catch(Exception e) { while(e.InnerException!=null)e=e.InnerException;Console.WriteLine("FAIL "+e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 1; }
    }
    static void Run(string[] args) {
        Directory.CreateDirectory(args[0]);var assembly=typeof(MapWindow).Assembly;
        var atlas=assembly.GetType("ScumMiniMap.BunkerAtlas").GetMethod("Load",F).Invoke(null,new object[]{Path.Combine(Path.GetDirectoryName(assembly.Location),"assets","bunkers")});
        string expected=Environment.GetEnvironmentVariable("SCUM_TEST_BUNKER")??"B_2_Research_Facility";
        var floor=((Array)Get(atlas,"Floors")).Cast<object>().Where(f=>(string)Get(f,"BunkerId")==expected).OrderBy(f=>expected=="B_2_Research_Facility"?(double)Get(f,"SurfaceZ"):-((float[])Get(f,"Tiles")).Length).First();
        // Select a model grid cell, not a captured player report.
        var offsets=(int[])Get(floor,"TileOffsets");var tiles=(float[])Get(floor,"Tiles");int width=(int)Get(floor,"Width"),columns=(width+7)/8;
        int tileIndex=Array.FindIndex(offsets,v=>v>=0);int offset=offsets[tileIndex];int cell=0;while(float.IsNaN(tiles[offset+cell]))cell++;
        int row=(tileIndex/columns)*8+cell/8,col=(tileIndex%columns)*8+cell%8;
        double step=(double)Get(floor,"CellSize");var p=new Position { X=(double)Get(floor,"MaxX")-(col+.5)*step,Y=(double)Get(floor,"MaxY")-(row+.5)*step,Z=tiles[offset+cell]+92,Yaw=40 };
        if(args.Length==5) { p.X=double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture);p.Y=double.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture);p.Z=double.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture); }
        using(var window=new MapWindow(Path.Combine(args[0],"profile"),true)) {
            Set(window,"position",p);Set(window,"updated",DateTime.UtcNow.AddMilliseconds(-600));
            var load=(System.Threading.Tasks.Task)Get(window,"bunkerLoad");Check(load.Wait(10000),"asynchronous atlas completes");
            Call(window,"ObserveBunker");Set(window,"updated",DateTime.UtcNow);Call(window,"ObserveBunker");
            var first=Call(window,"WpfSnapshot",true);
            object state=first;
            for(int i=0;i<100&&Get(state,"BunkerImage")==null;i++) { Thread.Sleep(20);state=Call(window,"WpfSnapshot",true); }
            Check(Get(state,"BunkerImage")!=null,"validated floor image completes asynchronously");
            Check((string)Get(Get(state,"Bunker"),"BunkerId")==expected,"correct bunker selected");
            Check(Get(state,"Store")==null&&Get(state,"Route")==null,"surface POIs and roads absent indoors");
            double standing=p.Z;p.Z+=160;Set(window,"updated",DateTime.UtcNow.AddMilliseconds(100));
            var historyPoint=(System.Drawing.PointF)typeof(MapWindow).GetMethod("ToMap",F).Invoke(null,new object[]{p});
            Call(window,"RecordLocationHistory",historyPoint,DateTime.UtcNow);
            var recorded=((System.Collections.IEnumerable)Get(window,"locationHistoryPoints")).Cast<object>().Last();
            Check(((string)Get(recorded,"Context"))=="default/"+(string)Get(Get(state,"Bunker"),"Id"),"brief indoor jump keeps history on the bunker view");
            p.Z=standing;Set(window,"updated",DateTime.UtcNow.AddMilliseconds(300));Call(window,"ObserveBunker");
            var current=Get(state,"Bunker");var overviewPan=(System.Drawing.PointF)Get(state,"Pan");
            double centreX=(617718-((double)Get(current,"MinX")+(double)Get(current,"MaxX"))/2)/1521618.0;
            double centreY=(618618-((double)Get(current,"MinY")+(double)Get(current,"MaxY"))/2)/1523618.0;
            Check(Math.Abs(overviewPan.X-centreX)<1e-6&&Math.Abs(overviewPan.Y-centreY)<1e-6,"default overview centres the floor even at an edge entrance");
            var overviewType=assembly.GetType("ScumMiniMap.WpfMapSurface");
            double overviewSide=(double)overviewType.GetMethod("MapSide",F).Invoke(null,new object[]{state,1000.0,720.0,false});
            double fraction=Math.Max(overviewSide*((double)Get(current,"MaxX")-(double)Get(current,"MinX"))/1521618.0/1000,overviewSide*((double)Get(current,"MaxY")-(double)Get(current,"MinY"))/1523618.0/720);
            Check(fraction>.89&&fraction<.95,"overview fits viewport with a small margin");
            var overview=(FrameworkElement)Activator.CreateInstance(overviewType,true);Set(overview,"DirectTerrain",true);Set(overview,"State",state);
            overview.Measure(new Size(1000,720));overview.Arrange(new Rect(0,0,1000,720));Call(overview,"PublishState",state);overview.UpdateLayout();
            Save(overview,Path.Combine(args[0],"bunker-overview.png"),1000,720);
            var views=((Array)Call(window,"WpfBunkerViews")).Cast<object>().ToArray();
            foreach(var view in views) {
                Call(window,"WpfSelectBunkerView",Get(view,"Id"));
                for(int i=0;i<100;i++) {state=Call(window,"WpfSnapshot",true);if(Get(state,"BunkerImage")!=null)break;Thread.Sleep(20);}
                Check(Get(state,"BunkerImage")!=null&&Get(state,"Bunker")==view,"manual floor selection loads the requested view "+Get(view,"Rank"));
                Set(overview,"State",state);Call(overview,"PublishState",state);overview.UpdateLayout();
                Save(overview,Path.Combine(args[0],"bunker-view-"+Get(view,"Rank")+".png"),1000,720);
                var large=(FrameworkElement)Activator.CreateInstance(overviewType,true);Set(large,"DirectTerrain",true);Set(large,"State",state);
                large.Measure(new Size(1800,1100));large.Arrange(new Rect(0,0,1800,1100));Call(large,"PublishState",state);large.UpdateLayout();
                Save(large,Path.Combine(args[0],"bunker-view-"+Get(view,"Rank")+"-large.png"),1800,1100);
                if(Get(view,"Id").Equals(Get(first,"BunkerCurrentView")))continue;
                Check(!Get(view,"Id").Equals(Get(state,"BunkerCurrentView")),"manual browsing preserves the player's actual floor identity");
            }
            Call(window,"WpfSelectBunkerView",new object[]{null});state=Call(window,"WpfSnapshot",true);
            Check(Get(Get(state,"Bunker"),"Id").Equals(Get(state,"BunkerCurrentView")),"automatic mode returns to the player's current floor");
            var worldZoom=Get(window,"fullMapZoom");Call(window,"WpfZoom",true,2);state=Call(window,"WpfSnapshot",true);
            Check((float)Get(state,"Zoom")>1&&Equals(Get(window,"fullMapZoom"),worldZoom),"indoor zoom preserves world zoom");
            var type=assembly.GetType("ScumMiniMap.WpfMapSurface");var surface=(FrameworkElement)Activator.CreateInstance(type,true);
            Set(surface,"DirectTerrain",true);Set(surface,"State",state);
            surface.Measure(new Size(1000,720));surface.Arrange(new Rect(0,0,1000,720));Call(surface,"PublishState",state);surface.UpdateLayout();
            object[] hit={new Point(500,360),new System.Drawing.PointF()};
            bool hitOk=(bool)type.GetMethod("TryMapPoint",F).Invoke(surface,hit);var mapped=(System.Drawing.PointF)hit[1];
            Check(hitOk&&Math.Abs(mapped.X-(617718-p.X)/1521618.0)<1e-6&&Math.Abs(mapped.Y-(618618-p.Y)/1523618.0)<1e-6,"zoomed click maps back to world coordinates");
            Save(surface,Path.Combine(args[0],"bunker-full.png"),1000,720);
            // Preserve saved point markers and the same player direction in the compact view.
            var compact=Call(window,"WpfSnapshot",false);
            double miniSide=(double)type.GetMethod("MapSide",F).Invoke(null,new object[]{compact,360.0,360.0,true});
            double visibleCm=360/miniSide*1521618.0;
            Check(visibleCm<=4000&&visibleCm>=1400,"minimap defaults to nearby rooms, not entire bunker");
            Set(surface,"Compact",true);Set(surface,"State",compact);
            surface.Measure(new Size(360,360));surface.Arrange(new Rect(0,0,360,360));surface.InvalidateVisual();surface.UpdateLayout();
            Save(surface,Path.Combine(args[0],"bunker-mini.png"),360,360);
            var runtimeType=assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime");
            var runtime=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(runtimeType);
            Set(runtime,"engine",window);Set(runtime,"application",Application.Current??new Application());Set(runtime,"disposed",true);
            var filters=(Window)Call(runtime,"BuildPoiFilters");var filterRoot=(System.Windows.Controls.DockPanel)filters.Content;
            var footer=(System.Windows.Controls.StackPanel)filterRoot.Children[0];
            foreach(var button in footer.Children.OfType<System.Windows.Controls.Button>().Take(2))button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            filters.Close();Check(true,"indoor POI select-all and deselect-all callbacks do not crash");
            double radius=Convert.ToDouble(Call(surface,"MarkerHitRadius"));
            Check(Math.Abs(radius-12/miniSide)<1e-9,"marker hit radius covers the icon using indoor scale");
            var store=Get(window,"scumMap");var marker=((System.Collections.IEnumerable)Get(store,"Markers")).Cast<object>().First();
            var markerPoint=new System.Drawing.PointF(Convert.ToSingle(Get(marker,"X")),Convert.ToSingle(Get(marker,"Y")));
            var hiddenTarget=Call(window,"WpfClickedTarget",markerPoint,.000001f);
            Check(!(bool)Get(hiddenTarget,"IsMapObject"),"hidden surface POI cannot name an indoor target");
            Set(window,"searchTarget",new MapZone { Name="Synthetic destination",Points=new[]{mapped} });
            state=Call(window,"WpfSnapshot",true);
            Check(!((string)Get(state,"RouteSummary")).Contains(ScumMiniMap.Localization.Get("NavigationEta")),"indoor target has no stale surface ETA");
            Set(state,"RouteSummary","STALE SQUAD ETA");Set(state,"Calculating",true);
            typeof(MapWindow).GetMethod("BunkerRouteState",F).Invoke(null,new[]{state});
            Check(!((string)Get(state,"RouteSummary")).Contains("STALE")&&!(bool)Get(state,"Calculating"),"post-composition squad road summary is removed indoors");
            Check((bool)Call(window,"WpfWaypoint","Synthetic bunker marker",null),"one-click current-position marker saves indoors");
            var saved=(MapZone[])Get(Call(window,"WpfSnapshot",true),"Saved");
            Check(saved.Any(z=>z.Name=="Synthetic bunker marker"&&Math.Abs(z.Centroid.X-mapped.X)<1e-6),"indoor marker retains world-coordinate alignment");
            Set(window,"position",new Position { X=500000,Y=500000,Z=50000 });Set(window,"updated",DateTime.UtcNow.AddMilliseconds(1));Call(window,"ObserveBunker");
            var outside=Call(window,"WpfSnapshot",true);Check(Get(outside,"Bunker")==null&&Equals(Get(outside,"Zoom"),worldZoom),"teleport restores world map and zoom");
        }
    }
    static void Save(FrameworkElement visual,string path,int w,int h) {
        var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        byte[] bytes=new byte[w*h*4];bitmap.CopyPixels(bytes,w*4,0);
        Check(bytes.Distinct().Count()>30,"render has visible geometry and player: "+Path.GetFileName(path));
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);
    }
}
