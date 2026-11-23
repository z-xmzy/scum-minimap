"""Locate exterior entry actors; keep provenance and never treat interior doors as exits."""
import collections,functools,gzip,hashlib,json,struct
import build_views as bv
from build_views import np,Image,ROOT,BASE,OUT,CAT
import analyze
PRIMARY=BASE/'bunker-extraction-20261031/output/scenes'
SUPPLEMENT=BASE/'bunker-20261102/extracted'

@functools.lru_cache(maxsize=32)
def scene(name):
    path=PRIMARY/(name+'.json')
    if not path.exists():path=SUPPLEMENT/(name+'.json')
    return json.loads(path.read_text(encoding='utf-8-sig'))['actors']

def cls(actor):return actor.get('class','').split('/')[-1].split('.')[0]

def collect():
    result=[]
    def add(b,s,a,label,component=None):
        t=component['world'] if component else a['transform']
        xyz=t['location']
        if any(e['bunker']==b['id'] and np.linalg.norm(np.array(e['world'])-xyz)<150 for e in result):return
        result.append(dict(bunker=b['id'],kind=b['type'],label=label,world=xyz,scene=s,actor=a['name'],actorClass=cls(a),component=component['name'] if component else None))
    for b in CAT:
        kind=b['type']
        names=b.get('entranceScenes',[]) if kind in ('ordinary','abandoned','secret','research') else b['scenes']
        for s in names:
            for a in scene(s):
                c=cls(a);components=a.get('components',[])
                if kind=='ordinary':
                    if c in ('BP_Bunker_Entrance_01','BP_Bunker_Entrance_01_A'):
                        door=next((x for x in components if x['name']=='BP_Tunnel_Big_Doors_021'),None)
                        if door:add(b,s,a,'正门入口',door)
                    if c in ('BP_Bunker_Entrance_03','BP_Bunker_Entrance_03b'):add(b,s,a,'后门入口（竖井）')
                elif kind=='abandoned' and c=='BP_Bunker_Entrance_02':
                    door=next((x for x in components if x['name']=='SM_MERGED_EntranceDoor_01'),None)
                    if door:add(b,s,a,'正门入口',door)
                elif kind=='secret' and c=='BP_NoNameDoor_Powered_SecretBunker':add(b,s,a,'入口')
                elif kind=='research' and c=='BP_Tunnel_BlastDoor_02_DefaultSlidingDoor' and a['name'].startswith('BP_Blast_Door_00_'):add(b,s,a,'正门入口')
                elif kind=='ww2' and a['name']==b['actor']:
                    name='SM_Single_DoorFrame_A_02' if c in ('BP_Bunker_Small_01','BP_Bunker_Small_02') else 'SM_Single_DoorFrame_A_03'
                    door=next((x for x in components if x['name']==name),None)
                    if door:add(b,s,a,'入口',door)
                elif kind=='naval':
                    if c=='BP_Bunker_Entrance_03b':add(b,s,a,'竖井入口')
                    elif c=='BP_Naval_Top_Entrance':add(b,s,a,'通道入口')
                    elif a.get('mesh','').split('/')[-1].split('.')[0]=='SM_Naval_Bottom_Entrance':add(b,s,a,'水面通道口')
                elif kind=='airbase' and a.get('mesh','').split('/')[-1].split('.')[0]=='SM_SM_Tunnel_Entrance_Net_01':add(b,s,a,'通道入口')
                elif kind=='shelter':
                    # Two ends of the extracted tunnel, not the nightclub's internal Exit triggers.
                    if a['name']=='BP_Tunnel_Small_Door3' and c=='BP_Tunnel_Small_Door':add(b,s,a,'通道口（俱乐部侧）')
                    elif a['name']=='ChildActor1_GEN_VARIABLE_BP_Metal_Door_02_C_CAT_19' and c=='BP_Metal_Door_02':add(b,s,a,'通道口（门厅侧）')
    return result

