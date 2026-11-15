from pathlib import Path
def read(path):
 p=Path(path)
 return p,p.read_bytes().decode('utf-8-sig').replace('\r\n','\n')
def write(p,s):
 original=p.read_bytes()
 if b'\r\n' in original:s=s.replace('\n','\r\n')
 p.write_bytes((b'\xef\xbb\xbf' if original.startswith(b'\xef\xbb\xbf') else b'')+s.encode())
p,s=read('source/src/SquadTools.cs')
s=s.replace('followingPeer=null;activeStops=new SharedMapMarker[0];','activeSavedRoute=null;returningToStart=false;followingPeer=null;activeStops=new SharedMapMarker[0];')
s=s.replace('if(state.Position!=null&&state.Tracking', 'if((activeSavedRoute==null||state.Updated!=lastSavedArrival)&&state.Position!=null&&state.Tracking')
s=s.replace('new DrawingPoint((float)activeStops[activeStop].X,(float)activeStops[activeStop].Y))<40)activeStop++;', '''PointOf(activeStops[activeStop]))<ArrivalRadius()) {
                    if(activeSavedRoute!=null)lastSavedArrival=state.Updated;
                    activeStop++;returningToStart=false;
                    if(activeStop==activeStops.Length&&activeSavedRoute!=null&&activeSavedRoute.Loop) {
                        activeStop=0;returningToStart=true;
                    }
                }''')
s=s.replace('engine.WpfClear();activeStops=new SharedMapMarker[0];squadTarget=null;', 'StopSquadNavigation();')
s=s.replace('Name=(activeStop+1)+"/"+activeStops.Length+" · "+m.Name', 'Name=(activeSavedRoute==null?"":activeSavedRoute.Name+" · "+SavedStopRole(activeStop,activeSavedRoute)+" ")+(activeStop+1)+"/"+activeStops.Length+" · "+m.Name')
s=s.replace('void StartSquadRoute(SharedMapMarker[] stops) { if(stops.Length==0)return;', '''void StartSquadRoute(SharedMapMarker[] stops) {
            if(stops.Length==0)return;
            if(activeSavedRoute!=null&&stops[0].Id==activeSavedRoute.Stops[0].Id) { StartSavedRoute(activeSavedRoute);return; }
            activeSavedRoute=null;returningToStart=false;BeginRoute(stops);
        }
        void BeginRoute(SharedMapMarker[] stops) {''')
s=s.replace('var routes=new StackPanel();routePage.Children.Add(routes);','AddSavedRoutes(routePage,window);\n            var routes=new StackPanel();routePage.Children.Add(routes);')
write(p,s)
p,s=read('source/src/SquadRouteControls.cs')
s=s.replace('bool ownActive=activeStops.Length','bool ownActive=activeSavedRoute==null&&activeStops.Length')
s=s.replace('activeStop=ownActive?AlignedStop(next):0;', 'activeStop=ownActive?AlignedStop(next):0;activeSavedRoute=null;returningToStart=false;')
s=s.replace('return activeStop==0&&','return activeSavedRoute==null&&activeStop==0&&')
s=s.replace('activeStops.Skip(activeStop)', 'RemainingStops()')
s=s.replace('state.LocalRoute=compact.LocalRoute=squad.Stops;', '''if(activeSavedRoute!=null)state.SharedMarkers=compact.SharedMarkers=state.SharedMarkers.Concat(activeStops).ToArray();
            state.LocalRoute=compact.LocalRoute=squad.Stops;''')
write(p,s)
p,s=read('source/src/SquadRoutePlanner.cs')
s=s.replace('stops.Length>16', '(stops.Length>SquadState.MaximumStops && !(stops.Length==SquadState.MaximumStops+1 && stops[0]==stops[stops.Length-1]))')
write(p,s)
p,s=read('source/scripts/Get-MiniMapSources.ps1')
a="Join-Path (Join-Path $miniRoot 'src') 'SquadTools.cs'"
s=s.replace(a,a+"\n"+a.replace('SquadTools','SavedRoute')+"\n"+a.replace('SquadTools','WpfSavedRoutes'))
write(p,s)
p,s=read('source/src/Localization.cs')
texts=[
('Library','Saved routes','已保存路线','已儲存路線'),('Help','Right-click the map to add stops in order. First is the start. A normal route ends at the last stop. A loop returns to the start and repeats.','在地图上右键按顺序添加站点。第一个是起点。普通路线在最后一站结束，循环路线经过最后一站后回到起点，开始下一圈。','在地圖上按右鍵依序新增站點。第一站為起點。一般路線於最後一站結束，循環路線則返回起點開始下一圈。'),('Name','Route name','路线名称','路線名稱'),('Normal','Normal route','普通路线','一般路線'),('Loop','Loop route','循环路线','循環路線'),('Start','Start','起点','起點'),('End','End','终点','終點'),('Via','Via','途经点','途經點'),('Stored','Route saved','路线已保存','路線已儲存'),('Save','Save current stops','保存当前站点为路线','儲存目前站點為路線'),('InvalidDraft','Enter a name and 2–16 stops. Adjacent stops must be distinct, including the return leg.','请填写名称并添加 2 至 16 个站点。相邻站点须分开，循环路线无需在末尾重复添加起点。','請填寫名稱並新增 2 至 16 個站點。相鄰站點須分開，循環路線無需在末尾重複新增起點。'),('Export','Export route code','导出路线码','匯出路線碼'),('Import','Import route code','导入路线码','匯入路線碼'),('Copied','Route code copied','路线码已复制','路線碼已複製'),('CopyManually','Select the code and press Ctrl+C','请选中路线码并按 Ctrl+C 复制','請選取路線碼並按 Ctrl+C 複製'),('Code','LX- route code','LX- 路线码','LX- 路線碼'),('InvalidCode','Invalid route code or incompatible map. Check the complete LX- code.','路线码无效或地图不匹配，请检查是否复制了完整的 LX- 代码。','路線碼無效或地圖不符，請檢查是否複製了完整的 LX- 代碼。')]
s=s.replace('AddB2TravelText();','AddB2TravelText();\n            AddSavedRouteText();',1)
a=s[s.index('            var catalogues=',s.index('static void AddB2TravelText()')):].split('\n')[0]
fn='        static void AddSavedRouteText() {\n'+a+'\n            string[][] entries={\n'+''.join('                new[]{"Lx'+k+'","'+en+'","'+zh+'","'+tw+'"},\n' for k,en,zh,tw in texts)+'            };\n            foreach(var entry in entries)for(int i=0;i<catalogues.Length;i++)catalogues[i].Add(entry[0],i==6?entry[2]:i==14?entry[3]:entry[1]);\n        }\n'
s=s.replace('        static void AddB2TravelText()',fn+'        static void AddB2TravelText()')
write(p,s)
for path in ['Build-LocalCandidate.ps1','source/src/MiniMap.cs','tools/package-ui.py','package/SCUM-MiniMap/使用说明.md']:
 p=Path(path);p.write_bytes(p.read_bytes().replace(b'2.4.8',b'2.4.9'))
p,s=read('package/SCUM-MiniMap/使用说明.md')
s=s.replace('走线、半越野','在地图上右键添加 2 至 16 个站点，然后打开「路线列表 → 已保存路线」，输入名称，选择普通或循环并保存。第一站为起点；普通路线在最后一站结束，循环路线自动回到起点并继续下一圈。选中路线即可开始导航或导出 LX- 路线码。收到路线码后将整串粘贴到输入框并导入，核对站点后点击开始导航。路线保存在当前地图的 app/data/maps 子目录，重启后可再次选择。\n\n走线、半越野',1)
write(p,s)
