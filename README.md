# SCUM 小地图（scum-minimap）

SCUM 游戏的外置地图覆盖层和道路导航工具：全屏战术地图、紧凑小地图、实时定位、道路/桥梁分层导航、语音播报、私有组队位置共享、地堡地图等。

## 上游项目与出处

**本项目基于 [Mike-Rafone-SCUM/MiniMap](https://github.com/Mike-Rafone-SCUM/MiniMap) 修改而来。** 原项目（SCUM MiniMap，为 Skynett 社区制作）的设计、代码与资源版权归原作者所有，感谢原作者的工作。

- 原项目仓库没有声明开源许可证，因此原作代码的版权保留给原作者。本仓库为私有的个人修改版本；如需公开发布或分发，应先取得原作者许可。
- 第三方数据、开源库、游戏版权声明见 [source/ATTRIBUTION.md](source/ATTRIBUTION.md)。
- SCUM 及其游戏内容版权归 Gamepires 与 Jagex 所有，本项目与其无关联，也未获其认可。

## 本版本在上游基础上的主要改动

从 v2.4.0 起（详见 [source/CHANGELOG.md](source/CHANGELOG.md)）：

- **v2.4.0** 本地独立世界导航：路网格由游戏地形、水面、道路桥梁几何、静态碰撞和手画道路生成
- **v2.4.1–v2.4.8** 大型建筑占用、车辆高度碰撞、地图启动预加载与性能优化
- **v2.4.9** 保存命名路线，LX- 路线码分享导入，顺序/循环导航
- **v2.4.10–v2.4.11** 桥面与地面道路分层导航，桥区路口语音播报修复
- **v2.4.12** 游戏显示系统光标时隐藏小地图；轨迹学习默认关闭
- 新界面（ui-redesign，已合入 main）
- 地堡地图房间识别与生成管线（bunker-rooms 分支）

## 目录结构

| 路径 | 内容 |
|---|---|
| `source/` | 应用源码（C#/WPF）、资源、构建与测试脚本、详细文档 |
| `source/README.md` | 原版用户说明（安装、设置、快捷键等，英文） |
| `source/DEVELOPMENT.md` | 开发与构建说明 |
| `tools/` | 路网、桥梁、地堡地图等数据烘焙脚本 |
| `tools/bunker-pipeline/` | 地堡地图生成管线 |
| `checks/` | 回归检查、基准测试与诊断脚本 |
| `Build-LocalCandidate.ps1` | 本地候选版本构建脚本 |

`版本/`、`实验/`、`发布包/` 等归档与构建产物不进 git（见 `.gitignore`）。

## 构建与测试

需要 Windows、.NET Framework 4.x 及其 x64 C# 编译器，在 `source/` 目录下用 PowerShell 运行：

```powershell
.\Test.ps1
.\Build.ps1 -Configuration Test
.\Build.ps1 -Configuration Release
```

更多细节见 [source/DEVELOPMENT.md](source/DEVELOPMENT.md)。

## 分支与版本

- `main`：当前主线（v2.4.12 + 新界面）
- `ui-redesign`：新界面开发分支
- `bunker-rooms`：地堡地图修复与生成管线，尚未合入 main
- 标签 `v2.4.0` … `v2.4.12` 对应各发布版本

## Git LFS

地图瓦片、导航网格、测试图片等大文件用 Git LFS 存储（约 250 MB）。克隆前请先安装 [Git LFS](https://git-lfs.com/)：

```bash
git lfs install
git clone https://github.com/z-xmzy/scum-minimap.git
```
