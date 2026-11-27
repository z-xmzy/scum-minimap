using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DrawingPoint=System.Drawing.PointF;

namespace ScumMiniMap {
    // WPF owns layout, composition, text, input controls and both map views.
    // The existing native controller is a service behind this presentation engine.
    internal static class WpfFieldTheme {
        // The active interface theme. Every member reads it, so windows built after a theme change pick up the new look.
        internal static WpfThemeSpec Current=WpfThemes.Default;
        internal static Brush Ground { get { return Current.Brush(Current.Ground); } }
        internal static Brush Panel { get { return Current.Brush(Current.PanelSolid); } }
        internal static Brush Raised { get { return Current.Brush(Current.Raised); } }
        internal static Brush Hover { get { return Current.Brush(Current.Hover); } }
        internal static Brush Line { get { return Current.Brush(Current.Line); } }
        internal static Brush Line2 { get { return Current.Brush(Current.Line2); } }
        internal static Brush Ink { get { return Current.Brush(Current.Ink); } }
        internal static Brush Muted { get { return Current.Brush(Current.Muted); } }
        internal static Brush Dim { get { return Current.Brush(Current.Dim); } }
        // Legacy name kept for existing callers: Amber is the interface accent.
        internal static Brush Amber { get { return Accent; } }
        internal static Brush Cyan { get { return Current.Brush(Current.Info); } }
        internal static Brush Accent { get { return Current.Brush(Current.Accent); } }
        internal static Brush AccentFill { get { return Current.AccentFill; } }
        internal static Brush AccentDim { get { return Current.Brush(Current.AccentSoft); } }
        internal static Brush AccentInk { get { return Current.Brush(Current.AccentInk); } }
        internal static Brush MapAmber { get { return Current.Brush(Current.Warning); } }
        internal static Brush Warn { get { return Current.Brush(Current.Danger); } }
        internal static Brush Success { get { return Current.Brush(Current.Success); } }
        internal static Brush Glass { get { return Current.Brush(Current.Panel); } }
        internal static FontFamily UiFont { get { return Current.UiFont; } }
        internal static FontFamily DisplayFont { get { return Current.DisplayFont; } }
        internal static FontFamily MonoFont { get { return Current.MonoFont; } }
        // Small section label: tracked condensed capitals in the tactical theme, quiet sentence case elsewhere.
        internal static TextBlock Eyebrow(string text) {
            var t=Text(Current.Caps(text),Current.Uppercase?13:12,Muted,true);
            if(Current.Uppercase) { t.FontFamily=DisplayFont;t.FontWeight=FontWeights.Bold; }
            t.Margin=new Thickness(0,0,0,6);return t;
        }
        internal static Brush ColorBrush(string hex) { var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();return b; }
        // Window and page titles: condensed caps in Tactical, a quieter weight in Minimal.
        internal static TextBlock Heading(string text,double size) {
            var t=Current;var block=Text(t.Caps(text),t.Uppercase?size+6:t.Tabs==WpfTabStyle.Rail?size-2:size,null,true);
            if(t.Uppercase) { block.FontFamily=DisplayFont;block.FontWeight=FontWeights.Bold; }
            return block;
        }
        internal static TextBlock Text(string text,double size=14,Brush ink=null,bool bold=false) { return new TextBlock { Text=text,FontSize=size,Foreground=ink??Ink,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis }; }
        internal static Button Button(string label,Action action,bool primary=false) { var b=new Button { Content=label,MinHeight=36,Padding=new Thickness(14,6,14,6),Margin=new Thickness(4,0,0,0),Background=primary?AccentFill:Raised,Foreground=primary?AccentInk:Ink,BorderBrush=primary?Brushes.Transparent:Line,BorderThickness=new Thickness(1) };
            // Transparent (text-style) buttons stay borderless; filled buttons get the hairline frame.
            b.Loaded+=(s,e)=> { if(ReferenceEquals(b.Background,Brushes.Transparent))b.BorderThickness=new Thickness(0); };b.Click+=(s,e)=> { if(b.IsEnabled)action(); };return b; }
        internal static Border Card(UIElement content,Thickness? padding=null) { return new Border { Background=Glass,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Min(Current.PanelRadius,16)),Padding=padding??new Thickness(16),Child=content }; }
        // Themed floating surface: chamfered in the tactical theme, rounded otherwise.
        internal static ChamferBorder Surface(UIElement content,Thickness? padding=null,bool solid=false) {
            return new ChamferBorder { Background=solid?Panel:Glass,BorderBrush=Line,BorderThickness=1,Cut=Current.Chamfer,Radius=Current.PanelRadius,Hatch=Current.Chamfer>0,Padding=padding??new Thickness(16),Child=content };
        }
        // Labels stay in Button.Content so language refresh and screen readers keep the same text.
        // Glyph arguments may be a Lucide icon name or a legacy Segoe MDL2 character.
        internal static void IconLabel(ContentControl button,string glyph,bool stacked=false,double iconSize=0,bool caps=false) {
            var panel=new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty,stacked?Orientation.Vertical:Orientation.Horizontal);
            FrameworkElementFactory icon;
            string lucide=WpfIcons.Has(glyph)?glyph:LegacyIcon(glyph);
            if(lucide!=null) {
                double size=iconSize>0?iconSize:stacked?20:17;
                icon=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
                icon.SetValue(System.Windows.Shapes.Path.DataProperty,WpfIcons.Geometry(lucide,size));
                icon.SetValue(FrameworkElement.WidthProperty,size);icon.SetValue(FrameworkElement.HeightProperty,size);
                icon.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty,Math.Max(1.25,size/12));
                icon.SetValue(System.Windows.Shapes.Shape.StrokeStartLineCapProperty,PenLineCap.Round);icon.SetValue(System.Windows.Shapes.Shape.StrokeEndLineCapProperty,PenLineCap.Round);
                icon.SetValue(System.Windows.Shapes.Shape.StrokeLineJoinProperty,PenLineJoin.Round);
                icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding { Path=new PropertyPath(System.Windows.Documents.TextElement.ForegroundProperty),RelativeSource=System.Windows.Data.RelativeSource.Self });
            } else {
                icon=new FrameworkElementFactory(typeof(TextBlock));
                icon.SetValue(TextBlock.FontFamilyProperty,new FontFamily("Segoe MDL2 Assets"));
                icon.SetValue(TextBlock.FontSizeProperty,iconSize>0?iconSize:stacked?20.0:17.0);
                icon.SetValue(TextBlock.TextProperty,glyph);
            }
            icon.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            icon.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            double gap=iconSize>0?Math.Round(iconSize*.45):stacked?5:10;
            icon.SetValue(FrameworkElement.MarginProperty,stacked?new Thickness(0,0,0,gap):new Thickness(0,0,gap,0));
            var label=new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding { Converter=caps?WpfKit.CapsConverter.Instance:null });
            label.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
            label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            label.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            panel.AppendChild(icon);
            if(stacked) {
                // Stacked labels sit in narrow tab and tool cells: shrink long words (CROSSHAIR, MY MARKERS) instead of cutting them.
                var fit=new FrameworkElementFactory(typeof(Viewbox));
                fit.SetValue(Viewbox.StretchProperty,Stretch.Uniform);fit.SetValue(Viewbox.StretchDirectionProperty,StretchDirection.DownOnly);
                fit.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
                label.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.None);fit.AppendChild(label);panel.AppendChild(fit);
            } else panel.AppendChild(label);
            button.ContentTemplate=new DataTemplate{VisualTree=panel};
            System.Windows.Automation.AutomationProperties.SetName(button,Convert.ToString(button.Content));
        }
        // Segoe MDL2 glyphs used by older call sites, mapped to their Lucide equivalents.
        static string LegacyIcon(string glyph) {
            switch(glyph) {
                case "\uE721":return "search";case "\uE716":return "users";case "\uE72E":return "radio";case "\uE81E":return "map";
                case "\uE707":return "map-pin";case "\uE81B":return "route";case "\uE897":return "circle-help";case "\uE713":return "settings";
                case "\uE8BB":return "x";case "\uE76C":return "chevron-right";case "\uE76B":return "chevron-left";case "\uE710":return "plus";
                case "\uE738":return "minus";case "\uE74D":return "trash-2";case "\uE70F":return "pencil";case "\uE8C8":return "copy";
                case "\uE73E":return "check";case "\uE946":return "info";case "\uE72C":return "refresh-cw";case "\uE896":return "download";
                case "\uE7C1":return "flag";case "\uE734":return "star";case "\uE81D":return "locate-fixed";case "\uE7B3":return "eye";
                default:return null;
            }
        }
        internal static System.Windows.Shapes.Path Icon(string name,double size=18,Brush ink=null) { return WpfIcons.Create(name,size,ink); }
        internal static UIElement Brand() {
            var row=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            UIElement mark;
            if(Current.GradientAccent) {
                var tile=new Border { Width=30,Height=30,CornerRadius=new CornerRadius(9),Background=AccentFill,Margin=new Thickness(0,0,10,0),Child=WpfIcons.Create("compass",17,Brushes.White) };mark=tile;
            } else if(Current.Tabs==WpfTabStyle.Rail) {
                var tile=new Border { Width=26,Height=26,CornerRadius=new CornerRadius(7),Background=Ink,Margin=new Thickness(0,0,10,0),Child=WpfIcons.Create("navigation-2",14,Ground) };mark=tile;
            } else {
                mark=new System.Windows.Shapes.Path { Data=Geometry.Parse("M12,1 L21,22 L12,17 L3,22 Z"),Fill=Accent,Width=16,Height=19,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center };
            }
            var name=Text(Current.Uppercase?"SCUM MINIMAP":"SCUM MiniMap",Current.Uppercase?17:14,Ink,true);
            if(Current.Uppercase) { name.FontFamily=DisplayFont;name.FontWeight=FontWeights.ExtraBold; }
            row.Children.Add(mark);row.Children.Add(name);return row;
        }
        internal static ResourceDictionary Resources() { return WpfThemes.Resources(Current); }
    }

    // Lucide SVG paths, adapted to cached WPF vectors. See resources/marker-icons/LICENSE.txt.
    internal static class WpfMarkerIcons {
        static readonly Dictionary<string,ImageSource> images=Build();
        static Dictionary<string,ImageSource> Build() {
            var result=new Dictionary<string,ImageSource>(StringComparer.Ordinal);
            Add(result,"pin","#63CDBA",new[]{"M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0","M9 10 A3 3 0 1 0 15 10 A3 3 0 1 0 9 10 Z"});
            Add(result,"house","#82D9AB",new[]{"M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8","M3 10a2 2 0 0 1 .709-1.528l7-6a2 2 0 0 1 2.582 0l7 6A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"});
            Add(result,"skull","#FF827B",new[]{"m12.5 17-.5-1-.5 1h1z","M15 22a1 1 0 0 0 1-1v-1a2 2 0 0 0 1.56-3.25 8 8 0 1 0-11.12 0A2 2 0 0 0 8 20v1a1 1 0 0 0 1 1z","M14 12 A1 1 0 1 0 16 12 A1 1 0 1 0 14 12 Z","M8 12 A1 1 0 1 0 10 12 A1 1 0 1 0 8 12 Z"});
            Add(result,"car","#83BDF7",new[]{"M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2","M9 17h6","M5 17 A2 2 0 1 0 9 17 A2 2 0 1 0 5 17 Z","M15 17 A2 2 0 1 0 19 17 A2 2 0 1 0 15 17 Z"});
            Add(result,"box","#F0C574",new[]{"M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z","m3.3 7 8.7 5 8.7-5","M12 22V12"});
            Add(result,"tent","#D5B4F4",new[]{"M3.5 21 14 3","M20.5 21 10 3","M15.5 21 12 15l-3.5 6","M2 21h20"});
            Add(result,"fuel","#F2A16C",new[]{"M14 13h2a2 2 0 0 1 2 2v2a2 2 0 0 0 4 0v-6.998a2 2 0 0 0-.59-1.42L18 5","M14 21V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v16","M2 21h13","M3 9h11"});
            Add(result,"flag","#E7D7A4",new[]{"M4 22V4a1 1 0 0 1 .4-.8A6 6 0 0 1 8 2c3 0 5 2 7.333 2q2 0 3.067-.8A1 1 0 0 1 20 4v10a1 1 0 0 1-.4.8A6 6 0 0 1 16 16c-3 0-5-2-8-2a6 6 0 0 0-4 1.528"});
            return result;
        }
        static void Add(Dictionary<string,ImageSource> result,string id,string color,string[] paths) {
            var drawing=new DrawingGroup();var ink=WpfFieldTheme.ColorBrush(color);
            var pen=new Pen(ink,1.9) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
            using(var dc=drawing.Open()) {
                dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));
                dc.DrawEllipse(WpfFieldTheme.Ground,new Pen(ink,1),new Point(16,16),15,15);
                dc.PushTransform(new TranslateTransform(4,4));foreach(var path in paths)dc.DrawGeometry(null,pen,Geometry.Parse(path));dc.Pop();
            }
            drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();result.Add(id,image);
        }
        internal static ImageSource Image(string id) { return images[WaypointIcons.Normalize(id)]; }
        internal static FrameworkElement Caption(string icon,string text) {
            var row=new DockPanel();
            var picture=new System.Windows.Controls.Image {Source=Image(icon),Width=28,Height=28,Margin=new Thickness(0,0,8,0)};DockPanel.SetDock(picture,Dock.Left);row.Children.Add(picture);
            var label=WpfFieldTheme.Text(text,14);label.VerticalAlignment=VerticalAlignment.Center;label.TextTrimming=TextTrimming.CharacterEllipsis;row.Children.Add(label);return row;
        }
        internal static FrameworkElement Tile(string icon,string text) {
            var stack=new StackPanel { HorizontalAlignment=HorizontalAlignment.Center };
            var picture=new System.Windows.Controls.Image { Source=Image(icon),Width=30,Height=30,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,6) };stack.Children.Add(picture);
            var label=WpfFieldTheme.Text(text,12,WpfFieldTheme.Ink);label.HorizontalAlignment=HorizontalAlignment.Center;label.TextTrimming=TextTrimming.CharacterEllipsis;label.MaxWidth=90;stack.Children.Add(label);return stack;
        }
        internal static ListBox Picker() {
            var panel=new FrameworkElementFactory(typeof(UniformGrid));panel.SetValue(UniformGrid.ColumnsProperty,4);
            var list=new ListBox { Tag="marker-icon",ItemsPanel=new ItemsPanelTemplate(panel),Margin=new Thickness(0,4,0,4),BorderThickness=new Thickness(0),Background=Brushes.Transparent };
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            // Bare template: the tiles never scroll, and the system template paints a light box while disabled.
            var template=new ControlTemplate(typeof(ListBox));template.VisualTree=new FrameworkElementFactory(typeof(ItemsPresenter));list.Template=template;
            list.IsEnabledChanged+=(s,e)=>list.Opacity=list.IsEnabled?1:.45;
            System.Windows.Automation.AutomationProperties.SetName(list,Localization.Get("MarkerIcon"));
            foreach(string id in WaypointIcons.Ids) {
                string key="MarkerIcon_"+id;string name=Localization.Get(key);
                var item=new ListBoxItem { Tag=id,Content=Tile(id,name),ToolTip=name,Padding=new Thickness(6,10,6,8),Margin=new Thickness(0,0,6,6),HorizontalContentAlignment=HorizontalAlignment.Center };
                System.Windows.Automation.AutomationProperties.SetName(item,name);list.Items.Add(item);
            }
            list.SelectedIndex=0;return list;
        }
        internal static string Selected(ListBox picker) { var item=picker.SelectedItem as ListBoxItem;return item==null?"pin":(string)item.Tag; }
    }
    internal sealed class WpfSatnavTicker:Grid {
        readonly TextBlock label;
        readonly TranslateTransform scroll=new TranslateTransform();
        double lastOverflow=-1;
        static readonly DependencyProperty ProgressProperty=DependencyProperty.Register("Progress",typeof(double),typeof(WpfSatnavTicker),new PropertyMetadata(0.0,(s,e)=> { var ticker=(WpfSatnavTicker)s;ticker.scroll.X=-Math.Max(0,ticker.lastOverflow)*(double)e.NewValue; }));
        internal WpfSatnavTicker() {
            ClipToBounds=true;Height=20;var canvas=new Canvas();label=WpfFieldTheme.Text("",14,WpfFieldTheme.Cyan);label.TextTrimming=TextTrimming.None;label.RenderTransform=scroll;canvas.Children.Add(label);Children.Add(canvas);
            SizeChanged+=(s,e)=>UpdateScroll();IsVisibleChanged+=(s,e)=>UpdateScroll();Loaded+=(s,e)=>UpdateScroll();Unloaded+=(s,e)=>BeginAnimation(ProgressProperty,null);
        }
        internal string Text { get { return label.Text; } set { if(label.Text==value)return;label.Text=value??"";ToolTip=label.Text;System.Windows.Automation.AutomationProperties.SetName(this,label.Text);UpdateScroll(); } }
        internal Brush Foreground { get { return label.Foreground; } set { label.Foreground=value; } }
        internal double Overflow { get { label.Measure(new Size(double.PositiveInfinity,20));return Math.Max(0,label.DesiredSize.Width-ActualWidth); } }
        void UpdateScroll() {
            double overflow=Overflow;
            if(!SystemParameters.ClientAreaAnimation||!IsVisible||ActualWidth<=0||overflow<=1) { BeginAnimation(ProgressProperty,null);scroll.X=0;lastOverflow=-1;return; }
            lastOverflow=overflow;scroll.X=-overflow*(double)GetValue(ProgressProperty);
            if(HasAnimatedProperties)return;
            double travel=Math.Max(2,overflow/28);
            var animation=new DoubleAnimationUsingKeyFrames { Duration=TimeSpan.FromSeconds(travel+2),AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(travel+1))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(travel+2))));
            BeginAnimation(ProgressProperty,animation);
        }
    }
    internal sealed class WpfZoneCanvas:FrameworkElement {
        readonly ZoneEditor editor;readonly bool source;
        BitmapSource frame;bool dirty=true;int clickCount;
        internal WpfZoneCanvas(ZoneEditor editor,bool source) {
            this.editor=editor;this.source=source;ClipToBounds=true;Focusable=true;Cursor=Cursors.Cross;
            SizeChanged+=(s,e)=>Redraw();
            MouseDown+=(s,e)=> { Focus();clickCount=e.ClickCount;if(e.ChangedButton==MouseButton.Left&&e.ClickCount==2) { Send("double",e.GetPosition(this),System.Windows.Forms.MouseButtons.Left,0,2);e.Handled=true;return; }Send("down",e.GetPosition(this),Button(e.ChangedButton));CaptureMouse();e.Handled=true; };
            MouseMove+=(s,e)=> { if(IsMouseCaptured)Send("move",e.GetPosition(this),e.MiddleButton==MouseButtonState.Pressed?System.Windows.Forms.MouseButtons.Middle:System.Windows.Forms.MouseButtons.Left); };
            MouseUp+=(s,e)=> { var button=Button(e.ChangedButton);Send("up",e.GetPosition(this),button);if(clickCount<2)Send("click",e.GetPosition(this),button);ReleaseMouseCapture();e.Handled=true; };
            MouseWheel+=(s,e)=> { Send("wheel",e.GetPosition(this),System.Windows.Forms.MouseButtons.None,e.Delta);e.Handled=true; };
            LostMouseCapture+=(s,e)=>editor.WpfCancelPointer();
        }
        static System.Windows.Forms.MouseButtons Button(MouseButton button) { return button==MouseButton.Middle?System.Windows.Forms.MouseButtons.Middle:button==MouseButton.Right?System.Windows.Forms.MouseButtons.Right:System.Windows.Forms.MouseButtons.Left; }
        void Send(string kind,Point at,System.Windows.Forms.MouseButtons button,int delta=0,int clicks=1) { editor.WpfPointer(source,kind,button,(int)at.X,(int)at.Y,delta,clicks);Redraw(); }
        internal void Redraw() { dirty=true;InvalidateVisual(); }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);if(ActualWidth<1||ActualHeight<1||editor.IsDisposed)return;
            if(dirty) {
                using(var bitmap=editor.WpfRender(source,(int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight))) {
                    var bits=bitmap.LockBits(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    try { frame=BitmapSource.Create(bitmap.Width,bitmap.Height,96,96,PixelFormats.Pbgra32,null,bits.Scan0,bits.Stride*bitmap.Height,bits.Stride);frame.Freeze(); }finally { bitmap.UnlockBits(bits); }
                }dirty=false;
            }
            if(frame!=null)dc.DrawImage(frame,new Rect(0,0,ActualWidth,ActualHeight));
        }
    }
    internal sealed class WpfWindowOwner:System.Windows.Forms.IWin32Window { public IntPtr Handle { get;private set; }internal WpfWindowOwner(IntPtr handle) { Handle=handle; } }
    internal sealed class WpfMapLabel { internal string Text;internal Point At;internal bool Major,Bounded;internal double Clearance=9,SafeWidth,SafeHeight,Angle,FontSize;internal Brush Ink,Background;internal BunkerLabel Source; }
    internal sealed class WpfPoiCluster {
        internal DrawingPoint Point;
        internal readonly List<ScumMapMarker> Members=new List<ScumMapMarker>();
        internal double Radius;
        internal bool Mixed;
        internal DrawingGroup Icon;
        internal int IconColor;
        internal Pen Outline;
    }
    internal sealed class WpfPoiCache {
        internal bool Clustering;
        internal double Side;
        internal double Spacing;
        internal float Zoom;
        internal string Filter;
        internal int Count;
        internal WpfPoiCluster[] Clusters;
        internal ScumMapMarker[] OrderedMarkers;
        internal Dictionary<ScumMapMarker,string> Labels;
        internal string LabelLanguage;
    }
    internal sealed partial class WpfMapSurface:FrameworkElement {
        // Layered windows pass fully transparent pixels to the game before WPF
        // hit testing runs. One alpha step keeps the tactical map clickable at 0%.
        static readonly Brush tacticalHitSurface=WpfFieldTheme.ColorBrush("#01000000");
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[],StreamGeometry> zoneGeometryCache=new System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[],StreamGeometry>();
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[][],StreamGeometry> habitatGeometryCache=new System.Runtime.CompilerServices.ConditionalWeakTable<DrawingPoint[][],StreamGeometry>();
        sealed class GeometryBounds { internal Rect Value;internal GeometryBounds(StreamGeometry geometry) { Value=geometry.Bounds; } }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<StreamGeometry,GeometryBounds> geometryBoundsCache=new System.Runtime.CompilerServices.ConditionalWeakTable<StreamGeometry,GeometryBounds>();
        static StreamGeometry WorldGeometry(DrawingPoint[][] rings) {
            var geometry=new StreamGeometry { FillRule=FillRule.Nonzero };using(var context=geometry.Open())foreach(var ring in rings) { if(ring==null||ring.Length<3)continue;context.BeginFigure(new Point(ring[0].X,ring[0].Y),true,true);context.PolyLineTo(ring.Skip(1).Select(p=>new Point(p.X,p.Y)).ToArray(),true,false); }geometry.Freeze();return geometry;
        }
        internal WpfMapState State;
        internal bool Compact;
        internal bool DirectTerrain;
        enum MapPass { All,Terrain,Trail,Annotations,Shared,Live }
        readonly VisualCollection mapLayers;
        readonly DrawingVisual terrainLayer=new DrawingVisual(),trailLayer=new DrawingVisual(),annotationLayer=new DrawingVisual(),sharedLayer=new DrawingVisual(),liveLayer=new DrawingVisual();
        string sharedLayerKey;
        internal int SharedLayerBuilds;
        string terrainLayerKey,annotationLayerKey,trailLayerKey,terrainDrawingKey,queuedTerrainKey;
        DrawingGroup terrainDrawing,trailDrawing,annotationDrawing,staticDrawing;
        CameraScene presentedCameraScene;
        double staticSide,staticWidth,staticHeight;
        DrawingPoint staticCentre;
        string staticOverlayKey;
        internal bool ZoomGestureActive;
        bool layersDrawn,layersCameraRasterized;
        internal int TerrainRevision,TerrainLayerBuilds,AnnotationLayerBuilds,LiveLayerBuilds;
        protected override int VisualChildrenCount { get { return mapLayers==null?0:mapLayers.Count; } }
        protected override Visual GetVisualChild(int index) { return mapLayers[index]; }
        void ClearTacticalLayers() {
            ResetCameraScenes();
            if(!layersDrawn)return;foreach(DrawingVisual layer in mapLayers)using(layer.RenderOpen()) { }
            layersDrawn=layersCameraRasterized=false;sharedLayerKey=terrainLayerKey=annotationLayerKey=trailLayerKey=terrainDrawingKey=queuedTerrainKey=null;terrainDrawing=trailDrawing=annotationDrawing=staticDrawing=null;presentedCameraScene=null;staticOverlayKey=null;
        }
        internal void PublishState(WpfMapState state) {
            Dispatcher.VerifyAccess();State=state;
            if(ActualWidth<1||ActualHeight<1||CameraInteracting) { RequestRedraw();return; }
            UpdateTacticalLayers();
        }
        string CameraLayerKey() {
            var centre=CameraCentre(State,ActualWidth,ActualHeight,Compact);
            return string.Join("|",new object[]{ActualWidth,ActualHeight,Compact,State.Shape,State.Zoom,Identity(State.Bunker),Identity(State.BunkerImage),centre.X,centre.Y,Rotation(State,Compact),VisualTreeHelper.GetDpi(this).DpiScaleX});
        }
        static int Identity(object value) { return value==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
        string SceneContentKey() { return OverlayContentKey()+"|"+(State==null?0:State.Opacity)+"|"+TerrainRevision; }
        string OverlayContentKey() {
            if(State==null)return "";
            return string.Join("|",new object[]{Identity(State.Store),State.Store==null?"":State.Store.GetFilterHashKey(),State.Store!=null&&State.Store.MasterEnabled,State.Shape,State.Grid,State.GridLabels,State.GridOpacity,State.LabelSize,State.SmartLabels,Identity(Terrain),Identity(BaseMap),Identity(State.Bunker),Identity(State.BunkerImage),State.History,State.HistoryColor,Identity(State.Trail),State.Zones,State.Waypoints,State.Labels,State.PoiClustering,State.ZoneRevision,Identity(State.Saved),Localization.CurrentCode});
        }
        void UpdateTacticalLayers() {
            if(ActualWidth<1||ActualHeight<1)return;
            if(State==null) {
                ClearTacticalLayers();using(var dc=terrainLayer.RenderOpen())dc.DrawRectangle(tacticalHitSurface,null,new Rect(0,0,ActualWidth,ActualHeight));layersDrawn=true;return;
            }
            string overlayKey=OverlayContentKey();
            if(cameraScene!=null&&(cameraScene.Opacity!=State.Opacity||cameraScene.OverlayKey!=overlayKey))cameraScene=null;
            // During a zoom gesture keep reprojecting the last complete snapshot even
            // while its replacement is being built: rebuilding the vector terrain every
            // wheel step is what made seamless zoom stutter.
            CameraScene previewScene=cameraScene;
            if(State.Bunker==null&&CameraInteracting&&!DirectTerrain&&previewScene!=null&&previewScene.Width==ActualWidth&&previewScene.Height==ActualHeight&&previewScene.Opacity==State.Opacity) {
                // The stationary snapshot already contains all static layers.
                // Camera motion only reprojects it; defer terrain preparation,
                // annotation recording and replacement raster jobs until idle.
                string preview="preview|"+CameraLayerKey();
                if(terrainLayerKey!=preview||!object.ReferenceEquals(presentedCameraScene,previewScene)) {
                    using(var dc=terrainLayer.RenderOpen())DrawCameraScene(dc,previewScene,staticDrawing,ActualWidth,ActualHeight);
                    terrainLayerKey=preview;presentedCameraScene=previewScene;
                }
                if(!layersCameraRasterized) { using(trailLayer.RenderOpen()) { }using(annotationLayer.RenderOpen()) { } }
                layersCameraRasterized=true;
                UpdateSharedLayer(preview);using(var dc=liveLayer.RenderOpen())DrawMapPass(dc,MapPass.Live);LiveLayerBuilds++;layersDrawn=true;return;
            }
            // The native game map and the zoomed map have different sizes and opacity.
            // Reproject the last recorded overlays while the first zoom bitmap is unavailable.
            if(ZoomGestureActive && CameraInteracting && State.Bunker==null && !DirectTerrain &&
                staticSide>0 && staticOverlayKey==overlayKey) {
                string preview="overview|"+CameraLayerKey()+"|"+State.Opacity;
                if(terrainLayerKey!=preview) {
                    double side=MapSide(State,ActualWidth,ActualHeight,false);
                    var centre=CameraCentre(State,ActualWidth,ActualHeight,false);
                    double scale=side/staticSide;
                    using(var dc=terrainLayer.RenderOpen()) {
                        DrawMapPass(dc,MapPass.Terrain,true);
                        dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));
                        dc.PushTransform(new MatrixTransform(scale,0,0,scale,
                            ActualWidth/2+(staticCentre.X-centre.X)*side-staticWidth*scale/2,
                            ActualHeight/2+(staticCentre.Y-centre.Y)*side-staticHeight*scale/2));
                        dc.DrawDrawing(trailDrawing);dc.DrawDrawing(annotationDrawing);dc.Pop();dc.Pop();
                    }
                    terrainLayerKey=preview;presentedCameraScene=null;
                }
                if(!layersCameraRasterized) { using(trailLayer.RenderOpen()) { }using(annotationLayer.RenderOpen()) { } }
                layersCameraRasterized=true;
                UpdateSharedLayer(preview);using(var dc=liveLayer.RenderOpen())DrawMapPass(dc,MapPass.Live);LiveLayerBuilds++;layersDrawn=true;return;
            }
            string camera=CameraLayerKey(),store=Identity(State.Store)+"|"+(State.Store==null?"":State.Store.GetFilterHashKey()+"|"+State.Store.MasterEnabled);
            string terrainKey=string.Join("|",new object[]{camera,store,State.Opacity,State.Grid,State.GridLabels,State.GridOpacity,State.LabelSize,TerrainRevision,Identity(Terrain),Identity(BaseMap),Localization.CurrentCode});
            if(terrainDrawing==null||terrainDrawingKey!=terrainKey) {
                terrainDrawing=new DrawingGroup();using(var dc=terrainDrawing.Open())DrawMapPass(dc,MapPass.Terrain);terrainDrawing.Freeze();terrainDrawingKey=terrainKey;TerrainLayerBuilds++;
            }
            string trailKey=camera+"|"+State.History+"|"+State.HistoryColor+"|"+Identity(State.Trail);
            bool trailChanged=trailLayerKey!=trailKey;
            if(trailChanged) { trailDrawing=new DrawingGroup();using(var dc=trailDrawing.Open())DrawMapPass(dc,MapPass.Trail);trailDrawing.Freeze();trailLayerKey=trailKey; }
            string annotation=string.Join("|",new object[]{camera,store,State.Zones,State.Waypoints,State.Labels,State.PoiClustering,State.LabelSize,State.ZoneRevision,Identity(State.Saved),Localization.CurrentCode,CameraInteracting});
            bool annotationChanged=annotationLayerKey!=annotation+"|"+PoiSceneBuilds;
            if(annotationChanged) {
                annotationDrawing=new DrawingGroup();using(var dc=annotationDrawing.Open())DrawMapPass(dc,MapPass.Annotations);annotationDrawing.Freeze();annotationLayerKey=annotation+"|"+PoiSceneBuilds;AnnotationLayerBuilds++;
            }
            staticWidth=ActualWidth;staticHeight=ActualHeight;staticSide=MapSide(State,staticWidth,staticHeight,false);
            staticCentre=CameraCentre(State,staticWidth,staticHeight,false);staticOverlayKey=overlayKey;
            string staticKey=terrainKey+"|"+trailKey+"|"+annotationLayerKey;
            if(State.Bunker==null&&!Compact&&!DirectTerrain&&queuedTerrainKey!=staticKey) {
                // Software composition must not replay dense annotation vectors on
                // every gesture frame. Keep live guidance out of this snapshot.
                staticDrawing=new DrawingGroup();staticDrawing.Children.Add(terrainDrawing);staticDrawing.Children.Add(trailDrawing);staticDrawing.Children.Add(annotationDrawing);staticDrawing.Freeze();
                QueueCameraScene(staticDrawing,ActualWidth,ActualHeight);queuedTerrainKey=staticKey;
            }
            bool raster=State.Bunker==null&&CameraInteracting&&!DirectTerrain;
            string presentation=(raster?staticKey:terrainKey)+"|"+raster;
            if(terrainLayerKey!=presentation||raster&&!object.ReferenceEquals(presentedCameraScene,cameraScene)) {
                using(var dc=terrainLayer.RenderOpen()) { if(raster)DrawCameraScene(dc,staticDrawing,ActualWidth,ActualHeight);else dc.DrawDrawing(terrainDrawing); }
                terrainLayerKey=presentation;presentedCameraScene=cameraScene;
            }
            if(trailChanged||layersCameraRasterized!=raster)using(var dc=trailLayer.RenderOpen()) { if(!raster)dc.DrawDrawing(trailDrawing); }
            if(annotationChanged||layersCameraRasterized!=raster)using(var dc=annotationLayer.RenderOpen()) { if(!raster)dc.DrawDrawing(annotationDrawing); }
            layersCameraRasterized=raster;
            UpdateSharedLayer(camera);using(var dc=liveLayer.RenderOpen())DrawMapPass(dc,MapPass.Live);LiveLayerBuilds++;layersDrawn=true;
        }
        internal bool CameraInteracting;
        DispatcherTimer cameraFrame;
        bool cameraFramePending;
        TimeSpan lastCameraComposition=TimeSpan.MinValue;
        internal int CameraFramesPresented;
        void CancelCameraFrame() {
            if(cameraFrame!=null)cameraFrame.Stop();
            if(cameraFramePending)CompositionTarget.Rendering-=CameraCompositionFrame;
            cameraFramePending=false;
        }
        void PresentCameraFrame() {
            if(!cameraFramePending)return;
            CancelCameraFrame();
            if(!CameraInteracting)return;
            CameraFramesPresented++;InvalidateVisual();
        }
        void CameraCompositionFrame(object sender,EventArgs args) {
            var frame=args as RenderingEventArgs;
            // WPF can raise Rendering more than once for the same composition
            // timestamp. Keep one invalidation per distinct frame.
            if(frame!=null) { if(lastCameraComposition==frame.RenderingTime)return;lastCameraComposition=frame.RenderingTime; }
            PresentCameraFrame();
        }
        sealed class CameraScene {
            internal DrawingGroup Drawing;internal BitmapSource Image;internal double Width,Height,Side;internal DrawingPoint Centre;internal int Generation,Opacity;internal string ContentKey,OverlayKey;
        }
        readonly object cameraGate=new object();
        CameraScene pendingCameraScene,completedCameraScene,cameraScene,idleCameraScene;
        bool cameraWorker,cameraApplyPosted;
        int cameraGeneration;
        internal int CameraSceneBuilds;
        internal void BeginCameraFrames() { if(!CameraInteracting)cameraScene=idleCameraScene!=null&&State!=null&&idleCameraScene.Opacity==State.Opacity&&idleCameraScene.OverlayKey==OverlayContentKey()?idleCameraScene:null;CameraInteracting=true; }
        internal void EndCameraFrames() {
            CameraInteracting=false;CancelCameraFrame();lastCameraComposition=TimeSpan.MinValue;
            // A short pause must not discard the only ready snapshot before its replacement arrives.
            if(cameraScene!=null)idleCameraScene=cameraScene;
            cameraScene=null;
        }
        void ResetCameraScenes() {
            lock(cameraGate) { cameraGeneration++;pendingCameraScene=null;completedCameraScene=null; }
            cameraScene=null;idleCameraScene=null;queuedTerrainKey=null;staticOverlayKey=null;ZoomGestureActive=false;
        }
        void QueueCameraScene(DrawingGroup drawing,double width,double height) {
            drawing.Freeze();var scene=new CameraScene { Drawing=drawing,Width=width,Height=height,Side=MapSide(State,width,height,false),Centre=CameraCentre(State,width,height,false),Opacity=State.Opacity,ContentKey=SceneContentKey(),OverlayKey=OverlayContentKey() };
            lock(cameraGate) {
                scene.Generation=cameraGeneration;pendingCameraScene=scene;
                if(cameraWorker)return;cameraWorker=true;
            }
            var dispatcher=Dispatcher;
            System.Threading.Tasks.Task.Run(()=> {
                var thread=System.Threading.Thread.CurrentThread;var priority=thread.Priority;
                try {
                    thread.Priority=System.Threading.ThreadPriority.BelowNormal;
                    while(true) {
                        CameraScene next;lock(cameraGate) { next=pendingCameraScene;pendingCameraScene=null;if(next==null) { cameraWorker=false;return; } }
                        try {
                            // Bound software sampling even on a 4K/high-DPI layered window.
                            // Only immutable drawings cross to this worker.
                            double scale=Math.Min(1,(CameraInteracting?1200:1600)/Math.Max(next.Width,next.Height));int pixelsWide=Math.Max(1,(int)Math.Ceiling(next.Width*scale)),pixelsHigh=Math.Max(1,(int)Math.Ceiling(next.Height*scale));
                            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) { dc.PushTransform(new ScaleTransform(scale,scale));dc.DrawDrawing(next.Drawing);dc.Pop(); }
                            var bitmap=new RenderTargetBitmap(pixelsWide,pixelsHigh,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();next.Image=bitmap;next.Drawing=null;
                            bool post=false;lock(cameraGate) { if(next.Generation!=cameraGeneration)continue;completedCameraScene=next;if(!cameraApplyPosted) { cameraApplyPosted=true;post=true; } }
                            if(post&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(new Action(()=> {
                                CameraScene completed;lock(cameraGate) { completed=completedCameraScene;completedCameraScene=null;cameraApplyPosted=false; }
                                if(completed==null||completed.Generation!=cameraGeneration||completed.ContentKey!=SceneContentKey())return;
                                CameraSceneBuilds++;
                                if(CameraInteracting) { cameraScene=completed;RequestRedraw(); }else idleCameraScene=completed;
                            }),DispatcherPriority.Render);
                        }catch(Exception error) { Program.LogException("CameraScene",error); }
                    }
                }finally { thread.Priority=priority; }
            });
        }
        void DrawCameraScene(DrawingContext dc,DrawingGroup fallback,double width,double height) { DrawCameraScene(dc,cameraScene,fallback,width,height); }
        void DrawCameraScene(DrawingContext dc,CameraScene scene,DrawingGroup fallback,double width,double height) {
            if(scene==null||scene.Width!=width||scene.Height!=height||scene.Opacity!=State.Opacity) { dc.DrawDrawing(fallback);return; }
            double side=MapSide(State,width,height,false),scale=side/scene.Side;var centre=CameraCentre(State,width,height,false);
            var bounds=new Rect(width/2+(scene.Centre.X-centre.X)*side-width*scale/2,height/2+(scene.Centre.Y-centre.Y)*side-height*scale/2,width*scale,height*scale);
            var viewport=new Rect(0,0,width,height);dc.PushClip(new RectangleGeometry(viewport));dc.DrawRectangle(tacticalHitSurface,null,viewport);
            // Fill newly exposed margins only, avoiding double-opacity over the frame.
            if(!bounds.Contains(viewport)&&BaseMap!=null) {
                dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(viewport),new RectangleGeometry(bounds)));dc.PushOpacity(State.Opacity/100d);
                dc.PushTransform(new MatrixTransform(side/1024,0,0,side/1024,width/2-centre.X*side,height/2-centre.Y*side));dc.DrawDrawing(BaseMap);dc.Pop();dc.Pop();dc.Pop();
            }
            dc.DrawImage(scene.Image,bounds);dc.Pop();
        }
        internal void RequestRedraw() {
            if(!CameraInteracting) { CancelCameraFrame();InvalidateVisual();return; }
            // Input can arrive much faster than a transparent window can present.
            // Present the latest state on the next composition frame instead of
            // waiting an additional 33 ms after every presented drag frame. Keep
            // a render-priority timer as a fallback when composition is suspended.
            if(cameraFramePending)return;
            cameraFramePending=true;CompositionTarget.Rendering+=CameraCompositionFrame;
            if(cameraFrame==null) { cameraFrame=new DispatcherTimer(DispatcherPriority.Render,Dispatcher) { Interval=TimeSpan.FromMilliseconds(33) };cameraFrame.Tick+=(s,e)=>PresentCameraFrame(); }
            cameraFrame.Start();
        }
        sealed class PoiStamp { internal Point At;internal double Radius,Scale;internal DrawingGroup Icon;internal Pen Outline; }
        sealed class PoiSceneTile { internal Rect Bounds;internal DrawingGroup Drawing; }
        static readonly System.Threading.SemaphoreSlim poiBuildSlots=new System.Threading.SemaphoreSlim(1);
        System.Threading.CancellationTokenSource poiBuild;
        WpfPoiCluster[] requestedPoiLayout,drawnPoiLayout;
        double requestedPoiSide,drawnPoiSide;
        PoiSceneTile[] poiSceneTiles;
        internal int PoiTilesDrawn;
        static DrawingGroup RecordPois(IEnumerable<PoiStamp> stamps,System.Threading.CancellationToken cancel) {
            var drawing=new DrawingGroup();int index=0;
            using(var context=drawing.Open())foreach(var stamp in stamps) {
                if((index++&127)==0)cancel.ThrowIfCancellationRequested();
                context.DrawEllipse(WpfFieldTheme.Ground,stamp.Outline,stamp.At,stamp.Radius,stamp.Radius);
                context.PushTransform(new MatrixTransform(stamp.Scale,0,0,stamp.Scale,stamp.At.X,stamp.At.Y));context.DrawDrawing(stamp.Icon);context.Pop();
            }
            drawing.Freeze();return drawing;
        }
        long poiGeneration;
        string labelLayoutKey;MapZone[] labelLayoutZones;ScumMapStore labelLayoutStore;BunkerFloor labelLayoutBunker;WpfMapLabel[] labelLayout;
        double labelLayoutSide,labelLayoutWidth,labelLayoutHeight;DrawingPoint labelLayoutCentre;
        internal int LabelLayoutBuilds;
        WpfMapLabel[] RetainedLabels(double width,double height) {
            var centre=CameraCentre(State,width,height,Compact);
            if(State.Bunker==null&&!Compact&&CameraInteracting&&labelLayout!=null&&labelLayoutSide>0&&labelLayoutWidth==width&&labelLayoutHeight==height&&labelLayoutZones==State.Saved&&labelLayoutStore==State.Store&&labelLayoutBunker==State.Bunker) {
                if(!State.Labels)return new WpfMapLabel[0];
                double side=MapSide(State,width,height,false),scale=side/labelLayoutSide;
                return labelLayout.Select(label=>new WpfMapLabel { Text=label.Text,Major=label.Major,At=new Point(width/2+(label.At.X-width/2)*scale+(labelLayoutCentre.X-centre.X)*side,height/2+(label.At.Y-height/2)*scale+(labelLayoutCentre.Y-centre.Y)*side) }).ToArray();
            }
            string key=string.Join("|",new object[]{width,height,Compact,MapSide(State,width,height,Compact),centre.X,centre.Y,Rotation(State,Compact),State.Labels,State.LabelSize,State.Zones,State.Waypoints,State.PoiClustering,State.Store==null?"":State.Store.GetFilterHashKey(),Localization.CurrentCode});
            if(labelLayout==null||labelLayoutKey!=key||labelLayoutZones!=State.Saved||labelLayoutStore!=State.Store||labelLayoutBunker!=State.Bunker) {
                labelLayout=SelectLabels(State,width,height,Compact);labelLayoutKey=key;labelLayoutZones=State.Saved;labelLayoutStore=State.Store;labelLayoutBunker=State.Bunker;labelLayoutSide=MapSide(State,width,height,Compact);labelLayoutCentre=centre;labelLayoutWidth=width;labelLayoutHeight=height;LabelLayoutBuilds++;
            }
            return labelLayout;
        }
        internal int PoiSceneBuilds;
        internal bool PoiSceneRasterized;
        static void PostPoiScene(Dispatcher dispatcher,Action apply) { if(!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(apply,DispatcherPriority.Background); }
        void DrawRetainedPois(DrawingContext dc,double side,double left,double top,double width,double height) {
            StreamPois(dc,side,left,top,width,height);
        }
        double renderRotation;
        internal static double Rotation(WpfMapState state,bool compact) { return compact&&state.RotateMap&&state.Position!=null?90-state.Position.Yaw:0; }
        internal static Point RotatePoint(Point p,double angle,double width,double height) {
            double radians=angle*Math.PI/180,c=Math.Cos(radians),s=Math.Sin(radians),x=p.X-width/2,y=p.Y-height/2;
            return new Point(width/2+x*c-y*s,height/2+x*s+y*c);
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScumMapStore,WpfPoiCache[]> poiCaches=new System.Runtime.CompilerServices.ConditionalWeakTable<ScumMapStore,WpfPoiCache[]>();
        sealed class PoiCellComparer:IEqualityComparer<Point> {
            public bool Equals(Point a,Point b) { return a.X==b.X&&a.Y==b.Y; }
            public int GetHashCode(Point point) { unchecked { return (int)point.X*73856093^(int)point.Y*19349663; } }
        }
        static readonly PoiCellComparer poiCellComparer=new PoiCellComparer();
        internal static double PoiRadius(double zoom,bool compact) { return Math.Min(compact?7.5:9, (compact?4.5:5.5)+Math.Log(Math.Max(1,zoom),2)*.8); }
        internal static WpfPoiCluster[] PoiClusters(WpfMapState state,double width,double height,bool compact) {
            if(state==null||state.Store==null||!state.Store.MasterEnabled||width<=0||height<=0)return new WpfPoiCluster[0];
            double side=MapSide(state,width,height,compact),spacing=Math.Max(compact?36:42,Math.Min(compact?64:80,Math.Min(width,height)/(compact?5:12)));
            var cache=poiCaches.GetValue(state.Store,store=>new[]{new WpfPoiCache(),new WpfPoiCache()})[compact?0:1];
            string filter=state.Store.GetFilterHashKey();
            if(!state.PoiClustering&&cache.Clusters!=null&&!cache.Clustering&&cache.Filter==filter&&cache.Count==state.Store.Markers.Count) {
                double radius=PoiRadius(state.Zoom,compact);
                if(cache.Clusters.Length>0&&cache.Clusters[0].Radius!=radius)foreach(var cluster in cache.Clusters)cluster.Radius=radius;
                cache.Zoom=state.Zoom;cache.Side=side;cache.Spacing=spacing;
                return cache.Clusters;
            }
            if(cache.Clusters!=null&&cache.Clustering==state.PoiClustering&&cache.Side==side&&cache.Spacing==spacing&&cache.Zoom==state.Zoom&&cache.Filter==filter&&cache.Count==state.Store.Markers.Count)return cache.Clusters;
            var bins=new Dictionary<Point,List<WpfPoiCluster>>(poiCellComparer);var clusters=new List<WpfPoiCluster>();
            if(cache.OrderedMarkers==null||cache.Filter!=filter||cache.Count!=state.Store.Markers.Count) {
                cache.Labels=null;
                var priorities=state.Store.CategoryById.ToDictionary(entry=>entry.Key,entry=> { string symbol=PoiSymbol(entry.Value);return symbol=="$"?0:symbol=="B"?1:symbol=="⌂"?2:3; });
                cache.OrderedMarkers=state.Store.Markers.Where(m=>state.Store.IsCategoryEnabled(m.CategoryId)).OrderBy(m=>priorities.ContainsKey(m.CategoryId)?priorities[m.CategoryId]:3).ThenBy(m=>m.X).ThenBy(m=>m.Y).ThenBy(m=>m.Id).ToArray();
            }
            foreach(var marker in cache.OrderedMarkers) {
                if(!state.PoiClustering) { var single=new WpfPoiCluster { Point=marker.Point };single.Members.Add(marker);clusters.Add(single);continue; }
                double x=marker.X*side,y=marker.Y*side;int bx=(int)Math.Floor(x/spacing),by=(int)Math.Floor(y/spacing);WpfPoiCluster nearest=null;double distance=spacing*spacing;
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++) {
                    List<WpfPoiCluster> candidates;if(!bins.TryGetValue(new Point(bx+dx,by+dy),out candidates))continue;
                    foreach(var candidate in candidates) { double cx=x-candidate.Point.X*side,cy=y-candidate.Point.Y*side,d=cx*cx+cy*cy;if(d<distance) { nearest=candidate;distance=d; } }
                }
                if(nearest==null) { nearest=new WpfPoiCluster { Point=marker.Point };clusters.Add(nearest);var cell=new Point(bx,by);List<WpfPoiCluster> bucket;if(!bins.TryGetValue(cell,out bucket))bins.Add(cell,bucket=new List<WpfPoiCluster>());bucket.Add(nearest); }
                nearest.Members.Add(marker);
            }
            foreach(var cluster in clusters) { cluster.Radius=cluster.Members.Count==1?PoiRadius(state.Zoom,compact):Math.Min(compact?10:13,(compact?7.5:9.5)+Math.Log10(cluster.Members.Count)*1.5);cluster.Mixed=cluster.Members.Any(member=>member.CategoryId!=cluster.Members[0].CategoryId); }
            cache.Clustering=state.PoiClustering;cache.Side=side;cache.Spacing=spacing;cache.Zoom=state.Zoom;cache.Filter=filter;cache.Count=state.Store.Markers.Count;cache.Clusters=clusters.ToArray();return cache.Clusters;
        }
        internal DrawingGroup BaseMap;
        internal WpfTerrainTiles Terrain;
        internal Action<DrawingPoint> PanChanged;
        internal Action ClearRouteRequested,StopNavigationRequested;
        internal Action<MapZone> DeleteWaypointRequested;
        internal Action<string> HoverChanged;
        internal void ClearHover() { ToolTip=null;hoverText=null;if(HoverChanged!=null)HoverChanged(null); }
        long lastHover;
        string hoverText;
        internal string HoverDetails(DrawingPoint at) {
            if(State==null)return null;
            var store=State.Store;var parts=new List<string>();double side=MapSide(State,ActualWidth,ActualHeight,Compact);
            foreach(var shared in State.SharedMarkers.Where(sharedItem=>MarkerDistance(new DrawingPoint((float)sharedItem.X,(float)sharedItem.Y),at)<=14/side))parts.Add(SharedMarkerLabel(shared));
            if(store==null||!store.MasterEnabled)return parts.Count==0?null:string.Join(Environment.NewLine,parts);
            double hit=18/side;var cluster=Index(PoiClusters(State,ActualWidth,ActualHeight,Compact)).Query(new Rect(at.X-hit,at.Y-hit,hit*2,hit*2)).Where(c=>MarkerDistance(c.Point,at)<=(c.Radius+3)/side).OrderBy(c=>MarkerDistance(c.Point,at)).FirstOrDefault();
            var marker=cluster==null?null:cluster.Members[0];
            if(cluster!=null&&cluster.Members.Count>1) { parts.Add(cluster.Members.Count.ToString(CultureInfo.CurrentCulture)+" × "+string.Join(", ",cluster.Members.Select(m=>Localization.GetMapObjectName(m.Title)).Distinct().Take(6))+(cluster.Members.Count>6?" …":""));marker=null; }
            if(marker!=null) { ScumMapCategory category;store.CategoryById.TryGetValue(marker.CategoryId,out category);parts.Add(Localization.GetMapObjectName(marker.Title));if(category!=null)parts.Add(Localization.GetCategoryName(category.Name)); }
            foreach(var habitat in store.Habitats.Where(h=>store.IsHabitatEnabled(h)&&h.Contains(at))) {
                var species=habitat.CategoryIds.Where(store.IsCategoryEnabled).Where(store.CategoryById.ContainsKey).Select(id=>Localization.GetCategoryName(store.CategoryById[id].Name)).Distinct().OrderBy(n=>n);
                parts.Add(Localization.GetMapObjectName(habitat.Name)+": "+string.Join(", ",species));
            }
            return parts.Count==0?null:string.Join(Environment.NewLine,parts.Distinct());
        }
        internal static void DrawPlayer(DrawingContext dc,Point p,double yaw,Brush ink,bool heading) {
            dc.PushTransform(new RotateTransform(yaw-90,p.X,p.Y));
            var arrow=new StreamGeometry();using(var context=arrow.Open()) { context.BeginFigure(new Point(p.X,p.Y-13),true,true);context.LineTo(new Point(p.X+9,p.Y+10),true,false);context.LineTo(new Point(p.X,p.Y+5),true,false);context.LineTo(new Point(p.X-9,p.Y+10),true,false); }
            if(heading)dc.DrawGeometry(ink,new Pen(WpfFieldTheme.Ground,2),arrow);else dc.DrawEllipse(ink,new Pen(WpfFieldTheme.Ground,2),p,7,7);dc.Pop();
        }
        internal const double RouteThickness=4;
        // Square minimap corner radius: sharp in the tactical theme, matching panel radii otherwise.
        internal static double HudCorner { get { var t=WpfFieldTheme.Current;return t.Chamfer>0?0:Math.Min(18,t.PanelRadius); } }
        internal static string PaletteSeed(int index) {
            if(index<0||index>=16)throw new ArgumentOutOfRangeException("index");
            // Pick evenly spaced existing identity hues. Old clients render the
            // same selected colour without a new protocol or extra map markers.
            uint target=(uint)(index*225);
            for(uint value=0;value<1000000;value++) {
                string seed=value.ToString("x8",CultureInfo.InvariantCulture);uint hash=2166136261;
                unchecked { foreach(char c in seed)hash=(hash^c)*16777619; }
                if(hash%3600==target)return seed;
            }
            throw new InvalidOperationException("Member palette unavailable");
        }
        internal static Brush MemberInk(string ownerId) {
            if(string.IsNullOrEmpty(ownerId))return WpfFieldTheme.MapAmber;
            // Relay rosters prefix the transport identity with the group ID.
            // Hash only the member so every viewer uses the same color.
            string member=ownerId.Substring(ownerId.LastIndexOf(':')+1);
            if(member.Length==32&&member.All(Uri.IsHexDigit))member=member.Substring(0,8).ToLowerInvariant();
            uint hash=2166136261;unchecked { foreach(char c in member)hash=(hash^c)*16777619; }
            double hue=(hash%3600)/600.0,chroma=.68,x=chroma*(1-Math.Abs(hue%2-1)),m=.30;
            double r=0,g=0,b=0;
            if(hue<1) { r=chroma;g=x; }else if(hue<2) { r=x;g=chroma; }else if(hue<3) { g=chroma;b=x; }else if(hue<4) { g=x;b=chroma; }else if(hue<5) { r=x;b=chroma; }else { r=chroma;b=x; }
            var ink=new SolidColorBrush(Color.FromRgb((byte)((r+m)*255),(byte)((g+m)*255),(byte)((b+m)*255)));ink.Freeze();return ink;
        }
        Brush OwnerInk(string ownerId) { return string.IsNullOrEmpty(ownerId)?(string.IsNullOrEmpty(State.LocalPeerId)?new SolidColorBrush(Color.FromArgb((byte)(State.RouteColor>>24),(byte)(State.RouteColor>>16),(byte)(State.RouteColor>>8),(byte)State.RouteColor)):MemberInk(State.LocalPeerId)):MemberInk(ownerId); }
        Brush RouteInk(string key) { return OwnerInk(string.IsNullOrEmpty(key)?null:key.Substring(0,key.LastIndexOf(':'))); }
        void DrawRoutePlan(DrawingContext dc,SquadRoutePlan plan,Brush ink,Func<DrawingPoint,Point> point) {
            for(int i=0;i<plan.Legs.Length;i++)DrawStreamedRoute(dc,plan.Legs[i],ink,plan.Verified[i],point);
        }
        static string SharedMarkerLabel(SharedMapMarker marker) { int kind;string name=SquadState.IsPing(marker)&&int.TryParse(marker.Id.Substring(8,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out kind)&&kind<3?Localization.Get(new[]{"PingDanger","PingLoot","PingPickup"}[kind]):marker.Death?Localization.Get("DeathName"):marker.Name;return (SquadState.IsStop(marker)?Localization.Get(SquadState.IsShared(marker)?"SquadRoute":"SoloRoute")+" · ":marker.Id==SharedMapMarker.GpsId?Localization.Get("SoloRoute")+" · ":"")+marker.Owner+" · "+(SquadState.IsStop(marker)?(SquadState.StopIndex(marker)+1)+". ":"")+name; }
        SharedMapMarker SharedMarkerAt(DrawingPoint at) { return State==null?null:State.SharedMarkers.Where(marker=>MarkerDistance(new DrawingPoint((float)marker.X,(float)marker.Y),at)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)).OrderBy(marker=>MarkerDistance(new DrawingPoint((float)marker.X,(float)marker.Y),at)).FirstOrDefault(); }
        MapZone SharedDestination(DrawingPoint at) { var marker=SharedMarkerAt(at);return marker==null?null:new MapZone { Name=SharedMarkerLabel(marker),Category=ZoneCategory.Custom,Layer="Shared markers",Points=new[]{new DrawingPoint((float)marker.X,(float)marker.Y)},Argb=System.Drawing.Color.Cyan.ToArgb() }; }
        void NavigateTo(DrawingPoint at) {
            var destination=SharedDestination(at);
            if(destination!=null&&NavigateShared!=null)NavigateShared(destination);
            else if(NavigateAt!=null)NavigateAt(at);
        }
        void UpdateHover(Point at) {
            if(CameraInteracting)return;
            long now=Environment.TickCount&int.MaxValue;if(now-lastHover<80&&now>=lastHover)return;lastHover=now;
            DrawingPoint point;string text=drag.HasValue||contextPointer.HasValue||openContext!=null||!TryMapPoint(at,out point)?null:HoverDetails(point);if(text==hoverText)return;hoverText=text;
            ToolTip=text==null?null:new ToolTip { Content=new TextBlock { Text=text,TextWrapping=TextWrapping.Wrap,MaxWidth=380 },Background=WpfFieldTheme.Panel,Foreground=WpfFieldTheme.Ink,BorderBrush=WpfFieldTheme.Line,Padding=new Thickness(12) };
            if(HoverChanged!=null)HoverChanged(text);
        }
        internal bool HitsDestination(DrawingPoint point) { if(State==null||State.Target==null)return false;return MarkerDistance(point,State.Target.Centroid)<=12/MapSide(State,ActualWidth,ActualHeight,Compact); }
        static double MarkerDistance(DrawingPoint a,DrawingPoint b) { return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)); }
        internal ContextMenu BuildContextMenu(DrawingPoint target) {
            var menu=new ContextMenu { Background=WpfFieldTheme.Panel,Foreground=WpfFieldTheme.Ink };
            menu.Items.Add(new MenuItem { Header=DestinationSearch.Sector(target),IsEnabled=false });
            var navigate=new MenuItem { Header=Localization.Get("MapGpsDestination"),ToolTip=Localization.Get("MapGpsSavedHelp") };navigate.Click+=(s,e)=>NavigateTo(target);menu.Items.Add(navigate);
            if(State!=null&&State.Bunker==null&&NavigateShared!=null) {
                var heights=NavigationRouter.BridgeHeights(target);
                for(int i=0;i<heights.Length;i++) {
                    double height=heights[i];string label=Localization.Get(i==0?"BridgeLower":"BridgeUpper");
                    var pick=new MenuItem { Header=label };pick.Click+=(s,e)=>NavigateShared(new MapZone {Name=label,Points=new[]{target},RouteHeightCm=height});menu.Items.Add(pick);
                }
            }
            var save=new MenuItem { Header=Localization.Get("MarkerAdd"),ToolTip=Localization.Get("MarkerHelp") };save.Click+=(s,e)=> { if(WaypointAt!=null)WaypointAt(target); };menu.Items.Add(save);
            if(RouteStopAt!=null&&State!=null) {
                var routeMenu=new MenuItem { Header=Localization.Get("RouteList") };
                var stop=new MenuItem { Header=Localization.Get("SquadAddStop"),IsEnabled=State.LocalRoute.Length<SquadState.MaximumStops,ToolTip=Localization.Get("MapGpsSavedHelp") };stop.Click+=(s,e)=> { var destination=SharedDestination(target);if(destination!=null&&RouteDestination!=null)RouteDestination(destination);else RouteStopAt(target); };menu.Items.Insert(1,stop);
                var clicked=State.SharedMarkers.Where(m=>SquadState.IsStop(m)&&MarkerDistance(new DrawingPoint((float)m.X,(float)m.Y),target)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)).FirstOrDefault();var chosen=clicked!=null?SquadState.Routes(State.SharedMarkers).FirstOrDefault(r=>SquadState.RouteKey(r[0])==SquadState.RouteKey(clicked)):State.LocalRoute;
                var start=new MenuItem { Header=Localization.Get("SquadStartRoute"),IsEnabled=chosen!=null&&chosen.Length>0 };start.Click+=(s,e)=> { if(RouteNavigate!=null)RouteNavigate(chosen); };routeMenu.Items.Add(start);
                if(clicked!=null&&clicked.OwnerId==null&&State.LocalRoute.Any(m=>m.Id==clicked.Id)) { var remove=new MenuItem { Header=Localization.Get("SquadDelete")+": "+clicked.Name };remove.Click+=(s,e)=> { if(RouteRemove!=null)RouteRemove(clicked.Id); };routeMenu.Items.Add(remove); }
                var ownMenu=new MenuItem { Header=Localization.Get("OwnRoute") };foreach(bool shared in new[]{false,true}) { bool kind=shared;var mode=new MenuItem { Header=Localization.Get(shared?"SquadRoute":"SoloRoute"),IsCheckable=true,IsChecked=State.LocalRoute.Length>0&&SquadState.IsShared(State.LocalRoute[0])==shared };mode.Click+=(s,e)=> { if(RouteKind!=null)RouteKind(kind); };ownMenu.Items.Add(mode); }routeMenu.Items.Add(ownMenu);
                var undo=new MenuItem { Header=Localization.Get("SquadUndoStop"),IsEnabled=State.LocalRoute.Length>0 };undo.Click+=(s,e)=> { if(RouteUndo!=null)RouteUndo(); };routeMenu.Items.Add(undo);
                var clearLocal=new MenuItem { Header=Localization.Get("WpfClearRoute"),IsEnabled=State.LocalRoute.Length>0 };clearLocal.Click+=(s,e)=> { if(RouteClear!=null)RouteClear(); };routeMenu.Items.Add(clearLocal);
                var end=new MenuItem { Header=Localization.Get("SquadStopNav"),IsEnabled=State.SquadNavigating||State.Target!=null };end.Click+=(s,e)=> { if(StopNavigationRequested!=null)StopNavigationRequested(); };routeMenu.Items.Add(end);
                var manage=new MenuItem { Header=Localization.Get("RouteList")+"…" };manage.Click+=(s,e)=> { if(RoutesRequested!=null)RoutesRequested(); };routeMenu.Items.Add(manage);menu.Items.Add(routeMenu);
            }
            if(NavigatePeer!=null&&State!=null)foreach(var peer in State.Peers.Where(p=>MarkerDistance(MapWindow.ToMap(p.Position),target)<=14/MapSide(State,ActualWidth,ActualHeight,Compact))) { string id=peer.Id;var follow=new MenuItem { Header=Localization.Get("SquadFollow")+": "+peer.Name };follow.Click+=(s,e)=>NavigatePeer(id);menu.Items.Add(follow); }
            if(PingAt!=null)for(int i=0;i<3;i++) { int kind=i;var ping=new MenuItem { Header=Localization.Get(new[]{"PingDanger","PingLoot","PingPickup"}[i]),IsEnabled=PingAvailable==null||PingAvailable(),ToolTip=Localization.Get("SquadPingHelp") };ping.Click+=(s,e)=>PingAt(kind,target);menu.Items.Add(ping); }
            menu.Items.Add(new Separator());var clear=new MenuItem { Header=Localization.Get("WpfClearRoute"),IsEnabled=State!=null&&(State.Target!=null||State.LocalRoute.Length>0) };clear.Click+=(s,e)=> { if(ClearRouteRequested!=null)ClearRouteRequested(); };menu.Items.Add(clear);
            var saved=State==null||State.Saved==null?null:State.Saved.Where(z=>z.Points!=null&&z.Points.Length==1&&MarkerDistance(target,z.Centroid)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)).OrderByDescending(z=>z.IsDeathMarker).ThenBy(z=>MarkerDistance(target,z.Centroid)).FirstOrDefault();
            if(saved!=null&&MarkerDistance(target,saved.Centroid)<=14/MapSide(State,ActualWidth,ActualHeight,Compact)) { var delete=new MenuItem { Header=Localization.T("DeleteWaypointNamed",saved.DisplayName) };delete.Click+=(s,e)=> { if(DeleteWaypointRequested!=null)DeleteWaypointRequested(saved); };menu.Items.Add(delete); }
            if(AdminMenuAt!=null) { var admin=AdminMenuAt(target);if(admin!=null) { menu.Items.Add(new Separator());menu.Items.Add(admin); } }
            return menu;
        }
        internal Func<DrawingPoint,MenuItem> AdminMenuAt;
        internal Action<int> ZoomRequested;
        internal Action<DrawingPoint> NavigateAt,WaypointAt,RouteStopAt;
        internal Action<int,DrawingPoint> PingAt;
        internal Action<SharedMapMarker[]> RouteNavigate;
        internal Action<string> RouteRemove,NavigatePeer;
        internal Action<bool> RouteKind;
        internal Action RouteUndo,RouteClear,RoutesRequested;
        internal Func<bool> PingAvailable;
        internal Action<MapZone> NavigateShared,RouteDestination;
        internal Canvas ContextHost;
        Border openContext;
        Point contextAnchor;
        internal bool ContextMenuOpen { get { return openContext!=null; } }
        Point? contextPointer;
        internal bool BeginContextPointer(Point point) {
            DrawingPoint target;if(!TryMapPoint(point,out target))return false;
            CancelPointer();ClearHover();contextPointer=point;CaptureMouse();return true;
        }
        internal bool EndContextPointer(Point point) {
            if(!contextPointer.HasValue)return false;
            var origin=contextPointer.Value;contextPointer=null;
            if(IsMouseCaptured)ReleaseMouseCapture();
            return OpenContextMenu(origin);
        }
        internal bool OpenContextMenu(Point point) {
            DrawingPoint target;if(ContextHost==null||!TryMapPoint(point,out target))return false;
            CloseContextMenu();ClearHover();contextAnchor=point;
            // Keep every level in the map's existing HWND. Native Popup/MenuItem
            // windows need activation/capture that conflicts with SCUM's focus.
            var actions=BuildContextMenu(target);
            var scroll=new ScrollViewer { Content=ContextItems(actions.Items),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=Math.Max(80,ActualHeight-24) };
            openContext=WpfFieldTheme.Card(scroll,new Thickness(6));openContext.Width=Math.Min(350,Math.Max(180,ActualWidth-16));
            openContext.SizeChanged+=(s,e)=>PositionContextMenu();ContextHost.Children.Add(openContext);PositionContextMenu();return true;
        }
        StackPanel ContextItems(ItemCollection items) {
            var panel=new StackPanel();
            foreach(var value in items) {
                var item=value as MenuItem;
                if(item==null) { panel.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(4,5,4,5) });continue; }
                string title=(item.IsChecked?"✓ ":"")+Convert.ToString(item.Header,CultureInfo.CurrentCulture);
                if(item.HasItems) {
                    var children=ContextItems(item.Items);children.Margin=new Thickness(12,0,0,0);children.Visibility=Visibility.Collapsed;
                    Button expand=null;expand=WpfFieldTheme.Button("▸ "+title,()=> { bool show=children.Visibility!=Visibility.Visible;children.Visibility=show?Visibility.Visible:Visibility.Collapsed;expand.Content=(show?"▾ ":"▸ ")+title; });
                    expand.IsEnabled=item.IsEnabled;expand.HorizontalContentAlignment=HorizontalAlignment.Left;expand.Margin=new Thickness(0,1,0,1);expand.MinHeight=32;panel.Children.Add(expand);panel.Children.Add(children);
                } else {
                    var captured=item;var action=WpfFieldTheme.Button(title,()=> { CloseContextMenu();captured.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent,captured)); });
                    action.ToolTip=item.ToolTip;action.IsEnabled=item.IsEnabled;action.HorizontalContentAlignment=HorizontalAlignment.Left;action.Margin=new Thickness(0,1,0,1);action.MinHeight=32;action.Padding=new Thickness(10,6,10,6);
                    action.Content=new TextBlock { Text=title,TextWrapping=TextWrapping.Wrap };panel.Children.Add(action);
                }
            }
            return panel;
        }
        internal static void AddAdminContextItems(ItemCollection items,MapWindow.AdminMapAction[] actions) {
            foreach(var action in actions) {
                if(action.Name==null) { items.Add(new Separator());continue; }
                var item=new MenuItem { Header=action.Name,ToolTip=action.Help,Tag=action.Command,IsEnabled=action.Enabled,IsChecked=action.Checked };
                if(action.Children!=null)AddAdminContextItems(item.Items,action.Children);
                else { var captured=action;item.Click+=(s,e)=> { e.Handled=true;if(captured.Invoke!=null)captured.Invoke(); }; }
                items.Add(item);
            }
        }
        void PositionContextMenu() {
            if(openContext==null)return;
            openContext.Measure(new Size(openContext.Width,Math.Max(80,ActualHeight-16)));
            Canvas.SetLeft(openContext,Math.Max(8,Math.Min(contextAnchor.X,ActualWidth-openContext.DesiredSize.Width-8)));
            Canvas.SetTop(openContext,Math.Max(8,Math.Min(contextAnchor.Y,ActualHeight-openContext.DesiredSize.Height-8)));
        }
        internal bool DismissContextOutside(Point point) {
            if(openContext==null)return false;
            var bounds=new Rect(Canvas.GetLeft(openContext),Canvas.GetTop(openContext),openContext.ActualWidth,openContext.ActualHeight);
            if(bounds.Contains(point))return false;CloseContextMenu();return true;
        }
        internal void CloseContextMenu() {
            contextPointer=null;if(IsMouseCaptured)ReleaseMouseCapture();
            var menu=openContext;if(menu==null)return;openContext=null;if(ContextHost!=null)ContextHost.Children.Remove(menu);
        }
        Point? drag;
        DrawingPoint pan;
        bool dragging;
        internal bool PointerCameraActive { get { return drag.HasValue&&dragging; } }
        internal float MarkerHitRadius() { return (float)((Compact?12:14)/Math.Max(1,State==null?1:MapSide(State,ActualWidth,ActualHeight,Compact))); }
        internal static double MapSide(WpfMapState state,double width,double height,bool compact) {
            if(!compact&&state.Bunker!=null)return .92*Math.Min(width/((state.Bunker.MaxX-state.Bunker.MinX)/1521618.0),height/((state.Bunker.MaxY-state.Bunker.MinY)/1523618.0))*Math.Max(1,state.Zoom);
            return (compact?Math.Max(width,height):Math.Min(width,height))*Math.Max(1,state.Zoom)*state.BunkerScale;
        }
        internal static DrawingPoint CameraCentre(WpfMapState state,double width,double height,bool compact) {
            var centre=compact&&state.Position!=null?MapWindow.ToMap(state.Position):state.Pan;
            // Indoor maps near the island edge (D4) must keep their own camera centre.
            // Clamping to the world-map viewport shifts the player away from the zoom centre.
            if(state.Bunker!=null)return centre;
            if(compact&&state.RotateMap&&state.Position!=null)return centre;
            double side=MapSide(state,width,height,compact);
            if(side<=0)return new DrawingPoint(.5f,.5f);
            double halfX=Math.Min(.5,width/(2*side)),halfY=Math.Min(.5,height/(2*side));
            return new DrawingPoint((float)Math.Max(halfX,Math.Min(1-halfX,centre.X)),(float)Math.Max(halfY,Math.Min(1-halfY,centre.Y)));
        }
        internal bool TryMapPoint(Point p,out DrawingPoint target) {
            target=new DrawingPoint();
            if(Compact||State==null||ActualWidth<=0||ActualHeight<=0||p.X<0||p.Y<0||p.X>ActualWidth||p.Y>ActualHeight)return false;
            double side=MapSide(State,ActualWidth,ActualHeight,Compact);if(side<=0)return false;
            target=new DrawingPoint((float)(CameraCentre(State,ActualWidth,ActualHeight,Compact).X+(p.X-ActualWidth/2)/side),(float)(CameraCentre(State,ActualWidth,ActualHeight,Compact).Y+(p.Y-ActualHeight/2)/side));
            return target.X>=0&&target.X<=1&&target.Y>=0&&target.Y<=1;
        }
        internal void BeginPointer(Point p) { if(Compact||State==null)return;ClearHover();drag=p;pan=CameraCentre(State,ActualWidth,ActualHeight,Compact);dragging=false; }
        internal void MovePointer(Point p) {
            if(!drag.HasValue||State==null)return;
            double dx=p.X-drag.Value.X,dy=p.Y-drag.Value.Y;
            if(!dragging&&Math.Abs(dx)<=SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(dy)<=SystemParameters.MinimumVerticalDragDistance)return;
            dragging=true;double side=MapSide(State,ActualWidth,ActualHeight,Compact);if(side<=0)return;
            if(PanChanged!=null)PanChanged(new DrawingPoint((float)Math.Max(0,Math.Min(1,pan.X-dx/side)),(float)Math.Max(0,Math.Min(1,pan.Y-dy/side))));
        }
        internal void EndPointer(Point p) {
            if(!drag.HasValue)return;MovePointer(p);bool click=!dragging;CancelPointer();DrawingPoint target;
            if(click&&TryMapPoint(p,out target)) { if(HitsDestination(target)&&ClearRouteRequested!=null)ClearRouteRequested();else NavigateTo(target); }
        }
        internal void CancelPointer() { drag=null;dragging=false; }
        internal WpfMapSurface() {
            mapLayers=new VisualCollection(this) { terrainLayer,trailLayer,annotationLayer,sharedLayer,liveLayer };
            FlowDirection=FlowDirection.LeftToRight;ClipToBounds=true;Focusable=true;
            PreviewMouseRightButtonDown+=(sender,args)=> { if(BeginContextPointer(args.GetPosition(this)))args.Handled=true; };
            PreviewMouseRightButtonUp+=(sender,args)=> { if(EndContextPointer(args.GetPosition(this)))args.Handled=true; };
            MouseWheel+=(s,e)=> { if(ZoomRequested!=null)ZoomRequested(e.Delta>0?1:-1);e.Handled=true; };
            MouseLeftButtonDown+=(s,e)=> { if(Compact||State==null)return;BeginPointer(e.GetPosition(this));CaptureMouse();e.Handled=true; };
            MouseMove+=(s,e)=> { if(drag.HasValue&&e.LeftButton!=MouseButtonState.Pressed)CancelPointer();else MovePointer(e.GetPosition(this));UpdateHover(e.GetPosition(this)); };
            MouseLeave+=(s,e)=> { ToolTip=null;hoverText=null; };
            ToolTipService.SetInitialShowDelay(this,250);ToolTipService.SetShowDuration(this,30000);
            MouseLeftButtonUp+=(s,e)=> { if(!drag.HasValue)return;EndPointer(e.GetPosition(this));ReleaseMouseCapture();e.Handled=true; };
            LostMouseCapture+=(s,e)=> { CancelPointer();contextPointer=null; };
            IsVisibleChanged+=(s,e)=> { if(!IsVisible) { CloseContextMenu();CancelPointer();EndCameraFrames();ResetCameraScenes(); } };
            Unloaded+=(s,e)=> { EndCameraFrames();ResetPoiStream();ClearTacticalLayers(); };
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);
            UpdateTacticalLayers();
        }
        void DrawMap(DrawingContext dc) {
            DrawMapPass(dc,MapPass.All);
        }
        void DrawMapPass(DrawingContext dc,MapPass pass,bool overviewOnly=false) {
            base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<1||h<1)return;
            if(Compact&&State!=null&&State.Shape=="Circle")dc.PushClip(new EllipseGeometry(new Point(w/2,h/2),Math.Min(w,h)/2,Math.Min(w,h)/2));else { double corner=Compact?HudCorner:0;dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h),corner,corner)); }
            if(pass==MapPass.All||pass==MapPass.Terrain)dc.DrawRectangle(Compact?Brushes.Transparent:tacticalHitSurface,null,new Rect(0,0,w,h));
            if(State==null) { dc.Pop();return; }
            var routeInk=new SolidColorBrush(Color.FromArgb((byte)(State.RouteColor>>24),(byte)(State.RouteColor>>16),(byte)(State.RouteColor>>8),(byte)State.RouteColor));
            var playerInk=new SolidColorBrush(Color.FromArgb((byte)(State.PlayerColor>>24),(byte)(State.PlayerColor>>16),(byte)(State.PlayerColor>>8),(byte)State.PlayerColor));
            double side=MapSide(State,w,h,Compact);DrawingPoint centre=CameraCentre(State,w,h,Compact);
            double left=w/2-side*centre.X,top=h/2-side*centre.Y;
            double rotation=renderRotation=Rotation(State,Compact);
            double overscan=rotation==0?0:Math.Max(w,h);
            var layerViewport=new Rect(-overscan,-overscan,w+2*overscan,h+2*overscan);
            dc.PushTransform(new RotateTransform(rotation,w/2,h/2));
            var visibleWorld=new Rect(left,top,side,side);visibleWorld.Intersect(layerViewport);dc.PushClip(new RectangleGeometry(visibleWorld));
            Func<DrawingPoint,Point> point=p=>new Point(left+p.X*side,top+p.Y*side);
            if(pass==MapPass.All||pass==MapPass.Terrain) {
            if(State.Bunker!=null&&State.BunkerImage!=null) {
                var b=State.Bunker;dc.DrawRectangle(WpfFieldTheme.Ground,null,layerViewport);
                dc.DrawImage(State.BunkerImage,new Rect(left+(617718-b.MaxX)/1521618.0*side,top+(618618-b.MaxY)/1523618.0*side,(b.MaxX-b.MinX)/1521618.0*side,(b.MaxY-b.MinY)/1523618.0*side));
            } else if(State.Opacity>0) {
                dc.PushOpacity(State.Opacity/100.0);
                if(Terrain!=null&&!overviewOnly)Terrain.DrawRotated(dc,w,h,left,top,side,VisualTreeHelper.GetDpi(this).DpiScaleX,rotation==0?0:Math.Max(w,h));
                else { dc.PushTransform(new TranslateTransform(left,top));dc.PushTransform(new ScaleTransform(side/1024,side/1024));if(BaseMap!=null)dc.DrawDrawing(BaseMap);dc.Pop();dc.Pop(); }
                dc.Pop();
            }
            if(State.GridOpacity>0&&(State.Grid||State.GridLabels)) {
                dc.PushOpacity(State.GridOpacity/100.0);
                if(State.Grid)for(int i=1;i<5;i++) {
                    var pen=new Pen(WpfFieldTheme.Ink,1);double x=left+i*side/5,y=top+i*side/5;
                    if(x>=layerViewport.Left-1&&x<=layerViewport.Right+1)dc.DrawLine(pen,new Point(x,Math.Max(top,layerViewport.Top-1)),new Point(x,Math.Min(top+side,layerViewport.Bottom+1)));
                    if(y>=layerViewport.Top-1&&y<=layerViewport.Bottom+1)dc.DrawLine(pen,new Point(Math.Max(left,layerViewport.Left-1),y),new Point(Math.Min(left+side,layerViewport.Right+1),y));
                }
                if(State.GridLabels)for(int row=0;row<5;row++)for(int col=0;col<5;col++)Label(dc,""+"DCBAZ"[row]+(4-col),new Point(left+col*side/5+8,top+row*side/5+8),WpfFieldTheme.Ink,Math.Max(9,State.LabelSize));
                dc.Pop();
            }
            if(State.Store!=null&&State.Store.MasterEnabled)foreach(var habitat in State.Store.Habitats.AsEnumerable().Reverse().Where(State.Store.IsHabitatEnabled)) {
                if(habitat.Rings==null)continue;var geometry=habitatGeometryCache.GetValue(habitat.Rings,WorldGeometry);var bounds=geometryBoundsCache.GetValue(geometry,g=>new GeometryBounds(g)).Value;bounds.Transform(new Matrix(side,0,0,side,left,top));if(!bounds.IntersectsWith(layerViewport))continue;
                var color=Color.FromRgb((byte)(habitat.Color>>16),(byte)(habitat.Color>>8),(byte)habitat.Color);
                dc.PushTransform(new MatrixTransform(side,0,0,side,left,top));dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(12,color.R,color.G,color.B)),new Pen(new SolidColorBrush(Color.FromArgb(100,color.R,color.G,color.B)),.8/side),geometry);dc.Pop();
            }
            }
            if((pass==MapPass.All||pass==MapPass.Trail)&&State.History)foreach(var segment in State.TrailSegments??new[]{State.Trail??new DrawingPoint[0]})if(segment.Length>1)DrawLine(dc,segment.Select(point),new Pen(new SolidColorBrush(Color.FromArgb((byte)(State.HistoryColor>>24),(byte)(State.HistoryColor>>16),(byte)(State.HistoryColor>>8),(byte)State.HistoryColor)),2));
            if(pass==MapPass.All||pass==MapPass.Annotations) {
            if(State.Bunker==null&&State.Zones)foreach(var zone in State.Saved.Where(z=>z.Points!=null&&z.Points.Length>2)) {
                var outline=Color.FromArgb((byte)(zone.Argb>>24),(byte)(zone.Argb>>16),(byte)(zone.Argb>>8),(byte)zone.Argb);int fillArgb=zone.FillArgb??(((zone.IsFaction||zone.Category==ZoneCategory.Faction?75:40)<<24)|(zone.Argb&0xFFFFFF));
                var geometry=zoneGeometryCache.GetValue(zone.Points,points=>WorldGeometry(new[]{points}));var bounds=geometryBoundsCache.GetValue(geometry,g=>new GeometryBounds(g)).Value;bounds.Transform(new Matrix(side,0,0,side,left,top));if(!bounds.IntersectsWith(layerViewport))continue;
                dc.PushTransform(new MatrixTransform(side,0,0,side,left,top));dc.DrawGeometry(new SolidColorBrush(Color.FromArgb((byte)(fillArgb>>24),(byte)(fillArgb>>16),(byte)(fillArgb>>8),(byte)fillArgb)),new Pen(new SolidColorBrush(outline),1.8/side),geometry);dc.Pop();
            }
            if(State.Zones)foreach(var zone in State.Saved.Where(z=>MapWindow.WpfCustomZone(z)&&z.Points.Length<3)) {
                var at=point(zone.Points[0]);if(!VisibleMarker(at,w,h,rotation))continue;var color=Color.FromArgb((byte)(zone.Argb>>24),(byte)(zone.Argb>>16),(byte)(zone.Argb>>8),(byte)zone.Argb);dc.DrawEllipse(new SolidColorBrush(color),new Pen(WpfFieldTheme.Ground,1.5),at,6,6);
            }
            if(State.Waypoints)foreach(var zone in State.Saved.Where(z=>z.Points!=null&&z.Points.Length==1&&!MapWindow.WpfCustomZone(z)&&!z.IsDeathMarker)) {
                var at=point(zone.Centroid);if(!VisibleMarker(at,w,h,rotation))continue;
                double radius=Compact?11:13;dc.PushTransform(new RotateTransform(-rotation,at.X,at.Y));
                dc.DrawImage(WpfMarkerIcons.Image(zone.Icon),new Rect(at.X-radius,at.Y-radius,radius*2,radius*2));dc.Pop();
            }
            if(State.Store!=null&&State.Store.MasterEnabled) {
                if(!Compact)DrawRetainedPois(dc,side,left,top,w,h);
                else foreach(var cluster in VisiblePoiClusters(State,w,h,Compact)) {
                    var at=point(cluster.Point);var screen=RotatePoint(at,rotation,w,h);
                    if(screen.X<-14||screen.Y<-14||screen.X>w+14||screen.Y>h+14)continue;
                    ScumMapCategory category;State.Store.CategoryById.TryGetValue(cluster.Members[0].CategoryId,out category);DrawPoi(dc,at,category,cluster);
                }

            }
            if(State.Labels&&State.Bunker!=null)foreach(var entrance in State.Bunker.Labels.Where(l=>l.Kind=="entrance"||l.Kind=="connection")) {
                var at=point(MapWindow.ToMap(new Position {X=entrance.X,Y=entrance.Y}));if(!VisibleMarker(at,w,h,rotation))continue;
                dc.DrawEllipse(WpfFieldTheme.Ground,new Pen(RoomColour(entrance.Colour),2),at,6,6);
            }
            foreach(var label in RetainedLabels(w,h)) {
                var at=RotatePoint(label.At,-rotation,w,h);
                if(label.Source!=null&&(label.Source.Kind=="entrance"||label.Source.Kind=="connection")) {
                    var anchor=point(MapWindow.ToMap(new Position {X=label.Source.X,Y=label.Source.Y}));
                    dc.DrawLine(new Pen(label.Ink,1),anchor,at);
                }
                Label(dc,label.Text,at,label.Ink??(label.Major?WpfFieldTheme.Ink:WpfFieldTheme.Muted),label.FontSize>0?label.FontSize:Math.Max(6,State.LabelSize)+(label.Major?2:0),label.Background,State.Bunker==null && label.Background==null);
            }
            }
            if(pass==MapPass.All||pass==MapPass.Live) {
            routeInk=(SolidColorBrush)RouteInk(State.ActiveSquadRoute);
            if(State.Route!=null&&State.Route.Success&&State.Route.BridgeLayers==null&&State.Position!=null) {
                var path=State.Route.Polyline;
                if(path!=null&&path.Length>0)DrawLine(dc,new[]{point(MapWindow.ToMap(State.Position)),point(path[0])},new Pen(routeInk,RouteThickness));
                else if(State.Target!=null)DrawLine(dc,new[]{point(MapWindow.ToMap(State.Position)),point(State.Target.Centroid)},new Pen(routeInk,RouteThickness));
            }
            if(State.Target!=null) { var p=point(State.Target.Centroid);dc.DrawEllipse(null,new Pen(routeInk,3),p,9,9);if(!Compact)Label(dc,State.Target.DisplayName,p+new Vector(12,-30),routeInk,13); }
            if(State.Position!=null&&(State.Bunker==null||State.BunkerCurrentView==null||State.Bunker.Id==State.BunkerCurrentView)) {
                DrawPlayer(dc,point(MapWindow.ToMap(State.Position)),State.Position.Yaw,string.IsNullOrEmpty(State.LocalPeerId)?playerInk:MemberInk(State.LocalPeerId),State.Heading);
            }
            foreach(var peer in State.Peers) {
                if(peer.Position==null)continue;
                var p=point(MapWindow.ToMap(peer.Position));if(!VisibleMarker(p,w,h,rotation))continue;var ink=MemberInk(peer.Id);
                // A teammate whose location is not being refreshed stays at the last known spot, faded.
                bool heldPeer=!PrivatePositions.Live(peer.Updated,DateTime.UtcNow);if(heldPeer)dc.PushOpacity(.5);
                DrawPlayer(dc,p,peer.Position.Yaw,ink,State.Heading);
                Label(dc,peer.Name,p+new Vector(11,-18),ink,12);if(heldPeer)dc.Pop();
            }
            }
            if(pass==MapPass.All||pass==MapPass.Shared) {
            RouteVerticesDrawn=0;
            if(State.Bunker==null) {
            if(State.Route!=null&&State.Route.Success&&State.Position!=null&&State.Route.Polyline!=null&&State.Route.Polyline.Length>0) {
                var path=State.Route.Polyline;var ink=RouteInk(State.ActiveSquadRoute);DrawStreamedRoute(dc,path,ink,true,point);
                if(State.Target!=null&&State.Route.BridgeLayers==null)DrawLine(dc,new[]{point(path[path.Length-1]),point(State.Target.Centroid)},new Pen(ink,RouteThickness));
            }
            string ownKey=State.LocalRoute.Length>0?SquadState.RouteKey(State.LocalRoute[0]):null;
            if(State.OwnRoutePlan!=null&&ownKey!=State.ActiveSquadRoute)DrawRoutePlan(dc,State.OwnRoutePlan,OwnerInk(null),point);
            if(State.SquadPlan!=null)DrawRoutePlan(dc,State.SquadPlan,RouteInk(State.ActiveSquadRoute),point);
            foreach(var plan in State.OtherRoutePlans.Where(p=>p.Key!=State.ActiveSquadRoute))DrawRoutePlan(dc,plan.Plan,RouteInk(plan.Key),point);
            foreach(var route in SquadState.Routes(State.SharedMarkers)) {
                string key=SquadState.RouteKey(route[0]);bool ready=key==ownKey&&State.OwnRoutePlan!=null||key==State.ActiveSquadRoute&&State.SquadPlan!=null||State.OtherRoutePlans.Any(p=>p.Key==key);
                if(!ready)for(int i=1;i<route.Length;i++) { var pen=new Pen(OwnerInk(route[0].OwnerId),RouteThickness) { DashStyle=DashStyles.Dot };dc.DrawLine(pen,point(new DrawingPoint((float)route[i-1].X,(float)route[i-1].Y)),point(new DrawingPoint((float)route[i].X,(float)route[i].Y))); }
            }
            }
            foreach(var marker in State.SharedMarkers) {
                var at=point(new DrawingPoint((float)marker.X,(float)marker.Y));if(!VisibleMarker(at,w,h,rotation))continue;var ink=marker.Death?WpfFieldTheme.ColorBrush("#FF654F"):OwnerInk(marker.OwnerId);
                if(SquadState.IsPing(marker))ink=marker.Id.Substring(8,2)=="00"?WpfFieldTheme.ColorBrush("#FF654F"):marker.Id.Substring(8,2)=="01"?WpfFieldTheme.MapAmber:WpfFieldTheme.Cyan;
                if(marker.Death) { dc.DrawRoundedRectangle(WpfFieldTheme.Ground,new Pen(ink,2),new Rect(at.X-7,at.Y-10,14,19),3,3);dc.DrawLine(new Pen(ink,2),at+new Vector(0,-5),at+new Vector(0,4));dc.DrawLine(new Pen(ink,2),at+new Vector(-4,-1),at+new Vector(4,-1)); }
                else { var diamond=new StreamGeometry();using(var shape=diamond.Open()) { shape.BeginFigure(at+new Vector(0,-8),true,true);shape.LineTo(at+new Vector(7,0),true,false);shape.LineTo(at+new Vector(0,8),true,false);shape.LineTo(at+new Vector(-7,0),true,false); }dc.DrawGeometry(WpfFieldTheme.Ground,new Pen(ink,2),diamond); }
                // Route names belong on the tactical map; the HUD keeps their symbols and guidance.
                if(!Compact||!SquadState.IsStop(marker)&&marker.Id!=SharedMapMarker.GpsId)Label(dc,SharedMarkerLabel(marker),at+new Vector(11,-19),ink,12);
            }
            foreach(var death in State.Saved.Where(z=>z.IsDeathMarker&&z.Points!=null&&z.Points.Length==1)) {
                var p=point(death.Centroid);if(!VisibleMarker(p,w,h,rotation))continue;var ink=WpfFieldTheme.ColorBrush("#FF654F");dc.DrawRoundedRectangle(WpfFieldTheme.Ground,new Pen(ink,2),new Rect(p.X-8,p.Y-11,16,20),4,4);
                dc.DrawLine(new Pen(ink,2),new Point(p.X,p.Y-6),new Point(p.X,p.Y+4));dc.DrawLine(new Pen(ink,2),new Point(p.X-4,p.Y-2),new Point(p.X+4,p.Y-2));
                Label(dc,death.DisplayName,p+new Vector(12,-21),ink,Compact?11:13);
            }
            }
            if((pass==MapPass.All||pass==MapPass.Live)&&Compact&&State.Compass)Label(dc,"N",new Point(w/2-7,h/2-Math.Min(w,h)/2+10),WpfFieldTheme.Ink,11);
            dc.Pop(); // Map bounds: edge guidance belongs to the viewport, including transparent margins.
            dc.Pop(); // Map rotation.
            if(pass==MapPass.All||pass==MapPass.Live) {
                DrawDestinationEdge(dc,w,h,p=>RotatePoint(point(p),rotation,w,h),routeInk);
                DrawPeerEdges(dc,w,h,p=>RotatePoint(point(p),rotation,w,h));
                if(State.Bunker!=null) {
                    dc.DrawRectangle(WpfFieldTheme.Ground,null,new Rect(0,h-(Compact?24:28),w,Compact?24:28));
                    Label(dc,Compact?(State.Bunker.DisplayName??Localization.T("BunkerHeightLayer",State.Bunker.Rank,State.Bunker.Count)):State.BunkerCaption,new Point(8,h-(Compact?22:26)),WpfFieldTheme.Ink,Compact?11:13);
                }
            }
            dc.Pop();
        }
        void DrawDestinationEdge(DrawingContext dc,double width,double height,Func<DrawingPoint,Point> screen,Brush ink) {
            if(State.Target==null||State.Position==null)return;
            DrawEdgeArrow(dc,width,height,screen(State.Target.Centroid),ink,null);
        }
        void DrawPeerEdges(DrawingContext dc,double width,double height,Func<DrawingPoint,Point> screen) {
            if(!Compact)return;
            foreach(var peer in State.Peers)if(peer.Position!=null) { bool heldPeer=!PrivatePositions.Live(peer.Updated,DateTime.UtcNow);if(heldPeer)dc.PushOpacity(.5);DrawEdgeArrow(dc,width,height,screen(MapWindow.ToMap(peer.Position)),MemberInk(peer.Id),peer.Name);if(heldPeer)dc.Pop(); }
            foreach(var marker in State.SharedMarkers.Where(shared=>shared.Id==SharedMapMarker.GpsId))DrawEdgeArrow(dc,width,height,screen(new DrawingPoint((float)marker.X,(float)marker.Y)),OwnerInk(marker.OwnerId),null);
        }
        void DrawEdgeArrow(DrawingContext dc,double width,double height,Point destination,Brush ink,string name) {
            bool circle=Compact&&State.Shape=="Circle";
            double radius=Math.Min(width,height)/2;
            bool visible=circle?(destination-new Point(width/2,height/2)).Length<=radius:destination.X>=0&&destination.X<=width&&destination.Y>=0&&destination.Y<=height;
            if(visible)return;
            var bounds=circle?new System.Drawing.RectangleF((float)(width/2-radius),(float)(height/2-radius),(float)(radius*2),(float)(radius*2)):new System.Drawing.RectangleF(0,0,(float)width,(float)height);
            DrawingPoint tip,direction;
            if(!MapWindow.TryDestinationEdge(bounds,circle,new DrawingPoint((float)(width/2),(float)(height/2)),new DrawingPoint((float)destination.X,(float)destination.Y),out tip,out direction))return;
            var rear=new Point(tip.X-direction.X*13,tip.Y-direction.Y*13);var arrow=new StreamGeometry();
            using(var context=arrow.Open()) { context.BeginFigure(new Point(tip.X,tip.Y),true,true);context.LineTo(rear+new Vector(-direction.Y*6,direction.X*6),true,false);context.LineTo(rear+new Vector(direction.Y*6,-direction.X*6),true,false); }
            dc.DrawGeometry(ink,new Pen(WpfFieldTheme.Ground,2),arrow);
            if(name!=null) {
                var text=new FormattedText(name,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Barlow Semi Condensed, Segoe UI"),12,ink,1.0) { MaxTextWidth=Math.Max(1,Math.Min(180,width-40)),MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis };
                double x=direction.X>.3?tip.X-22-text.Width:direction.X<-.3?tip.X+22:tip.X-text.Width/2;
                double y=direction.Y>.3?tip.Y-22-text.Height:direction.Y<-.3?tip.Y+22:tip.Y-text.Height/2;
                var at=new Point(Math.Max(10,Math.Min(width-text.Width-10,x)),Math.Max(10,Math.Min(height-text.Height-10,y)));
                dc.DrawRoundedRectangle(WpfFieldTheme.Ground,null,new Rect(at.X-4,at.Y-1,text.Width+8,text.Height+2),3,3);dc.DrawText(text,at);
            }
        }
        internal static WpfMapLabel[] SelectLabels(WpfMapState state,double width,double height,bool compact) {
            if(state==null||!state.Labels||width<=0||height<=0)return new WpfMapLabel[0];
            double side=MapSide(state,width,height,compact);DrawingPoint centre=CameraCentre(state,width,height,compact);
            Func<DrawingPoint,Point> screen=p=>RotatePoint(new Point(width/2+(p.X-centre.X)*side,height/2+(p.Y-centre.Y)*side),Rotation(state,compact),width,height);
            var candidates=new List<WpfMapLabel>();
            if(state.Bunker!=null)foreach(var label in state.Bunker.Labels) {
                var tint=label.Colour==null?WpfFieldTheme.Ground:RoomColour(label.Colour);
                candidates.Add(new WpfMapLabel {Text=label.Text,At=screen(MapWindow.ToMap(new Position {X=label.X,Y=label.Y})),Major=true,Bounded=label.Bounded,SafeWidth=(label.MaxX-label.MinX)/1521618.0*side,SafeHeight=(label.MaxY-label.MinY)/1523618.0*side,Angle=Rotation(state,compact),Ink=label.Bounded?Brushes.Black:label.Kind=="entrance"||label.Kind=="connection"?tint:null,Background=label.Bounded?(label.Kind=="sector"?tint:Brushes.Transparent):null,Source=label});
            }
            foreach(var zone in state.Saved) {
                if(zone.IsDeathMarker)continue; // Age labels belong to the retained shared/death layer.
                if(zone.Points==null||zone.Points.Length==0||string.IsNullOrWhiteSpace(zone.Name))continue;
                if(MapWindow.WpfCustomZone(zone)?!state.Zones:zone.Points.Length==1&&!state.Waypoints)continue;
                candidates.Add(new WpfMapLabel { Text=zone.DisplayName,At=screen(MapWindow.WpfZoneCentre(zone)),Major=zone.Points.Length>1,Clearance=zone.Points.Length==1&&!MapWindow.WpfCustomZone(zone)?18:9 });
            }
            if(state.Store!=null&&state.Store.MasterEnabled)foreach(var cluster in VisiblePoiClusters(state,width,height,compact)) {
                var marker=cluster.Members.FirstOrDefault(m=>m.CategoryId==27||m.CategoryId==26||m.CategoryId==80)??cluster.Members[0];
                var at=screen(marker.Point);if(at.X<0||at.Y<0||at.X>width||at.Y>height)continue;
                string name=PoiLabel(state,marker,compact);if(string.IsNullOrWhiteSpace(name))continue;
                candidates.Add(new WpfMapLabel { Text=name,At=at,Major=marker.CategoryId==27||marker.CategoryId==26||marker.CategoryId==80 });
            }
            var result=new List<WpfMapLabel>();var occupied=new List<Rect>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in candidates.Where(c=>c.At.X>=0&&c.At.Y>=0&&c.At.X<=width&&c.At.Y<=height).OrderByDescending(c=>c.Source!=null&&c.Source.Kind=="entrance").ThenByDescending(c=>c.Major).ThenBy(c=>(c.At-new Point(width/2,height/2)).LengthSquared)) {
                if(item.Bounded) {
                    double cosine=Math.Abs(Math.Cos(item.Angle*Math.PI/180)),sine=Math.Abs(Math.Sin(item.Angle*Math.PI/180));
                    for(double font=Math.Min(14,Math.Max(8,state.LabelSize+2));font>=8;font--) {
                        var boundedText=LabelText(item.Text,font,item.Ink);double bw=boundedText.Width+8,bh=boundedText.Height+4;
                        if(cosine*bw+sine*bh>item.SafeWidth||sine*bw+cosine*bh>item.SafeHeight)continue;
                        var box=new Rect(item.At.X-bw/2,item.At.Y-bh/2,bw,bh);
                        if(box.Left<0||box.Top<0||box.Right>width||box.Bottom>height||occupied.Any(r=>r.IntersectsWith(box)))continue;
                        result.Add(new WpfMapLabel {Text=item.Text,At=new Point(item.At.X-boundedText.Width/2,item.At.Y-boundedText.Height/2),Major=true,Bounded=true,FontSize=font,Ink=item.Ink,Background=item.Background,Source=item.Source});occupied.Add(box);break;
                    }
                    continue;
                }
                if(names.Contains(item.Text))continue;
                double size=state.LabelSize>0?state.LabelSize:12;
                var text=LabelText(item.Text,item.Major?size+2:size,WpfFieldTheme.Ink);
                double labelWidth=Math.Min(220,text.Width)+8,labelHeight=text.Height+4;
                double gap=item.Clearance;
                foreach(var offset in new[]{new Vector(gap,-labelHeight),new Vector(gap,8),new Vector(-labelWidth-gap,-labelHeight),new Vector(-labelWidth-gap,8)}) {
                    var at=item.At+offset;var box=new Rect(at.X-4,at.Y-1,labelWidth,labelHeight);
                    if(box.Left<0||box.Top<0||box.Right>width||box.Bottom>height||occupied.Any(r=>r.IntersectsWith(box)))continue;
                    result.Add(new WpfMapLabel { Text=item.Text,At=at,Major=item.Major,Ink=item.Ink,Source=item.Source });occupied.Add(box);names.Add(item.Text);break;
                }
                if(result.Count>=Math.Max(2,Math.Min(compact?3:60,(int)(width*height/(compact?24000:14000)))))break;
            }
            return result.ToArray();
        }
        static void DrawLine(DrawingContext dc,IEnumerable<Point> points,Pen pen) { var all=points.ToArray();if(all.Length<2)return;var geometry=new StreamGeometry();using(var c=geometry.Open()) { c.BeginFigure(all[0],false,false);c.PolyLineTo(all.Skip(1).ToArray(),true,false); }geometry.Freeze();pen.StartLineCap=pen.EndLineCap=PenLineCap.Round;pen.LineJoin=PenLineJoin.Round;dc.DrawGeometry(null,pen,geometry); }
        static string PoiLabel(WpfMapState state,ScumMapMarker marker,bool compact) {
            var cache=poiCaches.GetValue(state.Store,store=>new[]{new WpfPoiCache(),new WpfPoiCache()})[compact?0:1];
            if(cache.Labels==null||cache.LabelLanguage!=Localization.CurrentCode) { cache.Labels=new Dictionary<ScumMapMarker,string>();cache.LabelLanguage=Localization.CurrentCode; }
            string result;if(!cache.Labels.TryGetValue(marker,out result)) { result=Localization.GetMapObjectName(state.Store.GetMarkerDisplayName(marker));cache.Labels.Add(marker,result); }return result;
        }
        struct LabelKey:IEquatable<LabelKey> {
            internal string Text;internal double Size;internal Color Color;
            public bool Equals(LabelKey other) { return Text==other.Text&&Size==other.Size&&Color==other.Color; }
            public override bool Equals(object other) { return other is LabelKey&&Equals((LabelKey)other); }
            public override int GetHashCode() { unchecked { return (Text.GetHashCode()*397^Size.GetHashCode())*397^Color.GetHashCode(); } }
        }
        static readonly Typeface labelTypeface=new Typeface("Barlow Semi Condensed, Segoe UI"),poiTypeface=new Typeface("Segoe UI");
        static readonly Dictionary<LabelKey,FormattedText> labelTexts=new Dictionary<LabelKey,FormattedText>();
        static readonly Queue<LabelKey> labelTextOrder=new Queue<LabelKey>();
        static string labelCulture;
        static FormattedText LabelText(string text,double size,Brush ink) {
            if(labelCulture!=CultureInfo.CurrentCulture.Name) { labelCulture=CultureInfo.CurrentCulture.Name;labelTexts.Clear();labelTextOrder.Clear(); }
            var key=new LabelKey { Text=text,Size=size,Color=((SolidColorBrush)ink).Color };FormattedText result;
            if(labelTexts.TryGetValue(key,out result))return result;
            result=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,labelTypeface,size,ink,1.0) { MaxTextWidth=220,MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis };
            if(labelTexts.Count>=1024)labelTexts.Remove(labelTextOrder.Dequeue());labelTexts.Add(key,result);labelTextOrder.Enqueue(key);return result;
        }
        static readonly Dictionary<string,Brush> roomColours=new Dictionary<string,Brush>();
        static Brush RoomColour(string colour) {Brush brush;if(!roomColours.TryGetValue(colour,out brush)) {var value=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+colour));value.Freeze();roomColours.Add(colour,value);brush=value;}return brush;}
        static readonly Pen placeOutline=PlaceOutline();
        static Pen PlaceOutline(){var pen=new Pen(WpfFieldTheme.ColorBrush("#E51B221D"),2.8){LineJoin=PenLineJoin.Round};pen.Freeze();return pen;}
        static readonly Dictionary<FormattedText,Geometry> placeShapes=new Dictionary<FormattedText,Geometry>();
        void Label(DrawingContext dc,string text,Point at,Brush ink,double size,Brush background=null,bool place=false) {
            var screen=RotatePoint(at,renderRotation,ActualWidth,ActualHeight);
            // Text backgrounds are capped at 228 DIPs. Reject distant labels
            // before formatting; retain labels that can cross a viewport edge.
            if(screen.X<-228||screen.X>ActualWidth+4||screen.Y<-size*3||screen.Y>ActualHeight+1)return;
            var f=LabelText(text,size,ink);var bounds=new Rect(screen.X-4,screen.Y-1,Math.Min(220,f.Width)+8,f.Height+2);
            if(!bounds.IntersectsWith(new Rect(0,0,ActualWidth,ActualHeight)))return;
            dc.PushTransform(new RotateTransform(-renderRotation,at.X,at.Y));
            if(place){
                Geometry shape;
                if(!placeShapes.TryGetValue(f,out shape)){
                    if(placeShapes.Count>=512)placeShapes.Clear();
                    shape=f.BuildGeometry(new Point());shape.Freeze();placeShapes.Add(f,shape);
                }
                dc.PushTransform(new TranslateTransform(at.X,at.Y));dc.DrawGeometry(null,placeOutline,shape);dc.DrawText(f,new Point());dc.Pop();
            }else{
                double corner=WpfFieldTheme.Current.Chamfer>0?1:Math.Min(WpfFieldTheme.Current.ControlRadius,10)/2+2;dc.DrawRoundedRectangle(background??WpfFieldTheme.Ground,null,new Rect(at.X-4,at.Y-1,Math.Min(220,f.Width)+8,f.Height+2),corner,corner);
                dc.DrawText(f,at);
            }
            dc.Pop();
        }
        internal static string PoiSymbol(ScumMapCategory category) {
            string name=category==null?"":(category.Section+" "+category.Name).ToLowerInvariant();
            if(name.Contains("fish"))return "≈";if(name.Contains("hunt"))return "⌖";
            if(name.Contains("gas")||name.Contains("fuel"))return "F";
            if(name.Contains("bunker"))return "B";if(name.Contains("outpost")||name.Contains("trader"))return "$";
            if(name.Contains("hospital")||name.Contains("medic"))return "+";
            if(name.Contains("police"))return "P";if(name.Contains("military"))return "★";
            if(name.Contains("town")||name.Contains("city"))return "⌂";
            if(name.Contains("vehicle"))return "V";return "◆";
        }
        static readonly Dictionary<string,DrawingGroup> poiIcons=new Dictionary<string,DrawingGroup>();
        static readonly Queue<string> poiIconOrder=new Queue<string>();
        static readonly Dictionary<string,Pen> poiOutlinePens=new Dictionary<string,Pen>();
        void DrawPoi(DrawingContext dc,Point at,ScumMapCategory category,WpfPoiCluster cluster) {
            EnsurePoiAssets(category,cluster);
            double radius=cluster.Radius;
            dc.DrawEllipse(WpfFieldTheme.Ground,cluster.Outline,at,radius,radius);
            double scale=(cluster.Members.Count>1?(Compact?9:11):radius*1.45)/12;
            var transform=new Matrix(scale,0,0,scale,0,0);if(renderRotation!=0)transform.Rotate(-renderRotation);transform.Translate(at.X,at.Y);
            dc.PushTransform(new MatrixTransform(transform));dc.DrawDrawing(cluster.Icon);dc.Pop();
        }
        void EnsurePoiAssets(ScumMapCategory category,WpfPoiCluster cluster) {
            int color=category==null?unchecked((int)0xFFA4B1BC):category.ColorBackground;
            if(cluster.Icon==null) {
                string label=cluster.Members.Count>1?(cluster.Members.Count>99?"99+":cluster.Members.Count.ToString(CultureInfo.InvariantCulture)):PoiSymbol(category);
                string key=label;
                DrawingGroup icon;
                if(!poiIcons.TryGetValue(key,out icon)) {
                icon=new DrawingGroup();using(var drawing=icon.Open()) {
                    // One frozen vector glyph per symbol/count. Zoom changes its
                    // transform, never its font layout or drawing asset.
                    var text=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,poiTypeface,12,Brushes.White,1.0);
                    drawing.DrawText(text,new Point(-text.Width/2,-text.Height/2));
                }icon.Freeze();
                if(poiIcons.Count>=512)poiIcons.Remove(poiIconOrder.Dequeue());
                poiIcons.Add(key,icon);poiIconOrder.Enqueue(key);
                }
                cluster.Icon=icon;
            }
            if(cluster.Outline==null||cluster.IconColor!=color) {
            string penKey=color+"|"+cluster.Mixed;Pen outline;
            if(!poiOutlinePens.TryGetValue(penKey,out outline)) {
                var ink=cluster.Mixed?WpfFieldTheme.MapAmber:new SolidColorBrush(Color.FromRgb((byte)(color>>16),(byte)(color>>8),(byte)color));
                outline=new Pen(ink,2);outline.Freeze();if(poiOutlinePens.Count>=256)poiOutlinePens.Clear();poiOutlinePens.Add(penKey,outline);
            }
            cluster.Outline=outline;cluster.IconColor=color;
            }
        }
    }

    // Use the same calibrated pyramid as the main map, decoding only visible tiles.
    internal sealed class WpfTerrainTiles:IDisposable {
        sealed class Level { internal int Width,Height,Columns,Rows;internal long[] Offsets;internal int[] Lengths; }
        sealed class Tile { internal long Key;internal BitmapSource Image;internal int Level,Column,Row; }
        readonly object tileGate=new object();readonly Stack<Tile> tileQueue=new Stack<Tile>();readonly HashSet<long> pendingTiles=new HashSet<long>(),failedTiles=new HashSet<long>();
        readonly Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
        bool interactive,tileWorker;volatile bool disposed;BitmapSource overview;
        internal event Action Changed;
        DispatcherTimer tileRefresh;
        void ScheduleTileRefresh() {
            if(tileRefresh==null) {
                tileRefresh=new DispatcherTimer(DispatcherPriority.Background,dispatcher) { Interval=TimeSpan.FromMilliseconds(60) };
                tileRefresh.Tick+=(s,e)=> { tileRefresh.Stop();if(!disposed&&Changed!=null)Changed(); };
            }
            if(!tileRefresh.IsEnabled)tileRefresh.Start();
        }
        readonly Stream stream;
        readonly List<Level> levels=new List<Level>();
        readonly Dictionary<long,LinkedListNode<Tile>> cache=new Dictionary<long,LinkedListNode<Tile>>();
        readonly LinkedList<Tile> recent=new LinkedList<Tile>();
        internal int CachedTileCount { get { return cache.Count; } }
        internal WpfTerrainTiles() {
            stream=InstallationAssets.Open("map-tiles.bin");
            if(stream==null||!stream.CanSeek)throw new InvalidDataException("SCUM map tiles are missing.");
            using(var reader=new BinaryReader(stream,System.Text.Encoding.ASCII,true)) {
                if(new string(reader.ReadChars(4))!="MTL2"||reader.ReadInt32()!=512)throw new InvalidDataException("Invalid map tile header.");
                int count=reader.ReadInt32();if(count<1||count>16)throw new InvalidDataException("Invalid map levels.");
                for(int i=0;i<count;i++) {
                    var level=new Level { Width=reader.ReadInt32(),Height=reader.ReadInt32(),Columns=reader.ReadInt32(),Rows=reader.ReadInt32() };
                    if(level.Width<1||level.Height<1||level.Columns!=(level.Width+511)/512||level.Rows!=(level.Height+511)/512||(long)level.Columns*level.Rows>4096)throw new InvalidDataException("Invalid map dimensions.");
                    int tiles=level.Columns*level.Rows;level.Offsets=new long[tiles];level.Lengths=new int[tiles];
                    for(int j=0;j<tiles;j++) { level.Offsets[j]=reader.ReadInt64();level.Lengths[j]=reader.ReadInt32();if(level.Lengths[j]<1||level.Lengths[j]>8*1024*1024||level.Offsets[j]<12||level.Offsets[j]>stream.Length-level.Lengths[j])throw new InvalidDataException("Invalid map tile."); }
                    levels.Add(level);
                }
            }
        }
        BitmapSource ReadTile(int levelIndex,int column,int row) {
            dispatcher.VerifyAccess();
            long key=((long)levelIndex<<32)|((long)row<<16)|(uint)column;LinkedListNode<Tile> cached;
            if(cache.TryGetValue(key,out cached)) { recent.Remove(cached);recent.AddFirst(cached);return cached.Value.Image; }
            if(interactive) {
                lock(tileGate)if(!disposed&&!pendingTiles.Contains(key)&&!failedTiles.Contains(key)&&pendingTiles.Count<64) {
                    pendingTiles.Add(key);tileQueue.Push(new Tile { Key=key,Level=levelIndex,Column=column,Row=row });
                    if(!tileWorker) { tileWorker=true;System.Threading.Tasks.Task.Run((Action)DecodeTiles); }
                }
                return null;
            }
            var image=DecodeTile(levelIndex,column,row);CacheTile(new Tile { Key=key,Image=image });return image;
        }
        BitmapSource DecodeTile(int levelIndex,int column,int row) {
            var level=levels[levelIndex];int index=row*level.Columns+column;var bytes=new byte[level.Lengths[index]];stream.Position=level.Offsets[index];int read=0;
            while(read<bytes.Length) { int count=stream.Read(bytes,read,bytes.Length-read);if(count==0)throw new EndOfStreamException();read+=count; }
            BitmapSource image;using(var encoded=new MemoryStream(bytes,false)) { var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=encoded;bitmap.EndInit();bitmap.Freeze();image=bitmap; }
            return image;
        }
        void CacheTile(Tile tile) {
            if(cache.ContainsKey(tile.Key))return;var cached=new LinkedListNode<Tile>(tile);recent.AddFirst(cached);cache.Add(tile.Key,cached);
            if(cache.Count>128) { var oldest=recent.Last;recent.RemoveLast();cache.Remove(oldest.Value.Key); }
        }
        void DecodeTiles() {
            while(true) {
                Tile tile;lock(tileGate) { if(disposed||tileQueue.Count==0) { tileWorker=false;if(disposed)stream.Dispose();return; }tile=tileQueue.Pop(); }
                try {
                    tile.Image=DecodeTile(tile.Level,tile.Column,tile.Row);
                    if(!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(new Action(()=> {
                        lock(tileGate)pendingTiles.Remove(tile.Key);if(disposed)return;CacheTile(tile);ScheduleTileRefresh();
                    }),DispatcherPriority.Background);
                }catch(Exception error) { lock(tileGate) { pendingTiles.Remove(tile.Key);failedTiles.Add(tile.Key); }Program.LogException("TerrainTile",error); }
            }
        }
        internal void Draw(DrawingContext dc,double width,double height,double left,double top,double side,double dpi=1) { DrawRotated(dc,width,height,left,top,side,dpi,0); }
        internal void DrawRotated(DrawingContext dc,double width,double height,double left,double top,double side,double dpi,double margin) {
            dispatcher.VerifyAccess();
            int index=0;for(int i=1;i<levels.Count;i++) { if(levels[i].Width<side*dpi)break;index=i; }
            Level level;double scale;int firstX,firstY,lastX,lastY;
            while(true) {
                level=levels[index];scale=side/level.Width;
                double x1=Math.Max(0,(-margin-left)/scale),y1=Math.Max(0,(-margin-top)/scale),x2=Math.Min(level.Width,(width+margin-left)/scale),y2=Math.Min(level.Height,(height+margin-top)/scale);
                if(x2<=x1||y2<=y1)return;
                firstX=Math.Max(0,(int)Math.Floor(x1/512));firstY=Math.Max(0,(int)Math.Floor(y1/512));
                lastX=Math.Min(level.Columns-1,(int)Math.Ceiling(x2/512)-1);lastY=Math.Min(level.Rows-1,(int)Math.Ceiling(y2/512)-1);
                // A high-DPI overview can otherwise request more tiles than fit
                // in the cache, continually evicting and decoding its own frame.
                if((lastX-firstX+1)*(lastY-firstY+1)<=64||index==levels.Count-1)break;
                index++;
            }
            var images=new BitmapSource[(lastX-firstX+1)*(lastY-firstY+1)];int imageIndex=0;bool missing=false;
            for(int row=firstY;row<=lastY;row++)for(int column=firstX;column<=lastX;column++) { var image=ReadTile(index,column,row);images[imageIndex++]=image;missing|=image==null; }
            // The overview fills cold tiles only. Painting it beneath a complete
            // high-resolution viewport doubles bitmap sampling during every pan.
            if(missing&&overview!=null)dc.DrawImage(overview,new Rect(left,top,side,side));
            imageIndex=0;
            for(int row=firstY;row<=lastY;row++)for(int column=firstX;column<=lastX;column++) {
                var tile=images[imageIndex++];double x=column*512,y=row*512;
                if(tile==null)continue;
                // A two-pixel gutter preserves bilinear sampling at tile edges.
                dc.PushClip(new RectangleGeometry(new Rect(left+x*scale,top+y*scale,Math.Min(512,level.Width-x)*scale,Math.Min(512,level.Height-y)*scale)));
                dc.DrawImage(tile,new Rect(left+Math.Max(0,x-2)*scale,top+Math.Max(0,y-2)*scale,tile.PixelWidth*scale,tile.PixelHeight*scale));dc.Pop();
            }
        }
        internal DrawingGroup Overview() { var visual=new DrawingVisual();using(var dc=visual.RenderOpen())Draw(dc,905,905,0,0,905);var image=new RenderTargetBitmap(905,905,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();overview=image;interactive=true;var drawing=new DrawingGroup();using(var dc=drawing.Open())dc.DrawImage(image,new Rect(0,0,1024,1024));drawing.Freeze();return drawing; }
        public void Dispose() { dispatcher.VerifyAccess();lock(tileGate) { if(disposed)return;disposed=true;tileQueue.Clear();pendingTiles.Clear();if(!tileWorker)stream.Dispose(); }if(tileRefresh!=null)tileRefresh.Stop();Changed=null;cache.Clear();recent.Clear(); }
    }

    internal sealed partial class WpfFieldConsoleRuntime:IDisposable {
        internal static WpfFieldConsoleRuntime Active;
        void PostUi(Action apply) {
            if(disposed||application.Dispatcher.HasShutdownStarted)return;
            try { application.Dispatcher.BeginInvoke(new Action(()=> { if(!disposed)apply(); })); }catch(InvalidOperationException) { }
        }
        readonly MapWindow engine;
        DirectPositions privatePositions;
        RelayGroupStore relayGroups;
        readonly Dictionary<string,int> playerVisibility=new Dictionary<string,int>(StringComparer.Ordinal);
        PrivatePositions.Peer[] playerRoster=new PrivatePositions.Peer[0];
        int PlayerVisibility(string id) { int value;return id!=null&&playerVisibility.TryGetValue(id,out value)?value:31; }
        bool PlayerMarkerVisible(SharedMapMarker marker) { return (PlayerVisibility(marker.OwnerId)&(marker.Death?8:marker.Id==SharedMapMarker.GpsId?2:SquadState.IsStop(marker)?16:4))!=0; }
        void AddPlayers(Panel container,bool registerLabels=true,bool header=true) {
            if(privatePositions==null&&(relayGroups==null||relayGroups.Sessions.Length==0)&&playerRoster.Length==0)return;
            if(header)container.Children.Add(WpfFieldTheme.Text(Localization.Get("SharedPlayers"),11,WpfFieldTheme.Muted,true));
            if(playerRoster.Length==0)container.Children.Add(WpfFieldTheme.Text(Localization.Get("PlayerWaiting"),12,WpfFieldTheme.Muted));
            foreach(var player in playerRoster) {
                string ownerId=player.Id;var body=new StackPanel();var nameRow=new DockPanel();var dot=new System.Windows.Shapes.Ellipse { Width=9,Height=9,Fill=WpfMapSurface.MemberInk(ownerId),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center };DockPanel.SetDock(dot,Dock.Left);nameRow.Children.Add(dot);nameRow.Children.Add(WpfFieldTheme.Text(player.Name,16,WpfFieldTheme.Ink,true));body.Children.Add(nameRow);
                var statusLabel=WpfFieldTheme.Text(PeerStatus(player),11,WpfFieldTheme.Muted);statusLabel.Tag=ownerId;statusLabel.TextWrapping=TextWrapping.Wrap;statusLabel.TextTrimming=TextTrimming.None;if(registerLabels)playerStatusLabels[ownerId]=statusLabel;body.Children.Add(statusLabel);
                var follow=WpfFieldTheme.Button(S(followingPeer==ownerId?"SquadStopNav":"SquadFollow"),()=> { if(followingPeer==ownerId) { StopSquadNavigation();filterIdentity=null;Refresh(); }else FollowSquadPeer(ownerId); });follow.IsEnabled=player.Position!=null||followingPeer==ownerId;body.Children.Add(follow);
                var pills=new WrapPanel { Margin=new Thickness(0,8,0,0) };
                string[] labels={"PlayerLiveLocation","PlayerGps","PlayerSaved","PlayerDeaths","RouteList"};
                for(int index=0;index<labels.Length;index++) {
                    int bit=1<<index;bool selected=(PlayerVisibility(ownerId)&bit)!=0;
                    var pill=new ToggleButton { Content=Localization.Get(labels[index]),IsChecked=selected,Tag="player:"+ownerId+":"+bit,Margin=new Thickness(0,0,5,5),Padding=new Thickness(9,4,9,4),FontSize=12,Foreground=selected?WpfFieldTheme.Accent:WpfFieldTheme.Muted,Background=selected?WpfFieldTheme.AccentDim:WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Cursor=Cursors.Hand };
                    var template=new ControlTemplate(typeof(ToggleButton));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(Math.Min(10,WpfFieldTheme.Current.ChipRadius)));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));border.AppendChild(content);template.VisualTree=border;pill.Template=template;
                    System.Windows.Automation.AutomationProperties.SetName(pill,player.Name+": "+Localization.Get(labels[index]));
                    pill.Click+=(s,e)=> { int mask=PlayerVisibility(ownerId);playerVisibility[ownerId]=pill.IsChecked==true?mask|bit:mask&~bit;pill.Background=pill.IsChecked==true?WpfFieldTheme.AccentDim:WpfFieldTheme.Panel;pill.Foreground=pill.IsChecked==true?WpfFieldTheme.Accent:WpfFieldTheme.Muted;lastRefresh=-1000;Refresh(); };pills.Children.Add(pill);
                }
                body.Children.Add(pills);var card=WpfFieldTheme.Card(body,new Thickness(10));card.Margin=new Thickness(0,8,0,10);container.Children.Add(card);
            }
        }
        internal Func<System.Threading.Tasks.Task<string>> publicAddressLookup=null;
        readonly Application application;
        readonly DrawingGroup geography;
        readonly WpfTerrainTiles terrain;
        readonly DispatcherTimer refresh;
        ScumAutoLaunch autoLaunch;
        readonly bool diagnostic;
        readonly AppZoneFolder zoneFolder;
        System.Threading.Tasks.Task<string> zoneOpenRead;
        DateTime nextZoneOpenRead;
        bool zoneFileActive;
        bool hudMoving;
        long nextHudRaise,nextRefreshError;
        System.Drawing.Rectangle lastHudBounds;
        Window tactical,hud;
        Window settingsWindow;
        volatile bool settingsToggleOpen;
        WpfMapSurface tacticalMap,hudMap;
        Action<bool,bool> syncMapChrome;
        StackPanel sidebar,mapPage,squadPage;
        string sidebarPage="map";Action syncRail;
        readonly Dictionary<string,Button> sidebarPageButtons=new Dictionary<string,Button>();
        void ShowSidebarPage(string page) {
            sidebarPage=page;
            if(mapPage!=null)mapPage.Visibility=page=="map"?Visibility.Visible:Visibility.Collapsed;
            if(squadPage!=null)squadPage.Visibility=page=="squad"?Visibility.Visible:Visibility.Collapsed;
            if(crosshairPage!=null)crosshairPage.Visibility=page=="crosshair"?Visibility.Visible:Visibility.Collapsed;
            if(page=="crosshair")SyncCrosshairPage();
            if(screenFilterPage!=null)screenFilterPage.Visibility=page=="screen-filter"?Visibility.Visible:Visibility.Collapsed;
            if(page=="screen-filter")SyncScreenFilterPage();
            if(syncRail!=null)syncRail();
        }
        Grid hudLayout;
        Border hudStatus;
        Border nativeLocationCard;
        TextBlock targetTitle,targetDetail,hudLocation,hudSector,routePreviewTitle,routePreviewDetail,tacticalLocation;
        WpfSatnavTicker hudRoute;
        Border targetCard,routePreviewCard;
        Border hoverCard;
        TextBlock hoverInfo;
        void ShowHoverDetails(string text) { hoverInfo.Text=text??"";hoverCard.Visibility=string.IsNullOrWhiteSpace(text)?Visibility.Collapsed:Visibility.Visible; }
        readonly HashSet<string> expanded=new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Bunkers" };
        readonly List<Window> dialogs=new List<Window>();
        bool rebuilding,hudEnabled=true;volatile bool disposed;
        internal bool HudEnabled { get { return hudEnabled; } }
        readonly MapMouseShield mouseShield=new MapMouseShield();
        volatile bool tacticalMouseShieldActive;
        IntPtr gameMapWindow;
        Button followButton,zoomResetButton,addTargetStop;
        ComboBox bunkerViewsControl;
        Slider tacticalOpacity;
        TextBlock tacticalOpacityValue;
        bool syncingOpacity;
        DispatcherTimer cameraSettle;
        string filterIdentity,tacticalVisualIdentity,hudVisualIdentity;
        readonly Dictionary<int,CheckBox> poiCategoryControls=new Dictionary<int,CheckBox>();
        readonly Dictionary<string,CheckBox> poiSectionControls=new Dictionary<string,CheckBox>();
        readonly Dictionary<string,TextBlock> poiSectionCounts=new Dictionary<string,TextBlock>();
        string presentedPoiFilter;
        void RefreshInteraction() { EndCameraInteraction();lastRefresh=-1000;Refresh(); }
        void EndCameraInteraction() {
            if(cameraSettle!=null)cameraSettle.Stop();if(tacticalMap==null)return;
            tacticalMap.ZoomGestureActive=false;
            tacticalMap.EndCameraFrames();RenderOptions.SetBitmapScalingMode(tacticalMap,BitmapScalingMode.Unspecified);tacticalMap.RequestRedraw();
        }
        void RefreshCamera() {
            application.Dispatcher.VerifyAccess();if(disposed)return;
            if(tacticalMap.State==null) { RefreshInteraction();return; }
            // Camera input does not poll groups, recalculate routes or rebuild
            // the sidebar. The regular refresh continues to publish live data.
            engine.WpfUpdateCamera(tacticalMap.State,true);tacticalVisualIdentity=null;
            tacticalMap.BeginCameraFrames();tacticalMap.ClearHover();
            RenderOptions.SetBitmapScalingMode(tacticalMap,BitmapScalingMode.Linear);
            if(cameraSettle==null) { cameraSettle=new DispatcherTimer(DispatcherPriority.Background,application.Dispatcher) { Interval=TimeSpan.FromMilliseconds(200) };cameraSettle.Tick+=(s,e)=> { if(disposed) { cameraSettle.Stop();return; }if(tacticalMap.PointerCameraActive)return;cameraSettle.Stop();EndCameraInteraction(); }; }
            cameraSettle.Stop();cameraSettle.Start();
            syncMapChrome(tacticalMap.State.Bunker==null && tacticalMap.State.Opacity==0,false);
            SyncCameraControls(tacticalMap.State);tacticalMap.RequestRedraw();
        }
        float zoomTarget;
        DispatcherTimer zoomAnimation;
        void QueueZoom(int direction,bool tactical) {
            if(disposed||tacticalMap==null)return;
            if(!tactical) { engine.WpfZoom(false,direction);RefreshCamera();return; }
            if(tacticalMap.State!=null&&tacticalMap.State.Bunker!=null) { engine.WpfZoom(true,direction);RefreshCamera();return; }
            bool animating=zoomAnimation!=null&&zoomAnimation.IsEnabled;
            float current=animating?zoomTarget:(tacticalMap.State!=null?tacticalMap.State.Zoom:engine.WpfFullMapZoom);
            zoomTarget=ClampZoom(current*(float)Math.Pow(1f+engine.WpfZoomStepPercent/100f,direction));
            if(zoomTarget<=1.0001f&&(current<=1.0001f&&direction<0)) { zoomTarget=1f; }
            tacticalMap.ZoomGestureActive=true;
            if(zoomAnimation==null) {
                zoomAnimation=new DispatcherTimer(DispatcherPriority.Render,application.Dispatcher) { Interval=TimeSpan.FromMilliseconds(15) };
                zoomAnimation.Tick+=(s,e)=>AdvanceZoom();
            }
            zoomAnimation.Start();
            AdvanceZoom();
        }
        void AdvanceZoom() {
            if(disposed||tacticalMap==null||tacticalMap.State==null) { if(zoomAnimation!=null)zoomAnimation.Stop();return; }
            float current=tacticalMap.State==null?engine.WpfFullMapZoom:tacticalMap.State.Zoom;
            float next=current+(zoomTarget-current)*0.4f;
            if(Math.Abs(zoomTarget-next)<=0.01f) { next=zoomTarget;zoomAnimation.Stop(); }
            engine.WpfZoomTo(true,next);
            RefreshCamera();
        }
        float ClampZoom(float value) { return Math.Max(1f,Math.Min(engine.WpfZoomLimit,value)); }
        void SyncCameraControls(WpfMapState state) {
            if(bunkerViewsControl!=null) {bool old=rebuilding;rebuilding=true;try {bunkerViewsControl.SelectedItem=bunkerViewsControl.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==engine.WpfBunkerViewSelection);}finally {rebuilding=old;}}
            syncingOpacity=true;try { tacticalOpacity.Value=state.Opacity;tacticalOpacity.IsEnabled=false;tacticalOpacityValue.Text=state.Opacity.ToString("0",CultureInfo.CurrentCulture)+"%"; }finally { syncingOpacity=false; }
            StyleFollow();
            if(zoomResetButton!=null)zoomResetButton.Content=state.Zoom.ToString("0.0",CultureInfo.CurrentCulture)+"×";
            if(zoomBars!=null)zoomBars.Level=(int)Math.Round(Math.Log(Math.Max(1,state.Zoom),2));
            if(statusZoom!=null)statusZoom.Text=state.Zoom.ToString("0.0",CultureInfo.CurrentCulture)+"×";
        }
        void SyncPoiControls(ScumMapStore store) {
            if(store==null||presentedPoiFilter==store.GetFilterHashKey())return;
            rebuilding=true;try {
                foreach(var entry in poiCategoryControls)entry.Value.IsChecked=store.IsCategoryEnabled(entry.Key);
                foreach(var entry in poiSectionControls) {
                    var categories=store.CategoriesBySection[entry.Key];int enabled=categories.Count(c=>store.IsCategoryEnabled(c.Id));
                    entry.Value.IsChecked=enabled==0?(bool?)false:enabled==categories.Count?(bool?)true:null;
                    poiSectionCounts[entry.Key].Text=enabled+" / "+categories.Count;
                }
                presentedPoiFilter=store.GetFilterHashKey();
            }finally { rebuilding=false; }
        }
        readonly System.Diagnostics.Stopwatch refreshClock=System.Diagnostics.Stopwatch.StartNew();
        long lastRefresh=-1000;
        long lastRouteTextRefresh=-1000;
        Button deathRemovalButton;
        string chromeLanguage;
        IntPtr inputTacticalHandle;
        volatile IntPtr[] inputDialogHandles=new IntPtr[0];
        volatile bool inputEditingText;
        Window setupWindow;
        bool setupHasFocus;
        void SyncModalInput() { engine.WpfModal(dialogs.Count>0||(setupWindow!=null&&setupHasFocus)); }
        void SetSetupInputFocus(bool focused) { setupHasFocus=focused;if(setupWindow!=null)setupWindow.Topmost=focused;SyncModalInput();RefreshInputOwnership(); }
        void RefreshInputOwnership() {
            application.Dispatcher.VerifyAccess();
            System.Threading.Interlocked.Exchange(ref inputTacticalHandle,tactical==null?IntPtr.Zero:new WindowInteropHelper(tactical).Handle);
            inputDialogHandles=dialogs.Concat(setupWindow==null?new Window[0]:new[]{setupWindow}).Select(w=>new WindowInteropHelper(w).Handle).Where(h=>h!=IntPtr.Zero).ToArray();
            var focused=Keyboard.FocusedElement as DependencyObject;var owner=focused==null?null:System.Windows.Window.GetWindow(focused);
            inputEditingText=owner!=null&&owner.IsVisible&&EditingText(Keyboard.FocusedElement);
        }
        static void RefreshLocalizedChrome(DependencyObject root) {
            var element=root as FrameworkElement;string tag=element==null?null:element.Tag as string;
            if(tag!=null&&(tag.StartsWith("text:")||tag.StartsWith("tooltip:")||tag.StartsWith("caps:"))) {
                string value=Localization.Get(tag.Substring(tag.IndexOf(':')+1));
                // caps: labels follow the theme's capitalisation (tactical uppercases Latin text).
                if(tag.StartsWith("caps:")) { value=WpfFieldTheme.Current.Caps(value);var text=element as TextBlock;var content=element as ContentControl;if(text!=null)text.Text=value;else if(content!=null)content.Content=value; }
                else if(tag.StartsWith("text:")) { var text=element as TextBlock;var content=element as ContentControl;if(text!=null)text.Text=value;else if(content!=null)content.Content=value; }
                element.ToolTip=value;System.Windows.Automation.AutomationProperties.SetName(element,value);
            }
            foreach(object child in LogicalTreeHelper.GetChildren(root)) { var dependency=child as DependencyObject;if(dependency!=null)RefreshLocalizedChrome(dependency); }
        }
        static string VisualIdentity(WpfMapState state) {
            var key=new System.Text.StringBuilder();key.Append(Localization.Current);
            foreach(var field in typeof(WpfMapState).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)) {
                if(field.Name=="Updated")continue;var value=field.GetValue(state);
                if(value==null) { key.Append("null|");continue; }
                if(value is Position) { var p=(Position)value;key.Append(p.X).Append(',').Append(p.Y).Append(',').Append(p.Yaw); }
                else if(value is ScumMapStore)key.Append(((ScumMapStore)value).GetFilterHashKey());
                else if(value is PrivatePositions.Peer[])foreach(var peer in (PrivatePositions.Peer[])value)key.Append(peer.Id).Append('|').Append(peer.Name).Append(peer.Position.X).Append(peer.Position.Y).Append(peer.Position.Yaw).Append(PrivatePositions.Live(peer.Updated,DateTime.UtcNow));
                else if(value is SquadRoutePlan)key.Append(((SquadRoutePlan)value).Revision);
                else if(value is SquadRouteDrawing[])foreach(var route in (SquadRouteDrawing[])value)key.Append(route.Key).Append(route.Name).Append(route.Shared).Append(route.Plan.Revision);
                else if(value is SharedMapMarker[])foreach(var marker in (SharedMapMarker[])value)key.Append('|').Append(marker.OwnerId).Append('|').Append(marker.Owner).Append('|').Append(marker.Id).Append('|').Append(marker.Name).Append('|').Append(marker.Death).Append('|').Append(marker.X).Append('|').Append(marker.Y);
                else if(value is MapZone[]) { key.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value));foreach(var death in ((MapZone[])value).Where(zone=>zone.IsDeathMarker))key.Append('|').Append(death.DisplayName); }
                else if(value is DrawingPoint[]||value is WpfZoneOption[])key.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value));
                else key.Append(value);
                key.Append('|');
            }
            return key.ToString();
        }
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",EntryPoint="ShowWindow")]static extern bool ShowNativeWindow(IntPtr window,int command);
        [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr window,int index,int value);
        [DllImport("user32.dll")]static extern bool ClipCursor(IntPtr rectangle);
        [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
        ResourceDictionary themeResources;
        internal WpfFieldConsoleRuntime(string folder,bool diagnostic=false) {
            this.diagnostic=diagnostic;themeFolder=folder;
            WpfThemes.ExtractFonts(Path.Combine(folder,"field-console-fonts"));WpfFieldTheme.Current=WpfThemes.Load(folder);
            application=Application.Current??new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };themeResources=WpfFieldTheme.Resources();application.Resources.MergedDictionaries.Add(themeResources);
            engine=new MapWindow(folder,diagnostic);var handle=engine.Handle;engine.WpfInitializeDefaults();
            if(!diagnostic)try { relayGroups=new RelayGroupStore(engine.WpfDataFolder); }catch(Exception error) { Program.LogException("RestorePrivateGroups",error); }
            if(!diagnostic)zoneFolder=new AppZoneFolder(AppDomain.CurrentDomain.BaseDirectory);
            if(diagnostic)((System.Windows.Forms.Timer)typeof(MapWindow).GetField("timer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(engine)).Stop();
            NavigationRouter.Initialize();terrain=new WpfTerrainTiles();geography=terrain.Overview();
            string fonts=Path.Combine(folder,"field-console-fonts");Directory.CreateDirectory(fonts);
            foreach(string font in new[]{"BarlowSemiCondensed-Regular.ttf","BarlowSemiCondensed-SemiBold.ttf"})using(var stream=typeof(MapWindow).Assembly.GetManifestResourceStream(font))if(stream!=null)using(var file=File.Create(Path.Combine(fonts,font)))stream.CopyTo(file);
            ApplyWindowStyle();
            tactical=BuildTactical();hud=BuildHud();StartCrosshair();StartScreenFilter();
            terrain.Changed+=()=> { if(!disposed) { tacticalMap.TerrainRevision++;if(tactical.IsVisible&&tacticalMap.State!=null&&tacticalMap.State.Opacity>0)tacticalMap.RequestRedraw();if(hud.IsVisible)hudMap.InvalidateVisual(); } };
            WireTactical();
            refresh=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(250) };refresh.Tick+=(s,e)=> {
                // DispatcherTimer does not schedule its next tick if its callback escapes.
                try { Refresh(); }catch(Exception error) { if(refreshClock.ElapsedMilliseconds>=nextRefreshError) { nextRefreshError=refreshClock.ElapsedMilliseconds+10000;Program.LogException("WpfRefresh",error); } }
            };
            if(!diagnostic) {
                refresh.Start();
                autoLaunch=new ScumAutoLaunch(()=>application.Dispatcher.BeginInvoke(new Action(RequestExit)));
            }
        }
        internal static void Run(string folder) {
            using(var runtime=new WpfFieldConsoleRuntime(folder)) {
                Active=runtime;
                runtime.StartViews();
                Program.CompletePostUpdate(folder,runtime.ShowPostUpdate);
                if(runtime.engine.WpfNeedsOnboarding)runtime.ShowSetupWindow(false);
                runtime.application.Run();Active=null;
            }
        }
        internal Window BuildPostUpdate(string result) {
            // The updater writes a fixed English result; show it as a translated outcome with the technical detail underneath.
            string message=(result??"").Trim();bool installed=message.StartsWith("Update installed successfully",StringComparison.Ordinal),failed=message.StartsWith("Update failed",StringComparison.Ordinal);
            var found=System.Text.RegularExpressions.Regex.Match(message,@"v?(\d+(?:\.\d+){1,3})\s*$");string version=found.Success?found.Groups[1].Value:"";
            string title=installed?(version.Length>0?Localization.T("UpdateDoneTitle",version):Localization.Get("UpdateDoneTitleShort")):failed?Localization.Get("UpdateFailedTitle"):Localization.Get("UpdateCheckTitle");
            var window=Window(title,560,failed?400:340);var dialog=new WpfKit.Dialog(window,title);
            var head=new DockPanel();var icon=WpfKit.IconTile(installed?"circle-check":failed?"triangle-alert":"download",installed?WpfFieldTheme.Success:failed?WpfFieldTheme.Warn:null,44);icon.Margin=new Thickness(0,0,16,0);icon.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(icon,Dock.Left);head.Children.Add(icon);
            var lines=new StackPanel();var text=WpfKit.Help(installed?Localization.Get("UpdateDoneHelp"):failed?Localization.Get("UpdateFailedHelp"):message,15);text.Foreground=WpfFieldTheme.Ink;lines.Children.Add(text);
            if(failed) { var detail=WpfKit.Mono(message,11,WpfFieldTheme.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,10,0,0);lines.Children.Add(detail); }
            head.Children.Add(lines);head.Margin=new Thickness(0,8,0,0);dialog.Add(head);
            dialog.AddAction(WpfKit.Primary(Localization.Get("Done"),()=>window.Close(),"check"));return window;
        }
        void ShowPostUpdate(string result) { Modal(BuildPostUpdate(result)); }
        internal bool SuppressMapMouse(int message,System.Drawing.Point point,uint data) {
            // Called on the mouse hook thread: never read WPF dependency properties here.
            if(!tacticalMouseShieldActive)return mouseShield.Suppress(message,data,false,false,false);
            bool active=TacticalMouseShieldEnabled(tacticalMouseShieldActive,Native.IsOurWindow(Native.GetForegroundWindow()));
            if(!active)return mouseShield.Suppress(message,data,false,false,false);
            IntPtr target=Native.WindowFromPoint(point);
            bool inside=Native.IsOurWindow(target);
            // The game HWND was validated on the UI thread when opening the map.
            // Never enumerate processes or touch controls in a low-level hook.
            IntPtr game=System.Threading.Interlocked.CompareExchange(ref gameMapWindow,IntPtr.Zero,IntPtr.Zero);
            return mouseShield.Suppress(message,data,true,inside,!inside&&game!=IntPtr.Zero&&GetAncestor(target,2)==game);
        }
        internal static bool TacticalMouseShieldEnabled(bool visible,bool appFocused) { return visible&&appFocused; }
        internal static bool ShouldHideTactical(bool visible,bool gameFocused,bool appFocused) { return visible&&!gameFocused&&!appFocused; }
        internal void SyncTacticalFocus(bool gameFocused,bool appFocused) {
            if(ShouldHideTactical(tactical.IsVisible,gameFocused,appFocused)) { tacticalMouseShieldActive=false;tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false); }
            else if(tactical.IsVisible)tactical.Topmost=true;
        }
        internal void SetTactical(bool active) { if(active) { if(!tactical.IsVisible)ToggleTactical(); }else CloseTactical(); }
        internal bool TryDismissTactical(int key,int scan,bool chatPaused) {
            if(!tactical.IsVisible||dialogs.Count!=0||EditingText(Keyboard.FocusedElement))return false;
            var binding=engine.WpfSnapshot(false);bool mapKey=PhysicalKeyCapture.Matches(key,scan,binding.MapKey,binding.MapScan);
            if(key!=0x1B&&key!=0x09&&(!mapKey||chatPaused))return false;
            if(key==0x1B&&tacticalMap.ContextMenuOpen) { tacticalMap.CloseContextMenu();return true; }
            CloseTactical(mapKey&&new WindowInteropHelper(tactical).Handle==Native.GetForegroundWindow());return true;
        }
        static IntPtr TacticalWindowMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) {
            // MA_NOACTIVATE keeps keyboard/held movement in SCUM while still
            // delivering this mouse click to the map's controls.
            if(message==0x21) {handled=true;return new IntPtr(3);}
            return IntPtr.Zero;
        }
        internal void CloseTactical(bool closeGameMap=false) { tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false);SyncHudVisibility();if(closeGameMap&&!diagnostic&&gameMapWindow!=IntPtr.Zero) { var binding=engine.WpfSnapshot(false);if(!binding.TrackingPaused)Native.ReturnMapKey(gameMapWindow,binding.MapKey,binding.MapScan); }Refresh(); }
        internal System.Drawing.Rectangle LastMonitorBounds;
        System.Drawing.Rectangle lastTacticalBounds;
        internal static System.Drawing.Rectangle RecoverMapBounds(System.Drawing.Rectangle desired,System.Drawing.Rectangle[] monitors) {
            if(monitors==null||monitors.Length==0)return desired;
            // Recover a disconnected display and oversized saved windows so all
            // placement controls remain reachable. Coordinates are physical pixels.
            var monitor=monitors.OrderByDescending(m=> { var overlap=System.Drawing.Rectangle.Intersect(desired,m);return (long)Math.Max(0,overlap.Width)*Math.Max(0,overlap.Height); }).First();
            int width=Math.Min(Math.Max(640,desired.Width),monitor.Width),height=Math.Min(Math.Max(420,desired.Height),monitor.Height);
            return new System.Drawing.Rectangle(Math.Max(monitor.Left,Math.Min(desired.X,monitor.Right-width)),Math.Max(monitor.Top,Math.Min(desired.Y,monitor.Bottom-height)),width,height);
        }
        System.Drawing.Rectangle TacticalBounds() {
            var monitor=System.Windows.Forms.Screen.FromHandle(gameMapWindow==IntPtr.Zero?new WindowInteropHelper(hud).EnsureHandle():gameMapWindow);
            LastMonitorBounds=monitor.Bounds;
            System.Drawing.Rectangle gameBounds;
            Native.TryGameClientBounds(gameMapWindow,out gameBounds);
            return ResolveMapBounds(engine.WpfManualMap,engine.WpfMapBounds,gameBounds,monitor.Bounds,System.Windows.Forms.Screen.AllScreens.Select(s=>s.Bounds).ToArray());
        }
        internal static System.Drawing.Rectangle ResolveMapBounds(bool manual,System.Drawing.Rectangle saved,System.Drawing.Rectangle game,System.Drawing.Rectangle monitor,System.Drawing.Rectangle[] monitors) {
            if(manual&&saved.Width>=640&&saved.Height>=420)return RecoverMapBounds(saved,monitors);
            return game.Width>0&&game.Height>0?game:monitor;
        }
        internal void FitTacticalToMonitor() {
            var bounds=TacticalBounds();tactical.WindowState=WindowState.Normal;tactical.ResizeMode=ResizeMode.NoResize;tactical.WindowStartupLocation=WindowStartupLocation.Manual;
            if(diagnostic) { bounds.X=-32000;bounds.Y=-32000; }
            ApplyTacticalBounds(bounds);
        }
        void ApplyTacticalBounds(System.Drawing.Rectangle bounds) {
            var handle=new WindowInteropHelper(tactical).EnsureHandle();
            if(IsIconic(handle))ShowNativeWindow(handle,4); // SW_SHOWNOACTIVATE clears native minimization.
            // WPF can retain the iconic window size after minimization. Update its
            // layout properties as well as the native bounds before rendering again.
            var source=HwndSource.FromHwnd(handle);var size=source.CompositionTarget.TransformFromDevice.Transform(new Point(bounds.Width,bounds.Height));
            tactical.MinWidth=Math.Min(900,size.X);tactical.MinHeight=Math.Min(620,size.Y);
            tactical.Width=size.X;tactical.Height=size.Y;
            SetWindowPos(handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0010);
            tactical.UpdateLayout();lastTacticalBounds=bounds;
        }
        internal void StartViews() {
            tactical.Hide();engine.WpfTactical(false);SyncHudVisibility();Refresh();
            application.Dispatcher.BeginInvoke(new Action(()=> {
                if(disposed||tactical.IsVisible||lastTacticalBounds.Width>0)return;
                FitTacticalToMonitor();
                var content=(FrameworkElement)tactical.Content;
                var size=new Size(tactical.Width,tactical.Height);
                content.Measure(size);content.Arrange(new Rect(size));
                RefreshView(null,true);
            }),DispatcherPriority.ApplicationIdle);
        }
        internal static bool ShouldShowHud(bool enabled,bool tacticalVisible) { return enabled&&!tacticalVisible; }
        internal static System.Drawing.Rectangle RecoverHudBounds(System.Drawing.Rectangle desired,System.Drawing.Rectangle[] monitors) {
            if(monitors==null||monitors.Length==0)return desired;
            foreach(var monitor in monitors) { var visible=System.Drawing.Rectangle.Intersect(desired,monitor);if(visible.Width>=Math.Min(monitor.Width,desired.Width)&&visible.Height>=Math.Min(monitor.Height,desired.Height))return desired; }
            var nearest=monitors.OrderBy(m=>Math.Pow(Math.Max(m.Left-desired.Left,Math.Max(0,(double)desired.Left-m.Right)),2)+Math.Pow(Math.Max(m.Top-desired.Top,Math.Max(0,(double)desired.Top-m.Bottom)),2)).First();
            return new System.Drawing.Rectangle(Math.Max(nearest.Left,Math.Min(desired.Left,nearest.Right-desired.Width)),Math.Max(nearest.Top,Math.Min(desired.Top,nearest.Bottom-desired.Height)),desired.Width,desired.Height);
        }
        internal System.Drawing.Rectangle AdminMapBounds {
            get {
                if(!TacticalVisible)return System.Drawing.Rectangle.Empty;
                var origin=tacticalMap.PointToScreen(new Point(0,0));
                var end=tacticalMap.PointToScreen(new Point(tacticalMap.ActualWidth,tacticalMap.ActualHeight));
                return System.Drawing.Rectangle.FromLTRB((int)origin.X,(int)origin.Y,(int)end.X,(int)end.Y);
            }
        }
        bool TacticalVisible { get { return tactical.IsVisible&&tactical.WindowState!=WindowState.Minimized&&!IsIconic(new WindowInteropHelper(tactical).Handle); } }
        void SyncHudVisibility(bool force=false) {
            bool show=ShouldShowHud(hudEnabled,TacticalVisible) && !hudMoving && !Native.GameMenuCursorVisible(Native.GameFocused());
            if(!show) { if(hud.IsVisible)hud.Hide();return; }
            bool restore=!hud.IsVisible||hud.WindowState==WindowState.Minimized;
            if(hud.WindowState!=WindowState.Normal)hud.WindowState=WindowState.Normal;
            if(!hud.IsVisible)hud.Show();hud.Topmost=true;
            var handle=new WindowInteropHelper(hud).EnsureHandle();if(IsIconic(handle)) { ShowNativeWindow(handle,4);restore=true; }
            if(force||restore) { hudVisualIdentity=null;hudMap.InvalidateVisual(); }
            if(diagnostic)return;
            var state=engine.WpfSnapshot(false);hud.Width=state.HudWidth;hud.Height=state.HudHeight+(state.Status?60:0);
            var source=HwndSource.FromHwnd(handle);if(source==null||source.CompositionTarget==null)return;
            var size=source.CompositionTarget.TransformToDevice.Transform(new Point(hud.Width,hud.Height));
            var bounds=RecoverHudBounds(new System.Drawing.Rectangle(state.HudLeft,state.HudTop,Math.Max(1,(int)Math.Ceiling(size.X)),Math.Max(1,(int)Math.Ceiling(size.Y))),System.Windows.Forms.Screen.AllScreens.Select(s=>s.WorkingArea).ToArray());
            if(bounds.X!=state.HudLeft||bounds.Y!=state.HudTop)engine.WpfMoveHud(bounds.X,bounds.Y);
            bool raise=dialogs.Count==0&&(force||restore||refreshClock.ElapsedMilliseconds>=nextHudRaise);
            if(raise)nextHudRaise=refreshClock.ElapsedMilliseconds+2000;
            if(raise||restore||bounds!=lastHudBounds)SetWindowPos(handle,raise?new IntPtr(-1):IntPtr.Zero,bounds.X,bounds.Y,0,0,(uint)(0x0001|0x0010|0x0040|(raise?0:0x0004)));
            lastHudBounds=bounds;
        }
        internal void ToggleHud() {
            bool menuVisible=Native.GameMenuCursorVisible(Native.GameFocused());
            bool recover=!hudEnabled || TacticalVisible || (!menuVisible && (!hud.IsVisible || hud.WindowState==WindowState.Minimized || IsIconic(new WindowInteropHelper(hud).Handle)));
            if(TacticalVisible) { tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false); }
            hudEnabled=recover;SyncHudVisibility(true);
        }
        Window Window(string title,double w,double h) {
            var window=new Window { Title=title,Width=w,Height=h,MinWidth=Math.Min(w,420),MinHeight=Math.Min(h,300),WindowStartupLocation=WindowStartupLocation.CenterScreen,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.CanResizeWithGrip,Background=WpfFieldTheme.Ground,Foreground=WpfFieldTheme.Ink,FlowDirection=Localization.Current==AppLanguage.Arabic?FlowDirection.RightToLeft:FlowDirection.LeftToRight };
            window.SourceInitialized+=(s,e)=> { ThemedCaption.Apply(new WindowInteropHelper(window).Handle);RefreshInputOwnership(); };
            // Placement-controlled map/HUD windows have their own physical-pixel recovery.
            window.Loaded+=(s,e)=> { if(window!=tactical&&window!=hud&&window.Tag as string!="hud-move"&&window.Tag as string!="map-placement-preview")FitToolWindow(window,!diagnostic); };
            window.GotKeyboardFocus+=(s,e)=>RefreshInputOwnership();window.LostKeyboardFocus+=(s,e)=>inputEditingText=EditingText(e.NewFocus);
            window.PreviewKeyDown+=(s,e)=> {
                if(e.Key!=Key.Escape)return;
                // Dismiss the innermost interaction before its containing window.
                var dropdown=ChildrenOf<ComboBox>(window).FirstOrDefault(c=>c.IsDropDownOpen);
                if(dropdown!=null)dropdown.IsDropDownOpen=false;
                else if(window==tactical&&tacticalMap!=null&&tacticalMap.ContextMenuOpen)tacticalMap.CloseContextMenu();
                else window.Close();
                e.Handled=true;
            };
            return window;
        }
        static void FitToolWindow(Window window,bool position) {
            var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);if(source==null||source.CompositionTarget==null)return;
            var area=System.Windows.Forms.Screen.FromHandle(source.Handle).WorkingArea;
            var transform=source.CompositionTarget.TransformFromDevice;
            var origin=transform.Transform(new Point(area.Left,area.Top));var size=transform.Transform(new Vector(area.Width,area.Height));
            double width=Math.Max(1,size.X),height=Math.Max(1,size.Y);
            window.MinWidth=Math.Min(window.MinWidth,width);window.MinHeight=Math.Min(window.MinHeight,height);
            window.Width=Math.Min(window.Width,width);window.Height=Math.Min(window.Height,height);
            if(position) { window.Left=Math.Max(origin.X,Math.Min(window.Left,origin.X+width-window.Width));window.Top=Math.Max(origin.Y,Math.Min(window.Top,origin.Y+height-window.Height)); }
        }
        Window BuildHud() {
            var window=new Window { Title="SCUM MiniMap HUD",Width=240,Height=300,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ShowActivated=false,Left=48,Top=48 };
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(240) });layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(60) });
            hudMap=new WpfMapSurface { Compact=true,BaseMap=geography,Terrain=terrain };layout.Children.Add(hudMap);layout.Children.Add(BuildHudFrame());
            var status=BuildHudStatus();Grid.SetRow(status,1);layout.Children.Add(status);hudLayout=layout;hudStatus=status;window.Content=layout;
            window.SourceInitialized+=(s,e)=> { IntPtr handle=new WindowInteropHelper(window).Handle;SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000000|0x20); };
            window.Closing+=(s,e)=> { if(!disposed&&window==hud) { e.Cancel=true;window.Hide(); } };return window;
        }
        void AddLayer(Panel container,string key,string label,bool value,string icon=null) { var box=new CheckBox { Content=label,IsChecked=value,Margin=new Thickness(0,1,0,1),Tag="layer:"+key };if(icon!=null)RowIcon(box,icon,LayerTint(key));box.Checked+=(s,e)=> { engine.WpfLayer(key,true);Refresh(); };box.Unchecked+=(s,e)=> { engine.WpfLayer(key,false);Refresh(); };container.Children.Add(box); }
        void Category(int id,bool enabled) { if(rebuilding||engine.WpfPoiStore==null)return;engine.WpfPoiStore.SetCategoryEnabled(id,enabled);engine.WpfScheduleSave();RefreshInteraction(); }
        void FilterAll(bool enabled) { var store=engine.WpfPoiStore;if(store==null)return;store.SetAllEnabled(enabled);engine.WpfScheduleSave();RefreshInteraction(); }
        void FilterSection(string section,bool enabled) { var store=engine.WpfPoiStore;if(store==null||!store.CategoriesBySection.ContainsKey(section))return;foreach(var category in store.CategoriesBySection[section])store.SetCategoryEnabled(category.Id,enabled);engine.WpfScheduleSave();RefreshInteraction(); }
        MapZone lastPresentedTarget;
        internal void Refresh() { RefreshView(null); }
        void RefreshView(WpfMapState openingState,bool prepareMap=false) {
            application.Dispatcher.VerifyAccess();
            if(disposed)return;
            RefreshSetupPreview();
            // The native controller also requests refreshes. Coalesce those with the WPF timer.
            bool targetChanged=!object.ReferenceEquals(lastPresentedTarget,engine.WpfCurrentTarget);
            if(!diagnostic&&!prepareMap&&!targetChanged&&refreshClock.ElapsedMilliseconds-lastRefresh<100)return;
            lastRefresh=refreshClock.ElapsedMilliseconds;
            if(!diagnostic&&tactical.IsVisible) { var foreground=Native.GetForegroundWindow();SyncTacticalFocus(Native.IsGameWindow(foreground),Native.IsOurWindow(foreground)); }
            if(!diagnostic&&TacticalVisible) { var bounds=TacticalBounds();if(bounds!=lastTacticalBounds)ApplyTacticalBounds(bounds); }
            List<MapZone> refreshedZones;
            if(zoneFolder!=null&&zoneFolder.Poll(out refreshedZones))engine.WpfRefreshFolderZones(refreshedZones);
            if(!diagnostic&&!zoneFileActive&&dialogs.Count==0) {
                if(zoneOpenRead!=null&&zoneOpenRead.IsCompleted) {
                    string request=null;try { request=zoneOpenRead.GetAwaiter().GetResult(); }catch(Exception error) { Program.LogException("ZoneOpenQueue",error); }finally { zoneOpenRead=null; }
                    if(request!=null) { ShowZoneFile(request);return; }
                }
                if(zoneOpenRead==null&&DateTime.UtcNow>=nextZoneOpenRead) { nextZoneOpenRead=DateTime.UtcNow.AddSeconds(1);string profile=engine.WpfDataFolder;zoneOpenRead=System.Threading.Tasks.Task.Run(()=>ZoneFileOpen.Take(profile)); }
            }
            if(relayGroups!=null)relayGroups.SynchronizeNames();
            var sharingSessions=(relayGroups==null?new DirectPositions[0]:relayGroups.Sessions).Concat(privatePositions==null?new DirectPositions[0]:new[]{privatePositions}).ToArray();
            if(sharingSessions.Length>0) {
                var sample=engine.WpfSnapshot(false);var markers=OwnSquadPublication().Concat(engine.WpfSharedMarkers()).Take(128).ToArray();bool liveSample=sample.Tracking&&!sample.TrackingPaused,holdSample=sample.Position!=null&&!diagnostic&&Native.GameProcessRunning();
                // Opening the inventory, chat or map, or alt-tabbing, pauses location reads. Keep sharing the
                // last read spot while SCUM is still running so teammates do not see the marker blink out.
                foreach(var session in sharingSessions) { if(holdSample)session.UpdateHolding(sample.Position,sample.Updated);else session.Update(liveSample?sample.Position:null,sample.Updated);session.UpdateMarkers(relayGroups==null?markers:relayGroups.MarkersFor(session,markers)); }
            }
            UpdateSquad();UpdateRoutePreviews();foreach(var peer in playerRoster) { TextBlock label;if(playerStatusLabels.TryGetValue(peer.Id,out label))label.Text=PeerStatus(peer); }
            if(!diagnostic) {
                SyncHudVisibility();
                if(!prepareMap&&!TacticalVisible&&!hud.IsVisible)return;
            }
            bool fullView=prepareMap||diagnostic||TacticalVisible;
            var state=fullView&&openingState!=null?openingState:engine.WpfSnapshot(fullView);
            tacticalLocation.Text=Theme.Caps(state.BunkerCaption??state.Location);tacticalLocation.ToolTip=tacticalLocation.Text;
            targetChanged|=!object.ReferenceEquals(lastPresentedTarget,state.Target);
            lastPresentedTarget=state.Target;
            if(fullView)syncMapChrome(state.Bunker==null && state.Opacity==0,false);
            SyncCameraControls(state);
            var compact=fullView?engine.WpfSnapshot(false):state;
            if(sharingSessions.Length>0) {

                state.Peers=compact.Peers=(privatePositions==null?new PrivatePositions.Peer[0]:privatePositions.Snapshot()).Concat(relayGroups==null?new PrivatePositions.Peer[0]:relayGroups.Roster(true)).Where(peer=>(PlayerVisibility(peer.Id)&1)!=0).ToArray();
                state.SharedMarkers=compact.SharedMarkers=squad.Receive((privatePositions==null?new SharedMapMarker[0]:privatePositions.SnapshotMarkers()).Concat(relayGroups==null?new SharedMapMarker[0]:relayGroups.Markers()).Where(PlayerMarkerVisible)).Concat(squad.Local().Select(m=> { m.Owner=S("PrivateGroupSurvivor");return m; })).ToArray();
            }
            else state.SharedMarkers=compact.SharedMarkers=squad.Local();
            state.LocalPeerId=compact.LocalPeerId=sharingSessions.Select(session=>session.LocalId).FirstOrDefault();
            AddRouteState(state,compact);
            deathRemovalButton.IsEnabled=state.Saved.Any(marker=>marker.IsDeathMarker);if(chromeLanguage!=Localization.CurrentCode) { chromeLanguage=Localization.CurrentCode;RefreshLocalizedChrome(tactical);if(settingsWindow!=null)RefreshLocalizedChrome(settingsWindow); }
            if(fullView) { string key=VisualIdentity(state);if(key!=tacticalVisualIdentity) { tacticalVisualIdentity=key;tacticalMap.PublishState(state); } }
            if(diagnostic||hud.IsVisible) { string key=VisualIdentity(compact);if(key!=hudVisualIdentity) { hudVisualIdentity=key;hudMap.State=compact;hudMap.InvalidateVisual(); } }
            hud.Width=compact.HudWidth;hud.Height=compact.HudHeight+(compact.Status?60:0);
            hudLayout.RowDefinitions[0].Height=new GridLength(compact.StatusAbove&&compact.Status?60:compact.HudHeight);
            hudLayout.RowDefinitions[1].Height=new GridLength(compact.StatusAbove&&compact.Status?compact.HudHeight:compact.Status?60:0);
            Grid.SetRow(hudMap,compact.StatusAbove&&compact.Status?1:0);Grid.SetRow(hudFrame,Grid.GetRow(hudMap));SyncHudFrame(compact.Shape=="Circle",compact.Opacity);Grid.SetRow(hudStatus,compact.StatusAbove&&compact.Status?0:1);hudStatus.Visibility=compact.Status?Visibility.Visible:Visibility.Collapsed;
StyleFollow();
            if(zoomResetButton!=null)zoomResetButton.Content=state.Zoom.ToString("0.0",CultureInfo.CurrentCulture)+"×";
            targetCard.Visibility=state.Target==null?Visibility.Collapsed:Visibility.Visible;
            if(hudRoute.Foreground==null||((SolidColorBrush)hudRoute.Foreground).Color!=Color.FromArgb((byte)(state.RouteColor>>24),(byte)(state.RouteColor>>16),(byte)(state.RouteColor>>8),(byte)state.RouteColor))hudRoute.Foreground=new SolidColorBrush(Color.FromArgb((byte)(state.RouteColor>>24),(byte)(state.RouteColor>>16),(byte)(state.RouteColor>>8),(byte)state.RouteColor));
            targetTitle.Text=state.Target==null?"":Theme.Caps(state.Target.DisplayName);targetTitle.ToolTip=state.Target==null?null:state.Target.DisplayName;targetDetail.Text=state.RouteSummary;
            addTargetStop.Visibility=state.Target!=null&&!state.SquadNavigating?Visibility.Visible:Visibility.Collapsed;addTargetStop.IsEnabled=state.LocalRoute.Length<SquadState.MaximumStops;
            routePreviewCard.Visibility=state.LocalRoute.Length>0&&state.ActiveSquadRoute==null?Visibility.Visible:Visibility.Collapsed;
            nativeLocationCard.Visibility=fullView && state.Bunker==null && state.Opacity==0 &&
                targetCard.Visibility!=Visibility.Visible && routePreviewCard.Visibility!=Visibility.Visible?Visibility.Visible:Visibility.Collapsed;
            routePreviewTitle.Text=Localization.Get("OwnRoute")+" · "+Localization.Get(squad.Shared?"SquadRoute":"SoloRoute")+" · "+state.LocalRoute.Length+" / 16";routePreviewTitle.Foreground=squad.Shared?WpfFieldTheme.Cyan:WpfFieldTheme.Amber;routePreviewDetail.Text=state.OwnRouteSummary??"";
            string sector=compact.Position==null?"":DestinationSearch.Sector(MapWindow.ToMap(compact.Position));string locationLabel=compact.Location;
            if(sector.Length>0&&locationLabel.StartsWith(sector+" - ",StringComparison.Ordinal))locationLabel=locationLabel.Substring(sector.Length+3);
            hudSector.Text=sector;((FrameworkElement)hudSector.Parent).Visibility=sector.Length==0?Visibility.Collapsed:Visibility.Visible;hudSector.Visibility=((FrameworkElement)hudSector.Parent).Visibility;hudLocation.Text=locationLabel;hudLocation.ToolTip=compact.Location;
            if(targetChanged||refreshClock.ElapsedMilliseconds-lastRouteTextRefresh>=500||state.Target==null||compact.TrackingPaused) {
                lastRouteTextRefresh=refreshClock.ElapsedMilliseconds;
                hudRoute.Text=compact.TrackingPaused?compact.TrackingStatus:state.Target==null?"": "→ "+state.Target.DisplayName+" · "+state.RouteSummary;
            }
            SyncHudTravel(state,compact);
            if(fullView)SyncTacticalChrome(state,compact);
            string identity=(state.Bunker==null?"world":state.Bunker.BunkerId)+"|"+(state.Store==null?"":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(state.Store).ToString())+"|"+state.Grid+state.Zones+state.Waypoints+state.History+state.Labels+Localization.Current+"|"+state.Saved.Length+"|"+state.Saved.Count(m=>m.IsDeathMarker)+"|"+state.MapKey+"|"+state.Bindings.Key(AppControlAction.Search);
            identity+="|"+state.CustomZonesExpanded+"|"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(state.CustomZones);
            identity+="|players:"+(sharingSessions.Length==0?"off":sharingSessions.Any(session=>session.Ready)?"ready":"waiting")+string.Join(";",playerRoster.Select(peer=>peer.Id+":"+peer.Name+":"+(peer.Position!=null)));
            if(fullView&&identity!=filterIdentity) { filterIdentity=identity;rebuilding=true;try { RebuildSidebar(state); }finally { rebuilding=false; } }
            if(fullView)SyncPoiControls(state.Store);
            
        }
        internal void Handle(AppControlAction action) {
            switch(action) {
                case AppControlAction.PingDanger:SendPing(0);break;case AppControlAction.PingLoot:SendPing(1);break;case AppControlAction.PingPickup:SendPing(2);break;
                case AppControlAction.Settings:ShowSettings();break;case AppControlAction.Search:ShowSearch();break;
                case AppControlAction.ToggleOverlay:ToggleHud();break;case AppControlAction.ToggleCrosshair:ToggleCrosshair();break;
                case AppControlAction.ToggleScreenFilter:ToggleScreenFilter();break;case AppControlAction.CycleScreenFilter:CycleScreenFilter();break;
                case AppControlAction.AddWaypoint:ShowAddWaypoint();break;case AppControlAction.ClearWaypoint:ClearMapRoute();break;
                case AppControlAction.ZoomIn:QueueZoom(1,TacticalVisible);break;case AppControlAction.ZoomOut:QueueZoom(-1,TacticalVisible);break;
                case AppControlAction.StartupGuide:ShowOnboarding();break;case AppControlAction.ZoneEditor:ShowZoneWizard();break;default:engine.WpfNativeTool(action);break;
            }
            RefreshInteraction();
        }
        bool exitRequested;
        internal void RequestExit() {
            if(exitRequested)return;exitRequested=true;
            // Native admin windows treat user Close as a request to hide. Mark
            // application shutdown first so they cannot cancel or reopen.
            engine.WpfPrepareExit();
            // Finish the tray's native menu callback before ending WPF's loop.
            application.Dispatcher.BeginInvoke(new Action(()=>application.Shutdown()));
        }
        internal bool IsSettingsToggleKey(int key,int scan) { return settingsToggleOpen&&engine.WpfSettingsKeyMatches(key,scan); }
        void UpdateSettingsToggle() { settingsToggleOpen=settingsWindow!=null&&settingsWindow.IsVisible&&dialogs.Count==1&&dialogs[0]==settingsWindow;RefreshInputOwnership(); }
        internal void ToggleTactical(bool gameKey=false) {
            if(tactical.IsVisible) {
                bool forward=gameKey&&new WindowInteropHelper(tactical).Handle==Native.GetForegroundWindow();
                tactical.Hide();tacticalMap.ClearHover();engine.WpfTactical(false);SyncHudVisibility();
                if(forward&&!diagnostic) { var binding=engine.WpfSnapshot(false);Native.ReturnMapKey(gameMapWindow,binding.MapKey,binding.MapScan); }
            } else {
                gameMapWindow=diagnostic?IntPtr.Zero:Native.AdminGameWindow();
                hud.Hide();engine.WpfTactical(true);engine.WpfFollow();
                var openingState=engine.WpfSnapshot(true);
                syncMapChrome(openingState.Bunker==null && openingState.Opacity==0,true);
                FitTacticalToMonitor();tactical.Show();lastRefresh=-1000;
                RefreshView(openingState);return;
            }
            Refresh();
        }

        internal bool OwnsForeground(IntPtr handle) {
            // Also called on the keyboard-hook thread. Only read published values.
            if(handle==IntPtr.Zero||inputEditingText)return false;
            return handle==System.Threading.Interlocked.CompareExchange(ref inputTacticalHandle,IntPtr.Zero,IntPtr.Zero)||Array.IndexOf(inputDialogHandles,handle)>=0;
        }
        internal static bool EditingText(IInputElement focus) { return focus is TextBox || focus is PasswordBox || focus is ComboBox; }
        void FocusInteractive(Window window) {
            if(diagnostic||!window.IsVisible)return;
            var handle=new WindowInteropHelper(window).Handle;
            var focused=Keyboard.FocusedElement as DependencyObject;
            window.Cursor=Cursors.Arrow;Native.ForceForeground(handle);window.Activate();
            if(focused!=null&&System.Windows.Window.GetWindow(focused)==window)Keyboard.Focus((IInputElement)focused);
            else { window.Focus();window.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); }
            if(Native.GetForegroundWindow()==handle)ClipCursor(IntPtr.Zero);
        }
        void RestoreGameplayFocus() {
            if(diagnostic||gameMapWindow==IntPtr.Zero)return;
            if(Native.IsOurWindow(Native.GetForegroundWindow())&&Native.IsGameWindow(gameMapWindow))Native.SetForegroundWindow(gameMapWindow);
        }
        void Modal(Window window) {
            // A dialog opened from the HUD has no topmost tactical owner to keep it above SCUM.
            window.Topmost=true;window.ShowActivated=!diagnostic;
            if(diagnostic) { window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-32000;window.Top=-32000; }
            if(dialogs.Count>0)window.Owner=dialogs[dialogs.Count-1];else if(setupWindow!=null)window.Owner=setupWindow;else if(tactical.IsVisible)window.Owner=tactical;
            if(window.Owner!=null&&!diagnostic&&window.WindowStartupLocation!=WindowStartupLocation.Manual)window.WindowStartupLocation=WindowStartupLocation.CenterOwner;
            window.Loaded+=(s,e)=>FocusInteractive(window);
            window.ContentRendered+=(s,e)=>UpdateSettingsToggle();
            dialogs.Add(window);engine.WpfModal(true);
            UpdateSettingsToggle();
            var previousForeground=Native.GetForegroundWindow();
            try { window.ShowDialog(); }finally {
                dialogs.Remove(window);UpdateSettingsToggle();SyncModalInput();engine.WpfSave();
                // A theme picked in Settings is applied once no dialog depends on the current windows.
                if(dialogs.Count==0&&pendingTheme!=null) { var theme=pendingTheme;pendingTheme=null;bool reopen=window==settingsWindow;ApplyTheme(theme);if(reopen)application.Dispatcher.BeginInvoke(new Action(()=>ShowSettings(2))); }
                Refresh();
                if(dialogs.Count>0)FocusInteractive(dialogs[dialogs.Count-1]);else if(setupWindow!=null) {if(setupHasFocus)FocusInteractive(setupWindow);}else if(tactical.IsVisible)RestoreGameplayFocus();
                else if(!diagnostic&&Native.IsGameWindow(previousForeground))Native.SetForegroundWindow(previousForeground);
            }
        }
        void ShowSearch() { Modal(BuildSearch()); }
        // Command-palette search: one large field, live results with icons, actions pinned below.
        Window BuildSearch() {
            var t=WpfFieldTheme.Current;var window=Window(Localization.Get("SearchTitle"),600,520);window.ResizeMode=ResizeMode.NoResize;
            var layout=new DockPanel { Margin=new Thickness(22,20,22,18) };
            var field=new DockPanel();var glass=WpfIcons.Create("search",20,WpfFieldTheme.Muted);glass.Margin=new Thickness(14,0,10,0);DockPanel.SetDock(glass,Dock.Left);field.Children.Add(glass);
            var escape=WpfKit.Quiet("Esc",()=>window.Close());escape.MinHeight=28;escape.Padding=new Thickness(8,2,8,2);escape.FontSize=12;escape.Margin=new Thickness(6,0,8,0);escape.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(escape,Dock.Right);field.Children.Add(escape);
            var input=new TextBox { MaxLength=120,Text=diagnostic?"grid D4":"",FontSize=18,BorderThickness=new Thickness(0),Background=Brushes.Transparent,MinHeight=44,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(0) };field.Children.Add(input);
            var fieldFrame=new Border { Child=field,Background=WpfFieldTheme.Current.Brush(t.Input),BorderBrush=WpfFieldTheme.Line2,BorderThickness=new Thickness(t.Uppercase?0:1),CornerRadius=new CornerRadius(Math.Min(14,t.ControlRadius+2)),Padding=new Thickness(0,4,0,4) };
            if(t.Uppercase)fieldFrame.BorderThickness=new Thickness(0,0,0,2);
            input.GotKeyboardFocus+=(s,e)=>fieldFrame.BorderBrush=WpfFieldTheme.Accent;input.LostKeyboardFocus+=(s,e)=>fieldFrame.BorderBrush=WpfFieldTheme.Line2;
            DockPanel.SetDock(fieldFrame,Dock.Top);layout.Children.Add(fieldFrame);
            var hint=WpfKit.Help(Localization.Get("SearchHint"),12);hint.Margin=new Thickness(2,10,0,12);DockPanel.SetDock(hint,Dock.Top);layout.Children.Add(hint);
            var footer=new StackPanel { Margin=new Thickness(0,12,0,0) };var count=WpfKit.Mono("",12,WpfFieldTheme.Muted);count.Margin=new Thickness(2,0,0,8);var list=new ListBox { BorderThickness=new Thickness(0),Background=Brushes.Transparent };
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            Action choose=()=> { var item=list.SelectedItem as ListBoxItem;if(item==null)return;NavigateDestination((MapZone)item.Tag);window.Close(); };
            var go=WpfKit.Primary(Localization.Get("MapGpsDestination"),choose);go.Tag="search-navigate";go.ToolTip=Localization.Get("MapGpsSavedHelp");
            var clear=WpfKit.Quiet(Localization.Get("WpfClearRoute"),()=> { ClearMapRoute();window.Close(); });clear.Tag="search-clear-route";clear.IsEnabled=engine.WpfSnapshot(true).Target!=null||squad.Stops.Length>0||activeStops.Length>0||followingPeer!=null;
            var addStop=WpfKit.Secondary(S("SquadAddStop"),()=> { var item=list.SelectedItem as ListBoxItem;if(item==null||squad.Stops.Length>=SquadState.MaximumStops)return;AddSquadDestination((MapZone)item.Tag);window.Close(); });addStop.Tag="search-add-stop";addStop.ToolTip=S("MapGpsSavedHelp");
            var actions=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right };foreach(var action in new[]{clear,addStop,go}) { action.Margin=new Thickness(8,6,0,0);actions.Children.Add(action); }
            footer.Children.Add(count);footer.Children.Add(actions);
            var footerFrame=new Border { Child=footer,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0) };DockPanel.SetDock(footerFrame,Dock.Bottom);layout.Children.Add(footerFrame);layout.Children.Add(list);
            Action populate=()=> {
                list.Items.Clear();var results=engine.WpfSearch(input.Text);var state=engine.WpfSnapshot(true);
                foreach(var result in results) {
                    bool grid=result.Name.StartsWith("Grid ");
                    string detail=grid?Localization.Get("SectorCentre").Trim().Trim('/','(',')','\uFF08','\uFF09').Trim():DestinationSearch.Sector(result.Centroid)+" · "+(result.Subtitle??"");
                    if(state.Position!=null&&!grid)detail+=" · "+Localization.Get("FieldDirect")+": "+DestinationSearch.Distance(DestinationSearch.Metres(MapWindow.ToMap(state.Position),result.Centroid));
                    var row=WpfKit.Row(WpfKit.IconTile(grid?"grid-3x3":"map-pin",null,34),result.DisplayName,detail);row.Margin=new Thickness(4,2,4,2);
                    var item=new ListBoxItem { Content=row,Tag=result,ToolTip=result.DisplayName+" · "+detail,Padding=new Thickness(8,8,8,8),HorizontalContentAlignment=HorizontalAlignment.Stretch };
                    System.Windows.Automation.AutomationProperties.SetName(item,result.DisplayName+" · "+detail);list.Items.Add(item);
                }
                if(list.Items.Count>0)list.SelectedIndex=0;go.IsEnabled=results.Count>0;count.Text=results.Count==0?Localization.Get("NoMatches"):Localization.T("SearchResultCount",results.Count);
            };
            input.Tag="search-query";list.Tag="search-results";
            System.Windows.Automation.AutomationProperties.SetName(input,Localization.Get("SearchTitle"));
            System.Windows.Automation.AutomationProperties.SetHelpText(input,Localization.Get("SearchHint"));
            System.Windows.Automation.AutomationProperties.SetName(list,Localization.Get("SearchTitle"));
            list.SelectionChanged+=(s,e)=> { go.IsEnabled=list.SelectedItem!=null;addStop.IsEnabled=list.SelectedItem!=null&&squad.Stops.Length<SquadState.MaximumStops; };
            input.TextChanged+=(s,e)=>populate();input.PreviewKeyDown+=(s,e)=> {
                if(e.Key==Key.Down||e.Key==Key.Up) {
                    if(list.Items.Count>0) { list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,list.SelectedIndex+(e.Key==Key.Down?1:-1)));list.ScrollIntoView(list.SelectedItem); }
                    e.Handled=true;
                }else if(e.Key==Key.Enter) { choose();e.Handled=true; }
            };list.MouseDoubleClick+=(s,e)=>choose();list.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter) { choose();e.Handled=true; } };
            window.Content=layout;window.Loaded+=(s,e)=>input.Focus();populate();return window;
        }
        void ShowSettings() { ShowSettings(0); }
        void ShowSettings(int initial) {
            if(settingsWindow!=null) { if(settingsToggleOpen)settingsWindow.Close();else if(dialogs.Count>0)FocusInteractive(dialogs[dialogs.Count-1]);return; }
            settingsWindow=BuildSettings(initial);
            try { Modal(settingsWindow); }finally { settingsWindow=null;settingsToggleOpen=false; }
        }
        Window BuildMapPlacement() {
            if(!diagnostic)gameMapWindow=Native.AdminGameWindow();
            var window=Window(Localization.Get("MapPlacement"),600,680);window.Tag="map-placement-dialog";
            var dialog=new WpfKit.Dialog(window,Localization.Get("MapPlacement"),Localization.Get("MapPlacementHelp"));dialog.AddClose(Localization.Get("Close"));
            var mode=new ComboBox { ItemsSource=new[]{Localization.Get("MapFitGame"),Localization.Get("MapManual")},SelectedIndex=engine.WpfManualMap?1:0,Tag="map-placement-mode",MinHeight=38 };
            dialog.Add(WpfKit.Section(Localization.Get("MapPlacement"),mode));
            var fields=new UniformGrid { Columns=2 };var inputs=new TextBox[4];
            string[] labels={"X (px)","Y (px)",Localization.Get("MapWidth"),Localization.Get("MapHeight")};
            for(int i=0;i<4;i++) { inputs[i]=WpfKit.Input(null,"map-placement-"+i,7);inputs[i].FontFamily=WpfFieldTheme.MonoFont;System.Windows.Automation.AutomationProperties.SetName(inputs[i],labels[i]);var cell=WpfKit.Field(labels[i],inputs[i]);((FrameworkElement)cell).Margin=new Thickness(0,4,i%2==0?12:0,8);fields.Children.Add(cell); }
            Action<System.Drawing.Rectangle> populate=b=> { int[] values={b.X,b.Y,b.Width,b.Height};for(int i=0;i<4;i++)inputs[i].Text=values[i].ToString(CultureInfo.InvariantCulture); };
            populate(TacticalBounds());var error=WpfFieldTheme.Text("",13,WpfFieldTheme.Warn);error.TextWrapping=TextWrapping.Wrap;error.TextTrimming=TextTrimming.None;
            var nudges=BuildPixelNudges((dx,dy)=> { int x,y;if(!int.TryParse(inputs[0].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out x)||!int.TryParse(inputs[1].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out y)||x+dx < -100000||x+dx>100000||y+dy < -100000||y+dy>100000) { error.Text=Localization.Get("MapBoundsInvalid");return; }inputs[0].Text=(x+dx).ToString(CultureInfo.InvariantCulture);inputs[1].Text=(y+dy).ToString(CultureInfo.InvariantCulture);error.Text=""; });
            var manual=new DockPanel();nudges.Margin=new Thickness(20,0,0,0);DockPanel.SetDock(nudges,Dock.Right);manual.Children.Add(nudges);manual.Children.Add(fields);
            var manualBody=new StackPanel();manualBody.Children.Add(manual);manualBody.Children.Add(error);
            dialog.Add(WpfKit.Section(Localization.Get("MapManual"),manualBody));
            var move=WpfKit.Secondary(Localization.Get("MapMoveResize"),()=> { Modal(BuildMapMoveEditor());if(engine.WpfManualMap)populate(engine.WpfMapBounds); },"move");move.Tag="map-placement-drag";
            var monitor=WpfKit.Secondary(Localization.Get("MapFitMonitor"),()=> { mode.SelectedIndex=1;populate(LastMonitorBounds); },"monitor");monitor.Tag="map-placement-monitor";
            dialog.Add(WpfKit.Actions(move,monitor));
            Action enabled=()=> { fields.IsEnabled=nudges.IsEnabled=mode.SelectedIndex==1;move.IsEnabled=mode.SelectedIndex==1;error.Text=""; };mode.SelectionChanged+=(s,e)=>enabled();enabled();
            var apply=WpfKit.Primary(Localization.Get("Done"),()=> {
                if(mode.SelectedIndex==0) { engine.WpfPlaceMap(false,engine.WpfMapBounds);FitTacticalToMonitor();window.Close();return; }
                int x,y,w,h;
                if(!int.TryParse(inputs[0].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out x)||!int.TryParse(inputs[1].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out y)||!int.TryParse(inputs[2].Text,out w)||!int.TryParse(inputs[3].Text,out h)||x < -100000||x>100000||y < -100000||y>100000||w<640||w>32768||h<420||h>32768) { error.Text=Localization.Get("MapBoundsInvalid");return; }
                engine.WpfPlaceMap(true,RecoverMapBounds(new System.Drawing.Rectangle(x,y,w,h),System.Windows.Forms.Screen.AllScreens.Select(s=>s.Bounds).ToArray()));FitTacticalToMonitor();window.Close();
            },"check");apply.Tag="map-placement-save";dialog.AddAction(apply);
            return window;
        }
        Window BuildMapMoveEditor() {
            var bounds=TacticalBounds();var window=Window(Localization.Get("MapMoveResize"),bounds.Width,bounds.Height);window.Tag="map-placement-preview";window.Topmost=true;
            window.WindowStartupLocation=WindowStartupLocation.Manual;window.MinWidth=640;window.MinHeight=420;
            // An accent frame shows the exact edge of the map; the header is the drag handle.
            var frame=new Border { BorderBrush=WpfFieldTheme.Accent,BorderThickness=new Thickness(2),Background=WpfFieldTheme.Ground };var layout=new DockPanel();frame.Child=layout;
            var head=new DockPanel { LastChildFill=true };var grip=new Border { Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(16,11,16,11),Cursor=Cursors.SizeAll,Child=head };
            var size=WpfKit.Mono("",12,WpfFieldTheme.Muted);size.VerticalAlignment=VerticalAlignment.Center;size.Margin=new Thickness(16,0,0,0);DockPanel.SetDock(size,Dock.Right);head.Children.Add(size);
            var icon=WpfIcons.Create("move",17,WpfFieldTheme.Accent);icon.Margin=new Thickness(0,0,12,0);icon.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(icon,Dock.Left);head.Children.Add(icon);
            var title=WpfKit.Strong(Localization.Get("MapPlacementDrag"),15);title.VerticalAlignment=VerticalAlignment.Center;head.Children.Add(title);
            DockPanel.SetDock(grip,Dock.Top);layout.Children.Add(grip);grip.MouseLeftButtonDown+=(s,e)=>window.DragMove();
            var stage=new Grid();var preview=new WpfMapSurface { BaseMap=geography,Terrain=terrain,State=engine.WpfSnapshot(true),IsHitTestVisible=false };stage.Children.Add(preview);
            // Controls float over the preview in one card so the whole map stays visible.
            var nudges=BuildPixelNudges((dx,dy)=>NudgeWindow(window,dx,dy));nudges.Margin=new Thickness(0);
            var done=WpfKit.Primary(Localization.Get("Done"),()=> { var actual=Native.ReadWindowBounds(new WindowInteropHelper(window).Handle);if(actual.Width>=640&&actual.Height>=420) { engine.WpfPlaceMap(true,RecoverMapBounds(actual,System.Windows.Forms.Screen.AllScreens.Select(s=>s.Bounds).ToArray()));FitTacticalToMonitor(); }window.Close(); },"check");done.Tag="map-placement-preview-save";done.Margin=new Thickness(0,12,0,0);done.HorizontalAlignment=HorizontalAlignment.Stretch;
            var hint=WpfKit.Mono("Esc · "+Localization.Get("Cancel"),11,WpfFieldTheme.Muted);hint.Margin=new Thickness(0,8,0,0);hint.HorizontalAlignment=HorizontalAlignment.Center;
            var controls=new StackPanel();controls.Children.Add(nudges);controls.Children.Add(done);controls.Children.Add(hint);
            var card=WpfKit.Card(controls,new Thickness(14));card.HorizontalAlignment=HorizontalAlignment.Right;card.VerticalAlignment=VerticalAlignment.Bottom;card.Margin=new Thickness(16);card.Width=172;stage.Children.Add(card);
            layout.Children.Add(stage);window.Content=frame;
            Action measure=()=> { try { var actual=Native.ReadWindowBounds(new WindowInteropHelper(window).Handle);size.Text=actual.Width.ToString(CultureInfo.InvariantCulture)+" × "+actual.Height.ToString(CultureInfo.InvariantCulture)+" px"; }catch { size.Text=""; } };
            window.SizeChanged+=(s,e)=>measure();window.LocationChanged+=(s,e)=>measure();
            window.SourceInitialized+=(s,e)=> { var handle=new WindowInteropHelper(window).Handle;var source=HwndSource.FromHwnd(handle);var scale=source.CompositionTarget.TransformFromDevice.Transform(new Point(bounds.Width,bounds.Height));window.MinWidth=640*scale.X/bounds.Width;window.MinHeight=420*scale.Y/bounds.Height;window.Width=scale.X;window.Height=scale.Y;SetWindowPos(handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0010);measure(); };
            return window;
        }
        void ShowMoveHud() {
            hudMoving=true;hud.Hide();
            try { Modal(BuildHudMoveEditor()); }finally { hudMoving=false;SyncHudVisibility(true);Refresh(); }
        }
        Window BuildHudMoveEditor() {
            var state=engine.WpfSnapshot(false);const double head=28,tools=156;
            var window=Window(Localization.Get("WpfMoveMinimap"),Math.Max(state.HudWidth,300),state.HudHeight+(state.Status?60:0)+head+tools);window.Tag="hud-move";
            window.MinWidth=window.Width;window.MinHeight=window.Height;window.ResizeMode=ResizeMode.NoResize;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=hud.Left;window.Top=hud.Top-head;
            var layout=new DockPanel { Background=WpfFieldTheme.Ground };
            // The 28 px handle sits exactly above the HUD, so Done can subtract it again.
            var grip=new DockPanel();var gripIcon=WpfIcons.Create("move",13,WpfFieldTheme.AccentInk);gripIcon.Margin=new Thickness(10,0,8,0);gripIcon.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(gripIcon,Dock.Left);grip.Children.Add(gripIcon);
            var gripText=WpfFieldTheme.Text(Localization.Get("WpfDragMinimap"),12,WpfFieldTheme.AccentInk,true);gripText.VerticalAlignment=VerticalAlignment.Center;grip.Children.Add(gripText);
            var handle=new Border { Background=WpfFieldTheme.Accent,Height=head,Cursor=Cursors.SizeAll,Child=grip };handle.MouseLeftButtonDown+=(s,e)=> { if(e.ButtonState==MouseButtonState.Pressed)window.DragMove(); };DockPanel.SetDock(handle,Dock.Top);layout.Children.Add(handle);
            var done=WpfKit.Primary(Localization.Get("Done"),()=> { var source=HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());var location=source.CompositionTarget.TransformToDevice.Transform(new Point(window.Left,window.Top+head));engine.WpfMoveHud((int)Math.Round(location.X),(int)Math.Round(location.Y));window.Close(); },"check");
            done.HorizontalAlignment=HorizontalAlignment.Stretch;
            var nudges=BuildPixelNudges((dx,dy)=>NudgeWindow(window,dx,dy),32);nudges.Margin=new Thickness(0);nudges.VerticalAlignment=VerticalAlignment.Center;
            var hint=WpfKit.Mono("Esc · "+Localization.Get("Cancel"),11,WpfFieldTheme.Muted);hint.HorizontalAlignment=HorizontalAlignment.Center;hint.Margin=new Thickness(0,8,0,0);
            var finish=new StackPanel { VerticalAlignment=VerticalAlignment.Center,MinWidth=112,Margin=new Thickness(12,0,0,0) };finish.Children.Add(done);finish.Children.Add(hint);
            var bar=new DockPanel { Margin=new Thickness(14,10,14,10),LastChildFill=false };DockPanel.SetDock(finish,Dock.Right);bar.Children.Add(finish);DockPanel.SetDock(nudges,Dock.Left);bar.Children.Add(nudges);
            var tray=new Border { Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Height=tools,Child=bar };DockPanel.SetDock(tray,Dock.Bottom);layout.Children.Add(tray);
            var preview=new Grid { Width=state.HudWidth,HorizontalAlignment=HorizontalAlignment.Left };preview.RowDefinitions.Add(new RowDefinition { Height=new GridLength(state.StatusAbove&&state.Status?60:state.HudHeight) });preview.RowDefinitions.Add(new RowDefinition { Height=new GridLength(state.StatusAbove&&state.Status?state.HudHeight:state.Status?60:0) });var map=new WpfMapSurface { Compact=true,BaseMap=geography,Terrain=terrain,State=state };Grid.SetRow(map,state.StatusAbove&&state.Status?1:0);preview.Children.Add(map);var info=WpfFieldTheme.Card(WpfFieldTheme.Text(state.Location,14),new Thickness(12));Grid.SetRow(info,state.StatusAbove&&state.Status?0:1);info.Visibility=state.Status?Visibility.Visible:Visibility.Collapsed;preview.Children.Add(info);preview.Cursor=Cursors.SizeAll;preview.MouseLeftButtonDown+=(s,e)=> { if(e.ButtonState==MouseButtonState.Pressed)window.DragMove(); };layout.Children.Add(preview);window.Content=layout;return window;
        }
        void ShowPrivatePositions() { Modal(BuildRelayGroups()); }
        // One-pixel nudges as a small direction pad: ↑ on top, ← → beside the centre, ↓ below.
        static StackPanel BuildPixelNudges(Action<int,int> move,double cell=40) {
            var panel=new StackPanel { Margin=new Thickness(4,8,4,8),Tag="pixel-nudges",HorizontalAlignment=HorizontalAlignment.Left };panel.Children.Add(WpfKit.Label(Localization.Get("PixelNudge")));
            var pad=new Grid { HorizontalAlignment=HorizontalAlignment.Left };for(int i=0;i<3;i++) { pad.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(cell) });pad.RowDefinitions.Add(new RowDefinition { Height=new GridLength(cell) }); }
            int[] xs={-1,1,0,0},ys={0,0,-1,1};string[] icons={"chevron-left","chevron-right","chevron-up","chevron-down"};int[] columns={0,2,1,1},rows={1,1,0,2};
            for(int i=0;i<4;i++) {
                int dx=xs[i],dy=ys[i];var button=WpfFieldTheme.Button("",()=>move(dx,dy));button.MinWidth=0;button.MinHeight=0;button.Width=cell-4;button.Height=cell-4;button.Padding=new Thickness(0);button.Margin=new Thickness(2);button.Tag="nudge:"+dx+":"+dy;
                button.Content=WpfIcons.Create(icons[i],16,WpfFieldTheme.Ink);System.Windows.Automation.AutomationProperties.SetName(button,Localization.Get("PixelNudge")+" · "+(dx!=0?"X "+dx:"Y "+dy));
                Grid.SetColumn(button,columns[i]);Grid.SetRow(button,rows[i]);pad.Children.Add(button);
            }
            var unit=WpfKit.Mono("1 px",11,WpfFieldTheme.Muted);unit.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(unit,1);Grid.SetRow(unit,1);pad.Children.Add(unit);
            panel.Children.Add(pad);return panel;
        }
        static void NudgeWindow(Window window,int dx,int dy) {
            var handle=new WindowInteropHelper(window).EnsureHandle();var bounds=Native.ReadWindowBounds(handle);
            SetWindowPos(handle,IntPtr.Zero,bounds.X+dx,bounds.Y+dy,0,0,0x0015);
        }
        static string GroupState(RelayGroupStore.View group) {
            if(group.Pending!=""||group.Session!=null&&group.Session.ReconnectBlocked)return Localization.Get("GroupUnavailable");
            if(group.Session==null)return Localization.Get("SquadOffline");
            if(group.Session.Ready)return Localization.Get("PlayerConnected")+" · "+group.Session.Connections;
            return Localization.Get("PrivatePositionConnecting");
        }
        static string GroupFailure(Exception error) {
            var aggregate=error as AggregateException;if(aggregate!=null)error=aggregate.Flatten().InnerExceptions[0];
            return Localization.Get(error is RelayStorageException||error is UnauthorizedAccessException?"LocalSaveFailed":error is RelayGroupLimitException?"GroupLimit":error is ArgumentException||error is FormatException?"GroupInputInvalid":"GroupUnavailable");
        }
        void RunColourChoice(WrapPanel palette,string seed,TextBlock status) {
            try {
                relayGroups.SetMemberColor(seed);
                foreach(var button in palette.Children.OfType<Button>()) { bool selected=button.Tag as string=="member-colour:"+seed;button.BorderThickness=new Thickness(selected?3:1);string hex=((string)button.Content).Replace("✓ ","");button.Content=(selected?"✓ ":"")+hex; }
                status.Text=Localization.Get("MemberColourHelp");
            }catch(Exception error) { status.Text=GroupFailure(error); }
        }
        static Brush ColourButtonText(Color colour) {
            Func<byte,double> linear=value=> { double c=value/255.0;return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4); };
            return .2126*linear(colour.R)+.7152*linear(colour.G)+.0722*linear(colour.B)>.179?Brushes.Black:Brushes.White;
        }
        static Brush GroupInk(RelayGroupStore.View group) {
            if(group.Pending!=""||group.Session!=null&&group.Session.ReconnectBlocked)return WpfFieldTheme.Warn;
            if(group.Session==null)return WpfFieldTheme.Dim;
            return group.Session.Ready?WpfFieldTheme.Success:WpfFieldTheme.MapAmber;
        }
        Window BuildRelayGroups() {
            var window=Window(Localization.Get("PrivatePositions"),700,800);window.WindowStyle=WindowStyle.SingleBorderWindow;
            var dialog=new WpfKit.Dialog(window,Localization.Get("PrivatePositions"),Localization.Get("PrivateGroupHelp"));dialog.AddClose(Localization.Get("Close"));
            var status=WpfFieldTheme.Text("",13,WpfFieldTheme.Amber);status.TextWrapping=TextWrapping.Wrap;status.TextTrimming=TextTrimming.None;status.Margin=new Thickness(0,10,0,0);
            try { if(relayGroups==null)relayGroups=new RelayGroupStore(engine.WpfDataFolder); }
            catch { dialog.Add(WpfKit.Note(Localization.Get("LocalSaveFailed"),"warn"));dialog.AddAction(WpfKit.Primary(Localization.Get("Done"),()=>window.Close(),"check"));return window; }
            var form=new StackPanel();dialog.Add(form);
            // You: the name and colour teammates see.
            var name=WpfKit.Input(relayGroups.DisplayName,"group-name",32);
            var nameHelp=WpfKit.Help(Localization.Get("NameSavedHelp"),12);nameHelp.Visibility=Visibility.Collapsed;nameHelp.Margin=new Thickness(0,6,0,0);
            var saveName=WpfKit.Secondary(Localization.Get("GroupSaveName"),()=> { try { relayGroups.SetName(name.Text);nameHelp.Visibility=Visibility.Visible;status.Text=""; }catch(Exception error) { status.Text=GroupFailure(error);name.Focus(); } },"check");saveName.Tag="group-save-name";saveName.Margin=new Thickness(8,0,0,0);
            var nameRow=new DockPanel();DockPanel.SetDock(saveName,Dock.Right);nameRow.Children.Add(saveName);nameRow.Children.Add(name);
            var you=new StackPanel();var nameField=(FrameworkElement)WpfKit.Field(Localization.Get("PrivatePositionName"),nameRow);nameField.Margin=new Thickness(0,0,0,0);you.Children.Add(nameField);you.Children.Add(nameHelp);
            var palette=new WrapPanel { MaxWidth=8*44,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,4) };
            for(int i=0;i<16;i++) {
                string seed=WpfMapSurface.PaletteSeed(i);var ink=(SolidColorBrush)WpfMapSurface.MemberInk(seed+new string('0',24));string hex="#"+ink.Color.R.ToString("X2")+ink.Color.G.ToString("X2")+ink.Color.B.ToString("X2");
                var choice=WpfKit.Swatch((seed==relayGroups.MemberColorSeed?"\u2713 ":"")+hex,ink,ColourButtonText(ink.Color),()=> { RunColourChoice(palette,seed,status);Refresh(); });
                choice.Tag="member-colour:"+seed;choice.ToolTip=hex;System.Windows.Automation.AutomationProperties.SetName(choice,Localization.Get("MemberColour")+" "+hex);palette.Children.Add(choice);
            }
            var colourLabel=WpfKit.Label(Localization.Get("MemberColour"));colourLabel.Margin=new Thickness(0,18,0,0);you.Children.Add(colourLabel);you.Children.Add(palette);you.Children.Add(WpfKit.Help(Localization.Get("MemberColourHelp"),12));
            form.Children.Add(WpfKit.Section(Localization.Get("GroupYouTitle"),you));
            // Join with a code, or start a new group: two cards side by side.
            var code=new PasswordBox { MaxLength=128,MinHeight=38,Padding=new Thickness(10,7,10,7),VerticalContentAlignment=VerticalAlignment.Center };code.Tag="group-code";
            var groupLabel=WpfKit.Input(null,"new-group-label",48);
            System.Windows.Automation.AutomationProperties.SetName(name,Localization.Get("PrivatePositionName"));
            System.Windows.Automation.AutomationProperties.SetName(code,Localization.Get("PrivateGroupCode"));
            System.Windows.Automation.AutomationProperties.SetName(groupLabel,Localization.Get("PrivateGroupNameLabel"));
            bool closed=false,busy=false;var rows=new StackPanel();Action draw=null;
            Action<string> copyCode=value=> { code.Password=value;try { System.Windows.Clipboard.SetText(value);status.Text=Localization.Get("AdminCopied"); }catch { status.Text=Localization.Get("ClipboardFailed"); } };
            Action<Func<RelayMembership>,bool> run=(action,copy)=> {
                if(busy)return;busy=true;form.IsEnabled=false;status.Text=Localization.Get("PrivatePositionConnecting");string display=name.Text;
                System.Threading.Tasks.Task.Run(()=> { relayGroups.SetName(display);return action(); }).ContinueWith(task=> { if(task.IsFaulted)task.Exception.Handle(error=>true);PostUi(()=> {
                    busy=false;if(closed)return;form.IsEnabled=true;
                    if(task.Status==System.Threading.Tasks.TaskStatus.RanToCompletion) {
                        var group=task.Result;status.Text=group==null?"":GroupState(new RelayGroupStore.View(group));
                        if(group!=null&&group.Pending=="") { code.Password=group.Code;if(copy)copyCode(group.Code); }
                        if(group==null)code.Clear();
                    }else status.Text=GroupFailure(task.Exception);draw();Refresh();
                }); });
            };
            var join=WpfKit.Wide(WpfKit.Primary(Localization.Get("PrivateGroupConnect"),()=> { string value=code.Password;run(()=>relayGroups.Join(value),false); },"link"));join.Tag="group-connect";
            var create=WpfKit.Wide(WpfKit.Secondary(Localization.Get("PrivateGroupCreate"),()=> { string label=groupLabel.Text;run(()=>relayGroups.Create(label),true); },"user-plus"));create.Tag="group-create";
            Func<string,string,UIElement,Button,UIElement> entry=(icon,title,field,action)=> {
                var stack=new StackPanel();var head=new DockPanel { Margin=new Thickness(0,0,0,12) };var tile=WpfKit.IconTile(icon,null,32);tile.Margin=new Thickness(0,0,10,0);DockPanel.SetDock(tile,Dock.Left);head.Children.Add(tile);
                var label=WpfKit.Strong(title,15);label.VerticalAlignment=VerticalAlignment.Center;head.Children.Add(label);stack.Children.Add(head);
                var fieldElement=(FrameworkElement)field;fieldElement.Margin=new Thickness(0,0,0,12);stack.Children.Add(field);stack.Children.Add(action);return WpfKit.Card(stack,new Thickness(16));
            };
            var entries=new Grid { Margin=new Thickness(0,18,0,0) };entries.ColumnDefinitions.Add(new ColumnDefinition());entries.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(12) });entries.ColumnDefinitions.Add(new ColumnDefinition());
            var joinCard=entry("link",Localization.Get("GroupJoinTitle"),WpfKit.Field(Localization.Get("PrivateGroupCode"),code),join);var createCard=entry("user-plus",Localization.Get("GroupCreateTitle"),WpfKit.Field(Localization.Get("PrivateGroupNameLabel"),groupLabel),create);
            Grid.SetColumn(createCard,2);entries.Children.Add(joinCard);entries.Children.Add(createCard);form.Children.Add(entries);form.Children.Add(status);
            var count=WpfKit.Badge("0");form.Children.Add(WpfKit.Section(Localization.Get("GroupListTitle"),rows,null,count,false));
            draw=()=> {
                rows.Children.Clear();var views=relayGroups.GroupViews.ToArray();((TextBlock)count.Child).Text=views.Length.ToString(CultureInfo.InvariantCulture);
                if(views.Length==0)rows.Children.Add(WpfKit.Empty(Localization.Get("GroupListEmpty"),"users"));
                foreach(var group in views) {
                    var captured=group.Member;var card=new StackPanel();
                    var head=new DockPanel();var dot=new System.Windows.Shapes.Ellipse { Width=10,Height=10,Fill=GroupInk(group),Margin=new Thickness(2,6,12,0),VerticalAlignment=VerticalAlignment.Top,Tag="group-dot:"+group.Id };DockPanel.SetDock(dot,Dock.Left);head.Children.Add(dot);
                    bool live=group.Session!=null&&group.Session.Error==null;
                    var connect=live?WpfKit.Secondary(Localization.Get("PrivateGroupLeave"),()=> { },null):WpfKit.Primary(Localization.Get("PrivateGroupConnect"),()=> { },null);connect.Click+=(s,e)=> {
                        run(()=> { if(relayGroups.Connected(captured))relayGroups.Leave(captured);else relayGroups.Connect(captured);return captured; },false);
                    };connect.Tag="group-toggle:"+group.Id;connect.IsEnabled=group.Pending!="DELETE";connect.VerticalAlignment=VerticalAlignment.Top;connect.MinWidth=0;DockPanel.SetDock(connect,Dock.Right);head.Children.Add(connect);
                    var titles=new StackPanel();var title=WpfKit.Strong(group.Label+(group.IsOwner?" · "+Localization.Get("PrivateGroupOwner"):""),16);title.Tag="group-title:"+group.Id;titles.Children.Add(title);
                    string state=GroupState(group);var stateText=WpfFieldTheme.Text(state,12,WpfFieldTheme.Muted);stateText.Tag="group-state:"+group.Id;stateText.Margin=new Thickness(0,3,0,0);titles.Children.Add(stateText);head.Children.Add(titles);card.Children.Add(head);
                    var groupName=WpfKit.Input(group.Name,"rename-group:"+group.Id,48);
                    var rename=WpfKit.Quiet(Localization.Get("PrivateGroupRename"),()=> { string label=groupName.Text;run(()=> { relayGroups.Rename(captured,label);return captured; },false); },"pencil");rename.Margin=new Thickness(6,0,0,0);
                    System.Windows.Automation.AutomationProperties.SetName(groupName,Localization.Get("PrivateGroupNameLabel")+" · "+group.Label);
                    var renameRow=new DockPanel { Margin=new Thickness(22,14,0,0) };DockPanel.SetDock(rename,Dock.Right);renameRow.Children.Add(rename);renameRow.Children.Add(groupName);card.Children.Add(renameRow);
                    var buttons=new WrapPanel { Margin=new Thickness(16,8,0,-6) };
                    var copy=WpfKit.Quiet(Localization.Get("PrivateGroupCopy"),()=>copyCode(group.Code),"copy");copy.IsEnabled=group.Pending=="";copy.Tag="group-copy:"+group.Id;buttons.Children.Add(copy);
                    if(group.IsOwner) {
                        var manage=WpfKit.Quiet(Localization.Get("GroupManageMembers"),()=>Modal(BuildGroupMembers(captured)),"users");manage.Tag="group-members:"+group.Id;manage.IsEnabled=group.Pending=="";buttons.Children.Add(manage);
                        var rotate=WpfKit.Quiet(Localization.Get("PrivateGroupRotate"),()=> {
                            if(WpfKit.Ask(window,Localization.T("GroupRotateConfirm",group.Label),Localization.Get("PrivateGroupRotate"),MessageBoxButton.OKCancel,MessageBoxImage.Warning,MessageBoxResult.Cancel)!=MessageBoxResult.OK)return;
                            run(()=> { relayGroups.Rotate(captured);return captured; },true);
                        },"refresh-cw");rotate.Tag="group-rotate:"+group.Id;rotate.IsEnabled=group.Pending!="DELETE";buttons.Children.Add(rotate);
                    }
                    var forget=WpfKit.Quiet(Localization.Get("GroupForget"),()=> {
                        if(WpfKit.Ask(window,Localization.T("GroupForgetConfirm",group.Label),Localization.Get("GroupForget"),MessageBoxButton.OKCancel,MessageBoxImage.Warning,MessageBoxResult.Cancel)!=MessageBoxResult.OK)return;
                        run(()=> { relayGroups.Forget(captured);return null; },false);
                    },"circle-x");forget.Tag="group-forget:"+group.Id;forget.IsEnabled=group.Pending=="";buttons.Children.Add(forget);
                    if(group.IsOwner) {
                        var delete=WpfKit.Quiet(Localization.Get("PrivateGroupDelete"),()=> {
                            if(WpfKit.Ask(window,Localization.T("PrivateGroupDeleteConfirm",group.Label),Localization.Get("PrivateGroupDelete"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                            run(()=> { relayGroups.Delete(captured);return null; },false);
                        },"trash-2");delete.Foreground=WpfFieldTheme.Warn;delete.Tag="group-delete:"+group.Id;delete.IsEnabled=group.Pending==""||group.Pending=="DELETE";buttons.Children.Add(delete);
                    }
                    foreach(var button in buttons.Children.OfType<Button>().Concat(new[]{rename,connect}))System.Windows.Automation.AutomationProperties.SetName(button,button.Content+" · "+group.Label);
                    foreach(var button in buttons.Children.OfType<Button>())button.Margin=new Thickness(0,0,4,6);
                    card.Children.Add(buttons);var frame=WpfKit.Card(card,new Thickness(16,14,14,12));frame.Margin=new Thickness(0,0,0,10);rows.Children.Add(frame);
                }
            };draw();
            var done=WpfKit.Primary(Localization.Get("Done"),()=> { if(busy)return;try { relayGroups.SetName(name.Text);nameHelp.Visibility=Visibility.Visible;window.Close(); }catch(Exception error) { status.Text=GroupFailure(error);name.Focus(); } },"check");dialog.AddAction(done);
            window.Closing+=(s,e)=> { if(busy)e.Cancel=true; };
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };timer.Tick+=(s,e)=> {
                if(!busy) {
                    var views=relayGroups.GroupViews.ToArray();
                    foreach(var label in ChildrenOf<TextBlock>(rows).Where(x=>(x.Tag as string??"").StartsWith("group-state:"))) { var group=views.FirstOrDefault(g=>g.Id==((string)label.Tag).Substring(12));if(group!=null)label.Text=GroupState(group); }
                    foreach(var dot in ChildrenOf<System.Windows.Shapes.Ellipse>(rows).Where(x=>(x.Tag as string??"").StartsWith("group-dot:"))) { var group=views.FirstOrDefault(g=>g.Id==((string)dot.Tag).Substring(10));if(group!=null)dot.Fill=GroupInk(group); }
                    foreach(var toggle in ChildrenOf<Button>(rows).Where(x=>(x.Tag as string??"").StartsWith("group-toggle:"))) { var group=views.FirstOrDefault(g=>g.Id==((string)toggle.Tag).Substring(13));if(group!=null)toggle.Content=Localization.Get(group.Session==null||group.Session.Error!=null?"PrivateGroupConnect":"PrivateGroupLeave"); }
                    foreach(var title in ChildrenOf<TextBlock>(rows).Where(x=>(x.Tag as string??"").StartsWith("group-title:"))) { var group=views.FirstOrDefault(g=>g.Id==((string)title.Tag).Substring(12));if(group!=null)title.Text=group.Label+(group.IsOwner?" · "+Localization.Get("PrivateGroupOwner"):""); }
                }
                if(relayGroups.PersistenceError!=null)status.Text=Localization.Get("LocalSaveFailed");
            };timer.Start();window.Closed+=(s,e)=> { closed=true;timer.Stop();code.Clear(); };window.ContentRendered+=(s,e)=>code.Focus();return window;
        }
        Window BuildGroupMembers(RelayMembership group) {
            var window=Window(Localization.Get("GroupManageMembers")+" · "+group.Label,620,600);window.Tag="group-members";
            var dialog=new WpfKit.Dialog(window,window.Title,Localization.Get("GroupMembersHelp"));
            var rows=new StackPanel();var status=WpfFieldTheme.Text("",13,WpfFieldTheme.Amber);status.TextWrapping=TextWrapping.Wrap;status.TextTrimming=TextTrimming.None;dialog.Add(status);
            var count=WpfKit.Badge("…");dialog.Add(WpfKit.Section(Localization.Get("GroupMembersTitle"),rows,null,count));
            bool busy=false,closed=false;Action<string> refresh=null;
            refresh=target=> {
                if(busy||closed)return;busy=true;rows.IsEnabled=false;
                System.Threading.Tasks.Task.Run(()=> { if(target!=null)relayGroups.DisconnectMember(group,target);return relayGroups.ReadMembers(group); }).ContinueWith(task=> {
                    if(task.IsFaulted)task.Exception.Handle(error=>true);PostUi(()=> {
                        busy=false;if(closed)return;rows.IsEnabled=true;
                        if(task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion) { status.Text=Localization.Get("GroupMembersFailed");return; }
                        status.Text="";rows.Children.Clear();var members=task.Result.Items;((TextBlock)count.Child).Text=members.Count(m=>m.Online).ToString(CultureInfo.InvariantCulture)+" / "+members.Length.ToString(CultureInfo.InvariantCulture);
                        if(members.Length==0) { rows.Children.Add(WpfKit.Empty(Localization.Get("GroupMembersEmpty"),"users"));return; }
                        var items=new List<UIElement>();
                        foreach(var member in members.OrderByDescending(m=>m.IsOwner).ThenBy(m=>m.Name,StringComparer.CurrentCultureIgnoreCase)) {
                            var captured=member;Button disconnect=null;
                            if(!member.IsOwner&&member.Online) { disconnect=WpfKit.Secondary(Localization.Get("GroupDisconnect"),()=>refresh(captured.Id),"log-out");disconnect.Tag="group-disconnect:"+member.Id;System.Windows.Automation.AutomationProperties.SetName(disconnect,Localization.Get("GroupDisconnect")+" · "+member.Name); }
                            var lead=WpfKit.IconTile(member.IsOwner?"shield":"user-round",member.Online?WpfFieldTheme.Success:WpfFieldTheme.Dim,32);
                            items.Add(WpfKit.Row(lead,member.Name+(member.IsOwner?" · "+Localization.Get("PrivateGroupOwner"):""),Localization.Get(member.Online?"PlayerConnected":"SquadOffline"),disconnect));
                        }
                        rows.Children.Add(WpfKit.List(items));
                    });
                });
            };
            dialog.AddAction(WpfKit.Primary(Localization.Get("Done"),()=>window.Close(),"check"));
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };timer.Tick+=(s,e)=>refresh(null);window.Loaded+=(s,e)=> { refresh(null);timer.Start(); };window.Closed+=(s,e)=> { closed=true;timer.Stop();relayGroups.CloseMemberManagement(group); };return window;
        }
        static IEnumerable<T> ChildrenOf<T>(DependencyObject root) where T:DependencyObject {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i);var item=child as T;if(item!=null)yield return item;foreach(var nested in ChildrenOf<T>(child))yield return nested; }
        }
        Window BuildPrivatePositions() {
            var window=Window(Localization.Get("PrivatePositions"),560,690);var body=new StackPanel { Margin=new Thickness(24) };
            body.Children.Add(new TextBlock { Text=Localization.Get("PrivatePositionHelp"),Foreground=WpfFieldTheme.Ink,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16) });
            body.Children.Add(new TextBlock { Text=Localization.Get("PrivatePositionMarkersHelp")+" "+Localization.Get("PrivatePositionGpsHelp"),Foreground=WpfFieldTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) });
            body.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivatePositionName")));var name=new TextBox { MaxLength=32,Margin=new Thickness(0,4,0,12) };body.Children.Add(name);
            var mode=new ComboBox { ItemsSource=new[]{Localization.Get("PrivatePositionHost"),Localization.Get("PrivatePositionJoin")},SelectedIndex=1,Margin=new Thickness(0,4,0,12) };body.Children.Add(mode);
            var addressLabel=WpfFieldTheme.Text(Localization.Get("PrivatePositionAddress"));body.Children.Add(addressLabel);var address=new TextBox { Tag="private-position-address",MaxLength=253,Margin=new Thickness(0,4,0,12) };body.Children.Add(address);
            var lookupInfo=new TextBlock { Foreground=WpfFieldTheme.Muted,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,6,0,8) };
            bool hostMode=false,lookupBusy=false,closed=false;string hostAddress="",joinAddress="";
            Button detectButton=null;Action detect=null;detect=()=> {
                if(lookupBusy||!hostMode)return;lookupBusy=true;detectButton.IsEnabled=false;string original=address.Text;lookupInfo.Text=Localization.Get("PrivatePositionPublicIpHelp")+Environment.NewLine+Localization.Get("PrivatePositionConnecting");
                var lookup=publicAddressLookup??(diagnostic?(Func<System.Threading.Tasks.Task<string>>)(()=>System.Threading.Tasks.Task.FromResult("8.8.8.8")):(()=>System.Threading.Tasks.Task.Run(()=>PublicPositionAddress.Lookup())));
                System.Threading.Tasks.Task<string> pending;try { pending=lookup(); }catch(Exception) { pending=System.Threading.Tasks.Task.FromResult<string>(null); }
                pending.ContinueWith(result=> { if(result.IsFaulted)result.Exception.Handle(error=>true);PostUi(()=> {
                    lookupBusy=false;if(closed)return;detectButton.IsEnabled=privatePositions==null;string found=result.Status==System.Threading.Tasks.TaskStatus.RanToCompletion?PublicPositionAddress.Parse(result.Result):null;
                    if(found==null) { lookupInfo.Text=Localization.Get("PrivatePositionPublicIpFailed");return; }
                    if(hostMode&&address.Text==original) { address.Text=found;if(privatePositions!=null&&privatePositions.Host)privatePositions.SetShareAddress(found); }else if(!hostMode&&hostAddress==original)hostAddress=found;
                    lookupInfo.Text=Localization.Get("PrivatePositionPublicIpHelp");
                }); });
            };
            detectButton=WpfFieldTheme.Button(Localization.Get("PrivatePositionDetectPublicIp"),()=>detect());detectButton.Tag="private-position-detect-public-ip";detectButton.Visibility=Visibility.Collapsed;body.Children.Add(detectButton);body.Children.Add(lookupInfo);
            body.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivatePositionPort")));var port=new TextBox { Text="47831",MaxLength=5,Margin=new Thickness(0,4,0,12) };body.Children.Add(port);
            var localAddress=new TextBlock { Foreground=WpfFieldTheme.Muted,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed };body.Children.Add(localAddress);
            System.Threading.Tasks.Task.Run(()=> {
                string addresses="";try { addresses=string.Join(Environment.NewLine,System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up).SelectMany(n=>n.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&!System.Net.IPAddress.IsLoopback(a.Address)&&!a.Address.ToString().StartsWith("169.254.")).Select(a=>n.Name+": "+a.Address)).Distinct()); }catch(Exception) {}
                PostUi(()=> { if(!closed)localAddress.Text=Localization.Get("PrivatePositionLocalAddresses")+Environment.NewLine+addresses; });
            });
            mode.SelectionChanged+=(s,e)=> {
                if(hostMode)hostAddress=address.Text;else joinAddress=address.Text;hostMode=mode.SelectedIndex==0;address.Text=hostMode?hostAddress:joinAddress;
                addressLabel.Text=Localization.Get(hostMode?"PrivatePositionShareAddress":"PrivatePositionAddress");localAddress.Visibility=lookupInfo.Visibility=detectButton.Visibility=hostMode?Visibility.Visible:Visibility.Collapsed;
                lookupInfo.Text=Localization.Get("PrivatePositionPublicIpHelp");if(hostMode&&string.IsNullOrWhiteSpace(address.Text))detect();
            };
            body.Children.Add(WpfFieldTheme.Text(Localization.Get("PrivatePositionKey")));var secret=new PasswordBox { Margin=new Thickness(0,4,0,12),FontSize=16,MaxLength=64,MinHeight=40,Background=WpfFieldTheme.Ground,Foreground=WpfFieldTheme.Ink,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Padding=new Thickness(12,9,12,9) };body.Children.Add(secret);
            var generateKey=WpfFieldTheme.Button(Localization.Get("PrivatePositionNewKey"),()=> { secret.Password=PrivatePositions.NewKey();System.Windows.Clipboard.SetText(secret.Password); });body.Children.Add(generateKey);
            var status=new TextBlock { Foreground=WpfFieldTheme.Amber,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) };body.Children.Add(status);
            var enabled=new CheckBox { Content=Localization.Get("PrivatePositions"),IsChecked=privatePositions!=null,Margin=new Thickness(0,12,0,12) };body.Children.Add(enabled);
            if(privatePositions!=null) { if(privatePositions.Host)hostAddress=privatePositions.Address;else joinAddress=privatePositions.Address;name.Text=privatePositions.Name;mode.SelectedIndex=privatePositions.Host?0:1;address.Text=privatePositions.Address;port.Text=privatePositions.Port.ToString(CultureInfo.InvariantCulture); }
            Action<bool> inputs=active=> { name.IsEnabled=mode.IsEnabled=port.IsEnabled=secret.IsEnabled=generateKey.IsEnabled=!active;detectButton.IsEnabled=!active&&!lookupBusy;address.IsEnabled=true;address.IsReadOnly=active; };
            inputs(privatePositions!=null);
            enabled.Checked+=(s,e)=> {
                try { int number;if(!int.TryParse(port.Text,out number))throw new ArgumentException();privatePositions=new DirectPositions(mode.SelectedIndex==0,address.Text,number,secret.Password,name.Text,!diagnostic,null,false,null,null,relayGroups==null?null:relayGroups.MemberColorSeed);status.Text=Localization.Get("PrivatePositionConnecting");inputs(true);Refresh(); }
                catch(Exception) { status.Text=Localization.Get("PrivatePositionInvalid");enabled.IsChecked=false; }
            };
            enabled.Unchecked+=(s,e)=> { if(privatePositions!=null) { privatePositions.Dispose();privatePositions=null; }inputs(false);Refresh(); };
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };timer.Tick+=(s,e)=> { if(privatePositions!=null) {
                status.Text=privatePositions.Error!=null?Localization.Get("PrivatePositionInvalid"):privatePositions.Ready?Localization.Get("PrivatePositionConnected")+" · "+privatePositions.Connections:Localization.Get("PrivatePositionConnecting");
                if(privatePositions.Host&&privatePositions.RouterFinished) {
                    if(!privatePositions.RouterReady)status.Text+=Environment.NewLine+Localization.Get("PrivatePositionRouterManual");
                    if(string.IsNullOrWhiteSpace(address.Text)&&!string.IsNullOrWhiteSpace(privatePositions.Address))address.Text=privatePositions.Address;
                }
                if(privatePositions.Host&&privatePositions.FirewallFinished)status.Text+=Environment.NewLine+Localization.Get(privatePositions.FirewallReady?"PrivatePositionFirewallReady":"PrivatePositionFirewallFailed");
            } };timer.Start();
            body.Children.Add(WpfFieldTheme.Button(Localization.Get("Done"),()=>window.Close(),true));window.Closed+=(s,e)=> { closed=true;timer.Stop();secret.Clear(); };window.ContentRendered+=(s,e)=>name.Focus();window.Content=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };return window;
        }
        bool ValidateNumericFields(Window window) {
            var fields=LogicalSettingsChildren(window).OfType<TextBox>().Where(t=>t.Resources.Contains("commit-number")).ToArray();
            foreach(var field in fields)((Action)field.Resources["commit-number"])();
            var invalid=fields.FirstOrDefault(t=>!string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetItemStatus(t)));
            if(invalid==null)return true;for(var parent=LogicalTreeHelper.GetParent(invalid);parent!=null;parent=LogicalTreeHelper.GetParent(parent)) {var expander=parent as Expander;if(expander!=null)expander.IsExpanded=true;}invalid.Focus();invalid.BringIntoView();return false;
        }
        static IEnumerable<DependencyObject> LogicalSettingsChildren(DependencyObject parent) {
            foreach(object child in LogicalTreeHelper.GetChildren(parent)) {var element=child as DependencyObject;if(element==null)continue;yield return element;foreach(var descendant in LogicalSettingsChildren(element))yield return descendant;}
        }
        static bool SettingHasKey(WpfSettingDescriptor setting,string key) {
            string text=Localization.Get(key);return setting.Control.Text==text||setting.Control.Controls.Cast<System.Windows.Forms.Control>().Any(c=>c.Text==text);
        }
        internal static int SettingsCategory(WpfSettingDescriptor setting) {
            if(setting.GroupKey=="ShortcutHeading"||SettingHasKey(setting,"KeyRebindingWizard"))return 1;
            if(SettingHasKey(setting,"ZoneChime"))return 3;
            if(setting.GroupKey=="SecAppearance"||setting.GroupKey=="SecMapZones")return 2;
            if(setting.GroupKey=="SecTrackingZoom"||setting.GroupKey=="VoiceHeading")return 3;
            if(setting.GroupKey=="SecToolsShortcuts"||setting.Control.Name=="AdminToolsToggle")return 5;
            return 0;
        }
        Window BuildSettings(int initial) {
            // Shell per theme: Tactical uses a caps title with underlined tabs; Glass a tinted icon
            // sidebar with carded sections; Minimal a grouped sidebar, breadcrumb and hairline sections.
            var t=Theme;bool tabs=t.Tabs==WpfTabStyle.Strip,glass=t.GradientAccent;
            var window=Window(Localization.Get("Settings"),tabs?980:940,780);window.WindowStyle=WindowStyle.SingleBorderWindow;
            int[] order={0,2,3,1,4,5};string[] keys={"FieldGeneral","FieldShortcuts","FieldAppearance","SettingsTracking","SettingsGroups","SettingsTools"};
            string[] icons={"settings","keyboard","palette","volume-2","users","hard-drive-download"};string[] tints={"#8E97AD","#7C89A3","#8A6CFF","#4C9BFF","#2FCB8A","#22B8A6"};
            var items=new Dictionary<int,ListBoxItem>();var labels=new Dictionary<int,TextBlock>();var groupLabels=new List<KeyValuePair<TextBlock,string>>();
            var version=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);version.FontFamily=WpfFieldTheme.MonoFont;version.VerticalAlignment=VerticalAlignment.Center;
            var heading=WpfFieldTheme.Text("",tabs?20:15,tabs?WpfFieldTheme.Muted:WpfFieldTheme.Ink,true);heading.VerticalAlignment=VerticalAlignment.Center;
            var pageTitle=WpfFieldTheme.Text("",glass?24:26,null,true);pageTitle.TextWrapping=TextWrapping.Wrap;pageTitle.TextTrimming=TextTrimming.None;
            var crumb=WpfFieldTheme.Text("",13,WpfFieldTheme.Muted);crumb.VerticalAlignment=VerticalAlignment.Center;
            var done=WpfFieldTheme.Button(Localization.Get("Done"),()=> { if(!ValidateNumericFields(window))return;if(engine.WpfSave())window.Close();else WpfKit.Ask(window,Localization.Get("LocalSaveFailed"),Localization.Get("Settings"),MessageBoxButton.OK,MessageBoxImage.Warning); },true);done.Tag="text:Done";done.MinWidth=120;done.HorizontalAlignment=HorizontalAlignment.Right;
            if(t.Uppercase) { done.FontFamily=WpfFieldTheme.DisplayFont;done.FontWeight=FontWeights.Bold;done.FontSize=16; }
            var footer=new DockPanel { Tag="settings-footer",LastChildFill=false };DockPanel.SetDock(done,Dock.Right);footer.Children.Add(done);
            var nav=new ListBox { Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,Tag="settings-categories",Padding=new Thickness(0) };System.Windows.Automation.AutomationProperties.SetName(nav,Localization.Get("Settings"));
            if(tabs) { var wrap=new FrameworkElementFactory(typeof(WrapPanel));nav.ItemsPanel=new ItemsPanelTemplate(wrap); }
            var body=new StackPanel { Margin=tabs?new Thickness(0,4,10,24):glass?new Thickness(0,0,10,24):new Thickness(40,26,40,30) };var scroll=new ScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            foreach(int index in order) {
                if(!tabs&&!glass&&(index==0||index==4||index==5)) {
                    string groupKey=index==0?"SettingsGroupPreferences":index==4?"SettingsGroupGame":"SettingsGroupSystem";var group=WpfFieldTheme.Text("",11,WpfFieldTheme.Dim,true);groupLabels.Add(new KeyValuePair<TextBlock,string>(group,groupKey));
                    nav.Items.Add(new ListBoxItem { Content=group,IsEnabled=false,Focusable=false,Padding=new Thickness(10,index==0?2:16,10,4),Margin=new Thickness(0),Tag="settings-group" });
                }
                if(glass&&(index==4||index==5))nav.Items.Add(new ListBoxItem { Content=new Border { Height=1,Background=WpfFieldTheme.Line },IsEnabled=false,Focusable=false,Padding=new Thickness(8,8,8,8),Margin=new Thickness(0),Tag="settings-group" });
                var text=WpfFieldTheme.Text("",tabs?16:14,WpfFieldTheme.Ink,tabs);text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.None;text.VerticalAlignment=VerticalAlignment.Center;
                if(tabs) { text.FontFamily=WpfFieldTheme.DisplayFont;text.FontWeight=FontWeights.Bold;text.TextWrapping=TextWrapping.NoWrap; }
                labels.Add(index,text);var row=new DockPanel();
                if(glass) {
                    var tint=t.Colour(tints[index]);var fill=new LinearGradientBrush(Color.FromArgb(255,(byte)Math.Min(255,tint.R+40),(byte)Math.Min(255,tint.G+40),(byte)Math.Min(255,tint.B+40)),tint,new Point(0,0),new Point(1,1));fill.Freeze();
                    var tile=new Border { Width=28,Height=28,CornerRadius=new CornerRadius(8),Background=fill,Margin=new Thickness(0,0,12,0),Child=WpfIcons.Create(icons[index],15,Brushes.White) };row.Children.Add(tile);
                } else if(!tabs) {
                    var icon=WpfIcons.Create(icons[index],16,WpfFieldTheme.Muted);icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") { Source=text });icon.Margin=new Thickness(0,0,11,0);icon.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(icon);
                }
                foreach(UIElement child in row.Children)DockPanel.SetDock(child,Dock.Left);row.Children.Add(text);
                var item=new ListBoxItem {Content=row,Padding=tabs?new Thickness(16,10,16,10):glass?new Thickness(8,7,10,7):new Thickness(10,7,10,7),Margin=tabs?new Thickness(0,0,2,0):new Thickness(0,1,0,1),Tag="settings-category:"+index};
                item.SetResourceReference(FrameworkElement.StyleProperty,tabs?"SettingsTab":"SettingsNav");items.Add(index,item);nav.Items.Add(item);
            }
            int currentPage=-1,settingsRevision=-1;bool selecting=false;var expanded=new Dictionary<string,bool>();Action<int> page=null;
            Action<int> step=delta=> { int position=Math.Max(0,Array.IndexOf(order,currentPage));page(order[(position+delta+order.Length)%order.Length]);if(items.ContainsKey(currentPage))items[currentPage].Focus(); };
            UIElement root;
            if(tabs) {
                var layout=new DockPanel { Margin=new Thickness(36,24,36,16) };
                var top=new DockPanel { Margin=new Thickness(0,0,0,16) };DockPanel.SetDock(version,Dock.Right);top.Children.Add(version);
                var word=WpfFieldTheme.Text("SETTINGS",44,WpfFieldTheme.Ink,true);word.FontFamily=WpfFieldTheme.DisplayFont;word.FontWeight=FontWeights.Bold;word.VerticalAlignment=VerticalAlignment.Center;word.Margin=new Thickness(0,-6,16,-4);
                var titleRow=new StackPanel { Orientation=Orientation.Horizontal };titleRow.Children.Add(word);titleRow.Children.Add(heading);top.Children.Add(titleRow);DockPanel.SetDock(top,Dock.Top);layout.Children.Add(top);
                var strip=new DockPanel();Func<string,string,int,Button> stepper=(icon,key,delta)=> { var b=IconButton(icon,"tooltip:"+key,28,14);b.BorderBrush=WpfFieldTheme.Line;b.BorderThickness=new Thickness(1);b.VerticalAlignment=VerticalAlignment.Center;b.Click+=(s,e)=>step(delta);return b; };
                var previous=stepper("chevron-left","PagePrevious",-1);previous.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(previous,Dock.Left);strip.Children.Add(previous);
                var next=stepper("chevron-right","PageNext",1);next.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(next,Dock.Right);strip.Children.Add(next);strip.Children.Add(nav);
                var stripFrame=new Border { Child=strip,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,1),Margin=new Thickness(0,0,0,6) };DockPanel.SetDock(stripFrame,Dock.Top);layout.Children.Add(stripFrame);
                var hints=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
                Action<string,string> hint=(key,label)=> { hints.Children.Add(KeyCap(key));var caption=WpfFieldTheme.Text(Localization.Get(label),13,WpfFieldTheme.Muted);caption.Tag="text:"+label;caption.Margin=new Thickness(7,0,20,0);caption.VerticalAlignment=VerticalAlignment.Center;hints.Children.Add(caption); };
                hint("ESC","Close");hint("CTRL+TAB","PageNext");DockPanel.SetDock(hints,Dock.Left);footer.Children.Add(hints);
                var footerFrame=new Border { Child=footer,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,14,0,0) };DockPanel.SetDock(footerFrame,Dock.Bottom);layout.Children.Add(footerFrame);
                layout.Children.Add(scroll);root=layout;
            } else {
                var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(glass?244:226) });grid.ColumnDefinitions.Add(new ColumnDefinition());
                var side=new DockPanel();var brand=new DockPanel { Margin=new Thickness(glass?6:8,0,0,glass?18:16) };
                Border mark;
                if(glass)mark=new Border { Width=32,Height=32,CornerRadius=new CornerRadius(10),Background=WpfFieldTheme.AccentFill,Child=WpfIcons.Create("settings",17,WpfFieldTheme.AccentInk) };
                else mark=new Border { Width=22,Height=22,CornerRadius=new CornerRadius(5),Background=WpfFieldTheme.Ink,Child=WpfIcons.Create("settings",13,WpfFieldTheme.Ground) };
                mark.Margin=new Thickness(0,0,10,0);DockPanel.SetDock(mark,Dock.Left);brand.Children.Add(mark);brand.Children.Add(heading);DockPanel.SetDock(brand,Dock.Top);side.Children.Add(brand);
                version.Margin=new Thickness(glass?8:10,10,0,0);version.FontSize=11;DockPanel.SetDock(version,Dock.Bottom);side.Children.Add(version);side.Children.Add(nav);
                grid.Children.Add(new Border { Child=side,Background=WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,1,0),Padding=glass?new Thickness(14,20,14,14):new Thickness(12,16,12,14) });
                var main=new DockPanel();Grid.SetColumn(main,1);grid.Children.Add(main);
                if(glass) {
                    main.Margin=new Thickness(30,22,30,18);pageTitle.Margin=new Thickness(0,0,0,8);DockPanel.SetDock(pageTitle,Dock.Top);main.Children.Add(pageTitle);
                    var footerFrame=new Border { Child=footer,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,14,0,0) };DockPanel.SetDock(footerFrame,Dock.Bottom);main.Children.Add(footerFrame);
                } else {
                    var bar=new Border { Child=crumb,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(40,15,40,15) };DockPanel.SetDock(bar,Dock.Top);main.Children.Add(bar);
                    var footerFrame=new Border { Child=footer,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(40,12,40,14) };DockPanel.SetDock(footerFrame,Dock.Bottom);main.Children.Add(footerFrame);
                }
                main.Children.Add(scroll);root=grid;
            }
            Action<string,StackPanel,bool> section=(key,panel,open)=> {
                if(panel.Children.Count==0)return;bool saved;if(expanded.TryGetValue(key,out saved))open=saved;
                var title=WpfFieldTheme.Text(t.Caps(Localization.Get(key).Replace("&&","&")),tabs?16:glass?13:16,glass?WpfFieldTheme.Muted:WpfFieldTheme.Ink,true);title.TextWrapping=TextWrapping.Wrap;title.TextTrimming=TextTrimming.None;title.VerticalAlignment=VerticalAlignment.Center;
                object header=title;UIElement content=panel;Thickness margin;
                if(tabs) {
                    title.FontFamily=WpfFieldTheme.DisplayFont;title.FontWeight=FontWeights.Bold;var row=new DockPanel();
                    var tick=new Border { Width=3,Height=14,Background=WpfFieldTheme.Accent,Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center };DockPanel.SetDock(tick,Dock.Left);row.Children.Add(tick);
                    DockPanel.SetDock(title,Dock.Left);row.Children.Add(title);row.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center });
                    header=row;panel.Margin=new Thickness(12,6,0,6);margin=new Thickness(0,16,0,2);
                } else if(glass) {
                    content=new Border { Child=panel,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Min(16,t.PanelRadius)),Padding=new Thickness(18,12,18,14),Margin=new Thickness(0,4,0,0) };margin=new Thickness(0,12,0,4);
                } else {
                    content=new Border { Child=panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,10,0,4),Margin=new Thickness(0,6,0,0) };margin=new Thickness(0,20,0,4);
                }
                var expander=new Expander {Header=header,Content=content,IsExpanded=open,Margin=margin,Padding=new Thickness(0),Background=Brushes.Transparent,Foreground=WpfFieldTheme.Ink,Tag="settings-section:"+key};
                expander.Expanded+=(sender,e)=>expanded[key]=true;expander.Collapsed+=(sender,e)=>expanded[key]=false;body.Children.Add(expander);
            };
            Action<StackPanel,WpfSettingDescriptor> addNative=(panel,setting)=> {var converted=ConvertSetting(setting.Control,()=> {engine.WpfRebuildSettings();page(currentPage);Refresh();});if(converted!=null) {converted.Resources["native-setting"]=setting;panel.Children.Add(converted);} };
            page=index=> {
                if(!items.ContainsKey(index))index=0;
                if(currentPage>=0&&index!=currentPage&&!ValidateNumericFields(window)) {selecting=true;nav.SelectedItem=items[currentPage];selecting=false;return;}
                currentPage=index;settingsRevision=engine.WpfSettingsRevision;settingsPreview=null;
                window.Title=Localization.Get("Settings");version.Text="v"+MapWindow.VersionString+(Program.IsTestBuild?" "+Localization.Get("WpfTestBuild"):"");
                heading.Text=tabs&&string.Equals(window.Title,"Settings",StringComparison.OrdinalIgnoreCase)?"":window.Title;
                foreach(var group in groupLabels)group.Key.Text=Localization.Get(group.Value);
                foreach(var pair in items) {
                    bool active=pair.Key==index;var label=labels[pair.Key];label.Text=t.Caps(Localization.Get(keys[pair.Key]));
                    label.Foreground=active?(tabs?WpfFieldTheme.Accent:WpfFieldTheme.Ink):(glass?WpfFieldTheme.Ink:WpfFieldTheme.Muted);if(!tabs)label.FontWeight=active?FontWeights.SemiBold:FontWeights.Normal;
                    System.Windows.Automation.AutomationProperties.SetName(pair.Value,Localization.Get(keys[pair.Key]));
                }
                selecting=true;nav.SelectedItem=items[index];selecting=false;done.Content=Localization.Get("Done");
                pageTitle.Text=Localization.Get(keys[index]);crumb.Text=Localization.Get("Settings")+"  /  "+pageTitle.Text;
                body.Children.Clear();scroll.ScrollToTop();if(!tabs&&!glass) { pageTitle.Margin=new Thickness(0,0,0,4);body.Children.Add(pageTitle); }var state=engine.WpfSnapshot(true);
                var settings=Enumerable.Range(0,3).SelectMany(tab=>engine.WpfSettings(tab)).Where(setting=>SettingsCategory(setting)==index).ToArray();
                if(index==1) {
                    var help=WpfFieldTheme.Text(Localization.Get("KeyWizardIntro"),16,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;body.Children.Add(help);
                    int[] gameKeys={state.MapKey,state.ChatKey,state.CopyKey};string[] gameLabels={"KeyWizardMapKey","KeyWizardChatKey","KeyWizardCopyKey"};
                    for(int i=0;i<gameKeys.Length;i++) {
                        int binding=i;string label=Localization.Get(gameLabels[i]);var row=new DockPanel { Margin=new Thickness(0,8,0,8) };
                        string value=(i==2&&state.CopyModifier!=0?PhysicalKeyCapture.KeyName(state.CopyModifier)+" + ":"")+PhysicalKeyCapture.KeyName(gameKeys[i]);
                        var key=WpfFieldTheme.Button(value,()=> { CaptureKey(label,(vk,scan)=>engine.WpfGameBind(binding,vk,scan),false);page(1); });
                        key.Tag="game-binding-"+i;key.MinWidth=88;System.Windows.Automation.AutomationProperties.SetName(key,label+": "+value);DockPanel.SetDock(key,Dock.Right);row.Children.Add(key);row.Children.Add(WpfFieldTheme.Text(label,16));body.Children.Add(row);
                    }
                    foreach(var action in AppControlBindings.AllActions) { var captured=action;var row=new DockPanel { Margin=new Thickness(0,3,0,3) };var key=WpfFieldTheme.Button(PhysicalKeyCapture.KeyName(state.Bindings.Key(action)),()=> { CaptureKey(Localization.Get(AppControlBindings.LabelKey(captured)),(vk,scan)=>engine.WpfBind(captured,vk,scan),true);page(1); });key.MinWidth=88;System.Windows.Automation.AutomationProperties.SetName(key,Localization.Get(AppControlBindings.LabelKey(action))+": "+PhysicalKeyCapture.KeyName(state.Bindings.Key(action)));DockPanel.SetDock(key,Dock.Right);row.Children.Add(key);row.Children.Add(WpfFieldTheme.Text(Localization.Get(AppControlBindings.LabelKey(action)),16));body.Children.Add(row);body.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line }); }
                    foreach(var setting in engine.WpfSettings(1).Where(item=>item.Control is TacticalCheckBox||item.Control is System.Windows.Forms.Label))addNative(body,setting);

                } else if(index==0) {
                    var general=new StackPanel();foreach(var setting in settings.Where(item=>!SettingHasKey(item,"OpenStartupGuide")&&!SettingHasKey(item,"KeyRebindingWizard")))addNative(general,setting);
                    Button guide=null;guide=WpfFieldTheme.Button(Localization.Get("FirstTimeGuideTitle"),()=> {window.Close();application.Dispatcher.BeginInvoke(new Action(()=>ShowSetupWindow(false)));});guide.Tag="first-time-guide";general.Children.Add(guide);section("SecGeneral",general,true);
                    var startup=new StackPanel();
                        var autoLabel=WpfFieldTheme.Text(ScumAutoLaunch.Label,16);autoLabel.TextWrapping=TextWrapping.Wrap;autoLabel.HorizontalAlignment=HorizontalAlignment.Left;
                        var auto=new CheckBox { Content=autoLabel,IsChecked=ScumAutoLaunch.Enabled,Tag="auto-launch-scum",Margin=new Thickness(0,12,0,8),IsEnabled=!diagnostic };
                        System.Windows.Automation.AutomationProperties.SetName(auto,ScumAutoLaunch.Label);
                        auto.ToolTip=ScumAutoLaunch.Help;
                        auto.Click+=async (s,e)=> {
                            if(diagnostic)return;
                            bool enable=auto.IsChecked==true;auto.IsEnabled=false;
                            try { await System.Threading.Tasks.Task.Run(()=>ScumAutoLaunch.SetEnabled(enable)); }
                            catch(Exception error) { Program.LogException("ScumAutoLaunchSettings",error);WpfKit.Ask(window,error.Message,"SCUM MiniMap",MessageBoxButton.OK,MessageBoxImage.Error); }
                            auto.IsChecked=ScumAutoLaunch.Enabled;auto.IsEnabled=true;
                        };
                        startup.Children.Add(auto);
                        var autoHelp=WpfFieldTheme.Text(ScumAutoLaunch.Help,14,WpfFieldTheme.Muted);autoHelp.TextWrapping=TextWrapping.Wrap;startup.Children.Add(autoHelp);

                    section("SettingsStartup",startup,true);
                } else if(index==2) {
                    var themes=new StackPanel();themes.Children.Add(ThemePicker(window));section("UiTheme",themes,true);
                    var positioning=new StackPanel();var placement=WpfFieldTheme.Button(Localization.Get("MapPlacement"),()=>Modal(BuildMapPlacement()));placement.Tag="text:MapPlacement";WpfFieldTheme.IconLabel(placement,"map",false,15);placement.Margin=new Thickness(0,4,0,10);positioning.Children.Add(placement);var move=WpfFieldTheme.Button(Localization.Get("WpfMoveMinimap"),ShowMoveHud);move.Tag="text:WpfMoveMinimap";WpfFieldTheme.IconLabel(move,"move",false,15);move.Margin=new Thickness(0,0,0,4);positioning.Children.Add(move);section("SettingsPositioning",positioning,true);
                    var compact=new StackPanel();AddAppearanceControls(compact,state);var cluster=compact.Children.OfType<CheckBox>().Single(c=>c.Tag as string=="poi-clustering");compact.Children.Remove(cluster);
                    settingsPreview=new WpfMapSurface {Compact=true,BaseMap=geography,Terrain=terrain,Width=210,Height=210,HorizontalAlignment=HorizontalAlignment.Left,Tag="settings-preview"};compact.Children.Insert(0,settingsPreview);
                    foreach(var setting in settings.Where(item=>item.GroupKey=="SecAppearance"))addNative(compact,setting);section("SecAppearance",compact,true);
                    var layers=new StackPanel();layers.Children.Add(cluster);var markers=new StackPanel();var zones=new StackPanel();
                    string[] markerKeys={"HistoryEnabled","HistoryHover","HistoryDuration","HistoryClear","ShowCustomWaypoints","CustomWaypointsDescription","RouteGuidanceColor","PlayerMarkerColor","RouteHistoryColor"};
                    string[] zoneKeys={"MapZonesScreenshot","EnableZoneTsv","ClearAllCustomZones"};
                    foreach(var setting in settings.Where(item=>item.GroupKey=="SecMapZones"))addNative(markerKeys.Any(key=>SettingHasKey(setting,key))?markers:zoneKeys.Any(key=>SettingHasKey(setting,key))?zones:layers,setting);
                    section("SecMapZones",layers,false);section("SettingsMarkers",markers,false);section("SettingsZoneTools",zones,false);
                    var crosshairPanel=new StackPanel();AddCrosshairSettings(crosshairPanel);section("Crosshair",crosshairPanel,true);var presets=new StackPanel();presets.Children.Add(WpfFieldTheme.Button(Localization.Get("SquadPresets"),ShowMapPresets));section("SquadPresets",presets,false);RefreshSetupPreview();
                } else if(index==3) {
                    foreach(string group in new[]{"SecTrackingZoom","VoiceHeading"}) {var panel=new StackPanel();foreach(var setting in settings.Where(item=>item.GroupKey==group||group=="VoiceHeading"&&SettingHasKey(item,"ZoneChime")))addNative(panel,setting);section(group,panel,true);}
                } else if(index==4) {
                    var groups=new StackPanel();groups.Children.Add(WpfFieldTheme.Button(Localization.Get("PrivatePositions"),ShowPrivatePositions,true));section("PrivatePositions",groups,true);
                    var routes=new StackPanel();routes.Children.Add(WpfFieldTheme.Button(Localization.Get("RouteList"),ShowRoutes));var help=WpfFieldTheme.Text(Localization.Get("RouteSessionHelp"),13,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;help.TextTrimming=TextTrimming.None;routes.Children.Add(help);section("RouteList",routes,true);
                } else {
                    var admin=new StackPanel();var tools=new StackPanel();var recovery=new StackPanel();
                    foreach(var setting in settings)addNative(setting.Control.Name=="AdminToolsToggle"?admin:SettingHasKey(setting,"ResetDefaultMap")?recovery:tools,setting);
                    var data=WpfFieldTheme.Button(Localization.Get("OpenDataFolder"),()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","\""+engine.WpfDataFolder+"\"") {UseShellExecute=true}));data.Tag="text:OpenDataFolder";tools.Children.Add(data);
                    section("ControlAdminCommands",admin,true);section("SettingsTools",tools,true);section("SettingsRecovery",recovery,false);
                }
            };
            nav.SelectionChanged+=(sender,e)=> {var selected=nav.SelectedItem as ListBoxItem;var tag=selected==null?null:selected.Tag as string;if(!selecting&&tag!=null&&tag.StartsWith("settings-category:",StringComparison.Ordinal))page(int.Parse(tag.Substring(18),CultureInfo.InvariantCulture));};
            window.PreviewKeyDown+=(sender,e)=> {if(e.Key==Key.Tab&&(Keyboard.Modifiers&ModifierKeys.Control)!=0) {int position=Array.IndexOf(order,currentPage);int requested=order[(position+((Keyboard.Modifiers&ModifierKeys.Shift)!=0?order.Length-1:1))%order.Length];page(requested);if(currentPage==requested)items[currentPage].Focus();e.Handled=true;} };
            page(initial);window.Activated+=(sender,e)=> {if(settingsRevision!=engine.WpfSettingsRevision)page(currentPage);};window.Closed+=(sender,e)=>settingsPreview=null;window.Content=root;return window;
        }
        void AddAppearanceControls(StackPanel body,WpfMapState state) {
                        var rotate=new CheckBox { Content=Localization.Get("RotateMinimap"),IsChecked=state.RotateMap,Margin=new Thickness(0,8,0,8),Tag="rotate-minimap" };
                        rotate.Checked+=(s,e)=> { engine.WpfRotate(true);Refresh(); };rotate.Unchecked+=(s,e)=> { engine.WpfRotate(false);Refresh(); };body.Children.Insert(0,rotate);
                        var clusterPois=new CheckBox { Content=Localization.Get("PoiClustering"),IsChecked=state.PoiClustering,Margin=new Thickness(0,8,0,8),Tag="poi-clustering" };
                        clusterPois.Checked+=(s,e)=> { engine.WpfPoiClustering(true);Refresh(); };clusterPois.Unchecked+=(s,e)=> { engine.WpfPoiClustering(false);Refresh(); };body.Children.Insert(1,clusterPois);
                        var shape=new ComboBox { ItemsSource=new[]{Localization.Get("ZeSquare"),Localization.Get("ZeCircle")},SelectedIndex=state.Shape=="Circle"?1:0,Margin=new Thickness(0,14,0,14),Tag="minimap-shape" };
                        shape.SelectionChanged+=(s,e)=> { engine.WpfAppearance(shape.SelectedIndex==1?"Circle":"Square",engine.WpfSnapshot(false).Opacity);Refresh(); };body.Children.Insert(0,shape);body.Children.Insert(0,WpfFieldTheme.Text(Localization.Get("ZeChangeShape"),18,null,true));
        }
        void AddSetupSettings(StackPanel body,int tab,Action rebuild,string onlyGroup=null,string[] basicKeys=null) {
            string group=null;var more=new StackPanel();var basic=basicKeys==null?null:new HashSet<string>(basicKeys.Select(Localization.Get));
            foreach(var setting in engine.WpfSettings(tab).OrderBy(item=>onlyGroup=="SecMapZones"&&item.Control.Controls.Cast<System.Windows.Forms.Control>().Any(child=>child.Tag as string=="swatch")?0:1)) {
                if(onlyGroup!=null&&setting.GroupKey!=onlyGroup)continue;
                if(setting.Control.Text==Localization.Get("OpenStartupGuide")||setting.Control.Text==Localization.Get("KeyRebindingWizard"))continue;
                if(basic==null&&setting.Group!=group) { group=setting.Group;var title=WpfFieldTheme.Eyebrow(group);title.Margin=new Thickness(0,18,0,10);body.Children.Add(title); }
                var destination=basic==null||basic.Contains(setting.Control.Text)||setting.Control.Controls.Cast<System.Windows.Forms.Control>().Any(child=>basic.Contains(child.Text))?body:more;
                var control=ConvertSetting(setting.Control,()=> { engine.WpfRebuildSettings();rebuild();Refresh(); });if(control!=null)destination.Children.Add(control);
            }
            if(more.Children.Count>0)body.Children.Add(new Expander { Header=WpfKit.Strong(Localization.Get("SetupMoreOptions"),15),Content=more,IsExpanded=false,Margin=new Thickness(0,16,0,12),Tag="setup-more-options" });
        }
        static string SettingLabel(System.Windows.Forms.Control source) {
            if(!string.IsNullOrWhiteSpace(source.AccessibleName))return source.AccessibleName;
            if(source.Parent!=null) {
                var label=source.Parent.Controls.Cast<System.Windows.Forms.Control>().TakeWhile(c=>c!=source).OfType<System.Windows.Forms.Label>().LastOrDefault();
                if(label!=null&&!string.IsNullOrWhiteSpace(label.Text))return label.Text;
            }
            return source.Text??"";
        }
        FrameworkElement ConvertSetting(System.Windows.Forms.Control source,Action rebuild) {
            if(source.IsDisposed)return null;
            var customCheck=source as TacticalCheckBox;
            if(customCheck!=null) { var text=WpfFieldTheme.Text(customCheck.Text,18);text.TextWrapping=TextWrapping.Wrap;var c=new CheckBox { Content=text,IsChecked=customCheck.Checked,MinHeight=44,Margin=new Thickness(0,6,0,6),IsEnabled=customCheck.Enabled };c.Checked+=(s,e)=> { if(!customCheck.IsDisposed) {customCheck.Checked=true;Refresh();} };c.Unchecked+=(s,e)=> { if(!customCheck.IsDisposed) {customCheck.Checked=false;Refresh();} };System.Windows.Automation.AutomationProperties.SetName(c,customCheck.Text);return c; }
            var number=source as System.Windows.Forms.NumericUpDown;
            if(number!=null) {
                var panel=new StackPanel { Margin=new Thickness(0,4,0,8) };var row=new StackPanel { Orientation=Orientation.Horizontal };
                var input=new TextBox { Text=number.Value.ToString(CultureInfo.CurrentCulture),MinWidth=90,MaxWidth=130,MinHeight=44,FontSize=18,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,6,0),Tag="setting-number",IsEnabled=number.Enabled };
                var range=WpfFieldTheme.Text(number.Minimum.ToString("F"+number.DecimalPlaces,CultureInfo.CurrentCulture)+" – "+number.Maximum.ToString("F"+number.DecimalPlaces,CultureInfo.CurrentCulture),16,WpfFieldTheme.Muted);range.Margin=new Thickness(6,4,0,0);range.Tag="setting-range";
                System.Windows.Automation.AutomationProperties.SetName(input,SettingLabel(number));
                System.Windows.Automation.AutomationProperties.SetHelpText(input,range.Text);
                string expectedRange=range.Text;
                Action sync=()=> { if(number.IsDisposed)return;range.Text=expectedRange;System.Windows.Automation.AutomationProperties.SetItemStatus(input,"");input.Text=number.Value.ToString(CultureInfo.CurrentCulture);input.BorderBrush=WpfFieldTheme.Line;range.Foreground=WpfFieldTheme.Muted; };
                Action commit=()=> { if(number.IsDisposed)return;decimal n;if(decimal.TryParse(input.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out n)&&n>=number.Minimum&&n<=number.Maximum&&decimal.Round(n,number.DecimalPlaces)==n) {number.Value=n;sync();Refresh();}else {input.BorderBrush=Brushes.OrangeRed;range.Foreground=Brushes.OrangeRed;range.Text=Localization.Get("InvalidNumber")+": "+expectedRange;System.Windows.Automation.AutomationProperties.SetItemStatus(input,range.Text);System.Windows.Automation.AutomationProperties.SetHelpText(input,range.Text);} };input.Resources["commit-number"]=commit;
                var less=WpfFieldTheme.Button("−",()=> {if(number.IsDisposed)return;number.Value=Math.Max(number.Minimum,number.Value-number.Increment);sync();Refresh();});
                var more=WpfFieldTheme.Button("+",()=> {if(number.IsDisposed)return;number.Value=Math.Min(number.Maximum,number.Value+number.Increment);sync();Refresh();});
                less.MinWidth=more.MinWidth=44;less.IsEnabled=more.IsEnabled=number.Enabled;
                System.Windows.Automation.AutomationProperties.SetName(less,SettingLabel(number)+" −");
                System.Windows.Automation.AutomationProperties.SetName(more,SettingLabel(number)+" +");
                input.LostKeyboardFocus+=(s,e)=>commit();input.PreviewKeyDown+=(s,e)=> {if(e.Key==Key.Enter){commit();e.Handled=true;}else if(e.Key==Key.Up||e.Key==Key.Down){if(!number.IsDisposed){number.Value=Math.Max(number.Minimum,Math.Min(number.Maximum,number.Value+(e.Key==Key.Up?number.Increment:-number.Increment)));sync();Refresh();}e.Handled=true;}};
                bool subscribed=false;EventHandler changed=(s,e)=>sync();panel.Loaded+=(s,e)=> {if(!subscribed&&!number.IsDisposed){number.ValueChanged+=changed;subscribed=true;sync();}};panel.Unloaded+=(s,e)=> {if(subscribed){number.ValueChanged-=changed;subscribed=false;}};
                row.Children.Add(less);row.Children.Add(input);row.Children.Add(more);panel.Children.Add(row);panel.Children.Add(range);return panel;
            }
            var slider=source as TacticalSlider;
            if(slider!=null) { var panel=new StackPanel();var value=WpfFieldTheme.Text(slider.Value.ToString(CultureInfo.CurrentCulture),18,WpfFieldTheme.Amber,true);value.Tag="setting-slider-value";var c=new Slider { Minimum=slider.Minimum,Maximum=slider.Maximum,Value=slider.Value,MinWidth=220,MinHeight=44,TickFrequency=1,IsSnapToTickEnabled=true,SmallChange=1,LargeChange=10,Margin=new Thickness(8,4,8,4),IsEnabled=slider.Enabled };System.Windows.Automation.AutomationProperties.SetName(c,SettingLabel(slider));c.ValueChanged+=(s,e)=> { if(slider.IsDisposed)return;slider.Value=(int)Math.Round(c.Value);value.Text=slider.Value.ToString(CultureInfo.CurrentCulture);System.Windows.Automation.AutomationProperties.SetName(c,SettingLabel(slider));Refresh(); };panel.Children.Add(value);panel.Children.Add(c);return panel; }
            var combo=source as System.Windows.Forms.ComboBox;
            if(combo!=null) { var c=new ComboBox { ItemsSource=combo.Items.Cast<object>().ToArray(),SelectedIndex=combo.SelectedIndex,MinWidth=140,Margin=new Thickness(4,4,4,12),IsEnabled=combo.Enabled };System.Windows.Automation.AutomationProperties.SetName(c,SettingLabel(combo));c.SelectionChanged+=(s,e)=> { if(combo.IsDisposed)return;combo.SelectedIndex=c.SelectedIndex;if(combo.Items.Count==Localization.LanguageDisplayNames.Length)Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,rebuild);else Refresh(); };return c; }
            var button=source as System.Windows.Forms.Button;
            if(button!=null) {
                Button b=null;b=WpfFieldTheme.Button(button.Text,()=> {
                    if(button.Text==Localization.Get("OpenStartupGuide"))ShowOnboarding();
                    else if(button.Text==Localization.Get("KeyRebindingWizard"))Modal(BuildSettings(1));
                    else if(button.Text==Localization.Get("MapZonesScreenshot"))ShowZoneWizard();
                    else if(button.Text==Localization.Get("ConfigurePoiFilters"))Modal(BuildPoiFilters());
                    else if(!button.IsDisposed)typeof(System.Windows.Forms.Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});
                    if((string)button.Tag=="swatch"&&!button.IsDisposed)RefreshSwatch(b,button);
                    Refresh();
                });b.HorizontalAlignment=HorizontalAlignment.Left;b.IsEnabled=button.Enabled;b.Margin=new Thickness(0,4,0,8);
                if((string)button.Tag=="swatch") { RefreshSwatch(b,button);b.Content="●  "+button.Text;b.MinWidth=120;System.Windows.Automation.AutomationProperties.SetName(b,button.Text); }b.Tag=button.Name;return b;
            }
            var label=source as System.Windows.Forms.Label;
            if(label!=null) { var t=WpfFieldTheme.Text(label.Text,18,label.ForeColor==OverlayTheme.InkMuted?WpfFieldTheme.Muted:WpfFieldTheme.Ink);t.TextWrapping=TextWrapping.Wrap;t.Margin=new Thickness(0,4,0,8);bool subscribed=false;EventHandler changed=(s,e)=> {if(!label.IsDisposed)t.Text=label.Text;};t.Loaded+=(s,e)=> {if(!subscribed&&!label.IsDisposed){label.TextChanged+=changed;subscribed=true;t.Text=label.Text;}};t.Unloaded+=(s,e)=> {if(subscribed){label.TextChanged-=changed;subscribed=false;}};return t; }
            if(source.Name=="AdminToolsToggle") {
                var admin=new StackPanel();
                var enabled=new CheckBox { Content=Localization.Get("AdminToolsEnabled"),IsChecked=engine.AdminToolsEnabled,Tag="admin-tools-enabled",Margin=new Thickness(0,4,0,8) };
                enabled.Click+=(s,e)=> { engine.SetAdminToolsEnabled(enabled.IsChecked==true);enabled.IsChecked=engine.AdminToolsEnabled;Refresh(); };
                Button open=null;
                open=WpfFieldTheme.Button(Localization.Get("ControlAdminCommands"),()=> {
                    // Let WPF's modal loop release its disabled thread windows
                    // before showing/reusing the native admin palette.
                    var settings=System.Windows.Window.GetWindow(open);if(settings!=null)settings.Close();
                    application.Dispatcher.BeginInvoke(new Action(()=> { engine.ShowAdminCommandsFromSettings();enabled.IsChecked=engine.AdminToolsEnabled;Refresh(); }));
                });
                open.Tag="open-admin-commands";admin.Children.Add(enabled);admin.Children.Add(open);return admin;
            }
            if(source.Controls.Count>0) { var panel=new StackPanel { Orientation=Orientation.Vertical,Margin=new Thickness(0,8,0,12) };foreach(System.Windows.Forms.Control child in source.Controls) { var c=ConvertSetting(child,rebuild);if(c!=null)panel.Children.Add(c); }return panel; }
            return null;
        }
        WpfMapSurface setupPreview,settingsPreview;
        Window BuildPoiFilters() {
            var t=WpfFieldTheme.Current;double side=t.Uppercase?34:t.GradientAccent?28:32;
            var window=Window(Localization.Get("ConfigurePoiFilters"),640,740);var root=new DockPanel();var body=new StackPanel { Margin=new Thickness(side,6,side-8,24) };Action draw=null;
            // Bulk actions sit in the footer strip (first child); Done stays in the header like a sheet.
            var footer=new StackPanel { Orientation=Orientation.Horizontal,Background=t.GradientAccent?Brushes.Transparent:WpfFieldTheme.Panel };
            Action<bool> bulk=on=> { if(engine.WpfPoiStore==null)return;engine.WpfPoiStore.SetAllEnabled(on);engine.WpfSave();draw();Refresh(); };
            var all=WpfKit.Quiet(Localization.Get("SelectAll"),()=>bulk(true),"circle-check");all.Margin=new Thickness(side-10,12,4,14);footer.Children.Add(all);
            var none=WpfKit.Quiet(Localization.Get("DeselectAll"),()=>bulk(false),"circle-x");none.Margin=new Thickness(0,12,4,14);footer.Children.Add(none);
            var shown=WpfKit.Mono("",12,WpfFieldTheme.Muted);shown.VerticalAlignment=VerticalAlignment.Center;shown.Margin=new Thickness(14,0,0,0);footer.Children.Add(shown);
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var rule=new Border { Height=1,Background=WpfFieldTheme.Line };DockPanel.SetDock(rule,Dock.Bottom);root.Children.Add(rule);
            var top=new StackPanel { Margin=new Thickness(side,t.Uppercase?24:22,side,10) };var head=new DockPanel();
            var done=WpfKit.Primary(Localization.Get("Done"),()=>window.Close(),"check");done.VerticalAlignment=VerticalAlignment.Top;done.Margin=new Thickness(16,0,0,0);DockPanel.SetDock(done,Dock.Right);head.Children.Add(done);
            var titles=new StackPanel();
            if(t.Uppercase)titles.Children.Add(new Border { Width=28,Height=3,Background=WpfFieldTheme.Accent,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10) });
            titles.Children.Add(WpfKit.Title(Localization.Get("ConfigurePoiFilters"),24));var subtitle=WpfKit.Help(Localization.Get("PoiFiltersHelp"),14);subtitle.Margin=new Thickness(0,6,0,0);titles.Children.Add(subtitle);head.Children.Add(titles);
            head.MouseLeftButtonDown+=(s,e)=> { if(e.ButtonState==MouseButtonState.Pressed&&!(e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)) { try { window.DragMove(); } catch(InvalidOperationException) {} } };
            top.Children.Add(head);
            var field=new DockPanel();var glass=WpfIcons.Create("search",16,WpfFieldTheme.Muted);glass.Margin=new Thickness(12,0,8,0);DockPanel.SetDock(glass,Dock.Left);field.Children.Add(glass);
            var query=new TextBox { MaxLength=60,Tag="poi-filter-search",BorderThickness=new Thickness(0),Background=Brushes.Transparent,MinHeight=36,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(0) };
            System.Windows.Automation.AutomationProperties.SetName(query,Localization.Get("PoiFilterSearch"));
            var placeholder=WpfFieldTheme.Text(Localization.Get("PoiFilterSearch"),14,WpfFieldTheme.Dim);placeholder.IsHitTestVisible=false;placeholder.VerticalAlignment=VerticalAlignment.Center;
            var input=new Grid();input.Children.Add(placeholder);input.Children.Add(query);field.Children.Add(input);
            var frame=new Border { Child=field,Background=t.Brush(t.Input),BorderBrush=WpfFieldTheme.Line2,BorderThickness=t.Uppercase?new Thickness(0,0,0,2):new Thickness(1),CornerRadius=new CornerRadius(t.Uppercase?0:Math.Min(12,t.ControlRadius+2)),Margin=new Thickness(0,18,0,0) };
            query.GotKeyboardFocus+=(s,e)=>frame.BorderBrush=WpfFieldTheme.Accent;query.LostKeyboardFocus+=(s,e)=>frame.BorderBrush=WpfFieldTheme.Line2;top.Children.Add(frame);
            DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
            root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0) });
            Action count=()=> {
                var store=engine.WpfPoiStore;if(store==null) { shown.Text="";return; }
                int on=0,total=0;foreach(string section in store.Sections)foreach(var category in store.CategoriesBySection[section]) { total++;if(store.IsCategoryEnabled(category.Id))on++; }
                shown.Text=Localization.T("PoiShownCount",on,total);
            };
            Action filter=()=> {
                string text=(query.Text??"").Trim();placeholder.Visibility=text.Length==0?Visibility.Visible:Visibility.Collapsed;
                foreach(var group in body.Children.OfType<FrameworkElement>()) {
                    var grid=group.Tag as UniformGrid;if(grid==null)continue;int visible=0;
                    foreach(var check in grid.Children.OfType<CheckBox>()) { var name=check.ToolTip as string??"";bool match=text.Length==0||name.IndexOf(text,StringComparison.CurrentCultureIgnoreCase)>=0;check.Visibility=match?Visibility.Visible:Visibility.Collapsed;if(match)visible++; }
                    group.Visibility=visible>0?Visibility.Visible:Visibility.Collapsed;
                }
            };
            draw=()=> {
                body.Children.Clear();var store=engine.WpfPoiStore;if(store==null) { body.Children.Add(WpfKit.Empty(Localization.Get("NoMatches"),"map-pin"));count();return; }
                foreach(string section in store.Sections) {
                    var grid=new UniformGrid { Columns=2 };var categories=store.CategoriesBySection[section];
                    int on=categories.Count(c=>store.IsCategoryEnabled(c.Id));
                    var tally=WpfKit.Mono(on+" / "+categories.Count,12,on==0?WpfFieldTheme.Dim:WpfFieldTheme.Accent);tally.VerticalAlignment=VerticalAlignment.Center;
                    foreach(var category in categories) {
                        int categoryId=category.Id;string label=Localization.GetCategoryName(category.Name);
                        var face=new DockPanel();var colour=System.Drawing.Color.FromArgb(category.ColorBackground);
                        var dot=new System.Windows.Shapes.Ellipse { Width=8,Height=8,Fill=new SolidColorBrush(Color.FromRgb(colour.R,colour.G,colour.B)),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center };DockPanel.SetDock(dot,Dock.Left);face.Children.Add(dot);
                        var name=WpfFieldTheme.Text(label,14,WpfFieldTheme.Ink);name.TextTrimming=TextTrimming.CharacterEllipsis;name.VerticalAlignment=VerticalAlignment.Center;face.Children.Add(name);
                        var check=WpfKit.Check(face,store.IsCategoryEnabled(categoryId),"setup-poi:"+categoryId);check.ToolTip=label;check.Margin=new Thickness(0,2,12,2);System.Windows.Automation.AutomationProperties.SetName(check,label);
                        check.Click+=(s,e)=> { store.SetCategoryEnabled(categoryId,check.IsChecked==true);engine.WpfSave();int now=categories.Count(c=>store.IsCategoryEnabled(c.Id));tally.Text=now+" / "+categories.Count;tally.Foreground=now==0?WpfFieldTheme.Dim:WpfFieldTheme.Accent;count();Refresh(); };
                        grid.Children.Add(check);
                    }
                    var trailing=new StackPanel { Orientation=Orientation.Horizontal };trailing.Children.Add(tally);
                    bool any=on>0;var toggle=WpfKit.Quiet(Localization.Get(any?"DeselectAll":"SelectAll"),()=> { foreach(var c in categories)store.SetCategoryEnabled(c.Id,!any);engine.WpfSave();draw();Refresh(); });
                    toggle.MinHeight=26;toggle.Padding=new Thickness(8,2,8,2);toggle.FontSize=12;toggle.Margin=new Thickness(8,0,0,0);toggle.Tag="poi-section-toggle:"+section;System.Windows.Automation.AutomationProperties.SetName(toggle,toggle.Content+" · "+Localization.GetSectionName(section));trailing.Children.Add(toggle);
                    var group=WpfKit.Section(Localization.GetSectionName(section),grid,null,trailing);group.Tag=grid;body.Children.Add(group);
                }
                count();filter();
            };
            query.TextChanged+=(s,e)=>filter();draw();window.Content=root;window.ContentRendered+=(s,e)=>query.Focus();return window;
        }
        void RefreshSetupPreview() {
            if(setupPreview==null&&settingsPreview==null)return;var state=engine.WpfSnapshot(false);if(state.Position==null)state.Position=new Position { X=0,Y=0,Yaw=90 };foreach(var preview in new[]{setupPreview,settingsPreview}) {if(preview==null)continue;preview.Width=210.0*state.HudWidth/Math.Max(state.HudWidth,state.HudHeight);preview.Height=210.0*state.HudHeight/Math.Max(state.HudWidth,state.HudHeight);preview.State=state;preview.InvalidateVisual();}
        }
        sealed class GuideChecklistState { internal readonly bool[] Read=new bool[6];internal bool NoNumpad; }
        // Getting-started checklist: keyboard choice first, then six numbered confirmations worked top to bottom.
        // The SCUM menu path and the key capture sit under the line they belong to.
        FrameworkElement BuildGuideChecklist(GuideChecklistState state,Action<bool> readyChanged) {
            var root=new StackPanel();Action draw=null;
            draw=()=> {
                root.Children.Clear();
                root.Children.Add(WpfKit.Note(Localization.Get("GuideSwitchApps")));
                var choices=new UniformGrid { Columns=2 };string group="numpad-"+Guid.NewGuid().ToString("N");
                foreach(bool noPad in new[]{false,true}) {
                    bool selected=noPad;var face=new DockPanel();
                    var tile=WpfKit.IconTile(noPad?"keyboard":"grid-3x3",null,34);tile.Margin=new Thickness(0,0,12,0);tile.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(tile,Dock.Left);face.Children.Add(tile);
                    var label=WpfKit.Strong(Localization.Get(noPad?"GuideNoNumpad":"GuideHaveNumpad"),14);label.VerticalAlignment=VerticalAlignment.Center;face.Children.Add(label);
                    var choice=WpfKit.ChoiceCard(face,state.NoNumpad==noPad,group,noPad?"guide-no-numpad":"guide-have-numpad");choice.Margin=new Thickness(noPad?5:0,0,noPad?0:5,0);
                    choice.Checked+=(s,e)=> { if(state.NoNumpad==selected)return;state.NoNumpad=selected;state.Read[2]=state.Read[3]=false;draw(); };choices.Children.Add(choice);
                }
                root.Children.Add(WpfKit.Section(Localization.Get("GuideKeyboardTitle"),choices));
                var binding=engine.WpfSnapshot(false);bool defaultKey=binding.CopyKey==Program.DefaultCopyKey&&binding.CopyModifier==0;
                bool keyReady=state.NoNumpad?binding.CopyKey!=Program.DefaultCopyKey&&binding.CopyModifier==0:defaultKey;
                if(!keyReady)state.Read[3]=false;
                var progress=WpfKit.Mono("",13,WpfFieldTheme.Accent);progress.Tag="guide-progress";progress.TextTrimming=TextTrimming.None;progress.Margin=new Thickness(0,0,10,0);
                var meter=WpfKit.Meter(0,state.Read.Length);meter.Width=84;
                var trailing=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };trailing.Children.Add(progress);trailing.Children.Add(meter);
                Action sync=()=> { int count=state.Read.Count(value=>value);progress.Text=count+" / "+state.Read.Length;WpfKit.SetMeter(meter,count,state.Read.Length);readyChanged(count==state.Read.Length&&keyReady); };
                var list=new StackPanel();
                for(int i=0;i<6;i++) {
                    int index=i;string key=i==0?"GuideExtracted":i==1?"GuideWindowed":i==2?(state.NoNumpad?"GuideScumOther":"GuideScumNumpad"):i==3?(state.NoNumpad?"GuideMiniOther":"GuideMiniNumpad"):i==4?"GuideCloseSettings":"GuideTrackingTest";
                    var text=WpfFieldTheme.Text(Localization.Get(key),15);text.TextWrapping=TextWrapping.Wrap;
                    var check=new CheckBox { Content=text,IsChecked=state.Read[i],Tag="guide-read-"+i };
                    var details=new List<UIElement>();
                    if(i==2)details.Add(WpfKit.Breadcrumb(new[]{"SCUM","OPTIONS","MOUSE & KEYBOARD","CONTROLS","UI","COPY LOCATION","APPLY"},5,6));
                    if(i==3) {
                        string current=(binding.CopyModifier==0?"":PhysicalKeyCapture.KeyName(binding.CopyModifier)+" + ")+PhysicalKeyCapture.KeyName(binding.CopyKey);
                        var keyRow=new WrapPanel();var caption=WpfFieldTheme.Text(Localization.Get("WizardKeyCopy"),13,WpfFieldTheme.Muted);caption.Margin=new Thickness(0,0,10,0);keyRow.Children.Add(caption);
                        keyRow.Children.Add(WpfKit.KeyCap(current,"guide-current-key"));
                        Button action=null;
                        if(state.NoNumpad) {
                            action=WpfKit.Primary(Localization.Get("WizardBtnCapture"),()=> {
                                CaptureKey(Localization.Get("WizardKeyCopy"),(vk,scan)=> { if(vk==Program.DefaultCopyKey)return Localization.Get("GuideNoNumpad");string error=engine.WpfGameBind(2,vk,scan);if(error==null)engine.WpfSingleCopyKey(true);return error; },false);
                                state.Read[3]=false;draw();
                            },"keyboard");action.Tag="guide-capture-copy";action.IsEnabled=state.Read[2];
                        } else if(!defaultKey) {
                            action=WpfKit.Secondary(Localization.Get("GuideUseNumpad"),()=> { string error=engine.WpfGameBind(2,Program.DefaultCopyKey,0);if(error!=null) { ShowZoneMessage(error);return; }engine.WpfSingleCopyKey(true);state.Read[3]=false;draw(); },"rotate-ccw");action.Tag="guide-use-numpad";
                        }
                        if(action!=null) { action.MinHeight=32;action.Padding=new Thickness(12,4,14,4);action.Margin=new Thickness(14,0,0,0);keyRow.Children.Add(action); }
                        details.Add(keyRow);
                    }
                    var card=WpfKit.CheckItem(check,(i+1).ToString("00",CultureInfo.InvariantCulture),details.ToArray());
                    if(i==3)check.IsEnabled=keyReady;
                    check.Click+=(s,e)=> { state.Read[index]=check.IsChecked==true;if(index==2&&state.NoNumpad)draw();else sync(); };list.Children.Add(card);
                }
                root.Children.Add(WpfKit.Section(Localization.Get("GuideChecklistTitle"),list,Localization.Get("GuideChecklistIntro"),trailing));
                sync();
            };draw();return root;
        }
        Window BuildFirstTimeGuide() {
            var window=Window(Localization.Get("FirstTimeGuideTitle"),860,800);var dialog=new WpfKit.Dialog(window,Localization.Get("FirstTimeGuideTitle"),WpfKit.Plain(Localization.Get("WpfGettingStarted")));dialog.AddClose(Localization.Get("Close"));
            var done=WpfKit.Primary(Localization.Get("Done"),()=> { if(engine.WpfCompleteOnboarding())window.Close();else WpfKit.Ask(window,Localization.Get("LocalSaveFailed"),Localization.Get("WizardTitle"),MessageBoxButton.OK,MessageBoxImage.Warning); },"check");done.Tag="guide-done";done.IsEnabled=false;done.MinWidth=140;
            var hint=WpfFieldTheme.Text(Localization.Get("GuideFinishToContinue"),13,WpfFieldTheme.Muted);hint.VerticalAlignment=VerticalAlignment.Center;dialog.AddFooterLeft(hint);dialog.AddAction(done);
            var state=new GuideChecklistState();dialog.Add(BuildGuideChecklist(state,ready=> { done.IsEnabled=ready;hint.Visibility=ready?Visibility.Collapsed:Visibility.Visible; }));
            return window;
        }
        void ShowSetupWindow(bool detailed) {
            if(disposed)return;
            if(setupWindow!=null) {setupWindow.WindowState=WindowState.Normal;setupWindow.Topmost=true;setupWindow.Show();FocusInteractive(setupWindow);return;}
            if(tactical.IsVisible) {tactical.Hide();engine.WpfTactical(false);}
            // Automatic setup and Settings use one complete flow: the checklist
            // leads into appearance, map layers, tracking and sharing settings.
            var window=BuildOnboarding(0);setupWindow=window;
            window.Topmost=true;window.Owner=null;window.ShowInTaskbar=true;window.WindowStyle=WindowStyle.SingleBorderWindow;window.ResizeMode=ResizeMode.CanResize;window.ShowActivated=!diagnostic;
            if(diagnostic) {window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-32000;window.Top=-32000;}
            window.Activated+=(s,e)=>SetSetupInputFocus(true);window.Deactivated+=(s,e)=>SetSetupInputFocus(false);
            window.Loaded+=(s,e)=>FocusInteractive(window);
            window.ContentRendered+=(s,e)=>FocusInteractive(window);
            window.Closed+=(s,e)=> {setupWindow=null;setupHasFocus=false;SyncModalInput();RefreshInputOwnership();engine.WpfSave();if(!disposed)Refresh();};
            window.Show();RefreshInputOwnership();
        }
        void ShowOnboarding() { ShowSetupWindow(true); }
        internal void ShowZoneWizard(string file=null) {
            if(!application.Dispatcher.CheckAccess()) { application.Dispatcher.BeginInvoke(new Action(()=>ShowZoneWizard(file)));return; }
            if(file!=null&&Path.GetExtension(file).Equals(".tsv",StringComparison.OrdinalIgnoreCase)) { ShowZoneFile(file);return; }
            Modal(BuildZoneWizard(file));
        }
        internal void ShowZoneAssociation() {
            try { ZoneFileOpen.Register(System.Windows.Forms.Application.ExecutablePath);ShowZoneMessage(Localization.Get("ZoneAssociationInfo")); }
            catch(Exception error) { Program.LogException("ZoneFileAssociation",error);ShowZoneMessage(Localization.T("ZoneOpenError",error.Message)); }
        }
        void ShowZoneMessage(string message) {
            var window=Window(Localization.Get("ZoneEditorTitle"),560,300);var dialog=new WpfKit.Dialog(window,Localization.Get("ZoneEditorTitle"));
            dialog.Add(WpfKit.Note(message,"warn"));dialog.AddAction(WpfKit.Primary(Localization.Get("Done"),()=>window.Close()));Modal(window);
        }
        async void ShowZoneFile(string file) {
            if(zoneFileActive||disposed)return;zoneFileActive=true;
            try {
                var imported=await System.Threading.Tasks.Task.Run(()=> { if(!Path.GetExtension(file).Equals(".tsv",StringComparison.OrdinalIgnoreCase)||new FileInfo(file).Length>32L*1024*1024)throw new InvalidDataException("Invalid zone TSV file.");return ZoneStore.Load(file); });
                if(disposed)return;if(imported.Count==0)throw new InvalidDataException("The file contains no zones.");
                string initial=Path.GetFileNameWithoutExtension(file);if(initial.Equals("zones",StringComparison.OrdinalIgnoreCase))initial=Path.GetFileName(Path.GetDirectoryName(file));
                string layer=null;Modal(BuildZonePackName(initial,value=>layer=value));if(layer==null||disposed)return;
                var wizard=BuildZoneWizard();try { ((ZoneEditor)wizard.Tag).WpfImportZonePack(imported,layer);Modal(wizard); }finally { wizard.Close(); }
            }catch(Exception error) { Program.LogException("ZoneFileImport",error);if(!disposed)ShowZoneMessage(Localization.T("ZoneOpenError",error.Message)); }
            finally { zoneFileActive=false; }
        }
        Window BuildZonePackName(string initial,Action<string> accepted) {
            var window=Window(Localization.Get("NameThisMap"),520,270);window.ResizeMode=ResizeMode.NoResize;var dialog=new WpfKit.Dialog(window,Localization.Get("NameThisMap"));
            var input=WpfKit.Input(initial==null?"":initial.Substring(0,Math.Min(80,initial.Length)),"zone-pack-name",80);dialog.Add(input);
            var cancel=WpfKit.Quiet(Localization.Get("Cancel"),()=>window.Close());dialog.AddAction(cancel);
            var add=WpfKit.Primary(Localization.Get("ZeImportFile"),()=> { if(string.IsNullOrWhiteSpace(input.Text))return;accepted(input.Text.Trim());window.Close(); },"upload");add.Tag="zone-pack-accept";add.IsEnabled=!string.IsNullOrWhiteSpace(input.Text);dialog.AddAction(add);
            input.TextChanged+=(s,e)=>add.IsEnabled=!string.IsNullOrWhiteSpace(input.Text);input.KeyDown+=(s,e)=> { if(e.Key==Key.Enter&&add.IsEnabled) { accepted(input.Text.Trim());window.Close();e.Handled=true; }else if(e.Key==Key.Escape)window.Close(); };window.Loaded+=(s,e)=> { input.Focus();input.SelectAll(); };return window;
        }
        Window BuildZoneWizard(string file=null) {
            var editor=engine.WpfZoneEditor();var nativeHandle=editor.Handle;var window=Window(Localization.Get("ZoneEditorTitle"),1450,920);window.MinWidth=640;window.MinHeight=420;window.Tag=editor;
            editor.WpfOwner=new WpfWindowOwner(new WindowInteropHelper(window).EnsureHandle());
            var th=WpfFieldTheme.Current;Func<string,string> clean=text=>(text??"").Trim().TrimEnd(':','\uFF1A','.','\u2026','\u3002').Trim();
            // Header: title on the left, Cancel and Save on the right; the live instruction sits under it as a callout.
            var root=new DockPanel { Margin=new Thickness(24,20,24,20) };var header=new DockPanel { Margin=new Thickness(0,0,0,12) };
            var headerActions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top };
            var close=WpfKit.Quiet(Localization.Get("Cancel"),()=>window.Close(),"x");close.Margin=new Thickness(0,0,8,0);headerActions.Children.Add(close);
            var save=WpfKit.Primary(Localization.Get("ZeSaveZones"),()=>editor.WpfClick(editor.WpfAction("ZeSaveZones")),"save");save.Tag="ZeSaveZones";headerActions.Children.Add(save);
            DockPanel.SetDock(headerActions,Dock.Right);header.Children.Add(headerActions);
            var titles=new StackPanel();if(th.Uppercase)titles.Children.Add(new Border { Width=28,Height=3,Background=WpfFieldTheme.Accent,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10) });
            titles.Children.Add(WpfKit.Title(Localization.Get("ZoneEditorTitle"),24));header.Children.Add(titles);
            header.MouseLeftButtonDown+=(s,e)=> { if(e.OriginalSource is TextBlock||e.OriginalSource is DockPanel)window.DragMove(); };DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var instruction=WpfFieldTheme.Text(editor.WpfInstruction,14,WpfFieldTheme.Ink);instruction.TextWrapping=TextWrapping.Wrap;instruction.TextTrimming=TextTrimming.None;
            var hintRow=new DockPanel();var hintIcon=WpfIcons.Create("info",16,WpfFieldTheme.Accent);hintIcon.Margin=new Thickness(0,1,10,0);hintIcon.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(hintIcon,Dock.Left);hintRow.Children.Add(hintIcon);
            hintRow.Children.Add(new ScrollViewer { Content=instruction,MaxHeight=72,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
            var hint=new Border { Child=hintRow,Background=WpfFieldTheme.AccentDim,BorderBrush=th.Alpha(th.Accent,0x55),BorderThickness=th.Uppercase?new Thickness(3,0,0,0):new Thickness(1),CornerRadius=new CornerRadius(th.Uppercase?0:Math.Min(10,th.ControlRadius)),Padding=new Thickness(14,10,14,10),Margin=new Thickness(0,0,0,16) };
            DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
            var workspace=new Grid();workspace.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(250) });workspace.ColumnDefinitions.Add(new ColumnDefinition());workspace.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(290) });root.Children.Add(workspace);window.SizeChanged+=(s,e)=> { workspace.ColumnDefinitions[0].Width=new GridLength(Math.Min(250,Math.Max(170,window.ActualWidth*.2)));workspace.ColumnDefinitions[2].Width=new GridLength(Math.Min(290,Math.Max(190,window.ActualWidth*.24))); };
            var tools=new StackPanel();var toolScroll=new ScrollViewer { Content=tools,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,6,0) };workspace.Children.Add(WpfKit.Card(toolScroll,new Thickness(16,8,10,14)));
            var sourceCanvas=new WpfZoneCanvas(editor,true);var targetCanvas=new WpfZoneCanvas(editor,false);
            Action redraw=()=> { sourceCanvas.Redraw();targetCanvas.Redraw(); };
            bool syncing=false;var buttons=new List<KeyValuePair<Button,System.Windows.Forms.Control>>();
            var icons=new Dictionary<string,string> { {"ZeOpenScreenshot","scan"},{"ZeImportFile","folder-open"},{"ZeExport","share-2"},{"ZeManualFallback","hand"},{"ZeNewLayer","plus"},{"ZeDeleteLayer","trash-2"},{"ZeUndoPoint","undo-2"},{"ZeClearDraft","circle-x"},{"ControlResetZoom","maximize-2"},
                {"ZeZoneColour","palette"},{"ZeAddZone","plus"},{"ZeApplyName","check"},{"ZeDuplicate","copy"},{"ZeMoveUp","chevron-up"},{"ZeMoveDown","chevron-down"},{"ZeDeselect","square-dashed"},{"ZeDeleteSelected","trash-2"},{"ZeDeleteAll","trash-2"},{"ZeChangeShape","pencil"} };
            Func<string,string,Button> action=(key,kind)=> {
                var native=editor.WpfAction(key);Action click=()=> { if(key=="ZeZoneColour")editor.WpfChooseOutlineColour();else editor.WpfClick(native);redraw(); };string icon;icons.TryGetValue(key,out icon);
                // Labels such as "+ Layer" already carry a symbol; the icon replaces it.
                string text=Localization.Get(key);if(icon!=null)text=text.TrimStart('+','\uFF0B',' ').Trim();
                var button=kind=="primary"?WpfKit.Primary(text,click,icon):kind=="danger"?WpfKit.Danger(text,click,icon):kind=="quiet"?WpfKit.Quiet(text,click,icon):WpfKit.Secondary(text,click,icon);
                button.Tag=key;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=kind=="quiet"?HorizontalAlignment.Left:HorizontalAlignment.Center;button.Margin=new Thickness(0,3,0,3);button.MinWidth=0;
                buttons.Add(new KeyValuePair<Button,System.Windows.Forms.Control>(button,native));return button;
            };
            Func<UIElement,UIElement,UniformGrid> pair=(one,two)=> { var grid=new UniformGrid { Columns=2 };var a1=(FrameworkElement)one;var a2=(FrameworkElement)two;a1.Margin=new Thickness(0,3,3,3);a2.Margin=new Thickness(3,3,0,3);grid.Children.Add(one);grid.Children.Add(two);return grid; };
            Func<string,string,FrameworkElement> step=(number,title)=> {
                var row=new DockPanel { Margin=new Thickness(0,14,0,8) };var badge=WpfKit.Mono(number,12,WpfFieldTheme.Accent);badge.Margin=new Thickness(0,0,10,0);badge.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(badge,Dock.Left);row.Children.Add(badge);
                var label=WpfFieldTheme.Text(th.Caps(title),th.Uppercase?15:14,WpfFieldTheme.Ink,true);if(th.Uppercase) { label.FontFamily=WpfFieldTheme.DisplayFont;label.FontWeight=FontWeights.Bold; }label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);return row;
            };
            tools.Children.Add(step("01",Localization.Get("ZeStepImport")));
            tools.Children.Add(action("ZeOpenScreenshot","secondary"));tools.Children.Add(action("ZeImportFile","secondary"));tools.Children.Add(action("ZeExport","secondary"));tools.Children.Add(action("ZeManualFallback","quiet"));
            var layer=new ComboBox { MinHeight=36 };layer.SelectionChanged+=(s,e)=> { if(!syncing&&layer.SelectedIndex>=0) { editor.WpfLayer.SelectedIndex=layer.SelectedIndex;redraw(); } };
            var layerField=(FrameworkElement)WpfKit.Field(clean(Localization.Get("ZeLayer")),layer);layerField.Margin=new Thickness(0,10,0,4);tools.Children.Add(layerField);
            tools.Children.Add(pair(action("ZeNewLayer","quiet"),action("ZeDeleteLayer","quiet")));
            tools.Children.Add(step("02",Localization.Get("ZeStepDraw")));var drawButtons=new List<Button>();var drawGrid=new UniformGrid { Columns=2 };
            for(int i=0;i<editor.WpfDrawingTool.Items.Count;i++) {
                int tool=i;var button=WpfKit.Secondary(editor.WpfDrawingTool.Items[i].ToString(),()=> { editor.WpfDrawingTool.SelectedIndex=tool;redraw(); });button.HorizontalAlignment=HorizontalAlignment.Stretch;button.MinWidth=0;button.Padding=new Thickness(8,6,8,6);button.Margin=new Thickness(i%2==0?0:3,3,i%2==0?3:0,3);button.FontSize=th.Uppercase?13:13;
                drawButtons.Add(button);drawGrid.Children.Add(button);
            }
            tools.Children.Add(drawGrid);
            foreach(var key in new[]{"ZeUndoPoint","ZeClearDraft","ControlResetZoom"})tools.Children.Add(action(key,"quiet"));
            var mapGrid=new Grid { Margin=new Thickness(16,0,16,0) };mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(0) });mapGrid.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(mapGrid,1);workspace.Children.Add(mapGrid);
            Func<string,FrameworkElement,DockPanel> stage=(title,canvas)=> {
                var panel=new DockPanel();var label=WpfKit.Label(title);label.Margin=new Thickness(2,0,0,8);DockPanel.SetDock(label,Dock.Top);panel.Children.Add(label);
                panel.Children.Add(new Border { Child=canvas,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Background=WpfFieldTheme.Panel,CornerRadius=new CornerRadius(th.Uppercase?0:Math.Min(12,th.ControlRadius)),ClipToBounds=true });return panel;
            };
            var sourcePanel=stage(Localization.Get("ZeSourceTitle"),sourceCanvas);sourcePanel.Margin=new Thickness(0,0,12,0);mapGrid.Children.Add(sourcePanel);
            var targetPanel=stage(Localization.Get("ZeTargetTitle"),targetCanvas);Grid.SetColumn(targetPanel,1);mapGrid.Children.Add(targetPanel);
            var review=new StackPanel();var reviewScroll=new ScrollViewer { Content=review,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,6,0) };var reviewCard=WpfKit.Card(reviewScroll,new Thickness(16,8,10,14));Grid.SetColumn(reviewCard,2);workspace.Children.Add(reviewCard);
            review.Children.Add(step("03",Localization.Get("ZeStepReview")));
            var filter=WpfKit.Input(editor.WpfFilter.Text);System.Windows.Automation.AutomationProperties.SetName(filter,clean(Localization.Get("ZeFilterZones")));filter.TextChanged+=(s,e)=> { if(!syncing)editor.WpfFilter.Text=filter.Text; };
            var filterField=(FrameworkElement)WpfKit.Field(clean(Localization.Get("ZeFilterZones")),filter);filterField.Margin=new Thickness(0,0,0,8);review.Children.Add(filterField);
            var zoneTypes=Enum.GetValues(typeof(ZoneCategory)).Cast<ZoneCategory>().ToArray();
            var typeKeys=new[]{"ZeTypeCity","ZeTypeTown","ZeTypeFarm","ZeTypeTrader","ZeTypeFaction","ZeTypeMilitary","ZeTypeBunker","ZeTypeGasStation","ZeTypeCustom","ZeTypeUnknown"};
            var typeFilter=new ComboBox { Tag="zone-type-filter",ItemsSource=new[]{Localization.Get("ZeAllTypes")}.Concat(typeKeys.Select(Localization.Get)).ToArray(),SelectedIndex=0,Margin=new Thickness(0,0,0,10),MinHeight=36 };
            typeFilter.SelectionChanged+=(s,e)=> { if(!syncing) { editor.WpfCategoryFilter=typeFilter.SelectedIndex<=0?(ZoneCategory?)null:zoneTypes[typeFilter.SelectedIndex-1];redraw(); } };review.Children.Add(typeFilter);
            var zonesList=new ListBox { Tag="zone-selection-list",SelectionMode=SelectionMode.Extended,MinHeight=160,MaxHeight=260 };zonesList.SelectionChanged+=(s,e)=> { if(!syncing) { editor.WpfSelectVisible(zonesList.SelectedItems.Cast<object>().Select(item=>zonesList.Items.IndexOf(item)).ToArray());redraw(); } };review.Children.Add(zonesList);
            var selectionHelp=WpfKit.Help(Localization.Get("ZeMultiHelp"),12);selectionHelp.Margin=new Thickness(0,6,0,6);review.Children.Add(selectionHelp);
            var selectAll=WpfKit.Quiet(Localization.Get("SelectAll"),()=> { zonesList.SelectAll(); },"circle-check");selectAll.Tag="zone-select-all";
            var selectColour=WpfKit.Quiet(Localization.Get("ZeSelectSameColour"),()=> { editor.WpfSelectSameColour();redraw(); },"palette");selectColour.Tag="zone-select-colour";
            foreach(var quick in new[]{selectAll,selectColour}) { quick.HorizontalAlignment=HorizontalAlignment.Stretch;quick.HorizontalContentAlignment=HorizontalAlignment.Left;quick.MinWidth=0; }
            selectAll.Margin=new Thickness(0,2,0,0);selectColour.Margin=new Thickness(0,0,0,2);review.Children.Add(selectAll);review.Children.Add(selectColour);
            review.Children.Add(step("04",Localization.Get("ZeSelectedTitle")));
            var details=WpfKit.Help("",12);details.Margin=new Thickness(0,0,0,8);review.Children.Add(details);
            var zoneName=WpfKit.Input(editor.WpfName.Text,null,80);System.Windows.Automation.AutomationProperties.SetName(zoneName,Localization.Get("ZeZoneName"));zoneName.TextChanged+=(s,e)=> { if(!syncing)editor.WpfName.Text=zoneName.Text; };zoneName.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter) { editor.WpfNameEnter();redraw();e.Handled=true; } };
            var nameField=(FrameworkElement)WpfKit.Field(Localization.Get("ZeZoneName"),zoneName);nameField.Margin=new Thickness(0,0,0,6);review.Children.Add(nameField);
            var outline=action("ZeZoneColour","secondary");
            var fillColour=WpfKit.Secondary(Localization.Get("ZoneFillColour"),()=> { editor.WpfChooseFillColour();redraw(); },"droplet");fillColour.HorizontalAlignment=HorizontalAlignment.Stretch;fillColour.MinWidth=0;
            outline.Margin=new Thickness(0,3,0,3);fillColour.Margin=new Thickness(0,3,0,3);review.Children.Add(outline);review.Children.Add(fillColour);
            var fillOpacityLabel=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);fillOpacityLabel.Margin=new Thickness(0,8,0,0);review.Children.Add(fillOpacityLabel);var fillOpacity=new Slider { Minimum=0,Maximum=100,Value=16,IsSnapToTickEnabled=true,TickFrequency=1,Margin=new Thickness(0,4,0,8) };fillOpacity.ValueChanged+=(s,e)=> { if(!syncing) { editor.WpfSetFillOpacity((int)fillOpacity.Value);redraw(); } };review.Children.Add(fillOpacity);
            var fill=WpfKit.Check(Localization.Get("ZeFillZones"),true);fill.Margin=new Thickness(0,0,0,8);fill.Checked+=(s,e)=> { var native=editor.WpfAction("ZeFillZones") as System.Windows.Forms.CheckBox;if(native!=null)native.Checked=true;redraw(); };fill.Unchecked+=(s,e)=> { var native=editor.WpfAction("ZeFillZones") as System.Windows.Forms.CheckBox;if(native!=null)native.Checked=false;redraw(); };review.Children.Add(fill);
            var addZone=action("ZeAddZone","primary");addZone.Margin=new Thickness(0,6,0,3);review.Children.Add(addZone);
            review.Children.Add(pair(action("ZeApplyName","secondary"),action("ZeDuplicate","secondary")));
            review.Children.Add(pair(action("ZeMoveUp","quiet"),action("ZeMoveDown","quiet")));
            review.Children.Add(action("ZeDeselect","quiet"));
            var nativeShape=editor.WpfAction("ZoneShape") as System.Windows.Forms.ComboBox;var shape=new ComboBox { ItemsSource=nativeShape.Items.Cast<object>().ToArray(),SelectedIndex=0,Margin=new Thickness(0,8,0,3),MinHeight=36 };shape.SelectionChanged+=(s,e)=> { if(shape.SelectedIndex>=0)nativeShape.SelectedIndex=shape.SelectedIndex; };
            review.Children.Add(shape);review.Children.Add(action("ZeChangeShape","secondary"));
            var dangerRule=new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(0,12,0,8) };review.Children.Add(dangerRule);
            review.Children.Add(pair(action("ZeDeleteSelected","danger"),action("ZeDeleteAll","danger")));
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };string previousInstruction=null,previousDetails=null;
            Action sync=()=> { if(editor.IsDisposed)return;syncing=true;try {
                var layers=editor.WpfLayer.Items.Cast<object>().ToArray();if(!layer.Items.Cast<object>().SequenceEqual(layers))layer.ItemsSource=layers;layer.SelectedIndex=editor.WpfLayer.SelectedIndex;
                var items=editor.WpfList.Items.Cast<object>().ToArray();if(!zonesList.Items.Cast<object>().SequenceEqual(items))zonesList.ItemsSource=items;
                var selected=editor.WpfSelectedVisible;if(!zonesList.SelectedItems.Cast<object>().Select(item=>zonesList.Items.IndexOf(item)).OrderBy(i=>i).SequenceEqual(selected)) { zonesList.UnselectAll();foreach(int i in selected)zonesList.SelectedItems.Add(zonesList.Items[i]); }
                if(zoneName.Text!=editor.WpfName.Text)zoneName.Text=editor.WpfName.Text;
                instruction.Text=editor.WpfInstruction;details.Text=editor.WpfDetails;details.Visibility=string.IsNullOrWhiteSpace(details.Text)?Visibility.Collapsed:Visibility.Visible;
                if(previousInstruction!=instruction.Text||previousDetails!=details.Text||editor.WpfBusy)redraw();previousInstruction=instruction.Text;previousDetails=details.Text;
                mapGrid.ColumnDefinitions[0].Width=editor.WpfHasScreenshot?new GridLength(1,GridUnitType.Star):new GridLength(0);sourcePanel.Visibility=editor.WpfHasScreenshot?Visibility.Visible:Visibility.Collapsed;
                foreach(var entry in buttons) { entry.Key.IsEnabled=entry.Value!=null&&!entry.Value.IsDisposed&&entry.Value.Enabled&&!editor.WpfBusy;if((string)entry.Key.Tag=="ZeZoneColour") { var colour=editor.WpfColour;entry.Key.Background=new SolidColorBrush(Color.FromRgb(colour.R,colour.G,colour.B));entry.Key.Foreground=colour.GetBrightness()>.5?Brushes.Black:Brushes.White; } }
                for(int i=0;i<drawButtons.Count;i++) {
                    bool on=i==editor.WpfDrawingTool.SelectedIndex;drawButtons[i].Background=on?WpfFieldTheme.AccentDim:WpfFieldTheme.Raised;drawButtons[i].Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Ink;drawButtons[i].BorderBrush=on?WpfFieldTheme.Accent:WpfFieldTheme.Line2;
                    drawButtons[i].IsEnabled=!editor.WpfBusy&&((i!=3&&i!=5)||selected.Length<=1);
                }
                shape.IsEnabled=!editor.WpfBusy&&selected.Length==1;var shapeButton=buttons.First(p=>(string)p.Key.Tag=="ZeChangeShape").Key;shapeButton.IsEnabled=shape.IsEnabled;
                save.IsEnabled=!editor.WpfBusy;zoneName.IsEnabled=filter.IsEnabled=layer.IsEnabled=zonesList.IsEnabled=typeFilter.IsEnabled=selectAll.IsEnabled=selectColour.IsEnabled=!editor.WpfBusy;
                foreach(var entry in buttons.Where(p=>(string)p.Key.Tag=="ZeMoveUp"||(string)p.Key.Tag=="ZeMoveDown"))entry.Key.IsEnabled=!editor.WpfBusy&&selected.Length==1;
                var fillColor=editor.WpfFillColour;fillColour.Background=new SolidColorBrush(Color.FromRgb(fillColor.R,fillColor.G,fillColor.B));fillColour.Foreground=fillColor.GetBrightness()>.5?Brushes.Black:Brushes.White;fillOpacity.Value=Math.Round(fillColor.A*100.0/255);fillOpacityLabel.Text=Localization.Get("ZoneFillOpacity")+" · "+fillOpacity.Value+"%";fillColour.IsEnabled=fillOpacity.IsEnabled=!editor.WpfBusy;
            }finally { syncing=false; } };
            timer.Tick+=(s,e)=>sync();editor.FormClosed+=(s,e)=> { if(window.IsVisible)window.Close(); };
            window.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&!(Keyboard.FocusedElement is TextBox)) { editor.WpfClick(editor.WpfAction("ZeUndoPoint"));redraw();e.Handled=true; } };
            window.Loaded+=async(s,e)=> { timer.Start();if(!string.IsNullOrEmpty(file))await editor.ImportAutomatic(file); };
            window.Closing+=(s,e)=> { if(editor.WpfBusy)e.Cancel=true; };
            window.Closed+=(s,e)=> { timer.Stop();editor.Close();editor.Dispose();Refresh(); };window.Content=root;sync();return window;
        }
        static void RefreshSwatch(Button target,System.Windows.Forms.Button source) { var color=source.BackColor;target.Background=new SolidColorBrush(Color.FromArgb(color.A,color.R,color.G,color.B));target.ToolTip=System.Drawing.ColorTranslator.ToHtml(color); }
        // Setup flow: a stepper rail (product, steps, language) beside one page at a time, with the step
        // header on top and Back/Next pinned in a footer.
        Window BuildOnboarding(int initial) {
            engine.WpfRebuildSettings();var t=WpfFieldTheme.Current;
            var window=Window(Localization.Get("WizardTitle"),1100,720);window.MinWidth=600;window.MinHeight=420;
            var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(260) });layout.ColumnDefinitions.Add(new ColumnDefinition());
            var rail=new DockPanel { LastChildFill=true };layout.Children.Add(new Border { Child=rail,Background=t.GradientAccent?WpfFieldTheme.Glass:WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,1,0) });
            var brand=new StackPanel { Margin=new Thickness(24,28,20,26) };var mark=new DockPanel();var logo=WpfKit.IconTile("map",null,32);logo.Margin=new Thickness(0,0,11,0);DockPanel.SetDock(logo,Dock.Left);mark.Children.Add(logo);
            var product=WpfFieldTheme.Text("SKYNETT",t.Uppercase?24:18,WpfFieldTheme.Ink,true);if(t.Uppercase) { product.FontFamily=WpfFieldTheme.DisplayFont;product.FontWeight=FontWeights.Bold; }mark.Children.Add(product);brand.Children.Add(mark);
            var tagline=WpfFieldTheme.Text("",12,WpfFieldTheme.Muted);tagline.TextWrapping=TextWrapping.Wrap;tagline.TextTrimming=TextTrimming.None;tagline.Margin=new Thickness(0,10,0,0);brand.Children.Add(tagline);DockPanel.SetDock(brand,Dock.Top);rail.Children.Add(brand);
            Action draw=null;var language=new ComboBox { ItemsSource=Localization.LanguageDisplayNames,SelectedIndex=(int)Localization.Current };language.SelectionChanged+=(s,e)=> { Localization.Current=Localization.LanguageAt(language.SelectedIndex);engine.WpfRebuildSettings();filterIdentity=null;Refresh();if(draw!=null)draw(); };
            var languageLabel=WpfKit.Label(Localization.Get("Language"));var languageBox=new StackPanel { Margin=new Thickness(22,12,22,22) };languageBox.Children.Add(languageLabel);languageBox.Children.Add(language);DockPanel.SetDock(languageBox,Dock.Bottom);rail.Children.Add(languageBox);
            var steps=new StackPanel { Margin=new Thickness(14,0,14,0) };rail.Children.Add(new ScrollViewer { Content=steps,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            var page=new DockPanel();Grid.SetColumn(page,1);layout.Children.Add(page);
            var head=new StackPanel { Margin=new Thickness(40,30,40,0) };DockPanel.SetDock(head,Dock.Top);page.Children.Add(head);
            var footer=new DockPanel { LastChildFill=false };var footerFrame=new Border { Child=footer,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(40,14,40,16),Background=t.GradientAccent?Brushes.Transparent:WpfFieldTheme.Panel };DockPanel.SetDock(footerFrame,Dock.Bottom);page.Children.Add(footerFrame);
            var content=new StackPanel { Margin=new Thickness(40,8,32,28) };var pageScroll=new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0) };page.Children.Add(pageScroll);
            Action arrange=null;
            window.SizeChanged+=(s,e)=> {
                bool narrow=window.ActualWidth<900;double side=narrow?18:40;layout.ColumnDefinitions[0].Width=new GridLength(narrow?190:260);
                head.Margin=new Thickness(side,narrow?20:30,side,0);content.Margin=new Thickness(side,8,side-8,28);footerFrame.Padding=new Thickness(side,14,side,16);if(arrange!=null)arrange();
            };
            int step=Math.Max(0,Math.Min(4,initial)),visited=step;var checklist=new GuideChecklistState();bool guideReady=false;Button wizardNext=null;TextBlock footerHint=null;int pageGeneration=0;
            string[] stages={"FirstTimeGuideTitle","FieldAppearance","SecMapZones","SecTrackingZoom","PrivatePositions"};
            Func<string,string> stageName=key=>System.Text.RegularExpressions.Regex.Replace(Localization.Get(key),@"^\s*\d+[.\s]+","");
            Func<string,TextBlock> intro=key=> { var block=WpfKit.Help(Localization.Get(key),15);block.Margin=new Thickness(0,6,0,6);return block; };
            draw=()=> {
                pageScroll.ScrollToTop();int drawing=++pageGeneration;window.Title=Localization.Get("WizardTitle");tagline.Text=WpfKit.Plain(Localization.Get("WpfGettingStarted"));languageLabel.Text=t.Caps(Localization.Get("Language"));
                setupPreview=null;wizardNext=null;footerHint=null;arrange=null;content.Children.Clear();footer.Children.Clear();steps.Children.Clear();head.Children.Clear();
                visited=Math.Max(visited,step);double marker=t.Tabs==WpfTabStyle.Rail?22:26;
                for(int i=0;i<stages.Length;i++) {
                    int index=i;Action navigate=()=> { if(drawing!=pageGeneration||index>visited||(step==0&&!guideReady&&index>0))return;step=index;draw(); };
                    var button=WpfKit.Step(i+1,stageName(stages[i]),i==step,i<step||(i!=step&&i<visited),navigate);button.IsEnabled=i<=visited;steps.Children.Add(button);
                    if(i<stages.Length-1)steps.Children.Add(new Border { Width=1.5,Height=10,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(10+marker/2-.75,0,0,2),Background=i<visited?WpfFieldTheme.Accent:WpfFieldTheme.Line2 });
                }
                head.Children.Add(WpfKit.Label(Localization.T("WizardStepIndicator",step+1,stages.Length)));head.Children.Add(WpfKit.Title(stageName(stages[step]),28));
                var segments=new UniformGrid { Rows=1,Columns=stages.Length,Margin=new Thickness(0,16,0,8) };
                for(int i=0;i<stages.Length;i++)segments.Children.Add(new Border { Height=3,Margin=new Thickness(0,0,i<stages.Length-1?6:0,0),Background=i<=step?WpfFieldTheme.AccentFill:WpfFieldTheme.Line,CornerRadius=new CornerRadius(t.Uppercase?0:1.5) });
                head.Children.Add(segments);
                if(step==0) {
                    content.Children.Add(BuildGuideChecklist(checklist,ready=> { guideReady=ready;if(wizardNext!=null)wizardNext.IsEnabled=ready;if(footerHint!=null)footerHint.Visibility=ready?Visibility.Collapsed:Visibility.Visible; }));
                }else if(step==1) {
                    content.Children.Add(intro("SetupAppearanceHelp"));
                    var appearance=new Grid { Margin=new Thickness(0,8,0,0) };appearance.ColumnDefinitions.Add(new ColumnDefinition());appearance.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(250) });appearance.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });appearance.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
                    var controls=new StackPanel();appearance.Children.Add(controls);AddSetupSettings(controls,2,draw,"SecAppearance",new[]{"MapWidth","MapHeight","MapOpacity"});AddAppearanceControls(controls,engine.WpfSnapshot(false));
                    var side=new StackPanel();side.Children.Add(WpfKit.Label(Localization.Get("SetupPreview")));
                    var preview=new WpfMapSurface { Compact=true,BaseMap=geography,Terrain=terrain,Width=210,Height=210,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Tag="setup-preview" };
                    side.Children.Add(new Border { Child=preview,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Min(12,t.ControlRadius)),Padding=new Thickness(14),MinHeight=240 });
                    var move=WpfKit.Wide(WpfKit.Secondary(Localization.Get("WpfMoveMinimap"),ShowMoveHud,"move"));move.Margin=new Thickness(0,10,0,0);side.Children.Add(move);appearance.Children.Add(side);
                    // Preview beside the controls on wide windows, above them on narrow ones.
                    arrange=()=> {
                        bool wide=window.ActualWidth<=0||window.ActualWidth>=980;appearance.ColumnDefinitions[1].Width=wide?new GridLength(250):new GridLength(0);
                        Grid.SetColumn(side,wide?1:0);Grid.SetRow(side,0);Grid.SetRow(controls,wide?0:1);controls.Margin=new Thickness(0,0,wide?28:0,0);side.Margin=new Thickness(0,0,0,wide?0:18);side.MaxWidth=wide?double.PositiveInfinity:320;side.HorizontalAlignment=HorizontalAlignment.Left;
                    };arrange();content.Children.Add(appearance);setupPreview=preview;
                }else if(step==2) {
                    content.Children.Add(intro("SetupMapHelp"));
                    AddSetupSettings(content,2,draw,"SecMapZones",new[]{"GridLabels","GridBorders","ShowScumMap","ShowSavedZones","ConfigurePoiFilters"});
                    var defaults=WpfKit.Secondary(Localization.Get("SetupDefaultColours"),()=> { engine.WpfDefaultColours();draw();Refresh(); },"rotate-ccw");defaults.Tag="setup-default-colours";defaults.Margin=new Thickness(0,10,0,4);
                    ((StackPanel)((Expander)content.Children.Cast<object>().Last(item=>item is Expander)).Content).Children.Add(defaults);
                }else if(step==3) {
                    content.Children.Add(intro("SetupTrackingHelp"));
                    AddSetupSettings(content,0,draw,"SecTrackingZoom",new[]{"AutoZoomSpeed","MaxZoom"});
                }else if(step==4) {
                    content.Children.Add(intro("SetupSharingHelp"));
                    var card=new DockPanel();var icon=WpfKit.IconTile("users",null,44);icon.Margin=new Thickness(0,0,16,0);icon.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(icon,Dock.Left);card.Children.Add(icon);
                    var words=new StackPanel();words.Children.Add(WpfKit.Strong(Localization.Get("PrivatePositions"),16));var about=WpfKit.Help(Localization.Get("PrivateGroupHelp"),13);about.Margin=new Thickness(0,4,0,14);words.Children.Add(about);
                    var connection=WpfKit.Primary(Localization.Get("SetupOpenSharing"),ShowPrivatePositions,"share-2");connection.Tag="setup-private-positions";words.Children.Add(connection);card.Children.Add(words);
                    content.Children.Add(new Border { Child=card,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Min(12,t.ControlRadius)),Padding=new Thickness(20,18,20,18),Margin=new Thickness(0,10,0,4) });
                    var sharingDetails=new StackPanel { Margin=new Thickness(0,6,0,0) };
                    foreach(var pair in new[]{new[]{"PrivatePositionGpsHelp","navigation"},new[]{"PrivatePositionMarkersHelp","map-pin"},new[]{"SetupSharingVisibility","eye"}}) {
                        var row=new DockPanel { Margin=new Thickness(0,8,0,4) };var glyph=WpfIcons.Create(pair[1],16,WpfFieldTheme.Muted);glyph.Margin=new Thickness(2,2,12,0);glyph.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(glyph,Dock.Left);row.Children.Add(glyph);row.Children.Add(WpfKit.Help(Localization.Get(pair[0]),13));sharingDetails.Children.Add(row);
                    }
                    content.Children.Add(new Expander { Header=WpfKit.Strong(Localization.Get("SetupMoreOptions"),15),Content=sharingDetails,Margin=new Thickness(0,14,0,0) });
                }
                var next=WpfKit.Primary(WpfKit.Plain(Localization.Get(step==4?"Done":"WizardBtnNext")),()=> { if(drawing!=pageGeneration||(step==0&&!guideReady)||!ValidateNumericFields(window))return;if(step==4) { if(engine.WpfCompleteOnboarding())window.Close();else WpfKit.Ask(window,Localization.Get("LocalSaveFailed"),Localization.Get("WizardTitle"),MessageBoxButton.OK,MessageBoxImage.Warning); }else { step++;draw(); } },step==4?"check":null);
                wizardNext=next;next.IsEnabled=step!=0||guideReady;next.Tag="setup-next";next.MinWidth=140;DockPanel.SetDock(next,Dock.Right);footer.Children.Add(next);
                if(step==0) { footerHint=WpfFieldTheme.Text(Localization.Get("GuideFinishToContinue"),13,WpfFieldTheme.Muted);footerHint.Margin=new Thickness(0,0,16,0);footerHint.Visibility=guideReady?Visibility.Collapsed:Visibility.Visible;DockPanel.SetDock(footerHint,Dock.Right);footer.Children.Add(footerHint); }
                var back=WpfKit.Quiet(WpfKit.Plain(Localization.Get("WizardBtnPrev")),()=> { if(step>0) { step--;draw(); } },"chevron-left");back.Visibility=step>0?Visibility.Visible:Visibility.Hidden;DockPanel.SetDock(back,Dock.Left);footer.Children.Add(back);
                RefreshSetupPreview();
            };draw();window.Closed+=(s,e)=> { setupPreview=null;engine.WpfSave(); };window.Content=layout;RefreshSetupPreview();return window;
        }
        void CaptureKey(string title,Func<int,int,string> validate,bool unbind) {
            var window=Window(title,480,270);var dialog=new WpfKit.Dialog(window,title);
            var face=new DockPanel();var key=WpfKit.IconTile("keyboard",null,48);key.Margin=new Thickness(0,0,16,0);DockPanel.SetDock(key,Dock.Left);face.Children.Add(key);
            var instruction=WpfKit.Help(Localization.Get("ShortcutPrompt"),16);instruction.Foreground=WpfFieldTheme.Ink;instruction.VerticalAlignment=VerticalAlignment.Center;face.Children.Add(instruction);dialog.Add(face);
            var hint=WpfKit.Help("Esc · "+Localization.Get("Cancel"),12);hint.VerticalAlignment=VerticalAlignment.Center;dialog.AddFooterLeft(hint);
            if(unbind)dialog.AddAction(WpfKit.Secondary(Localization.Get("ControlUnbind"),()=> { string error=validate(0,0);if(error==null)window.Close();else instruction.Text=error; },"x"));
            window.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Escape)return;e.Handled=true;if(Keyboard.Modifiers!=ModifierKeys.None) { instruction.Text=Localization.Get("ShortcutSingleKey");return; }int vk=KeyInterop.VirtualKeyFromKey(e.Key);int scan;if(!PhysicalKeyCapture.TryGet(vk,out scan)) { instruction.Text=Localization.Get("ShortcutRetry");return; }string error=validate(vk,scan);if(error==null)window.Close();else instruction.Text=error; };Modal(window);
        }
        void ShowAddWaypoint(DrawingPoint? point=null) { Modal(BuildAddWaypoint(point)); }
        Window BuildSavedMarkers() {
            var window=Window(S("MarkerList"),860,640);var dialog=new WpfKit.Dialog(window,S("MarkerList"),S("MarkerHelp"));dialog.AddClose(S("Close"));
            var t=WpfFieldTheme.Current;double side=t.Uppercase?34:t.GradientAccent?28:32;
            // Two panes: the list scrolls on its own, the editor for the selected marker stays beside it.
            var panes=new Grid { Margin=new Thickness(side,8,side,22) };panes.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(300) });panes.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(20) });panes.ColumnDefinitions.Add(new ColumnDefinition());
            dialog.Scroll.Content=panes;dialog.Scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;dialog.Scroll.Padding=new Thickness(0);
            var left=new DockPanel();var list=new ListBox { Tag="marker-list",BorderThickness=new Thickness(0),Background=Brushes.Transparent };System.Windows.Automation.AutomationProperties.SetName(list,S("MarkerList"));ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            foreach(var marker in engine.WpfSavedMarkers())list.Items.Add(new ListBoxItem { Content=WpfMarkerIcons.Caption(marker.Icon,marker.DisplayName),Tag=marker,ToolTip=marker.DisplayName,Padding=new Thickness(10,8,10,8) });
            var listHead=new DockPanel { Margin=new Thickness(0,0,0,8) };var total=WpfKit.Badge(list.Items.Count.ToString(CultureInfo.InvariantCulture));DockPanel.SetDock(total,Dock.Right);listHead.Children.Add(total);listHead.Children.Add(WpfKit.Label(S("MarkerList")));
            DockPanel.SetDock(listHead,Dock.Top);left.Children.Add(listHead);
            var listFrame=new Border { Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(t.Uppercase?0:Math.Min(12,t.ControlRadius)),Padding=new Thickness(4) };
            // The list stays in the tree even when empty (tests and automation find it by tag); the empty state sits over it.
            if(list.Items.Count==0) { var stack=new Grid();stack.Children.Add(list);stack.Children.Add(WpfKit.Empty(S("MarkerEmpty"),"map-pin"));listFrame.Child=stack; } else listFrame.Child=list;
            left.Children.Add(listFrame);panes.Children.Add(left);
            var editor=new StackPanel();var heading=WpfKit.Strong(S("MarkerSelectHint"),17);heading.Margin=new Thickness(0,0,0,4);editor.Children.Add(heading);
            var where=WpfKit.Help("",12);where.Margin=new Thickness(0,0,0,14);editor.Children.Add(where);
            var name=WpfKit.Input(null,"marker-edit-name",120);System.Windows.Automation.AutomationProperties.SetName(name,S("WaypointNamePrompt"));editor.Children.Add(WpfKit.Field(S("MarkerNameLabel"),name));
            var icons=WpfMarkerIcons.Picker();editor.Children.Add(WpfKit.Field(S("MarkerIcon"),icons));
            var rename=WpfKit.Secondary(S("MarkerSave"),()=> { var item=list.SelectedItem as ListBoxItem;if(item==null)return;var marker=(MapZone)item.Tag;if(engine.WpfUpdateMarker(marker,name.Text,WpfMarkerIcons.Selected(icons))) { item.Content=WpfMarkerIcons.Caption(marker.Icon,marker.DisplayName);item.ToolTip=marker.DisplayName;heading.Text=marker.DisplayName;Refresh(); } },"check");rename.Tag="marker-rename";
            var navigate=WpfKit.Primary(S("MapGpsDestination"),()=> { var item=list.SelectedItem as ListBoxItem;if(item==null)return;NavigateDestination((MapZone)item.Tag);window.Close(); },"navigation");navigate.Tag="marker-navigate";
            editor.Children.Add(WpfKit.Actions(navigate,rename));
            var card=WpfKit.Card(editor,new Thickness(20,18,20,12));card.VerticalAlignment=VerticalAlignment.Top;Grid.SetColumn(card,2);panes.Children.Add(card);
            Action enable=()=> { rename.IsEnabled=list.SelectedItem!=null&&!string.IsNullOrWhiteSpace(name.Text);navigate.IsEnabled=list.SelectedItem!=null;name.IsEnabled=icons.IsEnabled=list.SelectedItem!=null; };
            list.SelectionChanged+=(s,e)=> {
                var item=list.SelectedItem as ListBoxItem;var marker=item==null?null:(MapZone)item.Tag;name.Text=marker==null?"":marker.Name;icons.SelectedIndex=Array.IndexOf(WaypointIcons.Ids,WaypointIcons.Normalize(marker==null?null:marker.Icon));
                heading.Text=marker==null?S("MarkerSelectHint"):marker.DisplayName;where.Text=marker==null||marker.Points==null||marker.Points.Length==0?"":MarkerGrid(marker.Points[0]);enable();
            };name.TextChanged+=(s,e)=>enable();enable();
            return window;
        }
        static string MarkerGrid(System.Drawing.PointF point) {
            if(float.IsNaN(point.X)||float.IsNaN(point.Y))return "";
            return Localization.T("MarkerSector",DestinationSearch.Sector(point));
        }
        Window BuildAddWaypoint(DrawingPoint? point=null) {
            var window=Window(Localization.Get("MarkerAdd"),620,500);window.WindowStyle=WindowStyle.SingleBorderWindow;
            var dialog=new WpfKit.Dialog(window,Localization.Get("MarkerAdd"),(point.HasValue?MarkerGrid(point.Value)+" · ":"")+Localization.Get("MarkerAddHelp"));
            var name=WpfKit.Input(null,"waypoint-name",120);name.MinHeight=44;name.FontSize=16;
            var prompt=Localization.Get("WaypointNamePrompt");System.Windows.Automation.AutomationProperties.SetName(name,prompt);
            dialog.Add(WpfKit.Field(prompt.TrimEnd(':','\uFF1A',' '),name));
            var icons=WpfMarkerIcons.Picker();dialog.Add(WpfKit.Field(S("MarkerIcon"),icons));
            bool available=point.HasValue||engine.WpfSnapshot(false).Position!=null,saving=false,closed=false;
            if(!available)dialog.Add(WpfKit.Note(engine.WpfTrackingStatus(),"warn"));
            Button save=null;
            Action commit=()=> {
                if(closed||saving||!available||string.IsNullOrWhiteSpace(name.Text))return;
                saving=true;save.IsEnabled=false;
                try { if(engine.WpfWaypointWithIcon(name.Text.Trim(),point,WpfMarkerIcons.Selected(icons))) { Refresh();window.Close(); } }
                finally { saving=false;if(!closed) { save.IsEnabled=available&&!string.IsNullOrWhiteSpace(name.Text);name.Focus(); } }
            };
            var cancel=WpfKit.Quiet(Localization.Get("Cancel"),()=>window.Close());cancel.Tag="waypoint-cancel";dialog.AddAction(cancel);
            save=WpfKit.Primary(Localization.Get("MapSaveLocation"),commit,"map-pin");save.Tag="waypoint-save";save.IsEnabled=false;dialog.AddAction(save);
            name.TextChanged+=(s,e)=>save.IsEnabled=available&&!saving&&!string.IsNullOrWhiteSpace(name.Text);
            name.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter) { commit();e.Handled=true; } };
            window.ContentRendered+=(s,e)=> { name.Focus();name.SelectAll(); };window.Closed+=(s,e)=>closed=true;
            return window;
        }
        internal void Preview(string folder) {
            Directory.CreateDirectory(folder);engine.WpfPreviewPosition("{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=90 R=0}");engine.WpfTarget(engine.WpfSearch("grid D4").First());
            var end=DateTime.UtcNow.AddSeconds(5);while(engine.WpfSnapshot(true).Calculating&&DateTime.UtcNow<end) { System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(10); }
            Refresh();Capture(tactical,Path.Combine(folder,"tactical-map.png"));for(int i=0;i<5;i++)using(new WindowLifetime(BuildOnboarding(i)))Capture(WindowLifetime.Current,Path.Combine(folder,"onboarding-"+(i+1)+".png"));
            for(int i=0;i<3;i++)using(new WindowLifetime(BuildSettings(i)))Capture(WindowLifetime.Current,Path.Combine(folder,"settings-"+new[]{"general","shortcuts","appearance"}[i]+".png"));
            using(new WindowLifetime(BuildSearch()))Capture(WindowLifetime.Current,Path.Combine(folder,"search.png"));foreach(string shape in new[]{"Square","Circle"}) { engine.WpfAppearance(shape,100);Refresh();Capture(hud,Path.Combine(folder,"hud-"+shape.ToLowerInvariant()+".png")); }
        }
        internal static void Capture(Window window,string file) { window.ShowActivated=false;window.Left=-32000;window.Top=-32000;window.Show();window.UpdateLayout();var content=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(file))encoder.Save(stream);window.Hide(); }
        sealed class WindowLifetime:IDisposable { internal static Window Current;readonly Window window;internal WindowLifetime(Window window) { this.window=window;Current=window; }public void Dispose() { window.Close(); } }
        public void Dispose() {
            application.Dispatcher.VerifyAccess();if(disposed)return;disposed=true;
            if(autoLaunch!=null)autoLaunch.Dispose();
            if(zoneOpenRead!=null)zoneOpenRead.ContinueWith(task=>Program.LogException("ZoneOpenQueue",task.Exception),System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            DisposeRoutePreviews();if(privatePositions!=null)privatePositions.Dispose();if(relayGroups!=null)relayGroups.Dispose();refresh.Stop();if(cameraSettle!=null)cameraSettle.Stop();if(setupWindow!=null)setupWindow.Close();hudMap.ResetPoiStream();tacticalMap.ResetPoiStream();if(crosshair!=null)crosshair.Dispose();if(screenFilter!=null)screenFilter.Dispose();hud.Close();tactical.Close();engine.WpfSave();engine.Dispose();terrain.Dispose();
        }
    }
}
