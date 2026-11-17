from pathlib import Path
p=Path('tools/package-ui.py');s=p.read_text(encoding='utf8').replace("r/'verified'","r/'verified-final'");p.write_text(s,encoding='utf8')
p=Path('checks/summarize.py');s=p.read_text(encoding='utf8').replace("'heads']:","'heads','through']:");p.write_text(s,encoding='utf8')
p=Path('checks/verify-topology.py');s=p.read_text(encoding='utf8').replace("rise>(.45+.32*span+.02)","rise>(.45*span+.01)");p.write_text(s,encoding='utf8')
