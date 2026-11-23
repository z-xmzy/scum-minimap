"""Regression and whole-catalogue display coverage checks, not a live-game claim."""
import collections,json
import build_views as b
from build_views import np
import analyze

def main():
    # A single flat platform cannot disappear just because semantic seeds disagree.
    grid=np.ones((12,15),dtype='f4')*100;chosen=np.zeros(grid.shape,dtype=int);chosen[:,9:]=1
    fixed,changes,_=b.coherent_planes(grid,chosen);assert (fixed==0).all() and changes==72
    grid=np.repeat(np.arange(12,dtype='f4')[:,None]*20,15,axis=1)
    chosen=np.repeat((np.arange(12)%2)[:,None],15,axis=1)
    fixed,changes,_=b.coherent_planes(grid,chosen);assert np.array_equal(fixed,chosen),'Stairs were flattened'
    # Small overlapping rooms must not be hidden under a large floor.
    large=np.ones((100,100),dtype='f4')*1000;small=np.full_like(large,np.nan);small[:20,:20]=500
    assignment,count=b.grouping([{'z':1000},{'z':500}],[large,small],20)
    assert count==2 and assignment[0]!=assignment[1]
    analyze.FOLDER=b.OUT;views=analyze.load('views.bin');analyze.FOLDER=b.SOURCE;raw=analyze.load()
    d1=next(v for v in views if v['bunker']=='D_1_Abandoned' and v['rank']==1)
    for x,y in [(-542697.377210787,550373.6658348661),(-543504.6894637515,550131.4721589768),(-544473.4641673089,549727.8160324945)]:
        col=int((d1['x1']-x)/d1['step']);row=int((d1['y1']-y)/d1['step'])
        assert abs(float(d1['grid'][row,col])-79060)<3,'D1 reported platform hole regressed'
    groups=collections.defaultdict(list)
    for r in raw:groups[r['bunker']].append(r)
    report=[]
    for ident,rs in groups.items():
        hi,step,gs=b.aligned(rs);b.clean(ident,gs,hi,step)
        expected=np.any(np.isfinite(gs),axis=0);yy,xx=np.where(expected)
        wx=hi[0]-(xx+.5)*step;wy=hi[1]-(yy+.5)*step;present=np.zeros(len(xx),bool)
        local=[v for v in views if v['bunker']==ident]
        for v in local:
            cx=np.floor((v['x1']-wx)/v['step']).astype(int);cy=np.floor((v['y1']-wy)/v['step']).astype(int)
            inside=(cx>=0)&(cx<v['w'])&(cy>=0)&(cy<v['h']);hit=np.flatnonzero(inside)
            present[hit]|=np.isfinite(v['grid'][cy[hit],cx[hit]])
        missing=int((~present).sum());assert missing==0,(ident,missing)
        report.append(dict(bunker=ident,checkedCells=int(expected.sum()),missingDisplayCells=missing,views=len(local)))
    entrances=json.loads((b.ROOT/'checks/entrance-audit.json').read_text(encoding='utf-8'))
    assert entrances['coveredSites']==96 and len(entrances['entries'])==115
    ordinary=[e for e in entrances['entries'] if e['kind']=='ordinary']
    assert len(ordinary)==16 and len({e['bunker'] for e in ordinary})==8
    assert all(e['actorClass'] in ('BP_Bunker_Entrance_01','BP_Bunker_Entrance_01_A') for e in ordinary if e['bunker']=='C_1')
    for e in entrances['entries']:
        v=next(v for v in views if v['id']==e['view']);x,y,z=e['world']
        assert v['x0']<=x<v['x1'] and v['y0']<=y<v['y1']
    # Each actual killbox actor has one owner: the view reached through its entrance.
    build=json.loads((b.ROOT/'checks/view-build-report.json').read_text(encoding='utf-8'))
    style=json.loads((b.ROOT/'checks/room-style-audit.json').read_text(encoding='utf-8'))
    labels=[l for l in style['labelChecks'] if l['text']=='杀戮房'];assert len(labels)==12
    import decorate_views as decoration
    for record in build['records']:
        for owner in record['roomEntranceOwnership']:
            key=owner['scene']+'/'+owner['actor'];matches=[l for l in labels if l['source']==key]
            assert len(matches)==1 and matches[0]['view']==owner['view'],key
            actor=next(a for a in decoration.scene(owner['scene'])['actors'] if a['name']==owner['actor'])
            for v in views:
                if v['bunker']!=record['bunker']:continue
                mask=decoration.room_mask(actor,v)
                if not mask.any():continue
                assert v['id']==owner['view'],(key,'room geometry on wrong view',v['rank'])
                heights=v['grid'][mask];z=owner['entrance'][2]
                assert ((heights>=z-145)&(heights<=z+105)).all(),(key,'roof or subfloor survived')
    a2=[v for v in views if v['bunker']=='A_2'];assert len(a2)==2,'Shaft cap became a separate floor'
    rear=next(e for e in ordinary if e['bunker']=='A_2' and '竖井' in e['label'])
    landing=next(v for v in a2 if v['id']==rear['view']);assert np.isfinite(landing['grid']).sum()*landing['step']**2>1000000,'Shaft is not attached to a room/corridor view'
    out=dict(bunkers=len(groups),checkedCells=sum(r['checkedCells'] for r in report),missingDisplayCells=0,killboxesChecked=12,regressions=['connected platform','stairs remain separate','overlapping minor room','D1 circled platform','main/rear entry distinction','one killbox per entrance level','no roof tubes or subfloor room duplicates','shaft attached to underground room'],records=report,limitations=['Coverage checks extracted surfaces after explicit non-floor removal; it cannot detect geometry absent from the original extraction.','Display coverage is not live traversability or complete room-semantic validation.'])
    (b.ROOT/'checks/geometry-coverage.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in out.items() if k!='records'}))
if __name__=='__main__':main()
