# 地堡地图生成管线（bunker-rooms 修复版）

地堡资产（`app/assets/bunkers`：atlas.bin、views.bin、*.tsv、PNG）不进 git。本目录是生成它们的脚本，
从 `D:\codex\SCUM-MiniMap-v2.3.25` 的各阶段目录拷来并修复。目录名保持原样，脚本靠相对的兄弟目录互相找数据。

## 2026-11-23 修复

1. **镜像模型地板丢失**：游戏里 scale 行列式为负（镜像摆放）的模型，三角形绕序会反转，原管线把地板当成天花板、
   天花板当成地板，地面被净空过滤掉。新增 `floorplans.transform_triangles()`，镜像时翻转面序；atlas、显示层、
   房间填色全部改用它（`bunker-20261102/supplement_geometry.py` 原本就处理了镜像，未改）。
   效果：普通地堡 17 间小实验室/防化室（A_2、B_0、B_4、C_1、D_0、D_3、Z_0、Z_2）补全；
   四座废弃地堡电梯底部通道从约 10% 补到 93–100%。单元测试 `test_geometry.py`。
2. **房间识别**（`decorate_views.py`）：G 区实验室 `BP_Room_G_Lab_*`、`WareHouse`、`BP_SM_Room_*`；
   空军基地等补充场景的房间模型按 mesh-manifest 找回；按家具识别卫生间/办公室/军械室/宿舍/储藏室；
   没有房间外壳模型的安检室、维修间只在原点附近标名字，不填色。
3. **游戏更新核对**：`Dump-Scenes.ps1` 只读导出当前游戏的地堡场景，`scene_diff.py` 和旧数据对比。
   build 26789993 与 26320533 相比地堡房间和结构没有变化（只有电线随机变体、二战地堡外沙袋/阻挡体）。

## 重建顺序

工作目录按以下布局放（输入数据从 D:\codex 拷贝，不改原件）：

```
<W>/bunker-extraction-20261031/   本目录同名脚本 + output/ + calibration/（原提取数据）
<W>/bunker-20261101/              Build-BunkerAtlas.py → Add-StairSurfaces.py
<W>/bunker-20261102/              + extracted/（原补充数据）；Build-BunkerAtlas.py → Add-StairSurfaces.py → Merge-Atlas.py
<W>/marker-icons-20261103/app/assets/bunkers/   ← 拷入 bunker-20261102/assets/bunkers/*
<W>/bunker-complete-20261104/     + research/；tools/build_views.py → decorate_views.py → build_entrances.py → build_connections.py
```

注意：`decorate_views.py` 只能在 `build_views.py` 之后跑一次（`build_entrances.py` 会改图尺寸），重跑要先删
`checks/room-walls` 和 `app/assets/bunkers` 从 build_views 开始。设 `PYTHONDONTWRITEBYTECODE=1`。

最终资产 = `marker-icons-20261103/app/assets/bunkers/*`（检测层）+ `bunker-complete-20261104/app/assets/bunkers/*`（显示层）。
验证：`source/tests/Test-BunkerMaps.ps1`、`Test-BunkerLayout.ps1`、`Test-BunkerRendering.ps1`（路径用纯英文目录，中文路径会让测试程序拿不到参数）。
