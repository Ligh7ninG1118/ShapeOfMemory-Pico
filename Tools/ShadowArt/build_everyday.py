"""Everyday-object silhouette levels: A LIFE (elderly man with a walking stick), ITALIA and NIPPON (maps).
Builds rough procedural meshes for everyday objects and a designed solution pose for each piece.
Unity rasterises the imported meshes at their solution poses at runtime, so the target is always
exactly reachable and a replaced mesh asset automatically updates its level's target.
Coordinates match Unity: metres, left-handed, y up, the front light shines along +z,
Euler (x,y,z) degrees compose as Ry*Rx*Rz. Solutions sit on the workbench grid (5 mm / 15 deg).
Run: python Tools/ShadowArt/build_everyday.py [--check]   (numpy + Pillow)
Writes Assets/ShadowWorkshop/Art/Everyday/EverydayObjects.json, then use
MetaHackthone > Shadow Workshop > Import everyday objects. Previews go to Logs/ShadowArt.
"""
from pathlib import Path
import json, math, sys
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ShadowWorkshop/Art/Everyday/EverydayObjects.json'
LOGS=ROOT/'Logs/ShadowArt'
SIZE,SPAN,CENTER=128,.56,np.array([0,.29,0])

# ---------------------------------------------------------------- materials
MATERIALS={
 'ball red':(.86,.22,.20,.55),'ball yellow':(.97,.80,.25,.55),'ball blue':(.22,.48,.85,.55),
 'book cover':(.55,.20,.16,.25),'book pages':(.93,.90,.80,.10),'book band':(.86,.70,.32,.45),
 'deck':(.20,.62,.58,.30),'grip':(.12,.12,.13,.05),'wheel':(.95,.88,.55,.35),'metal':(.72,.74,.76,.75),
 'laptop shell':(.70,.72,.75,.65),'laptop screen':(.10,.16,.24,.85),'laptop keys':(.16,.17,.19,.30),
 'mouse body':(.86,.86,.84,.55),'mouse dark':(.18,.19,.20,.40),
 'umbrella cloth':(.18,.26,.48,.30),'umbrella shaft':(.15,.15,.16,.60),'umbrella wood':(.52,.32,.17,.40),
 'cheese rind':(.93,.74,.36,.25),'cheese mark':(.60,.38,.16,.25),
 'marble':(.94,.92,.86,.35),'marble shade':(.82,.79,.72,.30),
 'oil glass':(.33,.50,.22,.70),'oil gold':(.86,.68,.28,.70),
 'chili red':(.82,.10,.10,.75),'stem green':(.27,.55,.20,.40),
 'crust':(.80,.55,.25,.20),'cheese':(.98,.84,.42,.35),'pepperoni':(.70,.16,.12,.45),
 'tin label':(.80,.16,.14,.40),'tin metal':(.75,.77,.79,.80),'tin green':(.20,.52,.27,.40),
 'rice':(.96,.96,.93,.20),'nori':(.07,.13,.09,.35),
 'kokeshi wood':(.93,.80,.58,.30),'kokeshi red':(.80,.15,.17,.45),'kokeshi hair':(.06,.06,.07,.50),
 'daruma red':(.84,.13,.12,.60),'daruma face':(.97,.95,.90,.40),'daruma gold':(.92,.73,.28,.60),
 'train white':(.95,.96,.97,.70),'train blue':(.12,.27,.62,.60),'train window':(.08,.10,.14,.85),
 'bowl outer':(.17,.22,.40,.55),'bowl inner':(.90,.88,.82,.45),'udon':(.98,.94,.78,.30),
 'neko white':(.97,.96,.94,.55),'neko red':(.82,.15,.15,.55),'neko gold':(.93,.74,.28,.70),
}

# ---------------------------------------------------------------- geometry helpers
def rot(e):
    x,y,z=np.radians(e)
    rx=np.array([[1,0,0],[0,math.cos(x),-math.sin(x)],[0,math.sin(x),math.cos(x)]])
    ry=np.array([[math.cos(y),0,math.sin(y)],[0,1,0],[-math.sin(y),0,math.cos(y)]])
    rz=np.array([[math.cos(z),-math.sin(z),0],[math.sin(z),math.cos(z),0],[0,0,1]])
    return ry@rx@rz

class Mesh:
    def __init__(s,name): s.name=name;s.v=[];s.sub={}
    def add(s,part,mat,e=(0,0,0),t=(0,0,0),scale=(1,1,1)):
        v,tri=part;v=(np.asarray(v,float)*scale)@rot(e).T+t
        if np.prod(np.sign(scale))<0:tri=np.asarray(tri)[:,::-1]
        base=len(s.v);s.v.extend(v.tolist());s.sub.setdefault(mat,[]).extend((np.asarray(tri)+base).tolist());return s
    def arrays(s):
        v=np.array(s.v);tri=np.array([t for mat in s.sub for t in s.sub[mat]]);return v,tri
    def scaled(s,f):s.v=(np.array(s.v)*f).tolist();return s
    def centred(s):
        v=np.array(s.v);c=(v.min(0)+v.max(0))/2;s.v=(v-c).tolist();return s

def orient(v,tri):
    # Closed parts: make cross(b-a,c-a) point outward (positive signed volume).
    v=np.asarray(v);tri=np.asarray(tri);a,b,c=v[tri[:,0]],v[tri[:,1]],v[tri[:,2]]
    return (v,tri if np.einsum('ij,ij->',a,np.cross(b,c))>=0 else tri[:,::-1])

