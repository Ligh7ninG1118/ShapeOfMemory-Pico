"""Torch levels, iteration 2: fixed lamps in a room corner, the player poses the pile itself.
The pile sits in an orb the player rolls freely (any rotation) and carries by its hub. Each lamp lights
its own wall; the final level has two lamps at 90 degrees and one pose must cast two pictures at once.

Single-lamp levels are designed in picture space (wall point + magnification -> placed on the lamp's ray).
The two-lamp level follows Mitra & Pauly, Shadow Art (2009): intersect each lamp's shadow cone through its target
picture to get the shadow hull, then greedily fill the hull with everyday objects (their example-based space
filling), so every object stays inside both pictures. Unity rasterises the imported meshes at the solution pose
to build the targets, so the answer is always exact. Mirrors Torch2Projection.cs.
Run: python Tools/ShadowArt/build_torch2.py [--check]   (numpy + Pillow). Previews: Logs/ShadowArt/Torch2-*.png
"""
from pathlib import Path
import json, math, sys
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0,str(Path(__file__).resolve().parent))
from build_everyday import Mesh, rot, box, lathe, sphere, tube, extrude, rrect, arc
from build_torch import euler_of

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/ShadowWorkshop/Art/Torch2/Torch2Objects.json'
LOGS=ROOT/'Logs/ShadowArt'
S2=1/math.sqrt(2)
# Workbench-local metres (table top y=0). Two walls form a corner behind the table.
CORNER=np.array([0.,0.,.80]);UP=np.array([0.,1.,0.])
HOME=np.array([0.,.34,.10])                 # pile pivot at rest; the solution pose is always here
WW,WH,PX=128,120,.0088                      # wall raster: 1.13 x 1.06 m
WALLS=[  # origin (bottom-left as seen from the room), right axis, normal into the room
 {'name':'left','origin':CORNER+np.array([-1.,0,-1])*S2*WW*PX,'u':np.array([1.,0,1])*S2,'n':np.array([1.,0,-1])*S2},
 {'name':'right','origin':CORNER.copy(),'u':np.array([1.,0,-1])*S2,'n':np.array([-1.,0,-1])*S2},
]
LAMP_DISTANCE,LAMP_RISE,HALF_ANGLE=.80,-.12,20
MATERIALS={
 'clock red':(.85,.22,.20,.55),'clock face':(.97,.96,.92,.30),'brass':(.85,.68,.30,.80),'mug':(.95,.95,.92,.55),'toast':(.85,.62,.32,.15),
 'brush handle':(.30,.70,.85,.50),'bristles':(.97,.97,.97,.20),'pillow':(.92,.90,.98,.15),'teddy':(.62,.42,.25,.10),'teddy nose':(.15,.10,.08,.40),
 'book blue':(.22,.32,.62,.30),'cushion':(.85,.45,.40,.10),'book green':(.20,.50,.35,.30),'cushion button':(.95,.85,.70,.30),'book red':(.70,.18,.16,.30),'frame wood':(.55,.36,.20,.35),'photo':(.55,.75,.85,.60),
 'key steel':(.75,.77,.80,.85),'slipper':(.55,.65,.80,.05),'slipper top':(.95,.92,.85,.05),'pot clay':(.75,.40,.25,.20),'leaf':(.30,.62,.30,.30),'remote':(.12,.12,.13,.40),'remote red':(.85,.15,.15,.50),
 'lamp base':(.85,.80,.65,.60),'plate':(.96,.96,.94,.50),'gift red':(.85,.20,.25,.30),'gift blue':(.25,.45,.85,.30),'ribbon':(.97,.82,.30,.50),
 'candle stripe':(.97,.55,.70,.30),'hat':(.55,.30,.85,.30),'balloon':(.95,.35,.25,.60),'string':(.9,.9,.9,.1),'map paper':(.92,.86,.68,.15),'map string':(.65,.25,.20,.1),
 'ticket':(.97,.97,.95,.25),'ticket band':(.20,.45,.80,.30),'tag':(.90,.70,.30,.25),'lens':(.10,.10,.12,.85),'frame':(.20,.20,.22,.6),'camera':(.20,.22,.25,.45),
 'polaroid body':(.95,.94,.90,.35),'rainbow red':(.88,.22,.20,.40),'rainbow orange':(.96,.55,.15,.40),'rainbow yellow':(.98,.80,.22,.40),'rainbow green':(.30,.66,.35,.40),'rainbow blue':(.20,.48,.82,.40),
 'pan':(.18,.18,.20,.60),'pan handle':(.30,.20,.12,.40),'egg':(.97,.93,.82,.30),'strawberry':(.85,.12,.15,.50),'cheese yellow':(.98,.82,.35,.25),
 'utensil':(.80,.82,.85,.85),'utensil black':(.12,.12,.13,.40),'lamp shade':(.97,.92,.78,.15),'pages':(.96,.94,.86,.10),'cap blue':(.25,.30,.65,.20),'candle':(.97,.93,.80,.25),'flame':(1.0,.80,.30,.20),'mask':(.35,.30,.55,.20),
}
# Lamp k shines straight at wall k from the opposite front corner.
LAMPS=[HOME+w['n']*LAMP_DISTANCE+UP*LAMP_RISE for w in WALLS]
N,R0,TOL=64,12,1
COVER_MIN,SPILL_MAX,IOU_MIN=.90,.10,.62
TILTS=(-10,-5,0,5,10)                       # free rotation: the picture may sit this tilted on its wall

def pose_matrix(yaw,pitch):return rot((pitch,0,0))@rot((0,yaw,0))   # Rx(pitch) * Ry(yaw): level start/solution poses

def axis_matrix(axis,deg):
    a=np.asarray(axis,float);a=a/np.linalg.norm(a);K=np.array([[0,-a[2],a[1]],[a[2],0,-a[0]],[-a[1],a[0],0]]);t=math.radians(deg)
    return np.eye(3)+math.sin(t)*K+(1-math.cos(t))*K@K

