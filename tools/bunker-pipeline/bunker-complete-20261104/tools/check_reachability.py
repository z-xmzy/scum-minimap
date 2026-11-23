"""Audit height-continuous sampled ground between entrances and bounded room labels."""
import collections
import json

import build_views as build
import analyze
from build_views import np


def contacts(left, right, z_left, z_right):
    valid = (left > 0) & (right > 0) & (left != right) & (np.abs(z_left - z_right) <= 60)
    if not valid.any():
        return ()
    pairs = np.stack([left[valid], right[valid]], axis=1)
    return np.unique(pairs, axis=0)


def terrain_groups(grids):
    # Consecutive horizontal cells form runs. Union touching runs within and across views.
    parents = [0]
    layers = []

    def root(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i

    def join(pairs):
        for a, b in pairs:
            a, b = root(a), root(b)
            if a != b:
                parents[b] = a

    for z in grids:
        present = np.isfinite(z)
        starts = present.copy()
        starts[:, 1:] &= ~present[:, :-1] | (np.abs(np.diff(z, axis=1)) > 60)
        ids = np.cumsum(starts, dtype=np.int32).reshape(z.shape) + len(parents) - 1
        ids[~present] = 0
        parents.extend(range(len(parents), len(parents) + int(starts.sum())))
        join(contacts(ids[:-1], ids[1:], z[:-1], z[1:]))
        for other, other_z in zip(layers, grids):
            for dy, dx in ((0,0),(0,1),(0,-1),(1,0),(-1,0)):
                a = (slice(max(dy,0), z.shape[0]+min(dy,0)), slice(max(dx,0), z.shape[1]+min(dx,0)))
                b = (slice(max(-dy,0), z.shape[0]-max(dy,0)), slice(max(-dx,0), z.shape[1]-max(dx,0)))
                join(contacts(ids[a], other[b], z[a], other_z[b]))
        layers.append(ids)
    roots = np.array([root(i) for i in range(len(parents))], dtype=np.int32)
    return [roots[ids] for ids in layers]


def main():
    analyze.FOLDER = build.OUT
    sites = collections.defaultdict(list)
    for view in analyze.load('views.bin'):
        sites[view['bunker']].append(view)
    entries = json.loads((build.ROOT/'checks/entrance-audit.json').read_text(encoding='utf-8'))['entries']
    labels = (build.OUT/'labels-bounded.tsv').read_text(encoding='utf-8').splitlines()
    rooms = [row.split('\t') for row in labels if row.endswith('\troom')]
    results = []
    for site, views in sites.items():
        hi, step, grids = analyze.aligned(views)
        ids = terrain_groups(grids)
        indices = {view['id']: i for i, view in enumerate(views)}

        def cell(x, y):
            return np.floor((hi - [float(x), float(y)]) / step).astype(int)

        pins = []
        for e in entries:
            if e['bunker'] == site:
                x, y = cell(*e['nearestFloor'][:2])
                region = int(ids[indices[e['view']]][y,x])
                assert region > 0, (site,e['label'])
                pins.append(dict(name=e['label'],component=region,modelToSampleM=e['distanceToFloorM']))
        accessible = {p['component'] for p in pins}
        checked = []
        for view, sx, sy, name, x0, y0, x1, y1, *_ in rooms:
            if view not in indices:
                continue
            terrain = ids[indices[view]]
            x, y = cell(sx, sy)
            a, b = cell(x1, y1)
            c, d = cell(x0, y0)
            # Select the nearest floor sample inside the verified room label rectangle.
            a, b = max(a,0), max(b,0)
            ys,xs = np.nonzero(terrain[b:d+1,a:c+1])
            component = 0
            if len(xs):
                k = np.argmin((xs+a-x)**2 + (ys+b-y)**2)
                component = int(terrain[ys[k]+b,xs[k]+a])
            checked.append(dict(name=name,view=view,component=component,reachable=component in accessible))
        results.append(dict(site=site,entrances=pins,rooms=checked))
    report = dict(sites=len(sites),rooms=sum(len(r['rooms']) for r in results),
                  reachedRooms=sum(room['reachable'] for r in results for room in r['rooms']),
                  records=results,limitations=['Surface continuity only. Does not model locks, obstacles, vertical ladders or missing collision meshes.'])
    (build.ROOT/'checks/reachability.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
    for r in results:
        missed=[p['name'] for p in r['rooms'] if not p['reachable']]
        if missed or len({p['component'] for p in r['entrances']})>1 or r['site']=='A_2':
            print(r['site'],len(r['rooms']), 'unreached',','.join(missed),'entry components',[p['component'] for p in r['entrances']])
    print('Reached',report['reachedRooms'],'of',report['rooms'],'room labels across',len(sites),'sites')


if __name__ == '__main__':
    main()

