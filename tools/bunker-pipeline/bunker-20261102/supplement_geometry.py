"""Explicit architecture selection for the missing complexes; spline meshes already deformed by upstream."""
import sys,pathlib,json,functools,importlib.util
HERE=pathlib.Path(__file__).resolve().parent
sys.dont_write_bytecode=True
sys.path.insert(0,str(HERE.parent/'bunker-extraction-20261031'))
from bunker_geometry import placements,read_json,write_json,structural
from floorplans import transform_vertices
import numpy as np

def selected(mesh, scene):
    p=mesh.lower(); name=p.rsplit('/',1)[-1].split('.')[0]
    if '/naval_base/' in p:
        return name.startswith(('sm_naval_base_main_room','sm_naval_base_tunnel','sm_naval_base_elevator','sm_naval_bottom_entrance','sm_naval_top_entrance'))
    if '/airport_zeljava/' in p:
        return any(x in p for x in ['/tunnels_big/','/tunnels_small/','/rooms/','/platforms/']) and 'spaceprogramtank' not in p
    if scene=='Safe_Zone_D_2':
        return '/tv_bunker/' in p and structural(mesh)
    if scene=='A_4_Military_Base_Underground':
        return 'tool_shed_stairs' in p or 'industrialstairs_stairs' in p
    if scene.startswith('D4_Airport') or scene.startswith('D_4_Airport'):
        return any(x in name for x in ['service_stairs','industrialstairs_stairs','industrialstairs_platform']) or name=='sm_stairs'
    return False

@functools.lru_cache(maxsize=500)
def read_mesh(mesh):
    v=[];f=[]
    with (HERE/'extracted/meshes'/(mesh+'.obj')).open() as stream:
        for line in stream:
            if line.startswith('v '):v.append([float(x) for x in line.split()[1:4]])
            elif line.startswith('f '):
                face=[int(x.split('/')[0])-1 for x in line.split()[1:]]
                assert len(face)==3
                f.append(face)
    v=np.array(v);f=np.array(f,dtype=np.int32)
    assert len(v) and len(f) and f.min()>=0 and f.max()<len(v)
    return v,f

def world_triangles(p):
    v,f=read_mesh(p['mesh'])
    t=transform_vertices(v,p['transform'])[f]
    # Reflection changes winding but not which material side is the walking surface.
    if np.prod(p['transform']['scale'])<0:t=t[:,[0,2,1]]
    return t

def prepare():
    groups={
      'A_4_Naval_Underground':['A_4_Military_Base_Underground','A_4_Military_Base','A_4_Military_Harbour_04'],
      'D_4_Underground_Airbase':['D4_Airport_Zeljava']+['D_4_Airport_Zeljava_Entrance'+x for x in ['', '_01','_02','_03','_04','_05']],
      'D_2_Safe_Zone_Tunnels':['Safe_Zone_D_2']}
    catalogue=[];decisions=[]
    for ident,scenes in groups.items():
        items=[];spline_count=0
        for name in scenes:
            manifest=read_json(HERE/'extracted'/(name+'-mesh-manifest.json'))
            by_component={(m['actor'],m['component']):m for m in manifest}
            scene=read_json(HERE/'extracted'/(name+'.json'))
            for p in placements(scene):
                keep=selected(p['mesh'],name)
                decisions.append(dict(scene=name,actor=p['actor'],component=p['component'],mesh=p['mesh'],include=keep))
                if not keep:continue
                m=by_component[(p['actor'],p['component'])]
                p.update(sourceMesh=p['mesh'],mesh=m['id'],scene=name,spline=m['spline'])
                spline_count+=m['spline'] is not None
                items.append(p)
        # Actors present in both surface and underground packages may share an identical placement.
        unique={json.dumps([p['mesh'],p['transform']],sort_keys=True):p for p in items}
        items=list(unique.values())
        assert items
        catalogue.append(dict(id=ident,type='naval' if ident.startswith('A_4') else 'airbase' if ident.startswith('D_4') else 'shelter',entranceScenes=[],scenes=scenes,placements=len(items),splines=spline_count))
        write_json(HERE/'extracted/output/bunkers'/(ident+'.json'),dict(placements=items))
        print(ident,len(items),'placements',spline_count,'splines',flush=True)
    write_json(HERE/'extracted/output/bunker-catalogue.json',dict(bunkers=catalogue))
    write_json(HERE/'checks/mesh-selection.json',decisions)

if __name__=='__main__':prepare()
