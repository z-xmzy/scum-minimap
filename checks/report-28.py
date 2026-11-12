import csv, json
from pathlib import Path
from collections import Counter
p=Path(__file__).resolve().parent
b=p.parent/'bake/audit'
def rows(path,delimiter=','):
    with path.open(encoding='utf-8-sig') as f:return list(csv.reader(f,delimiter=delimiter))
def dicts(path):
    with path.open(encoding='utf-8-sig') as f:return list(csv.DictReader(f))
regions={a[0]:a for a in rows(b/'regions.tsv','\t')}
hits=dict(rows(b/'hits.tsv','\t'))
models={a[0]:a[2].split('.')[-1] for f in [b.parent/'models.tsv',b/'props.tsv'] for a in rows(f,'\t')}
def names(s):
    return '、'.join(models[k] for k in s.split(',') if k)
fixed=rows(p/'road100.csv')
runs={r[1]:r for r in rows(p/'world3-regression.csv')[1:] if r[0]=='road100'}
header=['编号','整段结论','起点UV','终点UV','旧起点','旧终点','起点原地坡度°','终点原地坡度°','起点原地碰撞模型','终点原地碰撞模型','起点所在4米格附近模型','终点所在4米格附近模型','局部精度（起/终，米）','备注','新版路线米数']
output=[]
for line in dicts(p/'road100-summary.csv'):
    if line['world_success']=='True':continue
    i=line['pair'];a=i+'A';bkey=i+'B';x=regions[a];y=regions[bkey];new=runs[i];coords=fixed[int(i)]
    ah,bh=names(hits[a]),names(hits[bkey]);an=names((b/(a+'.near')).read_text());bn=names((b/(bkey+'.near')).read_text())
    if new[4]=='True':
        group='4米格误封，已恢复';note='原坐标经细格连续通道接回路网'
        if i=='10':note='旧版21格区域存在出口。细格从厂区设施间绕出，在实测边缘与全岛路网衔接'
    elif ah or bh:
        group='原坐标与固定模型相交';note=('起点' if ah else '终点')+'的车辆占用范围与碰撞体重叠'
        if i in ['28','64','66']:note+='；另一端原先误封，实测局部可通行'
        if i=='42':note+='；终点另处于海平面以上的独立陆块'
    elif i=='27':
        group='陆路不连通';note='终点原先误封；起点在岛上，原始1.5米地形中最近其他露出海面的地形采样中心相距67.5米，现有路桥数据未连接到目的地'
    elif max(float(x[-2]),float(y[-2]))>=40:
        group='超过当前车辆坡度阈值';note='原始地形在原坐标测得的坡度超出导航40°限制，实际车型通过能力待实机验证'
    else:raise ValueError(i)
    label={'open':'可通行','steep':'陡坡','solid':'固定障碍','water':'水面'}
    output.append([i,group,','.join(coords[2:4]),','.join(coords[4:]),label[line['start']],label[line['end']],round(float(x[-2]),2),round(float(y[-2]),2),ah,bh,an,bn,
        '/'.join(f'{4/int(t[4]):g}' for t in [x,y]),note,new[5] if new[4]=='True' else ''])
with (p/'28组逐组核对.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(header);w.writerows(output)
print(Counter(r[1] for r in output))
