"""Printed face for the Torch2 'Boarding pass' prop (Assets/ShadowWorkshop/Art/Torch2/Meshes/Boarding pass.asset).
The card is 75 x 185 mm; EverydayImporter gives it front-on UVs over its full outline (both faces read the right way
round), so this image covers the whole card, top of the image at the top of the card. The blue header lines up with
the mesh's raised band (card y 0.05..0.09 m) and runs full bleed so the card's edges stay blue there.
Drawn at 2x and downsampled. Edit the TRIP values below to change what's printed.
Run: python Tools/ShadowArt/build_boarding_pass.py   (Pillow). Unity reimports the PNG on focus / Ctrl+R.
"""
from pathlib import Path
import math, random
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ShadowWorkshop/Art/Torch2/Textures/boarding pass.png'
W,H=1024,2528          # 75 x 185 mm, ~13.7 px/mm; both multiples of 4 so the texture block-compresses
S=2                    # supersampling
HEADER=580             # card y 0.05 m (bottom of the band) in image px from the top

TRIP=dict(airline='BON VOYAGE',from_code='CDG',from_city='PARIS',from_term='TERMINAL 2E',to_code='HND',to_city='TOKYO',
          to_term='HANEDA T3',duration='13H 55M',passenger='VOYAGER / BON',flight='BV 026',date='14 NOV',departs='10:25',
          gate='B12',boarding='09:40',seat='12A',group='2',zone='C',seq='087',cls='ECONOMY',cls_code='Y',ticket='0572 6031 4428 9')

PAPER=(250,248,242);INK=(30,36,50);SOFT=(72,80,98);MUTED=(132,140,154);RULE=(222,218,208)
BLUE=(33,86,170);BLUE_TOP=(22,62,134);BLUE_BOT=(40,100,186);PALE=(192,212,244);ORANGE=(240,136,40);WATER=(233,238,246)

FONTS='C:/Windows/Fonts/'
def font(size,weight='Bold'):
    try:
        f=ImageFont.truetype(FONTS+'bahnschrift.ttf',size*S)
        f.set_variation_by_name(weight)
        return f
    except Exception:
        return ImageFont.truetype(FONTS+('segoeuib.ttf' if weight!='Regular' else 'segoeui.ttf'),size*S)
def mono(size): return ImageFont.truetype(FONTS+'consola.ttf',size*S)

img=Image.new('RGB',(W*S,H*S),PAPER)
d=ImageDraw.Draw(img)
def P(*v): return [x*S for x in v]

def width(text,f,track=0): return sum(f.getlength(c) for c in text)/S+track*(len(text)-1)
def text(x,y,s,f,fill,track=0,align='l'):
    # y is the baseline; track is extra letter spacing in px.
    w=width(s,f,track)
    if align=='r':x-=w
    elif align=='c':x-=w/2
    if not track:
        d.text((x*S,y*S),s,font=f,fill=fill,anchor='ls');return
    for c in s:
        d.text((x*S,y*S),c,font=f,fill=fill,anchor='ls');x+=f.getlength(c)/S+track

# Top view of a plane, nose along +x, unit length.
PLANE_HALF=[(.50,0),(.46,.035),(.38,.05),(.10,.05),(-.10,.46),(-.19,.46),(-.07,.05),(-.34,.045),(-.44,.19),(-.50,.19),(-.45,.03),(-.50,0)]
PLANE=PLANE_HALF+[(x,-y) for x,y in reversed(PLANE_HALF[1:-1])]
def plane(cx,cy,size,angle,fill,draw=None):
    a=math.radians(angle);c,s=math.cos(a),math.sin(a)
    (draw or d).polygon([((cx+(x*c-y*s)*size)*S,(cy+(x*s+y*c)*size)*S) for x,y in PLANE],fill=fill)

