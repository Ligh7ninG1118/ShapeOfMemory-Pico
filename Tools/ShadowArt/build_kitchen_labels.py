"""Printed surfaces for the GoodMorning kitchen props built by KitchenPropsBuilder (Assets/ShadowWorkshop/Editor):
  salt jar label.png  1024x416  wraps once round the salt jar's glazed body (u round, v up the label band);
                                the whole glaze uses it, so its background is the speckled cream glaze.
  sugar box.png       1024x1024 atlas, one region per face; KitchenPropsBuilder.SugarRegions must match REGIONS.
Drawn at 2x and downsampled. Run: python Tools/ShadowArt/build_kitchen_labels.py   (Pillow), then
MetaHackthone > Shadow Workshop > Build kitchen props (or let Unity reimport the PNGs).
"""
from pathlib import Path
import random
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ShadowWorkshop/Art/Torch2/Textures'
S=2
FONTS='C:/Windows/Fonts/'
def serif(size): return ImageFont.truetype(FONTS+'georgiab.ttf',size*S)
def sans(size,weight='Bold'):
    f=ImageFont.truetype(FONTS+'bahnschrift.ttf',size*S)
    try: f.set_variation_by_name(weight)
    except Exception: pass
    return f

def width(s,f,track=0): return sum(f.getlength(c) for c in s)/S+track*(len(s)-1)
def text(d,x,y,s,f,fill,track=0,align='l'):
    # x, y in final px; y is the baseline.
    w=width(s,f,track)
    if align=='c':x-=w/2
    elif align=='r':x-=w
    for c in s:
        d.text((x*S,y*S),c,font=f,fill=fill,anchor='ls');x+=f.getlength(c)/S+track
def P(*v): return [a*S for a in v]

# --- Salt jar label -------------------------------------------------------------------------------------------------
GLAZE=(244,239,226);COBALT=(36,72,140);SPECK=(118,102,86)
def salt_label():
    W,H=1024,416
    img=Image.new('RGB',(W*S,H*S),GLAZE);d=ImageDraw.Draw(img)
    rng=random.Random(5)
    for _ in range(2600):
        x,y,r=rng.uniform(0,W),rng.uniform(0,H),rng.uniform(.5,1.4)
        d.ellipse(P(x-r,y-r,x+r,y+r),fill=SPECK)
    # Plain glaze in the edge rows: the jar's foot and rim stretch them (clamped), where specks would streak.
    d.rectangle(P(0,0,W,8),fill=GLAZE);d.rectangle(P(0,H-8,W,H),fill=GLAZE)
    for y0,y1 in ((36,44),(52,55),(H-55,H-52),(H-44,H-36)):
        d.rectangle(P(0,y0,W,y1),fill=COBALT)
    for cx in (256,768):
        text(d,cx,250,'SALT',serif(124),COBALT,track=10,align='c')
        text(d,cx,306,'SEL DE MER',sans(26,'SemiBold'),COBALT,track=9,align='c')
    # Small diamond-and-dots motif between the words (at 0 = 1024 it is split across the seam).
    for cx in (0,512,1024):
        d.polygon([(cx*S,(208-14)*S),((cx+10)*S,208*S),(cx*S,(208+14)*S),((cx-10)*S,208*S)],fill=COBALT)
        for dx in (-26,26): d.ellipse(P(cx+dx-4,204,cx+dx+4,212),fill=COBALT)
    img.resize((W,H),Image.LANCZOS).save(OUT/'salt jar label.png')

# --- Sugar box atlas ------------------------------------------------------------------------------------------------
# Face regions in image px (x, y, w, h), top-left origin. Front/back 110 x 64 mm, top/bottom 110 x 64, sides 64 x 64.
REGIONS=dict(front=(8,8,500,291),back=(516,8,500,291),top=(8,307,500,291),bottom=(516,307,500,291),
             left=(8,606,291,291),right=(307,606,291,291))
PAPER=(250,248,243);RED=(198,48,42);INK=(70,64,62);GREY=(150,146,140)

