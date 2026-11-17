using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        internal string WpfRouteMapId { get { return mapSidecarPaths.MapId; } }
    }
    internal sealed partial class WpfFieldConsoleRuntime {
        SavedRoute activeSavedRoute;
        bool returningToStart;


        void StartSavedRoute(SavedRoute route) {
            if(route.Map!=engine.WpfRouteMapId)throw new InvalidDataException();
            activeSavedRoute=route;returningToStart=false;
            BeginRoute(route.Stops);
        }
        string SavedStopRole(int index,SavedRoute route) {
            return S(index==0?"LxStart":!route.Loop&&index==route.Stops.Length-1?"LxEnd":"LxVia");
        }
        IEnumerable<SharedMapMarker> RemainingStops() {
            if(returningToStart&&activeSavedRoute!=null)return activeStops.Take(1);
            var stops=activeStops.Skip(activeStop);
            return activeSavedRoute!=null&&activeSavedRoute.Loop?stops.Concat(activeStops.Take(1)):stops;
        }
        double ArrivalRadius() {
            if(activeSavedRoute==null)return 40;
            // Keep the arrival circles of adjacent saved stops separate, including the closing leg.
            double radius=40;
            for(int offset=-1;offset<=1;offset+=2) {
                int n=activeStop+offset;
                if(activeSavedRoute.Loop)n=(n+activeStops.Length)%activeStops.Length;
                if(n<0||n>=activeStops.Length)continue;
                radius=Math.Min(radius,DestinationSearch.Metres(PointOf(activeStops[n]),PointOf(activeStops[activeStop]))*.5);
            }
            return radius;
        }
        static System.Drawing.PointF PointOf(SharedMapMarker m) { return new System.Drawing.PointF((float)m.X,(float)m.Y); }

        void AddSavedRoutes(Panel parent,Window window) {
            var panel=new StackPanel();
            parent.Children.Add(new Expander { Header=S("LxLibrary"),Content=panel,IsExpanded=true,Margin=new Thickness(0,10,0,12) });
            panel.Children.Add(SquadHelp("LxHelp"));
            var name=new TextBox { MaxLength=64,Tag="lx-name" };
            panel.Children.Add(WpfFieldTheme.Text(S("LxName"),13));panel.Children.Add(name);
            var kind=new ComboBox { Tag="lx-kind",ItemsSource=new[]{S("LxNormal"),S("LxLoop")},SelectedIndex=0 };
            panel.Children.Add(kind);
            var status=WpfFieldTheme.Text("",13,WpfFieldTheme.Amber);status.TextWrapping=TextWrapping.Wrap;
            var list=new ComboBox { Tag="lx-list",MinWidth=160 };
            var preview=WpfFieldTheme.Text("",13);preview.TextWrapping=TextWrapping.Wrap;preview.TextTrimming=TextTrimming.None;
            var code=new TextBox { Tag="lx-code",TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
            string map=engine.WpfRouteMapId,dir=Path.Combine(engine.WpfDataFolder,"maps",map,"routes");
            Action<SavedRoute> select=route=> {
                var same=list.Items.Cast<SavedRoute>().FirstOrDefault(r=>r.Code()==route.Code());
                if(same==null) { list.Items.Add(route);same=route; }
                list.SelectedItem=same;
            };
            Action<SavedRoute> save=route=> { route.Store(dir);select(route);status.Text=S("LxStored"); };
            var saveButton=WpfFieldTheme.Button(S("LxSave"),()=> {
                try { save(new SavedRoute(name.Text,map,kind.SelectedIndex==1,squad.Stops)); }
                catch(InvalidDataException) { status.Text=S("LxInvalidDraft"); }
                catch(Exception error) { Program.LogException("SaveRoute",error);status.Text=S("LocalSaveFailed"); }
            });saveButton.Tag="lx-save";panel.Children.Add(saveButton);
            panel.Children.Add(list);panel.Children.Add(preview);
            var actions=new WrapPanel();panel.Children.Add(actions);
            var start=WpfFieldTheme.Button(S("SquadStartRoute"),()=> { StartSavedRoute((SavedRoute)list.SelectedItem);window.Close(); });
            start.Tag="lx-start";actions.Children.Add(start);
            var export=WpfFieldTheme.Button(S("LxExport"),()=> {
                var route=(SavedRoute)list.SelectedItem;code.Text=route.Code();code.Focus();code.SelectAll();
                try { Clipboard.SetText(code.Text);status.Text=S("LxCopied"); }
                catch(System.Runtime.InteropServices.ExternalException) { status.Text=S("LxCopyManually"); }
            });export.Tag="lx-export";actions.Children.Add(export);
            start.IsEnabled=export.IsEnabled=false;
            list.SelectionChanged+=(s,e)=> {
                var route=list.SelectedItem as SavedRoute;
                start.IsEnabled=export.IsEnabled=route!=null;code.Clear();
                preview.Text=route==null?"":string.Join(" → ",route.Stops.Select((m,i)=>SavedStopRole(i,route)+" · "+m.Name))+(route.Loop?" → "+S("LxStart"):"");
            };
            panel.Children.Add(WpfFieldTheme.Text(S("LxCode"),13));panel.Children.Add(code);
            var import=WpfFieldTheme.Button(S("LxImport"),()=> {
                SavedRoute route;
                try { route=SavedRoute.Decode(code.Text,map); }
                catch(Exception) { status.Text=S("LxInvalidCode");return; }
                try { save(route); }catch(Exception error) { Program.LogException("ImportRoute",error);status.Text=S("LocalSaveFailed"); }
            });import.Tag="lx-import";panel.Children.Add(import);status.Tag="lx-status";panel.Children.Add(status);
            bool loaded=false;
            panel.Loaded+=(s,e)=> {
                if(loaded)return;loaded=true;
                try {
                    if(Directory.Exists(dir))foreach(string file in Directory.EnumerateFiles(dir,"*.lx").OrderBy(f=>f,StringComparer.Ordinal)) {
                        try { if(new FileInfo(file).Length>SavedRoute.MaxCodeLength)throw new InvalidDataException();
                            list.Items.Add(SavedRoute.Decode(File.ReadAllText(file),map)); }
                        catch(Exception error) { Program.LogException("ReadRoute",error);status.Text=S("LxInvalidCode"); }
                    }
                    if(list.Items.Count>0)list.SelectedIndex=0;
                }catch(Exception error) { Program.LogException("ReadRoutes",error);status.Text=S("LxInvalidCode"); }
            };
        }
    }
}