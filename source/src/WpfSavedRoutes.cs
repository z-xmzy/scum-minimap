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

        // Saved routes on this map: save the current stops under a name, start or share a saved route,
        // and import a shared code.
        void AddSavedRoutes(Panel parent,Window window) {
            var panel=new StackPanel();
            string map=engine.WpfRouteMapId,dir=Path.Combine(engine.WpfDataFolder,"maps",map,"routes");
            var status=WpfFieldTheme.Text("",13,WpfFieldTheme.MapAmber);status.TextWrapping=TextWrapping.Wrap;status.TextTrimming=TextTrimming.None;status.Tag="lx-status";
            var routes=new List<SavedRoute>();var list=new StackPanel();
            var code=WpfKit.Input(null,"lx-code");code.TextWrapping=TextWrapping.NoWrap;code.HorizontalScrollBarVisibility=ScrollBarVisibility.Auto;code.FontFamily=WpfFieldTheme.MonoFont;
            Action draw=null;
            Action<SavedRoute> add=route=> { if(!routes.Any(r=>r.Code()==route.Code()))routes.Add(route);draw(); };
            Action<SavedRoute> save=route=> { route.Store(dir);add(route);status.Text=S("LxStored"); };
            draw=()=> {
                list.Children.Clear();
                if(routes.Count==0) { list.Children.Add(WpfKit.Empty(S("LxEmpty"),"route"));return; }
                list.Children.Add(WpfKit.List(routes.Select(r=> {
                    var route=r;
                    var start=WpfKit.Secondary(S("SquadStartRoute"),()=> { StartSavedRoute(route);window.Close(); },"play");start.Tag="lx-start";start.MinHeight=32;start.Padding=new Thickness(12,4,14,4);
                    var export=WpfKit.Quiet(S("LxExport"),()=> {
                        code.Text=route.Code();code.Focus();code.SelectAll();
                        try { Clipboard.SetText(code.Text);status.Text=S("LxCopied"); }
                        catch(System.Runtime.InteropServices.ExternalException) { status.Text=S("LxCopyManually"); }
                    },"copy");export.Tag="lx-export";export.MinHeight=32;export.Padding=new Thickness(8,4,8,4);
                    string stops=string.Join(" → ",route.Stops.Select((m,i)=>SavedStopRole(i,route)+" · "+m.Name))+(route.Loop?" → "+S("LxStart"):"");
                    return (UIElement)WpfKit.Row(WpfKit.IconTile(route.Loop?"refresh-cw":"route",null,30),route.Name+"  ·  "+S(route.Loop?"LxLoop":"LxNormal"),stops,start,export);
                })));
            };
            // Save the current stops.
            var name=WpfKit.Input(null,"lx-name",64);bool loop=false;
            Button normalKind=null,loopKind=null;
            Action markKind=()=> { WpfKit.MarkChip(normalKind,!loop);WpfKit.MarkChip(loopKind,loop); };
            normalKind=WpfKit.Chip(S("LxNormal"),()=> { loop=false;markKind(); },"arrow-right","lx-kind-0");loopKind=WpfKit.Chip(S("LxLoop"),()=> { loop=true;markKind(); },"refresh-cw","lx-kind-1");markKind();
            var saveButton=WpfKit.Primary(S("LxSave"),()=> {
                try { save(new SavedRoute(name.Text,map,loop,squad.Stops)); }
                catch(InvalidDataException) { status.Text=S("LxInvalidDraft"); }
                catch(Exception error) { Program.LogException("SaveRoute",error);status.Text=S("LocalSaveFailed"); }
            },"save");saveButton.Tag="lx-save";
            var form=new StackPanel();form.Children.Add(WpfKit.Field(S("LxName"),name));var kinds=new WrapPanel();kinds.Children.Add(normalKind);kinds.Children.Add(loopKind);form.Children.Add(kinds);
            var formActions=WpfKit.Actions(saveButton);formActions.Margin=new Thickness(0,6,0,0);form.Children.Add(formActions);
            panel.Children.Add(WpfKit.Card(form));panel.Children.Add(list);panel.Children.Add(status);
            // Share codes.
            var import=WpfKit.Secondary(S("LxImport"),()=> {
                SavedRoute route;
                try { route=SavedRoute.Decode(code.Text,map); }
                catch(Exception) { status.Text=S("LxInvalidCode");return; }
                try { save(route); }catch(Exception error) { Program.LogException("ImportRoute",error);status.Text=S("LocalSaveFailed"); }
            },"download");import.Tag="lx-import";
            var share=new StackPanel();share.Children.Add(WpfKit.Field(S("LxCode"),code));share.Children.Add(WpfKit.Actions(import));
            panel.Children.Add(new Expander { Header=WpfKit.Strong(S("LxCode"),14),Content=share,Margin=new Thickness(0,8,0,0) });
            parent.Children.Add(WpfKit.Section(S("LxLibrary"),panel,S("LxHelp")));
            bool loaded=false;
            panel.Loaded+=(s,e)=> {
                if(loaded)return;loaded=true;
                try {
                    if(Directory.Exists(dir))foreach(string file in Directory.EnumerateFiles(dir,"*.lx").OrderBy(f=>f,StringComparer.Ordinal)) {
                        try { if(new FileInfo(file).Length>SavedRoute.MaxCodeLength)throw new InvalidDataException();
                            routes.Add(SavedRoute.Decode(File.ReadAllText(file),map)); }
                        catch(Exception error) { Program.LogException("ReadRoute",error);status.Text=S("LxInvalidCode"); }
                    }
                }catch(Exception error) { Program.LogException("ReadRoutes",error);status.Text=S("LxInvalidCode"); }
                draw();
            };
            draw();
        }
    }
}