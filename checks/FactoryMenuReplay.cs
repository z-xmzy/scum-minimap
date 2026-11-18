using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;

class FactoryMenuReplay {
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
    static PointF destination;
    static double? targetZ;
    static bool clicked;
    static void Plain(PointF point){destination=point;targetZ=null;clicked=true;}
    public static void Pick(object zone){
        var type=zone.GetType();
        destination=((PointF[])type.GetField("Points",F).GetValue(zone))[0];
        targetZ=(double?)type.GetField("RouteHeightCm",F).GetValue(zone);
        clicked=true;
    }
    [STAThread] static int Main(string[] args){
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var router=assembly.GetType("ScumMiniMap.NavigationRouter");
        router.GetMethod("Initialize",F).Invoke(null,null);
        var surface=assembly.GetType("ScumMiniMap.WpfMapSurface");
        var map=Activator.CreateInstance(surface,true);
        var state=Activator.CreateInstance(assembly.GetType("ScumMiniMap.WpfMapState"),true);
        surface.GetField("State",F).SetValue(map,state);
        surface.GetField("NavigateAt",F).SetValue(map,(Action<PointF>)Plain);
        var cb=surface.GetField("NavigateShared",F);
        cb.SetValue(map,Delegate.CreateDelegate(cb.FieldType,typeof(FactoryMenuReplay).GetMethod("Pick")));
        var language=assembly.GetType("ScumMiniMap.Localization").GetMethod("Get",F,null,new[]{typeof(string)},null);
        var begin=new PointF(.6817823f,.8584385f);
        var end=new PointF(.6820781f,.8591285f);
        var heights=(double[])router.GetMethod("BridgeHeights",F).Invoke(null,new object[]{begin});
        if(heights.Length!=2)return 2;
        var menu=(ContextMenu)surface.GetMethod("BuildContextMenu",F).Invoke(map,new object[]{end});
        var mode=Enum.Parse(assembly.GetType("ScumMiniMap.RoadRouteMode"),"Quickest");
        int failed=0;
        foreach(string key in new[]{"MapGpsDestination","BridgeLower","BridgeUpper"}){
            clicked=false;
            string header=(string)language.Invoke(null,new object[]{key});
            var item=menu.Items.OfType<MenuItem>().SingleOrDefault(m=>header.Equals(m.Header));
            if(item==null){Console.WriteLine("FAIL missing "+key);failed++;continue;}
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var route=router.GetMethod("FindAtHeight",F).Invoke(null,new object[]{begin,destination,mode,null,heights[0]+100,targetZ});
            var type=route.GetType();
            var layers=(int[])type.GetField("BridgeLayers",F).GetValue(route);
            bool ok=(bool)type.GetField("Success",F).GetValue(route);
            double metres=(double)type.GetField("TotalDistanceMeters",F).GetValue(route);
            bool pass=clicked&&destination==end&&ok&&layers!=null&&layers.Length>0&&layers[0]==0
                &&(key=="BridgeUpper"?layers.Last()>0&&metres>50:layers.All(x=>x==0)&&metres<40);
            Console.WriteLine("{0} {1}: z={2} length={3:F2}m layers={4}",pass?"PASS":"FAIL",key,targetZ,metres,layers==null?"":string.Join(",",layers.Distinct()));
            if(!pass)failed++;
        }
        return failed==0?0:1;
    }
}
