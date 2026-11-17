from pathlib import Path
p=Path('tools/bake-bridges.py');s=p.read_text()
needle="for k,z in zip(rk[(distance<=6.5)&(abs(rh-np.asarray(zs)[idx])<=38)],rh[(distance<=6.5)&(abs(rh-np.asarray(zs)[idx])<=38)]):"
assert needle in s
s=s.replace(needle,'''on_ground=ground(rk%S,rk//S)
bridge_road=(distance<=6.5)&(abs(rh-np.asarray(zs)[idx])<=38)
bridge_road|=rh>on_ground+150
for k,z in zip(rk[bridge_road],rh[bridge_road]):''')
p.write_text(s)
p=Path('source/src/GridRouter.Bridges.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('dist+=error*.08;', '''// Position Z may be above the tyres or feet. Prefer a supporting floor
                            // below the position over a nearly equidistant ceiling above it.
                            dist+=(error+2*Math.Max(0,z-height.Value-35))*.08;''')
p.write_text(s,encoding='utf-8-sig')
