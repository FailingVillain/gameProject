import sys,os
sys.path.insert(0,r"C:/gameProject/gt-develop/gt-house-a2/game-tools/image")
from house_sheet import frames, GRASS
from PIL import Image, ImageDraw
R=r"C:/gameProject/gt-develop/gt-house-a2/render_a2"
rows=[]
for i in range(1,10):
    n=f"ko-giwa-0{i}"
    lay,fa=frames(R,n,"@64"); _,fb=frames(R,n+"-a","@64")
    rows.append((n,lay.get("label",""),[fa[0],fb[0],fa[1]],["tone a","tone b (-a)","ruin"]))
W=max(f.size[0] for r in rows for f in r[2]); H=max(f.size[1] for r in rows for f in r[2])
cols=3; pad=18
img=Image.new("RGBA",(cols*W+20,len(rows)*(H+pad)+10),GRASS); d=ImageDraw.Draw(img)
for ri,(n,lab,fs,names) in enumerate(rows):
    y=ri*(H+pad)+8
    for ci,f in enumerate(fs):
        x=ci*W+10
        img.alpha_composite(f,(x,y+pad)); d.text((x+2,y),f"{n} {names[ci]}",fill=(255,255,160))
os.makedirs(r"C:/gameProject/gt-develop/gt-house-a2/korean-division/docs/art_review",exist_ok=True)
img.save(r"C:/gameProject/gt-develop/gt-house-a2/korean-division/docs/art_review/housegen_a2.png"); print(img.size)
