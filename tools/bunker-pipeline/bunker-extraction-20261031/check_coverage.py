import csv
import math
from bunker_geometry import ROOT, read_json, write_json

buildings=[]
for path in (ROOT/'output/scenes').glob('*.json'):
    if path.stem == 'ApexHunt_Arena':
        continue
    scene=read_json(path)
    for actor in scene['actors']:
        if '/Buildings/Bunker/' in actor['class'] or actor['name'].startswith('BP_Bunker_'):
            buildings.append({'scene':path.stem,'actor':actor['name'],'location':actor['transform']['location']})
rows=[]
with (ROOT.parent/'bunker-research-20261031/existing-bunker-markers.csv').open(encoding='utf-8-sig',newline='') as stream:
    for marker in csv.DictReader(stream):
        if marker['category'] != 'WW2 bunkers':
            continue
        x=617718-float(marker['map_u'])*1521618
        y=618618-float(marker['map_v'])*1523618
        nearest=min(buildings,key=lambda b: math.hypot(b['location'][0]-x,b['location'][1]-y))
        distance=math.hypot(nearest['location'][0]-x,nearest['location'][1]-y)/100
        rows.append({'marker':marker['marker_id'],'sector':marker['sector'],'world':[x,y],'nearest':nearest,'distanceM':round(distance,2),'matchedWithin100m':distance<100})
write_json(ROOT/'output/ww2-poi-coverage.json',rows)
write_json(ROOT/'output/ww2-building-actors.json',buildings)
print('WW2 building actors:',len(buildings),'POIs:',len(rows),'matched <100m:',sum(x['matchedWithin100m'] for x in rows))
for row in rows:
    if not row['matchedWithin100m']:
        print(row['sector'],row['marker'],row['world'],row['distanceM'],row['nearest']['scene'])
