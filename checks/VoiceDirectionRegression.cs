using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

// Use the real polylines recorded by VoiceProbe, then deliberately follow, reverse or leave them.
class VoiceDirectionRegression {
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int passed, failed;
    static void Check(bool result, string name) {
        Console.WriteLine((result ? "PASS " : "FAIL ") + name);
        if (result) passed++; else failed++;
    }
    static double Metres(PointF a, PointF b) {
        return Math.Sqrt(Math.Pow((a.X - b.X) * 15216.18, 2) + Math.Pow((a.Y - b.Y) * 15236.18, 2));
    }
    static int Main(string[] args) {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var routeType = assembly.GetType("ScumMiniMap.RoadRoute");
        var navigator = assembly.GetType("ScumMiniMap.VoiceNavigator");
        var update = navigator.GetMethod("Update", Instance);
        var wrong = navigator.GetProperty("WrongWay", Instance);
        var reroute = navigator.GetProperty("NeedsReroute", Instance);
        var rows = File.ReadAllLines(args[1]).Select(line => line.Split('\t')).ToArray();
        foreach (var test in new[]{new[]{"B007X1","桥上直行去"},new[]{"B007X1","桥下直行去"},new[]{"B042","全长去"},new[]{"B042","全长回"}}) {
            var row = rows.Single(r => r[0] == test[0] && r[1] == test[1]);
            string label = test[0] + " " + test[1];
            var coordinates = row.Last().Split(';').Select(p => p.Split(',')).ToArray();
            var points = coordinates.Select(p => new PointF(float.Parse(p[0]),float.Parse(p[1]))).ToArray();
            var route = Activator.CreateInstance(routeType, true);
            Action<string,object> set = (name,value) => routeType.GetField(name, Instance).SetValue(route, value);
            set("Success",true); set("Polyline",points); set("JunctionIndices",new int[0]);
            set("BridgeLayers",coordinates.Select(p=>int.Parse(p[2])).ToArray());
            set("PlayerPoint",points[0]); set("TargetPoint",points.Last());
            var samples = new List<PointF>();
            double arc = 0, next = 0;
            for (int i = 1; i < points.Length; i++) {
                var a = points[i - 1]; var b = points[i]; double length = Metres(a,b);
                if (length == 0) continue;
                for (; next <= arc + length; next += 4) {
                    double t = (next - arc) / length;
                    samples.Add(new PointF((float)(a.X+(b.X-a.X)*t),(float)(a.Y+(b.Y-a.Y)*t)));
                }
                arc += length;
            }
            var voice = Activator.CreateInstance(navigator, true);
            int ticks = 0;
            Func<PointF,string[]> tick = p => (string[])update.Invoke(voice, new object[]{
                route, p, new DateTime(2026,11,19).AddSeconds(++ticks * .25), false});
            bool falseReroute = false;
            foreach (var point in samples) {
                tick(point);
                falseReroute |= (bool)reroute.GetValue(voice,null);
            }
            Check(!falseReroute, label + " following every 4 m never requests reroute");
            if (test[0] != "B007X1") continue;
            voice = Activator.CreateInstance(navigator, true);
            for (int i = 0; i <= 8; i++) tick(samples[i]);
            bool reverseWarning = false;
            for (int i = 7; i >= 1; i--) {
                var clips = tick(samples[i]);
                if (clips != null && clips.Contains("10_make_a_u_turn.mp3") &&
                    (bool)wrong.GetValue(voice,null) && (bool)reroute.GetValue(voice,null)) reverseWarning = true;
            }
            Check(reverseWarning,label + " reversing 28 m speaks U-turn and requests reroute");
            for (int i = 2; i <= 8; i++) tick(samples[i]);
            Check(!(bool)wrong.GetValue(voice,null) && !(bool)reroute.GetValue(voice,null),label + " correct direction clears warning");
            var fix = samples[8];
            for (int i = 0; i < 6; i++) tick(new PointF(fix.X + (float)((100 + i * 5) / 15216.18),fix.Y));
            Check((bool)reroute.GetValue(voice,null) && !(bool)wrong.GetValue(voice,null),label + " leaving the route requests reroute");
            tick(fix);
            Check(!(bool)reroute.GetValue(voice,null),label + " rejoining the road clears deviation");
        }
        Console.WriteLine(passed+" passed, "+failed+" failed");
        return failed == 0 ? 0 : 1;
    }
}
