using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ScumMiniMap {
    internal sealed partial class WpfFieldConsoleRuntime {
        ScreenFilterController screenFilter;

        void StartScreenFilter() {
            screenFilter=new ScreenFilterController(engine.WpfDataFolder,application.Dispatcher);
            screenFilter.Changed+=()=> { if(screenFilterPage!=null)SyncScreenFilterPage(); };
            if(!diagnostic)screenFilter.Start();
        }
        void ToggleScreenFilter() { if(screenFilter!=null)screenFilter.SetEnabled(!screenFilter.Settings.Enabled); }
        void CycleScreenFilter() { if(screenFilter!=null)screenFilter.CyclePreset(); }

        StackPanel screenFilterPage;Action screenFilterRefresh;ScreenFilterSettings screenFilterDraft;DispatcherTimer screenFilterSave;

        // Half-filled sun: reads as "brightness" at rail size.
        static UIElement ScreenFilterRailIcon() {
            var canvas=new Grid { Width=18,Height=18 };
            var ring=new Ellipse { Width=11,Height=11,StrokeThickness=1.6,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
            var half=new Path { Data=Geometry.Parse("M 9,3.5 A 5.5,5.5 0 0 1 9,14.5 Z"),Width=18,Height=18,Stretch=Stretch.None };
            var rays=new Path { Data=Geometry.Parse("M 9,0 L 9,2 M 9,16 L 9,18 M 0,9 L 2,9 M 16,9 L 18,9 M 2.6,2.6 L 4,4 M 14,14 L 15.4,15.4 M 2.6,15.4 L 4,14 M 14,4 L 15.4,2.6"),StrokeThickness=1.4,Width=18,Height=18,Stretch=Stretch.None };
            var binding=new System.Windows.Data.Binding("Foreground") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1) };
            ring.SetBinding(Shape.StrokeProperty,binding);half.SetBinding(Shape.FillProperty,binding);rays.SetBinding(Shape.StrokeProperty,binding);
            canvas.Children.Add(ring);canvas.Children.Add(half);canvas.Children.Add(rays);return canvas;
        }

        string ScreenFilterHotkeys() {
            Func<AppControlAction,string> name=action=> { int key=engine.AppControlKey(action);return key==0?Localization.Get("ScreenFilterUnbound"):PhysicalKeyCapture.KeyName(key); };
            return string.Format(Localization.Get("ScreenFilterHotkeys"),name(AppControlAction.ToggleScreenFilter),name(AppControlAction.CycleScreenFilter));
        }

        StackPanel ScreenFilterSidebarPage() {
            if(screenFilterPage!=null)return screenFilterPage;
            screenFilterPage=new StackPanel { Tag="sidebar-page:screen-filter" };screenFilterDraft=screenFilter.Settings.Clone();var draft=screenFilterDraft;
            screenFilterPage.Children.Add(WpfFieldTheme.Text(Localization.Get("ScreenFilter"),23,null,true));
            screenFilterSave=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) };screenFilterSave.Tick+=(s,e)=> { screenFilterSave.Stop();screenFilter.Save(); };
            bool loading=false;
            Action changed=()=> { if(loading)return;draft.Clamp();screenFilter.Apply(draft.Clone());screenFilterSave.Stop();screenFilterSave.Start(); };

            var enabled=new CheckBox { Content=WpfFieldTheme.Text(Localization.Get("ScreenFilterEnable"),15,null,true),Margin=new Thickness(0,12,0,6),Tag="screen-filter-enabled" };
            enabled.Click+=(s,e)=> { draft.Enabled=enabled.IsChecked==true;changed(); };screenFilterPage.Children.Add(enabled);
            Func<string,double,Brush,TextBlock> para=(text,size,ink)=> { var t=WpfFieldTheme.Text(text,size,ink);t.TextWrapping=TextWrapping.Wrap;t.TextTrimming=TextTrimming.None;return t; };
            var help=para(Localization.Get("ScreenFilterHelp"),12,WpfFieldTheme.Muted);help.Margin=new Thickness(0,0,0,8);screenFilterPage.Children.Add(help);
            var status=para("",12,WpfFieldTheme.Amber);status.Tag="screen-filter-status";status.Margin=new Thickness(0,0,0,4);screenFilterPage.Children.Add(status);
            var unlock=WpfFieldTheme.Button(Localization.Get("ScreenFilterUnlock"),()=> {
                if(MessageBox.Show(Localization.Get("ScreenFilterUnlockConfirm"),Localization.Get("ScreenFilter"),MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK)return;
                MessageBox.Show(Localization.Get(GammaDevice.AllowFullRange()?"ScreenFilterUnlockDone":"ScreenFilterUnlockFailed"),Localization.Get("ScreenFilter"));
                screenFilter.Apply(screenFilter.Settings.Clone());SyncScreenFilterPage();
            });
            unlock.Tag="screen-filter-unlock";unlock.HorizontalAlignment=HorizontalAlignment.Left;unlock.Margin=new Thickness(0,4,0,4);screenFilterPage.Children.Add(unlock);

            var presetCaption=WpfFieldTheme.Text(Localization.Get("ScreenFilterPreset"),14,null,true);presetCaption.Margin=new Thickness(0,10,0,6);screenFilterPage.Children.Add(presetCaption);
            var presetRow=new UniformGrid { Columns=4 };screenFilterPage.Children.Add(presetRow);
            var presetButtons=ScreenFilterSettings.Presets.Select(preset=> {
                var b=new Button { Content=Localization.Get("ScreenFilterPreset"+preset),MinHeight=32,Padding=new Thickness(4,4,4,4),Margin=new Thickness(0,0,4,0),BorderThickness=new Thickness(1),Tag="screen-filter-preset:"+preset };
                b.Click+=(s,e)=> { draft.Preset=preset;if(!draft.Enabled)draft.Enabled=true;screenFilterRefresh(); };presetRow.Children.Add(b);return b;
            }).ToArray();

            var curve=new Polyline { StrokeThickness=2,Stroke=WpfFieldTheme.Accent };
            var diagonal=new Line { X1=0,Y1=100,X2=100,Y2=0,StrokeThickness=1,Stroke=WpfFieldTheme.Line,StrokeDashArray=new DoubleCollection{3,3} };
            var plot=new Canvas { Width=100,Height=100 };plot.Children.Add(diagonal);plot.Children.Add(curve);
            var plotFrame=new Border { BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),Background=WpfFieldTheme.Ground,Margin=new Thickness(0,12,0,0),Height=110,Child=new Viewbox { Child=plot,Stretch=Stretch.Fill,Margin=new Thickness(4) } };
            screenFilterPage.Children.Add(plotFrame);
            var plotHint=para(Localization.Get("ScreenFilterCurve"),11,WpfFieldTheme.Muted);plotHint.Margin=new Thickness(0,2,0,0);screenFilterPage.Children.Add(plotHint);

            Func<string,int,int,Func<ScreenFilterValues,int>,Action<ScreenFilterValues,int>,Func<int,string>,Slider> slider=(key,min,max,read,write,format)=> {
                var row=new DockPanel { Margin=new Thickness(0,10,0,0) };var value=WpfFieldTheme.Text("",14,WpfFieldTheme.Amber,true);value.MinWidth=52;value.TextAlignment=TextAlignment.Right;DockPanel.SetDock(value,Dock.Right);row.Children.Add(value);
                row.Children.Add(WpfFieldTheme.Text(Localization.Get(key),14));screenFilterPage.Children.Add(row);
                var c=new Slider { Minimum=min,Maximum=max,TickFrequency=1,IsSnapToTickEnabled=true,SmallChange=1,LargeChange=10,Margin=new Thickness(0,2,0,0),Tag="screen-filter-"+key };
                System.Windows.Automation.AutomationProperties.SetName(c,Localization.Get(key));
                c.ValueChanged+=(s,e)=> { int v=(int)Math.Round(c.Value);value.Text=format(v);if(loading)return;write(draft.Current,v);changed();screenFilterRefreshView(); };
                screenFilterPage.Children.Add(c);c.Resources["read"]=read;c.Resources["label"]=value;c.Resources["format"]=format;return c;
            };
            Func<int,string> percent=v=>v.ToString(CultureInfo.CurrentCulture)+"%";
            var sliders=new[]{
                slider("ScreenFilterGamma",50,300,v=>v.Gamma,(v,x)=>v.Gamma=x,x=>(x/100.0).ToString("0.00",CultureInfo.CurrentCulture)),
                slider("ScreenFilterBrightness",-50,50,v=>v.Brightness,(v,x)=>v.Brightness=x,x=>(x>0?"+":"")+x.ToString(CultureInfo.CurrentCulture)),
                slider("ScreenFilterContrast",50,200,v=>v.Contrast,(v,x)=>v.Contrast=x,percent),
                slider("ScreenFilterSaturation",0,200,v=>v.Saturation,(v,x)=>v.Saturation=x,percent)};
            var saturationNote=para(Localization.Get("ScreenFilterSaturationNote"),11,WpfFieldTheme.Muted);saturationNote.Margin=new Thickness(0,2,0,0);screenFilterPage.Children.Add(saturationNote);

            var onlyGame=new CheckBox { Content=para(Localization.Get("ScreenFilterOnlyGame"),14,WpfFieldTheme.Ink),Margin=new Thickness(0,14,0,4),Tag="screen-filter-only-game" };
            onlyGame.Click+=(s,e)=> { draft.OnlyWhileGame=onlyGame.IsChecked==true;changed(); };screenFilterPage.Children.Add(onlyGame);
            var hotkeys=para("",12,WpfFieldTheme.Muted);hotkeys.Margin=new Thickness(0,8,0,0);screenFilterPage.Children.Add(hotkeys);
            var reset=WpfFieldTheme.Button(Localization.Get("ScreenFilterReset"),()=> { var d=ScreenFilterSettings.Default(draft.Preset);var c=draft.Current;c.Brightness=d.Brightness;c.Contrast=d.Contrast;c.Gamma=d.Gamma;c.Saturation=d.Saturation;screenFilterRefresh();changed(); });
            reset.Tag="screen-filter-reset";reset.HorizontalAlignment=HorizontalAlignment.Left;reset.Margin=new Thickness(0,14,0,0);screenFilterPage.Children.Add(reset);

            screenFilterRefresh=()=> {
                bool wasLoading=loading;loading=true;
                try {
                    enabled.IsChecked=draft.Enabled;onlyGame.IsChecked=draft.OnlyWhileGame;
                    for(int i=0;i<presetButtons.Length;i++) {
                        bool on=ScreenFilterSettings.Presets[i]==draft.Preset;
                        presetButtons[i].Background=on?WpfFieldTheme.AccentDim:WpfFieldTheme.Raised;presetButtons[i].BorderBrush=on?WpfFieldTheme.Accent:WpfFieldTheme.Line;presetButtons[i].Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Ink;
                    }
                    foreach(var c in sliders) {
                        c.Value=((Func<ScreenFilterValues,int>)c.Resources["read"])(draft.Current);
                        ((TextBlock)c.Resources["label"]).Text=((Func<int,string>)c.Resources["format"])((int)Math.Round(c.Value));
                    }
                    var points=new PointCollection();for(int i=0;i<=50;i++) { double x=i/50.0;points.Add(new Point(x*100,100-ScreenFilterMath.Level(draft.Current,x)*100)); }curve.Points=points;
                    string state;
                    switch(screenFilter.State) {
                        case ScreenFilterController.Status.Active:state=Localization.Get("ScreenFilterStateActive");break;
                        case ScreenFilterController.Status.Limited:state=string.Format(Localization.Get("ScreenFilterStateLimited"),Math.Round(screenFilter.Strength*100));break;
                        case ScreenFilterController.Status.Waiting:state=Localization.Get("ScreenFilterStateWaiting");break;
                        case ScreenFilterController.Status.Failed:state=Localization.Get("ScreenFilterStateFailed");break;
                        default:state=Localization.Get("ScreenFilterStateOff");break;
                    }
                    status.Text=state;status.Foreground=screenFilter.State==ScreenFilterController.Status.Limited||screenFilter.State==ScreenFilterController.Status.Failed?WpfFieldTheme.Warn:screenFilter.State==ScreenFilterController.Status.Active?WpfFieldTheme.Amber:WpfFieldTheme.Muted;
                    unlock.Visibility=screenFilter.State==ScreenFilterController.Status.Limited&&!GammaDevice.FullRangeAllowed()?Visibility.Visible:Visibility.Collapsed;
                    hotkeys.Text=ScreenFilterHotkeys();
                } finally { loading=wasLoading; }
            };
            // Preset buttons and the enable box route through here as well.
            Action refreshAndApply=screenFilterRefresh;
            screenFilterRefresh=()=> { refreshAndApply();changed(); };
            loading=true;refreshAndApply();loading=false;
            screenFilterRefreshView=refreshAndApply;
            return screenFilterPage;
        }
        Action screenFilterRefreshView;
        // Pick up hotkey changes and status updates while the page exists.
        void SyncScreenFilterPage() {
            if(screenFilterPage==null||screenFilterRefreshView==null)return;
            if(screenFilterDraft.Serialize()!=screenFilter.Settings.Serialize())screenFilterDraft.CopyFrom(screenFilter.Settings);
            screenFilterRefreshView();
        }
    }
}
