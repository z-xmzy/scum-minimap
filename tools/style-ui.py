from pathlib import Path
r=Path(__file__).resolve().parents[1]
p=r/'source/src/WpfFieldConsole.cs';s=p.read_text(encoding='utf-8-sig')
def replace(a,b):
    global s
    assert a in s,a[:120]
    s=s.replace(a,b,1)
i=s.index('    // Lucide SVG');theme=s[:i];tail=s[i:]
for old,new in {'#101519':'#151917','#151D23':'#1B211E','#232D33':'#2A322D','#2A343C':'#414D44','#E5ECF0':'#F3F1E9','#93A6B2':'#B3BEB4','#63CDBA':'#EBC782','#8DDBCB':'#B7DCC5','#3B625B':'#695D42'}.items():theme=theme.replace(old,new)
theme=theme.replace("CornerRadius=new CornerRadius(9),Padding=padding","CornerRadius=new CornerRadius(10),Padding=padding").replace("BorderBrush=Line,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(10)","BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10)")
theme=theme.replace("<Setter TargetName='body' Property='Opacity' Value='.85'/>","<Setter TargetName='body' Property='BorderBrush' Value='#B3BEB4'/><Setter TargetName='body' Property='BorderThickness' Value='1'/>").replace("<Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='body'","<Trigger Property='IsPressed' Value='True'><Setter TargetName='body' Property='Opacity' Value='.73'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='body'",1)
s=theme+tail
pos=s.index('        internal static ResourceDictionary Resources()')
s=s[:pos]+'''        // Labels stay in Button.Content so language refresh and screen readers keep the same text.
        internal static void IconLabel(Button button,string glyph,bool stacked=false) {
            var panel=new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty,stacked?Orientation.Vertical:Orientation.Horizontal);
            var icon=new FrameworkElementFactory(typeof(TextBlock));
            icon.SetValue(TextBlock.FontFamilyProperty,new FontFamily("Segoe MDL2 Assets"));
            icon.SetValue(TextBlock.FontSizeProperty,stacked?20.0:17.0);
            icon.SetValue(TextBlock.TextProperty,glyph);
            icon.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            icon.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            icon.SetValue(FrameworkElement.MarginProperty,stacked?new Thickness(0,0,0,5):new Thickness(0,0,10,0));
            var label=new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding());
            label.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
            label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            label.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            panel.AppendChild(icon);panel.AppendChild(label);button.ContentTemplate=new DataTemplate{VisualTree=panel};
            System.Windows.Automation.AutomationProperties.SetName(button,Convert.ToString(button.Content));
        }
        internal static UIElement Brand() {
            var row=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            var compass=new System.Windows.Shapes.Path { Data=Geometry.Parse("M12,1 L21,22 L12,17 L3,22 Z M12,5 L12,15"),Stroke=Amber,StrokeThickness=1.8,
                Width=19,Height=21,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center };
            row.Children.Add(compass);row.Children.Add(Text("SCUM MiniMap",16,Ink,true));return row;
        }
'''+s[pos:]
replace('var window=Window("SCUM MiniMap — Field console",1280,850)','var window=Window("SCUM MiniMap",1280,850)')
replace('var headerActions=new UniformGrid { Columns=4,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(18,0,18,0) };','var headerActions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };')
replace('var settings=WpfFieldTheme.Button(Localization.Get("Settings"),ShowSettings);settings.Tag="text:Settings";headerActions.Children.Add(settings);','''var settings=WpfFieldTheme.Button("\\uE713",ShowSettings);settings.FontFamily=new FontFamily("Segoe MDL2 Assets");settings.FontSize=17;settings.Tag="tooltip:Settings";headerActions.Children.Add(settings);''')
replace('var headerBar=new Border { Height=64,Background=WpfFieldTheme.Panel,VerticalAlignment=VerticalAlignment.Top,Child=headerActions };','''foreach(Button action in headerActions.Children) {
                action.Width=34;action.Height=34;action.MinHeight=34;action.Padding=new Thickness(0);action.Margin=new Thickness(2,0,0,0);action.Background=Brushes.Transparent;
            }
            var mast=new DockPanel();DockPanel.SetDock(headerActions,Dock.Right);mast.Children.Add(headerActions);mast.Children.Add(WpfFieldTheme.Brand());
            var headerBar=new Border { Height=64,Padding=new Thickness(18,0,12,0),BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,1),
                Background=WpfFieldTheme.Ground,VerticalAlignment=VerticalAlignment.Top,Child=mast };''')
