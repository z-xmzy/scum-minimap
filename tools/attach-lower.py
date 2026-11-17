from pathlib import Path
p=Path('tools/bake-bridges.py');s=p.read_text().replace("np.savez(report", " f.write((root/'tools/bridge-underroads/links.bin').read_bytes())\nnp.savez(report");p.write_text(s)
p=Path('source/src/GridRouter.Bridges.cs');s=p.read_text(encoding='utf-8-sig').replace('internal BridgeDeck[] Deck;', 'internal readonly Dictionary<int,List<int>> UnderRoads=new Dictionary<int,List<int>>();\n            internal BridgeDeck[] Deck;')
s=s.replace('bridges=b;', '''int under=r.ReadInt32();if(under<0||under>ng*40)throw new InvalidDataException("Bridge underpass edges");
                for(int i=0;i<under;i++){int a=r.ReadInt32(),c=r.ReadInt32();b.Add(b.UnderRoads,a,c);b.Add(b.UnderRoads,c,a);}
                bridges=b;''')
s=s.replace('List<int> gates;if', 'List<int> fineRoads;if(b.UnderRoads.TryGetValue(id,out fineRoads))foreach(int next in fineRoads)visit(next);\n                List<int> gates;if')
p.write_text(s,encoding='utf-8-sig')
