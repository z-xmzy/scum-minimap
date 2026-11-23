"""Catalogue actual bunker instances; retain independent world transforms and provenance."""
import collections
import math
import re
from bunker_geometry import ROOT, read_json, write_json, placements, structural, mesh_id
from floorplans import read_mesh, transform_vertices
import numpy as np


def bounds(items):
    low=np.full(3,np.inf)
    high=np.full(3,-np.inf)
    for item in items:
        v,_=read_mesh(item['mesh'])
        p=transform_vertices(v,item['transform'])
        low=np.minimum(low,p.min(axis=0))
        high=np.maximum(high,p.max(axis=0))
    assert np.isfinite(low).all() and np.isfinite(high).all()
    return {'min':low.tolist(),'max':high.tolist()}


def build():
    scenes={p.stem:read_json(p) for p in (ROOT/'output/scenes').glob('*.json')}
    catalogue=[]
    for name,scene in sorted(scenes.items()):
        if not re.fullmatch(r'TV_Base_[ABCDZ]_[0-4](?:_Abandoned|_Secret|_Research_Facility)?',name):
            continue
        category='research' if name.endswith('Research_Facility') else 'abandoned' if name.endswith('Abandoned') else 'secret' if name.endswith('Secret') else 'ordinary'
        related=[n for n in scenes if n==name or n.startswith(name+'_')]
        entrance_name=name[8:11]+'_TV_Base_'+('Research_Facility_' if category=='research' else '')+'Entrance'
        entrances=[n for n in scenes if n.casefold()==entrance_name.casefold()]
        assert len(entrances)==1, ('Missing or ambiguous entrance', name, entrances)
        items=[dict(item,scene=n) for n in related for item in placements(scenes[n]) if structural(item['mesh'])]
        anchors=[]
        for n in related:
            for actor in scenes[n]['actors']:
                for c in actor['components']:
                    m=re.search(r'Floor-([12])',c['name'])
                    if m and c['class']=='AbandonedBunkerZoneBoxComponent':
                        anchors.append({'component':c['name'],'floor':-int(m[1]),'transform':c['world'],
                                        'source':scenes[n]['package'],'extentVerified':False})
        ident=name.removeprefix('TV_Base_')
        entry={'id':ident,'type':category,'sector':name[8]+name[10], 'scenes':related,'entranceScenes':entrances,
               'boundsCm':bounds(items),'meshPlacements':len(items),'floorAnchors':anchors,
               'floorDefinition':'game-component-names' if anchors else 'not-yet-calibrated',
               'runtimeReady':False}
        write_json(ROOT/'output/bunkers'/(ident+'.json'),{'bunker':entry,'placements':items})
        catalogue.append(entry)
    # WWII blueprints can occur inside airport and main-bunker entrance scenes.
    seen={}
    for name,scene in sorted(scenes.items()):
        if name=='ApexHunt_Arena': continue
        for actor in scene['actors']:
            if '/Models/Buildings/Bunker/' not in actor['class']: continue
            key=(actor['class'],tuple(round(x,1) for x in actor['transform']['location']))
            if key in seen:
                seen[key]['alsoInScenes'].append(name)
                continue
            single={'name':name,'actors':[actor]}
            items=[dict(item,scene=name) for item in placements(single) if '/Models/Buildings/Bunker/' in item['mesh']]
            ident='WW2_'+name+'_'+str(actor['index'])
            entry={'id':ident,'type':'ww2','scenes':[name],'actor':actor['name'],'class':actor['class'],
                   'boundsCm':bounds(items),'meshPlacements':len(items),'floorAnchors':[],
                   'floorDefinition':'not-yet-calibrated','runtimeReady':False,'alsoInScenes':[]}
            seen[key]=entry
            write_json(ROOT/'output/bunkers'/(ident+'.json'),{'bunker':entry,'placements':items})
            catalogue.append(entry)
    # Keep the additional underground military complex distinct from the TV bunkers.
    extra='A_4_Military_Base_Underground'
    all_items=list(placements(scenes[extra]))
    write_json(ROOT/'output/bunkers'/('extra-'+extra+'.json'),{'source':scenes[extra]['package'],'placements':all_items,
               'note':'Complete source placements retained; architectural selection and floor calibration pending.'})
    write_json(ROOT/'output/bunker-catalogue.json',{'format':1,'gameBuild':26320533,'units':'UE centimetres; X/Y world axes, Z up',
               'bunkers':catalogue,'counts':dict(collections.Counter(b['type'] for b in catalogue)),
               'additionalUndergroundComplexes':[extra],
               'limitations':['Not a live-tested MiniMap release.','The complete scene and mesh inventory is not proof of traversability.',
                              'Floor anchors have game names and transforms, but collision extents and player-height offset need verification.',
                              'Source catalogue streaming flags are unusable: persistent-level crosscheck returns zero for all sublevels.']})
    assert sum(b['type']=='research' for b in catalogue)==3
    assert sum(b['type']=='abandoned' for b in catalogue)==4
    assert sum(b['type']=='ordinary' for b in catalogue)==8
    assert sum(b['type']=='secret' for b in catalogue)==8
    coverage=read_json(ROOT/'output/ww2-poi-coverage.json')
    assert len(coverage)==53 and all(x['matchedWithin100m'] for x in coverage)
    print('Bunkers:',len(catalogue),'counts:',dict(collections.Counter(b['type'] for b in catalogue)))
    print('Game floor anchors:',sum(len(b['floorAnchors']) for b in catalogue))


if __name__=='__main__': build()
