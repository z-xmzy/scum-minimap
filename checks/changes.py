from pathlib import Path
p=Path('source/src/WpfFieldConsole.cs')
s=Path('checks/WpfFieldConsole.original').read_text('utf-8')
def change(a,b):
 global s
 assert s.count(a)==1,a
 s=s.replace(a,b)
change('CameraScene lastPresentedCameraScene;', '''double staticSide,staticWidth,staticHeight;
        DrawingPoint staticCentre;
        string staticOverlayKey;''')
s=s.replace('lastPresentedCameraScene=null;', 'staticOverlayKey=null;')
change('if(cameraScene!=null&&cameraScene.ContentKey!=SceneContentKey())cameraScene=null;', '''string overlayKey=OverlayContentKey();
            if(cameraScene!=null&&cameraScene.OverlayKey!=overlayKey)cameraScene=null;''')
change('CameraScene previewScene=cameraScene??(ZoomGestureActive?lastPresentedCameraScene:null);','CameraScene previewScene=cameraScene;')
change('presentedCameraScene=previewScene;lastPresentedCameraScene=previewScene;','presentedCameraScene=previewScene;')
a='            string camera=CameraLayerKey(),store='
change(a,'''            // The native game map and the zoomed map have different sizes and opacity.
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
'''+a)
change('string staticKey=terrainKey+', '''staticWidth=ActualWidth;staticHeight=ActualHeight;staticSide=MapSide(State,staticWidth,staticHeight,false);
            staticCentre=CameraCentre(State,staticWidth,staticHeight,false);staticOverlayKey=overlayKey;
            string staticKey=terrainKey+''')
change('void DrawMapPass(DrawingContext dc,MapPass pass) {','void DrawMapPass(DrawingContext dc,MapPass pass,bool overviewOnly=false) {')
change('if(Terrain!=null)Terrain.DrawRotated','if(Terrain!=null&&!overviewOnly)Terrain.DrawRotated')
change('string SceneContentKey() {','''string SceneContentKey() { return OverlayContentKey()+"|"+(State==null?0:State.Opacity)+"|"+TerrainRevision; }
        string OverlayContentKey() {''')
change('State.Opacity,State.Shape,State.Grid,State.GridLabels,State.GridOpacity,State.LabelSize,TerrainRevision,Identity(Terrain)', 'State.Store!=null&&State.Store.MasterEnabled,State.Shape,State.Grid,State.GridLabels,State.GridOpacity,State.LabelSize,Identity(Terrain)')
change('publicNOTHERE','') if False else None
change('internal string ContentKey;', 'internal string ContentKey,OverlayKey;')
change('Opacity=State.Opacity,ContentKey=SceneContentKey()', 'Opacity=State.Opacity,ContentKey=SceneContentKey(),OverlayKey=OverlayContentKey()')
change('idleCameraScene.ContentKey==SceneContentKey()', 'idleCameraScene.OverlayKey==OverlayContentKey()')
change('internal event Action Changed;', '''internal event Action Changed;
        DispatcherTimer tileRefresh;
        void ScheduleTileRefresh() {
            if(tileRefresh==null) {
                tileRefresh=new DispatcherTimer(DispatcherPriority.Background,dispatcher) { Interval=TimeSpan.FromMilliseconds(60) };
                tileRefresh.Tick+=(s,e)=> { tileRefresh.Stop();if(!disposed&&Changed!=null)Changed(); };
            }
            if(!tileRefresh.IsEnabled)tileRefresh.Start();
        }''')
change('CacheTile(tile);var changed=Changed;if(changed!=null)changed();','CacheTile(tile);ScheduleTileRefresh();')
change('}Changed=null;cache.Clear();recent.Clear();','}if(tileRefresh!=null)tileRefresh.Stop();Changed=null;cache.Clear();recent.Clear();')
change('tacticalMap.TerrainRevision++;tacticalMap.RequestRedraw();hudMap.InvalidateVisual();', '''tacticalMap.TerrainRevision++;if(tactical.IsVisible&&tacticalMap.State!=null&&tacticalMap.State.Opacity>0)tacticalMap.RequestRedraw();if(hud.IsVisible)hudMap.InvalidateVisual();''')
change('tactical.WindowState=WindowState.Normal;tactical.Show();FitTacticalToMonitor();','FitTacticalToMonitor();tactical.Show();lastRefresh=-1000;')
p.write_text(s,'utf-8-sig')
for path in ['Build-LocalCandidate.ps1','source/src/MiniMap.cs','tools/package-ui.py','package/SCUM-MiniMap/使用说明.md']:
 p=Path(path);b=p.read_bytes();p.write_bytes(b.replace(b'2.4.7',b'2.4.8'))
