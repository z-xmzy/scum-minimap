from pathlib import Path
src=Path('source/src')
p=src/'GridRouter.Bridges.cs';s=p.read_text(encoding='utf-8-sig')
at=s.rfind('        byte terrainAt')
s=s[:at]+"""
        // Traverse actual routing edges so a road above the player cannot add a junction arm.
        IEnumerable<int> BridgeRoadNeighbours(int node) {
            var map = bridges;
            if (node >= map.GroundCount) {
                foreach (int next in map.Deck[node - map.GroundCount].Neighbours) {
                    byte terrain;
                    if (next >= map.GroundCount || GroundCost(next, out terrain) <= CostDirt)
                        yield return next;
                }
                yield break;
            }
            int x = node % map.Width, y = node / map.Width;
            for (int dy = -1; dy <= 1; dy++) {
                for (int dx = -1; dx <= 1; dx++) {
                    if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= map.Width ||
                        y + dy < 0 || y + dy >= map.Width) continue;
                    byte terrain;
                    int next = node + dx + dy * map.Width;
                    if (GroundCost(next, out terrain) > CostDirt) continue;
                    if (dx != 0 && dy != 0 &&
                        (GroundCost(node + dx, out terrain) > CostDirt ||
                         GroundCost(node + dy * map.Width, out terrain) > CostDirt)) continue;
                    yield return next;
                }
            }
            List<int> links;
            if (map.UnderRoads.TryGetValue(node, out links))
                foreach (int next in links) yield return next;
            if (map.Entrances.TryGetValue(node, out links))
                foreach (int next in links) yield return next;
        }

        bool UsesBridgeTopology(PointF point, int layer) {
            return bridges != null && (layer > 0 || bridges.Ground.ContainsKey(bridges.Cell(point)));
        }

        int BridgeJunctionArms(PointF point, int layer, bool exact, Dictionary<int, int> cache) {
            var map = bridges;
            int cell = map.Cell(point), center = -1;
            int radius = exact ? 0 : 2;
            double nearest = double.MaxValue;
            for (int dy = -radius; dy <= radius; dy++) {
                for (int dx = -radius; dx <= radius; dx++) {
                    int x = cell % map.Width + dx, y = cell / map.Width + dy;
                    if (x < 0 || y < 0 || x >= map.Width || y >= map.Width) continue;
                    int candidate = x + y * map.Width;
                    if (layer == 0) {
                        byte terrain;
                        if (GroundCost(candidate, out terrain) > CostDirt) continue;
                        double distance = RoadRouter.DistanceMeters(point, map.Point(candidate));
                        if (distance < nearest) { center = candidate; nearest = distance; }
                    } else {
                        List<int> nodes;
                        if (!map.ByCell.TryGetValue(candidate, out nodes)) continue;
                        foreach (int id in nodes) {
                            if (map.Group(id) != layer) continue;
                            double distance = RoadRouter.DistanceMeters(point, map.Point(id));
                            if (distance < nearest) { center = id; nearest = distance; }
                        }
                    }
                }
            }
            if (center < 0 || (exact && layer > 0 && nearest > 1.5)) return -1;
            int answer;
            if (cache.TryGetValue(center, out answer)) return answer;
            PointF origin = map.Point(center);
            var adjacency = new Dictionary<int, List<int>>();
            var squaredDistance = new Dictionary<int, double>();
            var queue = new Queue<int>();
            adjacency.Add(center, new List<int>());
            queue.Enqueue(center);
            while (queue.Count > 0) {
                int node = queue.Dequeue();
                var current = map.Point(node);
                double ox = (current.X - origin.X) * 15216.18;
                double oy = (current.Y - origin.Y) * 15236.18;
                squaredDistance[node] = ox * ox + oy * oy;
                foreach (int next in BridgeRoadNeighbours(node)) {
                    var position = map.Point(next);
                    double x = (position.X - origin.X) * 15216.18;
                    double y = (position.Y - origin.Y) * 15236.18;
                    if (Math.Abs(x) > 56 || Math.Abs(y) > 56) continue;
                    adjacency[node].Add(next);
                    if (adjacency.ContainsKey(next)) continue;
                    adjacency.Add(next, new List<int>());
                    queue.Enqueue(next);
                }
            }
            var seen = new HashSet<int>();
            int arms = 0;
            foreach (int start in adjacency.Keys) {
                if (squaredDistance[start] <= 16 * 16 || !seen.Add(start)) continue;
                int count = 0;
                bool touchesRing = false;
                queue.Enqueue(start);
                while (queue.Count > 0) {
                    int node = queue.Dequeue();
                    count++;
                    if (squaredDistance[node] <= 24 * 24) touchesRing = true;
                    foreach (int next in adjacency[node]) {
                        if (squaredDistance[next] > 16 * 16 && seen.Add(next)) queue.Enqueue(next);
                    }
                }
                if (count >= 4 && touchesRing && ++arms >= 3) break;
            }
            cache[center] = arms;
            return arms;
        }

        static int RouteLayerAtArc(double[] distances, List<int> layers, double arc) {
            if (layers == null) return 0;
            int index = Array.BinarySearch(distances, arc);
            if (index >= 0) return layers[index];
            index = ~index;
            if (index == 0) return layers[0];
            if (index >= layers.Count) return layers[layers.Count - 1];
            return arc - distances[index - 1] <= distances[index] - arc ? layers[index - 1] : layers[index];
        }
""" +s[at:]
p.write_text(s,encoding='utf-8-sig')
p=src/'GridRouter.RoadOnly.cs';s=p.read_text(encoding='utf-8-sig')
a='''        internal int[] BuildJunctionIndices(List<PointF> polyline, bool startConnector, bool endConnector) {
            if (polyline == null || polyline.Count < 4) return new int[0];'''
