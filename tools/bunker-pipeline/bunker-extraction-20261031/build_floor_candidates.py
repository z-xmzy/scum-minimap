"""Build reviewable height slices, not an unverified runtime auto-floor configuration."""
import collections
import hashlib
from bunker_geometry import ROOT, read_json, write_json
from floorplans import read_mesh, transform_vertices, cut_segments,transform_triangles
import numpy as np
from PIL import Image,ImageDraw,ImageChops


def candidates(triangles):
    cross=np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0])
    area=np.linalg.norm(cross,axis=1)/2
    up=(cross[:,2]>.97*area*2)&(area>1)
    down=(cross[:,2]<-.97*area*2)&(area>1)
    tops=triangles[up]
    weights=area[up]
    elevations=tops[:,:,2].mean(axis=1)
    bins=np.round(elevations/25).astype(int)
    totals=collections.defaultdict(float)
    for b,a in zip(bins,weights): totals[int(b)]+=float(a)
    if not totals: return [],tops,triangles[down]
    # Keep landing and mezzanine candidates visible for review, without calling them official floors.
    minimum=max(40000,max(totals.values())*.035)
    chosen=[]
    for b,total in sorted(totals.items(),key=lambda p:-p[1]):
        if total<minimum or any(abs(b*25-z)<125 for z in chosen): continue
        near=np.abs(elevations-b*25)<40
        chosen.append(float(np.average(elevations[near],weights=weights[near])))
    return sorted(chosen),tops,triangles[down]


def build():
    catalogue=read_json(ROOT/'output/bunker-catalogue.json')['bunkers']
    folder=ROOT/'output/floor-candidates'
    folder.mkdir(parents=True,exist_ok=True)
    result=[]
    for index,b in enumerate(catalogue,1):
        items=read_json(ROOT/'output/bunkers'/(b['id']+'.json'))['placements']
        t=np.concatenate([transform_triangles(*read_mesh(p['mesh']),p['transform']) for p in items])
        levels,up,down=candidates(t)
        lo=t[:,:,:2].min(axis=(0,1))-100
        hi=t[:,:,:2].max(axis=(0,1))+100
        cm_per_pixel=max(10,float(max(hi-lo))/1400)
        size=np.ceil((hi-lo)/cm_per_pixel).astype(int)
        def raster(tris):
            mask=Image.new('L',tuple(size),0)
            draw=ImageDraw.Draw(mask)
            for tri in tris:
                draw.polygon([tuple(p) for p in (hi-tri[:,:2])/cm_per_pixel],fill=255)
            return mask
        slices=[]
        for height in levels:
            floor=raster(up[np.abs(up[:,:,2].mean(axis=1)-height)<60])
            roof_z=down[:,:,2].mean(axis=1)
            ceiling=raster(down[(roof_z>height+140)&(roof_z<height+3000)])
            indoor=ImageChops.multiply(floor,ceiling)
            indoor_area=indoor.histogram()[255]*cm_per_pixel**2/10000
            if indoor_area<3: continue
            slug=hashlib.sha256(b['id'].encode()).hexdigest()[:12]+'-'+str(round(height))
            image=Image.new('RGBA',tuple(size),(14,22,27,255))
            image.paste((44,72,79,255),(0,0),indoor)
            draw=ImageDraw.Draw(image)
            segments=cut_segments(t,height+150)
            for segment in segments:
                draw.line([tuple(p) for p in (hi-segment)/cm_per_pixel],fill=(183,215,217,255),width=1)
            image.save(folder/(slug+'.png'))
            indoor.save(folder/(slug+'-footprint.png'))
            slices.append({'id':slug,'surfaceZCm':round(height,2),'indoorProjectionAreaM2':round(indoor_area,2),
                           'image':slug+'.png','footprint':slug+'-footprint.png',
                           'worldXYMin':lo.tolist(),'worldXYMax':hi.tolist(),'cmPerPixel':cm_per_pixel,
                           'officialFloorNumber':None,'calibrationStatus':'geometry-candidate-awaiting-player-Z-check'})
        result.append({'bunker':b['id'],'type':b['type'],'slices':slices,'gameFloorNumbers':sorted(set(a['floor'] for a in b['floorAnchors']))})
        if index%10==0 or index==len(catalogue): print(f'{index}/{len(catalogue)} bunkers; {sum(len(r["slices"]) for r in result)} height slices',flush=True)
        del t
    write_json(folder/'index.json',{'gameBuild':26320533,'status':'offline review candidates; not installed into MiniMap','bunkers':result})
    missing=[r['bunker'] for r in result if not r['slices']]
    write_json(folder/'missing-slices.json',missing)
    print('No slice generated for:',missing,flush=True)


if __name__=='__main__': build()
