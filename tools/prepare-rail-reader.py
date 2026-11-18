from pathlib import Path
import shutil,numpy as np
from scipy import ndimage
src=Path('tools/bridge-underroads');dst=Path('tools/bridge-rail-approaches');dst.mkdir(exist_ok=True);(dst/'tmp').mkdir(exist_ok=True)
for f in ['open-game.ps1','collect.ps1']:shutil.copyfile(src/f,dst/f)
mask=np.fromfile(src/'near.bin','B').reshape(3804,3804)
ndimage.binary_dilation(mask,iterations=16).astype('B').tofile(dst/'near.bin')
s=(src/'Collect.cs').read_text()
start=s.index('return (p.Contains("/roads/")');end=s.index(';}',start)
s=s[:start]+'return p.EndsWith(".railroad_01")'+s[end:]
(dst/'Collect.cs').write_text(s)
