import json,os,glob,collections,sys
old='D:/codex/SCUM-MiniMap-v2.3.25/bunker-extraction-20261031/output/scenes/'
old2='D:/codex/SCUM-MiniMap-v2.3.25/bunker-20261102/extracted/'
def load(p):
  b=open(p,'rb').read()
  t=b.decode('utf-16') if b[:2] in (b'\xff\xfe',b'\xfe\xff') else b.decode('utf-8-sig')
  return json.loads(t)
def key(a):
  loc=next((c['world']['location'] for c in a['components'] if c.get('world')),[0,0,0])
  return (a['class'].rsplit('/',1)[-1],tuple(round(v) for v in loc))
def mkey(a):
  return tuple(sorted((c.get('mesh','').rsplit('.',1)[-1],tuple(round(v) for v in c['world']['location'])) for c in a['components'] if c.get('mesh') and c.get('world')))
for f in sorted(glob.glob('scenes/*.json')):
  n=os.path.basename(f)
  o=old+n if os.path.exists(old+n) else old2+n if os.path.exists(old2+n) else None
  if not o:print('NOT-IN-OLD',n);continue
  A=load(o);B=load(f)
  ka=collections.Counter(key(a) for a in A['actors']);kb=collections.Counter(key(a) for a in B['actors'])
  ma=collections.Counter(m for a in A['actors'] for m in mkey(a));mb=collections.Counter(m for a in B['actors'] for m in mkey(a))
  add=kb-ka;rem=ka-kb;madd=mb-ma;mrem=ma-mb
  if add or rem or madd or mrem:
    print(n,'actors',len(A['actors']),'->',len(B['actors']),'actor+',sum(add.values()),'actor-',sum(rem.values()),'mesh+',sum(madd.values()),'mesh-',sum(mrem.values()))
    for k in [k for k in list(add)+list(rem) if any(t in k[0] for t in ('Room','Lab','Armory','depos','Depos','Entrance','Tunnel'))][:10]:print('   ','+' if k in add else '-',k)
    for k in [k for k in list(madd)+list(mrem) if any(t in k[0] for t in ('Room','Tunnel','Corridor','Stairs','Floor'))][:10]:print('   mesh','+' if k in madd else '-',k)
