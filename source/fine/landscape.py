import numpy as np
def sample(dem,x,y):
    # Triangle interpolation, matching the game's landscape diagonal.
    a=np.clip((14.82+x)/1.5,0,10159.99999); b=np.clip((5.82+y)/1.5,0,10159.99999)
    i=a.astype('int32'); j=b.astype('int32'); fx=a-i; fy=b-j
    p=dem[j,i];q=dem[j,i+1];r=dem[j+1,i];s=dem[j+1,i+1]
    return np.where(fx+fy<=1,p+fx*(q-p)+fy*(r-p),s+(1-fx)*(r-s)+(1-fy)*(q-s))