def triangulate(poly):
    # Ear clipping for a simple counter-clockwise polygon.
    idx=list(range(len(poly)));out=[];p=np.asarray(poly)
    def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    guard=0
    while len(idx)>3 and guard<10000:
        guard+=1
        for k in range(len(idx)):
            i,j,l=idx[k-1],idx[k],idx[(k+1)%len(idx)]
            if cross(p[i],p[j],p[l])<=1e-12:continue
            if any(cross(p[i],p[j],p[m])>=0 and cross(p[j],p[l],p[m])>=0 and cross(p[l],p[i],p[m])>=0 for m in idx if m not in(i,j,l)):continue
            out.append((i,j,l));idx.pop(k);break
    out.append(tuple(idx));return out

def ccw(poly):
    p=np.asarray(poly);area=np.sum(p[:,0]*np.roll(p[:,1],-1)-np.roll(p[:,0],-1)*p[:,1])
    return list(poly) if area>0 else list(poly)[::-1]

def extrude(poly,depth):
    # Polygon in xy, extruded along z; separate vertices per face for flat shading.
    poly=ccw(poly);n=len(poly);h=depth/2;v=[];t=[]
    faces=triangulate(poly)
    for z,flip in((-h,True),(h,False)):
        base=len(v);v+=[(x,y,z) for x,y in poly]
        t+=[(base+c,base+b,base+a) if flip else (base+a,base+b,base+c) for a,b,c in faces]
    for i in range(n):
        j=(i+1)%n;base=len(v);(x0,y0),(x1,y1)=poly[i],poly[j]
        v+=[(x0,y0,-h),(x1,y1,-h),(x1,y1,h),(x0,y0,h)];t+=[(base,base+1,base+2),(base,base+2,base+3)]
    return np.array(v),np.array(t)

def box(sx,sy,sz):return extrude([(-sx/2,-sy/2),(sx/2,-sy/2),(sx/2,sy/2),(-sx/2,sy/2)],sz)

def arc(cx,cy,r,a0,a1,n):return [(cx+r*math.cos(a),cy+r*math.sin(a)) for a in np.linspace(math.radians(a0),math.radians(a1),n)]

def rrect(w,h,r,n=5):
    return arc(w/2-r,h/2-r,r,0,90,n)+arc(-w/2+r,h/2-r,r,90,180,n)+arc(-w/2+r,-h/2+r,r,180,270,n)+arc(w/2-r,-h/2+r,r,270,360,n)

def lathe(profile,seg=16,a0=0,a1=360,caps=True):
    # Profile (radius,y) bottom to top around +y. Repeated points make a crease.
    full=abs(a1-a0)>=360;angles=np.radians(np.linspace(a0,a1,seg+1)[:seg if full else seg+1]);m=len(angles)
    v=[];t=[]
    for r,y in profile:v+=[(r*math.cos(a),y,r*math.sin(a)) for a in angles]
    for i in range(len(profile)-1):
        if profile[i]==profile[i+1]:continue
        for k in range(m if full else m-1):
            a=i*m+k;b=i*m+(k+1)%m;c=(i+1)*m+(k+1)%m;d=(i+1)*m+k
            if profile[i][0]>1e-9:t.append((a,d,c) if profile[i+1][0]>1e-9 else (a,d,b))
            if profile[i+1][0]>1e-9 and profile[i][0]>1e-9:t.append((a,c,b))
            elif profile[i+1][0]>1e-9:t.append((a,d,c))
    if caps and full:
        for end,(r,y) in ((0,profile[0]),(1,profile[-1])):
            if r<=1e-9:continue
            base=len(v);v.append((0,y,0));v+=[(r*math.cos(a),y,r*math.sin(a)) for a in angles]
            for k in range(m):t.append((base,base+1+k,base+1+(k+1)%m) if end==0 else (base,base+1+(k+1)%m,base+1+k))
    return np.array(v),np.array(t)

def sphere(r,seg=16,rings=10):
    return lathe([(r*math.sin(p),-r*math.cos(p)) for p in np.linspace(0,math.pi,rings+1)],seg,caps=False)

def tube(path,radii,seg=10):
    # Swept circle along a 3D path with end caps (parallel-transport frames).
    p=np.asarray(path,float);n=len(p);radii=np.broadcast_to(np.asarray(radii,float),(n,))
    tang=np.gradient(p,axis=0);tang/=np.linalg.norm(tang,axis=1,keepdims=True)
    ref=np.array([0,0,1.]) if abs(tang[0][2])<.9 else np.array([1.,0,0])
    u=np.cross(tang[0],ref);u/=np.linalg.norm(u);v=[];t=[]
    for i in range(n):
        if i>0:u=u-np.dot(u,tang[i])*tang[i];u/=np.linalg.norm(u)
        w=np.cross(tang[i],u)
        v+=[(p[i]+radii[i]*(math.cos(a)*u+math.sin(a)*w)).tolist() for a in np.linspace(0,2*math.pi,seg,endpoint=False)]
    for i in range(n-1):
        for k in range(seg):
            a=i*seg+k;b=i*seg+(k+1)%seg;c=(i+1)*seg+(k+1)%seg;d=(i+1)*seg+k;t+=[(a,b,c),(a,c,d)]
    for end in(0,n-1):
        base=len(v);v.append(p[end].tolist());v+=v[end*seg:end*seg+seg]
        t+=[(base,base+1+k,base+1+(k+1)%seg) if end==0 else (base,base+1+(k+1)%seg,base+1+k) for k in range(seg)]
    v,t=np.array(v),np.array(t)
    # Per-cap orientation is unknown until the frame is fixed; orient the closed whole.
    return orient(v,t)