def label(x,y,s,align='l',fill=MUTED): text(x,y,s,font(24,'SemiBold'),fill,track=5,align=align)
def value(x,y,s,size=58,fill=INK,align='l'): text(x,y,s,font(size),fill,track=1,align=align)
def rule(y): d.rectangle(P(64,y,960,y+3),fill=RULE)

# --- Header: gradient, logo, title, route arc -------------------------------------------------------------
for y in range(HEADER*S):
    t=y/(HEADER*S);d.line([(0,y),(W*S,y)],fill=tuple(round(a+(b-a)*t) for a,b in zip(BLUE_TOP,BLUE_BOT)))
d.ellipse(P(68,68,180,180),fill='white')
plane(124,124,84,-40,BLUE)
text(206,120,TRIP['airline'],font(64),'white',track=2)
text(208,166,'AIRLINES',font(28,'SemiBold'),PALE,track=12)

title=font(76);text(64,326,'BOARDING PASS',title,'white',track=3)
pill=font(28);pw=width(TRIP['cls'],pill,4)+56
d.rounded_rectangle(P(960-pw,276,960,330),radius=27*S,outline='white',width=3*S)
text(960-pw/2,314,TRIP['cls'],pill,'white',track=4,align='c')
text(64,380,"CARTE D'EMBARQUEMENT",font(26,'SemiBold'),PALE,track=6)

# Dashed flight arc across the bottom of the header, drawn on its own layer for transparency.
layer=Image.new('RGBA',img.size,(0,0,0,0));ld=ImageDraw.Draw(layer)
p0,c,p2=(96,520),(512,392),(928,520)
def q(t): return tuple((1-t)**2*a+2*(1-t)*t*b+t*t*e for a,b,e in zip(p0,c,p2))
n=44
for i in range(n):
    if i%2:continue
    a,b=q(i/n),q((i+1)/n);ld.line([(a[0]*S,a[1]*S),(b[0]*S,b[1]*S)],fill=(255,255,255,150),width=4*S)
ld.ellipse(P(p0[0]-10,p0[1]-10,p0[0]+10,p0[1]+10),fill=(255,255,255,230))
ld.ellipse(P(p2[0]-11,p2[1]-11,p2[0]+11,p2[1]+11),outline=(255,255,255,230),width=4*S)
mid=q(.5);plane(mid[0],mid[1],60,0,(255,255,255,255),ld)
img.paste(layer,(0,0),layer);d=ImageDraw.Draw(img)
text(p0[0],562,TRIP['from_code'],font(24,'SemiBold'),PALE,track=4,align='c')
text(p2[0],562,TRIP['to_code'],font(24,'SemiBold'),PALE,track=4,align='c')
d.rectangle(P(0,HEADER,W,HEADER+10),fill=ORANGE)

# --- Route ---------------------------------------------------------------------------------------------------
globe=Image.new('RGBA',img.size,(0,0,0,0));gd=ImageDraw.Draw(globe)
gx,gy,gr=512,795,190
gd.ellipse(P(gx-gr,gy-gr,gx+gr,gy+gr),outline=WATER,width=4*S)
for k in (-2,-1,0,1,2):
    h=gr*math.sin(math.radians(k*30));r=gr*math.cos(math.radians(k*30))
    gd.line(P(gx-r,gy+h,gx+r,gy+h),fill=WATER,width=3*S)
for k in (.3,.65,1):
    gd.ellipse(P(gx-gr*k,gy-gr,gx+gr*k,gy+gr),outline=WATER,width=3*S)
img.paste(globe,(0,0),globe);d=ImageDraw.Draw(img)

label(64,664,'FROM');label(960,664,'TO',align='r')
code=font(164)
text(60,846,TRIP['from_code'],code,INK,track=2);text(964,846,TRIP['to_code'],code,INK,track=2,align='r')
plane(512,776,92,0,BLUE)
text(512,846,TRIP['duration'],font(24,'SemiBold'),MUTED,track=3,align='c')
city=font(38,'SemiBold')
text(64,906,TRIP['from_city'],city,SOFT,track=3);text(960,906,TRIP['to_city'],city,SOFT,track=3,align='r')
label(64,948,TRIP['from_term']);label(960,948,TRIP['to_term'],align='r')
rule(1000)

