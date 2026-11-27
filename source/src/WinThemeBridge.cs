using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Windows Forms windows (admin tools, item spawner, zone editor, tray menu) follow the active WPF
    // interface theme: the same ground, panel, line, ink and accent colours, the theme's corner radius,
    // condensed display type in the tactical theme and a themed native title bar.
    internal static class WinTheme {
        static WpfThemeSpec cachedSpec;
        static readonly Dictionary<string,Color> cache=new Dictionary<string,Color>(StringComparer.Ordinal);
        static readonly object gate=new object();
        internal static WpfThemeSpec Spec { get { return WpfFieldTheme.Current??WpfThemes.Default; } }

        // Theme colours flattened onto the ground colour (WinForms has no translucency).
        static Color Token(string name,Func<WpfThemeSpec,string> pick) {
            var spec=Spec;
            lock(gate) {
                if(!ReferenceEquals(spec,cachedSpec)) { cache.Clear();cachedSpec=spec; }
                Color value;
                if(!cache.TryGetValue(name,out value)) { value=Over(Parse(pick(spec)),Parse(spec.Ground));cache[name]=value; }
                return value;
            }
        }
        internal static Color Ground { get { return Token("ground",t=>t.Ground); } }
        internal static Color Panel { get { return Token("panel",t=>t.PanelSolid); } }
        internal static Color Raised { get { return Token("raised",t=>t.Raised); } }
        internal static Color Hover { get { return Token("hover",t=>t.Hover); } }
        internal static Color Input { get { return Token("input",t=>t.Input); } }
        internal static Color Line { get { return Token("line",t=>t.Line); } }
        internal static Color Line2 { get { return Token("line2",t=>t.Line2); } }
        internal static Color Ink { get { return Token("ink",t=>t.Ink); } }
        internal static Color Muted { get { return Token("muted",t=>t.Muted); } }
        internal static Color Dim { get { return Token("dim",t=>t.Dim); } }
        internal static Color Accent { get { return Token("accent",t=>t.Accent); } }
        internal static Color AccentInk { get { return Token("accent-ink",t=>t.AccentInk); } }
        internal static Color AccentSoft { get { return Token("accent-soft",t=>t.AccentSoft); } }
        internal static Color Danger { get { return Token("danger",t=>t.Danger); } }
        internal static Color Success { get { return Token("success",t=>t.Success); } }
        internal static Color Info { get { return Token("info",t=>t.Info); } }
        internal static Color AccentHover { get { return Mix(Accent,Color.White,.16); } }
        // Panel colour raised half-way towards Raised: list headers, selected rows.
        internal static Color RaisedStrong { get { return Mix(Raised,Hover,.5); } }

        internal static bool Caps { get { return Spec.Uppercase; } }
        internal static bool Chamfer { get { return Spec.Chamfer>0; } }
        internal static int Radius { get { return Spec.Uppercase?2:(int)Math.Max(4,Math.Min(10,Spec.ControlRadius)); } }
        internal static string Upper(string text) { return Caps&&text!=null?text.ToUpper(CultureInfo.CurrentCulture):text; }

        internal static Color Parse(string hex) {
            string value=(hex??"").Trim().TrimStart('#');
            try {
                if(value.Length==6)return Color.FromArgb(255,int.Parse(value.Substring(0,2),NumberStyles.HexNumber),int.Parse(value.Substring(2,2),NumberStyles.HexNumber),int.Parse(value.Substring(4,2),NumberStyles.HexNumber));
                if(value.Length==8)return Color.FromArgb(int.Parse(value.Substring(0,2),NumberStyles.HexNumber),int.Parse(value.Substring(2,2),NumberStyles.HexNumber),int.Parse(value.Substring(4,2),NumberStyles.HexNumber),int.Parse(value.Substring(6,2),NumberStyles.HexNumber));
            } catch(FormatException) {}
            return Color.Magenta;
        }
        internal static Color Over(Color top,Color bottom) {
            double a=top.A/255.0;
            return Color.FromArgb(255,(int)Math.Round(top.R*a+bottom.R*(1-a)),(int)Math.Round(top.G*a+bottom.G*(1-a)),(int)Math.Round(top.B*a+bottom.B*(1-a)));
        }
        internal static Color Mix(Color a,Color b,double amount) {
            amount=Math.Max(0,Math.Min(1,amount));
            return Color.FromArgb(255,(int)Math.Round(a.R+(b.R-a.R)*amount),(int)Math.Round(a.G+(b.G-a.G)*amount),(int)Math.Round(a.B+(b.B-a.B)*amount));
        }
        internal static Color Alpha(Color colour,int alpha,Color under) { return Over(Color.FromArgb(alpha,colour),under); }

        // ── Type ──────────────────────────────────────────────────────────
        static readonly PrivateFontCollection displayFonts=new PrivateFontCollection();
        static readonly List<IntPtr> fontMemory=new List<IntPtr>();
        static bool displayLoaded;
        static FontFamily condensed;
        static FontFamily Condensed() {
            lock(gate) {
                if(displayLoaded)return condensed;
                displayLoaded=true;
                try {
                    using(var stream=typeof(WinTheme).Assembly.GetManifestResourceStream("BarlowCondensed-Bold.ttf")) {
                        if(stream!=null) {
                            var bytes=new byte[(int)stream.Length];int offset=0,read;
                            while(offset<bytes.Length&&(read=stream.Read(bytes,offset,bytes.Length-offset))>0)offset+=read;
                            if(offset==bytes.Length) {
                                IntPtr memory=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,memory,bytes.Length);fontMemory.Add(memory);
                                uint count=0;AddFontMemResourceEx(memory,(uint)bytes.Length,IntPtr.Zero,ref count);
                                displayFonts.AddMemoryFont(memory,bytes.Length);
                                if(displayFonts.Families.Length>0)condensed=displayFonts.Families[0];
                            }
                        }
                    }
                } catch(Exception error) { Program.LogException("WinThemeFont",error); }
                return condensed;
            }
        }
        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data,uint length,IntPtr reserved,ref uint count);
        // Body text: Segoe UI in every theme (closest system face to the interface font).
        internal static Font Body(float size,FontStyle style=FontStyle.Regular) {
            return new Font(style==FontStyle.Bold?"Segoe UI Semibold":"Segoe UI",size,style==FontStyle.Bold?FontStyle.Regular:style);
        }
        // Titles and section labels: condensed bold capitals in the tactical theme.
        internal static Font Display(float size) {
            var family=Caps?Condensed():null;
            if(family!=null&&family.IsStyleAvailable(FontStyle.Bold))return new Font(family,size+3f,FontStyle.Bold);
            if(family!=null)return new Font(family,size+3f,FontStyle.Regular);
            return new Font("Segoe UI Semibold",size,FontStyle.Regular);
        }
        // The condensed display face only covers Latin; GDI+ cannot fall back from a private font, so other scripts
        // (Chinese, Japanese, Korean, Thai, Cyrillic, Arabic) use the matching bold system face instead of a serif fallback.
        internal static Font DisplayFor(string text,float size) {
            string face=SystemFace(text);
            return face==null?Display(size):new Font(face,size,FontStyle.Bold);
        }
        static string SystemFace(string text) {
            if(text==null)return null;bool other=false;
            foreach(char c in text) {
                if(c>=0xAC00&&c<=0xD7AF)return "Malgun Gothic";
                if(c>=0x0E00&&c<=0x0E7F)return "Leelawadee UI";
                if(c>=0x3040&&c<=0x30FF)return "Yu Gothic UI";
                if(c>0x024F&&!(c>=0x2000&&c<=0x206F)&&!(c>=0x2190&&c<=0x21FF))other=true;
            }
            if(!other)return null;
            foreach(char c in text)if(c>=0x2E80)return "Microsoft YaHei UI";
            return "Segoe UI";
        }
        // Keeps a heading label in the right display face when its text changes (language switch).
        internal static void DisplayLabel(Label label,float size) {
            if(label==null)return;
            label.Font=DisplayFor(label.Text,size);
            label.TextChanged+=(s,e)=> { var font=DisplayFor(label.Text,size);if(font.Name!=label.Font.Name)label.Font=font;else font.Dispose(); };
        }

        // ── Shapes ────────────────────────────────────────────────────────
        internal static GraphicsPath Shape(Rectangle rect,int radius) {
            var path=new GraphicsPath();
            if(rect.Width<=0||rect.Height<=0) { path.AddRectangle(rect);return path; }
            int arc=Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
            if(arc<=1) { path.AddRectangle(rect);return path; }
            path.AddArc(rect.X,rect.Y,arc,arc,180,90);path.AddArc(rect.Right-arc,rect.Y,arc,arc,270,90);
            path.AddArc(rect.Right-arc,rect.Bottom-arc,arc,arc,0,90);path.AddArc(rect.X,rect.Bottom-arc,arc,arc,90,90);
            path.CloseFigure();return path;
        }
        internal static void FillShape(Graphics graphics,Rectangle rect,Color fill,Color border,int radius) {
            var mode=graphics.SmoothingMode;graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Shape(rect,radius)) {
                using(var brush=new SolidBrush(fill))graphics.FillPath(brush,path);
                if(border.A>0)using(var pen=new Pen(border,1f))graphics.DrawPath(pen,path);
            }
            graphics.SmoothingMode=mode;
        }
        // Chevron (› or ⌄) drawn as two strokes, so it stays crisp at any DPI.
        internal static void Chevron(Graphics graphics,Rectangle box,bool down,Color ink) { Chevron(graphics,box,down?2:1,ink,3f); }
        // direction: 0 up, 1 right, 2 down, 3 left.
        internal static void Chevron(Graphics graphics,Rectangle box,int direction,Color ink,float minimum) {
            var mode=graphics.SmoothingMode;graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float cx=box.X+box.Width/2f,cy=box.Y+box.Height/2f,s=Math.Max(minimum,Math.Min(box.Width,box.Height)/4f);
            using(var pen=new Pen(ink,1.6f) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round }) {
                if(direction==2)graphics.DrawLines(pen,new[]{new PointF(cx-s,cy-s/2),new PointF(cx,cy+s/2),new PointF(cx+s,cy-s/2)});
                else if(direction==0)graphics.DrawLines(pen,new[]{new PointF(cx-s,cy+s/2),new PointF(cx,cy-s/2),new PointF(cx+s,cy+s/2)});
                else if(direction==3)graphics.DrawLines(pen,new[]{new PointF(cx+s/2,cy-s),new PointF(cx-s/2,cy),new PointF(cx+s/2,cy+s)});
                else graphics.DrawLines(pen,new[]{new PointF(cx-s/2,cy-s),new PointF(cx+s/2,cy),new PointF(cx-s/2,cy+s)});
            }
            graphics.SmoothingMode=mode;
        }
        internal static void Tick(Graphics graphics,Rectangle box,Color ink) {
            var mode=graphics.SmoothingMode;graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float x=box.X,y=box.Y,w=box.Width,h=box.Height;
            using(var pen=new Pen(ink,Math.Max(1.6f,w/7f)) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round })
                graphics.DrawLines(pen,new[]{new PointF(x+w*.22f,y+h*.52f),new PointF(x+w*.42f,y+h*.72f),new PointF(x+w*.78f,y+h*.3f)});
            graphics.SmoothingMode=mode;
        }

        // ── Windows ───────────────────────────────────────────────────────
        // Dark, theme-coloured native title bar for any form.
        internal static void Caption(Form form) {
            if(form==null||Equals(form.Tag,"themed-caption"))return;
            EventHandler apply=(s,e)=>ThemedCaption.Apply(form.Handle,Spec);
            form.HandleCreated+=apply;if(form.IsHandleCreated)apply(form,EventArgs.Empty);
        }
        // Title band for a form: accent bar (tactical), title and an optional muted subtitle.
        internal static Panel Header(string title,string subtitle,out Label heading,out Label caption) {
            var band=new Panel { Dock=DockStyle.Top,Height=string.IsNullOrEmpty(subtitle)?58:82,Padding=new Padding(22,14,22,10),BackColor=Ground };
            heading=new Label { Text=Upper(title),Dock=DockStyle.Top,Height=Caps?34:30,ForeColor=Ink,AutoEllipsis=true,Tag="theme-heading" };DisplayLabel(heading,15f);
            caption=new Label { Text=subtitle??"",Dock=DockStyle.Fill,Font=Body(9f),ForeColor=Muted,AutoEllipsis=true,Tag="theme-muted",Visible=!string.IsNullOrEmpty(subtitle) };
            band.Controls.Add(caption);band.Controls.Add(heading);
            band.Paint+=(s,e)=> {
                using(var line=new Pen(Line))e.Graphics.DrawLine(line,0,band.Height-1,band.Width,band.Height-1);
                if(Caps)using(var accent=new SolidBrush(Accent))e.Graphics.FillRectangle(accent,22,6,28,3);
            };
            return band;
        }
        // Primary, secondary, quiet and danger button looks for plain WinForms buttons.
        internal static void Primary(Button button) { Paint(button,"primary"); }
        internal static void Secondary(Button button) { Paint(button,"secondary"); }
        internal static void Quiet(Button button) { Paint(button,"quiet"); }
        internal static void DangerButton(Button button) { Paint(button,"danger"); }
        static readonly Dictionary<Button,string> looks=new Dictionary<Button,string>();
        static void Paint(Button button,string look) {
            if(button==null)return;
            bool hooked;lock(gate) { hooked=looks.ContainsKey(button);looks[button]=look; }
            button.FlatStyle=FlatStyle.Flat;button.UseVisualStyleBackColor=false;button.FlatAppearance.BorderSize=0;button.Cursor=Cursors.Hand;
            button.BackColor=look=="primary"?Accent:look=="danger"?Alpha(Danger,0x2A,Ground):look=="quiet"?Ground:Raised;
            button.ForeColor=look=="primary"?AccentInk:look=="danger"?Danger:look=="quiet"?Muted:Ink;
            button.FlatAppearance.MouseOverBackColor=look=="primary"?AccentHover:Hover;button.FlatAppearance.MouseDownBackColor=look=="primary"?AccentHover:Hover;
            if(hooked)return;
            button.Paint+=(s,e)=> {
                string current;lock(gate) { if(!looks.TryGetValue(button,out current))return; }
                var parent=button.Parent;var back=parent==null?Ground:parent.BackColor;
                bool hover=button.Enabled&&button.ClientRectangle.Contains(button.PointToClient(Cursor.Position));
                Color fill=current=="primary"?(hover?AccentHover:Accent):current=="danger"?Alpha(Danger,hover?0x40:0x2A,back):current=="quiet"?(hover?Hover:back):(hover?Hover:Raised);
                Color border=current=="primary"?Color.Transparent:current=="danger"?Alpha(Danger,0x70,back):current=="quiet"?Color.Transparent:Line2;
                Color ink=!button.Enabled?Dim:current=="primary"?AccentInk:current=="danger"?Danger:current=="quiet"?Muted:Ink;
                if(!button.Enabled&&current=="primary") { fill=Raised;border=Line; }
                e.Graphics.Clear(back);
                FillShape(e.Graphics,new Rectangle(0,0,button.Width-1,button.Height-1),fill,border,Radius);
                TextRenderer.DrawText(e.Graphics,Upper(button.Text),button.Font,new Rectangle(6,0,button.Width-12,button.Height),ink,
                    TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                if(button.Focused)using(var pen=new Pen(Accent) { DashStyle=DashStyle.Dot })e.Graphics.DrawRectangle(pen,3,3,button.Width-7,button.Height-7);
            };
            button.MouseEnter+=(s,e)=>button.Invalidate();button.MouseLeave+=(s,e)=>button.Invalidate();
            button.Disposed+=(s,e)=> { lock(gate)looks.Remove(button); };
        }
        // Lucide icon rendered to a bitmap for menus and WinForms buttons (WPF geometry, drawn off-screen).
        internal static Bitmap Icon(string name,int size,Color ink) {
            string data=WpfIcons.PathData(name);if(data==null)return null;
            try {
                var geometry=System.Windows.Media.Geometry.Parse(data);
                float scale=GetScale();int pixels=(int)Math.Round(size*scale);
                var brush=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(ink.A,ink.R,ink.G,ink.B));
                var pen=new System.Windows.Media.Pen(brush,2) { StartLineCap=System.Windows.Media.PenLineCap.Round,EndLineCap=System.Windows.Media.PenLineCap.Round,LineJoin=System.Windows.Media.PenLineJoin.Round };
                var visual=new System.Windows.Media.DrawingVisual();
                using(var context=visual.RenderOpen()) {
                    context.PushTransform(new System.Windows.Media.ScaleTransform(pixels/24.0,pixels/24.0));
                    context.DrawGeometry(null,pen,geometry);context.Pop();
                }
                var target=new System.Windows.Media.Imaging.RenderTargetBitmap(pixels,pixels,96,96,System.Windows.Media.PixelFormats.Pbgra32);target.Render(visual);
                var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(target));
                using(var stream=new System.IO.MemoryStream()) { encoder.Save(stream);stream.Position=0;using(var decoded=new Bitmap(stream))return new Bitmap(decoded); }
            } catch(Exception error) { Program.LogException("WinThemeIcon",error);return null; }
        }
        static float GetScale() { using(var screen=Graphics.FromHwnd(IntPtr.Zero))return Math.Max(1f,screen.DpiX/96f); }
        // Raised card behind a group of controls.
        internal static void Card(Control panel) {
            panel.BackColor=Raised;
            panel.Paint+=(s,e)=> { using(var pen=new Pen(Line))e.Graphics.DrawRectangle(pen,0,0,panel.Width-1,panel.Height-1); };
        }

        // ── Inputs ────────────────────────────────────────────────────────
        static readonly ConditionalWeakTable<Control,HashSet<string>> fields=new ConditionalWeakTable<Control,HashSet<string>>();
        // True the first time a given treatment is applied to a control.
        static bool Mark(Control control,string treatment) { lock(gate)return fields.GetOrCreateValue(control).Add(treatment); }
        // A borderless text field (or number box) drawn inside a themed frame painted by its parent, so the field
        // itself keeps its place in the parent's controls. The frame sits in the padding around the field's bounds;
        // it turns accent while the field has focus, and a click on the padding focuses the field.
        internal static void Frame(Control field,int padX=10,int padY=8) {
            if(field==null)return;
            var box=field as TextBox;if(box!=null)box.BorderStyle=BorderStyle.None;
            var number=field as NumericUpDown;if(number!=null)number.BorderStyle=BorderStyle.None;
            field.BackColor=Input;field.ForeColor=Ink;
            if(!Mark(field,"frame"))return;
            Func<Rectangle> outline=()=>Rectangle.Inflate(field.Bounds,padX,padY);
            PaintEventHandler paint=(s,e)=> {
                if(!field.Visible)return;var r=outline();r.Width-=1;r.Height-=1;
                FillShape(e.Graphics,r,field.Enabled?Input:Raised,field.ContainsFocus?Accent:Line2,Math.Min(Radius,6));
            };
            MouseEventHandler click=(s,e)=> { if(field.Enabled&&field.Visible&&outline().Contains(e.Location)&&!field.Bounds.Contains(e.Location))field.Focus(); };
            Control host=null;
            Action attach=()=> {
                if(host!=null) { host.Paint-=paint;host.MouseDown-=click; }
                host=field.Parent;if(host!=null) { host.Paint+=paint;host.MouseDown+=click;host.Invalidate(); }
            };
            EventHandler redraw=(s,e)=> { if(field.Parent!=null)field.Parent.Invalidate(Rectangle.Inflate(field.Bounds,padX+2,padY+2)); };
            field.ParentChanged+=(s,e)=>attach();attach();
            field.Enter+=redraw;field.Leave+=redraw;field.EnabledChanged+=redraw;field.VisibleChanged+=redraw;field.SizeChanged+=redraw;field.LocationChanged+=(s,e)=> { if(field.Parent!=null)field.Parent.Invalidate(); };
            if(number!=null)Spin(number);
        }
        // Number box spin buttons drawn as two chevrons on the raised surface.
        internal static void Spin(NumericUpDown number) {
            number.BackColor=Input;number.ForeColor=Ink;
            foreach(Control child in number.Controls) {
                if(child is TextBox) { child.BackColor=Input;child.ForeColor=Ink;continue; }
                var buttons=child;if(!Mark(buttons,"spin"))continue;
                buttons.Paint+=(s,e)=> {
                    var r=buttons.ClientRectangle;int half=r.Height/2;var pointer=buttons.PointToClient(Cursor.Position);
                    using(var back=new SolidBrush(number.Enabled?Raised:Input))e.Graphics.FillRectangle(back,r);
                    if(number.Enabled&&r.Contains(pointer))using(var hover=new SolidBrush(Hover))e.Graphics.FillRectangle(hover,new Rectangle(0,pointer.Y<half?0:half,r.Width,pointer.Y<half?half:r.Height-half));
                    using(var line=new Pen(Line))e.Graphics.DrawLine(line,0,0,0,r.Height);
                    Color ink=number.Enabled?Muted:Dim;
                    Chevron(e.Graphics,new Rectangle(0,0,r.Width,half),0,ink,2.5f);Chevron(e.Graphics,new Rectangle(0,half,r.Width,r.Height-half),2,ink,2.5f);
                };
                buttons.MouseMove+=(s,e)=>buttons.Invalidate();buttons.MouseLeave+=(s,e)=>buttons.Invalidate();
            }
        }
        static readonly MethodInfo setStyle=typeof(Control).GetMethod("SetStyle",BindingFlags.Instance|BindingFlags.NonPublic);
        // Drop-down lists painted by the theme: input fill, 1 px frame (accent when focused or open), text and chevron.
        internal static void Combo(ComboBox combo,int height=36) {
            if(combo==null||combo is TacticalComboBox)return;
            combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=Math.Max(combo.ItemHeight,height-6);
            combo.BackColor=Input;combo.ForeColor=Ink;
            if(combo.DropDownStyle!=ComboBoxStyle.DropDownList) {
                EventHandler theme=(s,e)=> { try { SetWindowTheme(combo.Handle,"DarkMode_CFD",null); }catch(EntryPointNotFoundException) {}catch(DllNotFoundException) {} };
                combo.FlatStyle=FlatStyle.Standard;combo.HandleCreated+=theme;if(combo.IsHandleCreated)theme(combo,EventArgs.Empty);return;
            }
            if(!Mark(combo,"combo"))return;
            setStyle.Invoke(combo,new object[]{ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true});
            combo.Paint+=(s,e)=> {
                var g=e.Graphics;var parent=combo.Parent;g.Clear(parent==null?Ground:parent.BackColor);
                bool open=combo.DroppedDown,focus=combo.Focused||open,hover=combo.Enabled&&combo.ClientRectangle.Contains(combo.PointToClient(Cursor.Position));
                var r=new Rectangle(0,0,combo.Width-1,combo.Height-1);
                FillShape(g,r,combo.Enabled?(hover&&!focus?Mix(Input,Hover,.5):Input):Raised,focus?Accent:hover?Mix(Line2,Ink,.18):Line2,Math.Min(Radius,6));
                string text=combo.SelectedIndex>=0?combo.GetItemText(combo.SelectedItem):combo.Text;
                TextRenderer.DrawText(g,text,combo.Font,new Rectangle(10,0,combo.Width-40,combo.Height),combo.Enabled?Ink:Dim,
                    TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                Chevron(g,new Rectangle(combo.Width-30,0,22,combo.Height),open?0:2,open?Accent:Muted,3.5f);
            };
            combo.DrawItem+=(s,e)=> {
                if(e.Index<0||(e.State&DrawItemState.ComboBoxEdit)!=0)return;
                bool selected=(e.State&DrawItemState.Selected)!=0;
                using(var back=new SolidBrush(selected?AccentSoft:Raised))e.Graphics.FillRectangle(back,e.Bounds);
                if(selected)using(var bar=new SolidBrush(Accent))e.Graphics.FillRectangle(bar,e.Bounds.X,e.Bounds.Y,3,e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics,combo.GetItemText(combo.Items[e.Index]),combo.Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-14,e.Bounds.Height),selected?Accent:Ink,
                    TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
            };
            EventHandler repaint=(s,e)=>combo.Invalidate();
            combo.DropDown+=repaint;combo.DropDownClosed+=repaint;combo.Enter+=repaint;combo.Leave+=repaint;combo.MouseEnter+=repaint;combo.MouseLeave+=repaint;combo.SelectedIndexChanged+=repaint;combo.EnabledChanged+=repaint;
        }
        // Single-line text boxes left in place: dark native edit theme (dark border and scroll bars on Windows 10 1809+).
        internal static void DarkEdit(TextBox box) {
            if(box==null||!Mark(box,"dark-edit"))return;
            EventHandler apply=(s,e)=> { try { SetWindowTheme(box.Handle,"DarkMode_CFD",null); }catch(EntryPointNotFoundException) {}catch(DllNotFoundException) {} };
            box.HandleCreated+=apply;if(box.IsHandleCreated)apply(box,EventArgs.Empty);
        }
        // Grey hint text inside an empty text box.
        internal static void Cue(TextBox box,string text) {
            EventHandler apply=(s,e)=>SendMessage(box.Handle,0x1501,(IntPtr)1,text??"");
            box.HandleCreated+=apply;if(box.IsHandleCreated)apply(box,EventArgs.Empty);
        }
        // Category trees: full-row selection with an accent bar, vector chevrons for branches, no native glyphs.
        internal static void Tree(TreeView tree) {
            if(tree==null||!Mark(tree,"tree"))return;
            tree.BackColor=Panel;tree.ForeColor=Ink;tree.BorderStyle=BorderStyle.None;tree.ShowLines=false;tree.ShowPlusMinus=false;tree.ShowRootLines=false;
            tree.FullRowSelect=true;tree.HideSelection=false;tree.HotTracking=false;tree.DrawMode=TreeViewDrawMode.OwnerDrawAll;tree.ItemHeight=Math.Max(tree.ItemHeight,28);
            const int step=16,lead=8,glyph=14;
            TreeNode hot=null;
            tree.DrawNode+=(s,e)=> {
                var node=e.Node;if(node==null||e.Bounds.Height<=0)return;
                var row=new Rectangle(0,e.Bounds.Y,tree.ClientSize.Width,e.Bounds.Height);
                bool selected=node==tree.SelectedNode,focused=tree.Focused;
                Color back=selected?(focused?AccentSoft:Mix(AccentSoft,Panel,.35)):node==hot?Hover:Panel;
                using(var fill=new SolidBrush(back))e.Graphics.FillRectangle(fill,row);
                if(selected)using(var bar=new SolidBrush(Accent))e.Graphics.FillRectangle(bar,0,row.Y,3,row.Height);
                int x=lead+node.Level*step;
                if(node.Nodes.Count>0)Chevron(e.Graphics,new Rectangle(x,row.Y,glyph,row.Height),node.IsExpanded?2:1,selected?Accent:Muted,3f);
                var font=node.Level==0?new Font(tree.Font,FontStyle.Bold):tree.Font;
                try {
                    TextRenderer.DrawText(e.Graphics,node.Text,font,new Rectangle(x+glyph+6,row.Y,row.Width-(x+glyph+10),row.Height),selected?Ink:node.Level==0?Ink:Mix(Ink,Muted,.35),
                        TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                } finally { if(font!=tree.Font)font.Dispose(); }
            };
            tree.NodeMouseClick+=(s,e)=> {
                if(e.Button!=MouseButtons.Left||e.Node.Nodes.Count==0)return;int x=lead+e.Node.Level*step;
                if(e.X>=x-4&&e.X<=x+glyph+4)e.Node.Toggle();
            };
            tree.MouseMove+=(s,e)=> { var node=tree.GetNodeAt(e.Location);if(node!=hot) { hot=node;tree.Invalidate(); } };
            tree.MouseLeave+=(s,e)=> { if(hot!=null) { hot=null;tree.Invalidate(); } };
            tree.GotFocus+=(s,e)=>tree.Invalidate();tree.LostFocus+=(s,e)=>tree.Invalidate();
            tree.AfterSelect+=(s,e)=>tree.Invalidate();tree.AfterExpand+=(s,e)=>tree.Invalidate();tree.AfterCollapse+=(s,e)=>tree.Invalidate();
        }
        // Detail lists: the last column stretches so no empty header strip is left beside it.
        internal static void FillLastColumn(ListView list) {
            if(list==null||list.View!=View.Details||!Mark(list,"fill-column"))return;
            // The last column takes exactly the remaining client width (never more, so no horizontal scroll bar
            // appears and the fit cannot oscillate); re-entry from the resulting resize is ignored.
            bool fitting=false;
            Action fit=()=> {
                if(fitting||list.Columns.Count==0||list.ClientSize.Width<=0)return;int used=0;
                for(int i=0;i<list.Columns.Count-1;i++)used+=list.Columns[i].Width;
                int width=Math.Max(80,list.ClientSize.Width-used-1);var last=list.Columns[list.Columns.Count-1];
                if(last.Width==width)return;
                fitting=true;try { last.Width=width; } finally { fitting=false; }
            };
            list.Resize+=(s,e)=>fit();list.ColumnWidthChanged+=(s,e)=> { if(e.ColumnIndex<list.Columns.Count-1)fit(); };list.HandleCreated+=(s,e)=>fit();list.VisibleChanged+=(s,e)=>fit();fit();
            // Taller rows: a 1 px wide image list sets the row height for small-icon and details views.
            if(list.SmallImageList==null) { var rows=new ImageList { ImageSize=new Size(1,Math.Max(24,list.Font.Height+10)) };list.SmallImageList=rows;list.Disposed+=(s,e)=>rows.Dispose(); }
        }
        [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr window,string subApp,string idList);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,string lParam);
    }

    // Drop-in replacement for MessageBox.Show in Windows Forms code: same arguments and results,
    // drawn in the interface theme (icon tile, title, wrapped text, primary action on the right).
    internal static class ThemedMessage {
        internal static DialogResult Show(string text) { return Show(null,text,"SCUM MiniMap",MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1); }
        internal static DialogResult Show(string text,string caption) { return Show(null,text,caption,MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1); }
        internal static DialogResult Show(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon) { return Show(null,text,caption,buttons,icon,MessageBoxDefaultButton.Button1); }
        internal static DialogResult Show(IWin32Window owner,string text) { return Show(owner,text,"SCUM MiniMap",MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1); }
        internal static DialogResult Show(IWin32Window owner,string text,string caption) { return Show(owner,text,caption,MessageBoxButtons.OK,MessageBoxIcon.None,MessageBoxDefaultButton.Button1); }
        internal static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons) { return Show(owner,text,caption,buttons,MessageBoxIcon.None,MessageBoxDefaultButton.Button1); }
        internal static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon) { return Show(owner,text,caption,buttons,icon,MessageBoxDefaultButton.Button1); }
        internal static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon,MessageBoxDefaultButton defaultButton) {
            try {
                using(var dialog=Build(text,caption,buttons,icon,defaultButton)) {
                    var parent=owner as Control;
                    if(parent!=null&&(parent.IsDisposed||!parent.IsHandleCreated||!parent.Visible))owner=null;
                    dialog.StartPosition=owner==null?FormStartPosition.CenterScreen:FormStartPosition.CenterParent;
                    return owner==null?dialog.ShowDialog():dialog.ShowDialog(owner);
                }
            } catch(Exception error) {
                // Never lose the message: fall back to the system box if the themed one cannot be shown.
                Program.LogException("ThemedMessage",error);
                return MessageBox.Show(owner,text,caption,buttons,icon,defaultButton);
            }
        }
        static string Label(DialogResult result) {
            switch(result) {
                case DialogResult.OK:return Localization.Get("DialogOk");
                case DialogResult.Cancel:return Localization.Get("Cancel");
                case DialogResult.Yes:return Localization.Get("DialogYes");
                case DialogResult.No:return Localization.Get("DialogNo");
                case DialogResult.Retry:return Localization.Get("DialogRetry");
                case DialogResult.Abort:return Localization.Get("DialogAbort");
                default:return Localization.Get("DialogIgnore");
            }
        }
        static DialogResult[] Results(MessageBoxButtons buttons) {
            switch(buttons) {
                case MessageBoxButtons.OKCancel:return new[]{DialogResult.OK,DialogResult.Cancel};
                case MessageBoxButtons.YesNo:return new[]{DialogResult.Yes,DialogResult.No};
                case MessageBoxButtons.YesNoCancel:return new[]{DialogResult.Yes,DialogResult.No,DialogResult.Cancel};
                case MessageBoxButtons.RetryCancel:return new[]{DialogResult.Retry,DialogResult.Cancel};
                case MessageBoxButtons.AbortRetryIgnore:return new[]{DialogResult.Abort,DialogResult.Retry,DialogResult.Ignore};
                default:return new[]{DialogResult.OK};
            }
        }
        static Form Build(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon,MessageBoxDefaultButton defaultButton) {
            var form=new Form { Text=caption??"SCUM MiniMap",FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,TopMost=true,
                BackColor=WinTheme.Ground,ForeColor=WinTheme.Ink,Font=WinTheme.Body(10f),AutoScaleMode=AutoScaleMode.Dpi,KeyPreview=true };
            WinTheme.Caption(form);
            string glyph=icon==MessageBoxIcon.Warning?"triangle-alert":icon==MessageBoxIcon.Error?"circle-x":icon==MessageBoxIcon.Question?"circle-help":icon==MessageBoxIcon.Information?"info":null;
            Color tone=icon==MessageBoxIcon.Warning?WinTheme.Parse(WinTheme.Spec.Warning):icon==MessageBoxIcon.Error?WinTheme.Danger:WinTheme.Accent;
            int left=24,width=420;
            if(glyph!=null) {
                var tile=new Panel { Location=new Point(24,24),Size=new Size(40,40),BackColor=WinTheme.Ground };
                var image=WinTheme.Icon(glyph,20,tone);
                tile.Paint+=(s,e)=> {
                    WinTheme.FillShape(e.Graphics,new Rectangle(0,0,39,39),WinTheme.Alpha(tone,0x2A,WinTheme.Ground),Color.Transparent,WinTheme.Caps?0:10);
                    if(image!=null)e.Graphics.DrawImage(image,new Rectangle(10,10,20,20));
                };
                form.Disposed+=(s,e)=> { if(image!=null)image.Dispose(); };
                form.Controls.Add(tile);left=80;
            }
            var title=new Label { Text=WinTheme.Upper(caption??""),Location=new Point(left,24),AutoSize=false,Size=new Size(width,26),Font=WinTheme.Display(12.5f),ForeColor=WinTheme.Ink,AutoEllipsis=true };
            var measured=TextRenderer.MeasureText(text??"",form.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            int textHeight=Math.Min(420,Math.Max(22,measured.Height+4));
            var body=new Label { Text=text??"",Location=new Point(left,56),Size=new Size(width,textHeight),ForeColor=WinTheme.Mix(WinTheme.Ink,WinTheme.Muted,.35),UseMnemonic=false,AutoEllipsis=measured.Height>textHeight };
            form.Controls.Add(title);form.Controls.Add(body);
            int footerTop=Math.Max(56+textHeight+20,88);
            var footer=new Panel { Location=new Point(0,footerTop),Size=new Size(left+width+24,64),BackColor=WinTheme.Panel };
            footer.Paint+=(s,e)=> { using(var line=new Pen(WinTheme.Line))e.Graphics.DrawLine(line,0,0,footer.Width,0); };
            var results=Results(buttons);int x=footer.Width-24;Button accept=null,cancel=null;
            for(int i=results.Length-1;i>=0;i--) {
                var result=results[i];var button=new Button { Text=Label(result),DialogResult=result,Height=34,Font=WinTheme.Body(9.5f,FontStyle.Bold) };
                button.Width=Math.Max(96,TextRenderer.MeasureText(WinTheme.Upper(button.Text),button.Font).Width+36);x-=button.Width;button.Location=new Point(x,15);x-=8;
                if(i==0)WinTheme.Primary(button);else WinTheme.Secondary(button);
                footer.Controls.Add(button);
                if(i==(int)defaultButton/256)accept=button;
                if(result==DialogResult.Cancel||result==DialogResult.No||results.Length==1)cancel=button;
            }
            form.AcceptButton=accept??(Button)footer.Controls[footer.Controls.Count-1];form.CancelButton=cancel;
            form.Controls.Add(footer);form.ClientSize=new Size(footer.Width,footerTop+footer.Height);
            form.Shown+=(s,e)=> { var focus=form.AcceptButton as Button;if(focus!=null)focus.Focus(); };
            return form;
        }
    }
}
