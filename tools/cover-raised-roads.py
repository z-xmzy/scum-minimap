from pathlib import Path
import numpy as np
from scipy import ndimage
p=Path('tools/bridge-underroads/near.bin');near=np.fromfile(p,'B').reshape(3804,3804)
base=Path('package/SCUM-MiniMap/app/fine/world')
road=np.memmap(base/'roads.f32','f','r',shape=near.shape)
ground=np.memmap('../SCUM-MiniMap-world3-20261112/bake/ground.f32','f','r',shape=near.shape)
raised=np.isfinite(road)&(road>ground+150)
near|=ndimage.binary_dilation(raised,iterations=7).astype('B')
near.tofile(p)
print('elevated road cells',int(raised.sum()),'scanned neighbourhood',int(near.sum()))