# ---------------------------------------------------------------- everyday objects (local, bounds-centred)
def toy_ball():
    m=Mesh('Toy ball');v,t=sphere(.031,18,12);cols=['ball red','ball yellow','ball blue']
    # Beach-ball gores: assign each triangle by longitude.
    c=v[t].mean(1);g=(((np.arctan2(c[:,2],c[:,0])+math.pi)/(2*math.pi))*6).astype(int)%3
    for k in range(3):m.add((v,t[g==k]),cols[k])
    return m.centred()

def school_book():
    m=Mesh('School book')
    m.add(box(.082,.134,.024),'book pages',t=(.003,0,0))
    for z in(-.0135,.0135):m.add(box(.088,.142,.004),'book cover',t=(0,0,z))
    m.add(extrude(arc(0,0,.016,90,270,7),.142),'book cover',e=(90,0,0),t=(-.044,0,0),scale=(.5,1,1))
    for y in(-.042,.046):m.add(box(.010,.010,.034),'book band',t=(-.047,y,0))
    return m.centred()

def skateboard():
    m=Mesh('Skateboard')
    deck=rrect(.058,.205,.028,6)
    m.add(extrude(deck,.009),'deck');m.add(extrude(rrect(.050,.190,.024,6),.002),'grip',t=(0,0,-.0055))
    for y in(-.068,.068):
        m.add(box(.040,.012,.010),'metal',t=(0,y,.0095))
        for x in(-.025,.025):m.add(lathe([(.0095,-.0055),(.0095,.0055)],12),'wheel',e=(0,0,90),t=(x,y,.020))
    return m.centred()

def laptop(angle=105):
    m=Mesh('Laptop')
    # Hinge along z; the base extends along +x, the lid opens upward by `angle`.
    m.add(box(.080,.016,.104),'laptop shell',t=(.040,-.008,0));m.add(box(.064,.002,.086),'laptop keys',t=(.042,.0005,0))
    a=math.radians(angle);d=np.array([math.cos(a),math.sin(a)]);n=np.array([-d[1],d[0]])
    c=d*.040+n*-.006;m.add(box(.080,.012,.104),'laptop shell',e=(0,0,angle),t=(c[0],c[1],0))
    c=d*.042+n*.0005;m.add(box(.068,.0015,.088),'laptop screen',e=(0,0,angle),t=(c[0],c[1],0))
    return m.centred()

def computer_mouse():
    m=Mesh('Computer mouse')
    # Side profile: heel at -x, hump behind the middle, buttons toward +x (reads as a shoe).
    top=[(x,.004+.026*math.sqrt(max(0,1-((x+.006)/.036)**2))) for x in np.linspace(.030,-.030,13)]
    prof=[(-.030,0),(.030,0)]+top[:1]+top[1:]
    m.add(extrude(prof,.034),'mouse body')
    m.add(box(.012,.004,.004),'mouse dark',t=(.012,.026,0))
    m.add(box(.003,.010,.035),'mouse dark',t=(-.026,.010,0))
    return m.centred()

def umbrella():
    m=Mesh('Umbrella')
    prof=[(.0,.0),(.004,.0),(.004,.012),(.006,.016),(.0135,.055),(.0125,.090),(.0075,.150),(.0055,.152),(.0055,.192)]
    m.add(lathe(prof[:2]+[(.004,.012)],10),'umbrella shaft');m.add(lathe(prof[2:7],14),'umbrella cloth')
    m.add(lathe([(.0055,.150),(.0055,.196)],10,caps=True),'umbrella shaft')
    hook=[(.024-.024*math.cos(a),.196+.024*math.sin(a),0) for a in np.linspace(0,math.pi*1.05,14)]
    hook=[(0,.190,0)]+hook
    m.add(tube(hook,.0070,10),'umbrella wood')
    return m.centred()

def parmigiano():
    m=Mesh('Parmigiano wheel')
    prof=[(.0,.0),(.100,.0),(.107,.012),(.111,.056),(.107,.100),(.100,.112),(.0,.112)]
    m.add(lathe(prof,24),'cheese rind')
    m.add(lathe([(.1112,.048),(.1112,.064)],24,caps=False),'cheese mark')
    return m.centred()

def pisa_tower():
    m=Mesh('Leaning tower souvenir');prof=[(.0,0),(.040,0),(.040,.006)]
    for k in range(6):y=.006+k*.026;prof+=[(.036,y),(.036,y+.020),(.040,y+.020),(.040,y+.026)]
    prof+=[(.028,.162),(.028,.184),(.030,.184),(.030,.190),(.0,.190)]
    m.add(lathe(prof,18),'marble')
    for k in range(6):m.add(lathe([(.0362,.008+k*.026),(.0362,.024+k*.026)],18,caps=False),'marble shade')
    return m.centred()

