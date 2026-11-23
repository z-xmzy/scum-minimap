"""Extract the visible yellow road network from the bundled SCUM map into a compact
routable graph for the MiniMap navigation fix.

Pipeline (offline, reproducible):
  1. yellow mask on the 14481x14481 map.png (full-res strip processing; low memory)
  2. block grid: a 12x12 px block (~12.6 m) is a road node when enough yellow pixels
  3. 8-neighbour edges, real metre lengths, water-mask rejected
  4. keep only large connected components (drops POI icons / labels / borders)
  5. write app/assets/map-roads.bin + overlay preview PNG

Output format (little endian):
    magic  "MROD" (4 B)
    uint16 version = 1
    uint16 reserved = 0
    int32  nodeCount
    int32  edgeCount
    nodes[nodeCount] : float32 u, float32 v        (UV 0..1 on map.png)
    edges[edgeCount] : int32 u, int32 v, float32 lengthMeters
"""
import argparse
import json
import math
import struct
import time
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
RES = ROOT / 'source' / 'resources'
MAP = RES / 'map.png'
WATER = RES / 'water-mask.bin'
ROADS = RES / 'roads.bin'

MAP_W = 1521618.0          # cm across map (RoadRouter.MapScaleX*100)
MAP_H = 1523618.0
SCALE_X = MAP_W / 100.0    # 15216.18 m
SCALE_Y = MAP_H / 100.0    # 15236.18 m

BLOCK = 16                 # map pixels per graph node (~16.8 m)
MIN_BLOCK_YELLOW = 44      # ~17% of a 16x16 block; roads are ~7 px wide -> ~112
MIN_COMPONENT_NODES = 120  # drop icons / labels / map border fragments
WATER_MAX_M = 1.0


def yellow_mask_strip(arr):
    """RGB uint8 strip -> boolean mask (yellow road colour)."""
    r = arr[:, :, 0].astype(np.int16)
    g = arr[:, :, 1].astype(np.int16)
    b = arr[:, :, 2].astype(np.int16)
    return (r > 190) & (g > 140) & (b < 110) & ((r - b) > 90)


def load_water():
    import gzip
    raw = gzip.decompress(Path(WATER).read_bytes())
    assert raw[:4] == b'WATR'
    w, h = struct.unpack_from('<ii', raw, 4)
    return w, h, raw[12:]


class Water:
    def __init__(self):
        self.w, self.h, self.bits = load_water()

    def wet(self, u, v):
        if not (0 <= u < 1 and 0 <= v < 1):
            return True
        idx = int(v * self.h) * self.w + int(u * self.w)
        return bool(self.bits[idx >> 3] & (1 << (idx & 7)))

    def allows(self, a, b):
        dx, dy = b[0] - a[0], b[1] - a[1]
        n = max(1, math.ceil(max(abs(dx) * 15216.18, abs(dy) * 15236.18) / WATER_MAX_M))
        for i in range(n + 1):
            if self.wet(a[0] + dx * i / n, a[1] + dy * i / n):
                return False
        return True


def build_block_grid(block=BLOCK, threshold=MIN_BLOCK_YELLOW, mask=None):
    t0 = time.time()
    im = None
    if mask is None:
        im = Image.open(MAP).convert('RGB')
        w, h = im.size
    else:
        h, w = mask.shape
    nb_x = (w + block - 1) // block
    nb_y = (h + block - 1) // block
    counts = np.zeros((nb_y, nb_x), dtype=np.uint16)
    strip_rows = block * 100            # 100 block rows per strip
    for by in range(0, nb_y, 100):
        y0 = by * block
        y1 = min(h, y0 + strip_rows)
        if mask is None:
            arr = np.asarray(im.crop((0, y0, w, y1)))
            m = yellow_mask_strip(arr)
        else:
            m = mask[y0:y1]
        # pad columns to a multiple of block
        pad = nb_x * block - w
        if pad:
            m = np.pad(m, ((0, 0), (0, pad)), constant_values=False)
        rows = (m.shape[0] + block - 1) // block
        need = rows * block
        if m.shape[0] < need:
            m = np.pad(m, ((0, need - m.shape[0]), (0, 0)), constant_values=False)
        counts[by:by + rows] += m.reshape(rows, block, nb_x, block).sum(axis=(1, 3)).astype(np.uint16)
    road = counts >= threshold
    print(f'[extract] yellow mask blocks: {(counts>0).sum()} nonzero, road blocks: {int(road.sum())} '
          f'({road.sum()*block*block/(w*h)*100:.2f}% of map) in {time.time()-t0:.1f}s')
    return road, w, h, nb_x, nb_y


