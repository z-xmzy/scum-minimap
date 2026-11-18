using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

// Replay GPS samples 4 metres apart at 16 m/s through the packaged navigation entry points.
class VoiceReplay {
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static double Distance(PointF a, PointF b) {
        return Math.Sqrt(Math.Pow((a.X - b.X) * 15216.18, 2) + Math.Pow((a.Y - b.Y) * 15236.18, 2));
    }
    static object Field(object obj, string name) { return obj.GetType().GetField(name, Instance).GetValue(obj); }
    static void Main(string[] args) {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var router = assembly.GetType("ScumMiniMap.NavigationRouter");
        router.GetMethod("Initialize", Static).Invoke(null, null);
        var find = router.GetMethod("FindAtHeight", Static);
        var mode = Enum.Parse(assembly.GetType("ScumMiniMap.RoadRouteMode"), "Quickest");
        var nav = assembly.GetType("ScumMiniMap.VoiceNavigator");
        var update = nav.GetMethod("Update", Instance);
        var currentCue = nav.GetProperty("CurrentCue", Instance);
        var wrongWay = nav.GetProperty("WrongWay", Instance);
        var needsReroute = nav.GetProperty("NeedsReroute", Instance);
        using (var output = new StreamWriter(args[2], false, new System.Text.UTF8Encoding(false))) {
            output.WriteLine("id\tcase\tok\tmetres\tlayers\tjunctions\tspoken_turns\tleft\tright\tuturn\twrongway\tstraight\tsamples\troute_ms\treplay_ms\tturn_positions\treroute_samples\twrong_way_samples");
            foreach (string row in File.ReadLines(args[1])) {
                var c = row.Split('\t');
                var from = new PointF(float.Parse(c[3]), float.Parse(c[4]));
                var to = new PointF(float.Parse(c[5]), float.Parse(c[6]));
                var watch = Stopwatch.StartNew();
                var route = find.Invoke(null, new object[] { from, to, mode, null,
                    c.Length > 7 ? (object)double.Parse(c[7]) : null, c.Length > 8 ? (object)double.Parse(c[8]) : null });
                double routeMs = watch.Elapsed.TotalMilliseconds;
                var points = (PointF[])Field(route, "Polyline");
                var layers = (int[])Field(route, "BridgeLayers");
                var indices = (int[])Field(route, "JunctionIndices");
                var voice = Activator.CreateInstance(nav, true);
                int left = 0, right = 0, uturn = 0, wrong = 0, straight = 0, samples = 0;
                int rerouteSamples = 0, wrongSamples = 0;
                var spoken = new HashSet<string>();
                DateTime start = new DateTime(2026, 11, 19);
                double arc = 0, next = 0;
                watch.Restart();
                if ((bool)Field(route, "Success")) for (int i = 1; i < points.Length; i++) {
                    PointF a = points[i - 1], b = points[i];
                    double length = Distance(a, b);
                    if (length == 0) continue;
                    for (; next <= arc + length; next += 4) {
                        double fraction = (next - arc) / length;
                        var position = new PointF((float)(a.X + (b.X - a.X) * fraction), (float)(a.Y + (b.Y - a.Y) * fraction));
                        var clips = (string[])update.Invoke(voice, new object[] { route, position, start.AddSeconds(next / 16), false });
                        samples++;
                        if ((bool)needsReroute.GetValue(voice, null)) rerouteSamples++;
                        if ((bool)wrongWay.GetValue(voice, null)) wrongSamples++;
                        if (clips == null) continue;
                        string action = clips[clips.Length - 1].Substring(0, 2);
                        if (action == "05") { straight++; continue; }
                        if (action == "10" && (bool)wrongWay.GetValue(voice, null)) { wrong++; continue; }
                        if (action == "06") left++; else if (action == "07") right++; else if (action == "10") uturn++; else continue;
                        var point = (PointF)Field(currentCue.GetValue(voice, null), "Point");
                        int index = Array.IndexOf(points, point);
                        spoken.Add(action + "@" + point.X.ToString("R") + "," + point.Y.ToString("R") +
                            ",L" + (layers == null || index < 0 ? 0 : layers[index]));
                    }
                    arc += length;
                }
                output.WriteLine(string.Join("\t", new object[] {c[0], c[1], Field(route, "Success"), arc.ToString("F2"),
                    layers == null ? "" : string.Join(",", layers.Distinct()), indices == null ? 0 : indices.Length,
                    spoken.Count, left, right, uturn, wrong, straight, samples, routeMs.ToString("F3"),
                    watch.Elapsed.TotalMilliseconds.ToString("F3"), string.Join(";", spoken.OrderBy(s => s)), rerouteSamples, wrongSamples}));
            }
        }
    }
}