def olive_oil():
    m=Mesh('Olive oil bottle')
    m.add(lathe([(.0,.0),(.019,.0),(.019,.082),(.015,.094),(.0075,.104),(.0075,.118)],16),'oil glass')
    m.add(lathe([(.0085,.116),(.0085,.132),(.0,.132)],14),'oil gold')
    return m.centred()

def chili():
    m=Mesh('Chili pepper')
    path=[(.004*math.sin(s*2.4)+.010*s*s,.105-.105*s,0) for s in np.linspace(0,1,14)]
    radii=[.0165*(1-s)**.75+.0015 for s in np.linspace(0,1,14)]
    m.add(tube(path,radii,12),'chili red')
    m.add(lathe([(.010,.0),(.012,.006),(.004,.010),(.0035,.024),(.0,.024)],10),'stem green',e=(0,0,-15),t=(.0,.100,0))
    return m.centred()

def pizza_slice():
    m=Mesh('Pizza slice')
    # Tip at -x; the crust arc bulges toward +x. Faces the screen when standing on its edge.
    edge=arc(-.050,0,.092,-22,22,9);poly=[(-.050,0)]+edge
    m.add(extrude(poly,.010),'cheese')
    m.add(tube([(x,y,0) for x,y in arc(-.050,0,.090,-23,23,11)],.009,10),'crust')
    for x,y in((-.004,.006),(.020,-.013),(.022,.016)):m.add(lathe([(.0,0),(.008,0),(.008,.003),(.0,.003)],12),'pepperoni',e=(-90,0,0),t=(x,y,-.005))
    return m.centred()

def tomato_tin():
    m=Mesh('Tomato tin')
    m.add(lathe([(.0,0),(.0275,0),(.0285,.004),(.0285,.006)],18),'tin metal')
    m.add(lathe([(.0275,.006),(.0275,.090)],18,caps=False),'tin label')
    m.add(lathe([(.0278,.040),(.0278,.056)],18,caps=False),'tin green')
    m.add(lathe([(.0285,.090),(.0285,.093),(.0275,.096),(.0,.096)],18),'tin metal')
    return m.centred()

def onigiri():
    m=Mesh('Onigiri rice ball')
    pts=[];corners=[(0,.072),(-.062,-.036),(.062,-.036)]
    for i,(cx,cy) in enumerate(corners):
        c=np.array([cx,cy])*.78;a=math.degrees(math.atan2(cy,cx))
        pts+=arc(c[0],c[1],.018,a-60,a+60,5)
    m.add(extrude(pts,.042),'rice')
    m.add(box(.054,.046,.046),'nori',t=(0,-.026,0))
    return m.centred()

def kokeshi():
    m=Mesh('Kokeshi doll')
    m.add(lathe([(.0,0),(.022,0),(.023,.010),(.020,.070),(.017,.098),(.010,.104),(.0,.104)],16),'kokeshi red')
    m.add(lathe([(.0216,.020),(.0213,.040)],16,caps=False),'kokeshi wood')
    m.add(sphere(.025,16,10),'kokeshi wood',t=(0,.126,0))
    m.add(sphere(.0258,16,10),'kokeshi hair',t=(0,.131,.003),scale=(1,.8,1))
    return m.centred()

def daruma():
    m=Mesh('Daruma doll')
    m.add(lathe([(.0,.0)]+[(.052*math.sin(p),.052-.052*math.cos(p)) for p in np.linspace(.30,math.pi,12)],18),'daruma red',scale=(1,1.04,.9))
    m.add(extrude(rrect(.056,.044,.020,4),.004),'daruma face',t=(0,.066,-.044))
    m.add(box(.060,.007,.004),'daruma gold',t=(0,.034,-.047))
    return m.centred()

def shinkansen():
    m=Mesh('Bullet train')
    # Side profile: long nose toward -x.
    nose=[(-.075+.062*(1-math.cos(a)),.004+.036*math.sin(a)**.8) for a in np.linspace(0,math.pi/2,9)]
    prof=[(.075,.0),(.075,.042),(-.013,.042)]+nose[::-1][1:]+[(-.075,.0)]
    m.add(extrude([(x,y) for x,y in prof],.034),'train white')
    m.add(box(.142,.006,.0345),'train blue',t=(.004,.014,0))
    for x in np.linspace(.0,.064,5):m.add(box(.009,.008,.0348),'train window',t=(x,.030,0))
    return m.centred()

def udon_bowl():
    m=Mesh('Udon bowl')
    m.add(lathe([(.0,.0),(.016,.0),(.016,.005),(.019,.005),(.033,.020),(.037,.044),(.034,.044),(.031,.022),(.017,.009),(.0,.009)],18),'bowl outer')
    m.add(lathe([(.0330,.040),(.0308,.024)],18,caps=False),'bowl inner')
    m.add(lathe([(.0,.036),(.033,.036)],18,caps=False),'udon')
    return m.centred()

def maneki_neko():
    m=Mesh('Maneki-neko')
    m.add(sphere(.030,16,10),'neko white',t=(0,.030,0),scale=(1,1.05,.85))
    m.add(sphere(.026,16,10),'neko white',t=(0,.077,0),scale=(1.1,.95,.95))
    for s in(-1,1):m.add(lathe([(.010,0),(.0,.016)],8),'neko white',e=(0,0,-s*18),t=(s*.017,.096,0))
    m.add(tube([(-.022,.040,-.010),(-.036,.060,-.012),(-.040,.084,-.010)],.0085,10),'neko white')
    m.add(lathe([(.012,0),(.012,.0045)],16,caps=False),'neko red',e=(0,0,0),t=(0,.052,0),scale=(1.6,1,1.4))
    m.add(sphere(.0060,10,6),'neko gold',t=(0,.047,-.021))
    return m.scaled(.88).centred()

