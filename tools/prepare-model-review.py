from pathlib import Path
import csv,shutil
root=Path('.');dest=root/'tools/model-review';dest.mkdir(exist_ok=True);(dest/'tmp').mkdir(exist_ok=True)
shutil.copyfile(root/'tools/bridge-meshes/open-game.ps1',dest/'open-game.ps1')
bad={int(r['层']) for r in csv.DictReader(open('../SCUM-MiniMap-2.4.10-map/checks/两岸接入口清单.csv',encoding='utf-8-sig')) if r['结论']=='端部需核实'}
selected={c[0] for c in csv.reader(open('checks/deck-coverage.tsv',encoding='utf8'),delimiter='\t') if not c[2].isdigit() or int(c[2]) in bad}
cat={r['ID']:r for r in csv.DictReader(open('../SCUM-2.4.9-all-bridges/组件逐件清单.csv',encoding='utf-8-sig'))}
wanted={(cat[i]['关卡'],cat[i]['对象'],cat[i]['模型']) for i in selected}
output=[]
for r in csv.reader(open('tools/bridge-meshes/placements.tsv',encoding='utf-8-sig'),delimiter='\t'):
 if (r[0].split('/')[-1],r[1],r[3].split('.')[-1]) in wanted:output.append(r[:7])
with (dest/'targets.tsv').open('w',encoding='utf8',newline='') as f:csv.writer(f,delimiter='\t').writerows(output)
print('placements for full geometry review',len(output),'scenes',len(set(r[0] for r in output)))
