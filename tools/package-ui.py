from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED,ZIP_STORED
import hashlib
r=Path(__file__).resolve().parents[1]
root=r/'package/SCUM-MiniMap'
part=r/'SCUM-MiniMap-2.4.7-ui.part'
with ZipFile(part,'w',ZIP_DEFLATED,compresslevel=4) as z:
    for p in sorted(root.rglob('*')):
        if not p.is_file():continue
        v=p.relative_to(root)
        if '__pycache__' in v.parts or p.suffix=='.pyc':continue
        if v.parts[:2]==('app','data') and v.parts[2] not in ('field-console-fonts','voice-navigation','settings.ini','user-roads.bin','maps'):continue
        z.write(p,root.name+'/'+v.as_posix(),compress_type=ZIP_STORED if p.name=='models.bin' else ZIP_DEFLATED)
with ZipFile(part) as z:
    assert z.testzip() is None
    assert 'SCUM-MiniMap/启动 SCUM 地图.cmd' in z.namelist()
    z.extractall(r/'verified')
print(part.stat().st_size,len(z.infolist()),hashlib.file_digest(part.open('rb'),'sha256').hexdigest())

