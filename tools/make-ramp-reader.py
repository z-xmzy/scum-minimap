from pathlib import Path
import shutil,numpy as np
from scipy import ndimage
src=Path('tools/bridge-underroads');dst=Path('tools/bridge-ramps');dst.mkdir(exist_ok=True);(dst/'tmp').mkdir(exist_ok=True)
for f in ('collect.ps1','open-game.ps1'):shutil.copyfile(src/f,dst/f)
guard=np.fromfile(src/'near.bin','B').reshape(3804,3804);ndimage.binary_dilation(guard,iterations=18).astype('B').tofile(dst/'near.bin')
s=(src/'Collect.cs').read_text();a=s.index('return (p.Contains("/roads/")');b=s.index(';}',a)
s=s[:a]+'''return !p.Contains("_wm.") && !p.Contains("wall") && !p.Contains("rvt") && (p.Contains("ramp") || p.EndsWith(".sm_track") || (p.Contains("motocross_track/") && (p.EndsWith(".platform_01")||p.EndsWith(".platform_01_end")||p.EndsWith(".platform_02"))))'''+s[b:]
(dst/'Collect.cs').write_text(s)