# --- Details grid ---------------------------------------------------------------------------------------------
COL=(64,368,672)
label(COL[0],1072,'PASSENGER');value(COL[0],1142,TRIP['passenger'])
label(COL[2],1072,'SEQ');value(COL[2],1142,TRIP['seq'])
for x,(l,v) in zip(COL,(('FLIGHT',TRIP['flight']),('DATE',TRIP['date']),('DEPARTS',TRIP['departs']))):
    label(x,1230,l);value(x,1300,v)
label(COL[0],1408,'GATE');value(COL[0],1486,TRIP['gate'],size=70,fill=BLUE)
label(COL[1],1408,'BOARDING');value(COL[1],1486,TRIP['boarding'],size=70)
d.rounded_rectangle(P(COL[2],1366,960,1514),radius=22*S,fill=ORANGE)
label(COL[2]+26,1408,'SEAT',fill=(255,236,214));value(COL[2]+24,1494,TRIP['seat'],size=84,fill='white')
for x,(l,v) in zip(COL,(('GROUP',TRIP['group']),('ZONE',TRIP['zone']),('CLASS',TRIP['cls_code']))):
    label(x,1584,l);value(x,1650,v)
rule(1690)

# --- Barcode ---------------------------------------------------------------------------------------------------
rng=random.Random(TRIP['ticket'])
x=64.0;bars=[]
guard=[2,1,1,2,1,1]
pattern=guard+[rng.choice((1,1,1,2,2,3,4)) for _ in range(140)]+guard[::-1]
total=sum(pattern);mod=(960-64)/total
for i,m in enumerate(pattern):
    if i%2==0:bars.append((x,x+m*mod))
    x+=m*mod
for a,b in bars:d.rectangle([a*S,1722*S,b*S-1,1880*S],fill=INK)
text(512,1928,TRIP['ticket'],mono(30),SOFT,track=9,align='c')

# --- Perforation -----------------------------------------------------------------------------------------------
for x in range(18,W,24):d.ellipse(P(x-5,1980,x+5,1990),fill=(206,201,190))

# --- Stub ------------------------------------------------------------------------------------------------------
text(64,2060,'BOARDING PASS',font(28),BLUE,track=6)
text(960,2060,TRIP['airline']+' AIRLINES',font(22,'SemiBold'),MUTED,track=3,align='r')
small=font(96);text(64,2172,TRIP['from_code'],small,INK,track=1)
x=64+width(TRIP['from_code'],small,1)+24;plane(x+34,2136,62,0,BLUE)
text(x+86,2172,TRIP['to_code'],small,INK,track=1)
text(960,2172,TRIP['passenger'],font(34,'SemiBold'),SOFT,track=2,align='r')
for x,(l,v) in zip((64,288,512,736),(('FLIGHT',TRIP['flight']),('DATE',TRIP['date']),('GATE',TRIP['gate']),('SEAT',TRIP['seat']))):
    label(x,2244,l);value(x,2302,v,size=48,fill=ORANGE if l=='SEAT' else INK)
text(64,2370,f"BOARDING {TRIP['boarding']}  ·  GROUP {TRIP['group']}  ·  SEQ {TRIP['seq']}",font(26,'SemiBold'),MUTED,track=3)
d.rectangle(P(0,2420,W,H),fill=BLUE)
text(512,2486,'HAVE A WONDERFUL TRIP',font(30),'white',track=9,align='c')

# --- Downsample, then a little paper grain -----------------------------------------------------------------------
img=img.resize((W,H),Image.LANCZOS)
grain=Image.merge('RGB',[Image.effect_noise((W,H),40)]*3)
img=Image.blend(img,grain,.03)
OUT.parent.mkdir(parents=True,exist_ok=True)
img.save(OUT)
print('wrote',OUT)
