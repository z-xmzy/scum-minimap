from pathlib import Path
import csv,statistics,json,math
d={}
for name in ['old0','new0','new1','old1']:
 lines=list(csv.reader(open('checks/bench-'+name+'.tsv',encoding='utf8'),delimiter='\t'))
 a=sorted(float(r[-1]) for r in lines[1:]);d[name]=dict(calls=len(a),p50_ms=round(a[math.ceil(len(a)*.50)-1],3),p95_ms=round(a[math.ceil(len(a)*.95)-1],3),max_ms=round(a[-1],3),startup_ms=round(float(lines[0][1]),1),success=sum(r[-2]=='True' for r in lines[1:]))
print(json.dumps(d,indent=2))
Path('checks/bench-summary.json').write_text(json.dumps(d,indent=2))
