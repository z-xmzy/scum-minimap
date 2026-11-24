using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScumMiniMap {
    // Screen-centre crosshair. It is a separate click-through, non-activating
    // topmost window drawn by this process only: nothing is read from or
    // written to SCUM's memory and nothing is injected into the game.
    internal enum CrosshairStyle { Cross, CrossDot, TShape, Diagonal, Dot, Circle, CircleDot }

    internal sealed class CrosshairSettings {
        internal const string FileName="crosshair.ini";
        internal static readonly string[] PresetColours={"#00FF66","#00FFFF","#FF3B3B","#FFE600","#FFFFFF","#FF4DFF"};
        internal bool Enabled;
        internal CrosshairStyle Style=CrosshairStyle.CrossDot;
        internal Color Colour=Color.FromRgb(0x00,0xFF,0x66);
        internal int Length=8,Thickness=2,Gap=4,DotSize=2,Opacity=90,OffsetX,OffsetY;
        internal bool Outline=true,HideWhileAiming=true;

        internal CrosshairSettings Clone() { return (CrosshairSettings)MemberwiseClone(); }
        internal void CopyFrom(CrosshairSettings other) {
            Enabled=other.Enabled;Style=other.Style;Colour=other.Colour;Length=other.Length;Thickness=other.Thickness;Gap=other.Gap;
            DotSize=other.DotSize;Opacity=other.Opacity;OffsetX=other.OffsetX;OffsetY=other.OffsetY;Outline=other.Outline;HideWhileAiming=other.HideWhileAiming;
        }
        internal void Clamp() {
            Length=Math.Max(1,Math.Min(60,Length));Thickness=Math.Max(1,Math.Min(12,Thickness));Gap=Math.Max(0,Math.Min(40,Gap));
            DotSize=Math.Max(1,Math.Min(16,DotSize));Opacity=Math.Max(10,Math.Min(100,Opacity));
            OffsetX=Math.Max(-400,Math.Min(400,OffsetX));OffsetY=Math.Max(-400,Math.Min(400,OffsetY));
            if(!Enum.IsDefined(typeof(CrosshairStyle),Style))Style=CrosshairStyle.CrossDot;
        }
        internal static string Hex(Color colour) { return "#"+colour.R.ToString("X2")+colour.G.ToString("X2")+colour.B.ToString("X2"); }
        internal static bool TryParseColour(string text,out Color colour) {
            colour=Colors.Transparent;if(string.IsNullOrWhiteSpace(text))return false;
            text=text.Trim().TrimStart('#');int value;
            if(text.Length!=6||!int.TryParse(text,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value))return false;
            colour=Color.FromRgb((byte)(value>>16),(byte)(value>>8),(byte)value);return true;
        }
        internal string Serialize() {
            var lines=new[]{
                "Enabled="+Enabled,"Style="+Style,"Colour="+Hex(Colour),"Length="+Length,"Thickness="+Thickness,"Gap="+Gap,
                "DotSize="+DotSize,"Opacity="+Opacity,"OffsetX="+OffsetX,"OffsetY="+OffsetY,"Outline="+Outline,"HideWhileAiming="+HideWhileAiming};
            return string.Join("\r\n",lines)+"\r\n";
        }
        internal static CrosshairSettings Parse(string text) {
            var settings=new CrosshairSettings();if(text==null)return settings;
            foreach(string raw in text.Split('\n')) {
                string line=raw.Trim();int split=line.IndexOf('=');if(split<=0)continue;
                string key=line.Substring(0,split).Trim(),value=line.Substring(split+1).Trim();int number;bool flag;
                bool isNumber=int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out number),isFlag=bool.TryParse(value,out flag);
                switch(key) {
                    case "Enabled":if(isFlag)settings.Enabled=flag;break;
                    case "Outline":if(isFlag)settings.Outline=flag;break;
                    case "HideWhileAiming":if(isFlag)settings.HideWhileAiming=flag;break;
                    case "Style":CrosshairStyle style;if(Enum.TryParse(value,true,out style)&&Enum.IsDefined(typeof(CrosshairStyle),style))settings.Style=style;break;
                    case "Colour":Color colour;if(TryParseColour(value,out colour))settings.Colour=colour;break;
                    case "Length":if(isNumber)settings.Length=number;break;
                    case "Thickness":if(isNumber)settings.Thickness=number;break;
                    case "Gap":if(isNumber)settings.Gap=number;break;
                    case "DotSize":if(isNumber)settings.DotSize=number;break;
                    case "Opacity":if(isNumber)settings.Opacity=number;break;
                    case "OffsetX":if(isNumber)settings.OffsetX=number;break;
                    case "OffsetY":if(isNumber)settings.OffsetY=number;break;
                }
            }
            settings.Clamp();return settings;
        }
        internal static CrosshairSettings Load(string folder) {
            try { string path=Path.Combine(folder,FileName);if(File.Exists(path))return Parse(File.ReadAllText(path,Encoding.UTF8)); }
            catch(Exception error) { Program.LogException("CrosshairLoad",error); }
            return new CrosshairSettings();
        }
        internal bool Save(string folder) {
            try {
                Directory.CreateDirectory(folder);string path=Path.Combine(folder,FileName),temp=path+".tmp";
                File.WriteAllText(temp,Serialize(),new UTF8Encoding(false));
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                return true;
            } catch(Exception error) { Program.LogException("CrosshairSave",error);return false; }
        }

        // Half-size of the drawing in physical pixels, including the outline.
        internal int Extent {
            get {
                int outline=Outline?1:0;int radius;
                switch(Style) {
                    case CrosshairStyle.Dot:radius=(DotSize+1)/2;break;
                    case CrosshairStyle.Circle:case CrosshairStyle.CircleDot:radius=Gap+Length+(Thickness+1)/2;break;
                    case CrosshairStyle.Diagonal:radius=(int)Math.Ceiling((Gap+Length)*0.7072+Thickness);break;
                    default:radius=Gap+Length;break;
                }
                return Math.Max(radius,Math.Max((DotSize+1)/2,(Thickness+1)/2))+outline+2;
            }
        }
    }

    internal static class CrosshairRenderer {
        static readonly Brush OutlineBrush=Freeze(new SolidColorBrush(Color.FromArgb(230,0,0,0)));
        static Brush Freeze(Brush brush) { brush.Freeze();return brush; }
        // Draws in physical pixels around (cx,cy). Callers undo WPF's DPI scale first.
        internal static void Draw(DrawingContext context,CrosshairSettings settings,double cx,double cy) {
            var fill=new SolidColorBrush(settings.Colour);fill.Freeze();
            context.PushOpacity(settings.Opacity/100.0);
            if(settings.Outline)DrawShape(context,settings,cx,cy,OutlineBrush,1);
            DrawShape(context,settings,cx,cy,fill,0);
            context.Pop();
        }
        static void DrawShape(DrawingContext context,CrosshairSettings s,double cx,double cy,Brush brush,double grow) {
            bool dot=s.Style==CrosshairStyle.CrossDot||s.Style==CrosshairStyle.Dot||s.Style==CrosshairStyle.CircleDot;
            switch(s.Style) {
                case CrosshairStyle.Cross:case CrosshairStyle.CrossDot:case CrosshairStyle.TShape:
                    double half=s.Thickness/2.0;
                    // Pixel-aligned bars: odd thickness centres on the middle pixel.
                    double left=Math.Floor(cx-half),top=Math.Floor(cy-half),t=s.Thickness;
                    Bar(context,brush,grow,left+t+s.Gap,top,s.Length,t);
                    Bar(context,brush,grow,left-s.Gap-s.Length,top,s.Length,t);
                    Bar(context,brush,grow,left,top+t+s.Gap,t,s.Length);
                    if(s.Style!=CrosshairStyle.TShape)Bar(context,brush,grow,left,top-s.Gap-s.Length,t,s.Length);
                    break;
                case CrosshairStyle.Diagonal:
                    var pen=new Pen(brush,s.Thickness+grow*2) { StartLineCap=PenLineCap.Flat,EndLineCap=PenLineCap.Flat };pen.Freeze();
                    double inner=s.Gap*0.7071,outer=(s.Gap+s.Length)*0.7071,g=grow*0.7071;
                    foreach(var d in new[]{new Vector(1,1),new Vector(-1,1),new Vector(1,-1),new Vector(-1,-1)})
                        context.DrawLine(pen,new Point(cx+d.X*(inner-g),cy+d.Y*(inner-g)),new Point(cx+d.X*(outer+g),cy+d.Y*(outer+g)));
                    break;
                case CrosshairStyle.Circle:case CrosshairStyle.CircleDot:
                    var ring=new Pen(brush,s.Thickness+grow*2);ring.Freeze();double radius=s.Gap+s.Length;
                    context.DrawEllipse(null,ring,new Point(cx,cy),radius,radius);
                    break;
            }
            if(dot) {
                // Tiny dots stay pixel-sharp as squares; larger ones are round.
                if(s.DotSize<=3) { double x=Math.Floor(cx-s.DotSize/2.0+0.25),y=Math.Floor(cy-s.DotSize/2.0+0.25);Bar(context,brush,grow,x,y,s.DotSize,s.DotSize); }
                else { double r=s.DotSize/2.0+grow;context.DrawEllipse(brush,null,new Point(cx,cy),r,r); }
            }
        }
        static void Bar(DrawingContext context,Brush brush,double grow,double x,double y,double w,double h) {
            context.DrawRectangle(brush,null,new Rect(x-grow,y-grow,w+grow*2,h+grow*2));
        }
    }

    // Renders the crosshair with one unit equal to one physical pixel.
    internal sealed class CrosshairSurface:FrameworkElement {
        internal CrosshairSettings Settings;
        internal Brush Backdrop;
        protected override void OnRender(DrawingContext context) {
            if(Backdrop!=null)context.DrawRectangle(Backdrop,null,new Rect(0,0,ActualWidth,ActualHeight));
            if(Settings==null)return;
            double sx=1,sy=1;var source=PresentationSource.FromVisual(this);
            if(source!=null&&source.CompositionTarget!=null) { var m=source.CompositionTarget.TransformToDevice;sx=m.M11;sy=m.M22; }
            context.PushTransform(new ScaleTransform(1/sx,1/sy));
            double width=Math.Round(ActualWidth*sx),height=Math.Round(ActualHeight*sy);
            double odd=(Settings.Style==CrosshairStyle.Dot?Settings.DotSize:Settings.Thickness)%2==1?0.5:0;
            CrosshairRenderer.Draw(context,Settings,Math.Floor(width/2)+odd,Math.Floor(height/2)+odd);
            context.Pop();
        }
    }

    internal sealed class CrosshairOverlay:IDisposable {
        const int GWL_EXSTYLE=-20;
        const int WS_EX_TRANSPARENT=0x20,WS_EX_TOOLWINDOW=0x80,WS_EX_LAYERED=0x80000,WS_EX_NOACTIVATE=0x8000000;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int cx,int cy,uint flags);

        readonly string folder;
        readonly Func<bool> suppressed;
        readonly Window window;
        readonly CrosshairSurface surface;
        readonly DispatcherTimer timer;
        System.Drawing.Rectangle lastBounds;
        bool shown,disposed;
        internal CrosshairSettings Settings { get; private set; }

        internal CrosshairOverlay(string folder,Func<bool> suppressed,Dispatcher dispatcher) {
            this.folder=folder;this.suppressed=suppressed;Settings=CrosshairSettings.Load(folder);
            surface=new CrosshairSurface { Settings=Settings,IsHitTestVisible=false };
            window=new Window { WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,
                ShowInTaskbar=false,ShowActivated=false,Topmost=true,Focusable=false,IsHitTestVisible=false,Width=64,Height=64,
                WindowStartupLocation=WindowStartupLocation.Manual,Left=-32000,Top=-32000,Title="SCUM MiniMap Crosshair",Content=surface,Tag="crosshair-overlay" };
            window.SourceInitialized+=(s,e)=> {
                var handle=new WindowInteropHelper(window).Handle;
                SetWindowLong(handle,GWL_EXSTYLE,GetWindowLong(handle,GWL_EXSTYLE)|WS_EX_TRANSPARENT|WS_EX_TOOLWINDOW|WS_EX_LAYERED|WS_EX_NOACTIVATE);
            };
            timer=new DispatcherTimer(DispatcherPriority.Normal,dispatcher) { Interval=TimeSpan.FromMilliseconds(50) };
            timer.Tick+=(s,e)=> { try { Sync(); }catch(Exception error) { timer.Stop();Program.LogException("CrosshairSync",error); } };
        }
        internal void Start() { if(!disposed)timer.Start(); }

        // Same signals that hide the minimap: SCUM must own the foreground and
        // show no system cursor (inventory, pause, settings, map all show one).
        internal static bool ShouldShow(bool enabled,bool gameFocused,bool cursorVisible,bool appSuppressed,bool aiming,bool hideWhileAiming) {
            return enabled&&gameFocused&&!cursorVisible&&!appSuppressed&&!(hideWhileAiming&&aiming);
        }
        // Centre of the game's client area plus the user's offset, in physical pixels.
        internal static System.Drawing.Rectangle Placement(System.Drawing.Rectangle client,CrosshairSettings settings) {
            int extent=settings.Extent,size=extent*2;
            int cx=client.Left+client.Width/2+settings.OffsetX,cy=client.Top+client.Height/2+settings.OffsetY;
            return new System.Drawing.Rectangle(cx-extent,cy-extent,size,size);
        }

        internal void Apply(CrosshairSettings settings) {
            settings.Clamp();Settings=settings;surface.Settings=settings;surface.InvalidateVisual();lastBounds=System.Drawing.Rectangle.Empty;
            if(!disposed&&timer.IsEnabled)Sync();
        }
        internal bool Save() { return Settings.Save(folder); }
        internal void SetEnabled(bool enabled) { var next=Settings.Clone();next.Enabled=enabled;Apply(next);Save(); }

        void Sync() {
            if(disposed)return;
            bool gameFocused=Native.GameFocused();
            bool show=ShouldShow(Settings.Enabled,gameFocused,gameFocused&&Native.IsCursorVisible(),suppressed!=null&&suppressed(),Settings.HideWhileAiming&&Native.IsRightMouseDown(),Settings.HideWhileAiming);
            System.Drawing.Rectangle client=System.Drawing.Rectangle.Empty;
            if(show&&!Native.TryGameClientBounds(Native.GetForegroundWindow(),out client))show=false;
            if(!show) { if(shown) { window.Hide();shown=false; }return; }
            var bounds=Placement(client,Settings);
            if(!shown) { window.Show();shown=true;lastBounds=System.Drawing.Rectangle.Empty; }
            var handle=new WindowInteropHelper(window).Handle;
            if(bounds!=lastBounds) {
                // Physical-pixel placement; WPF follows the native size.
                SetWindowPos(handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0010|0x0040);
                lastBounds=bounds;surface.InvalidateVisual();
            }
        }
        public void Dispose() {
            if(disposed)return;disposed=true;timer.Stop();
            try { window.Close(); }catch(InvalidOperationException) { }
        }
    }
}
