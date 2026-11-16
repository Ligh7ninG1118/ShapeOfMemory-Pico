"""Placeholder button icons for Shape of Memory (Assets/ShadowWorkshop/PicoFinal): white shapes on transparent,
drawn at 4x and downsampled so the edges stay smooth. The game tints them; replace any PNG with real art of the
same name (keep it white on transparent) and rebuild the scene.
Run: python Tools/ShadowArt/build_icons.py [name ...]   (Pillow; names: only those). Then MetaHackthone > Pico Final >
Create or rebuild scene (or add the new PNG to MemoryGame.Icons in the scene).
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

def quit_(d):
    # The power symbol: a ring open at the top, a bar down into the gap.
    d.arc([at(.20,.24),at(.80,.84)],-55,235,fill='white',width=W)
    d.line([at(.5,.12),at(.5,.48)],fill='white',width=W)

def pause(d):
    for x in (.27,.57): d.rounded_rectangle([at(x,.22),at(x+.16,.78)],radius=N*.03,fill='white')
def gear(d):
    # Eight teeth round a ring with a hole.
    for k in range(8):
        a=math.radians(k*45);c,s=math.cos(a),math.sin(a);u,v=-s,c;r0,r1,w=.24,.40,.075
        d.polygon([at(.5+c*r0+u*w,.5+s*r0+v*w),at(.5+c*r1+u*w*.8,.5+s*r1+v*w*.8),at(.5+c*r1-u*w*.8,.5+s*r1-v*w*.8),at(.5+c*r0-u*w,.5+s*r0-v*w)],fill='white')
    d.ellipse([at(.21,.21),at(.79,.79)],fill='white')
    d.ellipse([at(.38,.38),at(.62,.62)],fill=(255,255,255,0))
def book(d):
    # An open book seen from above: two pages curving up from the spine.
    for side in (-1,1):
        x0,x1=.5,.5+side*.36
        d.polygon([at(x0,.34),at((x0+x1)/2,.26),at(x1,.30),at(x1,.74),at((x0+x1)/2,.70),at(x0,.78)],fill='white')
    d.line([at(.5,.32),at(.5,.80)],fill=(255,255,255,0),width=int(W*.55))
def heart(d):
    d.ellipse([at(.16,.22),at(.52,.58)],fill='white');d.ellipse([at(.48,.22),at(.84,.58)],fill='white')
    d.polygon([at(.18,.46),at(.82,.46),at(.5,.84)],fill='white')
def note(d):
    d.ellipse([at(.18,.62),at(.40,.80)],fill='white');d.ellipse([at(.56,.56),at(.78,.74)],fill='white')
    d.rectangle([at(.33,.22),at(.40,.72)],fill='white');d.rectangle([at(.71,.16),at(.78,.66)],fill='white')
    d.polygon([at(.33,.22),at(.78,.16),at(.78,.28),at(.33,.34)],fill='white')
def mute(d):
    note(d);d.line([at(.14,.14),at(.86,.86)],fill=(255,255,255,0),width=int(W*2.2));d.line([at(.16,.16),at(.84,.84)],fill='white',width=W)
def side_chevron(d,left):
    x0,x1=(.62,.36) if left else (.38,.64)
    d.line([at(x0,.22),at(x1,.5),at(x0,.78)],fill='white',width=int(W*1.3),joint='curve')
def glyph(d,text,font,size):
    # A letter from a system font (placeholders: real art can replace the PNG).
    from PIL import ImageFont
    face=ImageFont.truetype(font,int(N*size));box=d.textbbox((0,0),text,font=face)
    d.text(((N-(box[2]-box[0]))/2-box[0],(N-(box[3]-box[1]))/2-box[1]),text,font=face,fill='white')

ICONS={'play':play,'next':next_,'replay':replay,'reset':reset,'levels':levels,'home':home,'recenter':recenter,
       'up':lambda d:chevron(d,True),'down':lambda d:chevron(d,False),'hint':hint,'menu':menu,'back':back,'lock':lock,
       'quit':quit_,'pause':pause,'gear':gear,'book':book,'heart':heart,'music':note,'mute':mute,
       'left':lambda d:side_chevron(d,True),'right':lambda d:side_chevron(d,False),
       'lang-en':lambda d:glyph(d,'A','C:/Windows/Fonts/georgiab.ttf',.62),'lang-zh':lambda d:glyph(d,'文','C:/Windows/Fonts/msyhbd.ttc',.58)}

def main(names=None):
    OUT.mkdir(parents=True,exist_ok=True)
    for name,draw in ICONS.items():
        if names and name not in names: continue
        image,d=canvas();draw(d)
        image.resize((SIZE,SIZE),Image.LANCZOS).save(OUT/f'{name}.png')
    print(f'{len(names or ICONS)} icons -> {OUT}')

if __name__=='__main__':
    import sys
    main(sys.argv[1:])
