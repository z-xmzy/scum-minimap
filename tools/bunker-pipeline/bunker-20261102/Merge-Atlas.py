"""Append the supplemental format-3 records; preserve all original records and PNGs byte for byte."""
import gzip,struct,shutil,hashlib,json,pathlib
R=pathlib.Path(__file__).resolve().parent
original=R.parent/'bunker-20261101/assets-v2/bunkers'
supplement=R/'supplement/bunkers'
out=R/'assets/bunkers';out.mkdir(parents=True,exist_ok=True)
parts=[];layers=[];ids=set();manifest=[]
for folder in (original,supplement):
    data=gzip.decompress((folder/'atlas.bin').read_bytes())
    magic,version,build,count=struct.unpack('<4siii',data[:16]);assert (magic,version,build)==(b'SBMA',3,26320533)
    index=json.loads((folder/'index.json').read_text(encoding='utf-8-sig'));assert count==len(index['layers'])
    parts.append(data[16:])
    for layer in index['layers']:
        assert layer['id'] not in ids
        ids.add(layer['id']);layers.append(layer)
        shutil.copy2(folder/layer['image'],out/layer['image'])
        manifest.append(dict(image=layer['image'],sha256=hashlib.sha256((out/layer['image']).read_bytes()).hexdigest(),source=str(folder)))
with gzip.open(out/'atlas.bin','wb',compresslevel=6) as f:
    f.write(struct.pack('<4siii',b'SBMA',3,26320533,len(layers)))
    for part in parts:f.write(part)
index=dict(format=3,gameBuild=26320533,bunkers=len({l['bunker'] for l in layers}),floorLabels='geometry height layers; not official floor numbers',layers=layers)
(out/'index.json').write_text(json.dumps(index,ensure_ascii=False,indent=2),encoding='utf-8')
assert gzip.decompress((out/'atlas.bin').read_bytes())[16:16+len(parts[0])]==parts[0]
(R/'checks/merged-assets.json').write_text(json.dumps(dict(originalRecordsUnchanged=True,originalLayerCount=len(json.loads((original/'index.json').read_text())['layers']),layers=len(layers),locations=index['bunkers'],images=manifest),indent=2))
print('Merged',index['bunkers'],'locations,',len(layers),'layers; original records unchanged')
