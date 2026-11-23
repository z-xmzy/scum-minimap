using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using Forms = System.Windows.Forms;

// Run the fixture as SCUM.exe so the released foreground check sees a game
// window. The fixture only shows its own window and toggles its own cursor.
class MenuHudRegression {
    const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Field(object o, string name) { return o.GetType().GetField(name, members).GetValue(o); }
    static void Call(object o, string name, params object[] args) { o.GetType().GetMethod(name, members).Invoke(o, args); }
    static void Check(bool condition, string description) {
        if (!condition) throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }
    [STAThread] static int Main(string[] args) {
        try {
            var assembly = Assembly.LoadFrom(args[0]);
            string profile = args[1];
            Directory.CreateDirectory(profile);
            var type = assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime", true);
            using (var runtime = (IDisposable)Activator.CreateInstance(type, members, null, new object[]{profile, true}, null)) {
                var engine = Field(runtime, "engine");
                if (args[2] == "learning") {
                    bool expected = bool.Parse(args[3]);
                    var enabled = assembly.GetType("ScumMiniMap.NavTrails").GetField("LearningEnabled", BindingFlags.Static | BindingFlags.NonPublic);
                    Check((bool)Field(engine,"trailLearning") == expected && (bool)enabled.GetValue(null) == expected, "learning preference and router agree: " + expected);
                    return 0;
                }
                var native = assembly.GetType("ScumMiniMap.Native");
                var isFocused = native.GetMethod("GameFocused", BindingFlags.Static | BindingFlags.NonPublic);
                var cursorVisible = native.GetMethod("IsCursorVisible", BindingFlags.Static | BindingFlags.NonPublic);
                var hud = (Window)Field(runtime,"hud");
                var legacy = (Forms.Form)Field(engine,"overlay");
                using (var game = new Forms.Form()) {
                    game.Text = "Offline SCUM menu fixture";
                    game.StartPosition = Forms.FormStartPosition.Manual;
                    game.Bounds = new Rectangle(Forms.Cursor.Position.X-80, Forms.Cursor.Position.Y-80, 180,180);
                    game.Show(); game.Activate(); Forms.Application.DoEvents();
                    Console.WriteLine("focus={0} cursor={1} window={2} foreground={3}", isFocused.Invoke(null,null),cursorVisible.Invoke(null,null),game.Handle,native.GetMethod("GetForegroundWindow",BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null));
                    Check((bool)isFocused.Invoke(null,null) && (bool)cursorVisible.Invoke(null,null),"foreground fixture exposes the Windows cursor");
                    Call(runtime,"StartViews");
                    Check(!hud.IsVisible,"startup inside a menu keeps the minimap hidden");
                    Call(runtime,"CloseTactical",false);
                    Call(runtime,"SyncHudVisibility",true);
                    Check(!hud.IsVisible,"returning from the full map and forced recovery respect a menu");
                    Call(legacy,"SetGameFocus",true);
                    Check(legacy.Region != null && !legacy.Region.IsVisible(10,10),"legacy minimap is clipped in the menu");
                    legacy.GetType().GetProperty("FullMapMode").SetValue(legacy,true,null);
                    Check(legacy.Region == null,"legacy full map is available with a visible cursor");
                    legacy.GetType().GetProperty("FullMapMode").SetValue(legacy,false,null);
                    Forms.Cursor.Hide();
                    try {
                        Check(!(bool)cursorVisible.Invoke(null,null),"fixture returned to captured-cursor gameplay");
                        Call(runtime,"SyncHudVisibility",false);
                        Call(legacy,"SetGameFocus",true);
                        Check(hud.IsVisible && legacy.Region == null,"gameplay restores both compact maps");
                        Call(runtime,"ToggleHud");
                        Check(!hud.IsVisible,"manual hide works in gameplay");
                    } finally { Forms.Cursor.Show(); }
                    Call(runtime,"SyncHudVisibility",true);
                    Forms.Cursor.Hide();
                    try {
                        Call(runtime,"SyncHudVisibility",false);
                        Check(!hud.IsVisible,"leaving another menu preserves manual hide");
                    } finally { Forms.Cursor.Show(); }
                }
            }
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
