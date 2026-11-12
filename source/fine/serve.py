import sys
from pathlib import Path
from island import Engine
def serve():
    engine=None
    try:
        for line in sys.stdin:
            mode,*coords=line.split()
            if engine is None:engine=Engine(Path(sys.argv[1]))
            a,b,c,d=map(float,coords)
            path,_,_=engine.route((a,b),(c,d),mode)
            if path:
                print('OK '+';'.join(f'{x:.17g},{y:.17g}' for x,y in path),flush=True)
            else:print('NONE',flush=True)
    finally:
        if engine is not None:engine.close()
if __name__=='__main__':serve()

