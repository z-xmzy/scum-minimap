import gzip,hashlib,pathlib,struct,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
FOLDER=ROOT/'app/assets/bunkers'
def check():
    original=ROOT.parent/'marker-icons-20261103/app/assets/bunkers/atlas.bin'
    assert (FOLDER/'atlas.bin').read_bytes()==original.read_bytes(),'Detection atlas changed'
    with gzip.open(FOLDER/'views.bin','rb') as f:
        magic,version,build,count=struct.unpack('<4siii',f.read(16));assert (magic,version,build)==(b'SBMA',3,26320533)
        by_bunker={};total=0
        for _ in range(count):
            strings=[f.read(struct.unpack('<i',f.read(4))[0]).decode() for j in range(4)]
            rank,n,x0,y0,x1,y1,z,step,w,h=struct.unpack('<ii6dii',f.read(64));sha=f.read(32)
            assert sha==hashlib.sha256((FOLDER/strings[3]).read_bytes()).digest()
            assert 1<=rank<=n<=100 and 1<=w<=2048 and 1<=h<=2048
            assert abs(x1-x0-w*step)<.01 and abs(y1-y0-h*step)<.01
            tiles=struct.unpack('<i',f.read(4))[0];assert tiles>0;f.read(tiles*260)
            faces=struct.unpack('<i',f.read(4))[0];assert faces<=20000;f.read(faces*36)
            total+=((w+7)//8)*((h+7)//8)+tiles*64+faces*9
            by_bunker.setdefault(strings[1],[]).append(strings[0])
        assert not f.read();assert total<16000000
    assert len(by_bunker)==96,len(by_bunker)
    assert len(by_bunker['D_1_Abandoned'])==2,by_bunker['D_1_Abandoned']
    titles=dict(line.split('\t') for line in (FOLDER/'views.tsv').read_text(encoding='utf-8').splitlines())
    assert any('地下1层' in titles[x] for x in by_bunker['D_1_Abandoned'])
    assert any('地下2层' in titles[x] for x in by_bunker['D_1_Abandoned'])
    assert count<230
    print(json.dumps(dict(pages=count,bunkers=len(by_bunker),atlasUnchanged=True,loaderUnits=total),ensure_ascii=False))
if __name__=='__main__':check()
