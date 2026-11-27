using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DrawingPoint=System.Drawing.PointF;

namespace ScumMiniMap {
    // Heading strip across the top of the tactical theme's full map.
    internal sealed class WpfCompassTape:FrameworkElement {
        static readonly string[] Cardinals={"N","NE","E","SE","S","SW","W","NW"};
        double heading=double.NaN;
        internal double Heading { get { return heading; } set { if(value.Equals(heading))return;heading=value;InvalidateVisual(); } }
        internal static string Cardinal(double degrees) { return Cardinals[(int)Math.Round(Normal(degrees)/45)%8]; }
        internal static double Normal(double degrees) { return (degrees%360+360)%360; }
        protected override Size MeasureOverride(Size available) { return new Size(double.IsInfinity(available.Width)?460:Math.Min(460,available.Width),56); }
        protected override void OnRender(DrawingContext dc) {
            var t=WpfFieldTheme.Current;double w=ActualWidth,band=30;if(w<=0)return;
            var panel=t.Colour(t.Panel);
            var fade=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,0) };
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(0,panel.R,panel.G,panel.B),0));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(205,panel.R,panel.G,panel.B),.2));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(205,panel.R,panel.G,panel.B),.8));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(0,panel.R,panel.G,panel.B),1));
            dc.DrawRectangle(fade,null,new Rect(0,0,w,band));
            double h=double.IsNaN(heading)?0:Normal(heading),ppd=w/130;
            var face=new Typeface(WpfFieldTheme.DisplayFont,FontStyles.Normal,FontWeights.Bold,FontStretches.Normal);
            var minor=new Pen(t.Brush(t.Line2),1);var major=new Pen(t.Brush(t.Muted),1.2);
            for(int d=(int)Math.Floor((h-65)/5)*5;d<=h+65;d+=5) {
                double x=w/2+(d-h)*ppd;if(x<0||x>w)continue;
                int n=(int)Normal(d);double edge=Math.Max(0,Math.Min(1,Math.Min(x,w-x)/(w*.16)));
                dc.PushOpacity(edge);
                if(n%45==0) {
                    dc.DrawLine(major,new Point(x,band-9),new Point(x,band));
                    var label=new FormattedText(Cardinals[n/45],CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,n%90==0?15:13,n==0?t.Brush(t.Accent):t.Brush(t.Ink),1.0);
                    dc.DrawText(label,new Point(x-label.Width/2,2));
                } else if(n%15==0) {
                    dc.DrawLine(major,new Point(x,band-6),new Point(x,band));
                    var label=new FormattedText(n.ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,11,t.Brush(t.Muted),1.0);
                    dc.DrawText(label,new Point(x-label.Width/2,5));
                } else dc.DrawLine(minor,new Point(x,band-4),new Point(x,band));
                dc.Pop();
            }
            var marker=new StreamGeometry();
            using(var g=marker.Open()) { g.BeginFigure(new Point(w/2-5,band),true,true);g.LineTo(new Point(w/2+5,band),true,false);g.LineTo(new Point(w/2,band-6),true,false); }
            marker.Freeze();dc.DrawGeometry(t.Brush(t.Accent),null,marker);
            string readout=double.IsNaN(heading)?"—":((int)Math.Round(h)%360).ToString(CultureInfo.InvariantCulture)+"°  "+Cardinal(h);
            var value=new FormattedText(readout,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,15,t.Brush(t.AccentInk),1.0);
            double bw=value.Width+20,bh=22,top=band+2;
            dc.DrawRectangle(t.Brush(t.Accent),null,new Rect(w/2-bw/2,top,bw,bh));
            dc.DrawText(value,new Point(w/2-value.Width/2,top+(bh-value.Height)/2));
        }
    }

    // Five rising bars that read the zoom level at a glance (1x … 32x, doubling per bar).
    internal sealed class WpfZoomBars:FrameworkElement {
        int level=-1;
        internal int Level { get { return level; } set { value=Math.Max(0,Math.Min(5,value));if(value==level)return;level=value;InvalidateVisual(); } }
        protected override Size MeasureOverride(Size available) { return new Size(32,16); }
        protected override void OnRender(DrawingContext dc) {
            var t=WpfFieldTheme.Current;
            for(int i=0;i<5;i++) {
                double height=6+i*2.5,x=i*6.5;
                var bar=new StreamGeometry();
                using(var g=bar.Open()) { g.BeginFigure(new Point(x+1.5,16-height),true,true);g.LineTo(new Point(x+5,16-height),true,false);g.LineTo(new Point(x+3.5,16),true,false);g.LineTo(new Point(x,16),true,false); }
                bar.Freeze();dc.DrawGeometry(i<level?t.Brush(t.Accent):t.Brush(t.Line2),null,bar);
            }
        }
    }

    // Full-map chrome for the three interface themes: overlay cards, floating dock,
    // page panel and icon rail. Every control keeps its established field, Tag and callback.
    internal sealed partial class WpfFieldConsoleRuntime {
        readonly Dictionary<string,Button> sidebarTabButtons=new Dictionary<string,Button>();
        Button deathRemovalTool;
        TextBlock navDistance,navDistanceUnit,navMinutes,navMinutesUnit,navRouteKind,navNote;
        FrameworkElement navStats;
        Border squadCard;StackPanel squadRows;TextBlock squadOnline;string squadCardIdentity;
        readonly List<Button> squadPings=new List<Button>();
        readonly Dictionary<string,TextBlock[]> squadRowLabels=new Dictionary<string,TextBlock[]>(StringComparer.Ordinal);
        WpfCompassTape compassTape;WpfZoomBars zoomBars;
        TextBlock topSector,topLocation,topHeading,statusZoom,statusSquad,paneTitle,searchKeyLabel;
        WrapPanel keyHints;string keyHintIdentity;
        UniformGrid poiTiles;double lastPaneWidth=300;
        readonly HashSet<string> tileExpanded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static WpfThemeSpec Theme { get { return WpfFieldTheme.Current; } }
        // Overlay card. The outer Border keeps the established Border fields and visibility
        // switches; the inner surface draws the theme's chamfered or rounded panel.
        static Border Shell(UIElement content,Thickness padding,bool accentBar=false) {
            var surface=WpfFieldTheme.Surface(content,padding);
            if(accentBar&&Theme.Chamfer>0) { surface.AccentBrush=WpfFieldTheme.Accent;surface.AccentWidth=3; }
            return new Border { Child=surface,Margin=new Thickness(0,0,0,10) };
        }
        static Button IconButton(string icon,string tag,double size=36,double glyph=18) {
            var button=new Button { Width=size,Height=size,MinHeight=size,Padding=new Thickness(0),Margin=new Thickness(0),Foreground=WpfFieldTheme.Muted,Tag=tag,Focusable=true };
            button.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");
            var path=WpfIcons.Create(icon,glyph,WpfFieldTheme.Muted);
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") { Source=button });
            button.Content=path;
            if(tag!=null&&tag.StartsWith("tooltip:",StringComparison.Ordinal)) { string text=Localization.Get(tag.Substring(8));button.ToolTip=text;System.Windows.Automation.AutomationProperties.SetName(button,text); }
            return button;
        }
        static void SetIcon(Button button,string icon,double glyph) {
            var path=WpfIcons.Create(icon,glyph,WpfFieldTheme.Muted);
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") { Source=button });
            button.Content=path;
        }
        // Text action in the active theme: ghost by default, filled for primary, tinted for danger.
        static Button ActionButton(string key,Action action,string icon,bool primary=false,bool danger=false) {
            var t=Theme;var button=WpfFieldTheme.Button(Localization.Get(key),action,primary);
            button.Tag="text:"+key;button.FontSize=13;button.MinHeight=32;button.Padding=new Thickness(11,4,12,4);button.Margin=new Thickness(0,0,6,6);
            if(danger) { button.Foreground=WpfFieldTheme.Warn;button.Background=t.Alpha(t.Danger,0x1F);button.BorderBrush=t.Alpha(t.Danger,0x55); }
            if(t.Uppercase) { button.FontFamily=WpfFieldTheme.DisplayFont;button.FontWeight=FontWeights.Bold;button.FontSize=14; }
            if(icon!=null)WpfFieldTheme.IconLabel(button,icon,false,15);
            return button;
        }
        static Border KeyCap(string key,bool accent=false) {
            var t=Theme;var text=WpfFieldTheme.Text(key,11,accent?WpfFieldTheme.AccentInk:WpfFieldTheme.Ink,true);
            text.FontFamily=t.Uppercase?WpfFieldTheme.DisplayFont:WpfFieldTheme.MonoFont;if(t.Uppercase) { text.FontSize=12;text.FontWeight=FontWeights.Bold; }
            text.HorizontalAlignment=HorizontalAlignment.Center;text.TextTrimming=TextTrimming.None;
            return new Border { Child=text,MinWidth=22,Height=20,Padding=new Thickness(5,0,5,0),Background=accent?WpfFieldTheme.Accent:WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line2,BorderThickness=new Thickness(accent?0:1),
                CornerRadius=new CornerRadius(Math.Min(t.ControlRadius,5)),VerticalAlignment=VerticalAlignment.Center };
        }
        static FrameworkElement SectionHeader(string text,UIElement right=null) {
            var t=Theme;var row=new DockPanel { Margin=new Thickness(0,16,0,8) };
            if(right!=null) { DockPanel.SetDock(right,Dock.Right);row.Children.Add(right); }
            if(t.Chamfer>0) { var bar=new Border { Width=3,Height=13,Background=WpfFieldTheme.Accent,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center };DockPanel.SetDock(bar,Dock.Left);row.Children.Add(bar); }
            var label=WpfFieldTheme.Text(t.Caps(text),t.Uppercase?14:12,t.Uppercase?WpfFieldTheme.Ink:WpfFieldTheme.Muted,true);label.TextWrapping=TextWrapping.Wrap;label.TextTrimming=TextTrimming.None;
            if(t.Uppercase) { label.FontFamily=WpfFieldTheme.DisplayFont;label.FontWeight=FontWeights.Bold; }
            row.Children.Add(label);return row;
        }
        static Border Divider(bool vertical) {
            return vertical?new Border { Width=1,Height=20,Background=WpfFieldTheme.Line,Margin=new Thickness(6,0,6,0),VerticalAlignment=VerticalAlignment.Center }
                :new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(0,10,0,10) };
        }
        // Glass rows carry a tinted icon tile; the other themes use a quiet line icon.
        static string LayerTint(string key) {
            switch(key) { case "zones":return "#8A6CFF";case "waypoints":return "#FF7A59";case "grid":return "#9CA5BA";case "history":return "#3DDC97";case "labels":return "#4C9BFF";default:return null; }
        }
        static void RowIcon(ContentControl control,string icon,string tint) {
            // DockPanel (not a horizontal stack) so the label gets a real width and wraps in narrow panels.
            var t=Theme;var row=new FrameworkElementFactory(typeof(DockPanel));row.SetValue(DockPanel.LastChildFillProperty,true);
            var path=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            double size=t.GradientAccent?15:16;
            path.SetValue(System.Windows.Shapes.Path.DataProperty,WpfIcons.Geometry(icon,size));
            path.SetValue(FrameworkElement.WidthProperty,size);path.SetValue(FrameworkElement.HeightProperty,size);
            path.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty,1.6);
            path.SetValue(System.Windows.Shapes.Shape.StrokeStartLineCapProperty,PenLineCap.Round);path.SetValue(System.Windows.Shapes.Shape.StrokeEndLineCapProperty,PenLineCap.Round);
            path.SetValue(System.Windows.Shapes.Shape.StrokeLineJoinProperty,PenLineJoin.Round);
            path.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            if(t.GradientAccent&&tint!=null) {
                path.SetValue(System.Windows.Shapes.Shape.StrokeProperty,t.Brush(tint));
                var tile=new FrameworkElementFactory(typeof(Border));
                tile.SetValue(FrameworkElement.WidthProperty,26.0);tile.SetValue(FrameworkElement.HeightProperty,26.0);
                tile.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));tile.SetValue(Border.BackgroundProperty,t.Alpha(tint,0x2E));
                tile.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,11,0));tile.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
                path.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
                tile.SetValue(DockPanel.DockProperty,Dock.Left);tile.AppendChild(path);row.AppendChild(tile);
            } else {
                path.SetValue(System.Windows.Shapes.Shape.StrokeProperty,WpfFieldTheme.Muted);
                path.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,11,0));path.SetValue(DockPanel.DockProperty,Dock.Left);row.AppendChild(path);
            }
            var label=new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding());
            label.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            row.AppendChild(label);control.ContentTemplate=new DataTemplate { VisualTree=row };
        }
        // Search opens the place/grid finder; it looks like an input so the action is obvious.
        Button SearchBar(int key,out TextBlock keyLabel) {
            var t=Theme;var row=new DockPanel();
            var cap=KeyCap(key==0?"":PhysicalKeyCapture.KeyName(key));keyLabel=(TextBlock)cap.Child;keyLabel.Foreground=WpfFieldTheme.Muted;
            DockPanel.SetDock(cap,Dock.Right);row.Children.Add(cap);
            var icon=WpfIcons.Create("search",16,WpfFieldTheme.Muted);icon.Margin=new Thickness(0,0,10,0);icon.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
            var hint=WpfFieldTheme.Text(Localization.Get("SearchTitle"),14,WpfFieldTheme.Muted);hint.Tag="text:SearchTitle";row.Children.Add(hint);
            var search=new Button { Content=row,HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=t.GradientAccent?WpfFieldTheme.Glass:WpfFieldTheme.Current.Brush(t.Input),BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),
                Padding=new Thickness(12,0,8,0),MinHeight=42,Margin=new Thickness(0,0,0,10),Tag="sidebar-search",Cursor=Cursors.IBeam };
            System.Windows.Automation.AutomationProperties.SetName(search,Localization.Get("SearchTitle"));
            search.Click+=(s,e)=>ShowSearch();return search;
        }

        Window BuildTactical() {
            var t=Theme;bool floating=t.Tabs!=WpfTabStyle.Rail;
            var window=Window("SCUM MiniMap",1280,850);window.MinWidth=900;window.MinHeight=620;window.ResizeMode=ResizeMode.CanResize;window.Topmost=true;window.ShowActivated=false;
            bool solidTest=Program.IsTestBuild&&(Environment.GetCommandLineArgs().Any(arg=>string.Equals(arg,"--fast-tac-test",StringComparison.OrdinalIgnoreCase))||Environment.GetEnvironmentVariable("SCUM_MINIMAP_TAC_OPAQUE")=="1");
            window.AllowsTransparency=!solidTest;window.Background=solidTest?WpfFieldTheme.Ground:Brushes.Transparent;
            window.SourceInitialized+=(s,e)=> {
                var handle=new WindowInteropHelper(window).Handle;
                SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000000);
                HwndSource.FromHwnd(handle).AddHook(TacticalWindowMessage);
            };
            // The panel column sits on the right; the minimal theme mirrors it to the left.
            int shelfColumn=t.Mirror?0:1,mapColumn=1-shelfColumn;
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions[shelfColumn].Width=new GridLength(340);
            tacticalMap=new WpfMapSurface { BaseMap=geography,Terrain=terrain };
            tacticalMap.DirectTerrain=solidTest&&(RenderCapability.Tier>>16)>0&&RenderOptions.ProcessRenderMode!=RenderMode.SoftwareOnly&&Environment.GetEnvironmentVariable("SCUM_MINIMAP_TAC_RASTER")!="1";
            tacticalMap.RouteStopAt=AddSquadStop;tacticalMap.RouteDestination=AddSquadDestination;tacticalMap.PingAt=(kind,p)=>SendPing(kind,p);tacticalMap.PingAvailable=CanSendPing;tacticalMap.RoutesRequested=ShowRoutes;
            tacticalMap.RouteNavigate=StartSquadRoute;tacticalMap.RouteRemove=RemoveOwnRouteStop;tacticalMap.RouteKind=ChangeOwnRouteKind;tacticalMap.RouteUndo=UndoOwnRouteStop;tacticalMap.RouteClear=ClearOwnRoute;tacticalMap.NavigatePeer=FollowSquadPeer;
            tacticalMap.ClearRouteRequested=ClearMapRoute;tacticalMap.StopNavigationRequested=()=> { StopSquadNavigation();Refresh(); };tacticalMap.DeleteWaypointRequested=marker=> { engine.WpfDeleteWaypoint(marker);Refresh(); };
            tacticalMap.NavigateAt=p=>NavigateDestination(engine.WpfClickedTarget(p,tacticalMap.MarkerHitRadius()));tacticalMap.WaypointAt=p=>ShowAddWaypoint(p);tacticalMap.ZoomRequested=d=>QueueZoom(d,true);tacticalMap.PanChanged=p=> { engine.WpfPan(p);RefreshCamera(); };
            tacticalMap.NavigateShared=NavigateDestination;
            tacticalMap.AdminMenuAt=point=> {
                var actions=engine.WpfMapAdminActions(point);if(actions.Length==0)return null;
                var root=new MenuItem { Header=Localization.Get("ControlAdminCommands") };
                WpfMapSurface.AddAdminContextItems(root.Items,actions);return root;
            };
            // Native-map geometry spans both columns. Floating panels also leave the software
            // map full width underneath them; the docked minimal sidebar takes its own column.
            var scene=new Grid();Grid.SetColumnSpan(scene,2);scene.Children.Add(tacticalMap);
            var overlay=new Grid();scene.Children.Add(overlay);
            var contextLayer=new Canvas();tacticalMap.ContextHost=contextLayer;Grid.SetColumnSpan(contextLayer,2);Panel.SetZIndex(contextLayer,200);grid.Children.Add(contextLayer);
            window.PreviewMouseDown+=(s,e)=> { if(tacticalMap.DismissContextOutside(e.GetPosition(contextLayer))&&e.ChangedButton==MouseButton.Left)e.Handled=true; };

            // ── Navigation card ──────────────────────────────────────────────
            var navBody=new StackPanel();
            var navHead=new DockPanel { Margin=new Thickness(0,0,0,t.Uppercase?0:4) };
            var clear=WpfFieldTheme.Button(Localization.Get("WpfClearRoute"),ClearMapRoute);clear.Tag="text:WpfClearRoute";
            clear.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");clear.FontSize=12;clear.MinHeight=26;clear.Padding=new Thickness(7,2,8,2);clear.Margin=new Thickness(6,-4,-6,0);
            WpfFieldTheme.IconLabel(clear,"x",false,13);DockPanel.SetDock(clear,Dock.Right);navHead.Children.Add(clear);
            if(t.Uppercase) {
                var eyebrow=WpfFieldTheme.Text(t.Caps(Localization.Get("FieldTarget")),14,WpfFieldTheme.Accent,true);eyebrow.FontFamily=WpfFieldTheme.DisplayFont;eyebrow.FontWeight=FontWeights.Bold;eyebrow.Tag="caps:FieldTarget";navHead.Children.Add(eyebrow);
            } else if(t.GradientAccent) {
                var row=new StackPanel { Orientation=Orientation.Horizontal };row.Children.Add(new System.Windows.Shapes.Ellipse { Width=7,Height=7,Fill=WpfFieldTheme.Accent,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center });
                var eyebrow=WpfFieldTheme.Text(Localization.Get("NavActive"),12,WpfFieldTheme.Muted,true);eyebrow.Tag="text:NavActive";row.Children.Add(eyebrow);navHead.Children.Add(row);
            } else {
                var row=new StackPanel { Orientation=Orientation.Horizontal };var icon=WpfIcons.Create("navigation-2",14,WpfFieldTheme.Ink);icon.Margin=new Thickness(0,0,8,0);icon.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(icon);
                var eyebrow=WpfFieldTheme.Text(Localization.Get("FieldTarget"),13,WpfFieldTheme.Ink,true);eyebrow.Tag="text:FieldTarget";row.Children.Add(eyebrow);
                var badgeText=WpfFieldTheme.Text(Localization.Get("NavActive"),11,WpfFieldTheme.Accent,true);badgeText.Tag="text:NavActive";
                row.Children.Add(new Border { Child=badgeText,Background=WpfFieldTheme.AccentDim,CornerRadius=new CornerRadius(5),Padding=new Thickness(7,1,7,2),Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center });
                navHead.Children.Add(row);
            }
            navBody.Children.Add(navHead);
            targetTitle=WpfFieldTheme.Text("",t.Uppercase?36:20,null,true);
            if(t.Uppercase) { targetTitle.FontFamily=WpfFieldTheme.DisplayFont;targetTitle.FontWeight=t.DisplayWeight;targetTitle.Margin=new Thickness(0,-4,0,-2); }
            navBody.Children.Add(targetTitle);
            targetDetail=WpfFieldTheme.Text("",13,WpfFieldTheme.Muted);targetDetail.TextWrapping=TextWrapping.Wrap;targetDetail.TextTrimming=TextTrimming.None;targetDetail.Margin=new Thickness(0,2,0,0);navBody.Children.Add(targetDetail);
            navNote=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);navNote.TextWrapping=TextWrapping.Wrap;navNote.TextTrimming=TextTrimming.None;navNote.Margin=new Thickness(0,2,0,0);navNote.Visibility=Visibility.Collapsed;navBody.Children.Add(navNote);
            navStats=BuildNavStats();navBody.Children.Add(navStats);
            var targetActions=new WrapPanel { Margin=new Thickness(0,10,0,-6) };
            addTargetStop=ActionButton("SquadAddStop",()=>AddSquadDestination(engine.WpfSnapshot(true).Target),"plus");addTargetStop.ToolTip=S("MapGpsSavedHelp");targetActions.Children.Add(addTargetStop);
            var manageTarget=ActionButton("RouteList",ShowRoutes,"route");targetActions.Children.Add(manageTarget);
            var stopTarget=ActionButton("SquadStopNav",()=> { StopSquadNavigation();filterIdentity=null;Refresh(); },"circle-x",false,true);targetActions.Children.Add(stopTarget);
            navBody.Children.Add(targetActions);
            targetCard=Shell(navBody,t.Uppercase?new Thickness(18,14,16,16):new Thickness(16,14,16,16),true);
            targetCard.PreviewMouseRightButtonDown+=(s,e)=> { if(tacticalMap.BeginContextPointer(e.GetPosition(tacticalMap)))e.Handled=true; };

            // ── Idle location, planned route preview, hover details, squad ──
            var idleLocation=WpfFieldTheme.Text("",14,WpfFieldTheme.Ink,true);idleLocation.TextWrapping=TextWrapping.Wrap;
            if(t.Uppercase) { idleLocation.FontFamily=WpfFieldTheme.DisplayFont;idleLocation.FontSize=16; }
            nativeLocationCard=Shell(idleLocation,new Thickness(12,8,12,8),true);nativeLocationCard.Visibility=Visibility.Collapsed;
            var preview=new StackPanel();routePreviewTitle=WpfFieldTheme.Text("",t.Uppercase?18:15,null,true);if(t.Uppercase)routePreviewTitle.FontFamily=WpfFieldTheme.DisplayFont;preview.Children.Add(routePreviewTitle);
            routePreviewDetail=WpfFieldTheme.Text("",13,WpfFieldTheme.Muted);routePreviewDetail.TextWrapping=TextWrapping.Wrap;routePreviewDetail.TextTrimming=TextTrimming.None;routePreviewDetail.Margin=new Thickness(0,3,0,0);preview.Children.Add(routePreviewDetail);
            var previewActions=new WrapPanel { Margin=new Thickness(0,10,0,-6) };
            previewActions.Children.Add(ActionButton("SquadStartRoute",()=>StartSquadRoute(squad.Stops),"play",true));
            previewActions.Children.Add(ActionButton("WpfClearRoute",ClearMapRoute,"x"));
            previewActions.Children.Add(ActionButton("RouteList",ShowRoutes,"route"));
            preview.Children.Add(previewActions);
            routePreviewCard=Shell(preview,new Thickness(16,12,16,16),true);routePreviewCard.Visibility=Visibility.Collapsed;
            routePreviewCard.PreviewMouseRightButtonDown+=(s,e)=> { if(tacticalMap.BeginContextPointer(e.GetPosition(tacticalMap)))e.Handled=true; };
            hoverInfo=WpfFieldTheme.Text("",14);hoverInfo.TextWrapping=TextWrapping.Wrap;hoverInfo.TextTrimming=TextTrimming.None;
            hoverCard=Shell(new ScrollViewer { Content=hoverInfo,MaxHeight=240,VerticalScrollBarVisibility=ScrollBarVisibility.Auto },new Thickness(14,10,14,10));hoverCard.Visibility=Visibility.Collapsed;tacticalMap.HoverChanged=ShowHoverDetails;
            // The details card is an overlay on the map, not a dead area for its
            // context menu. Preserve normal left-click/scroll behavior in it.
            hoverCard.PreviewMouseRightButtonDown+=(s,e)=> { if(tacticalMap.BeginContextPointer(e.GetPosition(tacticalMap)))e.Handled=true; };
            squadCard=BuildSquadCard();
            var topCards=new StackPanel { Width=392,HorizontalAlignment=t.Mirror?HorizontalAlignment.Right:HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(18) };
            if(t.GradientAccent) { var searchBar=SearchBar(0,out searchKeyLabel);topCards.Children.Add(searchBar); }
            foreach(var card in new UIElement[]{nativeLocationCard,targetCard,routePreviewCard,hoverCard,squadCard})topCards.Children.Add(card);
            overlay.Children.Add(topCards);

            // ── Dock: zoom, follow and mark-position ─────────────────────────
            var controls=new WrapPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            // Zoom and actions are two unbreakable groups: the dock may only wrap between them.
            var zoomGroup=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };var actionGroup=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            controls.Children.Add(zoomGroup);controls.Children.Add(actionGroup);
            if(t.Uppercase) {
                var zoomLabel=WpfFieldTheme.Text(t.Caps(Localization.Get("HintZoom")),13,WpfFieldTheme.Muted,true);zoomLabel.FontFamily=WpfFieldTheme.DisplayFont;zoomLabel.Tag="caps:HintZoom";zoomLabel.Margin=new Thickness(4,0,8,0);zoomGroup.Children.Add(zoomLabel);
                zoomBars=new WpfZoomBars { Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center };zoomGroup.Children.Add(zoomBars);
            }
            var zoomOut=IconButton("minus","tooltip:ControlZoomOut",32,16);zoomOut.Click+=(s,e)=>QueueZoom(-1,true);zoomGroup.Children.Add(zoomOut);
            zoomResetButton=new Button { Content="1.0×",MinWidth=52,Height=32,MinHeight=32,Padding=new Thickness(6,0,6,0),Foreground=WpfFieldTheme.Ink,FontSize=t.Uppercase?20:14,FontWeight=FontWeights.SemiBold,Tag="tooltip:ControlResetZoom" };
            zoomResetButton.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");zoomResetButton.FontFamily=t.Uppercase?WpfFieldTheme.DisplayFont:WpfFieldTheme.MonoFont;
            if(t.GradientAccent)zoomResetButton.Background=WpfFieldTheme.Raised;
            zoomResetButton.Click+=(s,e)=> { engine.WpfNativeTool(AppControlAction.ResetZoom);RefreshInteraction(); };zoomGroup.Children.Add(zoomResetButton);
            var zoomIn=IconButton("plus","tooltip:ControlZoomIn",32,16);zoomIn.Click+=(s,e)=>QueueZoom(1,true);zoomGroup.Children.Add(zoomIn);
            actionGroup.Children.Add(Divider(true));
            followButton=WpfFieldTheme.Button(Localization.Get("WpfFollowPlayer"),()=> { engine.WpfFollow();Refresh(); });followButton.Tag="text:WpfFollowPlayer";WpfFieldTheme.IconLabel(followButton,"locate-fixed",false,15);actionGroup.Children.Add(followButton);
            var pin=WpfFieldTheme.Button(Localization.Get("MarkerCurrent"),()=> { if(!engine.WpfMarkCurrent())WpfKit.Ask(tactical,Localization.Get(engine.WpfSnapshot(false).Position==null?"PlayerNoLocation":"LocalSaveFailed"),Localization.Get("MarkerCurrent"),MessageBoxButton.OK,MessageBoxImage.Information);Refresh(); });
            pin.Tag="text:MarkerCurrent";pin.ToolTip=Localization.Get("MarkerHelp");pin.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");pin.Foreground=WpfFieldTheme.Ink;WpfFieldTheme.IconLabel(pin,"map-pin",false,15);actionGroup.Children.Add(pin);
            foreach(var button in new[]{followButton,pin}) { button.MinHeight=32;button.Height=double.NaN;button.Padding=new Thickness(11,4,12,4);button.Margin=new Thickness(3,0,0,0);button.FontSize=13;if(t.Uppercase) { button.FontFamily=WpfFieldTheme.DisplayFont;button.FontWeight=FontWeights.Bold;button.FontSize=15; } }
            if(t.GradientAccent)followButton.BorderThickness=new Thickness(0);
            // Opacity is automatic; keep its existing state binding without presenting a disabled slider as an action.
            var opacityLabel=WpfFieldTheme.Text(Localization.Get("OpacityLabel"),12,WpfFieldTheme.Muted);opacityLabel.Tag="text:OpacityLabel";
            var opacity=new Slider { Minimum=0,Maximum=100,Value=engine.WpfSnapshot(true).Opacity,Width=90,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0),Tag="tooltip:MapOpacity" };tacticalOpacity=opacity;
            opacity.SmallChange=1;opacity.LargeChange=10;opacity.IsSnapToTickEnabled=true;opacity.TickFrequency=1;opacity.IsMoveToPointEnabled=true;
            opacity.ToolTip="1× → 0% · >1× → 100%";System.Windows.Automation.AutomationProperties.SetHelpText(opacity,(string)opacity.ToolTip);opacity.IsEnabled=false;opacity.ValueChanged+=(s,e)=> { if(!syncingOpacity)SyncCameraControls(engine.WpfSnapshot(true)); };
            tacticalOpacityValue=WpfFieldTheme.Text(opacity.Value.ToString("0",CultureInfo.CurrentCulture)+"%",14,WpfFieldTheme.Muted);tacticalOpacityValue.Tag="map-opacity-value";tacticalOpacityValue.MinWidth=42;tacticalOpacityValue.Margin=new Thickness(8,0,0,0);
            foreach(var hidden in new FrameworkElement[]{opacityLabel,opacity,tacticalOpacityValue}) { hidden.Visibility=Visibility.Collapsed;controls.Children.Add(hidden); }
            var toolbar=new DockPanel();var move=new Thumb { Width=14,Margin=new Thickness(0,0,4,0),Cursor=Cursors.SizeAll,Tag="tooltip:WpfMoveControls" };DockPanel.SetDock(move,Dock.Left);toolbar.Children.Add(move);
            var resize=new Thumb { Width=10,Margin=new Thickness(4,0,0,0),Cursor=Cursors.SizeNWSE,Tag="tooltip:WpfResizeControls" };DockPanel.SetDock(resize,Dock.Right);toolbar.Children.Add(resize);toolbar.Children.Add(controls);
            var dockSurface=WpfFieldTheme.Surface(toolbar,new Thickness(6));if(t.GradientAccent)dockSurface.Radius=24;if(t.Chamfer>0)dockSurface.Cut=10;
            var chrome=new Border { Child=dockSurface,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(24) };overlay.Children.Add(chrome);

            // ── Software-map extras: heading/location readout, key hints, status bar ──
            tacticalLocation=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);
            FrameworkElement topInfo=null,keyHintBar=null,statusBar=null;
            if(t.CompassTape) {
                var stack=new StackPanel { HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,10,0,0),IsHitTestVisible=false };
                compassTape=new WpfCompassTape { HorizontalAlignment=HorizontalAlignment.Center };stack.Children.Add(compassTape);
                tacticalLocation.FontFamily=WpfFieldTheme.DisplayFont;tacticalLocation.FontWeight=FontWeights.Bold;tacticalLocation.FontSize=13;tacticalLocation.Foreground=WpfFieldTheme.Muted;tacticalLocation.HorizontalAlignment=HorizontalAlignment.Center;tacticalLocation.MaxWidth=460;
                tacticalLocation.Foreground=WpfFieldTheme.Ink;
                stack.Children.Add(new Border { Child=tacticalLocation,Background=WpfFieldTheme.Glass,Padding=new Thickness(10,1,10,3),Margin=new Thickness(0,3,0,0),HorizontalAlignment=HorizontalAlignment.Center });topInfo=stack;overlay.Children.Add(stack);
            } else if(t.GradientAccent) {
                var row=new StackPanel { Orientation=Orientation.Horizontal };
                topSector=WpfFieldTheme.Text("",12,WpfFieldTheme.AccentInk,true);
                row.Children.Add(new Border { Child=topSector,Background=WpfFieldTheme.AccentFill,CornerRadius=new CornerRadius(9),Padding=new Thickness(8,1,8,2),Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center });
                topLocation=WpfFieldTheme.Text("",13,WpfFieldTheme.Ink);topLocation.MaxWidth=360;row.Children.Add(topLocation);
                topHeading=WpfFieldTheme.Text("",13,WpfFieldTheme.Muted);topHeading.Margin=new Thickness(14,0,0,0);row.Children.Add(topHeading);
                var pill=WpfFieldTheme.Surface(row,new Thickness(8,6,14,6));pill.Radius=20;
                var host=new Border { Child=pill,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,16,0,0),IsHitTestVisible=false };topInfo=host;overlay.Children.Add(host);
            }
            if(t.KeyHints) {
                keyHints=new WrapPanel { VerticalAlignment=VerticalAlignment.Center };
                var bar=WpfFieldTheme.Surface(keyHints,new Thickness(10,7,10,7));bar.Cut=8;
                var host=new Border { Child=bar,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(18),IsHitTestVisible=false };keyHintBar=host;overlay.Children.Add(host);
            }
            if(t.StatusBar) {
                var bar=new DockPanel { LastChildFill=true };
                var right=new StackPanel { Orientation=Orientation.Horizontal };DockPanel.SetDock(right,Dock.Right);bar.Children.Add(right);
                statusZoom=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);statusZoom.FontFamily=WpfFieldTheme.MonoFont;statusZoom.Margin=new Thickness(16,0,0,0);right.Children.Add(statusZoom);
                statusSquad=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);statusSquad.Margin=new Thickness(16,0,0,0);right.Children.Add(statusSquad);
                topSector=WpfFieldTheme.Text("",11,WpfFieldTheme.Ink,true);topSector.FontFamily=WpfFieldTheme.MonoFont;
                var sector=new Border { Child=topSector,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line2,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(6,0,6,1),Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center };
                DockPanel.SetDock(sector,Dock.Left);bar.Children.Add(sector);
                topHeading=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);topHeading.FontFamily=WpfFieldTheme.MonoFont;topHeading.Margin=new Thickness(16,0,0,0);DockPanel.SetDock(topHeading,Dock.Right);bar.Children.Add(topHeading);
                topLocation=WpfFieldTheme.Text("",12,WpfFieldTheme.Ink);bar.Children.Add(topLocation);
                var host=new Border { Child=bar,Height=30,Padding=new Thickness(14,0,14,0),Background=WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),VerticalAlignment=VerticalAlignment.Bottom };
                statusBar=host;overlay.Children.Add(host);
            }
            grid.Children.Add(scene);

            // ── Page panel and icon rail ─────────────────────────────────────
            sidebar=new StackPanel { Margin=new Thickness(18,4,18,18) };
            var scrollContent=new Grid();scrollContent.Children.Add(sidebar);
            var scroller=new ScrollViewer { Content=scrollContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
            var closeOverlay=IconButton("x","tooltip:Close",38,17);closeOverlay.Click+=(s,e)=>CloseTactical(true);
            var paneClose=IconButton("x","tooltip:Close",30,16);paneClose.Click+=(s,e)=>CloseTactical(true);
            var sidebarToggle=IconButton(t.Mirror?"panel-left-open":"panel-right-open","tooltip:WpfExpandSidebar",38,17);
            var paneToggle=IconButton(t.Mirror?"panel-left-close":"panel-right-close","tooltip:WpfCollapseSidebar",30,16);
            // Pane header: brand (or page title in the minimal theme) with collapse/close.
            var mast=new DockPanel { LastChildFill=true };
            var headerButtons=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };headerButtons.Children.Add(paneToggle);headerButtons.Children.Add(paneClose);
            DockPanel.SetDock(headerButtons,Dock.Right);mast.Children.Add(headerButtons);
            UIElement brand;
            if(floating) {
                var brandStack=new StackPanel { VerticalAlignment=VerticalAlignment.Center };brandStack.Children.Add(WpfFieldTheme.Brand());
                if(t.GradientAccent) { var version=WpfFieldTheme.Text("v"+MapWindow.VersionString,11,WpfFieldTheme.Muted);version.Margin=new Thickness(40,-6,0,0);brandStack.Children.Add(version); }
                brand=brandStack;
            } else {
                paneTitle=WpfFieldTheme.Text(Localization.Get("TabMap"),14,WpfFieldTheme.Ink,true);paneTitle.VerticalAlignment=VerticalAlignment.Center;brand=paneTitle;
            }
            mast.Children.Add(brand);
            var header=new Border { Child=mast,Padding=new Thickness(floating?16:16,floating?12:0,8,floating?6:0),Height=floating?double.NaN:48,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,floating?0:1) };
            DockPanel.SetDock(header,Dock.Top);
            var paneDock=new DockPanel { LastChildFill=true };paneDock.Children.Add(header);
            // Page switches. Labels stay in Button.Content so language refresh and screen readers keep the same text.
            string[] pages={"map","squad","crosshair","screen-filter"},pageIcons={"layers","users","crosshair","sun-moon"},pageKeys={"TabMap","TabSquad","Crosshair","TabFilter"},railKeys={"SidebarTitle","SquadTools","Crosshair","ScreenFilter"};
            FrameworkElement tabs=null;
            if(floating) {
                var strip=new UniformGrid { Rows=1 };
                for(int i=0;i<pages.Length;i++) {
                    var tab=new Button { Content=t.Caps(Localization.Get(pageKeys[i])),Tag="caps:"+pageKeys[i],FontSize=12,Padding=new Thickness(2,7,2,6),Margin=new Thickness(t.GradientAccent?2:0,0,t.GradientAccent?2:0,0),Foreground=WpfFieldTheme.Muted };
                    tab.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");WpfFieldTheme.IconLabel(tab,pageIcons[i],true,17);
                    if(t.Uppercase) { tab.FontFamily=WpfFieldTheme.DisplayFont;tab.FontWeight=FontWeights.Bold;tab.FontSize=14;tab.BorderThickness=new Thickness(0,0,0,2);tab.BorderBrush=Brushes.Transparent; }
                    sidebarTabButtons[pages[i]]=tab;strip.Children.Add(tab);
                }
                if(t.GradientAccent) {
                    tabs=new Border { Child=strip,Background=WpfFieldTheme.Raised,CornerRadius=new CornerRadius(14),Padding=new Thickness(3),Margin=new Thickness(12,4,12,8) };
                } else {
                    // Strip tabs: chevron key-caps step through the pages.
                    var row=new DockPanel { Margin=new Thickness(10,4,10,8) };
                    var previous=IconButton("chevron-left","tooltip:PagePrevious",26,14);var next=IconButton("chevron-right","tooltip:PageNext",26,14);
                    foreach(var step in new[]{previous,next}) { step.BorderBrush=WpfFieldTheme.Line2;step.BorderThickness=new Thickness(1);step.Background=WpfFieldTheme.Raised;step.Foreground=WpfFieldTheme.Ink;step.VerticalAlignment=VerticalAlignment.Center; }
                    previous.Click+=(s,e)=> { int at=Array.IndexOf(pages,sidebarPage);ShowSidebarPage(pages[(Math.Max(0,at)+pages.Length-1)%pages.Length]); };
                    next.Click+=(s,e)=> { int at=Array.IndexOf(pages,sidebarPage);ShowSidebarPage(pages[(Math.Max(0,at)+1)%pages.Length]); };
                    DockPanel.SetDock(previous,Dock.Left);DockPanel.SetDock(next,Dock.Right);previous.Margin=new Thickness(0,0,6,0);next.Margin=new Thickness(6,0,0,0);
                    row.Children.Add(previous);row.Children.Add(next);row.Children.Add(new Border { Child=strip,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,1) });tabs=row;
                }
                DockPanel.SetDock(tabs,Dock.Top);paneDock.Children.Add(tabs);
            }
            // Dialog actions: a tool row at the foot of floating panels, the rail otherwise.
            Func<string,string,string,Action,Button> tool=(icon,key,label,action)=> {
                Button button;
                if(t.Uppercase) {
                    button=new Button { Content=t.Caps(Localization.Get(label)),Tag="caps:"+label,FontSize=12,Padding=new Thickness(2,8,2,7),Margin=new Thickness(2,0,2,0),Foreground=WpfFieldTheme.Muted,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1) };
                    button.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");button.FontFamily=WpfFieldTheme.DisplayFont;button.FontWeight=FontWeights.Bold;WpfFieldTheme.IconLabel(button,icon,true,17);
                } else {
                    button=new Button { Content=Localization.Get(label),Tag="text:"+label,FontSize=11,Padding=new Thickness(2,7,2,6),Margin=new Thickness(2,0,2,0),Foreground=WpfFieldTheme.Muted };
                    button.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");button.Foreground=WpfFieldTheme.Muted;WpfFieldTheme.IconLabel(button,icon,true,17);
                }
                button.ToolTip=Localization.Get(key);button.Click+=(s,e)=>action();return button;
            };
            if(floating) {
                var toolRow=new UniformGrid { Rows=1,Margin=new Thickness(10,8,10,10) };
                toolRow.Children.Add(tool("bookmark","MarkerList","ToolMarkers",()=>Modal(BuildSavedMarkers())));
                toolRow.Children.Add(tool("route","RouteList","RouteList",ShowRoutes));
                deathRemovalTool=tool("skull","ClearDeathMarkers","ToolDeaths",()=> { engine.ClearDeathMarkers();Refresh(); });ToolTipService.SetShowOnDisabled(deathRemovalTool,true);toolRow.Children.Add(deathRemovalTool);
                toolRow.Children.Add(tool("circle-help","OpenStartupGuide","ToolGuide",ShowOnboarding));
                toolRow.Children.Add(tool("settings","Settings","Settings",ShowSettings));
                var toolHost=new Border { Child=toolRow,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0) };DockPanel.SetDock(toolHost,Dock.Bottom);paneDock.Children.Add(toolHost);
            } else {
                var foot=new DockPanel();var version=WpfFieldTheme.Text("v"+MapWindow.VersionString,11,WpfFieldTheme.Dim);version.FontFamily=WpfFieldTheme.MonoFont;DockPanel.SetDock(version,Dock.Right);foot.Children.Add(version);
                foot.Children.Add(new System.Windows.Shapes.Ellipse { Width=6,Height=6,Fill=WpfFieldTheme.Success,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center });
                var footer=new Border { Child=foot,Height=32,Padding=new Thickness(16,0,14,0),BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0) };
                var footLabel=WpfFieldTheme.Text("SCUM MiniMap",11,WpfFieldTheme.Muted);foot.Children.Add(footLabel);
                DockPanel.SetDock(footer,Dock.Bottom);paneDock.Children.Add(footer);
            }
            paneDock.Children.Add(scroller);
            ChamferBorder paneSurface=null;FrameworkElement paneHost;
            if(floating) { paneSurface=WpfFieldTheme.Surface(paneDock,new Thickness(0));paneHost=paneSurface; }
            else paneHost=new Border { Child=paneDock,Background=WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=t.Mirror?new Thickness(0,0,1,0):new Thickness(1,0,0,0) };
            // Icon rail. Page buttons fold the panel when their page is already open.
            Func<string,string,Button> railButton=(icon,tag)=> { var b=IconButton(icon,tag,40,19);b.Margin=new Thickness(0,2,0,2);return b; };
            for(int i=0;i<pages.Length;i++)sidebarPageButtons[pages[i]]=railButton(pageIcons[i],"tooltip:"+railKeys[i]);
            var markersRail=railButton("bookmark","tooltip:MarkerList");markersRail.Click+=(s,e)=>Modal(BuildSavedMarkers());
            var routesRail=railButton("route","tooltip:RouteList");routesRail.Click+=(s,e)=>ShowRoutes();
            var help=railButton("circle-help","tooltip:OpenStartupGuide");help.Click+=(s,e)=>ShowOnboarding();
            deathRemovalButton=railButton("skull","tooltip:ClearDeathMarkers");ToolTipService.SetShowOnDisabled(deathRemovalButton,true);deathRemovalButton.Click+=(s,e)=> { engine.ClearDeathMarkers();Refresh(); };
            var settings=railButton("settings","tooltip:Settings");settings.Click+=(s,e)=>ShowSettings();
            var railDivider=new Border { Height=1,Width=26,Margin=new Thickness(0,6,0,6),Background=WpfFieldTheme.Line };
            var railTop=new StackPanel();
            if(!floating) { var logo=WpfFieldTheme.Brand() as Panel;var mark=logo==null||logo.Children.Count==0?null:logo.Children[0] as FrameworkElement;if(mark!=null) { logo.Children.Remove(mark);mark.Margin=new Thickness(7,6,7,12);mark.HorizontalAlignment=HorizontalAlignment.Center;railTop.Children.Add(mark); } }
            foreach(var item in new UIElement[]{closeOverlay,sidebarToggle,railDivider,sidebarPageButtons["map"],sidebarPageButtons["squad"],sidebarPageButtons["crosshair"],sidebarPageButtons["screen-filter"],markersRail,routesRail})railTop.Children.Add(item);
            var railBottom=new StackPanel();foreach(var item in new UIElement[]{help,deathRemovalButton,settings})railBottom.Children.Add(item);
            var rail=new DockPanel { LastChildFill=false };DockPanel.SetDock(railTop,Dock.Top);DockPanel.SetDock(railBottom,Dock.Bottom);rail.Children.Add(railTop);rail.Children.Add(railBottom);
            ChamferBorder railSurface=null;Border railFrame;
            if(floating) { railSurface=WpfFieldTheme.Surface(rail,new Thickness(4,6,4,6));railFrame=new Border { Child=railSurface }; }
            else railFrame=new Border { Child=rail,Background=WpfFieldTheme.Ground,BorderBrush=WpfFieldTheme.Line,BorderThickness=t.Mirror?new Thickness(0,0,1,0):new Thickness(1,0,0,0),Padding=new Thickness(5,8,5,8) };
            const double RailWidth=50;
            var shelf=new Grid { ClipToBounds=true };
            shelf.ColumnDefinitions.Add(new ColumnDefinition());shelf.ColumnDefinitions.Add(new ColumnDefinition());
            shelf.ColumnDefinitions[t.Mirror?0:1].Width=GridLength.Auto;
            shelf.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });shelf.RowDefinitions.Add(new RowDefinition());
            shelf.Children.Add(paneHost);shelf.Children.Add(railFrame);
            Grid.SetColumn(shelf,shelfColumn);grid.Children.Add(shelf);
            window.Content=grid;

            // ── Native-gutter column: overlay cards on top, dock underneath ──
            idleLocation.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Text") { Source=tacticalLocation });
            var nativeLeft=new Grid { HorizontalAlignment=t.Mirror?HorizontalAlignment.Right:HorizontalAlignment.Left,ClipToBounds=true };
            nativeLeft.RowDefinitions.Add(new RowDefinition());nativeLeft.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            Grid.SetColumnSpan(nativeLeft,2);grid.Children.Add(nativeLeft);
            var cardScroll=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalAlignment=VerticalAlignment.Top };nativeLeft.Children.Add(cardScroll);
            var toolScroll=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalAlignment=VerticalAlignment.Bottom };Grid.SetRow(toolScroll,1);nativeLeft.Children.Add(toolScroll);

            double controlScale=1;bool sidebarOpen=true,chromePlaced=false,autoFolded=false;
            bool? gameMap=null;
            Thickness floatingMargin=chrome.Margin;
            // In narrow native gutters (4:3, 5:4) panels overlap the game map; they fade while the pointer is
            // elsewhere so the target and markers underneath stay visible.
            bool coverCards=false,coverShelf=false;
            Action fadeCovers=()=> {
                nativeLeft.Opacity=coverCards&&!nativeLeft.IsMouseOver&&!nativeLeft.IsKeyboardFocusWithin?.9:1;
                shelf.Opacity=coverShelf&&!shelf.IsMouseOver&&!shelf.IsKeyboardFocusWithin?.9:1;
            };
            foreach(var cover in new FrameworkElement[]{nativeLeft,shelf}) { cover.MouseEnter+=(s,e)=>fadeCovers();cover.MouseLeave+=(s,e)=>fadeCovers();cover.IsKeyboardFocusWithinChanged+=(s,e)=>fadeCovers(); }
            // Default dock spot: centred for glass, bottom-right otherwise (left of the mirrored status bar's end).
            // Place and clamp the dock by its natural (unwrapped) width, so it never wraps over the software map.
            Func<double> chromeNatural=()=> { dockSurface.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));return Math.Max(chrome.ActualWidth,dockSurface.DesiredSize.Width); };
            Action placeChrome=()=> {
                double w=overlay.ActualWidth,cw=chromeNatural()*controlScale;if(w<=0||cw<=0)return;
                double bottom=t.StatusBar?42:18;
                chrome.Margin=new Thickness(t.GradientAccent?Math.Max(8,(w-cw)/2):t.Mirror||!t.KeyHints?Math.Max(8,w-cw-18):Math.Max(8,w-cw-18),0,0,bottom);chromePlaced=true;
            };
            Action clampControls=()=> {
                if(gameMap==true)return;if(!chromePlaced) { placeChrome();if(!chromePlaced)return; }
                chrome.Margin=new Thickness(Math.Max(8,Math.Min(chrome.Margin.Left,Math.Max(8,overlay.ActualWidth-chromeNatural()*controlScale-8))),0,0,Math.Max(8,Math.Min(chrome.Margin.Bottom,Math.Max(8,overlay.ActualHeight-chrome.ActualHeight*controlScale-8))));
            };
            move.DragDelta+=(s,e)=> { chrome.Margin=new Thickness(chrome.Margin.Left+e.HorizontalChange,0,0,chrome.Margin.Bottom-e.VerticalChange);clampControls(); };
            resize.DragDelta+=(s,e)=> { double room=Math.Max(.7,(overlay.ActualWidth-16)/Math.Max(1,chrome.ActualWidth));controlScale=Math.Max(.7,Math.Min(Math.Min(1.6,room),controlScale+e.HorizontalChange/400));chrome.LayoutTransform=new ScaleTransform(controlScale,controlScale);chrome.UpdateLayout();clampControls(); };
            chrome.SizeChanged+=(s,e)=>clampControls();overlay.SizeChanged+=(s,e)=>clampControls();
            var toolbarMenu=new ContextMenu();var resetControls=new MenuItem { Header=Localization.Get("WpfResetControls") };toolbarMenu.Opened+=(s,e)=>resetControls.Header=Localization.Get("WpfResetControls");
            resetControls.Click+=(s,e)=> { controlScale=1;chrome.LayoutTransform=Transform.Identity;chromePlaced=false;chrome.UpdateLayout();clampControls(); };toolbarMenu.Items.Add(resetControls);chrome.ContextMenu=toolbarMenu;

            Action arrange=()=> {
                bool native=gameMap==true;
                double gutter=Math.Max(0,(grid.ActualWidth-Math.Min(grid.ActualWidth,grid.ActualHeight))/2);
                double lowerGutter=Math.Floor(Math.Max(0,(grid.ActualHeight-grid.ActualWidth)/2));
                bool bottom=native&&gutter<1&&lowerGutter>0;
                double inset=floating?(native?6:16):0;
                // Narrow native gutters (4:3, 5:4): keep panels usable and let them overlap the game map's edge.
                const double MinimumShelf=330,MinimumCards=340;
                // Floating panels sit further from the window edge over the software map; widen the shelf by the
                // extra inset so the panel itself keeps the same width (and the same three-column tiles) in both views.
                double available=native&&!bottom?Math.Min(360,Math.Max(MinimumShelf,Math.Floor(gutter))):360+(floating&&!native?2*(16-6):0);
                bool railShown=!floating||!sidebarOpen;
                double width=sidebarOpen?available:Math.Min(RailWidth+2*inset+(floating?8:0),available);
                double paneWidth=Math.Max(0,(floating||bottom?width:width-RailWidth)-2*inset);lastPaneWidth=paneWidth;
                int paneColumn=t.Mirror?1:0,railColumn=1-paneColumn;
                Grid.SetColumn(railFrame,bottom?0:railColumn);Grid.SetColumnSpan(railFrame,bottom?2:1);Grid.SetRow(railFrame,0);Grid.SetRowSpan(railFrame,bottom?1:2);
                Grid.SetColumn(paneHost,bottom||floating?0:paneColumn);Grid.SetColumnSpan(paneHost,bottom||floating?2:1);Grid.SetRow(paneHost,bottom&&railShown?1:0);Grid.SetRowSpan(paneHost,bottom&&railShown?1:2);
                railFrame.Visibility=railShown?Visibility.Visible:Visibility.Collapsed;
                paneHost.Visibility=sidebarOpen?Visibility.Visible:Visibility.Collapsed;
                var railOrientation=bottom?Orientation.Horizontal:Orientation.Vertical;railTop.Orientation=railBottom.Orientation=railOrientation;
                DockPanel.SetDock(railTop,bottom?Dock.Left:Dock.Top);DockPanel.SetDock(railBottom,bottom?Dock.Right:Dock.Bottom);
                railDivider.Width=bottom?1:26;railDivider.Height=bottom?26:1;
                closeOverlay.Visibility=sidebarToggle.Visibility=railDivider.Visibility=sidebarOpen?Visibility.Collapsed:Visibility.Visible;
                if(floating) {
                    railFrame.Margin=new Thickness(inset);railFrame.VerticalAlignment=bottom?VerticalAlignment.Stretch:VerticalAlignment.Top;railFrame.HorizontalAlignment=t.Mirror?HorizontalAlignment.Left:HorizontalAlignment.Right;
                    paneSurface.Margin=new Thickness(inset);paneSurface.VerticalAlignment=bottom?VerticalAlignment.Stretch:VerticalAlignment.Top;
                } else railFrame.BorderThickness=bottom?new Thickness(0,0,0,1):t.Mirror?new Thickness(0,0,1,0):new Thickness(1,0,0,0);
                grid.ColumnDefinitions[shelfColumn].Width=new GridLength(bottom?0:width);grid.ColumnDefinitions[mapColumn].Width=new GridLength(1,GridUnitType.Star);
                Grid.SetColumn(shelf,bottom?0:shelfColumn);Grid.SetColumnSpan(shelf,bottom?2:1);
                shelf.VerticalAlignment=bottom?VerticalAlignment.Bottom:VerticalAlignment.Stretch;
                shelf.HorizontalAlignment=bottom?(t.Mirror?HorizontalAlignment.Left:HorizontalAlignment.Right):HorizontalAlignment.Stretch;
                shelf.Height=bottom?lowerGutter:double.NaN;shelf.Width=bottom?Math.Min(width,grid.ActualWidth):double.NaN;
                // The map stays full width under floating panels; the docked sidebar keeps its own column.
                Grid.SetColumn(scene,native||floating?0:mapColumn);Grid.SetColumnSpan(scene,native||floating?2:1);
                double reserve=!native&&floating?width:0;
                overlay.Margin=new Thickness(t.Mirror?reserve:0,0,t.Mirror?0:reserve,0);
                shelf.Visibility=width>0?Visibility.Visible:Visibility.Collapsed;
                double leftWidth=bottom?Math.Min(408,Math.Max(0,grid.ActualWidth-width)):Math.Min(424,Math.Max(MinimumCards,Math.Floor(gutter)));
                if(!bottom)leftWidth=Math.Max(0,Math.Min(leftWidth,grid.ActualWidth-width-24));
                nativeLeft.Visibility=native&&leftWidth>0?Visibility.Visible:Visibility.Collapsed;
                nativeLeft.Width=leftWidth;
                coverCards=native&&!bottom&&leftWidth>Math.Floor(gutter)+1;coverShelf=native&&!bottom&&sidebarOpen&&width>Math.Floor(gutter)+1;fadeCovers();
                if(topInfo!=null)topInfo.Visibility=native?Visibility.Collapsed:Visibility.Visible;
                if(keyHintBar!=null)keyHintBar.Visibility=native||overlay.ActualWidth<980?Visibility.Collapsed:Visibility.Visible;
                if(statusBar!=null)statusBar.Visibility=native?Visibility.Collapsed:Visibility.Visible;
                if(native) {
                    if(chrome.Parent==overlay) {
                        floatingMargin=chrome.Margin;
                        overlay.Children.Remove(chrome);overlay.Children.Remove(topCards);
                        cardScroll.Content=topCards;toolScroll.Content=chrome;
                        chrome.LayoutTransform=Transform.Identity;chrome.ContextMenu=null;
                        move.Visibility=resize.Visibility=Visibility.Collapsed;
                    }
                    double cardInset=Math.Min(8,leftWidth/10);
                    topCards.Margin=chrome.Margin=new Thickness(cardInset);
                    topCards.Width=Math.Max(0,leftWidth-2*cardInset-16);
                    chrome.Width=Math.Max(80,leftWidth-2*cardInset-2);
                    cardScroll.MaxHeight=bottom?lowerGutter:double.PositiveInfinity;
                    toolScroll.MaxHeight=bottom?lowerGutter:Math.Max(0,grid.ActualHeight*.50);
                    nativeLocationCard.Visibility=targetCard.Visibility==Visibility.Visible||routePreviewCard.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;
                } else if(toolScroll.Content!=null) {
                    cardScroll.Content=null;toolScroll.Content=null;
                    overlay.Children.Add(topCards);overlay.Children.Add(chrome);
                    nativeLocationCard.Visibility=Visibility.Collapsed;
                    move.Visibility=resize.Visibility=Visibility.Visible;
                    chrome.Width=double.NaN;chrome.Margin=floatingMargin;chrome.ContextMenu=toolbarMenu;
                    chrome.LayoutTransform=new ScaleTransform(controlScale,controlScale);
                    topCards.Width=392;topCards.Margin=new Thickness(18);
                }
                // Keep labels and controls usable in a narrow gutter. All items can be
                // reached by either scrollbar; the viewport itself never spills over the map.
                bool narrow=paneWidth<230;
                scroller.HorizontalScrollBarVisibility=narrow?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
                sidebar.Margin=new Thickness(narrow?8:16,4,narrow?8:16,16);
                scrollContent.MinWidth=narrow?220:0;
                brand.Visibility=paneWidth>=170?Visibility.Visible:Visibility.Collapsed;
                if(tabs!=null)tabs.Visibility=paneWidth>=150?Visibility.Visible:Visibility.Collapsed;
                if(poiTiles!=null)poiTiles.Columns=PoiColumns();
                sidebarToggle.Tag=sidebarOpen?"tooltip:WpfCollapseSidebar":"tooltip:WpfExpandSidebar";
                RefreshLocalizedChrome(sidebarToggle);
                syncRail();
                if(!native)clampControls();
            };
            syncRail=()=> {
                foreach(var pair in sidebarPageButtons) { bool on=sidebarOpen&&pair.Key==sidebarPage;pair.Value.Background=on?WpfFieldTheme.AccentDim:Brushes.Transparent;pair.Value.Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Muted; }
                foreach(var pair in sidebarTabButtons) {
                    bool on=pair.Key==sidebarPage;
                    if(t.Uppercase) { pair.Value.Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Muted;pair.Value.BorderBrush=on?WpfFieldTheme.Accent:Brushes.Transparent; }
                    else { pair.Value.Background=on?WpfFieldTheme.Hover:Brushes.Transparent;pair.Value.Foreground=on?WpfFieldTheme.Ink:WpfFieldTheme.Muted; }
                }
                if(paneTitle!=null) { int at=Array.IndexOf(pages,sidebarPage);paneTitle.Text=Localization.Get(at<0?"TabMap":pageKeys[at]); }
            };
            grid.SizeChanged+=(sender,e)=>arrange();
            syncMapChrome=(native,opening)=> {
                if(!opening && gameMap==native)return;
                // The floating panel covers part of the enlarged software map, so zooming in from the game's own
                // map folds it to its icon rail; the rail or a page icon opens it again. Opening the big map, or
                // going back to the game's map where the panel sits in the gutter, shows an automatically
                // folded panel again.
                if(floating) {
                    if(opening) { if(autoFolded) { sidebarOpen=true;autoFolded=false; } }
                    else if(gameMap==true&&!native&&sidebarOpen) { sidebarOpen=false;autoFolded=true; }
                    else if(gameMap==false&&native&&autoFolded) { sidebarOpen=true;autoFolded=false; }
                }
                gameMap=native;arrange();
            };
            sidebarToggle.Click+=(sender,e)=> { sidebarOpen=!sidebarOpen;autoFolded=false;arrange(); };
            paneToggle.Click+=(sender,e)=> { sidebarOpen=false;autoFolded=false;arrange(); };
            // Clicking the active page folds the pane; any other page opens it on that page.
            foreach(var pair in sidebarPageButtons) { string page=pair.Key;pair.Value.Click+=(sender,e)=> { autoFolded=false;if(sidebarOpen&&sidebarPage==page)sidebarOpen=false;else { sidebarOpen=true;ShowSidebarPage(page); }arrange(); }; }
            foreach(var pair in sidebarTabButtons) { string page=pair.Key;pair.Value.Click+=(sender,e)=>ShowSidebarPage(page); }
            syncMapChrome(true,true);
            RefreshLocalizedChrome(grid);chromeLanguage=Localization.CurrentCode;
            // A window replaced by a theme switch closes for real; the live one only hides.
            window.Closing+=(s,e)=> { if(!disposed&&!diagnostic&&window==tactical) { e.Cancel=true;window.Hide();engine.WpfTactical(false);SyncHudVisibility(); } };
            return window;
        }
        int PoiColumns() { return Math.Max(2,Math.Min(3,(int)Math.Floor((lastPaneWidth-32)/(Theme.Uppercase?92:90)))); }

        FrameworkElement BuildNavStats() {
            var t=Theme;
            Func<double,bool,TextBlock> value=(size,mono)=> { var text=WpfFieldTheme.Text("",size,WpfFieldTheme.Ink,true);text.TextTrimming=TextTrimming.None;
                if(t.Uppercase) { text.FontFamily=WpfFieldTheme.DisplayFont;text.FontWeight=t.DisplayWeight; } else if(mono)text.FontFamily=WpfFieldTheme.MonoFont;return text; };
            Func<TextBlock> unit=()=> { var text=WpfFieldTheme.Text("",t.Uppercase?13:12,WpfFieldTheme.Muted,true);text.Margin=new Thickness(3,0,0,t.Uppercase?5:4);text.VerticalAlignment=VerticalAlignment.Bottom;if(t.Uppercase)text.FontFamily=WpfFieldTheme.DisplayFont;return text; };
            navDistance=value(t.Uppercase?30:t.GradientAccent?30:17,true);navDistanceUnit=unit();
            navMinutes=value(t.Uppercase?30:t.GradientAccent?30:17,true);navMinutesUnit=unit();
            navRouteKind=WpfFieldTheme.Text("",t.Uppercase?16:12,t.Uppercase?WpfFieldTheme.Ink:t.GradientAccent?WpfFieldTheme.Accent:WpfFieldTheme.Ink,true);
            if(t.Uppercase) { navRouteKind.FontFamily=WpfFieldTheme.DisplayFont;navRouteKind.FontWeight=FontWeights.Bold; }
            Func<TextBlock,TextBlock,UIElement> pair=(number,suffix)=> { var row=new StackPanel { Orientation=Orientation.Horizontal };number.VerticalAlignment=VerticalAlignment.Bottom;row.Children.Add(number);row.Children.Add(suffix);return row; };
            Func<string,TextBlock> caption=key=> { var text=WpfFieldTheme.Text(t.Caps(Localization.Get(key)),11,WpfFieldTheme.Muted);text.Tag=t.Uppercase?"caps:"+key:"text:"+key;if(t.Uppercase)text.FontFamily=WpfFieldTheme.DisplayFont;return text; };
            if(t.GradientAccent) {
                var row=new WrapPanel { Margin=new Thickness(0,8,0,0) };
                var distance=pair(navDistance,navDistanceUnit);((FrameworkElement)distance).Margin=new Thickness(0,0,16,0);row.Children.Add(distance);
                var minutes=pair(navMinutes,navMinutesUnit);((FrameworkElement)minutes).Margin=new Thickness(0,0,16,0);row.Children.Add(minutes);
                var chip=new StackPanel { Orientation=Orientation.Horizontal };var icon=WpfIcons.Create("route",13,WpfFieldTheme.Accent);icon.Margin=new Thickness(0,0,6,0);icon.VerticalAlignment=VerticalAlignment.Center;chip.Children.Add(icon);chip.Children.Add(navRouteKind);
                row.Children.Add(new Border { Child=chip,Background=WpfFieldTheme.AccentDim,BorderBrush=Theme.Alpha(t.Accent,0x55),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(9,3,10,4),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,0,0) });
                return row;
            }
            var cells=new Grid { Margin=new Thickness(0,t.Uppercase?10:12,0,0) };
            for(int i=0;i<3;i++)cells.ColumnDefinitions.Add(new ColumnDefinition());
            navRouteKind.TextTrimming=TextTrimming.None;UIElement[] values={pair(navDistance,navDistanceUnit),pair(navMinutes,navMinutesUnit),new Viewbox { Child=navRouteKind,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left }};string[] captions={"NavDistanceLeft","NavigationEta","NavRouteKind"};
            for(int i=0;i<3;i++) {
                var cell=new StackPanel();
                if(t.Uppercase) { cell.Children.Add(values[i]);cell.Children.Add(caption(captions[i])); }
                else { cell.Children.Add(caption(captions[i]));cell.Children.Add(values[i]); }
                var frame=new Border { Child=cell,Padding=t.Uppercase?new Thickness(i==0?0:12,2,6,2):new Thickness(10,7,8,8),Margin=t.Uppercase?new Thickness(0):new Thickness(i==0?0:3,0,i==2?0:3,0),
                    BorderBrush=WpfFieldTheme.Line,BorderThickness=t.Uppercase?new Thickness(i==0?0:1,0,0,0):new Thickness(1),CornerRadius=new CornerRadius(t.Uppercase?0:t.ControlRadius) };
                Grid.SetColumn(frame,i);cells.Children.Add(frame);
            }
            if(!t.Uppercase)return cells;
            var stack=new StackPanel();stack.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(0,10,0,0) });stack.Children.Add(cells);return stack;
        }

        Border BuildSquadCard() {
            var t=Theme;var body=new StackPanel();
            var head=new DockPanel { Margin=new Thickness(0,0,0,6) };
            squadOnline=WpfFieldTheme.Text("",12,WpfFieldTheme.Success,true);if(t.Uppercase)squadOnline.FontFamily=WpfFieldTheme.DisplayFont;DockPanel.SetDock(squadOnline,Dock.Right);head.Children.Add(squadOnline);
            var title=new StackPanel { Orientation=Orientation.Horizontal };
            if(!t.Uppercase) { var icon=WpfIcons.Create("users",15,WpfFieldTheme.Ink);icon.Margin=new Thickness(0,0,8,0);icon.VerticalAlignment=VerticalAlignment.Center;title.Children.Add(icon); }
            var label=WpfFieldTheme.Text(t.Caps(Localization.Get("TabSquad")),t.Uppercase?14:14,t.Uppercase?WpfFieldTheme.Accent:WpfFieldTheme.Ink,true);label.Tag="caps:TabSquad";
            if(t.Uppercase) { label.FontFamily=WpfFieldTheme.DisplayFont;label.FontWeight=FontWeights.Bold; }
            title.Children.Add(label);head.Children.Add(title);body.Children.Add(head);
            squadRows=new StackPanel();body.Children.Add(squadRows);
            var pings=new UniformGrid { Rows=1,Margin=new Thickness(0,8,0,0) };
            string[] keys={"PingDanger","PingLoot","PingPickup"},icons={"triangle-alert","package","hand"};
            Brush[] inks={WpfFieldTheme.Warn,WpfFieldTheme.MapAmber,WpfFieldTheme.Cyan};
            for(int i=0;i<keys.Length;i++) {
                int kind=i;var ping=ActionButton(keys[i],()=>SendPing(kind),icons[i]);ping.Foreground=inks[i];ping.FontSize=12;ping.Padding=new Thickness(4,4,6,4);ping.Margin=new Thickness(i==0?0:3,0,i==2?0:3,0);
                if(t.Uppercase)ping.FontSize=13;ping.Padding=new Thickness(4,6,4,5);WpfFieldTheme.IconLabel(ping,icons[i],true,17);squadPings.Add(ping);pings.Children.Add(ping);
            }
            body.Children.Add(pings);
            var card=Shell(body,new Thickness(16,12,16,14),true);card.Visibility=Visibility.Collapsed;return card;
        }
        void RefreshSquadCard(WpfMapState state) {
            if(squadCard==null)return;
            bool sharing=privatePositions!=null||(relayGroups!=null&&relayGroups.Sessions.Length>0);
            squadCard.Visibility=(sharing||playerRoster.Length>0)&&state.Bunker==null?Visibility.Visible:Visibility.Collapsed;
            if(squadCard.Visibility!=Visibility.Visible)return;
            var t=Theme;var now=DateTime.UtcNow;
            string identity=string.Join(";",playerRoster.Select(p=>p.Id+":"+p.Name))+"|"+followingPeer+"|"+Localization.CurrentCode;
            if(identity!=squadCardIdentity) {
                squadCardIdentity=identity;squadRows.Children.Clear();squadRowLabels.Clear();
                if(playerRoster.Length==0)squadRows.Children.Add(WpfFieldTheme.Text(Localization.Get("PlayerWaiting"),12,WpfFieldTheme.Muted));
                foreach(var player in playerRoster.Take(6)) {
                    string id=player.Id;var ink=WpfMapSurface.MemberInk(id);
                    var row=new Grid { Margin=new Thickness(0,3,0,3),Tag="squad-row:"+id };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                    FrameworkElement marker;
                    if(t.GradientAccent) { var initial=WpfFieldTheme.Text(string.IsNullOrEmpty(player.Name)?"?":player.Name.Substring(0,1).ToUpper(CultureInfo.CurrentCulture),13,Brushes.White,true);initial.HorizontalAlignment=HorizontalAlignment.Center;marker=new Border { Width=28,Height=28,CornerRadius=new CornerRadius(14),Background=ink,Child=initial,Margin=new Thickness(0,0,10,0) }; }
                    else if(t.Uppercase)marker=new Border { Width=3,Height=30,Background=ink,Margin=new Thickness(0,0,10,0) };
                    else marker=new System.Windows.Shapes.Ellipse { Width=8,Height=8,Fill=ink,Margin=new Thickness(2,0,10,0),VerticalAlignment=VerticalAlignment.Center };
                    row.Children.Add(marker);
                    var text=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
                    var name=WpfFieldTheme.Text(t.Caps(player.Name),t.Uppercase?17:13,WpfFieldTheme.Ink,true);if(t.Uppercase) { name.FontFamily=WpfFieldTheme.DisplayFont;name.FontWeight=FontWeights.Bold; }
                    var status=WpfFieldTheme.Text("",11,WpfFieldTheme.Muted);text.Children.Add(name);text.Children.Add(status);Grid.SetColumn(text,1);row.Children.Add(text);
                    var distance=WpfFieldTheme.Text("",t.Uppercase?17:12,t.Uppercase?WpfFieldTheme.Ink:WpfFieldTheme.Muted,t.Uppercase);distance.Margin=new Thickness(8,0,6,0);
                    if(t.Uppercase) { distance.FontFamily=WpfFieldTheme.DisplayFont;distance.FontWeight=FontWeights.Bold; } else if(!t.GradientAccent)distance.FontFamily=WpfFieldTheme.MonoFont;
                    Grid.SetColumn(distance,2);row.Children.Add(distance);
                    bool following=followingPeer==id;
                    var follow=IconButton("navigation-2",following?"tooltip:SquadStopNav":"tooltip:SquadFollow",30,15);follow.Foreground=following?WpfFieldTheme.Accent:WpfFieldTheme.Muted;
                    if(t.Uppercase) { follow.BorderBrush=WpfFieldTheme.Line2;follow.BorderThickness=new Thickness(1); }
                    follow.Click+=(s,e)=> { if(followingPeer==id) { StopSquadNavigation();filterIdentity=null;Refresh(); } else FollowSquadPeer(id); };
                    follow.IsEnabled=player.Position!=null||following;Grid.SetColumn(follow,3);row.Children.Add(follow);
                    squadRowLabels[id]=new[]{status,distance};squadRows.Children.Add(row);
                }
                if(playerRoster.Length>6)squadRows.Children.Add(WpfFieldTheme.Text("+"+(playerRoster.Length-6),12,WpfFieldTheme.Muted));
            }
            int online=0;
            foreach(var player in playerRoster) {
                DateTime seen;bool connected=playerSeen.TryGetValue(player.Id,out seen)&&(now-seen).TotalSeconds<3;if(connected)online++;
                TextBlock[] labels;if(!squadRowLabels.TryGetValue(player.Id,out labels))continue;
                int age=player.Updated==DateTime.MinValue?-1:Math.Max(0,(int)(now-player.Updated).TotalSeconds);
                string when=age<0?Localization.Get("PlayerNoLocation"):age<60?Localization.T("HistorySecondsAgo",age):age<3600?Localization.T("HistoryMinutesAgo",age/60):Localization.T("HistoryHoursAgo",age/3600);
                labels[0].Text=Localization.Get(connected?"PlayerConnected":"SquadOffline")+" · "+when;
                labels[1].Text=state.Position!=null&&player.Position!=null?DestinationSearch.Distance(DestinationSearch.Metres(MapWindow.ToMap(state.Position),MapWindow.ToMap(player.Position))):"";
                var row=(FrameworkElement)labels[0].Parent;var line=row==null?null:row.Parent as FrameworkElement;if(line!=null)line.Opacity=connected?1:.62;
            }
            squadOnline.Text=Localization.T("SquadOnlineCount",online,playerRoster.Length);squadOnline.Foreground=online>0?WpfFieldTheme.Success:WpfFieldTheme.Muted;
            bool ready=CanSendPing();foreach(var ping in squadPings)ping.IsEnabled=ready;
        }
        void SyncKeyHints(WpfMapState state) {
            if(keyHints==null||state.Bindings==null)return;
            var b=state.Bindings;
            var items=new[]{
                new KeyValuePair<string,int[]>("HintCloseMap",new[]{state.MapKey}),new KeyValuePair<string,int[]>("HintSearch",new[]{b.Key(AppControlAction.Search)}),
                new KeyValuePair<string,int[]>("HintMarker",new[]{b.Key(AppControlAction.AddWaypoint)}),new KeyValuePair<string,int[]>("HintZoom",new[]{b.Key(AppControlAction.ZoomIn),b.Key(AppControlAction.ZoomOut)}),
                new KeyValuePair<string,int[]>("Settings",new[]{b.Key(AppControlAction.Settings)}),new KeyValuePair<string,int[]>("HintMinimap",new[]{b.Key(AppControlAction.ToggleOverlay)})
            };
            string identity=Localization.CurrentCode+"|"+string.Join(";",items.Select(i=>i.Key+"="+string.Join(",",i.Value)));
            if(identity==keyHintIdentity)return;keyHintIdentity=identity;keyHints.Children.Clear();
            foreach(var item in items) {
                var keys=item.Value.Where(k=>k!=0).ToArray();if(keys.Length==0)continue;
                var group=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,16,0) };
                foreach(int key in keys) { var cap=KeyCap(PhysicalKeyCapture.KeyName(key));cap.Margin=new Thickness(0,0,4,0);group.Children.Add(cap); }
                var label=WpfFieldTheme.Text(Theme.Caps(Localization.Get(item.Key)),12,WpfFieldTheme.Muted);label.Margin=new Thickness(3,0,0,0);if(Theme.Uppercase)label.FontFamily=WpfFieldTheme.DisplayFont;group.Children.Add(label);
                keyHints.Children.Add(group);
            }
        }
        // Per-refresh updates for the redesigned chrome: travel figures, readouts, squad card and hints.
        void SyncTacticalChrome(WpfMapState state,WpfMapState compact) {
            var t=Theme;
            bool stats=state.Target!=null&&state.TravelMeters>=0;
            navStats.Visibility=stats?Visibility.Visible:Visibility.Collapsed;
            if(stats) {
                string distance=DestinationSearch.Distance(state.TravelMeters);int split=distance.LastIndexOf(' ');
                navDistance.Text=split<0?distance:distance.Substring(0,split);navDistanceUnit.Text=t.Caps(split<0?"":distance.Substring(split+1));
                navMinutes.Text=state.TravelMinutes>0?state.TravelMinutes.ToString(CultureInfo.CurrentCulture):"—";navMinutesUnit.Text=state.TravelMinutes>0?t.Caps(Localization.Get("UnitMinutes")):"";
                navRouteKind.Text=t.Caps(Localization.Get(state.TravelCalculating?"FieldCalculating":state.TravelRoad?"NavRoad":"NavDirect"));
            }
            navNote.Text=state.TravelNote??"";navNote.Visibility=stats&&!string.IsNullOrEmpty(state.TravelNote)?Visibility.Visible:Visibility.Collapsed;
            targetDetail.Visibility=stats?Visibility.Collapsed:Visibility.Visible;
            if(deathRemovalTool!=null)deathRemovalTool.IsEnabled=deathRemovalButton.IsEnabled;
            double heading=state.Position==null?double.NaN:WpfCompassTape.Normal(state.Position.Yaw-90);
            if(compassTape!=null)compassTape.Heading=heading;
            string sector=compact.Position==null?"":DestinationSearch.Sector(MapWindow.ToMap(compact.Position));string place=state.BunkerCaption??compact.Location??"";
            if(sector.Length>0&&place.StartsWith(sector+" - ",StringComparison.Ordinal))place=place.Substring(sector.Length+3);
            if(topSector!=null) { topSector.Text=sector;((FrameworkElement)topSector.Parent).Visibility=sector.Length==0?Visibility.Collapsed:Visibility.Visible; }
            if(topLocation!=null) { topLocation.Text=place;topLocation.ToolTip=place; }
            if(topHeading!=null)topHeading.Text=double.IsNaN(heading)?"":Localization.Get("HeadingLabel")+" "+WpfCompassTape.Cardinal(heading)+" "+((int)Math.Round(heading)%360).ToString(CultureInfo.InvariantCulture)+"°";
            if(statusZoom!=null)statusZoom.Text=state.Zoom.ToString("0.0",CultureInfo.CurrentCulture)+"×";
            if(statusSquad!=null) { int seen=playerRoster.Count(p=> { DateTime at;return playerSeen.TryGetValue(p.Id,out at)&&(DateTime.UtcNow-at).TotalSeconds<3; });statusSquad.Text=playerRoster.Length==0?"":Localization.Get("TabSquad")+" "+seen+"/"+playerRoster.Length; }
            if(searchKeyLabel!=null&&state.Bindings!=null)searchKeyLabel.Text=PhysicalKeyCapture.KeyName(state.Bindings.Key(AppControlAction.Search));
            SyncKeyHints(state);RefreshSquadCard(state);
        }
        // Follow is a state toggle: filled when active (gradient in glass), quiet otherwise.
        void StyleFollow() {
            if(followButton==null)return;var t=Theme;bool on=engine.WpfFollowing;
            if(t.GradientAccent) { followButton.Background=on?WpfFieldTheme.AccentFill:WpfFieldTheme.Raised;followButton.Foreground=on?WpfFieldTheme.AccentInk:WpfFieldTheme.Ink;followButton.BorderBrush=Brushes.Transparent; }
            else { followButton.Background=on?WpfFieldTheme.AccentDim:WpfFieldTheme.Raised;followButton.Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Ink;followButton.BorderBrush=on?WpfFieldTheme.Accent:WpfFieldTheme.Line; }
        }

        void RebuildSidebar(WpfMapState state) {
            var t=Theme;
            poiCategoryControls.Clear();poiSectionControls.Clear();poiSectionCounts.Clear();presentedPoiFilter=null;poiTiles=null;
            playerStatusLabels.Clear();sidebar.Children.Clear();mapPage=new StackPanel();squadPage=new StackPanel();sidebar.Children.Add(mapPage);sidebar.Children.Add(squadPage);sidebar.Children.Add(CrosshairSidebarPage());sidebar.Children.Add(ScreenFilterSidebarPage());
            // The glass theme keeps search above the navigation card; the others open the map page with it.
            if(!t.GradientAccent) { TextBlock key;var search=SearchBar(state.Bindings.Key(AppControlAction.Search),out key);search.Margin=new Thickness(0,10,0,2);mapPage.Children.Add(search); }
            var mapHint=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0) };mapHint.Children.Add(KeyCap(PhysicalKeyCapture.KeyName(state.MapKey)));
            var mapHintText=WpfFieldTheme.Text(t.Caps(Localization.Get("HintCloseMap")),12,WpfFieldTheme.Muted);mapHintText.Margin=new Thickness(8,0,0,0);if(t.Uppercase)mapHintText.FontFamily=WpfFieldTheme.DisplayFont;mapHint.Children.Add(mapHintText);
            if(!t.KeyHints)mapPage.Children.Add(mapHint);
            // Squad page: group tools first, then each shared player with their layer pills.
            squadPage.Children.Add(SectionHeader(S("SquadTools")));
            var squadActions=new UniformGrid { Columns=2,Margin=new Thickness(-3,0,-3,14) };squadPage.Children.Add(squadActions);
            var squadButton=WpfFieldTheme.Button(S("SquadTools"),ShowSquadTools);squadButton.Tag="sidebar-squad";
            var privateButton=WpfFieldTheme.Button(Localization.Get("PrivatePositions"),ShowPrivatePositions);privateButton.Tag="sidebar-private";
            WpfFieldTheme.IconLabel(squadButton,"users",true,20);WpfFieldTheme.IconLabel(privateButton,"radio",true,20);
            foreach(var action in new[]{squadButton,privateButton}) { action.ToolTip=action.Content;action.Margin=new Thickness(3,0,3,0);action.FontSize=12;action.Padding=new Thickness(6,10,6,9); }
            squadActions.Children.Add(squadButton);squadActions.Children.Add(privateButton);
            bunkerViewsControl=null;
            if(state.Bunker!=null) {
                var views=engine.WpfBunkerViews();
                if(views.Length>0) {
                    mapPage.Children.Add(SectionHeader(S("BunkerView")));
                    bunkerViewsControl=new ComboBox { Tag="bunker-view",Margin=new Thickness(0,0,0,6) };
                    bunkerViewsControl.Items.Add(new ComboBoxItem { Content=S("BunkerFollowFloor"),Tag=null });
                    foreach(var view in views)bunkerViewsControl.Items.Add(new ComboBoxItem { Content=view.DisplayName??view.Id,Tag=view.Id });
                    bunkerViewsControl.SelectedItem=bunkerViewsControl.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==engine.WpfBunkerViewSelection);
                    System.Windows.Automation.AutomationProperties.SetName(bunkerViewsControl,S("BunkerView"));
                    bunkerViewsControl.SelectionChanged+=(s,e)=> { if(rebuilding)return;var selected=bunkerViewsControl.SelectedItem as ComboBoxItem;if(selected==null)return;engine.WpfSelectBunkerView((string)selected.Tag);RefreshInteraction(); };mapPage.Children.Add(bunkerViewsControl);
                }
            }
            AddPlayers(squadPage);if(squadPage.Children.Count==2)squadPage.Children.Add(WpfFieldTheme.Text(Localization.Get("PlayerWaiting"),12,WpfFieldTheme.Muted));
            // Layers.
            mapPage.Children.Add(SectionHeader(Localization.Get("SidebarLayers")));
            var layerBody=new StackPanel();
            mapPage.Children.Add(t.GradientAccent?(UIElement)new Border { Background=WpfFieldTheme.Raised,CornerRadius=new CornerRadius(14),Padding=new Thickness(12,2,12,2),Child=layerBody }:layerBody);
            var customRow=new DockPanel();var customMaster=new CheckBox { IsChecked=state.Zones,Width=t.SwitchWidth+4,Tag="custom-zones:master",ToolTip=Localization.Get("ShowSavedZones") };customMaster.Checked+=(s,e)=> { if(!rebuilding) { engine.WpfLayer("zones",true);Refresh(); } };customMaster.Unchecked+=(s,e)=> { if(!rebuilding) { engine.WpfLayer("zones",false);Refresh(); } };DockPanel.SetDock(customMaster,Dock.Right);customRow.Children.Add(customMaster);
            var customExpand=new Button { Content=(state.CustomZonesExpanded?"⌄  ":"›  ")+Localization.Get("ShowSavedZones"),Tag="custom-zones:expand",HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(0,8,0,8),Foreground=WpfFieldTheme.Ink,FontSize=14,FontWeight=FontWeights.Normal };
            customExpand.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");customExpand.Foreground=WpfFieldTheme.Ink;customExpand.Click+=(s,e)=> { engine.WpfExpandZones();filterIdentity=null;Refresh(); };RowIcon(customExpand,"square-dashed",LayerTint("zones"));customRow.Children.Add(customExpand);layerBody.Children.Add(customRow);
            if(state.CustomZonesExpanded&&state.CustomZones!=null)foreach(var group in state.CustomZones.GroupBy(z=>z.Layer).OrderBy(g=>g.Key,StringComparer.CurrentCultureIgnoreCase)) {
                string layer=group.Key;var layerToggle=new CheckBox { Content=layer,IsChecked=group.First().LayerEnabled,Margin=new Thickness(24,0,0,0),Tag="custom-zone-layer:"+layer };layerToggle.Checked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneLayerToggle(layer,true);filterIdentity=null;Refresh(); } };layerToggle.Unchecked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneLayerToggle(layer,false);filterIdentity=null;Refresh(); } };layerBody.Children.Add(layerToggle);
                foreach(var option in group.OrderBy(z=>z.Zone.DisplayName,StringComparer.CurrentCultureIgnoreCase)) {
                    string zoneKey=option.Key;var row=new CheckBox { Content=option.Zone.DisplayName,IsChecked=option.Enabled,Margin=new Thickness(40,0,0,0),FontSize=13,Tag="custom-zone:"+zoneKey,ToolTip=option.Zone.DisplayName };
                    row.Checked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneToggle(zoneKey,true);filterIdentity=null;Refresh(); } };row.Unchecked+=(s,e)=> { if(!rebuilding) { engine.WpfZoneToggle(zoneKey,false);filterIdentity=null;Refresh(); } };layerBody.Children.Add(row);
                }
            }
            AddLayer(layerBody,"waypoints",Localization.Get("ShowCustomWaypoints"),state.Waypoints,"bookmark");AddLayer(layerBody,"grid",Localization.Get("GridBorders"),state.Grid,"grid-3x3");AddLayer(layerBody,"history",Localization.Get("HistoryEnabled"),state.History,"history");AddLayer(layerBody,"labels",Localization.Get("ShowZoneLabels"),state.Labels,"tag");
            // Points of interest: tiles toggle whole groups (glass/tactical), a tree in the minimal theme.
            var bulk=new StackPanel { Orientation=Orientation.Horizontal };
            foreach(var pair in new[]{new KeyValuePair<string,bool>("SelectAll",true),new KeyValuePair<string,bool>("DeselectAll",false)}) {
                bool on=pair.Value;var button=new Button { Content=Localization.Get(pair.Key),FontSize=12,Padding=new Thickness(7,2,7,3),MinHeight=24,Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Muted,Margin=new Thickness(2,0,0,0) };
                button.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");button.Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Muted;button.Click+=(s,e)=>FilterAll(on);bulk.Children.Add(button);
            }
            mapPage.Children.Add(SectionHeader(Localization.Get("SidebarCategories"),bulk));
            if(state.Store!=null&&t.Tabs!=WpfTabStyle.Rail)AddPoiTiles(state.Store);
            else if(state.Store!=null)AddPoiTree(state.Store);
            // Map tools.
            mapPage.Children.Add(SectionHeader(S("SettingsTools")));
            var tools=new UniformGrid { Columns=1,Margin=new Thickness(-3,0,-3,0) };
            var placement=WpfFieldTheme.Button(Localization.Get("MapPlacement"),()=>Modal(BuildMapPlacement()));placement.Tag="map-placement";WpfFieldTheme.IconLabel(placement,"move",false,15);
            var presetsButton=WpfFieldTheme.Button(S("SquadPresets"),ShowMapPresets);presetsButton.Tag="sidebar-presets";WpfFieldTheme.IconLabel(presetsButton,"star",false,15);
            foreach(var button in new[]{presetsButton,placement}) { button.FontSize=12;button.Margin=new Thickness(3,0,3,6);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(8,6,8,6);button.ToolTip=button.Content;tools.Children.Add(button); }
            mapPage.Children.Add(tools);
            ShowSidebarPage(sidebarPage);
        }
        static string SectionIcon(string section) {
            switch((section??"").ToLowerInvariant()) {
                case "buildings":return "house";case "bunkers":return "warehouse";case "construction materials":return "hammer";case "crops":return "wheat";
                case "loot containers":return "package";case "outposts":return "tent";case "points of interest":return "star";case "quests":return "scroll-text";
                case "radiation":return "radiation";case "vehicles":return "car";case "water sources":return "droplet";case "fishing":return "fish";
                case "hunting":return "rabbit";case "fishing habitats":return "waves";case "hunting habitats":return "paw-print";default:return "map-pin";
            }
        }
        void AddPoiTiles(ScumMapStore store) {
            var t=Theme;poiTiles=new UniformGrid { Columns=PoiColumns(),Margin=new Thickness(-3,0,-3,0) };mapPage.Children.Add(poiTiles);
            var details=new StackPanel();mapPage.Children.Add(details);
            var detailIds=new List<int>();var expanders=new Dictionary<string,Button>(StringComparer.Ordinal);Action renderDetails=null;
            renderDetails=()=> {
                foreach(int id in detailIds)poiCategoryControls.Remove(id);
                detailIds.Clear();details.Children.Clear();
                // The tile's chevron mirrors its section: up and accented while the list below is open.
                foreach(var pair in expanders) {
                    bool open=tileExpanded.Contains(pair.Key);SetIcon(pair.Value,open?"chevron-up":"chevron-down",13);
                    pair.Value.Foreground=open?WpfFieldTheme.Accent:WpfFieldTheme.Muted;string tip=Localization.Get(open?"PoiHideCategories":"PoiShowCategories");pair.Value.ToolTip=tip;System.Windows.Automation.AutomationProperties.SetName(pair.Value,tip);
                }
                foreach(string section in store.Sections.Where(s=>tileExpanded.Contains(s))) {
                    string captured=section;var body=new StackPanel();
                    var head=new DockPanel();var close=IconButton("chevron-up","tooltip:PoiShowCategories",24,14);close.Click+=(s,e)=> { tileExpanded.Remove(captured);renderDetails(); };DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
                    head.Children.Add(WpfFieldTheme.Text(t.Caps(Localization.GetSectionName(section)),13,WpfFieldTheme.Ink,true));body.Children.Add(head);
                    foreach(var category in store.CategoriesBySection[section].OrderBy(c=>c.Name)) {
                        var row=new CheckBox { Content=Localization.GetCategoryName(category.Name)+"  "+category.MarkerCount,IsChecked=store.IsCategoryEnabled(category.Id),FontSize=13,Tag="poi-category:"+category.Id };
                        int id=category.Id;poiCategoryControls[id]=row;detailIds.Add(id);row.Checked+=(s,e)=>Category(id,true);row.Unchecked+=(s,e)=>Category(id,false);body.Children.Add(row);
                    }
                    details.Children.Add(new Border { Child=body,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(t.ControlRadius),Padding=new Thickness(12,6,8,4),Margin=new Thickness(0,6,0,0) });
                }
            };
            foreach(string section in store.Sections) {
                var categories=store.CategoriesBySection[section];int enabled=categories.Count(c=>store.IsCategoryEnabled(c.Id));string captured=section;
                var tile=new CheckBox { IsThreeState=true,IsChecked=enabled==0?(bool?)false:enabled==categories.Count?(bool?)true:null,Tag="poi-section:"+section,ToolTip=Localization.GetSectionName(section) };
                tile.SetResourceReference(FrameworkElement.StyleProperty,"PoiTile");
                System.Windows.Automation.AutomationProperties.SetName(tile,Localization.GetSectionName(section));
                var face=new Grid();face.RowDefinitions.Add(new RowDefinition());face.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });face.ColumnDefinitions.Add(new ColumnDefinition());face.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                var icon=WpfIcons.Create(SectionIcon(section),18,WpfFieldTheme.Muted);icon.HorizontalAlignment=HorizontalAlignment.Left;icon.VerticalAlignment=VerticalAlignment.Top;
                icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") { Source=tile });face.Children.Add(icon);
                var corner=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top };Grid.SetColumn(corner,1);face.Children.Add(corner);
                var expand=IconButton("chevron-down","tooltip:PoiShowCategories",20,13);expand.Margin=new Thickness(2,-3,-4,0);expanders[section]=expand;
                expand.Click+=(s,e)=> { if(!tileExpanded.Remove(captured))tileExpanded.Add(captured);renderDetails();e.Handled=true; };
                var name=WpfFieldTheme.Text(t.Caps(Localization.GetSectionName(section)),12,WpfFieldTheme.Ink,t.Uppercase);name.Margin=new Thickness(0,6,0,0);name.TextWrapping=TextWrapping.Wrap;name.MaxHeight=34;if(t.Uppercase)name.FontFamily=WpfFieldTheme.DisplayFont;FitWords(name);Grid.SetRow(name,1);Grid.SetColumnSpan(name,2);face.Children.Add(name);
                var count=WpfFieldTheme.Text(enabled+"/"+categories.Count,10,WpfFieldTheme.Muted);count.TextTrimming=TextTrimming.None;count.VerticalAlignment=VerticalAlignment.Top;count.FontFamily=WpfFieldTheme.MonoFont;corner.Children.Add(count);corner.Children.Add(expand);
                tile.Content=face;poiSectionControls[section]=tile;poiSectionCounts[section]=count;
                tile.Click+=(s,e)=>FilterSection(captured,!categories.All(c=>store.IsCategoryEnabled(c.Id)));
                poiTiles.Children.Add(tile);
            }
            renderDetails();
        }
        // Wrapped labels in narrow cells: shrink the font until the longest Latin word fits, so words never split mid-way.
        static void FitWords(TextBlock text) {
            double baseSize=text.FontSize;
            text.SizeChanged+=(s,e)=> {
                double room=text.ActualWidth;if(room<=0||!e.WidthChanged)return;
                string widest=(text.Text??"").Split(' ').Where(w=>w.All(c=>c<'\u2E80')).OrderByDescending(w=>w.Length).FirstOrDefault();if(string.IsNullOrEmpty(widest))return;
                double size=baseSize,dip=VisualTreeHelper.GetDpi(text).PixelsPerDip;var face=new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch);
                while(size>9&&new FormattedText(widest,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,face,size,Brushes.White,dip).WidthIncludingTrailingWhitespace>room)size-=.5;
                if(Math.Abs(text.FontSize-size)>.01)text.FontSize=size;
            };
        }
        void AddPoiTree(ScumMapStore store) {
            foreach(string section in store.Sections) {
                var categories=store.CategoriesBySection[section];bool open=expanded.Contains(section);int enabled=categories.Count(c=>store.IsCategoryEnabled(c.Id));
                var title=new Grid();title.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });title.ColumnDefinitions.Add(new ColumnDefinition());title.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                var chevron=WpfIcons.Create(open?"chevron-down":"chevron-right",13,WpfFieldTheme.Muted);chevron.Margin=new Thickness(0,0,6,0);chevron.VerticalAlignment=VerticalAlignment.Center;title.Children.Add(chevron);
                var icon=WpfIcons.Create(SectionIcon(section),14,WpfFieldTheme.Muted);icon.Margin=new Thickness(0,0,8,0);icon.VerticalAlignment=VerticalAlignment.Center;
                var nameRow=new DockPanel();DockPanel.SetDock(icon,Dock.Left);nameRow.Children.Add(icon);var sectionTitle=WpfFieldTheme.Text(Localization.GetSectionName(section),13,null,false);sectionTitle.TextWrapping=TextWrapping.Wrap;sectionTitle.TextTrimming=TextTrimming.None;nameRow.Children.Add(sectionTitle);Grid.SetColumn(nameRow,1);title.Children.Add(nameRow);
                var count=WpfFieldTheme.Text(enabled+" / "+categories.Count,11,WpfFieldTheme.Muted);count.FontFamily=WpfFieldTheme.MonoFont;count.Margin=new Thickness(6,0,0,0);Grid.SetColumn(count,2);title.Children.Add(count);
                var button=new Button { Content=title,HorizontalContentAlignment=HorizontalAlignment.Stretch,Foreground=WpfFieldTheme.Ink,Padding=new Thickness(6,6,6,6) };button.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");button.Foreground=WpfFieldTheme.Ink;
                var rows=new StackPanel { Visibility=open?Visibility.Visible:Visibility.Collapsed,Margin=new Thickness(26,0,0,4) };
                Action populate=()=> {
                    if(rows.Children.Count>0)return;
                    foreach(var category in categories.OrderBy(c=>c.Name)) {
                        var row=new CheckBox { Content=Localization.GetCategoryName(category.Name)+"  "+category.MarkerCount,IsChecked=store.IsCategoryEnabled(category.Id),FontSize=13,Tag="poi-category:"+category.Id };
                        int id=category.Id;poiCategoryControls[id]=row;row.Checked+=(s,e)=>Category(id,true);row.Unchecked+=(s,e)=>Category(id,false);rows.Children.Add(row);
                    }
                };
                string captured=section;button.Tag="poi-expand:"+section;
                button.Click+=(s,e)=> { bool show=!expanded.Remove(captured);if(show) { expanded.Add(captured);populate(); }rows.Visibility=show?Visibility.Visible:Visibility.Collapsed;chevron.Data=WpfIcons.Geometry(show?"chevron-down":"chevron-right",13); };
                var groupRow=new DockPanel();var groupToggle=new CheckBox { IsThreeState=true,IsChecked=enabled==0?(bool?)false:enabled==categories.Count?(bool?)true:null,Width=Theme.SwitchWidth+4,Margin=new Thickness(6,0,0,0),ToolTip=Localization.GetSectionName(section),Tag="poi-section:"+section };
                poiSectionControls[section]=groupToggle;poiSectionCounts[section]=count;
                System.Windows.Automation.AutomationProperties.SetName(groupToggle,Localization.GetSectionName(section));groupToggle.Click+=(s,e)=>FilterSection(captured,!categories.All(c=>store.IsCategoryEnabled(c.Id)));DockPanel.SetDock(groupToggle,Dock.Right);groupRow.Children.Add(groupToggle);groupRow.Children.Add(button);
                if(open)populate();var groupBody=new StackPanel();groupBody.Children.Add(groupRow);groupBody.Children.Add(rows);mapPage.Children.Add(groupBody);
            }
        }
    }

    // Interface theme switching and the Settings theme picker.
    internal sealed partial class WpfFieldConsoleRuntime {
        string themeFolder;WpfThemeSpec pendingTheme;
        void ApplyWindowStyle() {
            application.Resources[typeof(Window)]=new Style(typeof(Window)) { Setters={new Setter(Control.FontFamilyProperty,WpfFieldTheme.UiFont),new Setter(Control.ForegroundProperty,WpfFieldTheme.Ink),new Setter(Control.BackgroundProperty,WpfFieldTheme.Ground),new Setter(Control.FontSizeProperty,14.0)} };
        }
        void WireTactical() {
            var window=tactical;
            window.IsVisibleChanged+=(s,e)=> { if(window!=tactical)return;tacticalMouseShieldActive=tactical.IsVisible&&tactical.WindowState!=WindowState.Minimized;if(!tactical.IsVisible)tacticalMap.CloseContextMenu(); };
            window.StateChanged+=(s,e)=> { if(window==tactical)tacticalMouseShieldActive=tactical.IsVisible&&tactical.WindowState!=WindowState.Minimized; };
            window.Closed+=(s,e)=> { if(window==tactical)tacticalMouseShieldActive=false; };
        }
        // Rebuilds the full map and minimap in the new theme, keeping their visibility and
        // all engine state. Dialogs are closed first (see Modal), so no window is orphaned.
        internal void ApplyTheme(WpfThemeSpec theme) {
            if(theme==null||disposed)return;
            WpfThemes.Save(themeFolder,theme);
            if(ReferenceEquals(theme,WpfFieldTheme.Current))return;
            bool showTactical=tactical.IsVisible;
            WpfFieldTheme.Current=theme;
            application.Resources.MergedDictionaries.Remove(themeResources);themeResources=WpfFieldTheme.Resources();application.Resources.MergedDictionaries.Add(themeResources);
            ApplyWindowStyle();
            sidebarPageButtons.Clear();sidebarTabButtons.Clear();squadPings.Clear();squadRowLabels.Clear();squadCardIdentity=null;keyHintIdentity=null;
            compassTape=null;zoomBars=null;topSector=topLocation=topHeading=statusZoom=statusSquad=paneTitle=searchKeyLabel=null;keyHints=null;poiTiles=null;deathRemovalTool=null;bunkerViewsControl=null;
            // Cached sidebar pages still belong to the old window: flush pending saves and rebuild them in the new theme.
            if(crosshairSave!=null&&crosshairSave.IsEnabled) { crosshairSave.Stop();crosshair.Save(); }
            if(screenFilterSave!=null&&screenFilterSave.IsEnabled) { screenFilterSave.Stop();screenFilter.Save(); }
            crosshairPage=null;crosshairPageRefresh=null;screenFilterPage=null;screenFilterRefresh=null;screenFilterRefreshView=null;
            var oldTactical=tactical;var oldHud=hud;
            tactical=BuildTactical();hud=BuildHud();WireTactical();
            oldTactical.Close();oldHud.Close();
            filterIdentity=null;tacticalVisualIdentity=hudVisualIdentity=null;chromeLanguage=null;lastRefresh=-1000;
            lastTacticalBounds=System.Drawing.Rectangle.Empty;lastHudBounds=System.Drawing.Rectangle.Empty;
            if(showTactical) {
                var openingState=engine.WpfSnapshot(true);
                syncMapChrome(openingState.Bunker==null&&openingState.Opacity==0,true);
                FitTacticalToMonitor();tactical.Show();RefreshView(openingState);
            } else { SyncHudVisibility();Refresh(); }
        }
        // Theme cards: a miniature of each look, its name and one line on its character.
        FrameworkElement ThemePicker(Window settings) {
            var panel=new StackPanel();
            var help=WpfFieldTheme.Text(Localization.Get("UiThemeHelp"),14,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;help.TextTrimming=TextTrimming.None;help.Margin=new Thickness(0,0,0,12);panel.Children.Add(help);
            var cards=new UniformGrid { Columns=3,Margin=new Thickness(-5,0,-5,0) };
            foreach(var spec in WpfThemes.All) {
                var theme=spec;bool current=ReferenceEquals(theme,WpfFieldTheme.Current);
                var body=new StackPanel();body.Children.Add(ThemePreview(theme));
                var name=new DockPanel { Margin=new Thickness(2,10,2,2) };
                if(current) { var badge=WpfFieldTheme.Text(Localization.Get("ThemeCurrent"),11,WpfFieldTheme.Accent,true);var tag=new Border { Child=badge,Background=WpfFieldTheme.AccentDim,CornerRadius=new CornerRadius(5),Padding=new Thickness(7,1,7,2),VerticalAlignment=VerticalAlignment.Center };DockPanel.SetDock(tag,Dock.Right);name.Children.Add(tag); }
                name.Children.Add(WpfFieldTheme.Text(Localization.Get(theme.NameKey),16,WpfFieldTheme.Ink,true));body.Children.Add(name);
                var line=WpfFieldTheme.Text(Localization.Get(theme.DescriptionKey),12,WpfFieldTheme.Muted);line.TextWrapping=TextWrapping.Wrap;line.TextTrimming=TextTrimming.None;line.Margin=new Thickness(2,2,2,4);body.Children.Add(line);
                var card=new Button { Content=body,Tag="ui-theme:"+theme.Id,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(10),Margin=new Thickness(5,0,5,0),Background=WpfFieldTheme.Raised,
                    BorderBrush=current?WpfFieldTheme.Accent:WpfFieldTheme.Line,BorderThickness=new Thickness(current?2:1),VerticalContentAlignment=VerticalAlignment.Top };
                System.Windows.Automation.AutomationProperties.SetName(card,Localization.Get(theme.NameKey));
                card.Click+=(s,e)=> { if(ReferenceEquals(theme,WpfFieldTheme.Current))return;pendingTheme=theme;settings.Close(); };
                cards.Children.Add(card);
            }
            panel.Children.Add(cards);return panel;
        }
        static FrameworkElement ThemePreview(WpfThemeSpec t) {
            var stage=new Grid { Height=92,ClipToBounds=true };
            var map=new LinearGradientBrush(Color.FromRgb(46,58,48),Color.FromRgb(30,38,34),new Point(0,0),new Point(1,1));map.Freeze();
            stage.Children.Add(new Border { Background=map,CornerRadius=new CornerRadius(Math.Min(t.ControlRadius,8)) });
            var route=new System.Windows.Shapes.Path { Data=Geometry.Parse("M 30,84 C 60,60 70,70 90,46 S 120,30 132,14"),Stroke=t.Brush(t.Accent),StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round };
            if(t.RouteCasing!=null)stage.Children.Add(new System.Windows.Shapes.Path { Data=route.Data,Stroke=t.Brush(t.RouteCasing),StrokeThickness=5.5 });
            stage.Children.Add(route);
            Func<double,double,double,double,bool,FrameworkElement> panel=(left,top,width,height,accent)=> {
                var lines=new StackPanel { Margin=new Thickness(6,6,6,6) };
                lines.Children.Add(new Border { Height=4,Width=width*.45,HorizontalAlignment=HorizontalAlignment.Left,Background=accent?t.Brush(t.Accent):t.Brush(t.Muted),Margin=new Thickness(0,0,0,4) });
                lines.Children.Add(new Border { Height=7,Width=width*.7,HorizontalAlignment=HorizontalAlignment.Left,Background=t.Brush(t.Ink),Margin=new Thickness(0,0,0,4) });
                lines.Children.Add(new Border { Height=3,Width=width*.55,HorizontalAlignment=HorizontalAlignment.Left,Background=t.Brush(t.Line2) });
                var shape=new ChamferBorder { Background=t.Brush(t.Panel),BorderBrush=t.Brush(t.Line),Cut=t.Chamfer/3,Radius=t.PanelRadius/3,Child=lines,Width=width,Height=height,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(left,top,0,0) };
                if(accent&&t.Chamfer>0) { shape.AccentBrush=t.Brush(t.Accent);shape.AccentWidth=2; }
                return shape;
            };
            if(t.Mirror) {
                stage.Children.Add(new Border { Width=38,HorizontalAlignment=HorizontalAlignment.Left,Background=t.Brush(t.PanelSolid),BorderBrush=t.Brush(t.Line),BorderThickness=new Thickness(0,0,1,0) });
                stage.Children.Add(new Border { Height=10,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(38,0,0,0),Background=t.Brush(t.PanelSolid) });
                stage.Children.Add(panel(150,6,64,40,true));
            } else {
                stage.Children.Add(panel(6,6,62,42,true));
                var side=panel(158,6,52,78,false);side.HorizontalAlignment=HorizontalAlignment.Right;side.Margin=new Thickness(0,6,6,0);stage.Children.Add(side);
                var dock=new ChamferBorder { Background=t.Brush(t.Panel),BorderBrush=t.Brush(t.Line),Cut=t.Chamfer/4,Radius=t.GradientAccent?8:t.PanelRadius/3,Width=56,Height=14,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,6) };
                stage.Children.Add(dock);
            }
            return stage;
        }
    }

    // In-game minimap chrome per theme: Tactical corner brackets and an accent bezel, Glass a
    // frosted rim, Minimal a hairline. The status card below carries location and travel figures.
    internal sealed partial class WpfFieldConsoleRuntime {
        Grid hudFrame;FrameworkElement[] hudCircleParts;Grid hudSquareFrame;
        TextBlock hudDistance,hudDistanceUnit,hudMinutes,hudTarget;FrameworkElement hudTravel;
        FrameworkElement BuildHudFrame() {
            var t=Theme;bool tactical=t.Chamfer>0,glass=t.GradientAccent;var accent=WpfFieldTheme.Accent;
            hudFrame=new Grid { IsHitTestVisible=false };hudSquareFrame=new Grid();
            var halo=new System.Windows.Shapes.Ellipse { Stroke=WpfFieldTheme.ColorBrush("#A60B1013"),StrokeThickness=glass?2:3 };
            var ring=new System.Windows.Shapes.Ellipse { Stroke=tactical?accent:WpfFieldTheme.ColorBrush(glass?"#3DFFFFFF":"#52FFFFFF"),StrokeThickness=tactical?1.6:glass?6:1 };
            var inner=new System.Windows.Shapes.Ellipse { Stroke=WpfFieldTheme.ColorBrush("#5CFFFFFF"),StrokeThickness=1 };
            var ticks=new System.Windows.Shapes.Path { Stroke=accent,StrokeThickness=1.3,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
            var circle=new List<FrameworkElement> { halo,ring };if(glass)circle.Add(inner);if(tactical)circle.Add(ticks);hudCircleParts=circle.ToArray();
            hudFrame.SizeChanged+=(s,e)=> {
                double d=Math.Max(0,Math.Min(hudFrame.ActualWidth,hudFrame.ActualHeight)),r=d/2;
                halo.Width=halo.Height=ring.Width=ring.Height=d;inner.Width=inner.Height=Math.Max(0,d-12);
                var geometry=new StreamGeometry();
                using(var context=geometry.Open()) for(int i=0;i<36;i++) { double a=i*Math.PI/18,len=i%9==0?9:i%3==0?6:3;var outer=new Point(r+(r-2)*Math.Sin(a),r-(r-2)*Math.Cos(a));var tip=new Point(r+(r-2-len)*Math.Sin(a),r-(r-2-len)*Math.Cos(a));context.BeginFigure(outer,false,false);context.LineTo(tip,true,false); }
                geometry.Freeze();ticks.Data=geometry;ticks.Width=ticks.Height=d;
            };
            double corner=WpfMapSurface.HudCorner;
            if(tactical) {
                hudSquareFrame.Children.Add(new Border { BorderBrush=WpfFieldTheme.ColorBrush("#38FFFFFF"),BorderThickness=new Thickness(1) });
                string[] brackets={"M 1.5,20 L 1.5,1.5 L 20,1.5","M 0,1.5 L 18.5,1.5 L 18.5,20","M 18.5,0 L 18.5,18.5 L 0,18.5","M 1.5,0 L 1.5,18.5 L 20,18.5"};
                for(int i=0;i<4;i++)hudSquareFrame.Children.Add(new System.Windows.Shapes.Path { Data=Geometry.Parse(brackets[i]),Stroke=accent,StrokeThickness=3,Width=20,Height=20,
                    HorizontalAlignment=i==0||i==3?HorizontalAlignment.Left:HorizontalAlignment.Right,VerticalAlignment=i<2?VerticalAlignment.Top:VerticalAlignment.Bottom });
            } else if(glass) {
                hudSquareFrame.Children.Add(new Border { BorderBrush=WpfFieldTheme.ColorBrush("#3DFFFFFF"),BorderThickness=new Thickness(5),CornerRadius=new CornerRadius(corner) });
                hudSquareFrame.Children.Add(new Border { BorderBrush=WpfFieldTheme.ColorBrush("#5CFFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Max(0,corner-5)),Margin=new Thickness(5) });
            } else hudSquareFrame.Children.Add(new Border { BorderBrush=WpfFieldTheme.ColorBrush("#52FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(corner) });
            foreach(var part in hudCircleParts)hudFrame.Children.Add(part);hudFrame.Children.Add(hudSquareFrame);
            return hudFrame;
        }
        void SyncHudFrame(bool circle,int opacity) {
            if(hudFrame==null)return;
            foreach(var part in hudCircleParts)part.Visibility=circle?Visibility.Visible:Visibility.Collapsed;
            hudSquareFrame.Visibility=circle?Visibility.Collapsed:Visibility.Visible;
            hudFrame.Opacity=Math.Max(.3,Math.Min(1,opacity/100.0));
        }
        Border BuildHudStatus() {
            var t=Theme;bool tactical=t.Chamfer>0,glass=t.GradientAccent;
            hudSector=WpfFieldTheme.Text("",tactical?19:glass?12:11,tactical||glass?WpfFieldTheme.AccentInk:WpfFieldTheme.Ink,true);hudSector.TextTrimming=TextTrimming.None;hudSector.HorizontalAlignment=HorizontalAlignment.Center;hudSector.VerticalAlignment=VerticalAlignment.Center;
            hudLocation=WpfFieldTheme.Text("",tactical?14:glass?14:13,WpfFieldTheme.Ink,true);hudLocation.VerticalAlignment=VerticalAlignment.Center;
            hudDistance=WpfFieldTheme.Text("",tactical?15:glass?14:13,glass?WpfFieldTheme.Ink:WpfFieldTheme.Accent,true);hudDistance.TextTrimming=TextTrimming.None;hudDistance.VerticalAlignment=VerticalAlignment.Center;
            hudDistanceUnit=WpfFieldTheme.Text("",11,WpfFieldTheme.Muted,true);hudDistanceUnit.TextTrimming=TextTrimming.None;hudDistanceUnit.Margin=new Thickness(tactical?2:3,0,8,0);hudDistanceUnit.VerticalAlignment=VerticalAlignment.Center;
            hudMinutes=WpfFieldTheme.Text("",tactical?13:12,WpfFieldTheme.Muted,true);hudMinutes.TextTrimming=TextTrimming.None;hudMinutes.Margin=new Thickness(8,0,0,0);hudMinutes.VerticalAlignment=VerticalAlignment.Center;
            hudTarget=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);hudTarget.VerticalAlignment=VerticalAlignment.Center;
            if(tactical)foreach(var text in new[]{hudSector,hudLocation,hudDistance,hudDistanceUnit,hudMinutes}) { text.FontFamily=WpfFieldTheme.DisplayFont;text.FontWeight=FontWeights.Bold; }
            else if(!glass)foreach(var text in new[]{hudSector,hudDistance,hudDistanceUnit,hudMinutes})text.FontFamily=WpfFieldTheme.MonoFont;
            var travel=new DockPanel { Margin=new Thickness(0,3,0,0),LastChildFill=true };
            if(glass) { var tile=new Border { Width=20,Height=20,CornerRadius=new CornerRadius(6),Background=WpfFieldTheme.AccentDim,Margin=new Thickness(0,0,8,0),Child=WpfIcons.Create("corner-down-right",12,WpfFieldTheme.Accent) };DockPanel.SetDock(tile,Dock.Left);travel.Children.Add(tile); }
            DockPanel.SetDock(hudMinutes,Dock.Right);travel.Children.Add(hudMinutes);
            var figure=new StackPanel { Orientation=Orientation.Horizontal };figure.Children.Add(hudDistance);figure.Children.Add(hudDistanceUnit);DockPanel.SetDock(figure,Dock.Left);travel.Children.Add(figure);travel.Children.Add(hudTarget);
            travel.Visibility=Visibility.Collapsed;hudTravel=travel;
            hudRoute=new WpfSatnavTicker();hudRoute.Height=18;
            var lines=new StackPanel { VerticalAlignment=VerticalAlignment.Center };var locationRow=new DockPanel();
            Border sectorBadge;
            if(tactical)sectorBadge=new Border { Background=WpfFieldTheme.Accent,MinWidth=46,Padding=new Thickness(6,0,6,0),Margin=new Thickness(0,0,10,0),Child=hudSector };
            else if(glass)sectorBadge=new Border { Background=WpfFieldTheme.AccentFill,CornerRadius=new CornerRadius(8),Padding=new Thickness(7,1,7,2),Margin=new Thickness(0,0,8,0),Child=hudSector,VerticalAlignment=VerticalAlignment.Center };
            else sectorBadge=new Border { Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(5,0,5,1),Margin=new Thickness(0,0,8,0),Child=hudSector,VerticalAlignment=VerticalAlignment.Center };
            if(!tactical) { DockPanel.SetDock(sectorBadge,Dock.Left);locationRow.Children.Add(sectorBadge); }
            locationRow.Children.Add(hudLocation);lines.Children.Add(locationRow);lines.Children.Add(travel);lines.Children.Add(hudRoute);
            UIElement content=lines;
            if(tactical) { var row=new DockPanel();DockPanel.SetDock(sectorBadge,Dock.Left);row.Children.Add(sectorBadge);lines.Margin=new Thickness(0,0,6,0);row.Children.Add(lines);content=row; }
            var surface=WpfFieldTheme.Surface(content,tactical?new Thickness(5):glass?new Thickness(11,5,12,5):new Thickness(10,5,10,5),true);
            if(!tactical)surface.Radius=Math.Min(t.PanelRadius,glass?16:8);
            return new Border { Child=surface,Margin=new Thickness(0,6,0,0) };
        }
        void SyncHudTravel(WpfMapState state,WpfMapState compact) {
            if(hudTravel==null)return;var t=Theme;
            bool travel=state.Target!=null&&compact.TravelMeters>=0&&!compact.TrackingPaused;
            hudTravel.Visibility=travel?Visibility.Visible:Visibility.Collapsed;
            hudRoute.Visibility=travel||string.IsNullOrEmpty(hudRoute.Text)?Visibility.Collapsed:Visibility.Visible;
            if(!travel)return;
            string distance=DestinationSearch.Distance(compact.TravelMeters);int split=distance.LastIndexOf(' ');
            hudDistance.Text=split<0?distance:distance.Substring(0,split);hudDistanceUnit.Text=t.Caps(split<0?"":distance.Substring(split+1));
            hudMinutes.Text=compact.TravelMinutes>0?compact.TravelMinutes.ToString(CultureInfo.CurrentCulture)+" "+t.Caps(Localization.Get("UnitMinutes")):compact.TravelCalculating?Localization.Get("FieldCalculating"):"";
            hudTarget.Text=state.Target.DisplayName;hudTarget.ToolTip=state.Target.DisplayName;
        }
    }
}
