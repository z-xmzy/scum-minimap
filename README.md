# SCUM 小地图（scum-minimap）

SCUM 游戏的外置地图覆盖层和道路导航工具：全屏战术地图、紧凑小地图、实时定位、道路/桥梁分层导航、语音播报、私有组队位置共享、地堡地图等。

## 上游项目与出处

**本项目基于 [Mike-Rafone-SCUM/MiniMap](https://github.com/Mike-Rafone-SCUM/MiniMap) 修改而来。** 
## 本版本在上游基础上的主要改动

从 v2.4.0 起（详见 [source/CHANGELOG.md](source/CHANGELOG.md)）：

- **v2.4.0** 本地独立世界导航：路网格由游戏地形、水面、道路桥梁几何、静态碰撞和手画道路生成
- **v2.4.1–v2.4.8** 大型建筑占用、车辆高度碰撞、地图启动预加载与性能优化
- **v2.4.9** 保存命名路线，LX- 路线码分享导入，顺序/循环导航
- **v2.4.10–v2.4.11** 桥面与地面道路分层导航，桥区路口语音播报修复
- **v2.4.12** 游戏显示系统光标时隐藏小地图；轨迹学习默认关闭
- **v2.5.0** Windows 安装程序；启动时检查本仓库 Releases 的新版本，可取消
- **v2.5.1** 自定义准星（大地图侧边栏可调，打开背包/暂停时自动隐藏）；删除 Discord 引导
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
| `installer/` | Windows 安装程序（Inno Setup）脚本与构建脚本 |

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

## 安装程序与发布

普通用户从 [Releases](https://github.com/z-xmzy/scum-minimap/releases) 下载 `SCUM-MiniMap-Setup-x.y.z.exe` 安装。

发布新版本：

1. 在 `source/CHANGELOG.md` 写新版本条目，并改 `source/src/MiniMap.cs` 的 `VersionString`。
2. 把完整软件资源放到 `package/SCUM-MiniMap/`（含 `app/assets`、`app/fine`、`dist`，不进 git）。
3. 安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)，运行 `installer\Build-Installer.ps1`，在 `release/` 得到安装包、`update.txt`、`files.txt` 和 `files.pack`（需要 Python 3）。测试安装请加 `-TestInstall`，它用独立的 AppId，不会覆盖本机正式安装的卸载项和快捷方式。
4. 建 GitHub Release（标签 `vX.Y.Z`），先存为草稿，四个文件都上传完再发布并设为 Latest。已安装的软件启动时读 `releases/latest/download/update.txt`：2.6.0 起会再读该标签下的 `files.txt`，比对本机文件，只用 HTTP 分段下载从 `files.pack` 取变化的文件；没有文件包或变化太多时才下载安装包。2.5.x 只认识安装包。

## Git LFS

地图瓦片、导航网格、测试图片等大文件用 Git LFS 存储（约 250 MB）。克隆前请先安装 [Git LFS](https://git-lfs.com/)：

```bash
git lfs install
git clone https://github.com/z-xmzy/scum-minimap.git
```
