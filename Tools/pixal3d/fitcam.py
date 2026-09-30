import numpy as np, json
from PIL import Image, ImageDraw
d=np.load('tex/mesh.npz'); V=d['v'].astype(np.float64); T=d['t']
m=np.array(Image.open('tex/mask.png').convert('L').resize((256,384)))>128
W,H=256,384
mn,mx=V.min(0),V.max(0)
def proj(p):
    sx,sz,tx,tz,k,kx,kz=p
    y=(V[:,1]-mn[1])/(mx[1]-mn[1])       # 0 = front(-Y) .. 1 = back
    den=1+k*y
    u=((V[:,0]-mn[0])/(mx[0]-mn[0])*sx+tx+kx*y)/den
    v=((mx[2]-V[:,2])/(mx[2]-mn[2])*sz+tz+kz*y)/den
    return u,v
def iou(p):
    u,v=proj(p); im=Image.new('L',(W,H),0); dr=ImageDraw.Draw(im)
    P=np.stack([u*W,v*H],1)
    for tri in T[::2]:            # half the tris is plenty for silhouette
        dr.polygon([tuple(P[i]) for i in tri],fill=255)
    s=np.array(im)>128
    return (s&m).sum()/max(1,(s|m).sum())
p=np.array([1.1169,0.9319,-0.0025,0.01,0.103,0.0,0.0]); best=iou(p); print('start',best)
step=np.array([0.03,0.03,0.02,0.02,0.08,0.03,0.03])
for it in range(60):
    imp=False
    for i in range(7):
        for s in (+1,-1):
            q=p.copy(); q[i]+=s*step[i]; r=iou(q)
            if r>best: best,p,imp=r,q,True
    if not imp: step*=0.5
    if step[0]<0.002: break
print('best',best,list(p)); json.dump(list(p),open('tex/cam.json','w'))
