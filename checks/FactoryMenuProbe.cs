using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

class FactoryMenuProbe {
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object selectedZone;
    static void Zone<T>(T zone) { selectedZone=zone; }
    static object Get(object obj,string field) {return obj.GetType().GetField(field,All).GetValue(obj);}
    static object Call(Type type,string method,object instance,params object[] args) { return type.GetMethod(method,All).Invoke(instance,args); }
    [STAThread] static int Main(string[] args) {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var asm=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var router=asm.GetType("ScumMiniMap.NavigationRouter");
        var position=asm.GetType("ScumMiniMap.Position");
        var window=asm.GetType("ScumMiniMap.MapWindow");
        var surface=Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfMapSurface"),true);
        var type=surface.GetType();
        type.GetField("State",All).SetValue(surface,Activator.CreateInstance(asm.GetType("ScumMiniMap.WpfMapState"),true));
        var shared=type.GetField("NavigateShared",All);
        var capture=typeof(FactoryMenuProbe).GetMethod("Zone",All).MakeGenericMethod(shared.FieldType.GetGenericArguments()[0]);
        shared.SetValue(surface,Delegate.CreateDelegate(shared.FieldType,capture));
        PointF plain=PointF.Empty;
        type.GetField("NavigateAt",All).SetValue(surface,new Action<PointF>(p=>plain=p));
        Call(router,"Initialize",null);
        int total=0;
        foreach(var row in File.ReadLines(args[1]).Select(r=>r.Split('\t')).Where(r=>r[1]=="default"&&r[2]=="Quickest")){
            var target=new PointF(float.Parse(row[5]),float.Parse(row[6]));
            double x=617718-1521618*double.Parse(row[3]),y=618618-1523618*double.Parse(row[4]);
            var gps=Call(position,"Parse",null,string.Format("{{X={0:R} Y={1:R} Z={2}|P=0 Y=0 R=0}}",x,y,row[7]));
            var start=(PointF)Call(window,"ToMap",null,gps);
            var menu=(ContextMenu)Call(type,"BuildContextMenu",surface,target);
            var options=(double[])Call(router,"BridgeHeights",null,target);
            if(options.Length!=2)throw new Exception("No distinct lower and upper menu at "+target);
            for(int index=1;index<=3;index++){
                selectedZone=null;plain=PointF.Empty;
                ((MenuItem)menu.Items[index]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                double? height=selectedZone==null?(double?)null:(double?)Get(selectedZone,"RouteHeightCm");
                if((index==1&&(plain!=target||height!=null))||(index>=2&&height!=options[index-2]))throw new Exception("Menu callback mismatch "+index);
                var mode=Enum.Parse(asm.GetType("ScumMiniMap.RoadRouteMode"),"Quickest");
                var route=Call(router,"FindAtHeight",null,start,target,mode,null,(double)Get(gps,"Z"),height);
                int[] layers=(int[])Get(route,"BridgeLayers");
                bool arrives=(bool)Call(router,"SameSurface",null,target,options[0]+100,target,height);
                if(!(bool)Get(route,"Success")||layers[0]!=0||arrives!=(index!=3)||(index==3?layers.Last()<=0:layers.Any(v=>v!=0)))throw new Exception("Route or arrival layer mismatch "+target+" "+index);
                Console.WriteLine("{0} {1} GPS {2:F0},{3:F0},{4} target {5} lowerZ={6:F1} upperZ={7:F1} route={8:F2}m endLayer={9} lowerArrival={10}",row[0],new[]{"","plain","lower","upper"}[index],x,y,row[7],target,options[0],options[1],Get(route,"TotalDistanceMeters"),layers.Last(),arrives);
                total++;
            }
        }
        Console.WriteLine("PASS "+total+" WPF menu/GPS/arrival cases");return total==12?0:1;
    }
}
