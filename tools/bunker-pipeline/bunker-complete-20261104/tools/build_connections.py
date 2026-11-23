"""Expose actual touching display surfaces across pages; never bridge blank space."""
import collections,json
import build_views as b,analyze
from build_views import np

def components(mask):
    h,w=mask.shape;flat=mask.ravel();seen=np.zeros(len(flat),bool);result=[]
    for start in np.flatnonzero(flat):
        if seen[start]:continue
        seen[start]=True;todo=[int(start)];cells=[]
        while todo:
            i=todo.pop();cells.append(i);y,x=divmod(i,w)
            for n in (i-w if y else -1,i+w if y+1<h else -1,i-1 if x else -1,i+1 if x+1<w else -1):
                if n>=0 and flat[n] and not seen[n]:seen[n]=True;todo.append(n)
        result.append(np.array(cells,dtype=int))
    return result

def main():
    analyze.FOLDER=b.OUT;views=analyze.load('views.bin');groups=collections.defaultdict(list)
    titles=dict(line.split('\t') for line in (b.OUT/'views.tsv').read_text(encoding='utf-8').splitlines())
    for v in views:groups[v['bunker']].append(v)
    labels=[];report=[]
    for ident,vs in groups.items():
        hi,step,gs=b.aligned(vs);joins=[];mask=np.any(np.isfinite(gs),axis=0)
        comps=components(mask);significant=[c for c in comps if len(c)*step**2>=200000]
        for i,a in enumerate(gs):
            for j in range(i+1,len(gs)):
                other=gs[j];contact=np.zeros(a.shape,bool)
                for dy,dx in ((0,0),(0,1),(0,-1),(1,0),(-1,0)):
                    shifted=np.roll(other,(dy,dx),(0,1));touch=np.isfinite(a)&np.isfinite(shifted)&(np.abs(a-shifted)<=60)
                    if dy==1:touch[0]=False
                    elif dy==-1:touch[-1]=False
                    elif dx==1:touch[:,0]=False
                    elif dx==-1:touch[:,-1]=False
                    contact|=touch
                # Collapse nearby seams into a readable marker; marker stays on a real cell.
                for patch in components(b.dilate(contact,2)):
                    cells=patch[contact.ravel()[patch]]
                    if len(cells)<3:continue
                    yy=cells//a.shape[1];xx=cells%a.shape[1];k=int(np.argmin((xx-xx.mean())**2+(yy-yy.mean())**2));x=float(hi[0]-(xx[k]+.5)*step);y=float(hi[1]-(yy[k]+.5)*step)
                    # A masked floor edge can sit just outside the other page's crop.
                    if not all(v['x0']<=x<v['x1'] and v['y0']<=y<v['y1'] for v in (vs[i],vs[j])):continue
                    number=len(joins)+1
                    for source,target in ((i,j),(j,i)):labels.append((vs[source]['id'],x,y,'衔接 '+str(number)+' → '+titles[vs[target]['id']]))
                    joins.append(dict(views=[vs[i]['id'],vs[j]['id']],world=[x,y],matchingCells=len(cells),maxNeighbourHeightDifferenceCm=60))
        report.append(dict(bunker=ident,views=len(vs),crossViewConnections=joins,combinedComponentsOver20m2=len(significant),combinedComponentAreasM2=sorted([round(len(c)*step**2/10000,2) for c in significant],reverse=True)))
    (b.OUT/'connections.tsv').write_text(''.join('\t'.join(map(str,row))+'\n' for row in labels),encoding='utf-8')
    (b.ROOT/'checks/connection-audit.json').write_text(json.dumps(dict(records=report,labels=len(labels),limitations=['Connections show continuous sampled surfaces across display pages, not door-lock state or a navigable route.','Disconnected components are retained for review rather than filled with invented corridors.']),ensure_ascii=False,indent=2),encoding='utf-8')
    print('connection labels',len(labels))
    for r in report:
        if r['bunker']=='A_2' or r['combinedComponentsOver20m2']>1:print(r['bunker'],r['combinedComponentAreasM2'],len(r['crossViewConnections']))
if __name__=='__main__':main()
