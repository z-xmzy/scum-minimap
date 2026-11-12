"""Package the local build with an independent data directory; exclude profiles from the upstream ZIP."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
ROOT=Path(__file__).resolve().parents[1]
APP=ROOT/'app'
OUT=ROOT.parent/'SCUM-MiniMap-2.4.1-world2-20261112.zip'
PREFIX='SCUM-MiniMap-World2/'
with ZipFile(OUT.with_suffix('.part'),'w',ZIP_DEFLATED,compresslevel=5) as z:
    def put(p,target):
        z.write(p,PREFIX+target)
    for name in ('SkynettMiniMap.exe','package.txt','Start.cmd','Start-MiniMap.cmd','ATTRIBUTION.md','Lucide-LICENSE.txt','detect_zones.py','requirements.txt','keyboard-setup.png'):
        put(APP/name,'app/'+name)
    for name in ('assets/map-tiles.bin','assets/roads.bin','assets/map-roads.bin','data/user-roads.bin','data/maps/default/map.png'):
        put(APP/name,'app/'+name)
    for name in ('assets/bunkers','data/voice-navigation','data/field-console-fonts'):
        for p in sorted((APP/name).rglob('*')):
            if p.is_file():put(p,'app/'+p.relative_to(APP).as_posix())
    for name in ('RoadMarker.exe','RoadMarker-使用说明.txt','marking-kit/01-map-50.png'):
        put(APP/name,'dist/'+name)
    put(ROOT/'checks/使用说明-世界导航.md','使用说明-世界导航.md')
    put(ROOT/'checks/route-validation.csv','路线对照.csv')
    put(ROOT/'checks/version-comparison.csv','世界版逐组对照.csv')
    put(ROOT/'checks/road100-summary.csv','100组驾车走线.csv')
    z.writestr(PREFIX+'app/data/settings.ini','Language=zh-CN\r\n')
    z.writestr(PREFIX+'启动世界导航.cmd','@echo off\r\ncall "%~dp0app\\Start.cmd" %*\r\n')
with ZipFile(OUT.with_suffix('.part')) as z:
    assert z.testzip() is None
    z.extractall(ROOT/'release')
OUT.with_suffix('.part').replace(OUT)
