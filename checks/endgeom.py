exec(open('checks/handdiag.py').read().split('for c in rows:')[0])
r=np.memmap('package/SCUM-MiniMap/app/fine/world/roads.f32','f','r',shape=ground.shape)
for group in (471,1,863,1585,426):
 ids=np.flatnonzero(gr==group-1);p=xy[ids];axes=np.linalg.eigh(np.cov(p.T))[1];direction=axes[:,-1];t=p@direction
 print('group',group,'len',int(np.ptp(t)))
 for s in [-1,1]:
  subset=ids[t*s>max(t*s)-4];center=xy[subset].mean(0);i=subset[np.argmin(np.linalg.norm(xy[subset]-center,axis=1))]
  print('head',np.round(center),round(z[i]),end=' ')
  for d in [0,4,8,12,20]:
   x,y=((center+direction*s*d)/4).astype(int)
   print((int(ground[y,x]-z[i]),round(r[y,x]-z[i]) if np.isfinite(r[y,x]) else '-'),end=' ')
  print()