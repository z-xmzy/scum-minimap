"""Usage: python check_player_sample.py B_2_Research_Facility '{X=... Y=... Z=...|P=... Y=... R=...}'"""
import re
import sys
from bunker_geometry import ROOT,read_json
from floorplans import scene_triangles,horizontal_surfaces,ray_heights

if len(sys.argv)!=3:
    raise SystemExit(__doc__)
bunker=next((b for b in read_json(ROOT/'output/bunker-catalogue.json')['bunkers'] if b['id']==sys.argv[1]),None)
if bunker is None: raise SystemExit('Unknown bunker id')
m=re.fullmatch(r'\s*\{X=(-?\d+(?:\.\d+)?)\s+Y=(-?\d+(?:\.\d+)?)\s+Z=(-?\d+(?:\.\d+)?)\|P=(-?\d+(?:\.\d+)?)\s+Y=(-?\d+(?:\.\d+)?)\s+R=(-?\d+(?:\.\d+)?)\}\s*',sys.argv[2])
if not m: raise SystemExit('Expected one complete game coordinate report')
x,y,z=map(float,m.group(1,2,3))
if max(abs(x),abs(y),abs(z))>2000000: raise SystemExit('Coordinate outside supported game range')
triangles=scene_triangles(bunker['scenes']+bunker.get('entranceScenes',[]))
up,_=horizontal_surfaces(triangles)
heights=ray_heights(up,x,y)
below=heights[heights<=z+5]
print('Bunker:',bunker['id'])
print('Upward surfaces at this XY (cm):',heights.tolist())
if not len(below): raise SystemExit('No extracted ground surface beneath this position; do not calibrate automatically')
floor=float(below[-1])
print('Nearest lower surface Z (cm):',floor)
print('Player Z minus surface Z (cm):',round(z-floor,2))
print('Check this is a standing sample on a clear floor, not furniture or a moving lift.')
