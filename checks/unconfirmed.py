import csv,collections
from pathlib import Path
old=Path('../SCUM-2.4.9-all-bridges')
cat={r['ID']:r for r in csv.DictReader((old/'组件逐件清单.csv').open(encoding='utf-8-sig'))}
missing=[cat[r[0]] for r in csv.reader(open('checks/deck-coverage.tsv',encoding='utf8'),delimiter='\t') if not r[2].isdigit()]
models=list(csv.reader(open('tools/bridge-meshes/placements.tsv',encoding='utf-8-sig'),delimiter='\t'))
for m in missing:
 siblings=[(r[3].split('.')[-1],r[-1]) for r in models if r[0].split('/')[-1]==m['关卡'] and r[1]==m['对象']]
 print(m['ID'],m['模型'],m['关卡'],m['对象'],siblings[:8])
print('unconfirmed',len(missing))
Path('checks/unconfirmed-scenes.txt').write_text('\n'.join(sorted(set('/Game/ConZ_Files/Maps/The_Island/'+m['关卡'] for m in missing)|{'/Game/ConZ_Files/Maps/The_Island/Z_3_Motocross_Track'})),encoding='utf8')
