"""Walk every grid cell occupied by a segment for a nonzero distance, at the available resolution."""
import numpy as np
def cells(a,b):
    diff=b-a;ts=[np.array([0.,1.])]
    for k,delta in enumerate(diff):
        if abs(delta)<1e-14:continue
        low,high=sorted((a[k],b[k]))
        ts.append((np.arange(np.floor(low)+1,np.ceil(high))-a[k])/delta)
    t=np.unique(np.clip(np.concatenate(ts),0,1));c=np.floor(a+(t[:-1]+t[1:])[:,None]*.5*diff).astype(int)
    return t,c
def violations(e,path):
    from island import W,T,S
    errors=[]
    for idx,(p,q) in enumerate(zip(path,path[1:])):
        a=np.asarray(p)*W;b=np.asarray(q)*W;diff=b-a
        if np.abs(diff).max()<1e-9:continue
        cuts,xy=cells(a,b);x,y=xy.T
        bad=(e.cost[y,x]==0)|(e.cost[y,x]>=16)|(e.terrain[y,x]>=4)
        tx=x//T*T;ty=y//T*T
        i=0
        while i<len(x):
            key=(int(x[i]),int(y[i]),1,64)
            if key not in e.used:key=(int(tx[i]),int(ty[i]),T,S)
            if key not in e.used:
                if bad[i] and (cuts[i+1]-cuts[i])*np.linalg.norm(diff)>1e-7:errors.append((idx,cuts[i],cuts[i+1]))
                i+=1;continue
            ox,oy,n,s=key;j=i+1
            while j<len(x) and ox<=x[j]<ox+n and oy<=y[j]<oy+n:
                if s!=64 and (int(x[j]),int(y[j]),1,64) in e.used:break
                j+=1
            low,high=cuts[i],cuts[j]
            origin=np.array([ox,oy]);t,c=cells((a+low*diff-origin)*s,(a+high*diff-origin)*s)
            c=np.clip(c,0,n*s-1);blocked=e.used[key][c[:,1],c[:,0]]!=0
            for k in np.flatnonzero(blocked & (np.diff(t)*(high-low)*np.linalg.norm(diff)>1e-7)):
                errors.append((idx,low+t[k]*(high-low),low+t[k+1]*(high-low)))
            i=j
    return errors