def random_rotation(rng):
    w,x,y,z=(lambda q:q/np.linalg.norm(q))(rng.normal(size=4))
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])

def angle_between(A,B):return math.degrees(math.acos(max(-1,min(1,(np.trace(A.T@B)-1)/2))))

def project(points,lamp,wall):
    # Perspective from a point lamp onto a wall plane -> wall pixel coords and validity.
    toward=-wall['n'];d=(points-lamp)@toward;ok=d>1e-3
    t=((wall['origin']-lamp)@toward)/np.where(ok,d,1);q=lamp+(points-lamp)*t[:,None]
    rel=q-wall['origin'];return np.stack([rel@wall['u']/PX,rel@UP/PX],1),ok

def wall_point(p,k):
    xy,_=project(np.asarray(p,float)[None],LAMPS[k],WALLS[k]);return xy[0]*PX

def lit_mask(k,aim=None):
    lamp=LAMPS[k];aim=(HOME-lamp) if aim is None else aim;aim=aim/np.linalg.norm(aim);w=WALLS[k]
    gx,gy=np.meshgrid((np.arange(WW)+.5)*PX,(np.arange(WH)+.5)*PX)
    q=w['origin']+gx[...,None]*w['u']+gy[...,None]*UP;d=q-lamp;d/=np.linalg.norm(d,axis=-1,keepdims=True)
    return (d@aim)>math.cos(math.radians(HALF_ANGLE))

def shadow(pieces,k,yaw,pitch,pos=HOME,res=1,R=None):
    # pieces: [((v,tri),local_pos,local_euler)] in the pile frame. Same culling and coverage rule as Unity.
    lamp=LAMPS[k];w=WALLS[k];R=pose_matrix(yaw,pitch) if R is None else R;img=Image.new('L',(WW*res,WH*res),0);draw=ImageDraw.Draw(img)
    for (v,tri),lp,le in pieces:
        p=(v@rot(le).T+lp)@R.T+pos;a,b,c=p[tri[:,0]],p[tri[:,1]],p[tri[:,2]]
        facing=np.einsum('ij,ij->i',np.cross(b-a,c-a),lamp-a)>0;xy,ok=project(p,lamp,w);xy=xy*res
        for i,j,l in tri[facing]:
            if ok[i] and ok[j] and ok[l]:draw.polygon([(xy[i,0],WH*res-xy[i,1]),(xy[j,0],WH*res-xy[j,1]),(xy[l,0],WH*res-xy[l,1])],fill=1)
    m=np.array(img)[::-1].astype(bool)
    return m&lit_mask(k) if res==1 else m

def normalise(mask,tilt=0):
    # Centroid + RMS radius onto an N x N grid, the grid turned by tilt degrees about the centroid.
    ys,xs=np.nonzero(mask);out=np.zeros((N,N),bool)
    if len(xs)<12:return out
    cx,cy=xs.mean()+.5,ys.mean()+.5;r=math.sqrt(((xs+.5-cx)**2+(ys+.5-cy)**2).mean());acc=np.zeros((N,N),int)
    c,s=math.cos(math.radians(tilt)),math.sin(math.radians(tilt));g=np.arange(N)+.5-N/2
    for ox in(-.25,.25):
        for oy in(-.25,.25):
            GX,GY=np.meshgrid(g+ox,g+oy)
            X=np.floor(cx+(GX*c-GY*s)*r/R0).astype(int);Y=np.floor(cy+(GX*s+GY*c)*r/R0).astype(int)
            v=(X>=0)&(X<mask.shape[1])&(Y>=0)&(Y<mask.shape[0]);acc[v]+=mask[Y[v],X[v]]
    return acc>=2

def dilate(mask,r):
    out=mask.copy();h,w=mask.shape
    for dy in range(-r,r+1):
        for dx in range(-r,r+1):
            if dx*dx+dy*dy>r*r+r:continue
            s=np.zeros_like(mask);s[max(dy,0):h+min(dy,0),max(dx,0):w+min(dx,0)]=mask[max(-dy,0):h+min(-dy,0),max(-dx,0):w+min(-dx,0)];out|=s
    return out

def score(target,current):
    area=max(1,target.sum());cover=(target&dilate(current,TOL)).sum()/area;spill=(current&~dilate(target,TOL)).sum()/area
    iou=(target&current).sum()/max(1,(target|current).sum());return cover,spill,iou,cover>=COVER_MIN and spill<=SPILL_MAX and iou>=IOU_MIN

def best(targets,current,iou_min=IOU_MIN):
    # targets: the answer picture at each of TILTS. A passing tilt wins, then the higher IoU (as in Torch2Projection).
    def key(r):c,s,i,_=r;return(c>=COVER_MIN and s<=SPILL_MAX and i>=iou_min,i)
    return max((score(t,current) for t in targets),key=key)

def to_img(mask,scale=3,fg=(30,36,40),bg=(238,226,198)):
    arr=np.array(Image.new('RGB',(mask.shape[1]*scale,mask.shape[0]*scale),bg));a=np.kron(mask[::-1],np.ones((scale,scale),bool));arr[a]=fg;return Image.fromarray(arr)

# ---------------------------------------------------------------- shadow hull + example-based filling (two lamps)
def polygon_mask(polys,k,res=1):
    # Polygons in wall metres -> wall mask (row 0 = bottom).
    img=Image.new('L',(WW*res,WH*res),0);d=ImageDraw.Draw(img)
    for poly,fill in polys:d.polygon([(x/PX*res,WH*res-y/PX*res) for x,y in poly],fill=fill)
    return np.array(img)[::-1].astype(bool)

def erode(mask,r):return ~dilate(~mask,r)

def shadow_hull(targets,voxel=.004,reach=.17):
    # Voxel centres whose projection lands inside every target picture: the paper's shadow hull H = C1 n C2.
    g=np.arange(-reach,reach+1e-9,voxel);X,Y,Z=np.meshgrid(g,g,g,indexing='ij');P=np.stack([X.ravel(),Y.ravel(),Z.ravel()],1)+HOME
    keep=np.ones(len(P),bool)
    for k,t in enumerate(targets):
        xy,ok=project(P,LAMPS[k],WALLS[k]);x=np.floor(xy[:,0]).astype(int);y=np.floor(xy[:,1]).astype(int)
        inside=ok&(x>=0)&(x<WW)&(y>=0)&(y<WH);hit=np.zeros(len(P),bool);hit[inside]=t[y[inside],x[inside]];keep&=hit
    return P[keep]

