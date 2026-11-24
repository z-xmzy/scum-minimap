using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScumMiniMap {
    internal sealed partial class WpfFieldConsoleRuntime {
        CrosshairOverlay crosshair;

        void StartCrosshair() {
            crosshair=new CrosshairOverlay(engine.WpfDataFolder,()=>TacticalVisible||hudMoving,application.Dispatcher);
            if(!diagnostic)crosshair.Start();
        }
        void ToggleCrosshair() { if(crosshair!=null)crosshair.SetEnabled(!crosshair.Settings.Enabled); }
        void ShowCrosshairEditor() { Modal(BuildCrosshairEditor()); }

        StackPanel crosshairPage;Action crosshairPageRefresh;CrosshairSettings crosshairPageDraft;DispatcherTimer crosshairSave;
        // Ring with four ticks; the stroke follows the rail button's highlight colour.
        static UIElement CrosshairRailIcon() {
            var path=new System.Windows.Shapes.Path { Data=Geometry.Parse("M 10,0 L 10,6 M 10,14 L 10,20 M 0,10 L 6,10 M 14,10 L 20,10 M 10,3.5 A 6.5,6.5 0 1 1 9.99,3.5"),StrokeThickness=1.6,Width=17,Height=17,Stretch=Stretch.Uniform };
            path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1) });
            return path;
        }
        // Full-map sidebar page: edits apply live and save shortly after.
        StackPanel CrosshairSidebarPage() {
            if(crosshairPage!=null)return crosshairPage;
            crosshairPage=new StackPanel { Tag="sidebar-page:crosshair" };crosshairPageDraft=crosshair.Settings.Clone();var draft=crosshairPageDraft;
            crosshairPage.Children.Add(WpfFieldTheme.Text(Localization.Get("Crosshair"),23,null,true));
            var preview=new CrosshairSurface { Settings=draft,Backdrop=CrosshairBackdrop(),Height=120,Margin=new Thickness(0),Tag="crosshair-sidebar-preview",SnapsToDevicePixels=true };
            crosshairPage.Children.Add(new Border { BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Margin=new Thickness(0,12,0,4),Child=preview });
            var hint=WpfFieldTheme.Text(Localization.Get("CrosshairPreviewHint"),11,WpfFieldTheme.Muted);hint.Margin=new Thickness(0,0,0,8);crosshairPage.Children.Add(hint);
            crosshairSave=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) };crosshairSave.Tick+=(s,e)=> { crosshairSave.Stop();crosshair.Save(); };
            var controls=new StackPanel();crosshairPage.Children.Add(controls);
            crosshairPageRefresh=BuildCrosshairControls(controls,draft,()=> { crosshair.Apply(draft.Clone());preview.InvalidateVisual();crosshairSave.Stop();crosshairSave.Start(); },true);
            var reset=WpfFieldTheme.Button(Localization.Get("CrosshairReset"),()=> { draft.CopyFrom(new CrosshairSettings { Enabled=draft.Enabled });crosshairPageRefresh(); });
            reset.Tag="crosshair-reset";reset.HorizontalAlignment=HorizontalAlignment.Left;reset.Margin=new Thickness(0,14,0,0);crosshairPage.Children.Add(reset);
            crosshairPageRefresh();return crosshairPage;
        }
        // Pick up changes made by the hotkey or the settings window.
        void SyncCrosshairPage() {
            if(crosshairPage==null||crosshairPageRefresh==null)return;
            if(crosshairPageDraft.Serialize()==crosshair.Settings.Serialize())return;
            crosshairPageDraft.CopyFrom(crosshair.Settings);crosshairPageRefresh();
        }


        void AddCrosshairSettings(StackPanel panel) {
            var label=WpfFieldTheme.Text(Localization.Get("CrosshairEnable"),16);label.TextWrapping=TextWrapping.Wrap;
            var enabled=new CheckBox { Content=label,IsChecked=crosshair.Settings.Enabled,Margin=new Thickness(0,8,0,8),Tag="crosshair-enabled" };
            enabled.Click+=(s,e)=>crosshair.SetEnabled(enabled.IsChecked==true);panel.Children.Add(enabled);
            var help=WpfFieldTheme.Text(Localization.Get("CrosshairHelp"),14,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;help.TextTrimming=TextTrimming.None;help.Margin=new Thickness(0,0,0,10);panel.Children.Add(help);
            var customize=WpfFieldTheme.Button(Localization.Get("CrosshairCustomize"),()=> { ShowCrosshairEditor();enabled.IsChecked=crosshair.Settings.Enabled; });
            customize.Tag="text:CrosshairCustomize";customize.HorizontalAlignment=HorizontalAlignment.Left;panel.Children.Add(customize);
        }

        // Split sky/ground backdrop so contrast can be judged on both.
        static Brush CrosshairBackdrop() {
            var brush=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(0,1) };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x9F,0xB8,0xC4),0));brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xC9,0xD4,0xD2),.5));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x4A,0x52,0x3C),.5));brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x23,0x29,0x1E),1));
            brush.Freeze();return brush;
        }

        // Shared by the editor window and the full-map sidebar page.
        // Returns an action that reloads every control from the draft.
        Action BuildCrosshairControls(StackPanel controls,CrosshairSettings draft,Action apply,bool compact) {
            bool loading=false;Action refreshControls=null;
            Action changed=()=> { if(loading)return;draft.Clamp();apply(); };
            double big=compact?14:15;

            var enabledLabel=WpfFieldTheme.Text(Localization.Get("CrosshairEnable"),compact?15:16,null,true);
            var enabled=new CheckBox { Content=enabledLabel,Margin=new Thickness(0,0,0,6),Tag="crosshair-enabled" };enabled.Click+=(s,e)=> { draft.Enabled=enabled.IsChecked==true;changed(); };controls.Children.Add(enabled);
            var help=WpfFieldTheme.Text(Localization.Get("CrosshairHelp"),compact?12:13,WpfFieldTheme.Muted);help.TextWrapping=TextWrapping.Wrap;help.TextTrimming=TextTrimming.None;help.Margin=new Thickness(0,0,0,12);controls.Children.Add(help);

            Func<string,TextBlock> caption=key=> { var t=WpfFieldTheme.Text(Localization.Get(key),big,null,true);t.Margin=new Thickness(0,10,0,4);return t; };
            controls.Children.Add(caption("CrosshairStyle"));
            var styles=(CrosshairStyle[])Enum.GetValues(typeof(CrosshairStyle));
            var style=new ComboBox { ItemsSource=styles.Select(item=>Localization.Get("CrosshairStyle"+item)).ToArray(),MinWidth=compact?0:220,HorizontalAlignment=compact?HorizontalAlignment.Stretch:HorizontalAlignment.Left,Tag="crosshair-style" };
            style.SelectionChanged+=(s,e)=> { if(style.SelectedIndex>=0) { draft.Style=styles[style.SelectedIndex];changed(); } };controls.Children.Add(style);

            controls.Children.Add(caption("CrosshairColour"));
            var colourRow=new WrapPanel();controls.Children.Add(colourRow);
            var hex=new TextBox { Width=compact?84:96,MaxLength=7,Margin=new Thickness(compact?0:8,0,0,6),VerticalContentAlignment=VerticalAlignment.Center,Tag="crosshair-colour" };
            foreach(string preset in CrosshairSettings.PresetColours) {
                Color colour;CrosshairSettings.TryParseColour(preset,out colour);
                int side=compact?26:30;var swatch=new Button { Width=side,Height=side,MinHeight=side,Margin=new Thickness(0,0,6,6),Background=new SolidColorBrush(colour),BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),ToolTip=preset,Tag="crosshair-swatch" };
                System.Windows.Automation.AutomationProperties.SetName(swatch,preset);
                swatch.Click+=(s,e)=> { draft.Colour=colour;loading=true;hex.Text=preset;loading=false;changed(); };colourRow.Children.Add(swatch);
            }
            hex.TextChanged+=(s,e)=> { Color colour;if(CrosshairSettings.TryParseColour(hex.Text,out colour)) { draft.Colour=colour;hex.BorderBrush=WpfFieldTheme.Line;changed(); }else hex.BorderBrush=WpfFieldTheme.Warn; };
            colourRow.Children.Add(hex);

            Func<string,int,int,Func<int>,Action<int>,Slider> slider=(key,min,max,read,write)=> {
                var row=new DockPanel { Margin=new Thickness(0,10,0,0) };var value=WpfFieldTheme.Text("",big,WpfFieldTheme.Amber,true);value.MinWidth=44;value.TextAlignment=TextAlignment.Right;DockPanel.SetDock(value,Dock.Right);row.Children.Add(value);
                row.Children.Add(WpfFieldTheme.Text(Localization.Get(key),big));controls.Children.Add(row);
                var c=new Slider { Minimum=min,Maximum=max,TickFrequency=1,IsSnapToTickEnabled=true,SmallChange=1,LargeChange=5,Margin=new Thickness(0,2,0,0),Tag="crosshair-"+key };
                System.Windows.Automation.AutomationProperties.SetName(c,Localization.Get(key));
                c.ValueChanged+=(s,e)=> { int v=(int)Math.Round(c.Value);value.Text=v.ToString(CultureInfo.CurrentCulture);if(loading)return;write(v);changed(); };
                controls.Children.Add(c);c.Resources["read"]=read;c.Resources["label"]=value;return c;
            };
            var sliders=new[]{
                slider("CrosshairLength",1,60,()=>draft.Length,v=>draft.Length=v),
                slider("CrosshairThickness",1,12,()=>draft.Thickness,v=>draft.Thickness=v),
                slider("CrosshairGap",0,40,()=>draft.Gap,v=>draft.Gap=v),
                slider("CrosshairDotSize",1,16,()=>draft.DotSize,v=>draft.DotSize=v),
                slider("CrosshairOpacity",10,100,()=>draft.Opacity,v=>draft.Opacity=v),
                slider("CrosshairOffsetX",-400,400,()=>draft.OffsetX,v=>draft.OffsetX=v),
                slider("CrosshairOffsetY",-400,400,()=>draft.OffsetY,v=>draft.OffsetY=v)};

            var outline=new CheckBox { Content=WpfFieldTheme.Text(Localization.Get("CrosshairOutline"),big),Margin=new Thickness(0,16,0,4),Tag="crosshair-outline" };
            outline.Click+=(s,e)=> { draft.Outline=outline.IsChecked==true;changed(); };controls.Children.Add(outline);
            var aimingLabel=WpfFieldTheme.Text(Localization.Get("CrosshairHideAiming"),big);aimingLabel.TextWrapping=TextWrapping.Wrap;
            var aiming=new CheckBox { Content=aimingLabel,Margin=new Thickness(0,4,0,4),Tag="crosshair-hide-aiming" };
            aiming.Click+=(s,e)=> { draft.HideWhileAiming=aiming.IsChecked==true;changed(); };controls.Children.Add(aiming);

            refreshControls=()=> {
                loading=true;
                try {
                    enabled.IsChecked=draft.Enabled;style.SelectedIndex=Array.IndexOf(styles,draft.Style);hex.Text=CrosshairSettings.Hex(draft.Colour);hex.BorderBrush=WpfFieldTheme.Line;
                    foreach(var c in sliders) { c.Value=((Func<int>)c.Resources["read"])(); }
                    outline.IsChecked=draft.Outline;aiming.IsChecked=draft.HideWhileAiming;
                }finally { loading=false; }
                // Slider labels update from ValueChanged; force them when the value did not move.
                foreach(var c in sliders)((TextBlock)c.Resources["label"]).Text=((int)Math.Round(c.Value)).ToString(CultureInfo.CurrentCulture);
                changed();
            };
            return refreshControls;
        }

        internal Window BuildCrosshairEditor() {
            var original=crosshair.Settings.Clone();var draft=original.Clone();bool committed=false;
            var window=Window(Localization.Get("CrosshairCustomize"),860,660);window.WindowStyle=WindowStyle.SingleBorderWindow;window.Tag="crosshair-editor";
            var root=new DockPanel { Margin=new Thickness(26,20,26,20) };
            var heading=WpfFieldTheme.Text(Localization.Get("CrosshairCustomize"),26,null,true);heading.Margin=new Thickness(0,0,0,14);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            var footer=new DockPanel { Margin=new Thickness(0,16,0,0) };DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var workspace=new Grid();workspace.ColumnDefinitions.Add(new ColumnDefinition());workspace.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(320) });root.Children.Add(workspace);
            var controls=new StackPanel { Margin=new Thickness(0,0,20,0) };workspace.Children.Add(new ScrollViewer { Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });

            var previewColumn=new StackPanel();Grid.SetColumn(previewColumn,1);workspace.Children.Add(previewColumn);
            previewColumn.Children.Add(WpfFieldTheme.Eyebrow(Localization.Get("CrosshairPreviewHint")));
            var preview=new CrosshairSurface { Settings=draft,Backdrop=CrosshairBackdrop(),Width=280,Height=280,Tag="crosshair-preview",SnapsToDevicePixels=true };
            previewColumn.Children.Add(new Border { BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Child=preview,HorizontalAlignment=HorizontalAlignment.Left });
            var zoomLabel=WpfFieldTheme.Eyebrow("×3");zoomLabel.Margin=new Thickness(0,14,0,6);previewColumn.Children.Add(zoomLabel);
            var zoomHost=new Border { BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Left,ClipToBounds=true,Width=282,Height=152 };
            var zoomVisual=new CrosshairMagnifier(draft,CrosshairBackdrop()) { Tag="crosshair-zoom" };zoomHost.Child=zoomVisual;previewColumn.Children.Add(zoomHost);

            Action refreshControls=BuildCrosshairControls(controls,draft,()=> { crosshair.Apply(draft.Clone());preview.InvalidateVisual();zoomVisual.Refresh(); },false);

            var reset=WpfFieldTheme.Button(Localization.Get("CrosshairReset"),()=> { draft.CopyFrom(new CrosshairSettings { Enabled=draft.Enabled });refreshControls(); });
            reset.Tag="crosshair-reset";DockPanel.SetDock(reset,Dock.Left);footer.Children.Add(reset);
            var cancel=WpfFieldTheme.Button(Localization.Get("Cancel"),()=>window.Close());
            var done=WpfFieldTheme.Button(Localization.Get("Done"),()=> { committed=true;crosshair.Apply(draft.Clone());crosshair.Save();window.Close(); },true);done.MinWidth=120;
            var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };buttons.Children.Add(cancel);buttons.Children.Add(done);footer.Children.Add(buttons);
            window.Closed+=(s,e)=> { if(!committed)crosshair.Apply(original); };
            refreshControls();window.Content=root;return window;
        }

        // Shows the middle of the crosshair magnified ×3 without smoothing.
        sealed class CrosshairMagnifier:FrameworkElement {
            const int Size=100,Scale=3;
            readonly CrosshairSettings settings;readonly Brush backdrop;
            internal CrosshairMagnifier(CrosshairSettings settings,Brush backdrop) { this.settings=settings;this.backdrop=backdrop;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.NearestNeighbor); }
            internal void Refresh() { InvalidateVisual(); }
            protected override void OnRender(DrawingContext context) {
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen()) {
                    dc.DrawRectangle(backdrop,null,new Rect(0,0,Size,Size));
                    double odd=(settings.Style==CrosshairStyle.Dot?settings.DotSize:settings.Thickness)%2==1?0.5:0;
                    CrosshairRenderer.Draw(dc,settings,Size/2+odd,Size/2+odd);
                }
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(Size,Size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
                context.DrawImage(bitmap,new Rect(ActualWidth/2-Size*Scale/2.0,ActualHeight/2-Size*Scale/2.0,Size*Scale,Size*Scale));
            }
        }
    }
}
