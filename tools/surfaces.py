from pathlib import Path
p=Path('tools/bridge-meshes/Collect.cs');s=p.read_text().replace('new Dictionary<int,float>()','new Dictionary<(int cell,int band),float>()').replace('if(!touched.TryGetValue(key,out var old)||old<z)touched[key]=z;', 'var surface=(key,(int)Math.Floor(z/15));if(!touched.TryGetValue(surface,out var old)||old<z)touched[surface]=z;').replace('cells.Write(item.Key);','cells.Write(item.Key.cell);')
p.write_text(s)