def cube(d,cx,cy,s):
    # Isometric sugar cube centred at cx,cy with edge s (px).
    h=s*.5;top=[(cx,cy-s),(cx+s*.87,cy-h),(cx,cy),(cx-s*.87,cy-h)]
    left=[(cx-s*.87,cy-h),(cx,cy),(cx,cy+s),(cx-s*.87,cy+h)]
    right=[(cx,cy),(cx+s*.87,cy-h),(cx+s*.87,cy+h),(cx,cy+s)]
    for poly,fill in ((left,(226,224,226)),(right,(203,201,206)),(top,(255,255,255))):
        d.polygon([(x*S,y*S) for x,y in poly],fill=fill,outline=(176,172,178),width=2*S)
    rng=random.Random(int(cx*7+cy))
    for _ in range(18):   # crystal sparkle
        u,v=rng.random(),rng.random();x=cx+(u-v)*s*.8;y=cy-s*.5+(u+v-1)*s*.4
        d.ellipse(P(x-1,y-1,x+1,y+1),fill=(236,234,238))

def barcode(d,x,y,w,h,seed):
    rng=random.Random(seed);xx=x
    while xx<x+w-3:
        bw=rng.choice((1.5,1.5,2.5,3.5));
        if rng.random()<.6:d.rectangle(P(xx,y,xx+bw,y+h),fill=INK)
        xx+=bw+rng.choice((1.5,2.5))

def face(name,w,h):
    img=Image.new('RGB',(w*S,h*S),PAPER);d=ImageDraw.Draw(img)
    if name=='front':
        d.rectangle(P(0,0,w,118),fill=RED)
        text(d,w/2,92,'SUGAR',serif(78),'white',track=8,align='c')
        d.rectangle(P(10,128,w-10,h-10),outline=RED,width=2*S)
        for i,(dx,dy) in enumerate(((-58,0),(0,-14),(58,0),(-29,46),(29,46))): cube(d,w/2+dx,190+dy,30)
        text(d,26,h-24,'CUBES',sans(30),RED,track=8)
        text(d,w-26,h-24,'500 g',sans(30),RED,track=2,align='r')
    elif name=='back':
        d.rectangle(P(0,0,w,64),fill=RED)
        text(d,w/2,44,'SUGAR CUBES',serif(32),'white',track=6,align='c')
        for i,line in enumerate(('Pure cane sugar, pressed into cubes.','One or two cubes per cup.','Store in a cool, dry place.','Best before 11 / 2027')):
            text(d,30,108+i*36,line,sans(22,'SemiBold'),INK,track=1)
        barcode(d,w-150,h-80,120,52,3);text(d,w-90,h-14,'3 760 0112 2026',sans(13,'SemiBold'),INK,track=1,align='c')
    elif name in ('left','right'):
        d.rectangle(P(0,0,w,92),fill=RED)
        text(d,w/2,64,'SUGAR',serif(44),'white',track=5,align='c')
        cube(d,w/2,170,40)
        text(d,w/2,h-22,'500 g  ·  CUBES',sans(20),RED,track=4,align='c')
    elif name=='top':
        d.rectangle(P(0,0,w,h),outline=RED,width=10*S)
        for x in range(30,w-30,18): d.line(P(x,h/2,x+9,h/2),fill=GREY,width=2*S)
        text(d,w/2,h/2-30,'OPEN HERE',sans(34),RED,track=10,align='c')
        d.polygon([((w/2-12)*S,(h/2+26)*S),((w/2+12)*S,(h/2+26)*S),(w/2*S,(h/2+44)*S)],fill=RED)
        text(d,w/2,h-34,'SUGAR  ·  CUBES',serif(26),RED,track=6,align='c')
    elif name=='bottom':
        text(d,w/2,80,'MADE FOR GOOD MORNINGS',sans(20,'SemiBold'),GREY,track=4,align='c')
        barcode(d,w/2-80,120,160,70,9)
    return img.resize((w,h),Image.LANCZOS)

def sugar_box():
    atlas=Image.new('RGB',(1024,1024),PAPER)
    for name,(x,y,w,h) in REGIONS.items():
        f=face(name,w,h)
        # 4 px bleed of the region's edge colours so mipmaps don't pull in the neighbours.
        bleed=f.resize((w+8,h+8),Image.NEAREST);atlas.paste(bleed,(x-4,y-4));atlas.paste(f,(x,y))
    atlas.save(OUT/'sugar box.png')

OUT.mkdir(parents=True,exist_ok=True)
salt_label();sugar_box()
print('wrote',OUT/'salt jar label.png','and',OUT/'sugar box.png')