def build_yellow_maps(radius=4):
    """Full-resolution yellow mask plus a mask dilated by `radius` pixels."""
    im = Image.open(MAP).convert('RGB')
    w, h = im.size
    mask = np.zeros((h, w), dtype=bool)
    strip = 2000
    for y0 in range(0, h, strip):
        y1 = min(h, y0 + strip)
        arr = np.asarray(im.crop((0, y0, w, y1)))
        mask[y0:y1] = yellow_mask_strip(arr)
    del arr, im
    near = mask.copy()
    for _ in range(radius):
        near = near | np.roll(near, 1, 0) | np.roll(near, -1, 0) | np.roll(near, 1, 1) | np.roll(near, -1, 1)
    print(f'[extract] yellow mask: {int(mask.sum())} px; dilated by {radius}px')
    return mask, near, w, h


def samples_between(a, b, w, h, step_px=2.0):
    """Polyline pixel samples between two UV points at <= step_px spacing."""
    ax, ay = a[0] * w, a[1] * h
    bx, by = b[0] * w, b[1] * h
    n = max(1, int(math.ceil(max(abs(bx - ax), abs(by - ay)) / step_px)))
    out = []
    for i in range(n + 1):
        x = ax + (bx - ax) * i / n
        y = ay + (by - ay) * i / n
        xi = max(0, min(w - 1, int(round(x))))
        yi = max(0, min(h - 1, int(round(y))))
        out.append((xi, yi))
    return out


def build_graph(road, w, h, nb_x, nb_y, water, near=None, mask=None):
    t0 = time.time()
    node_of = -np.ones((nb_y, nb_x), dtype=np.int32)
    coords = []
    for by in range(nb_y):
        row = road[by]
        for bx in np.nonzero(row)[0]:
            # Snap the node to the centroid of the yellow pixels inside its block
            # instead of the block centre, so node positions sit on the road.
            x0, x1 = bx * BLOCK, min(w, (bx + 1) * BLOCK)
            y0, y1 = by * BLOCK, min(h, (by + 1) * BLOCK)
            if mask is not None:
                sub = mask[y0:y1, x0:x1]
                ys_, xs_ = np.nonzero(sub)
                if len(xs_):
                    px = x0 + xs_.mean() + 0.5
                    py = y0 + ys_.mean() + 0.5
                else:
                    px = (x0 + x1) * 0.5
                    py = (y0 + y1) * 0.5
            else:
                px = (x0 + x1) * 0.5
                py = (y0 + y1) * 0.5
            u = min(0.99999, px / w)
            v = min(0.99999, py / h)
            if water.wet(u, v):
                continue
            node_of[by, bx] = len(coords)
            coords.append((u, v))
    print(f'[extract] nodes on dry ground: {len(coords)} ({time.time()-t0:.1f}s)')
    edges = []
    dirs = [(1, 0), (0, 1), (1, 1), (1, -1)]
    rejected = 0
    split = 0
    for by in range(nb_y):
        for bx in range(nb_x):
            i = node_of[by, bx]
            if i < 0:
                continue
            for dx, dy in dirs:
                cx, cy = bx + dx, by + dy
                if cx < 0 or cx >= nb_x or cy < 0 or cy >= nb_y:
                    continue
                j = node_of[cy, cx]
                if j < 0:
                    continue
                a, b = coords[i], coords[j]
                if not water.allows(a, b):
                    continue
                # Phantom-edge guard: every 2 px sample of the block-to-block
                # connection must stay within 4 px of the yellow mask.
                diagonal = dx != 0 and dy != 0
                if diagonal and near is not None:
                    run = samples_between(a, b, w, h, 2.0)
                    valid = all(near[sy, sx] for sx, sy in run)
                    if valid and not (road[by, cx] or road[cy, bx]):
                        valid = False        # corner cut with no orthogonal road block
                    if not valid:
                        rejected += 1
                        continue
                length = math.hypot((b[0] - a[0]) * SCALE_X, (b[1] - a[1]) * SCALE_Y)
                edges.append((int(i), int(j), length))
    print(f'[extract] raw edges: {len(edges)}, rejected by yellow guard: {rejected} ({time.time()-t0:.1f}s)')
    return coords, edges


