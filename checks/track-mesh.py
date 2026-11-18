from pathlib import Path
import csv,numpy as np
from PIL import Image,ImageDraw
folder=Path('tools/model-review');tri=np.memmap(folder/'triangles.bin','<f4','r').reshape(-1,3,3)
rows=[r for r in csv.reader((folder/'index.tsv').open(encoding='utf-8-sig'),delimiter='\t') if r[0].endswith('Z_3_Motocross_Track')]
wanted=['Bridge_01','Bridge_02','Platform_01','Platform_01_End','Platform_02','SM_Track','Wall_Platform_01','Wall_Platform_02','Wall_Platform_03']
meshes=[]
for r in rows:
 name=r[3].split('.')[-1]
 if name not in wanted:continue
 raw=np.array(tri[int(r[5]):int(r[5])+int(r[6])],float)/100
 raw[:,:,0]=(6177.18-raw[:,:,0]);raw[:,:,1]=(6186.18-raw[:,:,1])*15216.18/15236.18
 n=np.cross(raw[:,1]-raw[:,0],raw[:,2]-raw[:,0]);valid=n[:,2]<-1e-8
 grade=np.hypot(n[:,0],n[:,1])/np.maximum(abs(n[:,2]),1e-15)
 meshes.append((name,raw[valid],grade[valid]))
 print(name,'xy',np.round(raw[:,:,:2].reshape(-1,2).mean(0),1),'z',np.round([raw[:,:,2].min(),raw[:,:,2].max()],1))
img=Image.new('RGB',(1400,900),'#fcfcfc');draw=ImageDraw.Draw(img)
palette={'Bridge_01':'#ef5b37','Bridge_02':'#e5ab16','Platform_01':'#1982c4','Platform_01_End':'#1982c4','Platform_02':'#1982c4','SM_Track':'#999999','Wall_Platform_01':'#52aa66','Wall_Platform_02':'#52aa66','Wall_Platform_03':'#52aa66'}
# Orthographic perspective, centering on surveyed approach structures.
for name,t,gr in sorted(meshes,key=lambda x:x[0]!='SM_Track'):
 p=t-np.array([3500,13680,112])
 x=700+p[:,:,0]*5+p[:,:,1]*1.7;y=550+p[:,:,1]*2.5-p[:,:,2]*18
 for xx,yy,normal in zip(x,y,gr):
  if min(xx)>1400 or max(xx)<0 or min(yy)>900 or max(yy)<0:continue
  pts=list(zip(xx.tolist(),yy.tolist()))
  draw.polygon(pts,fill=palette[name],outline=palette[name] if name=='SM_Track' else '#333333')
for i,(name,color) in enumerate(palette.items()):
 draw.rectangle((15,18+i*20,30,31+i*20),fill=color);draw.text((40,17+i*20),name,fill='black')
img.save('checks/track-original-mesh.png')
