"""Placeholder button icons for Shape of Memory (Assets/ShadowWorkshop/PicoFinal): white shapes on transparent,
drawn at 4x and downsampled so the edges stay smooth. The game tints them; replace any PNG with real art of the
same name (keep it white on transparent) and rebuild the scene.
Run: python Tools/ShadowArt/build_icons.py   (Pillow). Then MetaHackthone > Pico Final > Create or rebuild scene.
"""
from pathlib import Path
import math
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ShadowWorkshop/PicoFinal/Art/Icons'
SIZE,SCALE=128,4
N=SIZE*SCALE
W=int(N*.085)  # stroke width

def canvas():
    image=Image.new('RGBA',(N,N),(255,255,255,0))
    return image,ImageDraw.Draw(image)

def at(x,y): return (x*N,y*N)

def arrow_arc(d,start,end,clockwise,radius=.30):
    # Circular arrow: an arc from start to end (degrees, PIL's clockwise-from-+x), arrowhead at end.
    c=.5;box=[at(c-radius,c-radius),at(c+radius,c+radius)]
    d.arc([box[0],box[1]],start,end,fill='white',width=W)
    a=math.radians(end if clockwise else start)
    tip=(c+radius*math.cos(a),c+radius*math.sin(a))
    tangent=(-math.sin(a),math.cos(a)) if clockwise else (math.sin(a),-math.cos(a))
    normal=(math.cos(a),math.sin(a));h,s=.22,.14
    p=(tip[0]+tangent[0]*h*.75,tip[1]+tangent[1]*h*.75)
    d.polygon([at(*p),at(tip[0]+normal[0]*s-tangent[0]*h*.3,tip[1]+normal[1]*s-tangent[1]*h*.3),at(tip[0]-normal[0]*s-tangent[0]*h*.3,tip[1]-normal[1]*s-tangent[1]*h*.3)],fill='white')

def play(d): d.polygon([at(.32,.22),at(.32,.78),at(.78,.5)],fill='white')
def next_(d): d.polygon([at(.24,.22),at(.24,.78),at(.66,.5)],fill='white');d.rectangle([at(.68,.22),at(.78,.78)],fill='white')
def replay(d): arrow_arc(d,-210,60,False)
def reset(d): arrow_arc(d,-60,210,True);d.ellipse([at(.43,.43),at(.57,.57)],fill='white')
def levels(d):
    for x in (.20,.54):
        for y in (.20,.54): d.rounded_rectangle([at(x,y),at(x+.26,y+.26)],radius=N*.04,fill='white')
def home(d):
    d.polygon([at(.5,.16),at(.86,.5),at(.76,.5),at(.76,.84),at(.24,.84),at(.24,.5),at(.14,.5)],fill='white')
    d.rectangle([at(.43,.62),at(.57,.84)],fill=(255,255,255,0))
def recenter(d):
    d.ellipse([at(.30,.30),at(.70,.70)],outline='white',width=W)
    for a,b in (((.5,.08),(.5,.26)),((.5,.74),(.5,.92)),((.08,.5),(.26,.5)),((.74,.5),(.92,.5))): d.line([at(*a),at(*b)],fill='white',width=W)
def chevron(d,up):
    y0,y1=(.62,.36) if up else (.38,.64)
    d.line([at(.22,y0),at(.5,y1),at(.78,y0)],fill='white',width=int(W*1.3),joint='curve')
def hint(d):
    d.ellipse([at(.28,.12),at(.72,.56)],fill='white')
    d.polygon([at(.36,.48),at(.64,.48),at(.60,.66),at(.40,.66)],fill='white')
    d.rectangle([at(.39,.70),at(.61,.76)],fill='white');d.rectangle([at(.42,.80),at(.58,.86)],fill='white')
def menu(d):
    for y in (.27,.47,.67): d.rounded_rectangle([at(.18,y),at(.82,y+.09)],radius=N*.03,fill='white')
def back(d):
    d.line([at(.26,.5),at(.80,.5)],fill='white',width=W)
    d.polygon([at(.16,.5),at(.42,.26),at(.42,.74)],fill='white')
def lock(d):
    d.arc([at(.32,.14),at(.68,.56)],180,360,fill='white',width=W);d.line([at(.32+.0425,.35),at(.32+.0425,.48)],fill='white',width=W);d.line([at(.68-.0425,.35),at(.68-.0425,.48)],fill='white',width=W)
    d.rounded_rectangle([at(.24,.46),at(.76,.86)],radius=N*.05,fill='white')

ICONS={'play':play,'next':next_,'replay':replay,'reset':reset,'levels':levels,'home':home,'recenter':recenter,
       'up':lambda d:chevron(d,True),'down':lambda d:chevron(d,False),'hint':hint,'menu':menu,'back':back,'lock':lock}

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for name,draw in ICONS.items():
        image,d=canvas();draw(d)
        image.resize((SIZE,SIZE),Image.LANCZOS).save(OUT/f'{name}.png')
    print(f'{len(ICONS)} icons -> {OUT}')

if __name__=='__main__': main()
