"""Verify the exported data and document references without launching the game or MiniMap."""
import hashlib
from html.parser import HTMLParser
from pathlib import Path
from bunker_geometry import ROOT,read_json,write_json
from PIL import Image

manifest=read_json(ROOT/'output/scene-manifest.json')
assert len(manifest)==113
for entry in manifest:
    p=ROOT/'output/scenes'/(entry['name']+'.json')
    assert hashlib.sha256(p.read_bytes()).hexdigest()==entry['sha256'],p
    scene=read_json(p)
    assert scene['actorCount']==len(scene['actors'])==entry['actors'],p
    assert scene['package']==entry['package'],p
    assert scene['instanceCount']==sum(len(a.get('instances',[])) for a in scene['actors']),p
meshes=read_json(ROOT/'output/mesh-index.json')
assert len(meshes)==375 and not read_json(ROOT/'output/mesh-failures.json')
for row in meshes:
    p=ROOT/'output/meshes'/(row['id']+'.obj')
    assert p.stat().st_size>100,p
    with p.open() as stream:
        header=stream.readline()+stream.readline()
    assert 'scale 1, UE axes (Z up)' in header,p
catalogue=read_json(ROOT/'output/bunker-catalogue.json')
assert catalogue['counts']=={'abandoned':4,'ordinary':8,'secret':8,'research':3,'ww2':70}
assert len({x['id'] for x in catalogue['bunkers']})==93
for bunker in catalogue['bunkers']:
    obj=read_json(ROOT/'output/bunkers'/(bunker['id']+'.json'))
    assert len(obj['placements'])==bunker['meshPlacements']
    assert not bunker['runtimeReady'], 'Uncalibrated data must not be labelled ready'
coverage=read_json(ROOT/'output/all-poi-coverage.json')
assert len(coverage)==76 and all(x['within100m'] for x in coverage)
floors=read_json(ROOT/'output/floor-candidates/index.json')['bunkers']
assert {x['bunker'] for x in floors}=={x['id'] for x in catalogue['bunkers']}
assert not read_json(ROOT/'output/floor-candidates/missing-slices.json')
count=0
for bunker in floors:
    assert bunker['slices'],bunker['bunker']
    for item in bunker['slices']:
        assert item['officialFloorNumber'] is None
        with Image.open(ROOT/'output/floor-candidates'/item['image']) as im:
            im.verify()
        with Image.open(ROOT/'output/floor-candidates'/item['footprint']) as mask:
            assert mask.mode=='L' and mask.getbbox() is not None
            width,height=mask.size
        assert abs(width-(item['worldXYMax'][0]-item['worldXYMin'][0])/item['cmPerPixel'])<1
        assert abs(height-(item['worldXYMax'][1]-item['worldXYMin'][1])/item['cmPerPixel'])<1
        count+=1
assert count==170

class References(HTMLParser):
    def handle_starttag(self,tag,attrs):
        for key,value in attrs:
            if key in ('src','href'):
                assert (ROOT/'output'/value).is_file(),value
References().feed((ROOT/'output/地堡地图预览.html').read_text(encoding='utf-8'))
installed=ROOT.parent/'adaptive-20261031/app-final/SkynettMiniMap.exe'
assert hashlib.sha256(installed.read_bytes()).hexdigest()=='b390f655a73726d6c16941f7aa16367f03f882597db49e852bc49be4396a2ab3'
result={'passed':True,'scenes':len(manifest),'meshes':len(meshes),'bunkerInstances':93,'coveredExistingPrimaryMarkers':76,
        'heightSlices':count,'existingAdaptive2Unchanged':True,'liveGameCalibration':False,'runtimeIntegration':False}
write_json(ROOT/'output/verification.json',result)
print(result)