b='''        internal int[] BuildJunctionIndices(List<PointF> polyline, bool startConnector, bool endConnector) {
            return BuildRouteJunctionIndices(polyline, startConnector, endConnector, null);
        }

        int[] BuildRouteJunctionIndices(List<PointF> polyline, bool startConnector, bool endConnector, List<int> layers) {
            if (polyline == null || polyline.Count < (layers == null ? 4 : 3)) return new int[0];'''
assert a in s;s=s.replace(a,b)
a='''            // 1) 4 m candidate scan'''
idx=s.index(a,s.index('int[] BuildRouteJunctionIndices'))
s=s[:idx]+'''            var bridgeArms = new Dictionary<int, int>();
            Func<PointF, int, bool, int> countArms = (point, layer, exact) => {
                if (layers != null && UsesBridgeTopology(point, layer))
                    return BridgeJunctionArms(point, layer, exact, bridgeArms);
                return exact
                    ? TopologicalArmsAtExact(point.X, point.Y, armsRoad, armsFull, armsLab, armsStack)
                    : TopologicalArmsAt(point.X, point.Y, armsRoad, armsFull, armsLab, armsStack);
            };
'''+s[idx:]
s=s.replace('''                if (!RoadBranchCandidate(point.X, point.Y, ring, ringStack)) continue;''','''                int layer = RouteLayerAtArc(dists, layers, arc);
                if ((layers == null || !UsesBridgeTopology(point, layer)) &&
                    !RoadBranchCandidate(point.X, point.Y, ring, ringStack)) continue;''')
s=s.replace('if (TopologicalArmsAt(point.X, point.Y, armsRoad, armsFull, armsLab, armsStack) >= 3) hits.Add(arc);','if (countArms(point, layer, false) >= 3) hits.Add(arc);')
s=s.replace('if (TopologicalArmsAtExact(candidatePoint.X, candidatePoint.Y, armsRoad, armsFull, armsLab, armsStack) < 3) continue;','if (countArms(candidatePoint, RouteLayerAtArc(dists, layers, hits[k]), true) < 3) continue;')
s=s.replace('List<PointF> work = new List<PointF>(polyline);','List<PointF> work = new List<PointF>(polyline);\\n            List<int> workLayers = layers == null ? null : new List<int>(layers);'.replace('\\n','\n'))
s=s.replace('''                    work.Insert(pos, point);
                    workArc.Insert(pos, cueArc);
                    nearest = pos;''','''                    // Preserve the exact endpoints of an inter-layer connection.
                    if (workLayers != null && workLayers[pos - 1] != workLayers[pos]) {
                        nearest = cueArc - workArc[pos - 1] <= workArc[pos] - cueArc ? pos - 1 : pos;
                    } else {
                        work.Insert(pos, point);
                        workArc.Insert(pos, cueArc);
                        if (workLayers != null) workLayers.Insert(pos, RouteLayerAtArc(dists, layers, cueArc));
                        nearest = pos;
                    }''')
s=s.replace('if (TopologicalArmsAtExact(work[v].X, work[v].Y, armsRoad, armsFull, armsLab, armsStack) < 3) continue;','if (countArms(work[v], workLayers == null ? 0 : workLayers[v], true) < 3) continue;')
s=s.replace('''            polyline.AddRange(work);
            return kept.ToArray();''','''            polyline.AddRange(work);
            if (layers != null) { layers.Clear(); layers.AddRange(workLayers); }
            return kept.ToArray();''')
p.write_text(s,encoding='utf-8-sig')
p=src/'GridRouter.Local.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('''            var r=new RoadRoute{Success=true,Mode=mode,PlayerPoint=path[0]''','''            int[] layerCues = layers != null && mode == RoadRouteMode.Quickest
                ? BuildRouteJunctionIndices(path, false, false, layers) : null;
            var r=new RoadRoute{Success=true,Mode=mode,PlayerPoint=path[0]''')
s=s.replace('r.JunctionIndices=indices.ToArray();return r;', 'r.JunctionIndices=layerCues ?? indices.ToArray();return r;')
p.write_text(s,encoding='utf-8-sig')
# Stamp the isolated candidate after recording the change.
p=Path('source/CHANGELOG.md');s=p.read_text(encoding='utf-8-sig');s=s.replace('# Changelog','# Changelog\\n\\n## [2.4.11] - 2026-11-19\\n\\n- Rebuild junction cues from connected bridge and ground layers after route repair.\\n- Recheck unresolved bridge surfaces against placed game meshes.'.replace('\\n','\n'),1);p.write_text(s,encoding='utf8')
for name in ['Build-LocalCandidate.ps1','source/src/MiniMap.cs','source/packaging/LocalPackage.txt','tools/package-ui.py']:
 p=Path(name)
 if not p.exists():continue
 s=p.read_text(encoding='utf-8-sig').replace('2.4.10','2.4.11')
 p.write_text(s,encoding='utf-8-sig' if p.suffix in ['.cs','.ps1'] else 'utf8')
