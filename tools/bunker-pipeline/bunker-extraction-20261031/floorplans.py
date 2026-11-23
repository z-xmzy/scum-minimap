"""Geometry-derived floor-plan candidates; game coordinates are centimetres, Z up."""
import functools
import math
import sys
from bunker_geometry import ROOT, read_json, write_json, placements, structural, mesh_id
import numpy as np


def transform_vertices(vertices, transform):
    pitch,yaw,roll=np.radians(transform['rotation'])/2
    sp,cp=math.sin(pitch),math.cos(pitch)
    sy,cy=math.sin(yaw),math.cos(yaw)
    sr,cr=math.sin(roll),math.cos(roll)
    x,y,z,w=cr*sp*sy-sr*cp*cy,-cr*sp*cy-sr*cp*sy,cr*cp*sy-sr*sp*cy,cr*cp*cy+sr*sp*sy
    matrix=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                     [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                     [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    return (vertices*np.array(transform['scale']))@matrix.T+transform['location']


def transform_triangles(vertices, faces, transform):
    """World triangles with winding preserved. A mirrored placement (negative scale
    determinant) reverses winding, which would turn floors into ceilings."""
    sx,sy,sz=transform['scale']
    if sx*sy*sz<0: faces=faces[:,::-1]
    return transform_vertices(vertices,transform)[faces]


def cut_segments(triangles, height):
    t=triangles[(triangles[:,:,2].min(axis=1)<height)&(triangles[:,:,2].max(axis=1)>height)]
    if not len(t): return np.empty((0,2,2))
    points=np.empty((len(t),2,2))
    counts=np.zeros(len(t),dtype=int)
    for a,b in [(0,1),(1,2),(2,0)]:
        crossed=(t[:,a,2]<height)!=(t[:,b,2]<height)
        ids=np.flatnonzero(crossed)
        ratio=(height-t[ids,a,2])/(t[ids,b,2]-t[ids,a,2])
        points[ids,counts[ids]]=t[ids,a,:2]+ratio[:,None]*(t[ids,b,:2]-t[ids,a,:2])
        counts[ids]+=1
    result=points[counts==2]
    return result[np.linalg.norm(result[:,0]-result[:,1],axis=1)>.1]


def ray_heights(triangles, x, y):
    a,b,c=triangles[:,0],triangles[:,1],triangles[:,2]
    v,w=b-a,c-a
    denominator=v[:,0]*w[:,1]-v[:,1]*w[:,0]
    usable=np.abs(denominator)>1e-6
    a,v,w,denominator=a[usable],v[usable],w[usable],denominator[usable]
    dx,dy=x-a[:,0],y-a[:,1]
    s=(dx*w[:,1]-dy*w[:,0])/denominator
    t=(v[:,0]*dy-v[:,1]*dx)/denominator
    inside=(s>=-1e-6)&(t>=-1e-6)&(s+t<=1+1e-6)
    return np.unique(np.round((a[:,2]+s*v[:,2]+t*w[:,2])[inside],2))


@functools.lru_cache(maxsize=500)
def read_mesh(mesh):
    vertices=[]
    faces=[]
    with (ROOT/'output/meshes'/(mesh_id(mesh)+'.obj')).open() as f:
        for line in f:
            if line.startswith('v '): vertices.append([float(x) for x in line.split()[1:4]])
            elif line.startswith('f '):
                indices=[int(x.split('/')[0])-1 for x in line.split()[1:]]
                assert len(indices)==3, 'Exporter promised triangles'
                faces.append(indices)
    v=np.array(vertices)
    faces=np.array(faces,dtype=np.int32)
    assert len(v)>0 and len(faces)>0 and faces.min()>=0 and faces.max()<len(v)
    return v,faces


def scene_triangles(names):
    chunks=[]
    missing=set()
    for name in names:
        scene=read_json(ROOT/'output/scenes'/(name+'.json'))
        for item in placements(scene):
            mesh=item['mesh']
            if not structural(mesh): continue
            try: vertices,faces=read_mesh(mesh)
            except FileNotFoundError:
                missing.add(mesh)
                continue
            chunks.append(transform_triangles(vertices,faces,item['transform']))
    if missing: raise RuntimeError(f'{len(missing)} missing mesh exports: {sorted(missing)[:2]}')
    return np.concatenate(chunks) if chunks else np.empty((0,3,3))


def horizontal_surfaces(triangles):
    cross=np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0])
    # OBJ output preserves UE axes and reverses winding into right-handed normals.
    areas=np.linalg.norm(cross,axis=1)/2
    upward=(cross[:,2]>.97*areas*2)&(areas>1)
    return triangles[upward],areas[upward]


def inspect(names):
    t=scene_triangles(names)
    flat,areas=horizontal_surfaces(t)
    z=flat[:,:,2].mean(axis=1)
    bins=np.round(z/25).astype(int)
    unique=np.unique(bins)
    rows=sorted([{'z':int(k*25),'areaM2':round(float(areas[bins==k].sum()/10000),2)} for k in unique],key=lambda x:-x['areaM2'])
    print('Triangles',len(t),'bounds',t.min(axis=(0,1)),t.max(axis=(0,1)))
    print(rows[:35])
    write_json(ROOT/'output'/('height-surfaces-'+names[0]+'.json'),rows)


def preview(names, height, path):
    from PIL import Image, ImageDraw, ImageFont
    t=scene_triangles(names)
    flat,areas=horizontal_surfaces(t)
    floor=flat[np.abs(flat[:,:,2].mean(axis=1)-height)<90]
    walls=cut_segments(t,height+150)
    bounds=t[:,:,:2].reshape(-1,2)
    lo,hi=bounds.min(axis=0),bounds.max(axis=0)
    # Match MiniMap orientation: decreasing UE X goes right, decreasing UE Y goes down.
    scale=1400/max(hi-lo)
    size=np.ceil((hi-lo)*scale).astype(int)+100
    img=Image.new('RGB',(int(size[0]),int(size[1])+70),(14,22,27))
    draw=ImageDraw.Draw(img)
    def screen(points): return (hi-points)*scale+50
    for tri in floor:
        draw.polygon([tuple(p) for p in screen(tri[:,:2])],fill=(44,72,79))
    for seg in walls:
        draw.line([tuple(p) for p in screen(seg)],fill=(183,215,217),width=1)
    font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',20)
    draw.text((20,int(size[1])+5),f'{names[0]} | UE Z={height/100:.2f}m',font=font,fill=(231,236,239))
    draw.text((20,int(size[1])+32),'GEOMETRY SLICE - floor labels and entrance detection not calibrated',font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',15),fill=(238,190,99))
    path.parent.mkdir(parents=True,exist_ok=True)
    img.save(path)
    print(str(path),len(floor),'floor triangles',len(walls),'wall segments')


if __name__=='__main__': inspect(sys.argv[1:])
