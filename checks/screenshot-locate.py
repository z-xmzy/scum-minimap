import sys
from pathlib import Path
from PIL import Image
import cv2,numpy as np
Image.MAX_IMAGE_PIXELS=None
root=Path("checks")
frame=Path(sys.argv[1])
pic=cv2.imdecode(np.fromfile(frame,np.uint8),cv2.IMREAD_COLOR)
query=cv2.cvtColor(pic,cv2.COLOR_BGR2GRAY)
mask=~(np.linalg.norm(pic.astype(float)-[200,214,53],axis=2)<65)
sift=cv2.SIFT_create(nfeatures=2200,contrastThreshold=.025)
q,qd=sift.detectAndCompute(query,mask.astype(np.uint8)*255)
print("query",len(q),flush=True)
world=Image.open("source/resources/map.png")
size=world.size[0]
print("map",world.size,flush=True)
# Tile to bound SIFT memory. Use native terrain texture and both smaller scales.
for scale in (.25,.5,1.):
    scaled=world.convert("L").resize((round(size*scale),)*2,Image.Resampling.LANCZOS)
    gray=np.asarray(scaled);h,w=gray.shape
    votes=[]
    for y in range(0,h,950):
        for x in range(0,w,950):
            tile=gray[max(0,y-60):y+1010,max(0,x-60):x+1010]
            tk,td=sift.detectAndCompute(tile,None)
            if td is None or len(td)<2:continue
            knn=cv2.BFMatcher().knnMatch(qd,td,k=2)
            good=[m for m,n in knn if m.distance<.72*n.distance]
            if len(good)<5:continue
            qa=np.float32([q[m.queryIdx].pt for m in good])
            ta=np.float32([tk[m.trainIdx].pt for m in good])+[max(0,x-60),max(0,y-60)]
            A,I=cv2.estimateAffinePartial2D(qa,ta,method=cv2.RANSAC,ransacReprojThreshold=2)
            if I is not None and I.sum()>=5:
                votes.append((int(I.sum()),A/scale,qa[I.ravel()>0],ta[I.ravel()>0]/scale))
    if votes:
        votes.sort(key=lambda v:-v[0]);ct,A,s,t=votes[0]
        print("scale",scale,"inliers",ct,"runnerups",[v[0] for v in votes[1:5]],"affine",A,"query coverage",s.min(0),s.max(0),flush=True)
        if ct>=12:
            out=cv2.warpAffine(np.asarray(world.convert("RGB")),A,(query.shape[1],query.shape[0]),flags=cv2.INTER_LINEAR|cv2.WARP_INVERSE_MAP)
            Image.fromarray(out).save(root/"screenshot-matched-map.png")
            np.savetxt(root/"screenshot-transform.txt",A)
            break
    else: print("scale",scale,"no vote",flush=True)