# ---------------------------------------------------------------- map outlines (lon, lat)
ITALY=[
 [(7.53,43.79),(7.67,44.15),(6.95,44.45),(7.05,44.85),(6.63,45.12),(7.10,45.35),(6.82,45.80),(7.17,45.87),(7.66,45.98),(7.87,45.94),(8.10,46.25),(8.45,46.46),
  (8.65,46.05),(9.03,45.83),(9.15,46.15),(9.30,46.50),(10.00,46.40),(10.45,46.85),(11.00,46.77),(11.50,47.00),(12.20,47.08),(12.40,46.70),(13.00,46.60),
  (13.70,46.52),(13.60,46.20),(13.60,45.90),(13.90,45.60),(13.50,45.78),(13.10,45.67),(12.88,45.60),(12.35,45.44),(12.28,45.22),(12.53,44.97),(12.28,44.42),
  (12.57,44.06),(12.92,43.91),(13.52,43.62),(13.80,43.18),(14.21,42.46),(14.71,42.11),(15.00,42.00),(15.60,41.93),(16.02,41.95),(16.18,41.88),(16.05,41.71),
  (15.92,41.63),(16.15,41.38),(16.87,41.13),(17.30,40.95),(17.95,40.64),(18.20,40.40),(18.49,40.15),(18.36,39.80),(17.99,40.06),(17.90,40.26),(17.24,40.47),
  (16.82,40.37),(16.50,39.75),(16.63,39.60),(17.08,39.38),(17.20,39.03),(16.60,38.82),(16.55,38.69),(16.26,38.24),(16.06,37.92),(15.65,38.10),(15.71,38.25),
  (15.83,38.62),(16.16,38.73),(16.10,38.90),(16.03,39.36),(15.79,39.81),(15.60,40.07),(15.28,40.03),(14.99,40.35),(14.77,40.68),(14.33,40.60),(14.25,40.83),
  (14.07,40.82),(13.57,41.20),(13.25,41.28),(13.05,41.23),(12.62,41.45),(12.28,41.73),(11.80,42.09),(11.10,42.40),(10.50,42.93),(10.30,43.55),(10.25,43.87),
  (9.83,44.07),(9.40,44.27),(8.93,44.41),(8.48,44.30),(8.22,44.05),(8.03,43.88)],
 [(15.65,38.27),(15.24,38.22),(14.02,38.04),(13.36,38.13),(12.73,38.18),(12.51,38.02),(12.43,37.80),(12.59,37.65),(13.08,37.50),(13.58,37.27),(14.25,37.06),
  (14.85,36.72),(15.13,36.69),(15.29,37.07),(15.22,37.23),(15.09,37.50),(15.29,37.85)],
 [(9.19,41.24),(9.38,41.18),(9.55,40.92),(9.83,40.53),(9.72,40.38),(9.70,39.93),(9.62,39.29),(9.10,39.20),(8.85,38.88),(8.40,39.05),(8.42,39.45),(8.53,39.90),
  (8.38,40.04),(8.48,40.30),(8.31,40.56),(8.16,40.56),(8.25,41.07),(8.40,40.84),(8.70,40.91)],
]
JAPAN=[
 [(141.94,45.52),(142.60,44.95),(143.30,44.35),(144.30,44.00),(145.30,44.32),(145.10,43.70),(145.82,43.38),(145.20,43.15),(144.30,42.98),(143.33,42.30),(143.25,41.93),
  (142.50,42.25),(141.70,42.60),(141.00,42.30),(140.98,41.75),(140.70,41.75),(140.08,41.42),(139.85,42.20),(140.50,42.60),(140.40,43.20),(141.40,43.30),(141.60,43.90),
  (141.65,44.30),(141.75,45.20)],
 [(140.90,41.52),(141.47,41.40),(141.42,40.60),(141.95,40.10),(142.05,39.55),(141.65,38.95),(141.50,38.30),(141.00,38.25),(140.95,37.40),(140.90,36.80),(140.85,35.72),
  (140.38,35.20),(139.85,34.90),(139.80,35.35),(139.65,35.30),(139.15,35.25),(138.85,34.60),(138.75,35.05),(138.20,34.60),(137.30,34.65),(136.90,34.30),(136.25,34.15),
  (135.78,33.43),(135.10,33.90),(135.15,34.25),(135.40,34.62),(134.60,34.75),(133.90,34.55),(132.50,34.25),(131.95,33.95),(130.90,33.95),(131.40,34.42),(132.70,35.45),
  (133.40,35.55),(134.20,35.55),(135.30,35.55),(135.95,35.75),(136.70,36.60),(136.80,37.20),(137.30,37.50),(137.10,36.80),(137.70,36.95),(138.50,37.40),(139.10,37.95),
  (139.55,38.60),(139.85,39.30),(140.05,39.75),(139.70,39.95),(140.00,40.45),(139.90,40.70),(140.30,41.15),(140.65,40.95),(140.85,41.20)],
 [(132.70,33.85),(133.20,34.10),(134.05,34.35),(134.60,34.20),(134.75,33.85),(134.20,33.25),(133.60,33.50),(133.00,32.72),(132.50,32.90),(132.40,33.40),(132.00,33.35)],
 [(130.90,33.90),(130.40,33.62),(129.80,33.40),(129.65,33.10),(129.90,32.75),(130.20,32.85),(130.05,32.20),(130.30,31.25),(130.70,31.05),(130.95,31.50),(131.35,31.40),
  (131.45,31.95),(131.80,32.55),(131.90,33.10),(131.70,33.30),(131.75,33.60),(131.40,33.60)],
]