replace('var key=WpfFieldTheme.Card(WpfFieldTheme.Text(PhysicalKeyCapture.KeyName(state.MapKey),12,WpfFieldTheme.Muted),new Thickness(8,4,8,4));','var key=WpfFieldTheme.Card(WpfFieldTheme.Text(PhysicalKeyCapture.KeyName(state.MapKey),12,WpfFieldTheme.Muted,true),new Thickness(8,2,8,2));')
replace('Localization.Get("SidebarTitle"),21,null,true','Localization.Get("SidebarTitle"),23,null,true')
a=s.index('            var search=WpfFieldTheme.Button(Localization.Get("SearchTitle"),ShowSearch);',s.index('playerStatusLabels.Clear();sidebar.Children.Clear()'));b=s.index('            bunkerViewsControl=null;',a)
s=s[:a]+'''            var search=WpfFieldTheme.Button(Localization.Get("SearchTitle"),ShowSearch,true);
            search.ToolTip=PhysicalKeyCapture.KeyName(state.Bindings.Key(AppControlAction.Search));
            search.HorizontalContentAlignment=HorizontalAlignment.Left;search.Margin=new Thickness(0,14,0,9);search.MinHeight=44;
            WpfFieldTheme.IconLabel(search,"\\uE721");sidebar.Children.Add(search);
            var shortcuts=new UniformGrid { Columns=3,Margin=new Thickness(-3,0,-3,14) };sidebar.Children.Add(shortcuts);
            var markersButton=WpfFieldTheme.Button(S("MarkerList"),()=>Modal(BuildSavedMarkers()));markersButton.Tag="sidebar-markers";
            var routesButton=WpfFieldTheme.Button(S("RouteList"),ShowRoutes);routesButton.Tag="sidebar-routes";
            var squadButton=WpfFieldTheme.Button(S("SquadTools"),ShowSquadTools);squadButton.Tag="sidebar-squad";
            WpfFieldTheme.IconLabel(markersButton,"\\uE707",true);WpfFieldTheme.IconLabel(routesButton,"\\uE81B",true);WpfFieldTheme.IconLabel(squadButton,"\\uE716",true);
            foreach(var shortcut in new[]{markersButton,routesButton,squadButton}) {
                shortcut.ToolTip=shortcut.Content;shortcut.Padding=new Thickness(3,6,3,6);shortcut.Margin=new Thickness(3,0,3,0);
                shortcut.FontSize=13;shortcut.MinHeight=59;shortcuts.Children.Add(shortcut);
            }
'''+s[b:]
replace('var layersHeading=WpfFieldTheme.Text(Localization.Get("SidebarLayers"),12,WpfFieldTheme.Muted);','var layersHeading=WpfFieldTheme.Text(Localization.Get("SidebarLayers"),15,WpfFieldTheme.Ink,true);')
replace('Background=WpfFieldTheme.Raised,CornerRadius=new CornerRadius(8),Padding=new Thickness(12,4,12,4),Child=layerBody','Background=WpfFieldTheme.Ground,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12,3,12,3),Child=layerBody')
replace('var footer=new Border { Height=36,Padding=new Thickness(22,0,22,0),Background=WpfFieldTheme.Panel','var footer=new Border { Height=36,Padding=new Thickness(22,0,22,0),BorderThickness=new Thickness(0,1,0,0),BorderBrush=WpfFieldTheme.Line,Background=WpfFieldTheme.Ground')
replace('var tools=new StackPanel();var more=', 'var tools=new StackPanel();var more=')
replace('};sidebar.Children.Add(more);','};')
replace('            AddPlayers(sidebar);','            AddPlayers(sidebar);')
# Move utility tools below the map's primary layer controls, above POI categories.
ix=s.index('            var poi',s.index('Child=layerBody')) if '            var poi' in s[s.index('Child=layerBody'):] else -1
needle='var groupActions='
start=s.index('Child=layerBody');end=s.index('if(state.Store!=null)foreach',start)
pos=s.index('            var ',s.index('layerBody.Children.Add(customRow)',start)) if False else end
# Leave all dynamic POI groups in their original order, with tools at the end.
pos=s.index('\n        }',end);s=s[:pos]+'\n            more.Margin=new Thickness(0,10,0,0);sidebar.Children.Add(more);'+s[pos:]
replace('var root=new DockPanel { Margin=new Thickness(24) };var heading=new DockPanel { Margin=new Thickness(0,0,0,18) };','var root=new DockPanel { Margin=new Thickness(26,20,26,20) };var heading=new DockPanel { Margin=new Thickness(0,0,0,16) };')
replace('workspace.Children.Add(nav);','workspace.Children.Add(new Border { Background=WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(5),Child=nav });')
replace('Header=title,Content=panel,IsExpanded=open,Margin=new Thickness(0,8,0,12),Padding=new Thickness(10)','Header=title,Content=panel,IsExpanded=open,Margin=new Thickness(0,10,0,8),Padding=new Thickness(14)')
replace('hudSector.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(hudSector,Dock.Left);locationRow.Children.Add(hudSector);','var sectorBadge=new Border { Background=WpfFieldTheme.Ground,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Padding=new Thickness(5,0,5,0),Margin=new Thickness(0,0,8,0),Child=hudSector };DockPanel.SetDock(sectorBadge,Dock.Left);locationRow.Children.Add(sectorBadge);')
p.write_text(s,encoding='utf-8-sig')
for f in ['source/CHANGELOG.md','source/src/MiniMap.cs','Build-LocalCandidate.ps1']:
 p=r/f;s=p.read_text(encoding='utf-8-sig')
 if 'CHANGELOG' in f:s=s.replace('## [2.4.3]','## [2.4.4] - 2026-11-13\n\n- Refresh the map sidebar, search, display controls and settings hierarchy.\n\n## [2.4.3]',1)
 else:s=s.replace('2.4.3','2.4.4').replace('package/SCUM-MiniMap-World4/app','package/SCUM-MiniMap/app').replace('local-world4','local-map')
 p.write_text(s,encoding='utf-8-sig')

