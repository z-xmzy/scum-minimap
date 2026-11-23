"""Entry/room connectivity audit for the layered bunker maps.

Why this exists
---------------
The bunker plans are stored as independent display pages (views) per site.
Stairs/ramps are deliberately NOT merged into the floor grid (`stairs remain
separate` in check_geometry), so a plain surface-continuity walk reports most
rooms of a multi-floor site as unreachable. This audit therefore works per
display page and treats the stair/ramp triangles that the WPF renderer draws
on that page as connectors.

Output (per site, per page):
  ENTRY_*   where each audited entrance floor sample lands, and whether that
            floor component is the page's main plate or joins it through stairs
  unreachable[]  room labels on the SAME page whose floor component is not
            connected to the entrance component (cross-page rooms are a
            different floor/area and are only counted)

This is a cartographic/geometry audit, not live game verification: it does not
model locks, doors, ladders or collision meshes that were never sampled.

Usage:
  python tools/audit_connectivity.py                # fast stair mask, all sites
  python tools/audit_connectivity.py --full         # exact triangle raster
  python tools/audit_connectivity.py --only B_4 C_1 # limit sites
"""
import argparse
import collections
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
CHECKS = ROOT / "checks"
sys.path.insert(0, str(ROOT / "tools"))

import build_views as build  # noqa: E402  (sets up analyze/numpy import paths)
import analyze  # noqa: E402
import numpy as np  # noqa: E402

import check_reachability as cr  # noqa: E402

analyze.FOLDER = build.OUT


def stair_mask(faces, hi, step, shape, exact):
    """Cells where the renderer draws stair/ramp triangles on this page."""
    mask = np.zeros(shape, bool)
    if faces is None or len(faces) == 0:
        return mask
    if exact:
        size = np.array([shape[1], shape[0]])
        for tri in faces:
            hit = build.raster_points(np.asarray(tri, dtype="f8"), hi, step, size)
            if hit is None:
                continue
            yy, xx, _ = hit
            mask[yy, xx] = True
        return mask
    f = np.asarray(faces, dtype="f8")
    pts = [f.reshape(-1, 3), (f[:, 0] + f[:, 1]) / 2, (f[:, 1] + f[:, 2]) / 2,
           (f[:, 2] + f[:, 0]) / 2, f.mean(axis=1)]
    pts = np.concatenate(pts, axis=0)
    px = np.floor((hi[0] - pts[:, 0]) / step).astype(int)
    py = np.floor((hi[1] - pts[:, 1]) / step).astype(int)
    ok = (px >= 0) & (px < shape[1]) & (py >= 0) & (py < shape[0])
    mask[py[ok], px[ok]] = True
    mask |= np.roll(mask, 1, 0) | np.roll(mask, -1, 0) | np.roll(mask, 1, 1) | np.roll(mask, -1, 1)
    return mask


class Union:
    def __init__(self):
        self.parents = {}

    def root(self, a):
        self.parents.setdefault(a, a)
        while self.parents[a] != a:
            self.parents[a] = self.parents[self.parents[a]]
            a = self.parents[a]
        return a

    def join(self, a, b):
        ra, rb = self.root(a), self.root(b)
        if ra != rb:
            self.parents[rb] = ra


def page_data(view, grid, hi, step, shape, exact):
    ids = cr.terrain_groups([grid])[0]
    sizes = np.bincount(ids.ravel())
    stairs = stair_mask(view["stairs"], hi, step, shape, exact)
    union = Union()
    stair_node = ("stair", view["id"])
    if stairs.any():
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh = np.roll(stairs, (dy, dx), (0, 1))
            cc = np.roll(ids, (dy, dx), (0, 1))
            if dy == 1:
                sh[0] = False
                cc[0] = 0
            elif dy == -1:
                sh[-1] = False
                cc[-1] = 0
            elif dx == 1:
                sh[:, 0] = False
                cc[:, 0] = 0
            else:
                sh[:, -1] = False
                cc[:, -1] = 0
            for val in np.unique(cc[sh & (cc > 0)]):
                union.join(stair_node, ("comp", view["id"], int(val)))
    largest = int(np.argmax(sizes[1:]) + 1) if len(sizes) > 1 else 0
    return dict(view=view, grid=grid, ids=ids, sizes=sizes, stairs=stairs,
                union=union, stair_node=stair_node, largest=largest)


