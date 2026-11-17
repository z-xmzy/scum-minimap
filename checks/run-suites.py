import os,subprocess,sys,time
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
root=Path(__file__).resolve().parents[1]
app=root/'package/SCUM-MiniMap/app'
old=root.parent/'SCUM-2.4.9-all-bridges'
os.environ['TEMP']=os.environ['TMP']=str(root/'temp')
def run(n):
 source=old/(n+'.tsv')
 if not source.exists():source=root/'checks'/(n+'.tsv')
 with (root/'checks'/(n+'.log')).open('w') as log:
  subprocess.run([str(app/'BridgeProbe.exe'),str(app/'SkynettMiniMap.exe'),str(source),str(root/'checks'/(n+'.out'))],cwd=app,stdout=log,stderr=subprocess.STDOUT,check=True)
 print(n,'finished',flush=True)
with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(run,sys.argv[1:]))
