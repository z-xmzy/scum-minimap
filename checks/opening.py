from pathlib import Path
p=Path('source/src/WpfFieldConsole.cs')
s=p.read_text('utf-8-sig')
s=s.replace('RefreshLocalizedChrome(grid);\n            window.Closing','RefreshLocalizedChrome(grid);chromeLanguage=Localization.CurrentCode;\n            window.Closing')
s=s.replace('internal void Refresh() {\n            application.Dispatcher.VerifyAccess();', '''internal void Refresh() { RefreshView(null); }
        void RefreshView(WpfMapState openingState) {
            application.Dispatcher.VerifyAccess();''')
s=s.replace('var state=engine.WpfSnapshot(fullView);','var state=openingState??engine.WpfSnapshot(fullView);')
s=s.replace('FitTacticalToMonitor();tactical.Show();lastRefresh=-1000;', '''FitTacticalToMonitor();tactical.Show();lastRefresh=-1000;
                RefreshView(openingState);return;''')
# A hidden WPF Window is not measured by UpdateLayout. Warm its content tree at idle.
s=s.replace('internal void StartViews() { tactical.Hide();engine.WpfTactical(false);if(hudEnabled)hud.Show();Refresh(); }','''internal void StartViews() {
            tactical.Hide();engine.WpfTactical(false);if(hudEnabled)hud.Show();Refresh();
            application.Dispatcher.BeginInvoke(new Action(()=> {
                if(disposed||tactical.IsVisible||lastTacticalBounds.Width>0)return;
                FitTacticalToMonitor();
                var content=(FrameworkElement)tactical.Content;
                var size=new Size(tactical.Width,tactical.Height);
                content.Measure(size);content.Arrange(new Rect(size));
            }),DispatcherPriority.ApplicationIdle);
        }''')
p.write_text(s,'utf-8-sig')