def components(nodes_n, edges):
    adj = [[] for _ in range(nodes_n)]
    for k, (u, v, _l) in enumerate(edges):
        adj[u].append((v, k))
        adj[v].append((u, k))
    comp = [-1] * nodes_n
    sizes = []
    for start in range(nodes_n):
        if comp[start] >= 0:
            continue
        cid = len(sizes)
        stack = [start]
        comp[start] = cid
        n = 0
        while stack:
            x = stack.pop()
            n += 1
            for y, _ in adj[x]:
                if comp[y] < 0:
                    comp[y] = cid
                    stack.append(y)
        sizes.append(n)
    return comp, sizes


def rdp(points, eps):
    """Ramer-Douglas-Peucker simplification; keeps both endpoints."""
    if len(points) < 3:
        return list(points)
    a, b = points[0], points[-1]
    ab = (b[0] - a[0], b[1] - a[1])
    l2 = ab[0] * ab[0] + ab[1] * ab[1]
    best, idx = -1.0, 0
    for i in range(1, len(points) - 1):
        p = points[i]
        if l2 > 0:
            t = max(0.0, min(1.0, ((p[0] - a[0]) * ab[0] + (p[1] - a[1]) * ab[1]) / l2))
        else:
            t = 0.0
        q = (a[0] + t * ab[0], a[1] + t * ab[1])
        d = math.hypot(p[0] - q[0], p[1] - q[1])
        if d > best:
            best, idx = d, i
    if best <= eps:
        return [a, b]
    left = rdp(points[:idx + 1], eps)
    right = rdp(points[idx:], eps)
    return left[:-1] + right


def polyline_length(points):
    return sum(math.hypot((b[0] - a[0]) * SCALE_X, (b[1] - a[1]) * SCALE_Y)
               for a, b in zip(points, points[1:]))


def chain_merge(nodes, edges, rdp_meters=2.5):
    """Contract degree-2 nodes into polyline edges; keep junctions and endpoints."""
    adj = [[] for _ in range(len(nodes))]
    for eid, (u, v, _l) in enumerate(edges):
        if u == v:
            continue
        adj[u].append(eid)
        adj[v].append(eid)
    deg = [len(x) for x in adj]
    used = [False] * len(edges)
    emap = {eid: (u, v) for eid, (u, v, _l) in enumerate(edges)}
    out_nodes = []
    index_of = {}

    def node_index(src):
        if src in index_of:
            return index_of[src]
        index_of[src] = len(out_nodes)
        out_nodes.append(nodes[src])
        return index_of[src]

    result = []

    def walk(start, first_edge):
        points = [nodes[start]]
        cur = start
        eid = first_edge
        while True:
            if used[eid]:
                return None
            used[eid] = True
            u, v = emap[eid]
            nxt = v if u == cur else u
            points.append(nodes[nxt])
            if deg[nxt] != 2:
                break
            nxt_edges = [e for e in adj[nxt] if e != eid]
            if not nxt_edges:
                break
            eid = nxt_edges[0]
            cur = nxt
        if len(points) > 2:
            points = rdp(points, rdp_meters / SCALE_X)
        length = polyline_length(points)
        if length <= 0.01:
            return None
        result.append((node_index(start), node_index(nxt), length, points))
        return True

    for i in range(len(nodes)):
        if deg[i] == 2:
            continue
        for eid in adj[i]:
            if not used[eid]:
                walk(i, eid)
    # closed loops with every node degree 2
    for eid, (u, v) in emap.items():
        if not used[eid]:
            walk(u, eid)
    return out_nodes, result


