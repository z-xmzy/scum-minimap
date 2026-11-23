"""Static local review document. No server, network requests or installation."""
from html import escape
from bunker_geometry import ROOT,read_json

data=read_json(ROOT/'output/floor-candidates/index.json')
types={'ordinary':'普通地堡','abandoned':'废弃地堡','secret':'秘密地堡','research':'新研究设施','ww2':'二战地堡'}
order={'research':0,'abandoned':1,'ordinary':2,'secret':3,'ww2':4}
rows=sorted(data['bunkers'],key=lambda x:(order[x['type']],x['bunker']))
parts=['''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>SCUM 地堡地图提取核对</title><style>body{margin:32px auto;padding:0 20px;max-width:1100px;background:#10181e;color:#e4eaed;font:16px/1.6 system-ui}a{color:#91d9dc}details{margin:16px 0;padding:12px 16px;background:#19262f;border:1px solid #344752;border-radius:8px}summary{cursor:pointer;font-weight:600}img{max-width:100%;max-height:620px;object-fit:contain}figure{margin:20px 0;border-top:1px solid #344752;padding-top:12px}figcaption{margin-bottom:8px}.note{padding:16px;border-left:4px solid #e5b461;background:#28291f}code{overflow-wrap:anywhere}</style>
<h1>SCUM 地堡地图提取核对</h1><p>本机游戏 Build 26320533 · 93 个实例 · 170 张高度切片 · 2026-11-01</p>
<p class="note">这是从游戏模型生成的离线核对图。高度切片数量不等于游戏楼层数；门的开关、动态障碍和人物高度尚未实测。当前小地图程序尚未接入这些数据。</p>
<p>点击地堡展开，点击图片查看原图。废弃地堡已提取游戏自身的地下 1、2 层标签；每张切片与正式楼层的对应关系仍需核对。</p>''']
for row in rows:
    name=escape(row['bunker'])
    parts.append(f'<details><summary>{types[row["type"]]} · {name} · {len(row["slices"])} 张高度切片</summary>')
    for i,s in enumerate(sorted(row['slices'],key=lambda s:-s['surfaceZCm']),1):
        src='floor-candidates/'+escape(s['image'],quote=True)
        parts.append(f'<figure><figcaption>切片 {i} · 地板候选 Z = {s["surfaceZCm"]/100:.2f} 米（世界高度）</figcaption><a href="{src}"><img loading="lazy" src="{src}" alt="{name} 高度切片 {i}"></a></figure>')
    parts.append('</details>')
parts.append('<p>详细数据：<a href="bunker-catalogue.json">实例目录</a> · <a href="all-poi-coverage.json">原地图点位覆盖核对</a> · <a href="provenance.json">来源与版本</a></p></html>')
(ROOT/'output/地堡地图预览.html').write_text('\n'.join(parts),encoding='utf-8')
print('Wrote static review document for',len(rows),'bunkers')
