from pathlib import Path
p=Path('source/CHANGELOG.md');s=p.read_text(encoding='utf-8-sig');s=s.replace('## [2.4.9]', '''## [2.4.10] - 2026-11-17

- Separate the ground road and surveyed bridge surface at island crossings. Join them at height-matched approaches.
- Use live position height for bridge navigation and offer a choice of bridge deck or lower road at overlapping map destinations.

## [2.4.9]''',1);p.write_text(s,encoding='utf-8')
p=Path('package/SCUM-MiniMap/使用说明.md');s=p.read_text(encoding='utf-8-sig')
s=s.replace('旧版可保存在另一目录独立使用。','''桥上桥下重叠时，在大地图上右键点交叉位置，选择「导航到桥面」或「导航到桥下」。有新鲜定位时，起点按游戏高度选层。导航沿模型桥面、地面道路及相接的桥头或匝道规划。没有找到可连接的入口时，可能显示无法规划。旧 LX- 路线点仅含平面坐标，在上下层重叠处可把途经点放在各自的独立路段。

桥面和桥下路面使用本机 SCUM Build 26621909 的室外模型，基础地形、碰撞栅格及部分道路资料沿用 Build 26593778。净空、道闸状态及具体车辆的可通行性需要在游戏内确认。

请将本版解压到新目录，旧版可独立使用。''')
p.write_text(s,encoding='utf-8-sig')
p=Path('tools/package-ui.py');s=p.read_text().replace("if '__pycache__' in v.parts", "if p.name=='BridgeProbe.exe':continue\n        if '__pycache__' in v.parts");p.write_text(s)