def deviation_stats(edges_poly, mask, w, h, max_level=12):
    """Sample every final edge at <=2 px and report distance to the yellow mask."""
    xs, ys, diag = [], [], []
    for _u, _v, _length, points in edges_poly:
        for a, b in zip(points, points[1:]):
            dxp = (b[0] - a[0]) * w
            dyp = (b[1] - a[1]) * h
            seg = math.hypot(dxp, dyp)
            is_diag = seg > 0 and abs(dxp) > 0.2 * seg and abs(dyp) > 0.2 * seg
            for sx, sy in samples_between(a, b, w, h, 2.0):
                xs.append(sx)
                ys.append(sy)
                diag.append(is_diag)
    xa = np.asarray(xs, dtype=np.int32)
    ya = np.asarray(ys, dtype=np.int32)
    da = np.asarray(diag, dtype=bool)
    n = len(xa)
    dist = np.full(n, 99, dtype=np.int8)
    pending = np.ones(n, dtype=bool)
    nf = mask.copy()
    for level in range(0, max_level + 1):
        if level > 0:
            nf = nf | np.roll(nf, 1, 0) | np.roll(nf, -1, 0) | np.roll(nf, 1, 1) | np.roll(nf, -1, 1)
        hit = nf[ya, xa]
        newly = hit & pending
        dist[newly] = level
        pending &= ~hit
        if not pending.any():
            break
    far = dist >= 4          # >3.5 px
    very = dist >= 7         # >6 px
    print(f'[extract] yellow check: samples={n} >3.5px={int(far.sum())} ({100.0*far.sum()/max(1,n):.2f}%) '
          f'>6px={int(very.sum())} ({100.0*very.sum()/max(1,n):.2f}%) maxDev={int(dist.max())}px')
    for label, subset in (('orthogonal', ~da), ('diagonal', da)):
        count = int(subset.sum())
        print(f'[extract]   {label}: n={count} >3.5px={int(far[subset].sum())} '
              f'({100.0*far[subset].sum()/max(1,count):.2f}%) >6px={int(very[subset].sum())} '
              f'({100.0*very[subset].sum()/max(1,count):.2f}%) maxDev={int(dist[subset].max()) if count else 0}px')
    return {'samples': n, 'over35': int(far.sum()), 'over6': int(very.sum()),
            'maxDevPx': int(dist.max())}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--output', type=Path, default=ROOT / 'app' / 'assets' / 'map-roads.bin')
    ap.add_argument('--preview', type=Path, default=ROOT / 'checks' / 'map-roads-20261105' / 'preview.png')
    ap.add_argument('--block', type=int, default=BLOCK)
    ap.add_argument('--threshold', type=int, default=MIN_BLOCK_YELLOW)
    ap.add_argument('--min-component', type=int, default=MIN_COMPONENT_NODES)
    ap.add_argument('--rdp', type=float, default=2.5, help='chain polyline simplification tolerance in metres')
    ap.add_argument('--no-guard', action='store_true', help='disable per-segment yellow validation')
    args = ap.parse_args()

    t_start = time.time()
    water = Water()
    mask, near, w, h = build_yellow_maps(4)
    road, w, h, nb_x, nb_y = build_block_grid(args.block, args.threshold, mask)
    coords, edges = build_graph(road, w, h, nb_x, nb_y, water, None if args.no_guard else near, mask)
    del near

    # keep only large dry connected components
    comp, sizes = components(len(coords), edges)
    keep = {c for c, n in enumerate(sizes) if n >= args.min_component}
    remap = {}
    out_nodes = []
    for i, c in enumerate(comp):
        if c in keep:
            remap[i] = len(out_nodes)
            out_nodes.append(coords[i])
    out_edges = [(remap[u], remap[v], l) for u, v, l in edges if u in remap and v in remap]
    print(f'[extract] components: {len(sizes)} (kept {len(keep)}), nodes {len(coords)}->{len(out_nodes)}, '
          f'edges {len(edges)}->{len(out_edges)}')

    # component summary
    top = sorted(sizes, reverse=True)[:6]
    print(f'[extract] largest components: {top}')

    # chain-merge degree-2 nodes into polyline edges (junction graph)
    m_nodes, m_edges = chain_merge(out_nodes, out_edges, rdp_meters=args.rdp)
    m_edges = [e for e in m_edges if e[0] != e[1]]   # contracted loops are not routable edges
    pre_nodes = len(out_nodes)
    # purification: drop duplicate / reverse-parallel edges (same node pair)
    seen = set()
    unique = []
    for edge in m_edges:
        key = (edge[0], edge[1]) if edge[0] < edge[1] else (edge[1], edge[0])
        if key in seen:
            continue
        seen.add(key)
        unique.append(edge)
    m_edges = unique
    # purification: iteratively prune short dead-end branches (degree 1, <= 25 m)
    pruned = 0
    while True:
        deg = {}
        for u, v, _l, _p in m_edges:
            deg[u] = deg.get(u, 0) + 1
            deg[v] = deg.get(v, 0) + 1
        keep = []
        removed = False
        for edge in m_edges:
            u, v, length, _p = edge
            if (deg[u] == 1 and length <= 25.0) or (deg[v] == 1 and length <= 25.0):
                removed = True
                pruned += 1
                continue
            keep.append(edge)
        m_edges = keep
        if not removed:
            break
    used = set()
    for edge in m_edges:
        used.add(edge[0])
        used.add(edge[1])
    print(f'[extract] purification: duplicates removed, short dead-ends pruned={pruned}, '
          f'nodes used={len(used)} (was {pre_nodes})')
    m_len = sum(e[2] for e in m_edges)
    v1_len = sum(e[2] for e in out_edges)
    print(f'[extract] chain merge: nodes {len(out_nodes)}->{len(m_nodes)}, '
          f'edges {len(out_edges)}->{len(m_edges)}, points={sum(len(e[3]) for e in m_edges)}, '
          f'length {m_len/1000:.1f} km (grid {v1_len/1000:.1f} km, delta {100*(m_len-v1_len)/v1_len:+.2f}%)')

    # write binary (version 2: polyline edges)
    data = bytearray(b'MROD' + struct.pack('<HHI', 2, 0, len(m_nodes)) + struct.pack('<I', len(m_edges)))
    for u, v in m_nodes:
        data.extend(struct.pack('<ff', u, v))
    for u, v, l, points in m_edges:
        data.extend(struct.pack('<iifH', u, v, l, len(points)))
        for pu, pv in points:
            data.extend(struct.pack('<ff', pu, pv))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(data)

    print(f'[extract] wrote {args.output} ({len(data)} bytes, {len(data)/1024/1024:.2f} MiB)')
    print(f'[extract] nodes={len(m_nodes)} edges={len(m_edges)} total_edge_length={m_len/1000:.1f} km')
    deviation_stats(m_edges, mask, w, h)
    print(f'[extract] total time {time.time()-t_start:.1f}s')

    # preview: map background + extracted graph
    try:
        prev_w = 2400
        im = Image.open(MAP).convert('RGB').resize((prev_w, prev_w), Image.LANCZOS)
        from PIL import ImageDraw
        dr = ImageDraw.Draw(im, 'RGBA')
        s = prev_w / w
        for _u, _v, _l, points in m_edges:
            dr.line([(pu * w * s, pv * h * s) for pu, pv in points], fill=(0, 255, 255, 255), width=1)
        args.preview.parent.mkdir(parents=True, exist_ok=True)
        im.save(args.preview)
        # Samobor inset
        try:
            import sys
            sys.path.insert(0, str(ROOT / 'checks' / 'route-analysis-20261105'))
            import parse_roads as pr
            sm = pr.load_scummap()
            for name, want in (('samobor', 'Samobor City'), ('novigrad', 'Novigrad')):
                m = next((x for x in sm['markers'] if x['title'] == want), None)
                if not m:
                    continue
                inset_px = 900
                half = 0.035
                crop = Image.open(MAP).convert('RGB').crop((
                    int((m['u'] - half) * w), int((m['v'] - half) * h),
                    int((m['u'] + half) * w), int((m['v'] + half) * h))).resize((inset_px, inset_px), Image.LANCZOS)
                d2 = ImageDraw.Draw(crop, 'RGBA')
                sc = inset_px / (2 * half * w)
                for _u, _v, _l, points in m_edges:
                    pix = [((pu - (m['u'] - half)) * w * sc, (pv - (m['v'] - half)) * h * sc) for pu, pv in points]
                    if any(-50 < x < inset_px + 50 and -50 < y < inset_px + 50 for x, y in pix):
                        d2.line(pix, fill=(0, 255, 255, 255), width=2)
                crop.save(args.preview.parent / f'inset-{name}.png')
        except Exception as exc:
            print('[extract] inset preview skipped:', exc)
        print(f'[extract] preview -> {args.preview}')
    except Exception as exc:
        print('[extract] preview skipped:', exc)


if __name__ == '__main__':
    main()













