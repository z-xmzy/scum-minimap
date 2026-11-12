"""Assemble a private, isolated CPython distribution and the versioned game dataset."""
import hashlib,itertools,sys,shutil,zipfile,struct,zlib
from pathlib import Path
import cv2,numpy as np
ROOT=Path(__file__).resolve().parents[1]
OLD=ROOT.parent/'SCUM-MiniMap-world3-20261112'
TEST=ROOT.parent/'SCUM-MiniMap-world4-test'
DEST=ROOT/'package/SCUM-MiniMap-World4/app/fine'
DATA=DEST/'world';PY=DEST/'runtime'
DATA.mkdir(exist_ok=True);PY.mkdir(exist_ok=True)
sys.path.insert(0,str(OLD/'tools/lib'))
from scipy.spatial import ConvexHull,QhullError
def copy(p,dest):shutil.copyfile(p,dest)
base=Path(sys.base_prefix)
for name in ('python.exe','python3.dll','python314.dll','vcruntime140.dll','vcruntime140_1.dll','LICENSE.txt'):copy(base/name,PY/name)
for p in (base/'DLLs').iterdir():
    if p.suffix in ('.pyd','.dll') and not p.name.startswith(('_test','_tkinter','tcl','tk8')):copy(p,PY/p.name)
with zipfile.ZipFile(PY/'python314.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in (base/'Lib').rglob('*.py'):
        rel=p.relative_to(base/'Lib')
        if set(rel.parts)&{'site-packages','test','tests','idlelib','tkinter','ensurepip','turtledemo','venv'}:continue
        z.write(p,rel.as_posix())
(PY/'python314._pth').write_text('python314.zip\n.\npackages\n..\n',encoding='ascii')
pk=PY/'packages';pk.mkdir(exist_ok=True)
for source,names in [(Path(np.__file__).parents[1],['numpy','numpy.libs','numpy-*.dist-info','cv2','opencv*.dist-info']),(OLD/'tools/lib',['scipy','scipy.libs','scipy-*.dist-info'])]:
    for pat in names:
        for item in source.glob(pat):
            if not item.is_dir():continue
            if item.name.endswith('dist-info'):
                for f in item.rglob('*'):
                    if f.is_file() and any(s in f.as_posix().lower() for s in ('license','copying','notice')):
                        d=pk/f.relative_to(source);d.parent.mkdir(parents=True,exist_ok=True);copy(f,d)
            else:shutil.copytree(item,pk/item.name,dirs_exist_ok=True,ignore=shutil.ignore_patterns('__pycache__','tests','*.pyc','*.pdb'))
for n in ['water.f32','deck-used.f32','roads.f32']:copy(OLD/'bake'/n,DATA/n)
copy(OLD/'bake/height/tmp/work.f32',DATA/'height.f32')
for n in ['instances.npy','radius.npy']:copy(TEST/'bake'/n,DATA/n)
for n in ['nav-world-grid.bin','nav-world-terrain.bin']:copy(ROOT/'source/resources'/n,DATA/n)
# The coarse layers already contain these baked hand-marked corridors.
sys.path.insert(0,str(OLD/'tools'))
from bake import markings
marks,count=markings();(DATA/'handroads.z').write_bytes(zlib.compress(marks.tobytes(),8))
tables={}
for table in (TEST/'bake/models.tsv',TEST/'bake/audit/props.tsv'):
    for row in table.read_text().splitlines():
        i,kind,*_=row.split('\t');tables[int(i)]=kind
seen={};raw=TEST/'bake/audit';ids=np.unique(np.load(DATA/'instances.npy',mmap_mode='r')['id'])
with (DATA/'models.bin').open('wb') as blob,(DATA/'models.tsv').open('w') as idx:
    for i in ids:
        body=(raw/f'{i}.raw').read_bytes();digest=hashlib.sha256(body).digest()
        if digest not in seen:
            pos=0;out=bytearray()
            while pos<len(body):
                kind,n=struct.unpack_from('<ii',body,pos);pos+=8
                v=np.frombuffer(body,'<f4',count=n*3,offset=pos).reshape(n,3);pos+=12*n
                if kind==1:
                    v=np.unique(v,axis=0)
                    try:v=v[ConvexHull(v).simplices].reshape(-1,3)
                    except QhullError:
                        box=np.array(list(itertools.product(*zip(v.min(axis=0)-.01,v.max(axis=0)+.01))),dtype='<f4')
                        v=box[ConvexHull(box).simplices].reshape(-1,3)
                out.extend(struct.pack('<ii',kind,len(v)));out.extend(v.tobytes())
            comp=zlib.compressobj(4,wbits=-15);chunk=comp.compress(out)+comp.flush()
            seen[digest]=blob.tell(),len(chunk);blob.write(chunk)
        offset,size=seen[digest];idx.write(f'{i}\t{tables[i]}\t{offset}\t{size}\n')
print('model IDs',len(ids),'unique bodies',len(seen),'hand roads',count,flush=True)

