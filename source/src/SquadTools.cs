using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DrawingPoint=System.Drawing.PointF;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        internal MapPreset WpfCapturePreset(string name) {
            return new MapPreset { Name=name,Map=mapSidecarPaths.MapId,Categories=scumMap==null?"":scumMap.GetDisabledCategoriesString(),ZoneLayers=string.Join(";",disabledZoneLayers),Zones=string.Join(";",disabledWpfZones),Grid=gridBorders,Waypoints=showCustomWaypoints,History=locationHistory,Labels=showZoneLabels,ShowZones=showZones,Poi=showScumMap&&(scumMap==null||scumMap.MasterEnabled),Rotate=wpfRotateMap,Cluster=wpfPoiClustering,Opacity=mapOpacity,Zoom=zoom,TacticalOpacity=WpfTacticalOpacityValue,TacticalZoom=fullMapZoom };
        }
        internal bool WpfApplyPreset(MapPreset p) {
            if(p.Map!=mapSidecarPaths.MapId)return false;
            gridBorders=p.Grid;showCustomWaypoints=p.Waypoints;locationHistory=p.History;showZoneLabels=p.Labels;showZones=p.ShowZones;showScumMap=p.Poi;
            if(scumMap!=null) { scumMap.MasterEnabled=p.Poi;scumMap.ApplyDisabledCategoriesString(p.Categories); }
            disabledZoneLayers.Clear();foreach(string s in p.ZoneLayers.Split(';').Where(s=>s.Length>0))disabledZoneLayers.Add(s);
            disabledWpfZones.Clear();foreach(string s in p.Zones.Split(';').Where(s=>s.Length>0))disabledWpfZones.Add(s);
            fullMapOpacity=p.TacticalOpacity;fullMapZoom=Math.Max(1,Math.Min(maxZoom,p.TacticalZoom));wpfRotateMap=p.Rotate;wpfPoiClustering=p.Cluster;mapOpacity=p.Opacity;zoom=targetZoom=Math.Max(1,Math.Min(maxZoom,p.Zoom));SettingsChanged();SaveSettings();WpfRebuildSettings();return true;
        }
    }
    internal sealed partial class WpfFieldConsoleRuntime {
        readonly SquadState squad=new SquadState();
        readonly Dictionary<string,PrivatePositions.Peer> knownPlayers=new Dictionary<string,PrivatePositions.Peer>(StringComparer.Ordinal);
        readonly Dictionary<string,DateTime> playerSeen=new Dictionary<string,DateTime>(StringComparer.Ordinal);
        readonly Dictionary<string,TextBlock> playerStatusLabels=new Dictionary<string,TextBlock>(StringComparer.Ordinal);
        string followingPeer;MapZone squadTarget;DateTime nextSquadRoute;
        SharedMapMarker[] activeStops=new SharedMapMarker[0];int activeStop;
        static string S(string key) { return Localization.Get(key); }
        void StopSquadNavigation() { activeSavedRoute=null;returningToStart=false;followingPeer=null;activeStops=new SharedMapMarker[0];squadTarget=null;squadRoutePlanner.Clear();activeRouteSignature=null;engine.WpfClear(); }
        static TextBlock SquadHelp(string key) { var text=WpfFieldTheme.Text(S(key),13,WpfFieldTheme.Muted);text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.None;text.Margin=new Thickness(0,10,0,10);return text; }
        bool CanSendPing() {
            var sessions=(relayGroups==null?new DirectPositions[0]:relayGroups.Sessions).Concat(privatePositions==null?new DirectPositions[0]:new[]{privatePositions});
            bool squadDialog=dialogs.Count>0&&dialogs[dialogs.Count-1].Tag as string=="squad-tools";
            return sessions.Any(s=>s.Ready)&&engine.WpfCanPing(squadDialog);
        }
        static IEnumerable<TextBlock> SquadPlayerLabels(Panel players) { return players.Children.OfType<Border>().Select(b=>b.Child).OfType<StackPanel>().SelectMany(p=>p.Children.OfType<TextBlock>()).Where(t=>t.Tag is string); }
        void SendPing(int kind,DrawingPoint? at=null) {
            if(!CanSendPing())return;var state=engine.WpfSnapshot(false);
            var p=at??MapWindow.ToMap(state.Position);string[] keys={"PingDanger","PingLoot","PingPickup"};squad.Ping(kind,S(keys[kind]),p.X,p.Y);Refresh();
        }
        string PeerStatus(PrivatePositions.Peer p) {
            DateTime seen;bool connected=playerSeen.TryGetValue(p.Id,out seen)&&(DateTime.UtcNow-seen).TotalSeconds<3;
            string age=p.Updated==DateTime.MinValue?S("PlayerNoLocation"):S("SquadUpdated")+" "+Math.Max(0,(int)(DateTime.UtcNow-p.Updated).TotalSeconds)+" s";
            return S(connected?"PlayerConnected":"SquadOffline")+" · "+(p.Position==null?S("PlayerNoLocation"):S("PlayerLiveLocation"))+" · "+age;
        }
        void UpdateSquad() {
            var now=DateTime.UtcNow;
            var current=(privatePositions==null?new PrivatePositions.Peer[0]:privatePositions.Roster()).Concat(relayGroups==null?new PrivatePositions.Peer[0]:relayGroups.Roster()).ToArray();
            foreach(var p in current) { PrivatePositions.Peer previous;if(p.Position==null)p.Updated=knownPlayers.TryGetValue(p.Id,out previous)?previous.Updated:DateTime.MinValue;knownPlayers[p.Id]=p;if(p.Online)playerSeen[p.Id]=now;else playerSeen.Remove(p.Id); }
            foreach(string id in knownPlayers.Keys.Where(id=>id.Contains(":")&&!current.Any(p=>p.Id==id)).ToArray()) { knownPlayers.Remove(id);playerSeen.Remove(id); }
            foreach(string id in playerSeen.Where(p=>now-p.Value>TimeSpan.FromMinutes(5)).Select(p=>p.Key).ToArray()) { playerSeen.Remove(id);knownPlayers.Remove(id); }
            playerRoster=knownPlayers.Values.OrderBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).Select(p=>new PrivatePositions.Peer { Id=p.Id,Name=p.Name,Updated=p.Updated,Position=current.Any(c=>c.Id==p.Id)?p.Position:null }).ToArray();
            SyncSelectedSharedRoute();
            var state=engine.WpfSnapshot(false);
            // A manual destination or Clear takes precedence over squad navigation.
            if(squadTarget!=null&&!object.ReferenceEquals(squadTarget,state.Target)||(followingPeer!=null&&squadTarget==null&&state.Target!=null)) { activeSavedRoute=null;returningToStart=false;followingPeer=null;activeStops=new SharedMapMarker[0];squadTarget=null;squadRoutePlanner.Clear();activeRouteSignature=null; }
            if(followingPeer!=null) {
                var peer=current.FirstOrDefault(p=>p.Id==followingPeer);
                if(peer==null||peer.Position==null) { bool clear=squadTarget!=null;squadTarget=null;if(clear)engine.WpfClear();return; }
                var destination=MapWindow.ToMap(peer.Position);
                if(now>=nextSquadRoute&&(squadTarget==null||DestinationSearch.Metres(squadTarget.Centroid,destination)>=25||!NavigationRouter.SameSurface(squadTarget.Centroid,squadTarget.RouteHeightCm,destination,peer.Position.Z))) { squadTarget=new MapZone { Name=S("SquadFollow")+": "+peer.Name,Points=new[]{destination},RouteHeightCm=peer.Position.Z };engine.WpfSquadTarget(squadTarget);nextSquadRoute=now.AddSeconds(2); }
            }
            else if(activeStops.Length>0&&activeStop<activeStops.Length) {
                int previousStop=activeStop;
                if(state.Position!=null&&state.Tracking&&!state.TrackingPaused&&PrivatePositions.Fresh(state.Updated,now)&&DestinationSearch.Metres(MapWindow.ToMap(state.Position),PointOf(activeStops[activeStop]))<ArrivalRadius()&&NavigationRouter.SameSurface(MapWindow.ToMap(state.Position),state.Position.Z,PointOf(activeStops[activeStop]),null)) {
                    activeStop++;returningToStart=false;
                    if(activeStop==activeStops.Length&&activeSavedRoute!=null&&activeSavedRoute.Loop) {
                        activeStop=0;returningToStart=true;
                    }
                }
                if(activeStop>=activeStops.Length) { StopSquadNavigation(); }
                else if(previousStop!=activeStop||squadTarget==null||!object.ReferenceEquals(squadTarget,state.Target))SetSquadStop();
                else { var stop=activeStops[activeStop];if(squadTarget.Centroid.X!=(float)stop.X||squadTarget.Centroid.Y!=(float)stop.Y)SetSquadStop(); }
            }
        }
        void SetSquadStop() { var m=activeStops[activeStop];squadTarget=new MapZone { Name=(activeSavedRoute==null?"":activeSavedRoute.Name+" · "+SavedStopRole(activeStop,activeSavedRoute)+" ")+(activeStop+1)+"/"+activeStops.Length+" · "+m.Name,Points=new[]{new DrawingPoint((float)m.X,(float)m.Y)} };engine.WpfSquadTarget(squadTarget); }
        void StartSquadRoute(SharedMapMarker[] stops) {
            if(stops.Length==0)return;
            if(activeSavedRoute!=null&&stops[0].Id==activeSavedRoute.Stops[0].Id) { StartSavedRoute(activeSavedRoute);return; }
            activeSavedRoute=null;returningToStart=false;BeginRoute(stops);
        }
        void BeginRoute(SharedMapMarker[] stops) {followingPeer=null;activeStops=stops.Select(m=>m.Copy()).ToArray();activeStop=0;SetSquadStop();Refresh(); }
        void NavigateDestination(MapZone destination) { StopSquadNavigation();engine.WpfTarget(destination);filterIdentity=null;RefreshInteraction(); }
        void AddSquadDestination(MapZone destination) { if(destination!=null&&squad.AddStop(destination.DisplayName,destination.Centroid.X,destination.Centroid.Y))OwnRouteEdited(); }
        void AddSquadStop(DrawingPoint at) { AddSquadDestination(engine.WpfClickedTarget(at,.003f)); }
        void ShowSquadTools() { Modal(BuildSquadTools(1)); }
        void ShowRoutes() { Modal(BuildSquadTools(0)); }
        void ShowMapPresets() { Modal(BuildSquadTools(2)); }
        // Routes, squad and map presets share one window with three tabs. Each page is a stack of themed
        // sections: what is active now first, then the choices, then the lists.
        Window BuildSquadTools(int initial) {
            var window=Window(S("SquadTools"),680,780);window.Tag="squad-tools";window.WindowStyle=WindowStyle.SingleBorderWindow;
            var dialog=new WpfKit.Dialog(window,S("SquadTools"));dialog.AddClose(S("Done"));
            var pages=new[]{new StackPanel(),new StackPanel(),new StackPanel()};int selected=-1;
            string[] tabKeys={"RouteList","SquadTools","SquadPresets"};string[] subtitles={"SquadRouteHelp","SquadToolsHelp","PresetHelp"};
            WrapPanel tabs=null;
            Action<int> select=index=> {
                selected=index;window.Title=S(tabKeys[index]);dialog.SetTitle(window.Title,S(subtitles[index]));
                dialog.Body.Children.Clear();dialog.Body.Children.Add(pages[index]);dialog.Scroll.ScrollToTop();WpfKit.MarkTabs(tabs,index);
            };
            tabs=WpfKit.ButtonTabs(tabKeys.Select(S).ToArray(),new[]{"route","users","star"},select,"squad-tab-");dialog.AddTabBar(tabs);
            window.PreviewKeyDown+=(s,e)=> { if(e.Key==System.Windows.Input.Key.Tab&&(System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0) { select((selected+((System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Shift)!=0?2:1))%3);((Button)tabs.Children[selected]).Focus();e.Handled=true; } };

            // Routes: destination, route type, own route, squad routes, saved routes.
            var routePage=pages[0];Action redraw=null;
            var targetName=WpfKit.Strong("",16);var targetMeta=WpfKit.Help("",13);targetMeta.Margin=new Thickness(0,3,0,0);
            var targetText=new StackPanel { VerticalAlignment=VerticalAlignment.Center };targetText.Children.Add(targetName);targetText.Children.Add(targetMeta);
            var addTarget=WpfKit.Secondary(S("SquadAddStop"),()=> { AddSquadDestination(engine.WpfSnapshot(true).Target);redraw(); },"plus");addTarget.Tag="route-add-target";addTarget.ToolTip=S("MapGpsSavedHelp");
            var stopNavigation=WpfKit.Quiet(S("SquadStopNav"),()=> { StopSquadNavigation();filterIdentity=null;Refresh();redraw(); },"circle-stop");stopNavigation.Tag="route-stop-navigation";
            var targetRow=new DockPanel();var targetIcon=WpfKit.IconTile("navigation",null,40);targetIcon.Margin=new Thickness(0,0,14,0);DockPanel.SetDock(targetIcon,Dock.Left);targetRow.Children.Add(targetIcon);targetRow.Children.Add(targetText);
            var targetCard=new StackPanel();targetCard.Children.Add(targetRow);targetCard.Children.Add(WpfKit.Actions(addTarget,stopNavigation));((FrameworkElement)targetCard.Children[1]).Margin=new Thickness(54,12,0,-8);
            routePage.Children.Add(WpfKit.Section(S("RouteTargetTitle"),WpfKit.Card(targetCard)));
            var modes=new WrapPanel();var modeButtons=new List<Button>();
            foreach(RoadRouteMode mode in Enum.GetValues(typeof(RoadRouteMode))) {
                var choice=mode;string icon=mode==RoadRouteMode.Quickest?"zap":mode==RoadRouteMode.Shortest?"milestone":mode==RoadRouteMode.PavedOnly?"car":"user-round";
                var button=WpfKit.Chip(S("RouteMode"+mode),()=> { engine.WpfSetRouteMode(choice);ownRouteSignature=null;activeRouteSignature=null;UpdateRoutePreviews();filterIdentity=null;Refresh();redraw(); },icon,"route-mode-"+mode);
                modeButtons.Add(button);modes.Children.Add(button);
            }
            routePage.Children.Add(WpfKit.Section(S("RouteModeTitle"),modes,S("RouteModeHelp")));
            var routes=new StackPanel();var kinds=new StackPanel { Orientation=Orientation.Horizontal };
            var soloKind=WpfKit.Chip(S("SoloRoute"),()=> { ChangeOwnRouteKind(false);redraw(); },"user-round","route-kind-solo");var squadKind=WpfKit.Chip(S("SquadRoute"),()=> { ChangeOwnRouteKind(true);redraw(); },"users","route-kind-squad");
            foreach(var chip in new[]{soloKind,squadKind}) { chip.Margin=new Thickness(6,0,0,0);chip.MinHeight=28;chip.Padding=new Thickness(10,3,10,4);kinds.Children.Add(chip); }
            var ownSection=(StackPanel)WpfKit.Section(S("SoloRoute"),routes,null,kinds);routePage.Children.Add(ownSection);
            var remoteRoutes=new StackPanel();var remoteSection=WpfKit.Section(S("RouteFromSquad"),remoteRoutes);routePage.Children.Add(remoteSection);
            var help=WpfKit.Note(S("RouteAutoHelp"));help.Margin=new Thickness(0,14,0,0);routePage.Children.Add(help);
            AddSavedRoutes(routePage,window);
            string routeIdentity=null;
            redraw=()=> {
                foreach(var button in modeButtons)WpfKit.MarkChip(button,(string)button.Tag=="route-mode-"+engine.WpfRouteMode);
                var state=engine.WpfSnapshot(true);targetName.Text=state.Target==null?S("KeyNone"):state.Target.DisplayName;targetMeta.Text=state.Target==null?S("RouteTargetHelp"):state.RouteSummary??"";
                addTarget.IsEnabled=state.Target!=null&&activeStops.Length==0&&followingPeer==null&&squad.Stops.Length<SquadState.MaximumStops;
                stopNavigation.IsEnabled=state.Target!=null||followingPeer!=null||activeStops.Length>0;
                var local=squad.Stops;var remote=SquadState.Routes(ReceivedSquadMarkers());
                var plan=ownRoutePlanner.Plan;string identity=StopSignature(local)+"|"+squad.Shared+"|"+ownRoutePlanner.Busy+"|"+(plan==null?0:plan.Revision)+"|"+string.Join("|",remote.Select(r=>SquadState.RouteKey(r[0])+":"+r[0].Owner+":"+StopSignature(r)));
                if(identity==routeIdentity)return;routeIdentity=identity;routes.Children.Clear();remoteRoutes.Children.Clear();
                WpfKit.MarkChip(soloKind,!squad.Shared);WpfKit.MarkChip(squadKind,squad.Shared);
                var heading=ownSection.Children.OfType<DockPanel>().FirstOrDefault();
                if(heading!=null)foreach(var label in heading.Children.OfType<TextBlock>())label.Text=WpfFieldTheme.Current.Caps(S(squad.Shared?"SquadRoute":"SoloRoute")+" · "+local.Length+" / 16");
                string summary=state.OwnRouteSummary??(ownRoutePlanner.Busy?S("FieldCalculating"):plan==null?"":engine.WpfTravelSummary(plan.Distance,plan.MissingRoads==0,false,null,plan.UnplannedLegs,plan.AllFailed));
                if(!string.IsNullOrEmpty(summary)) { var line=WpfKit.Mono(summary,13,WpfFieldTheme.Muted);line.TextWrapping=TextWrapping.Wrap;line.TextTrimming=TextTrimming.None;line.Margin=new Thickness(0,0,0,8);routes.Children.Add(line); }
                if(local.Length==0)routes.Children.Add(WpfKit.Empty(S("RouteEmpty"),"map-pin"));
                else routes.Children.Add(WpfKit.List(local.Select(m=> {
                    var marker=m;var remove=WpfKit.Quiet(S("SquadDelete"),()=> { RemoveOwnRouteStop(marker.Id);redraw(); },"x");remove.Tag="route-remove:"+m.Id;remove.MinHeight=30;remove.Padding=new Thickness(8,3,8,3);
                    return (UIElement)WpfKit.Row(WpfKit.Number(SquadState.StopIndex(m)+1),m.Name,null,remove);
                })));
                var own=WpfKit.Primary(S("SquadStartRoute"),()=> { StartSquadRoute(local);window.Close(); },"play");own.IsEnabled=local.Length>0;
                var undo=WpfKit.Secondary(S("SquadUndoStop"),()=> { UndoOwnRouteStop();redraw(); },"undo-2");undo.IsEnabled=local.Length>0;
                var clear=WpfKit.Quiet(S("WpfClearRoute"),()=> { ClearOwnRoute();redraw(); },"trash-2");clear.IsEnabled=local.Length>0;
                routes.Children.Add(WpfKit.Actions(own,undo,clear));
                remoteSection.Visibility=remote.Count()==0?Visibility.Collapsed:Visibility.Visible;
                if(remote.Any())remoteRoutes.Children.Add(WpfKit.List(remote.Select(route=> {
                    var copy=route;var start=WpfKit.Secondary(S("SquadStartRoute"),()=> { StartSquadRoute(copy);window.Close(); },"play");start.MinHeight=32;start.Padding=new Thickness(12,4,14,4);
                    var dot=new System.Windows.Shapes.Ellipse { Width=10,Height=10,Fill=WpfMapSurface.MemberInk(route[0].OwnerId) };
                    return (UIElement)WpfKit.Row(dot,route[0].Owner,route.Length+" / 16",start);
                })));
            };redraw();

            // Squad: connection, pings, players.
            var squadPage=pages[1];
            var hero=new DockPanel();var heroIcon=WpfKit.IconTile("users",null,44);heroIcon.Margin=new Thickness(0,0,16,0);heroIcon.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(heroIcon,Dock.Left);hero.Children.Add(heroIcon);
            var heroText=new StackPanel();heroText.Children.Add(WpfKit.Strong(S("PrivatePositions"),16));var heroHelp=WpfKit.Help(S("SquadConnectHelp"),13);heroHelp.Margin=new Thickness(0,4,0,12);heroText.Children.Add(heroHelp);
            heroText.Children.Add(WpfKit.Primary(S("SetupOpenSharing"),ShowPrivatePositions,"share-2"));hero.Children.Add(heroText);squadPage.Children.Add(WpfKit.Card(hero,new Thickness(20,18,20,18)));
            var pingRow=new UniformGrid { Columns=3 };var pingButtons=new List<Button>();string[] keys={"PingDanger","PingLoot","PingPickup"};string[] pingIcons={"triangle-alert","package","hand"};
            for(int i=0;i<3;i++) {
                int k=i;var ping=new Button { Content=S(keys[i]),Tag="squad-ping-"+i,MinHeight=76,Margin=new Thickness(i==0?0:4,0,i==2?0:4,0),Padding=new Thickness(8,10,8,10),FontSize=13,BorderThickness=new Thickness(1) };
                ping.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");ping.Background=i==0?WpfFieldTheme.Current.Alpha(WpfFieldTheme.Current.Danger,0x1C):WpfFieldTheme.Raised;ping.BorderBrush=i==0?WpfFieldTheme.Current.Alpha(WpfFieldTheme.Current.Danger,0x59):WpfFieldTheme.Line;ping.Foreground=i==0?WpfFieldTheme.Warn:WpfFieldTheme.Ink;
                if(WpfFieldTheme.Current.Uppercase) { ping.FontFamily=WpfFieldTheme.DisplayFont;ping.FontWeight=FontWeights.Bold;ping.FontSize=15; }
                WpfFieldTheme.IconLabel(ping,pingIcons[i],true,22);ping.Click+=(s,e)=> { if(ping.IsEnabled)SendPing(k); };pingButtons.Add(ping);pingRow.Children.Add(ping);
            }
            squadPage.Children.Add(WpfKit.Section(S("SquadPingTitle"),pingRow,S("SquadPingHelp")));
            var players=new StackPanel();squadPage.Children.Add(WpfKit.Section(S("SharedPlayers"),players));string playerIdentity=null;
            AddPlayers(players,false,false);if(players.Children.Count==0)players.Children.Add(WpfKit.Empty(S("PlayerWaiting"),"users"));
            Action update=()=> {
                redraw();bool ready=CanSendPing();foreach(var ping in pingButtons)ping.IsEnabled=ready;
                string identity=string.Join(";",playerRoster.Select(p=>p.Id+":"+p.Name+":"+(p.Position!=null)+":"+PlayerVisibility(p.Id)))+"|"+followingPeer;
                if(identity!=playerIdentity) { playerIdentity=identity;players.Children.Clear();AddPlayers(players,false,false);if(players.Children.Count==0)players.Children.Add(WpfKit.Empty(S("PlayerWaiting"),"users")); }
                foreach(var label in SquadPlayerLabels(players)) { var peer=playerRoster.FirstOrDefault(p=>p.Id==(string)label.Tag);if(peer!=null)label.Text=PeerStatus(peer); }
            };
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };timer.Tick+=(s,e)=>update();window.Loaded+=(s,e)=> { update();timer.Start(); };window.Closed+=(s,e)=>timer.Stop();

            // Map presets: save the current map, then the saved list.
            var presetPage=pages[2];
            string file=Path.Combine(engine.WpfDataFolder,"map-presets.dat");var presets=new List<MapPreset>();bool presetReadable=true;
            var presetStatus=WpfFieldTheme.Text("",13,WpfFieldTheme.MapAmber);presetStatus.TextWrapping=TextWrapping.Wrap;presetStatus.TextTrimming=TextTrimming.None;
            try { presets.AddRange(MapPresetFile.Load(file)); }catch(Exception e) { presetReadable=false;Program.LogException("MapPresets",e);presetStatus.Text=S("PresetReadFailed"); }
            var presetName=WpfKit.Input(S("SquadLooting"),null,64);System.Windows.Automation.AutomationProperties.SetName(presetName,S("SquadPresets"));
            var names=new WrapPanel { Margin=new Thickness(0,8,0,0) };foreach(string key in new[]{"SquadLooting","SquadDriving","SquadExploring"}) { string n=S(key);names.Children.Add(WpfKit.Chip(n,()=>presetName.Text=n)); }
            var saved=new StackPanel();Action showSaved=null;Action save=()=> { try { MapPresetFile.Save(file,presets);presetStatus.Text="";showSaved(); }catch(Exception e) { Program.LogException("MapPresetsSave",e);presetStatus.Text=S("LocalSaveFailed");throw; } };
            showSaved=()=> {
                saved.Children.Clear();
                if(presets.Count==0) { saved.Children.Add(WpfKit.Empty(S("PresetEmpty"),"star"));return; }
                saved.Children.Add(WpfKit.List(presets.ToArray().Select(p=> {
                    var preset=p;var apply=WpfKit.Secondary(S("PresetApply"),()=> { if(engine.WpfApplyPreset(preset)) { filterIdentity=null;Refresh(); } },"check");apply.MinHeight=32;apply.Padding=new Thickness(12,4,14,4);
                    var remove=WpfKit.Quiet(S("SquadDelete"),()=> { int index=presets.IndexOf(preset);presets.RemoveAt(index);try { save(); }catch { presets.Insert(index,preset); } },"trash-2");remove.MinHeight=32;remove.Padding=new Thickness(8,4,8,4);
                    return (UIElement)WpfKit.Row(WpfKit.IconTile("star",null,30),preset.Name,null,apply,remove);
                })));
            };
            var savePreset=WpfKit.Primary(S("SquadSavePreset"),()=> { if(!presetReadable)return;string name=presetName.Text.Trim();if(name.Length==0)return;var previous=presets.ToArray();var p=engine.WpfCapturePreset(name);presets.RemoveAll(v=>v.Name==name&&v.Map==p.Map);if(presets.Count>=24) { presets.Clear();presets.AddRange(previous);return; }presets.Add(p);try { save(); }catch { presets.Clear();presets.AddRange(previous); } },"save");savePreset.IsEnabled=presetReadable;
            var form=new StackPanel();form.Children.Add(WpfKit.Field(S("PresetName"),presetName));((FrameworkElement)form.Children[0]).Margin=new Thickness(0,0,0,0);form.Children.Add(names);form.Children.Add(presetStatus);
            var formActions=WpfKit.Actions(savePreset);formActions.Margin=new Thickness(0,10,0,0);form.Children.Add(formActions);
            if(!presetReadable) {
                Button recover=null;recover=WpfKit.Danger(S("PresetRecover"),()=> {
                    if(WpfKit.Ask(window,S("PresetReadFailed"),S("PresetRecover"),MessageBoxButton.OKCancel,MessageBoxImage.Warning,MessageBoxResult.Cancel)!=MessageBoxResult.OK)return;
                    try { string backup=MapPresetFile.Recover(file);presetReadable=true;savePreset.IsEnabled=true;recover.IsEnabled=false;presetStatus.Text=S("PresetRecover")+": "+backup; }
                    catch(Exception e) { Program.LogException("MapPresetsRecover",e);presetStatus.Text=S("LocalSaveFailed"); }
                },"refresh-cw");recover.Tag="preset-recover";formActions.Children.Add(recover);recover.Margin=new Thickness(0,0,8,8);
            }
            presetPage.Children.Add(WpfKit.Section(S("PresetSaveTitle"),WpfKit.Card(form)));
            presetPage.Children.Add(WpfKit.Section(S("PresetSavedTitle"),saved));showSaved();
            select(Math.Max(0,Math.Min(2,initial)));return window;
        }
    }
}
