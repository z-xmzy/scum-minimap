from pathlib import Path
import csv,collections,zipfile,json
root=Path(__file__).resolve().parents[1];checks=root/'checks';old=root.parent/'SCUM-2.4.9-all-bridges'
summary=list(csv.DictReader((checks/'逐条复测.csv').open(encoding='utf-8-sig')))
catalog=list(csv.DictReader((old/'区域与手画清单.csv').open(encoding='utf-8-sig')))
components={r['ID']:r['区域'].split(';') for r in csv.DictReader((old/'组件逐件清单.csv').open(encoding='utf-8-sig'))}
all_rows=[]
for b in catalog:
 if not b['ID'].startswith('B'):continue
 id=b['ID'];original=[r for r in summary if r['用例集'] in ['area','approach','extra'] and (r['ID']==id or r['ID'].startswith(id+'X') or id in components.get(r['ID'],[]))]
 actual=[r for r in summary if r['用例集']=='decks' and id in components.get(r['ID'],[])]
 cross=[r for r in summary if r['用例集']=='levels' and r['ID'].startswith(id+'X')]
 all_rows.append(dict(区域=id,格区=b['格区'],模型=b['备注'],原用例调用=len(original),原用例返回路线=sum(r['规划成功']=='True' for r in original),独立模型桥面调用=len(actual),独立桥面返回路线=sum(r['规划成功']=='True' for r in actual),交叉点直行调用=len(cross),交叉点直行返回路线=sum(r['规划成功']=='True' for r in cross)))
with (checks/'逐区域复测.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.DictWriter(f,fieldnames=list(all_rows[0]));w.writeheader();w.writerows(all_rows)
archive=root.parent/'SCUM-MiniMap-2.4.10-桥面复测证据.zip'
files=['全岛桥面修复复测报告.md','release-manifest.json','逐条复测.csv','逐区域复测.csv','levels-verification.csv','decks-verification.csv','heads-verification.csv','两岸接入口清单.csv','topology-verification.json','bench-summary.json','BridgeProbe.cs','BridgeIntegration.cs','BridgeBench.cs','summarize.py','verdict.py','gate-verdict.py','verify-topology.py','integration-release.log','release-exits.txt','ui-release.log','muted-release.log','packaged-final.log','remaining-exits.txt','build.log']
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for name in files:z.write(checks/name,name)
 for n in ['area','extra','approach','other','stacked','levels','decks','heads','through']:
  p=old/(n+'.tsv')
  if not p.exists():p=checks/(n+'.tsv')
  z.write(p,'inputs/'+p.name);z.write(checks/(n+'.out'),'outputs/'+n+'.out')
 for name in ['levels-expected.tsv','deck-coverage.tsv','heads-expected.tsv','heads-skipped.tsv','through-expected.tsv','bench.tsv','bench-old0.tsv','bench-old1.tsv','bench-new0.tsv','bench-new1.tsv']:
  z.write(checks/name,'inputs/'+name)
 for name in ['区域与手画清单.csv','组件逐件清单.csv','上下层交叉清单.csv']:
  z.write(old/name,'original-inventory/'+name)
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print('regions',len(all_rows),'evidence_bytes',archive.stat().st_size)