def map_outline(regions,lat0,lon0,lat1,lon1,height,centre=(0,.29)):
    c=math.cos(math.radians((lat0+lat1)/2));s=height/(lat1-lat0)
    mx=(lon0+lon1)/2;my=(lat0+lat1)/2
    return [[(centre[0]+(lon-mx)*c*s,centre[1]+(lat-my)*s) for lon,lat in r] for r in regions]

# ---------------------------------------------------------------- levels
def P(x,y,z=0,ex=0,ey=0,ez=0):return {'position':[x,y,z],'euler':[ex,ey,ez]}
def T(x,z,ex=0,ey=0,ez=0):return {'position':[x,None,z],'euler':[ex,ey,ez]}
TRAY_TOP=.062

# (object, label, solution pose, tray pose, map island). Map solutions came from --fit, then hand-checked.
LEVELS=[
 {'id':'ALife','title':'A  L I F E','button':'A LIFE','subtitle':'BALL / BOOK / SKATEBOARD / LAPTOP / MOUSE / UMBRELLA',
  'intro':'Six things from one life. Turn them in the light until their shadows stand up as one person.',
  'hint':'Start with the head. The umbrella becomes something to lean on. Seen from the side, an open laptop bends like an elbow.',
  'complete':'A LIFE\nThe ball he chased, the books he carried, the board he rode, the work he did -\nall of it, still holding him up.',
  'accent':[.94,.70,.39],'outline':[],
  'pieces':[
   (toy_ball,'BALL',P(.090,.405),T(-.40,-.10),None),
   (school_book,'BOOK',P(.025,.330,0,0,0,-30),T(-.42,.08,90,0,0),None),
   (skateboard,'SKATEBOARD',P(-.010,.180,0,0,0,15),T(-.42,.30,90,0,0),None),
   (laptop,'LAPTOP',P(.095,.300),T(.42,-.08,0,90,0),None),
   (computer_mouse,'MOUSE',P(.030,.095),T(.40,.10,0,-90,0),None),
   (umbrella,'UMBRELLA',P(.165,.190),T(.42,.30,90,0,0),None),
  ]},
 {'id':'Italia','title':'I T A L I A','button':'ITALIA','subtitle':'PARMIGIANO / TOWER / OLIVE OIL / PEPERONCINO / PIZZA / POMODORI',
  'intro':'Six souvenirs of one country. Fill the map with their shadows.',
  'hint':'The tower leans down the peninsula. The chili is the toe, the bottle the heel, and a slice of pizza is an island.',
  'complete':'ITALIA\nCheese in the north, a leaning tower down the middle,\npeperoncino in the toe and pizza by the sea.',
  'accent':[.45,.80,.55],'map':(ITALY,36.6,6.6,47.1,18.5,.45),
  'pieces':[
   (parmigiano,'PARMIGIANO',P(-.070,.440,0,0,0,15),T(-.42,-.06),0),
   (pisa_tower,'TOWER',P(.010,.320,0,0,0,45),T(-.42,.16),0),
   (olive_oil,'OLIVE OIL',P(.150,.240,0,0,0,-135),T(-.42,.33),0),
   (chili,'CHILI',P(.115,.160,0,0,180,15),T(.40,-.08,90,0,0),0),
   (pizza_slice,'PIZZA',P(.040,.110,0,0,0,-15),T(.42,.10,90,0,0),1),
   (tomato_tin,'TOMATOES',P(-.115,.215),T(.42,.30),2),
  ]},
 {'id':'Nippon','title':'N I P P O N','button':'NIPPON','subtitle':'ONIGIRI / KOKESHI / DARUMA / SHINKANSEN / UDON / MANEKI-NEKO',
  'intro':'Six things from one country, one for each part of the islands. Fill the map with their shadows.',
  'hint':'The rice ball is the north island. The bullet train runs west along the coast; the lucky cat waves from the south-west.',
  'complete':'NIPPON\nRice in the north, dolls and trains through the middle,\na bowl of udon and a waving cat in the south.',
  'accent':[.92,.38,.36],'map':(JAPAN,30.9,129.5,45.6,145.9,.46),
  'pieces':[
   (onigiri,'ONIGIRI',P(.115,.470,0,0,0,15),T(-.42,-.08),0),
   (kokeshi,'KOKESHI',P(.070,.310,0,0,0,-15),T(-.42,.32,0,0,90),1),
   (daruma,'DARUMA',P(.025,.225),T(-.42,.12),1),
   (shinkansen,'SHINKANSEN',P(-.080,.185),T(.42,-.08,0,90,0),1),
   (udon_bowl,'UDON',P(-.105,.140),T(.42,.12),2),
   (maneki_neko,'MANEKI-NEKO',P(-.170,.115),T(.42,.32),3),
  ]},
]

