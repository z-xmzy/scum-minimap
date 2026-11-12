from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED,ZIP_STORED
from hashlib import sha256
import sys
R=Path(__file__).resolve().parents[1]
root=R/'package/SCUM-MiniMap-World4';out=R.parent/'SCUM-MiniMap-2.4.3-world4-20261112.zip'
def allowed(p):
    parts=p.relative_to(root).parts
    if '__pycache__' in parts or p.suffix=='.pyc':return False
    if parts[:2]==('app','data'):
        return parts[2] in ('field-console-fonts','maps','voice-navigation','settings.ini','user-roads.bin')
    return True
assert (root/'app/fine/world/models.bin').stat().st_size>0
assert (root/'app/fine/runtime/python314._pth').is_file()
assert (root/'app/data/settings.ini').read_text(encoding='utf-8-sig').strip()=='Language=zh-CN'
part=out.with_suffix('.part')
with ZipFile(part,'w',ZIP_DEFLATED,compresslevel=4) as z:
    for p in sorted(root.rglob('*')):
        if p.is_file() and allowed(p):
            z.write(p,root.name+'/'+p.relative_to(root).as_posix(),compress_type=ZIP_STORED if p.name=='models.bin' else ZIP_DEFLATED)
with ZipFile(part) as z:
    assert z.testzip() is None
    target=R/'extracted'
    if target.exists():raise RuntimeError('use a fresh extracted directory for release validation')
    z.extractall(target);print(len(z.infolist()),'entries',flush=True)
print('CRC verified; release candidate',part,'sha256',sha256(part.read_bytes()).hexdigest(),flush=True)