def audit(exact, only):
    views = analyze.load("views.bin")
    entries = json.loads((build.ROOT / "checks/entrance-audit.json").read_text(encoding="utf-8"))["entries"]
    rooms = [r.split("\t") for r in (build.OUT / "labels-bounded.tsv").read_text(encoding="utf-8").splitlines()
             if r.endswith("\troom")]
    by_site = collections.defaultdict(list)
    for v in views:
        by_site[v["bunker"]].append(v)
    rooms_by_view = collections.defaultdict(list)
    for r in rooms:
        rooms_by_view[r[0]].append(r)
    entries_by_site = collections.defaultdict(list)
    for e in entries:
        entries_by_site[e["bunker"]].append(e)

    report = []
    for site in sorted(by_site):
        if only and site not in only:
            continue
        vs = sorted(by_site[site], key=lambda r: r["rank"])
        hi, step, grids = analyze.aligned(vs)
        shape = grids[0].shape
        pages = [page_data(v, grids[i], hi, step, shape, exact) for i, v in enumerate(vs)]
        index_of_view = {p["view"]["id"]: k for k, p in enumerate(pages)}
        items = []
        pin = None
        for e in entries_by_site.get(site, []):
            idx = index_of_view.get(e.get("view"))
            if idx is None:
                items.append(dict(type="ENTRY_VIEW_MISSING", label=e.get("label"), view=e.get("view")))
                continue
            page = pages[idx]
            x, y = float(e["nearestFloor"][0]), float(e["nearestFloor"][1])
            col = int(np.floor((hi[0] - x) / step))
            row = int(np.floor((hi[1] - y) / step))
            comp = int(page["ids"][row, col]) if (0 <= row < shape[0] and 0 <= col < shape[1]) else 0
            if comp == 0:
                items.append(dict(type="ENTRY_HOLE", label=e.get("label"), page=page["view"]["rank"]))
                continue
            vid = page["view"]["id"]
            root = page["union"].root(("comp", vid, comp))
            on_main = root == page["union"].root(("comp", vid, page["largest"]))
            via_stairs = page["stairs"].any() and root == page["union"].root(page["stair_node"])
            items.append(dict(type="ENTRY", label=e.get("label"), page=page["view"]["rank"],
                              compCells=int(page["sizes"][comp]), onLargest=bool(on_main),
                              viaStairs=bool(via_stairs), distanceToFloorM=e.get("distanceToFloorM")))
            if pin is None or (e.get("distanceToFloorM") or 99) < (pin[2].get("distanceToFloorM") or 99):
                pin = (page, root, e)
        if pin is None:
            report.append(dict(site=site, kind=vs[0]["kind"], pages=len(pages), items=items, unreachable=[]))
            continue
        pin_page, pin_root, _ = pin
        unreachable = []
        cross_page = 0
        for r in rooms:
            vid, sx, sy, name = r[0], r[1], r[2], r[3]
            idx = index_of_view.get(vid)
            if idx is None:
                continue
            if pages[idx]["view"]["id"] != pin_page["view"]["id"]:
                cross_page += 1
                continue
            col = int(np.floor((hi[0] - float(sx)) / step))
            row = int(np.floor((hi[1] - float(sy)) / step))
            if not (0 <= row < shape[0] and 0 <= col < shape[1]):
                unreachable.append(dict(room=name, page=pages[idx]["view"]["rank"], reason="OUT_OF_BOUNDS"))
                continue
            comp = int(pages[idx]["ids"][row, col])
            if comp == 0:
                unreachable.append(dict(room=name, page=pages[idx]["view"]["rank"], reason="NO_FLOOR_CELL"))
                continue
            if pages[idx]["union"].root(("comp", pages[idx]["view"]["id"], comp)) != pin_root:
                unreachable.append(dict(room=name, page=pages[idx]["view"]["rank"],
                                        reason="DISCONNECTED", compCells=int(pages[idx]["sizes"][comp])))
        report.append(dict(site=site, kind=vs[0]["kind"], pages=len(pages), crossPageRooms=cross_page,
                           unreachable=unreachable, items=items))

    suffix = "-full" if exact else ""
    (CHECKS / f"connectivity-audit{suffix}.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    holes = sum(1 for r in report for i in r["items"] if i["type"] == "ENTRY_HOLE")
    islands = sum(1 for r in report for i in r["items"]
                  if i["type"] == "ENTRY" and not (i["onLargest"] or i["viaStairs"]))
    disconnected = sum(len(r["unreachable"]) for r in report)
    lines = [f"# 地堡入口/房间连通性审计（{'精确三角栅格' if exact else '快速楼梯掩膜'}）", "",
             f"- 地点: {len(report)}；入口空洞: {holes}；入口孤立: {islands}；同层不连通房间标签: {disconnected}", "",
             "| 地点 | 页数 | 入口状态 | 同层不连通房间 |", "|---|---|---|---|"]
    for r in report:
        state = []
        for i in r["items"]:
            if i["type"] == "ENTRY":
                state.append(f"{i['label']}:" + ("主地板" if i["onLargest"] else
                                                 "经楼梯" if i["viaStairs"] else f"孤立{i['compCells']}格"))
            elif i["type"] == "ENTRY_HOLE":
                state.append(f"{i['label']}:空洞")
        if state or r["unreachable"]:
            detail = ", ".join(f"{b['room']}({b['reason']})" for b in r["unreachable"][:8])
            if len(r["unreachable"]) > 8:
                detail += "…"
            lines.append(f"| {r['site']} | {r['pages']} | {'; '.join(state) or '-'} | {detail or '-'} |")
    (CHECKS / f"connectivity-audit{suffix}.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"sites={len(report)} entryHoles={holes} entryIslands={islands} disconnectedRoomLabels={disconnected}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--full", action="store_true", help="exact triangle raster (slower)")
    ap.add_argument("--only", nargs="*", default=None, help="limit to these site ids")
    args = ap.parse_args()
    audit(args.full, set(args.only) if args.only else None)