def bind(entries,views):
    points={}
    for v in views:
        y,x=np.where(np.isfinite(v['grid']));points[v['id']]=np.column_stack((v['x1']-(x+.5)*v['step'],v['y1']-(y+.5)*v['step'],v['grid'][y,x]))
    for e in entries:
        local=[v for v in views if v['bunker']==e['bunker']];xyz=np.array(e['world'])
        # Actor origins are door thresholds, not player positions. Vertical term
        # distinguishes stacked levels without pretending to know capsule height.
        scores=[];shaft='竖井' in e['label']
        for v in local:
            d=points[v['id']]-xyz;distance=d[:,0]**2+d[:,1]**2+(0 if shaft else .25)*d[:,2]**2
            i=int(distance.argmin());scores.append((float(distance[i]),v,points[v['id']][i]))
        score,v,nearest=min(scores,key=lambda x:x[0]);e['view']=v['id'];e['nearestFloor']=nearest.tolist();e['distanceToFloorM']=round(score**.5/100,2)
        e['assignment']='underground landing at shaft XY' if shaft else 'nearest floor to exterior door threshold'
        # A shaft's opening can be several metres above its underground landing.
        # Do not move the marker onto an unrelated floor or expand across a scene.
        e['included']=score<5000**2
    counts=collections.Counter((e['bunker'],e['label']) for e in entries if e['included']);indexes=collections.Counter()
    for e in entries:
        key=e['bunker'],e['label']
        if counts[key]>1:indexes[key]+=1;e['label']+=' '+str(indexes[key])

def pad_and_write(views,entries):
    for v in views:
        local=[e for e in entries if e['included'] and e['view']==v['id']]
        if not local:continue
        xy=np.array([e['world'][:2] for e in local]);step=v['step']
        # Padding is empty display space, never an invented navigable floor.
        high=np.maximum(0,np.ceil((xy.max(axis=0)+250-[v['x1'],v['y1']])/step)).astype(int)
        low=np.maximum(0,np.ceil(([v['x0'],v['y0']]-xy.min(axis=0)+250)/step)).astype(int)
        if not (high.any() or low.any()):continue
        w=v['w']+int(high[0]+low[0]);h=v['h']+int(high[1]+low[1]);assert w<=2048 and h<=2048,(v['bunker'],w,h)
        im=Image.new('RGBA',(w,h),(14,22,27,255));im.paste(Image.open(OUT/v['image']),(int(high[0]),int(high[1])));im.save(OUT/v['image'])
        v['grid']=np.pad(v['grid'],((high[1],low[1]),(high[0],low[0])),constant_values=np.nan)
        v['x1']+=float(high[0]*step);v['y1']+=float(high[1]*step);v['x0']-=float(low[0]*step);v['y0']-=float(low[1]*step);v['w']=w;v['h']=h
    with gzip.open(OUT/'views.bin','wb',compresslevel=6) as f:
        f.write(struct.pack('<4siii',b'SBMA',3,26320533,len(views)))
        for v in views:
            for key in ('id','bunker','kind','image'):
                raw=v[key].encode();f.write(struct.pack('<i',len(raw)));f.write(raw)
            f.write(struct.pack('<ii6dii',v['rank'],v['total'],v['x0'],v['y0'],v['x1'],v['y1'],v['z'],v['step'],v['w'],v['h']))
            f.write(hashlib.sha256((OUT/v['image']).read_bytes()).digest());bv.write_grid(f,v['grid']);f.write(struct.pack('<i',len(v['stairs'])));f.write(v['stairs'].tobytes())

def main():
    analyze.FOLDER=OUT;views=analyze.load('views.bin');entries=collect();bind(entries,views)
    pad_and_write(views,entries)
    (OUT/'entrances.tsv').write_text(''.join(f"{e['view']}\t{e['world'][0]}\t{e['world'][1]}\t{e['label']}\n" for e in entries if e['included']),encoding='utf-8')
    covered={e['bunker'] for e in entries if e['included']}
    result=dict(entries=entries,coveredSites=len(covered),unresolvedSites=[b['id'] for b in CAT if b['id'] not in covered],limitations=['Static entry actors do not establish current door power, lock, or server access state.','Unidentified entrances are not inferred from interior Exit signs or small room doors.'])
    (ROOT/'checks/entrance-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(entries=len(entries),included=sum(e['included'] for e in entries),coveredSites=len(covered),unresolved=result['unresolvedSites']),ensure_ascii=False))
    for e in entries:
        if e['distanceToFloorM']>10:print(e['bunker'],e['label'],e['distanceToFloorM'],e['included'])
if __name__=='__main__':main()