def surface_points(v,tri,spacing=.003,seed=0):
    # Dense surface samples stand in for the triangle raster during the search.
    rng=np.random.default_rng(seed);a,b,c=v[tri[:,0]],v[tri[:,1]],v[tri[:,2]];area=np.linalg.norm(np.cross(b-a,c-a),axis=1)/2
    n=np.maximum(1,(area/(spacing*spacing)).astype(int));idx=np.repeat(np.arange(len(tri)),n)
    r1=np.sqrt(rng.random(len(idx)));r2=rng.random(len(idx))
    return a[idx]*(1-r1)[:,None]+b[idx]*(r1*(1-r2))[:,None]+c[idx]*(r1*r2)[:,None]

def fill_hull(palette,targets,yaw,pitch,steps=16,candidates=1500,seed=3,anchors=(),spill_weight=3.0,edge_weight=2.0,passes=2):
    """Greedy example-based space filling (Mitra & Pauly 2009, section 4). Candidates are aimed at a still-uncovered
    pixel: a hull voxel on that pixel's lamp ray becomes the object's centre. Returns cluster-local placements."""
    rng=np.random.default_rng(seed);halo=[dilate(t,TOL) for t in targets];covered=[np.zeros_like(t) for t in targets]
    used=set();placed=[];hull=shadow_hull(targets)
    # Outline pixels (rays, horns, ears) carry the picture; interior pixels are cheap to fill with any big object.
    weight=[1+edge_weight*(t&~erode(t,2)) for t in targets]
    pixel=[]
    for k in range(len(targets)):
        xy,_=project(hull,LAMPS[k],WALLS[k]);pixel.append(np.floor(xy[:,1]).astype(int)*WW+np.floor(xy[:,0]).astype(int))
    pts=[surface_points(*m.arrays(),seed=i) for i,(m,_) in enumerate(palette)]
    rots=[np.eye(3)]+[rot(e) for e in rng.uniform(-180,180,(90,3))]+[rot((ax*90,ay*90,az*90)) for ax in range(4) for ay in range(4) for az in range(2)]
    def evaluate(P):
        gain=0;spill=0;masks=[]
        for k in range(len(targets)):
            xy,ok=project(P,LAMPS[k],WALLS[k]);x=np.floor(xy[:,0]).astype(int);y=np.floor(xy[:,1]).astype(int)
            inside=ok&(x>=0)&(x<WW)&(y>=0)&(y<WH);m=np.zeros_like(targets[k]);m[y[inside],x[inside]]=True
            spill+=(m&~halo[k]).sum()+(~inside).sum()*.5;gain+=weight[k][m&targets[k]&~covered[k]].sum();masks.append(m)
        return gain-spill_weight*spill,masks
    def commit(i,R,t):
        s,masks=evaluate(pts[i]@R.T+t)
        for k,m in enumerate(masks):covered[k]|=m&targets[k]
        used.add(i);placed.append((i,R,t));return s
    for i,p,R in anchors:commit(i,np.asarray(R,float),np.asarray(p,float))
    for step in range(steps):
        free=[i for i in range(len(palette)) if i not in used]
        if not free:break
        best=(0,None)
        for c in range(candidates):
            k=c%len(targets);todo=np.flatnonzero((targets[k]&~covered[k]).ravel())
            if len(todo)==0:continue
            want=todo[int(rng.integers(len(todo)))];on=np.flatnonzero(pixel[k]==want)
            if len(on)==0:continue
            t=hull[on[int(rng.integers(len(on)))]];i=free[int(rng.integers(len(free)))];R=rots[int(rng.integers(len(rots)))]
            sc,_=evaluate(pts[i]@R.T+t)
            if sc>best[0]:best=(sc,(i,R,t))
        if best[1] is None:break
        s0,(i,R,t)=best
        for _ in range(250):
            t2=t+rng.normal(0,.005,3);R2=rot(rng.normal(0,5,3))@R;s2,_=evaluate(pts[i]@R2.T+t2)
            if s2>s0:s0,R,t=s2,R2,t2
        commit(i,R,t)
        print(f"   fill {palette[i][1]:<12} score={s0:5.0f}  cover "+" ".join(f"{c.sum()/max(1,tg.sum()):.2f}" for c,tg in zip(covered,targets)))
    # Second pass: lift each object out and re-place it against everything else (coordinate descent).
    def recompute():
        for k in range(len(targets)):covered[k][:]=False
        for i,R,t in placed:
            _,masks=evaluate(pts[i]@R.T+t)
            for k,m in enumerate(masks):covered[k]|=m&targets[k]
    for sweep in range(passes):
        for n in range(len(placed)):
            i,R,t=placed[n];placed[n]=(i,R,t+1e3);recompute()
            s0,_=evaluate(pts[i]@R.T+t)
            for c in range(candidates//3):
                if c%3==0:
                    k=c%len(targets);todo=np.flatnonzero((targets[k]&~covered[k]).ravel())
                    on=np.flatnonzero(pixel[k]==todo[int(rng.integers(len(todo)))]) if len(todo) else []
                    if len(on)==0:continue
                    t2=hull[on[int(rng.integers(len(on)))]];R2=rots[int(rng.integers(len(rots)))]
                else:t2=t+rng.normal(0,.008,3);R2=rot(rng.normal(0,8,3))@R
                s2,_=evaluate(pts[i]@R2.T+t2)
                if s2>s0:s0,R,t=s2,R2,t2
            placed[n]=(i,R,t);recompute()
        print(f"   pass {sweep+1} cover "+" ".join(f"{c.sum()/max(1,tg.sum()):.2f}" for c,tg in zip(covered,targets)))
    inv=pose_matrix(yaw,pitch).T;out=[]
    for i,R,t in placed:out.append((i,palette[i][1],((t-HOME)@inv.T).round(4).tolist(),euler_of(inv@R)))
    return out

# ---------------------------------------------------------------- objects: day and night
def alarm_clock():
    m=Mesh('Alarm clock')
    m.add(extrude(arc(0,0,.042,0,360,25)[:-1],.030),'clock red');m.add(extrude(arc(0,0,.035,0,360,21)[:-1],.002),'clock face',t=(0,0,-.016))
    for s in(-1,1):m.add(sphere(.017,10,6),'brass',t=(s*.026,.043,0));m.add(box(.006,.018,.006),'brass',e=(0,0,s*25),t=(s*.026,-.044,0))
    return m.centred()
def coffee_mug():
    m=Mesh('Coffee mug')
    m.add(lathe([(.0,0),(.037,0),(.039,.088),(.034,.088),(.032,.008),(.0,.008)],18),'mug')
    m.add(tube([(.036+.022*math.sin(a),.045+.026*math.cos(a),0) for a in np.linspace(0,math.pi,9)],.006,8),'mug')
    return m.centred()
def toast():
    m=Mesh('Toast');pts=[(-.044,-.050),(.044,-.050),(.046,.018)]+arc(0,.026,.052,10,170,11)+[(-.046,.018)]
    m.add(extrude(pts,.014),'toast');return m.centred()
def toothbrush():
    m=Mesh('Toothbrush');m.add(lathe([(.0,0),(.006,0),(.007,.12),(.004,.15),(.004,.175),(.0,.175)],8),'brush handle')
    m.add(box(.012,.026,.014),'bristles',t=(.0,.162,.009));return m.centred()
def pillow():
    m=Mesh('Pillow');m.add(extrude(rrect(.19,.12,.045,6),.05),'pillow');return m.centred()
def teddy_bear():
    m=Mesh('Teddy bear')
    m.add(sphere(.038,14,8),'teddy',t=(0,.0,0),scale=(1,1.15,.85));m.add(sphere(.030,14,8),'teddy',t=(0,.068,0))
    for s in(-1,1):
        m.add(sphere(.012,8,6),'teddy',t=(s*.024,.094,0));m.add(sphere(.015,8,6),'teddy',t=(s*.040,.012,0),scale=(1,1.5,1))
        m.add(sphere(.016,8,6),'teddy',t=(s*.022,-.042,0),scale=(1,1.3,1))
    m.add(sphere(.011,8,6),'teddy nose',t=(0,.064,-.028));return m.centred()
def story_book():
    m=Mesh('Story book');m.add(box(.150,.110,.024),'book blue');m.add(box(.144,.104,.025),'pages',t=(.004,0,0));return m.centred()
def night_cap():
    m=Mesh('Night cap');m.add(lathe([(.0,0),(.050,0),(.052,.012),(.0,.150)],14),'cap blue');m.add(sphere(.016,10,6),'pages',t=(0,.152,0))
    m.add(lathe([(.051,.0),(.053,.0),(.053,.016),(.051,.016)],14,caps=False),'pages');return m.centred()
def candle():
    m=Mesh('Candle');m.add(lathe([(.0,0),(.013,0),(.013,.085),(.0,.085)],10),'candle');m.add(lathe([(.0,.085),(.007,.095),(.0,.115)],8),'flame')
    m.add(lathe([(.0,0),(.030,0),(.030,.006),(.0,.006)],12),'brass');return m.centred()
def sleep_mask():
    m=Mesh('Sleep mask');m.add(extrude(arc(-.040,0,.032,40,320,9)+arc(.040,0,.032,-140,140,9),.010),'mask');return m.centred()

# ---------------------------------------------------------------- objects: home
def cushion():
    m=Mesh('Cushion');m.add(extrude(rrect(.16,.16,.05,6),.05),'cushion');m.add(sphere(.012,8,6),'cushion button',t=(0,0,-.026));return m.centred()
def novel():
    m=Mesh('Novel');m.add(box(.112,.172,.030),'book red');m.add(box(.106,.166,.031),'pages',t=(.004,0,0));return m.centred()
def picture_frame():
    m=Mesh('Picture frame');m.add(box(.130,.100,.014),'frame wood');m.add(box(.100,.072,.015),'photo');m.add(box(.012,.07,.05),'frame wood',e=(-25,0,0),t=(0,-.01,.03));return m.centred()
def house_keys():
    m=Mesh('House keys');m.add(tube([(.018*math.cos(a),.018*math.sin(a),0) for a in np.linspace(0,2*math.pi,13)],.0025,6),'brass')
    for k,a in enumerate((-20,25)):
        r=math.radians(a-90);d=np.array([math.cos(r),math.sin(r)])
        m.add(box(.012,.055,.003),'key steel',e=(0,0,a),t=(*(d*.045+np.array([0,-.005])),k*.004));m.add(extrude(arc(0,0,.013,0,360,13)[:-1],.003),'key steel',t=(*(d*.020),k*.004))
    return m.centred()
def slipper():
    m=Mesh('Slipper');m.add(extrude(rrect(.050,.115,.024,5),.030),'slipper',e=(90,0,0));m.add(extrude(rrect(.054,.055,.02,4),.03),'slipper top',e=(90,0,0),t=(0,.022,-.03));return m.centred()
def potted_plant():
    m=Mesh('Potted plant');m.add(lathe([(.0,0),(.028,0),(.036,.060),(.0,.060)],14),'pot clay')
    for a,h in((-35,.10),(0,.12),(35,.095)):m.add(sphere(.022,10,6),'leaf',e=(0,0,a),t=(math.sin(math.radians(-a))*.035,.06+h*.55,0),scale=(.6,1.6,.4))
    return m.centred()
def open_book(angle=56):
    m=Mesh('Open book');a=math.radians(angle);L=.15
    for side in(-1,1):
        c=np.array([side*math.sin(a)*L/2,-math.cos(a)*L/2])
        m.add(box(.006,L,.11),'book green',e=(0,0,side*angle),t=(c[0],c[1],0));m.add(box(.003,L-.008,.104),'pages',e=(0,0,side*angle),t=(c[0]-side*.004*math.cos(a),c[1]-.004*math.sin(a),0))
    return m.centred()
def tv_remote():
    m=Mesh('TV remote');m.add(extrude(rrect(.045,.170,.012,4),.020),'remote');m.add(extrude(arc(0,0,.006,0,360,9)[:-1],.004),'remote red',t=(0,.06,-.011));return m.centred()
def table_lamp():
    m=Mesh('Table lamp');m.add(lathe([(.0,0),(.040,0),(.040,.010),(.006,.012),(.006,.110),(.0,.110)],14),'lamp base')
    m.add(lathe([(.060,.090),(.036,.160),(.033,.160),(.057,.092)],16,caps=False),'lamp shade');return m.centred()

# ---------------------------------------------------------------- objects: birthday, voyage, morning
def dinner_plate():
    m=Mesh('Dinner plate');m.add(lathe([(.0,0),(.07,0),(.11,.014),(.112,.018),(.105,.018),(.068,.006),(.0,.006)],24),'plate');return m.centred()
def gift_box(w=.20,h=.08,d=.14,name='Big gift box',colour='gift red'):
    m=Mesh(name);m.add(box(w,h,d),colour);m.add(box(.022,h+.002,d+.002),'ribbon');m.add(box(w+.002,h+.002,.022),'ribbon');return m.centred()
def small_gift():return gift_box(.15,.07,.10,'Small gift box','gift blue')
def birthday_candle():
    m=Mesh('Birthday candle');m.add(lathe([(.0,0),(.0065,0),(.0065,.070),(.0,.070)],8),'candle stripe');m.add(lathe([(.0,.071),(.009,.082),(.0,.104)],8),'flame');return m.centred()
def party_hat():
    m=Mesh('Party hat');m.add(lathe([(.0,0),(.045,0),(.0,.11)],14),'hat');m.add(sphere(.012,8,6),'ribbon',t=(0,.112,0));return m.centred()
def balloon():
    m=Mesh('Balloon');m.add(sphere(.045,14,10),'balloon',scale=(1,1.18,1));m.add(lathe([(.0,0),(.007,0),(.0,.01)],6),'balloon',t=(0,-.062,0))
    m.add(tube([(0,-.06,0),(.006,-.09,0),(-.004,-.12,0),(.003,-.15,0)],.0015,5),'string');return m.centred()
def map_tube():
    m=Mesh('Rolled map');m.add(lathe([(.0,0),(.018,0),(.018,.23),(.0,.23)],14),'map paper');m.add(lathe([(.0185,.03),(.0185,.045)],14,caps=False),'map string')
    m.add(lathe([(.0185,.185),(.0185,.200)],14,caps=False),'map string');return m.centred()
def boarding_pass():
    m=Mesh('Boarding pass');m.add(box(.075,.185,.002),'ticket');m.add(box(.075,.04,.0022),'ticket band',t=(0,.07,0));return m.centred()
def luggage_tag():
    m=Mesh('Luggage tag');m.add(extrude([(-.026,-.045),(.026,-.045),(.026,.030),(0,.046),(-.026,.030)],.004),'tag');m.add(tube([(0,.040,0),(.01,.06,0),(-.01,.075,0)],.0018,5),'string')
    return m.centred()
def sunglasses():
    m=Mesh('Sunglasses')
    for s in(-1,1):m.add(extrude(arc(0,0,.026,0,360,17)[:-1],.004),'lens',t=(s*.033,0,0),scale=(1,.8,1))
    m.add(box(.016,.004,.004),'frame',t=(0,.008,0));return m.centred()
def polaroid_camera():
    # Instant camera: wedge body (tall front, top sloping to a short back), black front, lens barrel, rainbow stripe,
    # flash, viewfinder and a print half out of the slot under the lens. Front faces -z like the lens.
    m=Mesh('Polaroid camera');W,H,F=.11,.082,.046                     # width, height, half depth
    m.add(extrude([(F,0),(F,H),(-.008,H),(-F,.052),(-F,0)],W),'polaroid body',e=(0,90,0))
    m.add(box(W-.008,.050,.004),'camera',t=(0,.030,-F-.001))
    for i,c in enumerate(('rainbow red','rainbow orange','rainbow yellow','rainbow green','rainbow blue')):
        m.add(box(.0055,.026,.0045),c,t=(-.011+i*.0055,.068,-F-.0012))
    m.add(lathe([(.0,0),(.025,0),(.025,.016),(.019,.019),(.0,.019)],18),'camera',e=(-90,0,0),t=(0,.033,-F))
    m.add(lathe([(.0,.018),(.0145,.018),(.0145,.0205),(.0,.0215)],18),'lens',e=(-90,0,0),t=(0,.033,-F))
    m.add(box(.030,.014,.004),'clock face',t=(.034,.070,-F-.0012))
    m.add(box(.020,.012,.016),'camera',t=(-.034,H+.006,-.014))
    m.add(box(.070,.0016,.030),'ticket',t=(0,.0045,-F-.015));m.add(box(.056,.0018,.022),'photo',t=(0,.0046,-F-.017))
    return m.centred()
def frying_pan():
    m=Mesh('Frying pan');m.add(lathe([(.0,0),(.095,0),(.105,.035),(.099,.035),(.090,.006),(.0,.006)],24),'pan')
    m.add(lathe([(.0,0),(.009,0),(.009,.15),(.0,.15)],8),'pan handle',e=(0,0,-90),t=(.10,.03,0));return m.centred()
def egg():
    prof=[(.03*math.sin(p),-.03*math.cos(p)*(1.0 if p<math.pi/2 else 1.35)) for p in np.linspace(0,math.pi,12)]
    m=Mesh('Egg');m.add(lathe(prof,12),'egg');return m.centred()
def strawberry():
    m=Mesh('Strawberry');m.add(lathe([(.0,0),(.010,.006),(.019,.024),(.017,.036),(.0,.040)],10),'strawberry');m.add(lathe([(.0,.038),(.014,.040),(.0,.046)],8),'leaf');return m.centred()
def cheese_wedge():
    m=Mesh('Cheese wedge');m.add(extrude([(-.06,-.02),(.03,-.02),(.03,.025)],.04),'cheese yellow');return m.centred()
def spatula():
    m=Mesh('Spatula');m.add(box(.065,.075,.004),'utensil');m.add(lathe([(.0,0),(.006,0),(.006,.14),(.0,.14)],8),'utensil black',t=(0,.035,0));return m.centred()
def whisk():
    m=Mesh('Whisk');m.add(lathe([(.0,0),(.008,0),(.008,.10),(.0,.10)],8),'utensil black')
    loop=[(.035*math.sin(t),.10+.05*(1-math.cos(t))+.035*(1-math.cos(t)),0) for t in np.linspace(0,math.pi,12)]
    for a in(0,60,120):m.add(tube(loop+[(-x,y,z) for x,y,z in loop[::-1][1:]],.0016,5),'utensil',e=(0,a,0))
    return m.centred()
def ladle():
    m=Mesh('Ladle');m.add(sphere(.035,12,6),'utensil',scale=(1,.6,1));m.add(lathe([(.0,0),(.005,0),(.005,.19),(.0,.19)],8),'utensil',t=(.03,.0,0),e=(0,0,-15));return m.centred()
def fork():
    m=Mesh('Fork');m.add(box(.010,.11,.004),'utensil',t=(0,.055,0));m.add(box(.026,.022,.004),'utensil',t=(0,-.008,0))
    for x in(-.010,-.0035,.0035,.010):m.add(box(.0035,.035,.004),'utensil',t=(x,-.035,0))
    return m.centred()

# ---------------------------------------------------------------- level design
def view_frame(k):
    w=WALLS[k];return np.stack([w['u'],UP,-w['n']],1)   # columns: right, up, toward the wall
def D(u,v,mag,*turns):
    # Picture space for the level's lamp: shadow centre offset (u,v) from the pile centre's shadow, magnification,
    # then turns about the lamp's view axes (x right, y up, z toward the wall), applied in order.
    R=np.eye(3)
    for axis,deg in turns:R=rot({'x':(deg,0,0),'y':(0,deg,0),'z':(0,0,deg)}[axis])@R
    return {'u':u,'v':v,'mag':mag,'view':R}

def resolve_single(level):
    k=level['lamps'][0];L=LAMPS[k];w=WALLS[k];F=view_frame(k);c=wall_point(HOME,k);inv=pose_matrix(level['solution'][0],level['solution'][1]).T
    out=[]
    for make,label,d in level['pieces']:
        q=w['origin']+(c[0]+d['u'])*w['u']+(c[1]+d['v'])*UP;p=L+(q-L)/d['mag']
        out.append((make,label,((p-HOME)@inv.T).round(4).tolist(),euler_of(inv@F@d['view'])))
    return out

# (object, label, placement). Solutions: (yaw, pitch) of the pile at HOME. Starts are the pose the level opens in.
# Magnifications are spread wide on purpose: pieces at very different depths only line up from the answer.
# The orb turns freely, so a level must also reject every other orientation (checked on 1000 random ones).
LEVELS=[
 {'id':'Birthday','title':'H A P P Y   B I R T H D A Y','button':'BIRTHDAY','lamps':[0],'solution':(30,-20),'start':(-110,30),
  'iou_min':.70,   # free rotation: one in 1000 random orientations reached IoU .65 with a tilted box-and-balloon blob
  'riddle':'A plate, two presents, three candles, a party hat and a balloon. Somebody is a year older.',
  'hint':'Tip the pile back a little. The presents are the layers, the plate sits underneath and the balloon floats on the right.',
  'complete':'HAPPY BIRTHDAY\nMake a wish.','accent':[.98,.62,.70],
  'pieces':[
   (dinner_plate,'PLATE',D(-.02,-.20,2.25)),
   (gift_box,'BIG PRESENT',D(-.02,-.115,1.42)),
   (small_gift,'SMALL PRESENT',D(-.02,.005,2.15)),
   (birthday_candle,'CANDLE',D(-.095,.12,1.35)),(birthday_candle,'CANDLE',D(-.02,.13,2.3)),(birthday_candle,'CANDLE',D(.055,.12,1.7)),
   (party_hat,'PARTY HAT',D(-.13,-.11,1.9,('z',62))),
   (balloon,'BALLOON',D(.20,.12,1.55,('z',-12))),
  ]},
 {'id':'BonVoyage','title':'B O N   V O Y A G E','button':'BON VOYAGE','lamps':[1],'solution':(-40,50),'start':(60,-10),
  'riddle':'A map, two boarding passes, two luggage tags, sunglasses and a polaroid camera. Time to leave.',
  'hint':'Tip the pile a long way forward. The rolled map is the body, the tickets are wings and the tags are the tail.',
  'complete':'BON VOYAGE\nWindow seat, please.','accent':[.45,.70,.98],
  # A plane taking off, flying right with the nose up: short far wing above the body, long near wing below,
  # tail at the back. Fitted to a reference icon (IoU .90) with every piece kept >= 1 cm from the others.
  'pieces':[
   (map_tube,'MAP',D(.015,.023,2.052,('x',5.6),('y',-19.1),('z',-80.6))),
   (boarding_pass,'TICKET',D(.051,-.075,1.672,('x',41.0),('y',38.8),('z',2.5))),
   (boarding_pass,'TICKET',D(-.006,.074,1.362,('x',69.9),('y',-35.1),('z',-170.0))),
   (luggage_tag,'TAG',D(-.237,.024,1.475,('x',46.0),('y',-13.5),('z',40.2))),
   (luggage_tag,'TAG',D(-.156,-.047,1.643,('x',-24.1),('y',-32.2),('z',156.8))),
   (sunglasses,'SUNGLASSES',D(.120,.076,1.890,('x',17.3),('y',.7),('z',8.7))),
   (polaroid_camera,'CAMERA',D(-.037,.011,1.422,('x',6.7),('y',50.5),('z',12.7))),
  ]},
 {'id':'GoodMorning','title':'G O O D   M O R N I N G','button':'GOOD MORNING','lamps':[0],'solution':(110,-35),'start':(-40,20),
  'riddle':'A pan, an egg, strawberries, cheese, a spatula, a whisk, a ladle and two forks. Someone is up before you.',
  'hint':'Lean the pile back and keep turning. The pan is a body, the egg a head, the utensils fan out as a tail and the forks stand like feet.',
  'complete':'GOOD MORNING\nCock-a-doodle-doo.','accent':[.98,.55,.30],
  'pieces':[
   (frying_pan,'FRYING PAN',D(.02,-.03,1.5,('x',-62),('z',32))),
   (egg,'EGG',D(-.125,.075,2.05,('z',25))),
   (strawberry,'STRAWBERRY',D(-.145,.145,1.6,('z',15))),(strawberry,'STRAWBERRY',D(-.105,.15,2.3,('z',-20))),
   (cheese_wedge,'CHEESE',D(-.205,.06,1.8)),
   (spatula,'SPATULA',D(.175,.20,1.45,('z',-25))),(whisk,'WHISK',D(.235,.12,2.2,('z',-70))),(ladle,'LADLE',D(.205,.235,1.7,('z',8))),
   (fork,'FORK',D(-.035,-.205,1.4)),(fork,'FORK',D(.045,-.205,2.15)),
  ]},
]

# ---------------------------------------------------------------- two-lamp level: HOME
def _heart(c,s=.21):
    pts=[]
    for t in np.linspace(0,2*math.pi,80,endpoint=False):
        x=16*math.sin(t)**3;y=13*math.cos(t)-5*math.cos(2*t)-2*math.cos(3*t)-math.cos(4*t);pts.append((c[0]+x/17*s,c[1]+(y+2)/17*s))
    return [(pts,1)]
def _house(c,s=.22):
    x,y=c
    return [([(x-s*.70,y-s),(x+s*.70,y-s),(x+s*.70,y+s*.10),(x+s*.92,y+s*.10),(x,y+s),(x-s*.92,y+s*.10),(x-s*.70,y+s*.10)],1),
            ([(x+s*.32,y+s*.40),(x+s*.54,y+s*.40),(x+s*.54,y+s*.92),(x+s*.32,y+s*.92)],1)]
DUAL={'id':'Home','title':'H O M E','button':'HOME','lamps':[0,1],'solution':(0,0),'start':(135,-40),'seed':5,
  'riddle':'An open book, a cushion, a story book, a slipper, a candle and the house keys. Two lamps, one pose, two pictures.',
  'hint':'Both walls must be right at once. One wall wants something you love, the other the place you keep it.',
  'complete':'HOME\nHome is where the heart is.','accent':[.95,.45,.45],
  'targets':(_heart,_house),
  # Chunky pictures overlap random blobs well (random poses reach IoU .79); .88 also keeps the reveal from looking skewed.
  'iou_min':.88,
  # The roof is placed by hand: a book left open spine-up is a roof to the right lamp and a block inside the heart's
  # lobes to the left one. The optimiser fills the rest around it.
  'anchors':[(0,1,0,.115,1.6)],
  'palette':(open_book,cushion,story_book,slipper,candle,house_keys)}
LEVELS.append(DUAL)

def tuck_clutter(palette,targets,placed_world,yaw,pitch,seed=1,tries=1500):
    """Leftover palette objects go fully inside the hull and inside shadow that already exists, so they add
    nothing to either picture but make the pile look like a pile."""
    rng=np.random.default_rng(seed);hull=shadow_hull(targets);used={i for i,_,_ in placed_world};out=[]
    pieces=[(palette[i][0].arrays(),R,t) for i,R,t in placed_world]
    current=[]
    for k in range(len(targets)):
        m=np.zeros_like(targets[k])
        for (v,tri),R,t in pieces:
            xy,ok=project(v@R.T+t,LAMPS[k],WALLS[k]);x=np.floor(xy[:,0]).astype(int);y=np.floor(xy[:,1]).astype(int)
            inside=ok&(x>=0)&(x<WW)&(y>=0)&(y<WH);m[y[inside],x[inside]]=True
        current.append(dilate(m,1))
    for i,(mesh,label) in enumerate(palette):
        if i in used:continue
        P0=surface_points(*mesh.arrays(),seed=i);best=None
        for _ in range(tries):
            R=rot(rng.uniform(-180,180,3));t=hull[int(rng.integers(len(hull)))];P=P0@R.T+t;bad=0
            for k in range(len(targets)):
                xy,ok=project(P,LAMPS[k],WALLS[k]);x=np.floor(xy[:,0]).astype(int);y=np.floor(xy[:,1]).astype(int)
                inside=ok&(x>=0)&(x<WW)&(y>=0)&(y<WH);bad+=(~inside).sum()+(~current[k][y[inside],x[inside]]).sum()
            if best is None or bad<best[0]:best=(bad,R,t)
            if bad==0:break
        if best and best[0]==0:out.append((i,best[1],best[2]));print(f"   tuck {label}")
        else:print(f"   ! no hiding place for {label}")
    return out

def resolve_dual(level,seeds=None):
    c=[wall_point(HOME,k) for k in (0,1)];targets=[polygon_mask(f(c[k]),k)&lit_mask(k) for k,f in enumerate(level['targets'])]
    palette=[(f(),f().name) for f in level['palette']];best=None
    anchors=[]
    for i,k,u,v,mag in level.get('anchors',()):
        w=WALLS[k];q=w['origin']+(c[k][0]+u)*w['u']+(c[k][1]+v)*UP;anchors.append((i,LAMPS[k]+(q-LAMPS[k])/mag,view_frame(k)))
    for seed in (seeds or [level['seed']]):
        placed=fill_hull(palette,targets,*level['solution'],seed=seed,edge_weight=3,candidates=2000,passes=3,anchors=anchors)
        pieces=[(palette[i][0].arrays(),pos,eul) for i,_,pos,eul in placed]
        ious=[(s&t).sum()/(s|t).sum() for s,t in ((shadow(pieces,k,*level['solution']),targets[k]) for k in (0,1))]
        print(f"   seed {seed}: iou "+" ".join(f"{x:.2f}" for x in ious))
        if best is None or min(ious)>best[0]:best=(min(ious),seed,placed)
    _,seed,placed=best;level['seed']=seed
    # Re-express in world space at the solution pose to tuck the leftovers, then back to the pile frame.
    R0=pose_matrix(*level['solution']);world=[(i,R0@rot(eul),R0@np.array(pos)+HOME) for i,_,pos,eul in placed]
    for i,R,t in tuck_clutter(palette,targets,world,*level['solution']):placed.append((i,palette[i][1],((t-HOME)@R0).round(4).tolist(),euler_of(R0.T@R)))
    return [(level['palette'][i],label,pos,eul) for i,label,pos,eul in placed],targets

# ---------------------------------------------------------------- build
SAMPLES=1000

def build(check=False,seeds=None):
    LOGS.mkdir(parents=True,exist_ok=True);rng=np.random.default_rng(5)
    data={'version':1,'room':{'walls':[{'origin':w['origin'].round(5).tolist(),'u':w['u'].round(6).tolist(),'n':w['n'].round(6).tolist()} for w in WALLS],
          'lamps':[l.round(5).tolist() for l in LAMPS],'home':HOME.tolist(),'width':WW,'height':WH,'pixel':PX,'halfAngle':HALF_ANGLE},
          'compare':{'size':N,'radius':R0,'tolerance':TOL,'cover':COVER_MIN,'spill':SPILL_MAX,'iou':IOU_MIN,'tilts':list(TILTS)},
          'materials':[{'name':k,'color':list(c[:3]),'smoothness':c[3]} for k,c in MATERIALS.items()],'objects':[],'levels':[]}
    names=set()
    for level in LEVELS:
        resolved,drawn=resolve_dual(level,seeds) if len(level['lamps'])>1 else (resolve_single(level),None)
        pieces=[];entry={k:level[k] for k in('id','title','button','riddle','hint','complete','accent','lamps')}
        entry['iou_min']=level.get('iou_min',IOU_MIN);iou_min=entry['iou_min']
        entry['solution']=list(level['solution']);entry['start']=list(level['start']);entry['pieces']=[]
        for make,label,pos,eul in resolved:
            m=make();pieces.append((m.arrays(),pos,eul));entry['pieces'].append({'object':m.name,'label':label,'position':pos,'euler':eul})
            if np.linalg.norm(pos)>.24:print(f"  ! {label} far from the pivot: {np.linalg.norm(pos):.3f}")
            if m.name not in names:
                names.add(m.name);data['objects'].append({'name':m.name,'vertices':[round(c,5) for c in np.array(m.v).flatten().tolist()],
                    'submeshes':[{'material':k,'triangles':[i for t in ts for i in t]} for k,ts in m.sub.items()]})
        data['levels'].append(entry);y0,p0=level['solution'];A=pose_matrix(y0,p0)
        sols=[shadow(pieces,k,0,0,R=A) for k in level['lamps']];targets=[[normalise(s,t) for t in TILTS] for s in sols]
        def weakest(R,pos=HOME):
            # Weakest wall's IoU among walls that meet cover and spill (0 if one does not), and whether all pass.
            iou,passes=1.0,True
            for ts,k in zip(targets,level['lamps']):
                c,s,i,_=best(ts,normalise(shadow(pieces,k,0,0,pos,R=R)),iou_min);shape=c>=COVER_MIN and s<=SPILL_MAX
                iou=min(iou,i if shape else 0);passes&=shape and i>=iou_min
            return iou,passes
        ok=lambda R,pos=HOME:weakest(R,pos)[1]
        axes=[('up',UP),('right',np.array([1.,0,0]))]+[(f'lamp{k} axis',HOME-LAMPS[k]) for k in level['lamps']]
        windows=[]
        for name,axis in axes:
            w=[d for d in range(-24,25,3) if ok(axis_matrix(axis,d)@A)];windows.append(f"{name} {w[0]}..{w[-1]}" if w else f"{name} -")
        moves=[tuple(d) for d in ((.06,0,0),(-.06,0,0),(0,.06,0),(0,-.06,0),(0,0,.06),(0,0,-.06)) if ok(A,HOME+np.array(d))]
        far=[];
        for _ in range(SAMPLES):
            R=random_rotation(rng)
            if angle_between(R,A)>30:far.append(weakest(R))
        hits=sum(p for _,p in far);top=sorted(i for i,_ in far)[-3:]
        print(f"{level['id']} (seed {level.get('seed','-')}, iou_min {iou_min}): windows {'; '.join(windows)}  6cm moves ok {len(moves)}/6  "
              f"random far orientations passing {hits}/{len(far)} (closest IoU with cover+spill met {', '.join(f'{t:.2f}' for t in top)})  start ok {ok(pose_matrix(*level['start']))}")
        sheet=Image.new('RGB',(WW*3*len(level['lamps'])*2,WH*3),(20,20,20))
        for j,k in enumerate(level['lamps']):
            sheet.paste(to_img(sols[j]),(j*WW*6,0));sheet.paste(to_img(shadow(pieces,k,*level['start'])),(j*WW*6+WW*3,0))
        sheet.resize((sheet.width//2,sheet.height//2)).save(LOGS/f"Torch2-{level['id']}.png")
    if not check:
        OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(data,separators=(',',':')))
        print('wrote',OUT.relative_to(ROOT),f'{OUT.stat().st_size/1024:.0f} KB')

if __name__=='__main__':
    seeds=[int(a) for a in sys.argv[sys.argv.index('--seeds')+1].split(',')] if '--seeds' in sys.argv else None
    build('--check' in sys.argv,seeds)
