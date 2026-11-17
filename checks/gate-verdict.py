from pathlib import Path
import csv,collections
folder=Path('checks');exp={r[0]:int(r[2]) for r in csv.reader((folder/'heads-expected.tsv').open(encoding='utf8'),delimiter='\t')};counts=collections.Counter();issues=[];out=[]
for c in csv.reader((folder/'heads.out').open(encoding='utf8'),delimiter='\t'):
 if len(c)<12:continue
 key=c[0];cross=key.startswith('X');ok=c[-3]=='True';goal=exp[key]
 a,b=(0,goal) if c[1].endswith('去') else (goal,0)
 layers=[int(p.split(',')[-1]) for p in c[-1].split(';')] if ok else []
 if not ok:status='安全拒绝' if cross else '未规划'
 elif layers[0]!=a or layers[-1]!=b or len(set(layers))<2:status='目标层不符'
 else:status='经入口换层'
 counts[(key[0],status)]+=1
 if status in ('目标层不符','未规划') and c[1].endswith('去') and c[2]=='Quickest':issues.append((key,c[1],status,layers[:1],layers[-1:],goal))
 out.append([*c[:3],a,b,round(float(c[-2]),2),status])
with (folder/'heads-verification.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['位置','测试','模式','要求起始层','要求终止层','里程米','结论']);w.writerows(out)
print(counts);print(issues[:20])
