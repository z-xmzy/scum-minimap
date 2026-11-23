"""Read-only SCUM scene/mesh extraction. All work products stay beside this file."""
import collections
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'tools/python-libs'))
PAKS = Path(r'D:\Stea\steamapps\common\SCUM\SCUM\Content\Paks')
CLI = ROOT / 'tools/scumstudio-3.6.0/scumstudio.exe'


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def environment():
    env = os.environ.copy()
    env.update(TEMP=str(ROOT/'temp'), TMP=str(ROOT/'temp'),
               SCUMSTUDIO_HOME=str(ROOT/'tool-home'),
               DOTNET_BUNDLE_EXTRACT_BASE_DIR=str(ROOT/'temp/dotnet-bundle'),
               SCUMSTUDIO_AES_KEY=(ROOT/'temp/scum-pak-key.txt').read_text().strip())
    return env


def placements(scene):
    """Components and expanded instances, never the representative actor mesh twice."""
    for actor in scene['actors']:
        for component in actor['components']:
            mesh = component.get('mesh')
            if not mesh or not component.get('world') or component.get('visible') is False:
                continue
            if 'instanceCount' in component:
                instances = [i for i in actor.get('instances', []) if i['component'] == component['name']]
                assert len(instances) == component['instanceCount'], (scene['name'], actor['name'])
                transforms = [i['world'] for i in instances]
            else:
                transforms = [component['world']]
            for transform in transforms:
                yield {'mesh':mesh, 'transform':transform, 'actor':actor['name'], 'component':component['name']}


def structural(mesh):
    """Only architectural geometry; the complete unfiltered scenes are retained separately."""
    p = mesh.lower()
    name = p.rsplit('/', 1)[-1].split('.')[0]
    if '/buildings/bunker/' in p:
        return True
    if '/tv_bunker/' in p:
        if any(x in p for x in ['/skyblockers/', '/es/', '/props/', '/debris/', '/tunnel_objects/']):
            return '/doors/' in p
        if re.search(r'_(es|wm|sb)$', name) or any(x in name for x in ['environmentdescription', 'skyblock', 'lamp', 'light', 'cable', 'wiring', 'pipe', 'display', 'monitor', 'laser', 'purge_panel', 'keycard', 'locking_mechanism']):
            return False
        return True
    return any(x in name for x in ['industrialstairs_platform', 'industrialstairs_stairs', 'concretefloorblock'])


def mesh_id(mesh):
    return hashlib.sha256(mesh.encode()).hexdigest()[:16]


def inventory():
    selected = read_json(ROOT/'output/selected-levels.json')
    expected = {x['name'] for x in selected}
    scenes = {p.stem:p for p in (ROOT/'output/scenes').glob('*.json')}
    assert expected <= scenes.keys(), 'Scene extraction is incomplete'
    used = collections.Counter()
    rows = []
    for name in sorted(expected):
        scene = read_json(scenes[name])
        items = [p for p in placements(scene) if structural(p['mesh'])]
        used.update(p['mesh'] for p in items)
        rows.append({'name':name, 'actors':scene['actorCount'], 'structuralPlacements':len(items), 'warnings':scene['warnings']})
    write_json(ROOT/'output/structural-inventory.json', rows)
    meshes = [{'id':mesh_id(m), 'mesh':m, 'placements':n} for m,n in sorted(used.items())]
    write_json(ROOT/'output/mesh-index.json', meshes)
    print(f'{len(rows)} scenes; {len(meshes)} unique structural meshes; {sum(used.values())} placements', flush=True)


def export_meshes():
    rows = read_json(ROOT/'output/mesh-index.json')
    out = ROOT/'output/meshes'
    out.mkdir(parents=True, exist_ok=True)
    env = environment()
    failures = []
    for n,row in enumerate(rows, 1):
        target = out/(row['id']+'.obj')
        if not target.exists():
            log = ROOT/'logs'/('mesh-'+row['id']+'.log')
            with log.open('w', encoding='utf-8') as stream:
                result = subprocess.run([str(CLI),'asset','mesh-export',str(PAKS),row['mesh'],'-o',str(target),'--scale','1','--ue-axes'], env=env, stdout=stream, stderr=stream)
            if result.returncode or not target.exists():
                failures.append(row)
        if n % 20 == 0 or n == len(rows):
            print(f'meshes {n}/{len(rows)}; failed {len(failures)}', flush=True)
    write_json(ROOT/'output/mesh-failures.json', failures)
    if failures:
        raise RuntimeError(f'{len(failures)} mesh exports failed; see mesh-failures.json')


if __name__ == '__main__':
    {'inventory':inventory, 'export':export_meshes}[sys.argv[1]]()