# ---------------------------------------------------------------- projection (mirrors SilhouetteProjection.cs)
def raster(v,tri,pose,mask):
    size=mask.shape[0];p=(v@rot(pose['euler']).T+np.asarray(pose['position'])-CENTER)[:,:2]/SPAN*size+size/2
    for a,b,c in tri:
        A,B,C=p[a],p[b],p[c];area=(B[0]-A[0])*(C[1]-A[1])-(B[1]-A[1])*(C[0]-A[0])
        if abs(area)<.001:continue
        x0=max(int(math.floor(min(A[0],B[0],C[0]))),0);x1=min(int(math.ceil(max(A[0],B[0],C[0]))),size-1)
        y0=max(int(math.floor(min(A[1],B[1],C[1]))),0);y1=min(int(math.ceil(max(A[1],B[1],C[1]))),size-1)
        if x0>x1 or y0>y1:continue
        X,Y=np.meshgrid(np.arange(x0,x1+1)+.5,np.arange(y0,y1+1)+.5)
        e0=(B[0]-A[0])*(Y-A[1])-(B[1]-A[1])*(X-A[0]);e1=(C[0]-B[0])*(Y-B[1])-(C[1]-B[1])*(X-B[0]);e2=(A[0]-C[0])*(Y-C[1])-(A[1]-C[1])*(X-C[0])
        inside=(e0>=0)&(e1>=0)&(e2>=0) if area>0 else (e0<=0)&(e1<=0)&(e2<=0)
        mask[y0:y1+1,x0:x1+1]|=inside
    return mask

def dilate(mask,r):
    out=mask.copy()
    for dy in range(-r,r+1):
        for dx in range(-r,r+1):
            if dx*dx+dy*dy>r*r+r:continue
            out|=np.roll(np.roll(mask,dy,0),dx,1)
    return out

TOLERANCE,REGION_MIN,COVER_MIN,SPILL_MAX,PIECE_SPILL_MAX=3,.80,.90,.07,.12
def score(regions,current,pieces=None):
    # Mirrors SilhouetteProjection.Render. `pieces` are the per-object masks behind `current`.
    target=np.any(regions,0);near=dilate(current,TOLERANCE);halo=dilate(target,TOLERANCE)
    cover=(target&near).sum()/max(1,target.sum());spill=(current&~halo).sum()/max(1,target.sum())
    region=[(r&near).sum()/max(1,r.sum()) for r in regions]
    own=max([(m&~halo).sum()/max(1,r.sum()) for m,r in zip(pieces,regions)]) if pieces is not None else 0
    return cover,spill,min(region),cover>=COVER_MIN and spill<=SPILL_MAX and min(region)>=REGION_MIN and own<=PIECE_SPILL_MAX

def preview(level,objects,regions,scale=4):
    img=Image.new('RGB',(SIZE*scale,SIZE*scale),(226,228,214));d=ImageDraw.Draw(img)
    palette=[(201,92,70),(70,134,148),(220,180,90),(110,160,90),(150,110,170),(90,90,90)]
    for i,r in enumerate(regions):
        ys,xs=np.nonzero(r)
        for x,y in zip(xs,ys):d.rectangle([x*scale,(SIZE-1-y)*scale,x*scale+scale-1,(SIZE-1-y)*scale+scale-1],fill=palette[i%6])
    for poly in level.get('outline_px',[]):
        d.line([(x*scale,(SIZE-y)*scale) for x,y in poly+[poly[0]]],fill=(30,30,30),width=2)
    return img

def land_mask(outline,res):
    px=SPAN/res;img=Image.new('L',(res,res),0);d=ImageDraw.Draw(img)
    for poly in outline:d.polygon([((x-CENTER[0])/px+res/2,res-((y-CENTER[1])/px+res/2)) for x,y in poly],fill=1)
    return np.array(img)[::-1].astype(bool)

def shifted(mask,dx,dy):
    out=np.zeros_like(mask);h,w=mask.shape
    out[max(dy,0):h+min(dy,0),max(dx,0):w+min(dx,0)]=mask[max(-dy,0):h+min(-dy,0),max(-dx,0):w+min(-dx,0)];return out

def fit(level,res=448,reach=5,turns=2,passes=3):
    # Design aid only: grid search (5 mm = 4 px here, 15 deg turns) that fills each piece's island.
    regions_,lat0,lon0,lat1,lon1,h=level['map'];outline=map_outline(regions_,lat0,lon0,lat1,lon1,h)
    land=land_mask(outline,res);islands=[land_mask([poly],res) for poly in outline];step=round(.005/(SPAN/res))
    pieces=level['pieces'];meshes=[make().arrays() for make,*_ in pieces];masks=[None]*len(pieces)
    for n in range(passes):
        for i,(make,label,sol,tray,island) in enumerate(pieces):
            others=np.zeros_like(land)
            for j,m in enumerate(masks):
                if j!=i and m is not None:others|=m
            mine=islands[island];best=(-1e9,)
            for k in range(-turns,turns+1):
                e=[sol['euler'][0],sol['euler'][1],sol['euler'][2]+15*k];mask=raster(*meshes[i],{'position':sol['position'],'euler':e},np.zeros((res,res),bool))
                for dx in range(-reach,reach+1):
                    for dy in range(-reach,reach+1):
                        m=shifted(mask,dx*step,dy*step);sc=(m&mine&~others).sum()-1.5*(m&~land).sum()-.6*(m&others).sum()-1.5*(m&land&~mine).sum()
                        if sc>best[0]:best=(sc,dx,dy,e,m)
            sc,dx,dy,e,m=best;masks[i]=m
            sol['position']=[round(sol['position'][0]+dx*.005,3),round(sol['position'][1]+dy*.005,3),sol['position'][2]];sol['euler']=[int(a) for a in e]
    placed=np.any(masks,0)
    for (make,label,sol,tray,island),m in zip(pieces,masks):
        print(f"   ({make.__name__},'{label}',P({sol['position'][0]:.3f},{sol['position'][1]:.3f},0,{sol['euler'][0]},{sol['euler'][1]},{sol['euler'][2]}))  on land={(m&land).sum()/m.sum():.2f}")
    print(f"  {level['id']} union/land IoU={(placed&land).sum()/(placed|land).sum():.2f} land covered={(placed&land).sum()/land.sum():.2f}")

