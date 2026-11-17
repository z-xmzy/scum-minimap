from pathlib import Path
import gzip,struct,shutil,numpy as np
base=Path('tools/bridge-meshes');out=Path('tools/bridge-underroads');out.mkdir(exist_ok=True);(out/'tmp').mkdir(exist_ok=True)
for name in ('open-game.ps1','collect.ps1'):shutil.copyfile(base/name,out/name)
with gzip.open('source/resources/nav-bridges.bin') as f:
 h,w,ng,_=struct.unpack('<4siii',f.read(16))
 guard=np.zeros(w*w,'B')
 for i in range(ng):guard[struct.unpack('<iBBf?',f.read(11))[0]]=1
guard.tofile(out/'near.bin')
s=(base/'Collect.cs').read_text()
s=s.replace('const int W=15216;', 'const int W=15216;byte[] near;')
s=s.replace('cat=c;reader=', 'near=File.ReadAllBytes(Path.Combine(folder,"near.bin"));cat=c;reader=')
a=s.index('return p.Contains("bridge")');b=s.index(';}',a)
s=s[:a]+'''return (p.Contains("/roads/")||p.Contains("/road/")) && !p.Contains("bridge") && !p.Contains("_rvt.") && (p.Contains("asphalt")||p.Contains("dirtroad")||p.Contains("gravel")||p.Contains("road_segment"))'''+s[b:]
s=s.replace('double s=((xx-x)', 'if(near[xx/4+yy/4*(W/4)]==0)continue;double s=((xx-x)')
s=s.replace('if(vertices.Count<2)', 'if(touched.Count==0)return; if(vertices.Count<2)')
(out/'Collect.cs').write_text(s)