def build(check=False):
    LOGS.mkdir(parents=True,exist_ok=True);data={'version':1,'workspace':{'size':SIZE,'span':SPAN,'center':CENTER.tolist()},
        'tolerance':{'pixels':TOLERANCE,'region':REGION_MIN,'cover':COVER_MIN,'spill':SPILL_MAX,'piece':PIECE_SPILL_MAX},
        'materials':[{'name':k,'color':list(c[:3]),'smoothness':c[3]} for k,c in MATERIALS.items()],'objects':[],'levels':[]}
    rng=np.random.default_rng(7);report=[]
    for level in LEVELS:
        if 'map' in level:
            regions_,lat0,lon0,lat1,lon1,h=level['map'];level['outline']=map_outline(regions_,lat0,lon0,lat1,lon1,h)
        level['outline_px']=[[((x-CENTER[0])/SPAN*SIZE+SIZE/2,(y-CENTER[1])/SPAN*SIZE+SIZE/2) for x,y in poly] for poly in level['outline']]
        objects=[];regions=[];entry={k:level[k] for k in('id','title','button','subtitle','intro','hint','complete','accent')}
        entry['outline']=[{'points':[round(c,4) for xy in poly for c in xy]} for poly in level['outline']];entry['pieces']=[]
        for make,label,solution,tray,island in level['pieces']:
            # Release snaps to the 5 mm / 15 deg workbench grid, so the answer must sit on it.
            assert all(abs(c/.005-round(c/.005))<1e-6 for c in solution['position']) and all(e%15==0 for e in solution['euler']),f"{label} solution is off the grid"
            m=make();v,tri=m.arrays();objects.append((m,v,tri))
            # Trays: rest the object on the tray top in its natural lying pose.
            if tray['position'][1] is None:tray['position'][1]=round(TRAY_TOP-float((v@rot(tray['euler']).T)[:,1].min()),4)
            regions.append(raster(v,tri,solution,np.zeros((SIZE,SIZE),bool)))
            data['objects'].append({'name':m.name,'vertices':[round(c,5) for c in v.flatten().tolist()],
                'submeshes':[{'material':k,'triangles':[i for t in ts for i in t]} for k,ts in m.sub.items()]})
            entry['pieces'].append({'object':m.name,'label':label,'solution':solution,'tray':tray})
        data['levels'].append(entry);regions=np.array(regions)
        img=preview(level,objects,regions);img.save(LOGS/f"Everyday-{level['id']}.png")
        # Robustness: small hand errors must pass, wrong poses must not.
        def jitter(pos,deg):
            masks=[]
            for (m,v,tri),(_,_,sol,_,_) in zip(objects,level['pieces']):
                pose={'position':np.array(sol['position'])+rng.normal(0,pos,3),'euler':np.array(sol['euler'])+rng.normal(0,deg,3)}
                masks.append(raster(v,tri,pose,np.zeros((SIZE,SIZE),bool)))
            return score(regions,np.any(masks,0),masks)[3]
        exact=score(regions,np.any(regions,0));tray=np.zeros((SIZE,SIZE),bool)
        for (m,v,tri),(_,_,_,t,_) in zip(objects,level['pieces']):raster(v,tri,t,tray)
        small=np.mean([jitter(.003,3) for _ in range(40)]);medium=np.mean([jitter(.008,8) for _ in range(40)]);large=np.mean([jitter(.025,25) for _ in range(40)])
        missing=[]
        for i in range(len(objects)):
            cur=np.zeros((SIZE,SIZE),bool)
            for j,(m,v,tri) in enumerate(objects):
                if j!=i:raster(v,tri,level['pieces'][j][2],cur)
            missing.append(score(regions,cur)[3])
        report.append(f"{level['id']}: missing-ok={[level['pieces'][i][1] for i,m in enumerate(missing) if m]} exact={exact[3]} tray={score(regions,tray)[3]} pass small={small:.2f} medium={medium:.2f} large={large:.2f} missing-one-passes={sum(missing)} area={np.any(regions,0).sum()}px")
    print('\n'.join(report))
    if not check:
        OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(data,separators=(',',':')))
        print('wrote',OUT.relative_to(ROOT),f'{OUT.stat().st_size/1024:.0f} KB')

if __name__=='__main__':
    if '--fit' in sys.argv:
        for level in LEVELS:
            if 'map' in level:fit(level)
    build('--check' in sys.argv or '--fit' in sys.argv)
